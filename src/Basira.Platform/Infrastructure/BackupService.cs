using System.IO.Compression;
using System.Text.Json;
using Basira.Platform.Data;
using Microsoft.Data.Sqlite;

namespace Basira.Platform.Infrastructure;

/// <summary>
/// النسخ الاحتياطي: ملف ZIP فيه لقطة متسقة من قاعدة البيانات (نسخ SQLite المباشر دون إيقاف المنصة) وكل الملفات الأصلية وبيان بمحتواه.
/// لا يتضمن session.key: النسخة لا تحمل مفتاح فك أسرار الإعدادات، فمفاتيح الخدمات السحابية تُدخل من جديد إن استُعيدت على جهاز آخر.
/// الاستعادة تُجهَّز ثم تُطبَّق عند التشغيل التالي قبل فتح قاعدة البيانات، وتُحفظ البيانات السابقة جانباً.
/// </summary>
public sealed class BackupService
{
    public const string Prefix = "Basira_backup_";
    private const string PendingName = "restore-pending.zip";
    private readonly PlatformOptions _options;
    private readonly Db _db;
    private readonly SettingsStore _settings;
    private readonly AuditStore _audit;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(PlatformOptions options, Db db, SettingsStore settings, AuditStore audit)
    {
        _options = options; _db = db; _settings = settings; _audit = audit;
    }

    public sealed record Manifest(string App, string Version, string CreatedAt, int Documents, int Files, long Bytes);

