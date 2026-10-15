using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Basira.Web.Services;

public sealed record SessionClaims(string UserName, long ExpiresUnix);

/// <summary>رموز جلسة موقَّعة (HMAC-SHA256) بصلاحية محددة؛ لا حالة مخزنة على الخادم.</summary>
public sealed class SessionTokens
{
    private readonly byte[] _key;
    private readonly TimeProvider _time;
    private readonly TimeSpan _lifetime;

    public SessionTokens(PortalOptions options, TimeProvider time)
    {
        _key = options.SessionKey;
        _time = time;
        _lifetime = options.SessionLifetime;
    }

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? FromB64(string s)
    {
        try
        {
            s = s.Replace('-', '+').Replace('_', '/');
            s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
            return Convert.FromBase64String(s);
        }
        catch (FormatException) { return null; }
    }

    public string Issue(string userName)
    {
        var claims = new SessionClaims(userName, _time.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds());
        var payload = B64(JsonSerializer.SerializeToUtf8Bytes(claims));
        return payload + "." + B64(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(payload)));
    }

    public SessionClaims? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 1024) return null;
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return null;
        var payload = token[..dot];
        var sig = FromB64(token[(dot + 1)..]);
        if (sig is null) return null;
        var expected = HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(payload));
        if (!CryptographicOperations.FixedTimeEquals(sig, expected)) return null;
        var raw = FromB64(payload);
        if (raw is null) return null;
        try
        {
            var claims = JsonSerializer.Deserialize<SessionClaims>(raw);
            if (claims is null || string.IsNullOrWhiteSpace(claims.UserName)) return null;
            return claims.ExpiresUnix > _time.GetUtcNow().ToUnixTimeSeconds() ? claims : null;
        }
        catch (JsonException) { return null; }
    }
}
