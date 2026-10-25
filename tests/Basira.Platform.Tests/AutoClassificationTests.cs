using System.Net;
using System.Text.Json;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>التصنيف الآلي والعنوان من المحتوى عبر المنصة: الإدراج، والتأكيد والتغيير، وعدم الكتابة فوق ما عدّله المستخدم، والتعلم.</summary>
public class AutoClassificationTests
{
    private const string Leave = "سعادة مدير الموارد البشرية المحترم\nالسلام عليكم ورحمة الله وبركاته\nأرجو التكرم بالموافقة على منحي إجازة اضطرارية لمدة ثلاثة أيام اعتباراً من يوم الأحد القادم لظروف عائلية طارئة.\nوتقبلوا تحياتي";

    [Fact]
    public async Task Uploaded_text_gets_a_content_title_and_a_category()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var up = await Upload(c, "scan_0042.txt", Utf8(Leave));
        await WaitIdle(c);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        var doc = d.GetProperty("document");
        Assert.Equal("طلب منحي إجازة اضطرارية لمدة ثلاثة أيام اعتباراً من يوم الأحد القادم", doc.GetProperty("title").GetString());
        Assert.True(doc.GetProperty("titleAuto").GetBoolean());
        Assert.Equal("الموارد البشرية", doc.GetProperty("category").GetString());
        Assert.Equal("الإجازات", doc.GetProperty("subCategory").GetString());
        Assert.True(doc.GetProperty("categoryAuto").GetBoolean());
        Assert.Contains("إجازة", d.GetProperty("classification").GetProperty("evidence").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task User_choices_win_over_automation_and_feed_accuracy_and_learning()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        // تصنيف حدده المستخدم عند الإدراج لا يُمس.
        var manual = await Upload(c, "x.txt", Utf8(Leave), category: "مراسلات");
        var auto = await Upload(c, "y.txt", Utf8(Leave + "\nمقدمه: سعد"));
        await WaitIdle(c);
        Assert.Equal("مراسلات", (await Get(c, "/api/documents/" + manual.GetProperty("id").GetString())).GetProperty("document").GetProperty("category").GetString());

        // تأكيد التصنيف الآلي يحسب في الدقة ويجعله مثالاً يتعلم منه.
        var id = auto.GetProperty("id").GetString();
        var v = (await Get(c, "/api/documents/" + id)).GetProperty("document").GetProperty("version").GetInt32();
        await Json(await Send(c, HttpMethod.Post, $"/api/documents/{id}/classification", new { action = "confirm", version = v }));
        var doc = (await Get(c, "/api/documents/" + id)).GetProperty("document");
        Assert.False(doc.GetProperty("categoryAuto").GetBoolean());

        // تعديل العنوان يدوياً ثم إعادة المعالجة: لا يُكتب فوقه.
        await Json(await Send(c, HttpMethod.Put, "/api/documents/" + id, new { version = doc.GetProperty("version").GetInt32(), title = "إجازة اضطرارية — الموظف س" }));
        await Json(await Send(c, HttpMethod.Post, $"/api/documents/{id}/reprocess"));
        await WaitIdle(c);
        doc = (await Get(c, "/api/documents/" + id)).GetProperty("document");
        Assert.Equal("إجازة اضطرارية — الموظف س", doc.GetProperty("title").GetString());
        Assert.Equal("الموارد البشرية", doc.GetProperty("category").GetString());

        var overview = await Get(c, "/api/classification/overview");
        Assert.Equal(1, overview.GetProperty("feedback").GetProperty("reviewed").GetInt32());
        Assert.Equal(100, overview.GetProperty("feedback").GetProperty("accuracy").GetDouble());
        Assert.Equal(1, overview.GetProperty("counts").GetProperty("training").GetInt32());
        Assert.Contains(overview.GetProperty("unmapped").EnumerateArray(), u => u.GetProperty("name").GetString() == "مراسلات");
    }

    [Fact]
    public async Task Taxonomy_is_editable_and_reclassify_fills_unclassified_documents()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var tax = (await Get(c, "/api/taxonomy")).EnumerateArray().Select(x => new
        {
            name = x.GetProperty("name").GetString(),
            keywords = x.GetProperty("keywords").EnumerateArray().Select(k => k.GetString()).ToList(),
            subs = x.GetProperty("subs").EnumerateArray().Select(s => new { name = s.GetProperty("name").GetString(), keywords = s.GetProperty("keywords").EnumerateArray().Select(k => k.GetString()).ToList() }).ToList(),
        }).ToList<object>();
        tax.Add(new { name = "الأوقاف", keywords = new List<string> { "وقف", "الأوقاف" }, subs = new[] { new { name = "النظارة", keywords = new List<string> { "!ناظر الوقف", "صك الوقف" } } }.ToList() });
        await Json(await Send(c, HttpMethod.Put, "/api/taxonomy", new { categories = tax }));
        // مستند أُنشئ بنص دون تصنيف يُصنف عند إنشائه.
        var id = (await Json(await Send(c, HttpMethod.Post, "/api/documents", new { text = "الموضوع: تعيين ناظر الوقف\nنفيدكم بصدور صك الوقف وتعيين ناظر الوقف الجديد." }))).GetProperty("id").GetString();
        var doc = (await Get(c, "/api/documents/" + id)).GetProperty("document");
        Assert.Equal("تعيين ناظر الوقف", doc.GetProperty("title").GetString());
        Assert.Equal("الأوقاف", doc.GetProperty("category").GetString());
        Assert.Equal("النظارة", doc.GetProperty("subCategory").GetString());
        // قارئ لا يعدّل الشجرة.
        var reader = await f.UserAsync(c, "reader3", "قارئ");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, HttpMethod.Put, "/api/taxonomy", new { categories = tax })).StatusCode);
        var r = await Json(await Send(c, HttpMethod.Post, "/api/classification/reclassify", new { scope = "unclassified" }));
        Assert.Equal(0, r.GetProperty("changed").GetInt32());
    }
}
