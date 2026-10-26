using Basira.Platform.Processing;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>العنوان من المحتوى في كل الأحوال: رفض عناوين القراءة الرديئة، وعدم إبقاء أسماء ملفات السكانر.</summary>
public class TitleTests
{
    [Fact]
    public void Garbled_ocr_candidates_are_rejected_for_the_next_readable_one()
    {
        Assert.False(TitleBuilder.Plausible("تج nd ge oak دراسة الموضوع م اداه قل"));
        Assert.False(TitleBuilder.Plausible("جا داعني ا ار"));
        Assert.False(TitleBuilder.Plausible("» ا ل لتر S رق"));
        Assert.True(TitleBuilder.Plausible("طلب نيانات المشاريع المتعترة"));
        Assert.True(TitleBuilder.Plausible("محضر اجتماع لجنة المشتريات رقم 5"));
        Assert.True(TitleBuilder.Plausible("ترقية نظام ERP في الإدارة"));
        // سطر أول تالف ثم جملة مقروءة.
        const string text = "L) / _ تج nd ge oak دراسة م اداه قل\nنفيدكم بأنه تمت دراسة طلب تمديد عقد صيانة المصاعد وتمت الموافقة عليه.";
        Assert.Equal("دراسة طلب تمديد عقد صيانة المصاعد وتمت الموافقة عليه", TitleBuilder.Build(text));
    }

    [Fact]
    public void Subject_label_misread_by_ocr_is_still_found()
    {
        Assert.Equal("طلب بيانات المشاريع المتعثرة", LetterFields.Parse("جامعة الملك سعود\nالموضوعخ: طلب بيانات المشاريع المتعثرة").Subject);
        Assert.Equal("اعتماد الميزانية", LetterFields.Parse("الموصوع: اعتماد الميزانية").Subject);
        Assert.Equal("اعتماد الميزانية", LetterFields.Parse("| الموضوع : اعتماد الميزانية").Subject);
    }

    [Theory]
    [InlineData("scan_0042", false)]
    [InlineData("IMG_20261026_101500", false)]
    [InlineData("WhatsApp Image 2026-10-26 at 10.15.00", false)]
    [InlineData("Document (3)", false)]
    [InlineData("صورة 1", false)]
    [InlineData("مستند جديد", false)]
    [InlineData("عقد صيانة المصاعد", true)]
    [InlineData("خطاب وزارة المالية 3300", true)]
    [InlineData("budget approval letter", true)]
    public void Scanner_file_names_are_not_titles(string stem, bool meaningful) => Assert.Equal(meaningful, TitleBuilder.MeaningfulFileName(stem));

    [Fact]
    public void Every_document_gets_a_title_never_a_scanner_name()
    {
        Assert.Equal("طلب تمديد مدة العقد", TitleBuilder.ForDocument("الموضوع: طلب تمديد مدة العقد\nنأمل التمديد.", "", "scan_0042.jpg", "وارد", "", "", "", ""));
        Assert.Equal("عقد صيانة المصاعد", TitleBuilder.ForDocument("", "", "عقد_صيانة_المصاعد.pdf", "وارد", "", "", "", ""));
        Assert.Equal("خطاب وارد من وزارة المالية — المشتريات والعقود — 1448/04/01هـ",
            TitleBuilder.ForDocument("ضجيج غير مقروء xx", "", "IMG_2026.jpg", "وارد", "وزارة المالية", "المشتريات والعقود", "", "1448/04/01هـ"));
        Assert.Equal("خطاب وارد مصوّر لم يُقرأ نصه — 1448/05/15هـ", TitleBuilder.ForDocument("", "", "scan_0042.jpg", "وارد", "", "", "", "1448/05/15هـ"));
    }

    [Fact]
    public async Task Ingested_documents_never_keep_scanner_names_and_old_titles_can_be_rebuilt()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var unreadable = await Upload(c, "scan_0042.txt", Utf8("xx 12 / ؟؟ 7"));
        var named = await Upload(c, "عقد صيانة المصاعد.txt", Utf8("xx 13 / ؟؟ 8"));
        var letter = await Upload(c, "IMG_20261026.txt", Utf8("وزارة المالية\nالموضوع: اعتماد الميزانية التشغيلية\nنرفق لكم مشروع الميزانية."));
        await WaitIdle(c);
        string Title(System.Text.Json.JsonElement d) => d.GetProperty("document").GetProperty("title").GetString()!;
        Assert.Equal("خطاب وارد مصوّر لم يُقرأ نصه", Title(await Get(c, "/api/documents/" + unreadable.GetProperty("id").GetString())));
        Assert.Equal("عقد صيانة المصاعد", Title(await Get(c, "/api/documents/" + named.GetProperty("id").GetString())));
        Assert.Equal("اعتماد الميزانية التشغيلية", Title(await Get(c, "/api/documents/" + letter.GetProperty("id").GetString())));

        // مستند قديم بقي عنوانه اسم الملف (كما في الأرشيف قبل هذا الإصدار): يُعاد توليده؛ وعنوان كتبه مستخدم لا يُمس.
        var id = letter.GetProperty("id").GetString()!;
        var v = (await Get(c, "/api/documents/" + id)).GetProperty("document").GetProperty("version").GetInt32();
        await Json(await Send(c, HttpMethod.Put, "/api/documents/" + id, new { version = v, title = "IMG_20261026" }));
        var nid = named.GetProperty("id").GetString()!;
        v = (await Get(c, "/api/documents/" + nid)).GetProperty("document").GetProperty("version").GetInt32();
        await Json(await Send(c, HttpMethod.Put, "/api/documents/" + nid, new { version = v, title = "عنوان يدوي" }));
        var r = await Json(await Send(c, HttpMethod.Post, "/api/titles/rebuild"));
        Assert.Equal(1, r.GetProperty("changed").GetInt32());
        Assert.Equal("اعتماد الميزانية التشغيلية", Title(await Get(c, "/api/documents/" + id)));
        Assert.Equal("عنوان يدوي", Title(await Get(c, "/api/documents/" + nid)));
    }
}
