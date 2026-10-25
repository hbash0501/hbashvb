using Basira.Platform.Data;

namespace Basira.Platform.Processing;

/// <summary>
/// مجلد الإدراج: أي ملف يحفظه السكانر أو الموظف في المجلد المحدد يُدرج تلقائياً ثم يُنقل إلى «Imported»،
/// وما يتعذر إدراجه إلى «Failed». بديل عملي عن التحكم المباشر بالسكانر يعمل مع أي جهاز يحفظ في مجلد.
/// </summary>
public sealed class HotFolderService : BackgroundService
{
    private readonly SettingsStore _settings;
    private readonly IServiceProvider _services;
    private readonly ILogger<HotFolderService> _log;
    private readonly Dictionary<string, long> _sizes = new();

    public HotFolderService(SettingsStore settings, IServiceProvider services, ILogger<HotFolderService> log)
    {
        _settings = settings; _services = services; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ScanAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogWarning(ex, "Inbox folder scan failed"); }
            await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);
        }
    }

    public async Task<int> ScanAsync(CancellationToken ct)
    {
        var folder = _settings.Get("inbox_folder");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return 0;
        var direction = _settings.Get("default_direction");
        var imported = 0;
        foreach (var file in Directory.GetFiles(folder))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith('.') || name.StartsWith('~') || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) continue;
            // انتظر حتى يثبت حجم الملف بين فحصين (اكتمال الكتابة من السكانر).
            var size = new FileInfo(file).Length;
            if (!_sizes.TryGetValue(file, out var previous) || previous != size) { _sizes[file] = size; continue; }
            _sizes.Remove(file);
            var inbox = _services.GetRequiredService<InboxService>();
            try
            {
                await using (var s = File.OpenRead(file))
                    await inbox.ImportAsync(s, name, new ImportOptions(Direction: direction), "مجلد الإدراج", ct);
                Move(file, Path.Combine(folder, "Imported", DateTime.Now.ToString("yyyy-MM-dd")));
                imported++;
            }
            catch (IOException) { /* الملف ما زال مفتوحاً؛ يُعاد في الفحص التالي. */ }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Inbox import failed for {File}", name);
                Move(file, Path.Combine(folder, "Failed"));
            }
        }
        return imported;
    }

    private static void Move(string file, string dir)
    {
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, Path.GetFileName(file));
        if (File.Exists(target)) target = Path.Combine(dir, Path.GetFileNameWithoutExtension(file) + "-" + DateTime.Now.ToString("HHmmssfff") + Path.GetExtension(file));
        File.Move(file, target);
    }
}
