using System.Net;
using System.Text;
using System.Text.Json;
using Basira.Platform.Processing;
using Basira.Platform.Processing.Ocr;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>خادم قراءة مرئية وهمي على هذا الجهاز بواجهة OpenAI (كما في Ollama وllama.cpp)، يرد بنص يحدده الاختبار.</summary>
public sealed class FakeVisionServer : IDisposable
{
    private readonly HttpListener _listener = new();
    public string Url { get; }
    public Func<int, string> Reply { get; set; } = _ => "";
    public string[] Models { get; set; } = { "qwen3-vl:4b" };
    public int Requests;
    public string LastPrompt = "";
    public int LastImageBytes;
    /// <summary>يتصرف كـ Ollama: /api/version و/api/chat (الواجهة الأصلية التي تقبل حجم السياق).</summary>
    public bool Ollama { get; set; }
    public int DelayMs { get; set; }
    public string LastPath = "";
    public int LastNumCtx;

    public FakeVisionServer()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Url = $"http://127.0.0.1:{port}/v1";
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
            string json;
            var path = ctx.Request.Url!.AbsolutePath;
            if (path.EndsWith("/models", StringComparison.Ordinal))
                json = JsonSerializer.Serialize(new { data = Models.Select(m => new { id = m }) });
            else if (path == "/api/version")
            {
                if (!Ollama) { ctx.Response.StatusCode = 404; ctx.Response.Close(); continue; }
                json = "{\"version\":\"0.12.9\"}";
            }
            else if (path == "/api/chat")
            {
                LastPath = path;
                using var reader = new StreamReader(ctx.Request.InputStream);
                using var body = JsonDocument.Parse(await reader.ReadToEndAsync());
                var m = body.RootElement.GetProperty("messages")[0];
                LastPrompt = m.GetProperty("content").GetString()!;
                LastImageBytes = Convert.FromBase64String(m.GetProperty("images")[0].GetString()!).Length;
                LastNumCtx = body.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32();
                var n = Interlocked.Increment(ref Requests);
                if (DelayMs > 0) await Task.Delay(DelayMs);
                json = JsonSerializer.Serialize(new { model = "x", message = new { role = "assistant", content = Reply(n) }, done = true });
            }
            else
            {
                LastPath = path;
                using var reader = new StreamReader(ctx.Request.InputStream);
                using var body = JsonDocument.Parse(await reader.ReadToEndAsync());
                var parts = body.RootElement.GetProperty("messages")[0].GetProperty("content").EnumerateArray().ToList();
                LastPrompt = parts.First(p => p.GetProperty("type").GetString() == "text").GetProperty("text").GetString()!;
                var url = parts.First(p => p.GetProperty("type").GetString() == "image_url").GetProperty("image_url").GetProperty("url").GetString()!;
                LastImageBytes = Convert.FromBase64String(url[(url.IndexOf(',') + 1)..]).Length;
                var n = Interlocked.Increment(ref Requests);
                if (DelayMs > 0) await Task.Delay(DelayMs);
                json = JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = Reply(n) } } } });
            }
            var bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException) { } // أغلق العميل الاتصال بعد المهلة
        }
    }

    public void Dispose() { try { _listener.Stop(); } catch (ObjectDisposedException) { } }
}

/// <summary>
/// محرك القراءة المرئي: حراسة العنوان المحلي، وتنظيف الرد، والتحقق بمحركين، والرجوع إلى Tesseract، والقياس على العينات.
/// الخادم هنا وهمي يرد بنص محدد؛ دقة النماذج الحقيقية تقاس على جهاز المستخدم بزر «قياس المحركات».
/// </summary>
public class VisionOcrTests
{
    private readonly ITestOutputHelper _out;
    public VisionOcrTests(ITestOutputHelper o) => _out = o;

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public const string IncomingText = """
        وزارة المالية
        الرقم: 4521
        التاريخ: 1447/03/12هـ
        الموضوع: طلب بيانات المشاريع المتعثرة
        سعادة مدير عام الشؤون الإدارية المحترم
        السلام عليكم ورحمة الله وبركاته
        نأمل تزويدنا ببيانات المشاريع المتعثرة لديكم خلال (15) يوماً من تاريخه، مع بيان أسباب التعثر والإجراءات المتخذة لمعالجتها.
        وتقبلوا تحياتنا
        """;

