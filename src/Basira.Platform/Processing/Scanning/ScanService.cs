using System.Collections.Concurrent;
using Basira.Platform.Data;

namespace Basira.Platform.Processing.Scanning;

/// <summary>مهمة مسح: صفحاتها بانتظار المراجعة (تدوير، حذف، ترتيب) ثم الإدراج.</summary>
public sealed class ScanJob
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public required string DeviceId { get; init; }
    public required string Owner { get; init; }
    public string State { get; set; } = "scanning"; // scanning | ready | failed
    public string Message { get; set; } = "";
    public int Scanned { get; set; }
    public List<ScanPage> Pages { get; } = new();
    public DateTime Touched { get; set; } = DateTime.UtcNow;
    public object Lock { get; } = new();
}

/// <summary>
/// المسح الضوئي على الجهاز الذي يعمل عليه البرنامج: ماسحات Windows (WIA) والماسحات الشبكية (eSCL) التي يضيفها المدير بعنوانها.
/// المسح في الخلفية، والصفحات تبقى في الذاكرة حتى تُدرج أو تُلغى (وتُنسى بعد ساعتين).
/// </summary>
public sealed class ScanService
{
    private const string EsclKey = "escl_scanners";
    private readonly SettingsStore _settings;
    private readonly InboxService _inbox;
    private readonly ConcurrentDictionary<string, ScanJob> _jobs = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    private readonly ConcurrentDictionary<string, (DateTime At, ScannerCaps Caps)> _caps = new();
    public const int MaxPagesPerJob = 300;

    public ScanService(SettingsStore settings, InboxService inbox) { _settings = settings; _inbox = inbox; }

    // ===== الأجهزة =====

    public List<string> EsclAddresses() => _settings.GetRaw(EsclKey).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public async Task<(List<ScannerDevice> Devices, string Note)> DevicesAsync(CancellationToken ct)
    {
        var list = new List<ScannerDevice>();
        var note = "";
        if (OperatingSystem.IsWindows())
        {
            if (WiaScanner.Available)
                try { list.AddRange(await Task.Run(WiaScanner.List, ct)); }
                catch (ScannerException ex) { note = ex.Message; }
            else note = "مكتبة WIA غير متوفرة في هذا الإصدار من Windows.";
        }
        foreach (var root in EsclAddresses())
        {
            var id = "escl:" + root;
            var name = _caps.TryGetValue(id, out var c) ? c.Caps.Model : new Uri(root).Host;
            list.Add(new ScannerDevice(id, name, "escl", root));
        }
        return (list, note);
    }

    public async Task<ScannerDevice> AddEsclAsync(string address, string actor, CancellationToken ct)
    {
        var root = EsclScanner.Root(address);
        var (caps, _) = await EsclScanner.CapabilitiesAsync(root, ct);
        var all = EsclAddresses();
        if (!all.Contains(root)) all.Add(root);
        _settings.SetRaw(EsclKey, string.Join("\n", all), actor, "إضافة ماسح شبكي");
        _caps["escl:" + root] = (DateTime.UtcNow, caps);
        return new ScannerDevice("escl:" + root, caps.Model, "escl", root);
    }

    public void RemoveEscl(string deviceId, string actor)
    {
        var root = deviceId.StartsWith("escl:", StringComparison.Ordinal) ? deviceId[5..] : deviceId;
        var all = EsclAddresses();
        if (all.Remove(root)) _settings.SetRaw(EsclKey, string.Join("\n", all), actor, "حذف ماسح شبكي");
        _caps.TryRemove(deviceId, out _);
    }

    private string Known(string deviceId)
    {
        if (deviceId.StartsWith("escl:", StringComparison.Ordinal))
        {
            var root = deviceId[5..];
            return EsclAddresses().Contains(root) ? root : throw new ScannerException("الماسح غير مضاف.");
        }
        if (deviceId.StartsWith("wia:", StringComparison.Ordinal) && OperatingSystem.IsWindows()) return deviceId[4..];
        throw new ScannerException("الماسح غير معروف.");
    }

