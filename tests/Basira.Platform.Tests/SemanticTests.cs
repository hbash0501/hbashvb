using System.Net;
using Basira.Platform.Intelligence;
using Basira.Platform.Intelligence.Semantic;
using Xunit;
using Xunit.Abstractions;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>
/// النموذج الدلالي: قراءة المقطِّع، وتشغيل ONNX، والدمج مع القواعد، والمعايرة، وسلوك المنصة بالنموذج وبدونه.
/// النموذج المستخدم هنا نموذج اختبار صغير (تمثيل بحقيبة الكلمات، tools/semantic/make-test-model.py) يختبر المسار لا الدقة.
/// </summary>
public class SemanticTests
{
    private readonly ITestOutputHelper _out;
    public SemanticTests(ITestOutputHelper output) => _out = output;

    private static string TestModel => Path.Combine(AppContext.BaseDirectory, "Fixtures", "semantic-test-model");

    [Fact]
    public void Unigram_tokenizer_adds_special_tokens_and_fuses_unknowns()
    {
        var t = HfTokenizer.Load(Path.Combine(TestModel, "tokenizer.json"));
        Assert.Equal("Unigram", t.ModelType);
        var ids = t.Encode("طلب إجازة", 64);
        Assert.Equal(0, ids[0]);
        Assert.Equal(2, ids[^1]);
        Assert.Equal(4, ids.Length); // كل كلمة معروفة قطعة واحدة
        // حروف لا يغطيها المعجم: رمز مجهول واحد لكل تتابع.
        var unknown = t.Encode("ДДД", 64);
        Assert.Equal(new long[] { 0, 3, 2 }, unknown);
        // القص يحفظ رمزي البداية والنهاية.
        var clipped = t.Encode(string.Join(' ', Enumerable.Repeat("طلب", 50)), 10);
        Assert.Equal(10, clipped.Length);
        Assert.Equal(2, clipped[^1]);
    }

    [Fact]
    public void WordPiece_tokenizer_splits_subwords_and_punctuation()
    {
        const string json = """
            {"normalizer":{"type":"BertNormalizer","lowercase":true},
             "post_processor":{"type":"BertProcessing","sep":["[SEP]",3],"cls":["[CLS]",2]},
             "model":{"type":"WordPiece","unk_token":"[UNK]","continuing_subword_prefix":"##",
               "vocab":{"[PAD]":0,"[UNK]":1,"[CLS]":2,"[SEP]":3,"play":4,"##ing":5,",":6,"عقد":7}}}
            """;
        var t = new HfTokenizer(json);
        Assert.Equal(new long[] { 2, 4, 5, 6, 7, 1, 3 }, t.Encode("Playing, عقد xyz", 32));
    }

    [Fact]
    public void Onnx_model_embeds_to_normalized_vectors_where_related_texts_are_closer()
    {
        using var m = SemanticModel.Load(TestModel);
        Assert.Equal(48, m.Dimension);
        Assert.Equal("basira-test-embedder", m.Manifest.Name);
        var a = m.Embed("طلب إجازة اضطرارية لمدة ثلاثة أيام");
        var b = m.Embed("أرجو الموافقة على إجازة اضطرارية");
        var c = m.Embed("فاتورة توريد أجهزة حاسب");
        Assert.InRange(a.Sum(x => x * x), 0.999, 1.001);
        Assert.True(SemanticModel.Cosine(a, b) > SemanticModel.Cosine(a, c));
        Assert.Equal(a, m.Embed("طلب إجازة اضطرارية لمدة ثلاثة أيام"));
    }

    [Fact]
    public void Semantic_signal_classifies_text_without_keywords_and_is_ignored_at_zero_weight()
    {
        var tax = Taxonomy.Default();
        const string text = "نأمل النظر في الموضوع المشار إليه أعلاه وإفادتنا";
        var sem = new Dictionary<string, double> { ["الشؤون القانونية"] = 0.9, ["الموارد البشرية"] = 0.1 };
        var subs = new Dictionary<string, Dictionary<string, double>> { ["الشؤون القانونية"] = new() { ["الاستشارات القانونية"] = 0.8, ["القضايا والدعاوى"] = 0.2 } };
        var r = DocumentClassifier.Classify(tax, ArchiveModel.Empty, text, "", new SemanticSignal(sem, subs, 0.6, 0.55));
        Assert.Equal("الشؤون القانونية", r.Category);
        Assert.Equal("الاستشارات القانونية", r.SubCategory);
        Assert.EndsWith("دلالي", r.Method);
        var off = DocumentClassifier.Classify(tax, ArchiveModel.Empty, text, "", new SemanticSignal(sem, subs, 0, 0.55));
        Assert.Equal(DocumentClassifier.Classify(tax, ArchiveModel.Empty, text).Category, off.Category);
        Assert.DoesNotContain("دلالي", off.Method);
    }