    [Theory]
    [InlineData("http://127.0.0.1:11434/v1", true)]
    [InlineData("http://localhost:8080/v1", true)]
    [InlineData("http://192.168.1.20:11434/v1", true)]
    [InlineData("http://10.0.0.5:8000/v1", true)]
    [InlineData("http://172.20.1.1/v1", true)]
    [InlineData("http://ocr-server:11434/v1", true)]
    [InlineData("http://gpu01.local:11434/v1", true)]
    [InlineData("http://[::1]:11434/v1", true)]
    [InlineData("https://api.openai.com/v1", false)]
    [InlineData("http://8.8.8.8/v1", false)]
    [InlineData("http://172.32.0.1/v1", false)]
    [InlineData("ftp://127.0.0.1/", false)]
    [InlineData("127.0.0.1:11434", false)]
    public void Only_local_or_internal_servers_are_accepted(string url, bool ok) => Assert.Equal(ok, VisionOcr.IsLocalUrl(url, out _));

    [Fact]
    public void Model_replies_are_tidied_and_loops_cut()
    {
        Assert.Equal("الرقم: 4521\nالموضوع: طلب", VisionOcr.Tidy("```text\n# الرقم: 4521\n**الموضوع:** طلب\n```"));
        var loop = "بداية الخطاب\n" + string.Concat(Enumerable.Repeat("وتقبلوا تحياتنا الطيبة\n", 40));
        Assert.Equal("بداية الخطاب\nوتقبلوا تحياتنا الطيبة\nوتقبلوا تحياتنا الطيبة\nوتقبلوا تحياتنا الطيبة", VisionOcr.Tidy(loop));
        Assert.Equal("نفيدكم بأن الموضوع قيد الدراسة", VisionOcr.Tidy("نفيدكم بأن الموضوع قيد الدراسة نفيدكم بأن الموضوع قيد الدراسة نفيدكم بأن الموضوع قيد الدراسة نفيدكم بأن الموضوع قيد الدراسة"));
        Assert.Equal("نص", VisionOcr.Tidy("<think>تفكير</think>نص"));
        Assert.True(VisionOcr.Agreement(IncomingText, IncomingText.Replace("4521", "4527")) > 0.9);
        Assert.True(VisionOcr.Agreement(IncomingText, "دعوة لحضور حفل التكريم السنوي للموظفين المتميزين في القاعة الكبرى") < 0.2);
        Assert.Equal("OCR:", new VisionOptions("http://127.0.0.1/v1", "PaddleOCR-VL-1.5").EffectivePrompt);
    }

    [Fact]
    public void Fields_are_confirmed_only_when_both_engines_agree()
    {
        var second = new OcrDocument { Text = IncomingText.Replace("1447/03/12", "1447/03/17") };
        second.Checks.Add(new FieldCheck("number", "4521", new List<string> { "4521" }, true, 0, new SKRectI(10, 10, 90, 40), 0, 4, ""));
        var checks = CrossCheck.Build(IncomingText, second);
        var number = checks.Single(c => c.Field == "number");
        Assert.True(number.Confident);
        Assert.Equal(new SKRectI(10, 10, 90, 40), number.Box); // مقتطع المراجعة من قراءة Tesseract
        Assert.Equal("4521", IncomingText.Substring(number.Offset, number.Length));
        var date = checks.Single(c => c.Field == "date");
        Assert.False(date.Confident);
        Assert.Equal(CrossCheck.Disagree, date.Reason);
        Assert.Equal(2, date.Candidates.Count);
        Assert.True(checks.Single(c => c.Field == "subject").Confident);
        Assert.True(checks.Single(c => c.Field == "deadline").Confident);

        // محرك واحد: لا اعتماد آلي.
        Assert.All(CrossCheck.Build(IncomingText, null), c => Assert.Equal((false, CrossCheck.SingleEngine), (c.Confident, c.Reason)));
        // ما قرأه Tesseract وغاب عن المحرك المرئي يُعرض للتأكيد.
        var noNumber = CrossCheck.Build(IncomingText.Replace("الرقم: 4521\n", ""), second);
        Assert.Contains(noNumber, c => c.Field == "number" && !c.Confident && c.Value == "4521" && c.Offset == -1);
    }