    public async Task<ScannerCaps> CapabilitiesAsync(string deviceId, CancellationToken ct, bool fresh = false)
    {
        if (!fresh && _caps.TryGetValue(deviceId, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromMinutes(5)) return c.Caps;
        var target = Known(deviceId);
        ScannerCaps caps;
        if (deviceId.StartsWith("escl:", StringComparison.Ordinal)) caps = (await EsclScanner.CapabilitiesAsync(target, ct)).Caps;
        else if (OperatingSystem.IsWindows()) caps = await Task.Run(() => OperatingSystem.IsWindows() ? WiaScanner.Capabilities(target) : throw new PlatformNotSupportedException(), ct);
        else throw new ScannerException("الماسح غير معروف.");
        _caps[deviceId] = (DateTime.UtcNow, caps);
        return caps;
    }

    private async Task<List<ScannedImage>> AcquireAsync(string deviceId, ScanSettings s, ScannerCaps caps, Action<int> onPage, CancellationToken ct)
    {
        var target = Known(deviceId);
        var gate = _gates.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(TimeSpan.FromSeconds(2), ct)) throw new ScannerException("الماسح مشغول بمسح آخر من هذه المنصة.");
        try
        {
            if (deviceId.StartsWith("escl:", StringComparison.Ordinal)) return await EsclScanner.ScanAsync(target, s, onPage, ct);
            if (OperatingSystem.IsWindows()) return await Task.Run(() => OperatingSystem.IsWindows() ? WiaScanner.Scan(target, s, caps, onPage, ct) : throw new PlatformNotSupportedException(), ct);
            throw new ScannerException("الماسح غير معروف.");
        }
        finally { gate.Release(); }
    }

    // ===== المعاينة =====

    /// <summary>معاينة سريعة للزجاج كاملاً بدقة منخفضة لاختيار منطقة المسح.</summary>
    public async Task<(byte[] Jpeg, double WidthMm, double HeightMm)> PreviewAsync(string deviceId, CancellationToken ct)
    {
        var caps = await CapabilitiesAsync(deviceId, ct);
        if (!caps.Sources.Contains("flatbed")) throw new ScannerException("المعاينة للماسح ذي الزجاج فقط.");
        var dpi = caps.Resolutions.Where(r => r >= 75).DefaultIfEmpty(caps.Resolutions.Min()).Min();
        var s = new ScanSettings("flatbed", "gray", dpi, "max");
        var images = await AcquireAsync(deviceId, s, caps, _ => { }, ct);
        var page = PageTools.Prepare(images[0], "gray", 0, 0, false);
        return (page.Data, caps.MaxWidthMm, caps.MaxHeightMm);
    }

    // ===== المهام =====

    private void Sweep()
    {
        foreach (var (id, job) in _jobs)
            if (DateTime.UtcNow - job.Touched > TimeSpan.FromHours(2) && job.State != "scanning") _jobs.TryRemove(id, out _);
    }

    public ScanJob Get(string id, string owner)
    {
        Sweep();
        if (!_jobs.TryGetValue(id, out var job) || job.Owner != owner) throw new KeyNotFoundException("مهمة المسح غير موجودة أو انتهت.");
        job.Touched = DateTime.UtcNow;
        return job;
    }

    /// <summary>بدء مسح في الخلفية، في مهمة جديدة أو بإضافة صفحات إلى مهمة قائمة (لمسح دفعة ثانية).</summary>
    public async Task<ScanJob> StartAsync(string deviceId, ScanSettings s, string owner, string? appendTo, CancellationToken ct)
    {
        if (s.Validate() is { } why) throw new ScannerException(why);
        var caps = await CapabilitiesAsync(deviceId, ct);
        if (!caps.Sources.Contains(s.Source)) throw new ScannerException("هذا الماسح لا يدعم المصدر المختار.");
        ScanJob job;
        if (appendTo is not null)
        {
            job = Get(appendTo, owner);
            lock (job.Lock)
            {
                if (job.State == "scanning") throw new ScannerException("المسح السابق في هذه المهمة لم ينتهِ بعد.");
                if (job.Pages.Count >= MaxPagesPerJob) throw new ScannerException($"بلغت المهمة {MaxPagesPerJob} صفحة؛ أدرجها ثم ابدأ مهمة جديدة.");
                job.State = "scanning"; job.Message = ""; job.Scanned = 0;
            }
        }
        else
        {
            Sweep();
            job = new ScanJob { DeviceId = deviceId, Owner = owner };
            _jobs[job.Id] = job;
        }
        var dpi = caps.Resolutions.Contains(s.Dpi) ? s.Dpi : caps.Resolutions.OrderBy(r => Math.Abs(r - s.Dpi)).First();
        var settings = s with { Dpi = dpi, MaxPages = s.MaxPages == 0 ? MaxPagesPerJob - job.Pages.Count : Math.Min(s.MaxPages, MaxPagesPerJob - job.Pages.Count) };
        _ = Task.Run(async () =>
        {
            try
            {
                var images = await AcquireAsync(deviceId, settings, caps, n => job.Scanned = n, CancellationToken.None);
                var brightness = caps.DeviceBrightness ? 0 : settings.Brightness;
                var contrast = caps.DeviceContrast ? 0 : settings.Contrast;
                var added = 0;
                foreach (var img in images)
                {
                    var page = PageTools.Prepare(img, settings.ColorMode, brightness, contrast, brightness != 0 || contrast != 0);
                    lock (job.Lock) job.Pages.Add(page);
                    added++;
                }
                lock (job.Lock)
                {
                    job.State = "ready";
                    var blanks = job.Pages.Count(p => p.Blank);
                    job.Message = $"مُسحت {added} صفحة" + (blanks > 0 ? $"، منها {blanks} تبدو فارغة" : "") + ".";
                }
            }
            catch (Exception ex)
            {
                lock (job.Lock)
                {
                    job.State = job.Pages.Count > 0 ? "ready" : "failed";
                    job.Message = ex is ScannerException ? ex.Message : "تعذر المسح: " + ex.Message;
                }
            }
            job.Touched = DateTime.UtcNow;
        });
        return job;
    }

