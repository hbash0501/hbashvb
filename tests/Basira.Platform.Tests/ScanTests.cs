using System.Net;
using System.Text;
using System.Xml.Linq;
using Basira.Platform.Processing.Scanning;
using SkiaSharp;
using UglyToad.PdfPig;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>ماسح شبكي وهمي بواجهة eSCL على هذا الجهاز: يعلن قدراته، ويقبل مهمة المسح، ويسلّم صفحات محددة ثم 404.</summary>
public sealed class FakeEsclScanner : IDisposable
{
    private readonly HttpListener _listener = new();
    public string Address { get; }
    public List<byte[]> Pages { get; set; } = new();
    public string LastJob = "";
    public bool AdfEmpty { get; set; }
    public int Deleted;
    private int _next;

    public const string Capabilities = """
        <?xml version="1.0" encoding="UTF-8"?>
        <scan:ScannerCapabilities xmlns:scan="http://schemas.hp.com/imaging/escl/2011/05/03" xmlns:pwg="http://www.pwg.org/schemas/2010/12/sm">
          <pwg:Version>2.6</pwg:Version>
          <pwg:MakeAndModel>Test MFP 4000</pwg:MakeAndModel>
          <scan:Platen><scan:PlatenInputCaps>
            <scan:MinWidth>16</scan:MinWidth><scan:MaxWidth>2550</scan:MaxWidth><scan:MinHeight>16</scan:MinHeight><scan:MaxHeight>3508</scan:MaxHeight>
            <scan:SettingProfiles><scan:SettingProfile>
              <scan:ColorModes><scan:ColorMode>BlackAndWhite1</scan:ColorMode><scan:ColorMode>Grayscale8</scan:ColorMode><scan:ColorMode>RGB24</scan:ColorMode></scan:ColorModes>
              <scan:DocumentFormats><pwg:DocumentFormat>image/jpeg</pwg:DocumentFormat><pwg:DocumentFormat>application/pdf</pwg:DocumentFormat></scan:DocumentFormats>
              <scan:SupportedResolutions><scan:DiscreteResolutions>
                <scan:DiscreteResolution><scan:XResolution>150</scan:XResolution><scan:YResolution>150</scan:YResolution></scan:DiscreteResolution>
                <scan:DiscreteResolution><scan:XResolution>300</scan:XResolution><scan:YResolution>300</scan:YResolution></scan:DiscreteResolution>
                <scan:DiscreteResolution><scan:XResolution>600</scan:XResolution><scan:YResolution>600</scan:YResolution></scan:DiscreteResolution>
              </scan:DiscreteResolutions></scan:SupportedResolutions>
            </scan:SettingProfile></scan:SettingProfiles>
          </scan:PlatenInputCaps></scan:Platen>
          <scan:Adf>
            <scan:AdfSimplexInputCaps><scan:MaxWidth>2550</scan:MaxWidth><scan:MaxHeight>4200</scan:MaxHeight></scan:AdfSimplexInputCaps>
            <scan:AdfDuplexInputCaps><scan:MaxWidth>2550</scan:MaxWidth><scan:MaxHeight>4200</scan:MaxHeight></scan:AdfDuplexInputCaps>
          </scan:Adf>
          <scan:BrightnessSupport><scan:Min>0</scan:Min><scan:Max>2000</scan:Max><scan:Normal>1000</scan:Normal><scan:Step>1</scan:Step></scan:BrightnessSupport>
        </scan:ScannerCapabilities>
        """;

