using Basira.Platform.Infrastructure;

namespace Basira.Platform.Data;

/// <summary>إعدادات المنصة القابلة للتعديل من الإدارة.</summary>
public sealed class SettingsStore
{
    private readonly Db _db;
    public SettingsStore(Db db) => _db = db;

    public static readonly Dictionary<string, string> Defaults = new()
    {
        ["organization"] = "",
        ["tesseract_path"] = "",
        ["pdftoppm_path"] = "",
        ["ocr_languages"] = "ara+eng",
        ["inbox_folder"] = "",
        ["reply_days"] = "7",
        ["default_direction"] = "وارد",
        ["semantic_model_path"] = "",
        ["semantic_model_url"] = "",
    };

    public Dictionary<string, string> All()
    {
        using var c = _db.Open();
        var stored = c.Query("SELECT key, value FROM settings", r => (r.GetString(0), r.GetString(1))).ToDictionary(x => x.Item1, x => x.Item2);
        return Defaults.ToDictionary(kv => kv.Key, kv => stored.TryGetValue(kv.Key, out var v) ? v : kv.Value);
    }

    public string Get(string key) => All().TryGetValue(key, out var v) ? v : "";

    public int GetInt(string key, int fallback, int min, int max) => int.TryParse(Get(key), out var v) ? Math.Clamp(v, min, max) : fallback;

    public void Set(IDictionary<string, string> values, string actor)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        foreach (var (k, v) in values.Where(kv => Defaults.ContainsKey(kv.Key)))
            c.Exec("INSERT INTO settings(key, value) VALUES(@k,@v) ON CONFLICT(key) DO UPDATE SET value=@v", new { k, v = v.Trim() }, tx);
        AuditStore.Append(c, tx, actor, "تعديل الإعدادات", string.Join("، ", values.Keys.Where(Defaults.ContainsKey)));
        tx.Commit();
    }

    /// <summary>قيمة مخزنة خارج الإعدادات المعروضة (مثل شجرة التصنيف).</summary>
    public string GetRaw(string key)
    {
        using var c = _db.Open();
        return c.Scalar("SELECT value FROM settings WHERE key=@key", new { key }) as string ?? "";
    }

    public void SetRaw(string key, string value, string actor, string auditAction)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        c.Exec("INSERT INTO settings(key, value) VALUES(@key,@value) ON CONFLICT(key) DO UPDATE SET value=@value", new { key, value }, tx);
        AuditStore.Append(c, tx, actor, auditAction, key);
        tx.Commit();
    }
}