    /// <summary>نافذة الماسح الأصلية (Windows): صفحة واحدة تُضاف إلى المهمة.</summary>
    public async Task<ScanJob> NativeAsync(string deviceId, string owner, string? appendTo, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || !deviceId.StartsWith("wia:", StringComparison.Ordinal)) throw new ScannerException("نافذة الماسح الأصلية متاحة لماسحات Windows فقط.");
        var target = Known(deviceId);
        var img = await Task.Run(() => OperatingSystem.IsWindows() ? WiaScanner.NativeDialog(target) : throw new PlatformNotSupportedException(), ct) ?? throw new ScannerException("أُلغي المسح.");
        var job = appendTo is null ? new ScanJob { DeviceId = deviceId, Owner = owner } : Get(appendTo, owner);
        _jobs[job.Id] = job;
        lock (job.Lock) { job.Pages.Add(PageTools.Prepare(img, "color", 0, 0, false)); job.State = "ready"; job.Message = "أُضيفت صفحة من نافذة الماسح."; }
        return job;
    }

    /// <summary>الترتيب النهائي للصفحات وتدويرها؛ الصفحات غير المذكورة تُحذف.</summary>
    public void Arrange(string id, string owner, IReadOnlyList<(string PageId, int Rotation)> order)
    {
        var job = Get(id, owner);
        lock (job.Lock)
        {
            if (job.State == "scanning") throw new ScannerException("انتظر انتهاء المسح.");
            var byId = job.Pages.ToDictionary(p => p.Id);
            var next = new List<ScanPage>();
            foreach (var (pid, rot) in order)
            {
                if (!byId.TryGetValue(pid, out var p) || next.Contains(p)) continue;
                p.Rotation = ((rot % 360) + 360) % 360 / 90 * 90;
                next.Add(p);
            }
            job.Pages.Clear();
            job.Pages.AddRange(next);
        }
    }

    public void Cancel(string id, string owner)
    {
        var job = Get(id, owner);
        if (job.State != "scanning") _jobs.TryRemove(id, out _);
    }

    /// <summary>
    /// إدراج الصفحات في الأرشيف: مستند واحد، أو مستند لكل صفحة، أو مستند لكل مجموعة تفصلها صفحة فارغة (لمسح خطابات كثيرة دفعة واحدة).
    /// skipBlank: حذف الصفحات الفارغة (ظهر الأوراق في المسح على الوجهين).
    /// </summary>
    public async Task<List<ImportResult>> ImportAsync(string id, string owner, ImportOptions options, string split, bool skipBlank, CancellationToken ct)
    {
        var job = Get(id, owner);
        List<ScanPage> pages;
        lock (job.Lock)
        {
            if (job.State == "scanning") throw new ScannerException("انتظر انتهاء المسح.");
            pages = job.Pages.ToList();
        }
        var groups = new List<List<ScanPage>>();
        if (split == "blank")
        {
            var current = new List<ScanPage>();
            foreach (var p in pages)
            {
                if (p.Blank) { if (current.Count > 0) groups.Add(current); current = new(); }
                else current.Add(p);
            }
            if (current.Count > 0) groups.Add(current);
        }
        else
        {
            var kept = skipBlank ? pages.Where(p => !p.Blank).ToList() : pages;
            groups = split == "each" ? kept.Select(p => new List<ScanPage> { p }).ToList() : kept.Count > 0 ? new() { kept } : new();
        }
        if (groups.Count == 0) throw new ScannerException("لا صفحات للإدراج (كلها فارغة أو محذوفة).");
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var results = new List<ImportResult>();
        for (var i = 0; i < groups.Count; i++)
        {
            var pdf = PageTools.BuildPdf(groups[i]);
            using var ms = new MemoryStream(pdf);
            var name = groups.Count == 1 ? $"مسح_{stamp}.pdf" : $"مسح_{stamp}_{i + 1:000}.pdf";
            results.Add(await _inbox.ImportAsync(ms, name, groups.Count == 1 ? options : options with { Title = "" }, owner, ct));
        }
        _jobs.TryRemove(id, out _);
        return results;
    }
}