    [Fact]
    public async Task Platform_without_a_model_reports_where_to_put_it_and_classifies_by_rules()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var st = await Get(c, "/api/admin/semantic");
        Assert.False(st.GetProperty("loaded").GetBoolean());
        var candidates = st.GetProperty("candidates").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Contains(candidates, x => x.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal) && x.EndsWith(Path.Combine("models", "semantic"), StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Post, "/api/admin/semantic/calibrate")).StatusCode);
        var up = await Upload(c, "a.txt", Utf8("أرجو التكرم بالموافقة على منحي إجازة اضطرارية لمدة ثلاثة أيام لظروف عائلية."));
        await WaitIdle(c);
        Assert.Equal("الموارد البشرية", (await Get(c, "/api/documents/" + up.GetProperty("id").GetString())).GetProperty("document").GetProperty("category").GetString());
        // غير المدير لا يصل إلى إدارة النموذج.
        var clerk = await f.UserAsync(c, "clerk", "موظف أرشفة");
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/api/admin/semantic")).StatusCode);
    }

    [Fact]
    public async Task Download_fetches_verifies_and_activates_the_model_from_the_configured_source()
    {
        // مصدر محلي على هذا الجهاز بنفس تخطيط مستودع النموذج (onnx/model_quantized.onnx وtokenizer.json؛ config.json غائب لأنه اختياري).
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/repo/");
        listener.Start();
        var served = new List<string>();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch (Exception) { return; }
                var path = ctx.Request.Url!.AbsolutePath["/repo/".Length..];
                lock (served) served.Add(path);
                var file = path switch { "tokenizer.json" => "tokenizer.json", "onnx/model_quantized.onnx" => "model.onnx", _ => null };
                if (file is null) { ctx.Response.StatusCode = 404; ctx.Response.Close(); continue; }
                var bytes = await File.ReadAllBytesAsync(Path.Combine(TestModel, file));
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        });

        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["semantic_model_url"] = "file:///etc/" })).StatusCode);
        // مجلد محدد في الإعدادات هو وجهة التنزيل (وإلا فبجوار الملف التنفيذي).
        var folder = Path.Combine(f.Data, "models", "custom");
        await Json(await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["semantic_model_url"] = $"http://127.0.0.1:{port}/repo", ["semantic_model_path"] = folder }));
        Assert.Equal(folder, (await Get(c, "/api/admin/semantic")).GetProperty("downloadTarget").GetString());
        await Json(await Send(c, HttpMethod.Post, "/api/admin/semantic/download"));
        var until = DateTime.UtcNow.AddSeconds(60);
        var st = await Get(c, "/api/admin/semantic");
        while (st.GetProperty("download").GetProperty("running").GetBoolean() && DateTime.UtcNow < until)
        {
            await Task.Delay(100);
            st = await Get(c, "/api/admin/semantic");
        }
        Assert.Equal("", st.GetProperty("download").GetProperty("error").GetString());
        var target = st.GetProperty("download").GetProperty("completed").GetString()!;
        Assert.Equal(folder, target);
        Assert.True(File.Exists(Path.Combine(target, "model_quantized.onnx")));
        Assert.False(Directory.Exists(Path.Combine(target, ".download")));
        Assert.Contains("onnx/model_quantized.onnx", served);
        Assert.True(st.GetProperty("loaded").GetBoolean());
        Assert.Equal("multilingual-e5-small", st.GetProperty("model").GetProperty("name").GetString());
        Assert.Equal("query: ", st.GetProperty("model").GetProperty("prefix").GetString());
        listener.Stop();
    }

    [Fact]
    public async Task Placed_model_is_found_calibrated_and_used_for_new_documents()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        // نسخة من النموذج في مجلد البيانات (كما يضعه المستخدم أو يُنزَّل إليه).
        var folder = Path.Combine(f.Data, "models", "semantic");
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.GetFiles(TestModel)) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        var st = await Json(await Send(c, HttpMethod.Post, "/api/admin/semantic/reload"));
        Assert.True(st.GetProperty("loaded").GetBoolean());
        Assert.Equal(48, st.GetProperty("model").GetProperty("dimension").GetInt32());

        var cal = await Json(await Send(c, HttpMethod.Post, "/api/admin/semantic/calibrate"));
        Assert.Equal(100, cal.GetProperty("tuneCount").GetInt32());
        Assert.Equal(40, cal.GetProperty("heldOutCount").GetInt32());
        _out.WriteLine(cal.ToString());
        // المعايرة لا تختار وزناً يخفض الدقة على عينات المعايرة.
        Assert.True(cal.GetProperty("tuneFused").GetDouble() >= cal.GetProperty("tuneRules").GetDouble());
        st = await Get(c, "/api/admin/semantic");
        Assert.True(st.GetProperty("calibration").GetProperty("current").GetBoolean());

        // نموذج الاختبار لا يفهم المعنى فتُبقي المعايرة القواعد وحدها غالباً؛ يُفرض وزن هنا لاختبار مسار الدمج في المنصة.
        var classifier = (ClassificationService)f.Services.GetService(typeof(ClassificationService))!;
        var settings = (Basira.Platform.Data.SettingsStore)f.Services.GetService(typeof(Basira.Platform.Data.SettingsStore))!;
        settings.SetRaw(ClassificationService.CalibrationKey, (classifier.Calibration! with { Weight = 0.5, Temperature = 0.05 }).ToJson(), "اختبار", "معايرة");

        var up = await Upload(c, "b.txt", Utf8("أرجو التكرم بالموافقة على منحي إجازة اضطرارية لمدة ثلاثة أيام لظروف عائلية."));
        await WaitIdle(c);
        var id = up.GetProperty("id").GetString()!;
        var d = await Get(c, "/api/documents/" + id);
        Assert.Equal("الموارد البشرية", d.GetProperty("document").GetProperty("category").GetString());
        Assert.EndsWith("دلالي", d.GetProperty("classification").GetProperty("method").GetString()!);
        // متجه المستند محفوظ؛ وبعد تأكيد تصنيفه يصبح جاراً يقارَن به ما يشبهه.
        var semantic = (SemanticService)f.Services.GetService(typeof(SemanticService))!;
        Assert.Single(semantic.StoredAll(classifier.Calibration!.ModelId));
        var v = d.GetProperty("document").GetProperty("version").GetInt32();
        await Json(await Send(c, HttpMethod.Post, $"/api/documents/{id}/classification", new { action = "set", category = "الشؤون القانونية", subCategory = "الاستشارات القانونية", version = v }));
        var near = classifier.Classify("أرجو التكرم بالموافقة على منحي إجازة اضطرارية لمدة ثلاثة أيام لظروف عائلية.", "");
        Assert.Contains(near.Alternatives, a => a.Category == "الشؤون القانونية");

        // تغيير الشجرة يبطل المعايرة ويعيدها في الخلفية.
        var tax = await Get(c, "/api/taxonomy");
        var body = new { categories = tax.EnumerateArray().Select(x => new { name = x.GetProperty("name").GetString(), keywords = x.GetProperty("keywords").EnumerateArray().Select(k => k.GetString()).ToList(), subs = x.GetProperty("subs").EnumerateArray().Select(s => new { name = s.GetProperty("name").GetString(), keywords = s.GetProperty("keywords").EnumerateArray().Select(k => k.GetString()).Append("كلمة إضافية").ToList() }).ToList() }).ToList() };
        await Json(await Send(c, HttpMethod.Put, "/api/taxonomy", body));
        var until = DateTime.UtcNow.AddSeconds(60);
        while (!(await Get(c, "/api/admin/semantic")).GetProperty("calibration").GetProperty("current").GetBoolean() && DateTime.UtcNow < until)
            await classifier.WaitPreparedAsync();
        Assert.True((await Get(c, "/api/admin/semantic")).GetProperty("calibration").GetProperty("current").GetBoolean());
    }
}
