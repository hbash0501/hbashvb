using System.Security.Cryptography;

namespace Basira.Platform.Infrastructure;

/// <summary>إعدادات التشغيل: من التهيئة أو متغيرات البيئة، بقيم افتراضية تناسب جهازاً واحداً.</summary>
public sealed class PlatformOptions
{
    public string DataFolder { get; init; } = "";
    public byte[] SessionKey { get; init; } = Array.Empty<byte>();
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromHours(10);
    public int MaxFailedLogins { get; init; } = 5;
    public TimeSpan LockoutWindow { get; init; } = TimeSpan.FromMinutes(15);
    public bool ForceSecureCookies { get; init; }
    public int Workers { get; init; } = 2;
    public long MaxUploadBytes { get; init; } = 200L * 1024 * 1024;

    public string FilesFolder => Path.Combine(DataFolder, "files");
    public string DatabasePath => Path.Combine(DataFolder, "basira.db");

    public static string DefaultDataFolder() =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Basira")
            : Path.Combine(AppContext.BaseDirectory, "data");

    public static PlatformOptions From(IConfiguration c)
    {
        string? Get(string key, string env) => c[key] ?? Environment.GetEnvironmentVariable(env);
        var data = Path.GetFullPath(Get("Basira:DataFolder", "BASIRA_DATA") ?? DefaultDataFolder());
        Directory.CreateDirectory(data);
        var secret = Get("Basira:SessionSecret", "BASIRA_SESSION_SECRET");
        byte[] key;
        if (!string.IsNullOrWhiteSpace(secret)) key = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
        else
        {
            // سر محفوظ في مجلد البيانات حتى لا تنتهي الجلسات عند إعادة التشغيل.
            var keyFile = Path.Combine(data, "session.key");
            if (File.Exists(keyFile) && new FileInfo(keyFile).Length == 32) key = File.ReadAllBytes(keyFile);
            else { key = RandomNumberGenerator.GetBytes(32); File.WriteAllBytes(keyFile, key); }
        }
        int Int(string k, string e, int def, int min, int max) => Math.Clamp(int.TryParse(Get(k, e), out var v) ? v : def, min, max);
        return new PlatformOptions
        {
            DataFolder = data,
            SessionKey = key,
            SessionLifetime = TimeSpan.FromHours(Int("Basira:SessionHours", "BASIRA_SESSION_HOURS", 10, 1, 72)),
            MaxFailedLogins = Int("Basira:MaxFailedLogins", "BASIRA_MAX_FAILED_LOGINS", 5, 3, 20),
            ForceSecureCookies = string.Equals(Get("Basira:SecureCookies", "BASIRA_SECURE_COOKIES"), "true", StringComparison.OrdinalIgnoreCase),
            Workers = Int("Basira:Workers", "BASIRA_WORKERS", Math.Clamp(Environment.ProcessorCount / 2, 1, 4), 1, 16),
        };
    }
}