    public async Task WriteAsync(Stream output, string actor, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        var temp = Path.Combine(_options.DataFolder, "backup-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var src = _db.Open())
            using (var dst = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false }.ToString()))
            {
                dst.Open();
                src.BackupDatabase(dst);
            }
            int documents;
            using (var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            {
                c.Open();
                documents = Convert.ToInt32(c.Scalar("SELECT COUNT(*) FROM documents"));
            }
            var files = Directory.Exists(_options.FilesFolder)
                ? Directory.EnumerateFiles(_options.FilesFolder, "*", SearchOption.AllDirectories).Where(f => !Path.GetFileName(f).StartsWith("tmp-", StringComparison.Ordinal)).ToList()
                : new List<string>();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                long bytes = 0;
                await AddAsync(zip, temp, "basira.db", CompressionLevel.Fastest, ct);
                foreach (var f in files)
                {
                    ct.ThrowIfCancellationRequested();
                    // الملفات الأصلية (PDF وصور) مضغوطة أصلاً: تُخزَّن كما هي.
                    await AddAsync(zip, f, "files/" + Path.GetRelativePath(_options.FilesFolder, f).Replace('\\', '/'), CompressionLevel.NoCompression, ct);
                    bytes += new FileInfo(f).Length;
                }
                var manifest = new Manifest("Basira", Api.AuthApi.AppVersion, DateTime.UtcNow.ToString("O"), documents, files.Count, bytes);
                await using var m = zip.CreateEntry("manifest.json").Open();
                await JsonSerializer.SerializeAsync(m, manifest, cancellationToken: ct);
            }
            _audit.Append(actor, "نسخة احتياطية", $"{documents} مستند، {files.Count} ملف");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(temp);
            _gate.Release();
        }
    }

    private static async Task AddAsync(ZipArchive zip, string path, string name, CompressionLevel level, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, level);
        entry.LastWriteTime = File.GetLastWriteTime(path);
        await using var input = File.OpenRead(path);
        await using var output = entry.Open();
        await input.CopyToAsync(output, ct);
    }

    /// <summary>نسخة في مجلد النسخ الاحتياطية، مع حذف ما زاد على العدد المحفوظ.</summary>
    public async Task<string> CreateInFolderAsync(string folder, int keep, string actor, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var name = $"{Prefix}{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
        var path = Path.Combine(folder, name);
        var partial = path + ".part";
        try
        {
            await using (var fs = File.Create(partial)) await WriteAsync(fs, actor, ct);
            File.Move(partial, path, overwrite: true);
        }
        catch { TryDelete(partial); throw; }
        foreach (var old in Directory.GetFiles(folder, Prefix + "*.zip").OrderByDescending(f => f, StringComparer.Ordinal).Skip(Math.Max(1, keep)))
            TryDelete(old);
        _settings.SetRaw("backup_last", $"{DateTime.UtcNow:O}|{path}|{new FileInfo(path).Length}", actor, "نسخة احتياطية في المجلد");
        if (_settings.GetRaw("backup_error") != "") _settings.SetRaw("backup_error", "", actor, "نجح النسخ الاحتياطي بعد تعذره");
        return path;
    }

    /// <summary>فحص ملف نسخة: من بصيرة، وفيه قاعدة بيانات سليمة بجداولها. يعيد بيانه.</summary>
    public static Manifest Inspect(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var m = zip.GetEntry("manifest.json") ?? throw new InvalidDataException("الملف ليس نسخة احتياطية من بصيرة.");
        Manifest manifest;
        using (var s = m.Open()) manifest = JsonSerializer.Deserialize<Manifest>(s) ?? throw new InvalidDataException("بيان النسخة تالف.");
        if (manifest.App != "Basira") throw new InvalidDataException("الملف ليس نسخة احتياطية من بصيرة.");
        var dbEntry = zip.GetEntry("basira.db") ?? throw new InvalidDataException("النسخة لا تحوي قاعدة البيانات.");
        var temp = Path.Combine(Path.GetTempPath(), "basira-check-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            dbEntry.ExtractToFile(temp);
            using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false, Mode = SqliteOpenMode.ReadOnly }.ToString());
            c.Open();
            if (c.Scalar("PRAGMA integrity_check") as string != "ok") throw new InvalidDataException("قاعدة البيانات في النسخة تالفة.");
            if (Convert.ToInt32(c.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('documents','users','audit')")) != 3)
                throw new InvalidDataException("قاعدة البيانات في النسخة ليست من بصيرة.");
        }
        catch (SqliteException ex) { throw new InvalidDataException("تعذر فتح قاعدة البيانات في النسخة: " + ex.Message); }
        finally { SqliteConnection.ClearAllPools(); TryDelete(temp); }
        var files = zip.Entries.Count(e => e.FullName.StartsWith("files/", StringComparison.Ordinal) && e.Length > 0);
        if (files != manifest.Files) throw new InvalidDataException($"النسخة ناقصة: فيها {files} ملف من {manifest.Files}.");
        return manifest;
    }

    /// <summary>تجهيز الاستعادة: تُحفظ النسخة بعد فحصها، وتُطبَّق عند التشغيل التالي.</summary>
    public async Task<Manifest> StageAsync(Stream upload, string actor, CancellationToken ct)
    {
        var pending = Path.Combine(_options.DataFolder, PendingName);
        var temp = pending + ".part";
        try
        {
            await using (var fs = File.Create(temp)) await upload.CopyToAsync(fs, ct);
            var manifest = Inspect(temp);
            File.Move(temp, pending, overwrite: true);
            _audit.Append(actor, "تجهيز استعادة نسخة احتياطية", $"نسخة {manifest.CreatedAt[..10]}: {manifest.Documents} مستند؛ تُطبَّق عند إعادة التشغيل");
            return manifest;
        }
        catch (InvalidDataException) { TryDelete(temp); throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { TryDelete(temp); throw new InvalidDataException("تعذر حفظ النسخة: " + ex.Message); }
    }

    public Manifest? Pending()
    {
        var pending = Path.Combine(_options.DataFolder, PendingName);
        try { return File.Exists(pending) ? Inspect(pending) : null; }
        catch (InvalidDataException) { return null; }
    }

    public void CancelPending(string actor)
    {
        var pending = Path.Combine(_options.DataFolder, PendingName);
        if (File.Exists(pending)) { TryDelete(pending); _audit.Append(actor, "إلغاء استعادة نسخة احتياطية", ""); }
    }

    /// <summary>
    /// عند التشغيل وقبل فتح قاعدة البيانات: إن وُجدت استعادة مجهزة تُنقل البيانات الحالية إلى مجلد «before-restore» ثم تُستخرج النسخة.
    /// أي خطأ يعيد البيانات كما كانت. يعيد وصف ما حدث، أو null إن لم توجد استعادة.
    /// </summary>
    /// <summary>نتيجة آخر استعادة طُبّقت عند هذا التشغيل (تُعرض في الإدارة).</summary>
    public static string? RestoredMessage { get; set; }

    public static string? ApplyPending(PlatformOptions o)
    {
        var pending = Path.Combine(o.DataFolder, PendingName);
        if (!File.Exists(pending)) return null;
        var aside = Path.Combine(o.DataFolder, "before-restore-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
        var moved = new List<(string From, string To)>();
        try
        {
            var manifest = Inspect(pending);
            Directory.CreateDirectory(aside);
            foreach (var name in new[] { "basira.db", "basira.db-wal", "basira.db-shm", "files" })
            {
                var from = Path.Combine(o.DataFolder, name);
                var to = Path.Combine(aside, name);
                if (File.Exists(from)) { File.Move(from, to); moved.Add((from, to)); }
                else if (Directory.Exists(from)) { Directory.Move(from, to); moved.Add((from, to)); }
            }
            using (var zip = ZipFile.OpenRead(pending))
                foreach (var e in zip.Entries)
                {
                    if (e.FullName != "basira.db" && !e.FullName.StartsWith("files/", StringComparison.Ordinal)) continue;
                    var target = Path.GetFullPath(Path.Combine(o.DataFolder, e.FullName));
                    if (!target.StartsWith(Path.GetFullPath(o.DataFolder) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue; // مسار يخرج من مجلد البيانات
                    if (e.FullName.EndsWith('/')) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    e.ExtractToFile(target, overwrite: true);
                }
            File.Delete(pending);
            return $"استُعيدت نسخة {manifest.CreatedAt[..10]} ({manifest.Documents} مستند). البيانات السابقة في {aside}";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // التراجع: حذف ما استُخرج وإرجاع البيانات السابقة.
            foreach (var name in new[] { "basira.db", "files" })
            {
                var p = Path.Combine(o.DataFolder, name);
                if (moved.Any(m => m.From == p)) { if (File.Exists(p)) File.Delete(p); else if (Directory.Exists(p)) Directory.Delete(p, true); }
            }
            foreach (var (from, to) in moved) { if (File.Exists(to)) File.Move(to, from); else if (Directory.Exists(to)) Directory.Move(to, from); }
            File.Move(pending, pending + ".failed", overwrite: true);
            return "تعذرت الاستعادة وبقيت البيانات كما كانت: " + ex.Message;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>نسخة احتياطية يومية تلقائية إلى المجلد المحدد في الإعدادات.</summary>
public sealed class BackupScheduler : BackgroundService
{
    private readonly BackupService _backup;
    private readonly SettingsStore _settings;
    private readonly ILogger<BackupScheduler> _log;

    public BackupScheduler(BackupService backup, SettingsStore settings, ILogger<BackupScheduler> log) { _backup = backup; _settings = settings; _log = log; }

    public static DateTime? Last(SettingsStore s) =>
        DateTime.TryParse(s.GetRaw("backup_last").Split('|')[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t : null;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), ct); } catch (TaskCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            var folder = _settings.Get("backup_folder").Trim();
            // بعد تعذر النسخ يُعاد المحاولة كل 6 ساعات لا كل نصف ساعة، فلا يمتلئ سجل التدقيق بالخطأ نفسه.
            var failedRecently = DateTime.TryParse(_settings.GetRaw("backup_error").Split('|')[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var failedAt) && DateTime.UtcNow - failedAt < TimeSpan.FromHours(6);
            if (folder != "" && !failedRecently && (Last(_settings) is not { } last || DateTime.UtcNow - last > TimeSpan.FromHours(24)))
            {
                try { await _backup.CreateInFolderAsync(folder, _settings.GetInt("backup_keep", 14, 1, 365), "النسخ التلقائي", ct); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
                {
                    _log.LogWarning(ex, "Automatic backup failed");
                    _settings.SetRaw("backup_error", $"{DateTime.UtcNow:O}|{ex.Message}", "النسخ التلقائي", "تعذر النسخ الاحتياطي التلقائي");
                }
            }
            try { await Task.Delay(TimeSpan.FromMinutes(30), ct); } catch (TaskCanceledException) { return; }
        }
    }
}