    [Fact]
    public async Task Ollama_native_api_gets_a_context_that_fits_the_page_and_slow_servers_are_paused()
    {
        // سياق يتسع للصورة: A4 بعرض 1280 نحو 2950 رمزاً للصورة؛ وبعرض 1800 يتجاوز 8192 (سياق Ollama الافتراضي 4096).
        Assert.Equal(7168, VisionOcr.ContextFor(1280, 1810));
        Assert.True(VisionOcr.ContextFor(1800, 2546) > 8192);

        using var server = new FakeVisionServer { Ollama = true, Reply = _ => IncomingText };
        var o = new VisionOptions(server.Url, "qwen3-vl:4b");
        var text = await VisionOcr.ReadAsync(Fixture("incoming-letter.png"), o, CancellationToken.None);
        Assert.Equal(IncomingText.Trim(), text);
        Assert.Equal("/api/chat", server.LastPath);
        Assert.Contains("Transcribe", server.LastPrompt);
        Assert.InRange(server.LastNumCtx, 4096, 16384);

        // مهلة قصيرة وخادم بطيء: تجاوزان متتاليان يوقفان المحرك مؤقتاً، والفحص الجديد يستأنفه.
        server.DelayMs = 2500;
        var quick = o with { TimeoutSeconds = 1 };
        await Assert.ThrowsAsync<VisionTimeoutException>(() => VisionOcr.ReadAsync(Fixture("incoming-letter.png"), quick, CancellationToken.None));
        Assert.False(VisionOcr.Suspended(server.Url, out _));
        await Assert.ThrowsAsync<VisionTimeoutException>(() => VisionOcr.ReadAsync(Fixture("incoming-letter.png"), quick, CancellationToken.None));
        Assert.True(VisionOcr.Suspended(server.Url, out var until));
        Assert.True(until > DateTime.UtcNow.AddMinutes(VisionOcr.SuspendMinutes - 1));
        VisionOcr.Forget(server.Url);
        Assert.False(VisionOcr.Suspended(server.Url, out _));
        server.DelayMs = 0;
    }

