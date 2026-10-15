using System.Security.Cryptography;

namespace Basira.Web.Services;

/// <summary>إعدادات البوابة، تُقرأ من التهيئة أو متغيرات البيئة.</summary>
public sealed class PortalOptions
{
    public string DataFolder { get; init; } = "";
    public string ArchiveRootFolder { get; init; } = "";
    public byte[] SessionKey { get; init; } = Array.Empty<byte>();
    public bool SessionKeyIsEphemeral { get; init; }
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromHours(8);
    public int MaxFailedLogins { get; init; } = 5;
    public TimeSpan LockoutWindow { get; init; } = TimeSpan.FromMinutes(15);
    public bool ForceSecureCookies { get; init; }

    public static PortalOptions From(IConfiguration c)
    {
        string? Get(string key, string env) => c[key] ?? Environment.GetEnvironmentVariable(env);
        var data = Get("Basira:DataFolder", "BASIRA_DATA") ?? Path.Combine(AppContext.BaseDirectory, "data");
        var root = Get("Basira:ArchiveRootFolder", "BASIRA_ARCHIVE_ROOT") ?? Path.Combine(data, "ArchiveFiles");
        var secret = Get("Basira:SessionSecret", "BASIRA_SESSION_SECRET");
        var ephemeral = string.IsNullOrWhiteSpace(secret);
        var key = ephemeral ? RandomNumberGenerator.GetBytes(32) : SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret!));
        return new PortalOptions
        {
            DataFolder = Path.GetFullPath(data),
            ArchiveRootFolder = Path.GetFullPath(root),
            SessionKey = key,
            SessionKeyIsEphemeral = ephemeral,
            SessionLifetime = TimeSpan.FromHours(Math.Clamp(int.TryParse(Get("Basira:SessionHours", "BASIRA_SESSION_HOURS"), out var h) ? h : 8, 1, 72)),
            MaxFailedLogins = Math.Clamp(int.TryParse(Get("Basira:MaxFailedLogins", "BASIRA_MAX_FAILED_LOGINS"), out var m) ? m : 5, 3, 20),
            ForceSecureCookies = string.Equals(Get("Basira:SecureCookies", "BASIRA_SECURE_COOKIES"), "true", StringComparison.OrdinalIgnoreCase),
        };
    }
}
