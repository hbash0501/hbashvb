using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Basira.Platform.Infrastructure;

public sealed record SessionClaims(string UserName, string Stamp, long ExpiresUnix);

/// <summary>رموز جلسة موقَّعة HMAC-SHA256 تحمل «ختم الأمان» للمستخدم؛ تغيير كلمة المرور أو الإيقاف يبطل الجلسات فوراً.</summary>
public sealed class SessionTokens
{
    private readonly byte[] _key;
    private readonly TimeProvider _time;
    private readonly TimeSpan _lifetime;

    public SessionTokens(PlatformOptions options, TimeProvider time) { _key = options.SessionKey; _time = time; _lifetime = options.SessionLifetime; }

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[]? FromB64(string s)
    {
        try { s = s.Replace('-', '+').Replace('_', '/'); return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=')); }
        catch (FormatException) { return null; }
    }

    public string Issue(string userName, string stamp)
    {
        var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new SessionClaims(userName, stamp, _time.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds())));
        return payload + "." + B64(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(payload)));
    }

    public SessionClaims? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 1024) return null;
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return null;
        var payload = token[..dot];
        var sig = FromB64(token[(dot + 1)..]);
        if (sig is null || !CryptographicOperations.FixedTimeEquals(sig, HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(payload)))) return null;
        var raw = FromB64(payload);
        if (raw is null) return null;
        try
        {
            var c = JsonSerializer.Deserialize<SessionClaims>(raw);
            return c is not null && c.UserName != "" && c.ExpiresUnix > _time.GetUtcNow().ToUnixTimeSeconds() ? c : null;
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>قفل مؤقت بعد تكرار إخفاق الدخول لكل (مستخدم + عنوان).</summary>
public sealed class LoginThrottle
{
    private sealed class State { public int Failures; public DateTimeOffset WindowStart; public DateTimeOffset LockedUntil; }
    private readonly ConcurrentDictionary<string, State> _states = new();
    private readonly int _max;
    private readonly TimeSpan _window;
    private readonly TimeProvider _time;

    public LoginThrottle(PlatformOptions o, TimeProvider time) { _max = o.MaxFailedLogins; _window = o.LockoutWindow; _time = time; }
    private static string Key(string user, string ip) => user.Trim().ToLowerInvariant() + "|" + ip;

    public TimeSpan? LockedFor(string user, string ip)
    {
        if (!_states.TryGetValue(Key(user, ip), out var s)) return null;
        lock (s) { var now = _time.GetUtcNow(); return s.LockedUntil > now ? s.LockedUntil - now : null; }
    }

    public void Failure(string user, string ip)
    {
        var s = _states.GetOrAdd(Key(user, ip), _ => new State());
        lock (s)
        {
            var now = _time.GetUtcNow();
            if (s.Failures == 0 || now - s.WindowStart > _window) { s.Failures = 0; s.WindowStart = now; }
            if (++s.Failures >= _max) { s.LockedUntil = now + _window; s.Failures = 0; }
        }
    }

    public void Success(string user, string ip) => _states.TryRemove(Key(user, ip), out _);
}