    public FakeEsclScanner()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Address = $"127.0.0.1:{port}";
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _ = Task.Run(Loop);
    }

    private async Task Loop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); } catch (Exception) { return; }
            var path = ctx.Request.Url!.AbsolutePath;
            var res = ctx.Response;
            byte[] body = Array.Empty<byte>();
            if (path == "/eSCL/ScannerCapabilities") { body = Encoding.UTF8.GetBytes(Capabilities); res.ContentType = "text/xml"; }
            else if (path == "/eSCL/ScannerStatus") { body = Encoding.UTF8.GetBytes($"<scan:ScannerStatus xmlns:scan=\"http://schemas.hp.com/imaging/escl/2011/05/03\" xmlns:pwg=\"http://www.pwg.org/schemas/2010/12/sm\"><pwg:State>Idle</pwg:State><scan:AdfState>{(AdfEmpty ? "ScannerAdfEmpty" : "ScannerAdfLoaded")}</scan:AdfState></scan:ScannerStatus>"); res.ContentType = "text/xml"; }
            else if (path == "/eSCL/ScanJobs" && ctx.Request.HttpMethod == "POST")
            {
                using var r = new StreamReader(ctx.Request.InputStream);
                LastJob = await r.ReadToEndAsync();
                _next = 0;
                res.StatusCode = 201;
                res.Headers["Location"] = $"http://{Address}/eSCL/ScanJobs/job1";
            }
            else if (path.EndsWith("/NextDocument", StringComparison.Ordinal))
            {
                if (_next < Pages.Count) { body = Pages[_next++]; res.ContentType = "image/jpeg"; }
                else res.StatusCode = 404;
            }
            else if (ctx.Request.HttpMethod == "DELETE") Interlocked.Increment(ref Deleted);
            else res.StatusCode = 404;
            try
            {
                res.ContentLength64 = body.Length;
                if (body.Length > 0) await res.OutputStream.WriteAsync(body);
                res.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException) { }
        }
    }

    public static byte[] Jpeg(string pngPath)
    {
        using var b = SKBitmap.Decode(pngPath);
        using var img = SKImage.FromBitmap(b);
        return img.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();
    }

    public static byte[] BlankJpeg(int w = 1240, int h = 1754)
    {
        using var b = new SKBitmap(w, h);
        using (var c = new SKCanvas(b))
        {
            c.Clear(new SKColor(250, 250, 248));
            // ظل حافة الورقة كما في المسح الحقيقي: لا يجعلها غير فارغة.
            using var edge = new SKPaint { Color = new SKColor(60, 60, 60) };
            c.DrawRect(0, 0, 12, h, edge);
        }
        using var img = SKImage.FromBitmap(b);
        return img.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();
    }

    public void Dispose() { try { _listener.Stop(); } catch (ObjectDisposedException) { } }
}

