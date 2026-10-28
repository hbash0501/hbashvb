using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Basira.Platform.Processing.Ocr;

namespace Basira.Platform.Processing.Scanning;

/// <summary>
/// الماسحات الشبكية عبر eSCL (ويسمى AirScan أو Mopria Scan): بروتوكول HTTP مفتوح تدعمه أغلب الطابعات متعددة الوظائف الحديثة
/// دون تعريف. العنوان يجب أن يكون على الشبكة الداخلية، وتُقبل شهادة الجهاز الذاتية (معتادة في الطابعات).
/// </summary>
public static class EsclScanner
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        UseProxy = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
    }) { Timeout = Timeout.InfiniteTimeSpan };

    private const string ScanNs = "http://schemas.hp.com/imaging/escl/2011/05/03";
    private const string PwgNs = "http://www.pwg.org/schemas/2010/12/sm";

    /// <summary>جذر eSCL من عنوان يكتبه المستخدم: «192.168.1.50» أو «http://printer.local/eSCL».</summary>
    public static string Root(string address)
    {
        var a = address.Trim().TrimEnd('/');
        if (!a.Contains("://", StringComparison.Ordinal)) a = "http://" + a;
        if (!Uri.TryCreate(a, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) throw new ScannerException("عنوان الماسح غير صحيح.");
        if (!VisionOcr.IsLocalUrl(u.GetLeftPart(UriPartial.Authority) + "/", out _)) throw new ScannerException("يُقبل ماسح على الشبكة الداخلية فقط.");
        return u.AbsolutePath is "" or "/" ? a + "/eSCL" : a;
    }

    private static IEnumerable<XElement> All(XContainer x, string name) => x.Descendants().Where(e => e.Name.LocalName == name);
    private static string? First(XContainer x, string name) => All(x, name).FirstOrDefault()?.Value.Trim();
    private static double Mm(string? threeHundredths) => double.TryParse(threeHundredths, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 300 * 25.4 : 0;

    public static async Task<(ScannerCaps Caps, EsclRanges Ranges)> CapabilitiesAsync(string root, CancellationToken ct)
    {
        string xml;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            xml = await Http.GetStringAsync(root + "/ScannerCapabilities", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { throw new ScannerException("تعذر الاتصال بالماسح: " + ex.Message, ex); }
        return ParseCapabilities(xml);
    }

    public sealed record EsclRanges((int Min, int Max, int Normal)? Brightness, (int Min, int Max, int Normal)? Contrast, bool NativeBw);

    public static (ScannerCaps, EsclRanges) ParseCapabilities(string xml)
    {
        XDocument d;
        try { d = XDocument.Parse(xml); }
        catch (System.Xml.XmlException ex) { throw new ScannerException("رد الماسح ليس قدرات eSCL صالحة.", ex); }
        var sources = new List<string>();
        var platen = All(d, "PlatenInputCaps").FirstOrDefault();
        var adf = All(d, "AdfSimplexInputCaps").FirstOrDefault();
        if (platen is not null) sources.Add("flatbed");
        if (adf is not null) sources.Add("feeder");
        if (All(d, "AdfDuplexInputCaps").Any() || All(d, "AdfOptions").Any(o => o.Value.Contains("Duplex", StringComparison.Ordinal))) sources.Add("duplex");
        if (sources.Count == 0) throw new ScannerException("لم يعلن الماسح أي مصدر للورق.");
        var area = platen ?? adf!;
        var modes = All(d, "ColorMode").Select(e => e.Value.Trim()).Distinct().ToList();
        var colors = new List<string>();
        if (modes.Contains("RGB24")) colors.Add("color");
        if (modes.Contains("Grayscale8") || modes.Contains("RGB24")) colors.Add("gray");
        colors.Add("bw"); // من الرمادي بعتبة تلقائية إن لم يدعمه الجهاز
        var res = All(d, "DiscreteResolution").Select(r => int.TryParse(First(r, "XResolution"), out var v) ? v : 0).Where(v => v > 0).ToHashSet();
        foreach (var range in All(d, "ResolutionRange"))
            if (int.TryParse(First(range, "Min"), out var min) && int.TryParse(First(range, "Max"), out var max))
                foreach (var v in new[] { 75, 100, 150, 200, 300, 400, 600, 1200 }) if (v >= min && v <= max) res.Add(v);
        if (res.Count == 0) res.UnionWith(new[] { 150, 300 });
        (int, int, int)? Range(string name)
        {
            var e = All(d, name).FirstOrDefault();
            return e is not null && int.TryParse(First(e, "Min"), out var mn) && int.TryParse(First(e, "Max"), out var mx) && mx > mn
                ? (mn, mx, int.TryParse(First(e, "Normal"), out var n) ? n : (mn + mx) / 2) : null;
        }
        var b = Range("BrightnessSupport");
        var c = Range("ContrastSupport");
        var caps = new ScannerCaps(First(d, "MakeAndModel") ?? "ماسح eSCL", sources, colors, res.OrderBy(v => v).ToList(),
            Math.Round(Mm(First(area, "MaxWidth")), 1), Math.Round(Mm(First(area, "MaxHeight")), 1), b is not null, c is not null);
        return (caps, new EsclRanges(b, c, modes.Contains("BlackAndWhite1")));
    }

    /// <summary>قيمة من -100..100 إلى مدى الجهاز حول قيمته العادية.</summary>
    public static int Scale(int v, (int Min, int Max, int Normal) r) =>
        v >= 0 ? r.Normal + (int)Math.Round(v / 100.0 * (r.Max - r.Normal)) : r.Normal + (int)Math.Round(v / 100.0 * (r.Normal - r.Min));

    public static string JobXml(ScanSettings s, ScannerCaps caps, EsclRanges ranges)
    {
        var (left, top, width, height) = s.Region(caps);
        int T(double mm) => (int)Math.Round(mm / 25.4 * 300);
        XNamespace scan = ScanNs, pwg = PwgNs;
        var mode = s.ColorMode == "color" ? "RGB24" : "Grayscale8";
        var root = new XElement(scan + "ScanSettings",
            new XAttribute(XNamespace.Xmlns + "scan", ScanNs), new XAttribute(XNamespace.Xmlns + "pwg", PwgNs),
            new XElement(pwg + "Version", "2.0"),
            new XElement(pwg + "ScanRegions", new XElement(pwg + "ScanRegion",
                new XElement(pwg + "ContentRegionUnits", "escl:ThreeHundredthsOfInches"),
                new XElement(pwg + "XOffset", T(left)), new XElement(pwg + "YOffset", T(top)),
                new XElement(pwg + "Width", T(width)), new XElement(pwg + "Height", T(height)))),
            new XElement(pwg + "InputSource", s.Source == "flatbed" ? "Platen" : "Feeder"),
            s.Source == "duplex" ? new XElement(scan + "Duplex", "true") : null,
            new XElement(scan + "ColorMode", mode),
            new XElement(scan + "XResolution", s.Dpi), new XElement(scan + "YResolution", s.Dpi),
            new XElement(pwg + "DocumentFormat", "image/jpeg"),
            new XElement(scan + "DocumentFormatExt", "image/jpeg"),
            ranges.Brightness is { } br && s.Brightness != 0 ? new XElement(scan + "Brightness", Scale(s.Brightness, br)) : null,
            ranges.Contrast is { } co && s.Contrast != 0 ? new XElement(scan + "Contrast", Scale(s.Contrast, co)) : null);
        return new XDeclaration("1.0", "UTF-8", null) + Environment.NewLine + root;
    }

    /// <summary>مسح كامل: إنشاء المهمة، ثم سحب الصفحات واحدة واحدة حتى ينفد الورق (أو صفحة واحدة من الزجاج).</summary>
    public static async Task<List<ScannedImage>> ScanAsync(string root, ScanSettings s, Action<int> onPage, CancellationToken ct)
    {
        var (caps, ranges) = await CapabilitiesAsync(root, ct);
        if (!caps.Sources.Contains(s.Source)) throw new ScannerException("هذا الماسح لا يدعم المصدر المختار.");
        if (s.Source != "flatbed" && await AdfEmptyAsync(root, ct)) throw new ScannerException("لا أوراق في وحدة التغذية.");
        using var content = new StringContent(JobXml(s, caps, ranges), Encoding.UTF8, "text/xml");
        HttpResponseMessage created;
        try { created = await Http.PostAsync(root + "/ScanJobs", content, ct); }
        catch (HttpRequestException ex) { throw new ScannerException("تعذر الاتصال بالماسح: " + ex.Message, ex); }
        using (created)
        {
            if (created.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.Conflict) throw new ScannerException("الماسح مشغول بمهمة أخرى؛ أعد المحاولة بعد قليل.");
            if (created.StatusCode != HttpStatusCode.Created || created.Headers.Location is null)
                throw new ScannerException($"رفض الماسح الإعدادات (رمز {(int)created.StatusCode}). جرّب دقة أو مقاساً آخر.");
            var job = created.Headers.Location.IsAbsoluteUri ? created.Headers.Location : new Uri(new Uri(root), created.Headers.Location);
            var pages = new List<ScannedImage>();
            try
            {
                while (s.MaxPages == 0 || pages.Count < s.MaxPages)
                {
                    var page = await NextAsync(job, ct);
                    if (page is null) break;
                    pages.Add(new ScannedImage(page, s.Dpi));
                    onPage(pages.Count);
                    if (s.Source == "flatbed") break;
                }
            }
            finally
            {
                try { using var _ = await Http.DeleteAsync(job, CancellationToken.None); } catch (HttpRequestException) { }
            }
            if (pages.Count == 0) throw new ScannerException(s.Source == "flatbed" ? "لم يُعِد الماسح صورة." : "لم تُسحب أي ورقة من وحدة التغذية.");
            return pages;
        }
    }

    private static async Task<byte[]?> NextAsync(Uri job, CancellationToken ct)
    {
        var until = DateTime.UtcNow.AddMinutes(3);
        while (true)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(3));
            using var r = await Http.GetAsync(job.AbsoluteUri.TrimEnd('/') + "/NextDocument", cts.Token);
            if (r.StatusCode == HttpStatusCode.OK) return await r.Content.ReadAsByteArrayAsync(ct);
            if (r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
            // 503: الجهاز ما زال يمسح الصفحة.
            if (r.StatusCode != HttpStatusCode.ServiceUnavailable || DateTime.UtcNow > until) throw new ScannerException($"توقف المسح (رمز {(int)r.StatusCode}).");
            await Task.Delay(800, ct);
        }
    }

    private static async Task<bool> AdfEmptyAsync(string root, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var status = await Http.GetStringAsync(root + "/ScannerStatus", cts.Token);
            return First(XDocument.Parse(status), "AdfState") is "ScannerAdfEmpty";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException) { return false; }
    }
}