    [Fact]
    public async Task Benchmark_stops_the_vision_run_at_the_first_timeout()
    {
        using var server = new FakeVisionServer { DelayMs = 40_000, Reply = _ => IncomingText };
        using var f = new PlatformFactory();
        var c = await Configure(f, server);
        await Json(await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["vision_ocr_timeout"] = "30" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["vision_ocr_timeout"] = "5" })).StatusCode);
        await Json(await Send(c, HttpMethod.Post, "/api/admin/ocr/benchmark"));
        var until = DateTime.UtcNow.AddMinutes(6);
        var st = await Get(c, "/api/admin/ocr");
        while (st.GetProperty("running").GetBoolean() && DateTime.UtcNow < until) { await Task.Delay(500); st = await Get(c, "/api/admin/ocr"); }
        var auto = st.GetProperty("benchmark").GetProperty("engines").EnumerateArray().Single(e => e.GetProperty("mode").GetString() == "auto");
        Assert.True(auto.GetProperty("incomplete").GetBoolean());
        Assert.Equal(0, auto.GetProperty("pagesDone").GetInt32());
        Assert.Contains("30 ثانية", auto.GetProperty("note").GetString());
        Assert.Equal(1, server.Requests); // لم يُكمل الصفحات الباقية بعد تجاوز المهلة
    }

    private static async Task<HttpClient> Configure(PlatformFactory f, FakeVisionServer server)
    {
        var c = await f.AdminAsync();
        await Json(await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["vision_ocr_url"] = server.Url, ["vision_ocr_model"] = "qwen3-vl:4b" }));
        return c;
    }

    [Fact]
    public async Task Vision_text_is_used_and_verified_by_tesseract()
    {
        using var server = new FakeVisionServer { Reply = _ => "```\n" + IncomingText + "\n```" };
        using var f = new PlatformFactory();
        var c = await Configure(f, server);
        var st = await Get(c, "/api/admin/ocr");
        Assert.Equal("qwen3-vl:4b", st.GetProperty("vision").GetProperty("model").GetString());

        var up = await Upload(c, "scan_0001.png", await File.ReadAllBytesAsync(Fixture("incoming-letter.png")));
        await WaitIdle(c, 180);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        Assert.Equal(1, server.Requests);
        Assert.Contains("Transcribe", server.LastPrompt);
        Assert.True(server.LastImageBytes > 10_000);
        Assert.Equal(IncomingText.Trim(), d.GetProperty("text").GetString());
        Assert.Equal("طلب بيانات المشاريع المتعثرة", d.GetProperty("document").GetProperty("title").GetString());
        var checks = d.GetProperty("checks").EnumerateArray().ToList();
        foreach (var k in checks) _out.WriteLine($"{k.GetProperty("field").GetString()} = {k.GetProperty("value").GetString()} | {k.GetProperty("state").GetString()} | {k.GetProperty("reason").GetString()}");
        Assert.Contains(checks, k => k.GetProperty("field").GetString() == "number" && k.GetProperty("state").GetString() == "auto");
        Assert.Contains(checks, k => k.GetProperty("field").GetString() == "date" && k.GetProperty("state").GetString() == "auto");
        Assert.Equal("4521", d.GetProperty("document").GetProperty("externalNo").GetString());
    }

    [Fact]
    public async Task Disagreement_goes_to_review_and_hallucination_falls_back_to_tesseract()
    {
        var replies = new Queue<string>(new[] { IncomingText.Replace("4521", "4527"), "دعوة لحضور حفل التكريم السنوي للموظفين المتميزين في القاعة الكبرى بمقر الوزارة يوم الخميس القادم." });
        using var server = new FakeVisionServer { Reply = _ => replies.Dequeue() };
        using var f = new PlatformFactory();
        var c = await Configure(f, server);
        var png = await File.ReadAllBytesAsync(Fixture("incoming-letter.png"));

        var wrongNumber = await Upload(c, "a.png", png);
        await WaitIdle(c, 180);
        var d = await Get(c, "/api/documents/" + wrongNumber.GetProperty("id").GetString());
        var number = d.GetProperty("checks").EnumerateArray().Single(k => k.GetProperty("field").GetString() == "number");
        Assert.Equal("pending", number.GetProperty("state").GetString());
        Assert.Contains("4521", number.GetProperty("candidates").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("4527", number.GetProperty("candidates").EnumerateArray().Select(x => x.GetString()));

        // صورة مختلفة البايتات (لا تُرفض مكررة) بالمحتوى نفسه.
        using var bmp = SKBitmap.Decode(png);
        using var copy = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Jpeg, 95);
        var hallucinated = await Upload(c, "b.jpg", copy.ToArray());
        await WaitIdle(c, 180);
        d = await Get(c, "/api/documents/" + hallucinated.GetProperty("id").GetString());
        Assert.Contains("طلب بيانات المشاريع", d.GetProperty("text").GetString());
        Assert.Contains("اعتُمدت قراءة Tesseract", d.GetProperty("processingError").GetString());
    }

    [Fact]
    public async Task Unreachable_server_falls_back_and_public_addresses_are_refused()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var r = await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["vision_ocr_url"] = "https://api.example.com/v1" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("الإنترنت", await r.Content.ReadAsStringAsync());

        var dead = new FakeVisionServer();
        var url = dead.Url;
        dead.Dispose();
        await Json(await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["vision_ocr_url"] = url, ["vision_ocr_model"] = "qwen3-vl:4b" }));
        var st = await Get(c, "/api/admin/ocr");
        Assert.True(st.GetProperty("digitReader").GetProperty("available").GetBoolean());
        Assert.Equal("", st.GetProperty("digitReader").GetProperty("problem").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, st.GetProperty("vision").ValueKind);
        Assert.Contains("غير متاح", st.GetProperty("visionNote").GetString());
        var up = await Upload(c, "x.png", await File.ReadAllBytesAsync(Fixture("incoming-letter.png")));
        await WaitIdle(c, 180);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        Assert.Contains("طلب بيانات المشاريع", d.GetProperty("text").GetString());
        Assert.Contains("غير متاح", d.GetProperty("processingError").GetString());
    }

    [Fact]
    public async Task Benchmark_measures_both_engines_on_the_bundled_samples()
    {
        var samples = OcrBenchmark.Samples();
        Assert.Equal(6, samples.Count);
        Assert.Equal(new[] { "clean", "mild", "mild", "moderate", "moderate", "severe" }, samples.Select(s => s.Level));
        // خادم يرد بالنص الصحيح لكل عينة بترتيبها: يحاكي محركاً مرئياً متقناً لاختبار المسار والقياس.
        using var server = new FakeVisionServer { Reply = n => samples[(n - 1) % samples.Count].Text };
        using var f = new PlatformFactory();
        var c = await Configure(f, server);
        await Json(await Send(c, HttpMethod.Post, "/api/admin/ocr/benchmark"));
        var until = DateTime.UtcNow.AddMinutes(8);
        var st = await Get(c, "/api/admin/ocr");
        while (st.GetProperty("running").GetBoolean() && DateTime.UtcNow < until) { await Task.Delay(500); st = await Get(c, "/api/admin/ocr"); }
        Assert.Equal("", st.GetProperty("error").GetString());
        var engines = st.GetProperty("benchmark").GetProperty("engines").EnumerateArray().ToList();
        foreach (var e in engines) _out.WriteLine(e.ToString());
        var tess = engines.Single(e => e.GetProperty("mode").GetString() == "tesseract");
        var auto = engines.Single(e => e.GetProperty("mode").GetString() == "auto");
        Assert.Equal(0, auto.GetProperty("cer").GetDouble());
        Assert.Equal(18, auto.GetProperty("fields").GetInt32());
        Assert.Equal(0, auto.GetProperty("silentWrong").GetInt32());
        Assert.True(tess.GetProperty("cer").GetDouble() > 0);
        Assert.Contains("qwen3-vl:4b", auto.GetProperty("engine").GetString());
    }
}