/// <summary>
/// المسح الضوئي: قراءة قدرات eSCL وبناء طلب المسح، ومعالجة الصفحات (فارغة، أبيض وأسود، تدوير، PDF)، والمسار كاملاً
/// من ماسح شبكي وهمي حتى مستندات مقروءة في الأرشيف. ماسحات WIA لا تُختبر هنا (Windows فقط).
/// </summary>
public class ScanTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Escl_capabilities_and_job_settings()
    {
        var (caps, ranges) = EsclScanner.ParseCapabilities(FakeEsclScanner.Capabilities);
        Assert.Equal("Test MFP 4000", caps.Model);
        Assert.Equal(new[] { "flatbed", "feeder", "duplex" }, caps.Sources);
        Assert.Equal(new[] { "color", "gray", "bw" }, caps.ColorModes);
        Assert.Equal(new[] { 150, 300, 600 }, caps.Resolutions);
        Assert.Equal(215.9, caps.MaxWidthMm, 1);
        Assert.Equal(297.0, caps.MaxHeightMm, 0);
        Assert.True(caps.DeviceBrightness);
        Assert.False(caps.DeviceContrast);

        var s = new ScanSettings("duplex", "bw", 300, "A4", Brightness: 50, Contrast: 30);
        var xml = XDocument.Parse(EsclScanner.JobXml(s, caps, ranges));
        string V(string name) => xml.Descendants().First(e => e.Name.LocalName == name).Value;
        Assert.Equal("Feeder", V("InputSource"));
        Assert.Equal("true", V("Duplex"));
        Assert.Equal("Grayscale8", V("ColorMode")); // الأبيض والأسود من الرمادي بعتبة تلقائية
        Assert.Equal("300", V("XResolution"));
        Assert.Equal("2480", V("Width")); // A4 بأجزاء 300 من البوصة
        Assert.Equal("3508", V("Height"));
        Assert.Equal("1500", V("Brightness")); // منتصف المسافة بين العادي 1000 والأقصى 2000
        Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "Contrast"); // لا يدعمه الجهاز: يُطبَّق على الصورة

        // منطقة مختارة من المعاينة تُقص على حدود الزجاج.
        var region = new ScanSettings(LeftMm: 100, TopMm: 50, WidthMm: 200, HeightMm: 100).Region(caps);
        Assert.Equal((100, 50, 115.9, 100), (region.Left, region.Top, Math.Round(region.Width, 1), region.Height));
        Assert.NotNull(new ScanSettings(Dpi: 5000).Validate());
        Assert.Throws<ScannerException>(() => EsclScanner.Root("8.8.8.8"));
        Assert.Equal("http://192.168.1.40/eSCL", EsclScanner.Root("192.168.1.40"));
        Assert.Equal("https://10.0.0.9/eSCL", EsclScanner.Root("https://10.0.0.9/eSCL/"));
    }

    [Fact]
    public void Pages_are_prepared_rotated_and_bound_into_a_pdf()
    {
        var letter = PageTools.Prepare(new ScannedImage(FakeEsclScanner.Jpeg(Fixture("incoming-letter.png")), 150), "bw", 0, 0, false);
        Assert.False(letter.Blank);
        Assert.False(letter.Jpeg);
        using (var bw = SKBitmap.Decode(letter.Data))
            Assert.All(Enumerable.Range(0, 200).Select(i => bw.GetPixel(i * 3 % bw.Width, i * 7 % bw.Height)), c => Assert.True(c.Red is 0 or 255));
        var blank = PageTools.Prepare(new ScannedImage(FakeEsclScanner.BlankJpeg(), 150), "gray", 0, 0, false);
        Assert.True(blank.Blank);
        var color = PageTools.Prepare(new ScannedImage(FakeEsclScanner.Jpeg(Fixture("incoming-letter.png")), 150), "color", 20, 10, true);
        Assert.True(color.Jpeg);

        letter.Rotation = 90;
        var (_, rw, rh) = PageTools.Rotated(letter);
        Assert.Equal((letter.Height, letter.Width), (rw, rh));
        var pdf = PageTools.BuildPdf(new[] { letter, color });
        using var doc = PdfDocument.Open(pdf);
        Assert.Equal(2, doc.NumberOfPages);
        var p1 = doc.GetPage(1);
        // دُوّرت: يتبادل العرض والارتفاع، ومقاس الصفحة من دقة المسح.
        Assert.Equal(letter.Height * 72.0 / 150, p1.Width, 0);
        Assert.Equal(letter.Width * 72.0 / 150, p1.Height, 0);
    }

    [Fact]
    public async Task Feeder_batch_is_split_at_blank_sheets_into_readable_documents()
    {
        using var scanner = new FakeEsclScanner
        {
            Pages = new() { FakeEsclScanner.Jpeg(Fixture("incoming-letter.png")), FakeEsclScanner.BlankJpeg(), FakeEsclScanner.Jpeg(Fixture("indic-letter.png")) },
        };
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var reader = await f.UserAsync(admin, "reader1", "قارئ");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/scan/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, HttpMethod.Post, "/api/scan/devices", new { address = "8.8.8.8" })).StatusCode);

        var device = await Json(await Send(admin, HttpMethod.Post, "/api/scan/devices", new { address = scanner.Address }));
        var deviceId = device.GetProperty("id").GetString()!;
        Assert.Equal("Test MFP 4000", device.GetProperty("name").GetString());
        var list = await Get(admin, "/api/scan/devices");
        Assert.Contains(list.GetProperty("devices").EnumerateArray(), d => d.GetProperty("id").GetString() == deviceId && d.GetProperty("name").GetString() == "Test MFP 4000");
        var caps = await Get(admin, "/api/scan/devices/caps?id=" + Uri.EscapeDataString(deviceId));
        Assert.Contains("duplex", caps.GetProperty("sources").EnumerateArray().Select(x => x.GetString()));

        scanner.AdfEmpty = true;
        var empty = await Json(await Send(admin, HttpMethod.Post, "/api/scan/jobs", new { deviceId, settings = new { source = "feeder", colorMode = "gray", dpi = 300 } }));
        var failed = await Wait(admin, empty.GetProperty("id").GetString()!);
        Assert.Equal("failed", failed.GetProperty("state").GetString());
        Assert.Contains("لا أوراق", failed.GetProperty("message").GetString());
        scanner.AdfEmpty = false;

        var started = await Json(await Send(admin, HttpMethod.Post, "/api/scan/jobs", new { deviceId, settings = new { source = "feeder", colorMode = "gray", dpi = 250, brightness = 10 } }));
        var jobId = started.GetProperty("id").GetString()!;
        var job = await Wait(admin, jobId);
        Assert.Equal("ready", job.GetProperty("state").GetString());
        var pages = job.GetProperty("pages").EnumerateArray().ToList();
        Assert.Equal(3, pages.Count);
        Assert.Equal(new[] { false, true, false }, pages.Select(p => p.GetProperty("blank").GetBoolean()));
        Assert.Contains("<scan:XResolution>300</scan:XResolution>", scanner.LastJob); // أقرب دقة يدعمها الجهاز
        Assert.Contains("Feeder", scanner.LastJob);
        Assert.True(scanner.Deleted >= 1); // أُغلقت المهمة في الجهاز
        var thumb = await admin.GetAsync($"/api/scan/jobs/{jobId}/pages/{pages[0].GetProperty("id").GetString()}");
        Assert.Equal("image/jpeg", thumb.Content.Headers.ContentType?.MediaType);
        // مهمة المسح لصاحبها: موظف آخر يملك صلاحية المسح لا يراها.
        var clerk = await f.UserAsync(admin, "clerk1", "موظف أرشفة");
        Assert.Equal(HttpStatusCode.NotFound, (await clerk.GetAsync($"/api/scan/jobs/{jobId}")).StatusCode);

        // المراجعة: الترتيب كما هو مع تدوير لا يغيّر شيئاً، ثم الإدراج بالفصل عند الصفحة الفارغة.
        await Json(await Send(admin, HttpMethod.Post, $"/api/scan/jobs/{jobId}/arrange", new { pages = pages.Select(p => new { id = p.GetProperty("id").GetString(), rotation = 0 }) }));
        var imported = await Json(await Send(admin, HttpMethod.Post, $"/api/scan/jobs/{jobId}/import", new { direction = "وارد", confidentiality = "عادي", split = "blank" }));
        Assert.Equal(2, imported.GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/scan/jobs/{jobId}")).StatusCode);
        await WaitIdle(admin, 240);
        var first = await Get(admin, "/api/documents/" + imported[0].GetProperty("id").GetString());
        Assert.Equal(".pdf", first.GetProperty("document").GetProperty("fileType").GetString());
        Assert.Contains("4521", first.GetProperty("text").GetString());
        var second = await Get(admin, "/api/documents/" + imported[1].GetProperty("id").GetString());
        Assert.NotEqual("", second.GetProperty("text").GetString());
    }

    private static async Task<System.Text.Json.JsonElement> Wait(HttpClient c, string jobId)
    {
        var until = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            var j = await Get(c, "/api/scan/jobs/" + jobId);
            if (j.GetProperty("state").GetString() != "scanning" || DateTime.UtcNow > until) return j;
            await Task.Delay(200);
        }
    }
}
