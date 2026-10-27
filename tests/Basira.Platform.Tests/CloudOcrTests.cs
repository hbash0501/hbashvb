using System.Net;
using Basira.Platform.Infrastructure;
using Basira.Platform.Processing.Ocr;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>
/// القراءة السحابية (Gemini وClaude): المفتاح مشفّر ولا يعود إلى الواجهة، ولا إرسال بلا إقرار المدير،
/// والمستندات غير العادية السرية لا تُرسل أبداً، وكل إرسال في سجل التدقيق. الخدمة هنا وهمية على هذا الجهاز.
/// </summary>
[Collection("cloud-ocr")]
public class CloudOcrTests
{
    private const string GeminiKey = "AIzaTestKey-0123456789abcdefWXYZ";
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Secrets_are_sealed_with_the_platform_key()
    {
        var box = new SecretBox(new PlatformOptions { SessionKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray() });
        var sealedValue = box.Seal(GeminiKey);
        Assert.DoesNotContain("WXYZ", sealedValue);
        Assert.NotEqual(sealedValue, box.Seal(GeminiKey)); // nonce جديد لكل حفظ
        Assert.Equal(GeminiKey, box.Open(sealedValue));
        // سر منصة آخر أو قيمة تالفة: لا مفتاح، لا استثناء.
        Assert.Equal("", new SecretBox(new PlatformOptions { SessionKey = new byte[32] }).Open(sealedValue));
        Assert.Equal("", box.Open(sealedValue[..^4] + "AAAA"));
        Assert.Equal("••••WXYZ", SecretBox.Hint(GeminiKey));
    }

    [Fact]
    public async Task Gemini_needs_consent_reads_with_its_key_and_never_gets_confidential_documents()
    {
        using var server = new FakeVisionServer { Models = new[] { "models/gemini-3.8-flash", "models/gemini-3.5-flash" }, Reply = _ => VisionOcrTests.IncomingText };
        VisionOcr.GeminiUrl = server.Url;
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();

        // لا اختيار لخدمة سحابية قبل الإقرار.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["vision_provider"] = "gemini" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Post, "/api/admin/ocr/cloud", new { provider = "gemini", key = "short" })).StatusCode);
        var saved = await Json(await Send(c, HttpMethod.Post, "/api/admin/ocr/cloud", new { provider = "gemini", key = GeminiKey, consent = true }));
        Assert.True(saved.GetProperty("hasKey").GetBoolean());
        Assert.Equal("••••WXYZ", saved.GetProperty("keyHint").GetString());
        Assert.Equal("admin", saved.GetProperty("consentBy").GetString());
        await Json(await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["vision_provider"] = "gemini" }));

        // المفتاح لا يعود في أي رد ولا يُخزن نصاً صريحاً.
        Assert.DoesNotContain(GeminiKey, await c.GetStringAsync("/api/admin/settings"));
        Assert.DoesNotContain(GeminiKey, await c.GetStringAsync("/api/admin/ocr"));
        Assert.DoesNotContain("0123456789abcdef", await c.GetStringAsync("/api/audit?take=50"));
        var st = await Get(c, "/api/admin/ocr");
        Assert.Equal("gemini-3.8-flash", st.GetProperty("vision").GetProperty("model").GetString());
        Assert.True(st.GetProperty("vision").GetProperty("cloud").GetBoolean());
        var probe = await Json(await Send(c, HttpMethod.Post, "/api/admin/ocr/probe", new { provider = "gemini" }));
        Assert.True(probe.GetProperty("ok").GetBoolean());
        Assert.Contains("gemini-3.8-flash", probe.GetProperty("models").EnumerateArray().Select(m => m.GetString()));

        var up = await Upload(c, "scan_0001.png", await File.ReadAllBytesAsync(Fixture("incoming-letter.png")));
        await WaitIdle(c, 180);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        Assert.Equal(1, server.Requests);
        Assert.Equal("Bearer " + GeminiKey, server.LastAuthorization);
        Assert.Equal("/v1/chat/completions", server.LastPath);
        Assert.Equal("low", server.LastReasoning);
        Assert.Equal(VisionOcrTests.IncomingText.Trim(), d.GetProperty("text").GetString());
        Assert.Contains(d.GetProperty("checks").EnumerateArray(), k => k.GetProperty("field").GetString() == "number" && k.GetProperty("state").GetString() == "auto");
        var audit = await c.GetStringAsync("/api/audit?take=50");
        Assert.Contains("قراءة سحابية", audit);
        Assert.Contains("Google Gemini", audit);

        // مستند «سري»: لا يُرسل، ويُقرأ على هذا الجهاز.
        var secret = await Upload(c, "scan_0002.png", await File.ReadAllBytesAsync(Fixture("incoming-letter.png")), confidentiality: "سري");
        await WaitIdle(c, 180);
        Assert.Equal(1, server.Requests);
        var sd = await Get(c, "/api/documents/" + secret.GetProperty("id").GetString());
        Assert.Contains("4521", sd.GetProperty("text").GetString());

        // سحب الإقرار يعيد القراءة إلى المحرك المحلي.
        await Json(await Send(c, HttpMethod.Post, "/api/admin/ocr/cloud", new { provider = "gemini", consent = false }));
        st = await Get(c, "/api/admin/ocr");
        Assert.Equal("local", st.GetProperty("provider").GetString());
        Assert.Equal(JsonValueKindNull, st.GetProperty("vision").ValueKind);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;

    [Fact]
    public async Task Claude_reads_through_the_official_sdk_with_refusal_fallback()
    {
        using var server = new FakeVisionServer { Models = new[] { "claude-opus-5-5", "claude-sonnet-5-5" }, Reply = _ => "```\n" + VisionOcrTests.IncomingText + "\n```" };
        VisionOcr.ClaudeUrl = server.Url[..^3];
        const string key = "sk-ant-test-0123456789abcdef";
        var o = new VisionOptions(VisionOcr.ClaudeUrl, VisionOcr.DefaultModel("claude"), Provider: "claude", ApiKey: key);

        var text = await VisionOcr.ReadAsync(Fixture("incoming-letter.png"), o, CancellationToken.None);
        Assert.Equal(VisionOcrTests.IncomingText.Trim(), text);
        Assert.Equal("/v1/messages", server.LastPath);
        Assert.True(server.LastApiKey == key || server.LastAuthorization == "Bearer " + key); // الحزمة الرسمية تحدد ترويسة المفتاح
        Assert.Equal("claude-opus-5-5", server.LastModel);
        Assert.Contains("server-side-fallback", server.LastBeta);
        Assert.Contains("Transcribe", server.LastPrompt);
        Assert.True(server.LastImageBytes > 10_000);

        var (ok, _, models) = await VisionOcr.ProbeAsync(VisionOcr.ClaudeUrl, CancellationToken.None, "claude", key);
        Assert.True(ok);
        Assert.Contains("claude-opus-5-5", models);

        server.RejectKey = true;
        var rejected = await VisionOcr.ProbeAsync(VisionOcr.ClaudeUrl, CancellationToken.None, "claude", key);
        Assert.False(rejected.Ok);
        Assert.Contains("مرفوض", rejected.Message);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => VisionOcr.ReadAsync(Fixture("incoming-letter.png"), o, CancellationToken.None));
        Assert.Contains("مرفوض", ex.Message);
        // بلا مفتاح لا طلب أصلاً.
        await Assert.ThrowsAsync<InvalidOperationException>(() => VisionOcr.ReadAsync(Fixture("incoming-letter.png"), o with { ApiKey = "" }, CancellationToken.None));
    }
}
