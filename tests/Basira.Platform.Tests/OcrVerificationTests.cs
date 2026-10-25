using System.Globalization;
using System.Net;
using System.Text.Json;
using AIDocumentArchiveVB;
using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using Basira.Platform.Processing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>طبقات دقة OCR: بوابة الجودة، وقارئ الأرقام الهندية، وطابور التحقق، وتعليق القرارات على الحقول غير المؤكدة.</summary>
public class OcrVerificationTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static bool HasTesseract => TextExtractor.Find("", "", "ara+eng").Tesseract is not null;

    private static async Task<string> CreateDoc(HttpClient c, object body) =>
        (await Json(await Send(c, HttpMethod.Post, "/api/documents", body))).GetProperty("id").GetString()!;

    /// <summary>يُدرج فحصاً معلقاً مباشرة (كما يكتبه طابور المعالجة) ثم ينبّه اللقطة.</summary>
    private static long AddPendingCheck(PlatformFactory f, string docId, string field, string target, string value, string text, string span)
    {
        var offset = text.IndexOf(span, StringComparison.Ordinal);
        Assert.True(offset >= 0);
        long id;
        using (var db = new SqliteConnection("Data Source=" + Path.Combine(f.Data, "basira.db") + ";Pooling=False"))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO field_checks(document_id, field, target, value, candidates, state, reason, text_offset, text_length, span_text, created_at)
                VALUES($d, $f, $t, $v, '[]', 'pending', 'ثقة القراءة منخفضة', $o, $l, $s, '2026-10-25T00:00:00.000Z');
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$d", docId); cmd.Parameters.AddWithValue("$f", field); cmd.Parameters.AddWithValue("$t", target);
            cmd.Parameters.AddWithValue("$v", value); cmd.Parameters.AddWithValue("$o", offset); cmd.Parameters.AddWithValue("$l", span.Length);
            cmd.Parameters.AddWithValue("$s", span);
            id = (long)cmd.ExecuteScalar()!;
        }
        f.Services.GetRequiredService<ReviewStore>().NotifyChanged();
        return id;
    }

    [Fact]
    public async Task Pending_reference_neither_links_nor_creates_ghost_until_corrected()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var a = await CreateDoc(c, new { title = "طلب بيانات", direction = "وارد", externalNo = "4521", documentDate = "1447/03/12هـ", text = "نأمل تزويدنا بالبيانات." });
        const string misread = "إشارة إلى خطاب سعادتكم رقم 4527 وتاريخ 1447/03/12هـ";
        var text = misread + " بشأن طلب البيانات، نرفق لكم المطلوب.";
        var b = await CreateDoc(c, new { title = "الرد", direction = "صادر", text });
        // قبل التحقق: الرقم المقروء 4527 غير موجود، لكنه لا يُعلن خطاباً غائباً لأن القراءة معلقة.
        var check = AddPendingCheck(f, b, "reference", "", "4527@", text, misread);
        var before = await Get(c, "/api/documents/" + b);
        var r = before.GetProperty("references").EnumerateArray().Single();
        Assert.True(r.GetProperty("pending").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("target").ValueKind);
        Assert.False(r.GetProperty("missing").GetBoolean());
        Assert.Equal(a, r.GetProperty("suggestion").GetProperty("id").GetString()); // يختلف بحرف واحد
        Assert.Equal(0, (await Get(c, "/api/missing")).GetArrayLength());
        Assert.Equal(1, before.GetProperty("document").GetProperty("pendingChecks").GetInt32());

        // الطابور يعرضه مع الاقتراح من الأرشيف، والقارئ لا يملك الحسم.
        var queue = await Get(c, "/api/review");
        var item = queue.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(a, item.GetProperty("suggestions")[0].GetProperty("id").GetString());
        var reader = await f.UserAsync(c, "reader2", "قارئ");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, HttpMethod.Post, $"/api/review/{check}", new { action = "confirm" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Post, $"/api/review/{check}", new { action = "correct", value = "نص لا إشارة فيه" })).StatusCode);

        // التصحيح يستبدل النص في موضعه، فيُربط الرد بالخطاب الصحيح.
        var res = await Json(await Send(c, HttpMethod.Post, $"/api/review/{check}", new { action = "correct", value = misread.Replace("4527", "4521") }));
        Assert.Equal(0, res.GetProperty("pending").GetInt32());
        var after = await Get(c, "/api/documents/" + b);
        Assert.Contains("رقم 4521 وتاريخ", after.GetProperty("text").GetString());
        Assert.EndsWith("نرفق لكم المطلوب.", after.GetProperty("text").GetString());
        var linked = after.GetProperty("references").EnumerateArray().Single();
        Assert.Equal(a, linked.GetProperty("target").GetProperty("id").GetString());
        Assert.Equal("corrected", after.GetProperty("checks")[0].GetProperty("state").GetString());
        Assert.Contains(after.GetProperty("revisions").EnumerateArray(), v => v.GetProperty("summary").GetString()!.StartsWith("تصحيح القراءة الآلية"));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Post, $"/api/review/{check}", new { action = "confirm" })).StatusCode);
        Assert.True((await Get(c, "/api/audit")).GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Pending_header_number_is_not_used_as_identity_until_confirmed()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        const string header = "الرقم: 4521";
        var aText = header + "\nالموضوع: طلب بيانات";
        var a = await CreateDoc(c, new { title = "طلب بيانات", direction = "وارد", externalNo = "4521", text = aText });
        var b = await CreateDoc(c, new { title = "الرد", direction = "صادر", text = "إشارة إلى خطاب سعادتكم رقم 4521 وتاريخ 1447/03/12هـ نرفق المطلوب." });
        var check = AddPendingCheck(f, a, "number", "external_no", "4521", aText, "4521");
        var r = (await Get(c, "/api/documents/" + b)).GetProperty("references").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, r.GetProperty("target").ValueKind);
        Assert.False(r.GetProperty("missing").GetBoolean());
        Assert.Equal(a, r.GetProperty("suggestion").GetProperty("id").GetString());
        Assert.Equal(0, (await Get(c, "/api/missing")).GetArrayLength());

        await Json(await Send(c, HttpMethod.Post, $"/api/review/{check}", new { action = "confirm" }));
        r = (await Get(c, "/api/documents/" + b)).GetProperty("references").EnumerateArray().Single();
        Assert.Equal(a, r.GetProperty("target").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Pending_date_does_not_start_a_deadline_until_corrected()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        const string dateSpan = "1447/03/12هـ";
        var text = "التاريخ: " + dateSpan + "\nنأمل الإفادة خلال (15) يوماً من تاريخه.";
        var id = await CreateDoc(c, new { title = "طلب", direction = "وارد", documentDate = "1447/03/12هـ", text });
        Assert.NotEqual("", (await Get(c, "/api/documents/" + id)).GetProperty("document").GetProperty("due").GetString());
        var check = AddPendingCheck(f, id, "date", "document_date", "2025-09-04", text, dateSpan);
        Assert.Equal("", (await Get(c, "/api/documents/" + id)).GetProperty("document").GetProperty("due").GetString());

        await Json(await Send(c, HttpMethod.Post, $"/api/review/{check}", new { action = "correct", value = "1447/03/20هـ" }));
        var d = (await Get(c, "/api/documents/" + id)).GetProperty("document");
        var expected = new UmAlQuraCalendar().ToDateTime(1447, 3, 20, 0, 0, 0, 0);
        Assert.Equal(expected.ToString("yyyy-MM-dd"), d.GetProperty("documentDate").GetString());
        Assert.Equal(expected.AddDays(15).ToString("yyyy-MM-dd"), d.GetProperty("due").GetString());
    }

    [Fact]
    public async Task Indic_digit_letter_is_read_and_verified_end_to_end()
    {
        if (!HasTesseract) return; // يتطلب Tesseract؛ يُثبَّت في CI.
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var up = await Upload(c, "indic-letter.png", await File.ReadAllBytesAsync(Fixture("indic-letter.png")));
        await WaitIdle(c, 180);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        var doc = d.GetProperty("document");
        // نماذج Tesseract تقرأ «٢٢٠٥٢» حروفاً لاتينية؛ قارئ الأرقام يعيدها.
        Assert.Equal("22052", CorrespondenceReferenceParser.NumberKey(doc.GetProperty("externalNo").GetString()!));
        Assert.Equal(new UmAlQuraCalendar().ToDateTime(1448, 9, 12, 0, 0, 0, 0).ToString("yyyy-MM-dd"), doc.GetProperty("documentDate").GetString());
        Assert.Contains("٩٨٢٢٤", d.GetProperty("text").GetString());
        var r = d.GetProperty("references").EnumerateArray().Single();
        Assert.Equal("98224", r.GetProperty("number").GetString());
        Assert.Equal(new UmAlQuraCalendar().ToDateTime(1448, 5, 2, 0, 0, 0, 0).ToString("yyyy-MM-dd"), r.GetProperty("date").GetString());
        Assert.Equal("", d.GetProperty("scanQuality").GetString());
        var checks = d.GetProperty("checks").EnumerateArray().ToDictionary(k => k.GetProperty("field").GetString()!, k => k.GetProperty("state").GetString());
        Assert.Equal("auto", checks["number"]);
        Assert.Equal("auto", checks["date"]);
        Assert.Equal("auto", checks["reference"]);
        // كل حقل إما مؤكد آلياً أو معروض للتحقق مع مقتطعه، ولا ثالث.
        foreach (var item in (await Get(c, "/api/review")).GetProperty("items").EnumerateArray())
        {
            var crop = await c.GetAsync($"/api/review/{item.GetProperty("check").GetProperty("id").GetInt64()}/crop");
            Assert.Equal(HttpStatusCode.OK, crop.StatusCode);
            Assert.Equal("image/png", crop.Content.Headers.ContentType!.MediaType);
        }
    }

    [Fact]
    public async Task Poor_scan_is_flagged_for_rescan_and_nothing_is_auto_confirmed()
    {
        if (!HasTesseract) return;
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        // مسح رديء: تصغير شديد ثم تكبير مع تمويه وخفض التباين.
        using var src = SKBitmap.Decode(Fixture("incoming-letter.png"));
        using var small = src.Resize(new SKImageInfo(src.Width / 4, src.Height / 4), SKFilterQuality.Low);
        using var poor = new SKBitmap(src.Width / 2, src.Height / 2);
        using (var canvas = new SKCanvas(poor))
        using (var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(2.5f, 2.5f), ColorFilter = SKColorFilter.CreateLighting(new SKColor(110, 110, 110), new SKColor(120, 120, 120)) })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(small, new SKRect(0, 0, poor.Width, poor.Height), paint);
        }
        using var data = SKImage.FromBitmap(poor).Encode(SKEncodedImageFormat.Jpeg, 40);
        var up = await Upload(c, "poor-scan.jpg", data.ToArray());
        await WaitIdle(c, 180);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        Assert.NotEqual("", d.GetProperty("scanQuality").GetString());
        Assert.Contains("إعادة المسح", d.GetProperty("processingError").GetString());
        Assert.True(d.GetProperty("document").GetProperty("scanIssues").GetBoolean());
        Assert.DoesNotContain(d.GetProperty("checks").EnumerateArray(), k => k.GetProperty("state").GetString() == "auto");
    }

    [Theory]
    [InlineData("45210", "45216", true)]
    [InlineData("4521", "452", false)]   // قصير جداً
    [InlineData("4521/ص", "4521/س", true)]
    [InlineData("98224", "98224", false)] // مطابق ليس «يختلف بحرف»
    [InlineData("12345", "54321", false)]
    public void One_edit_apart_detects_single_character_misreads(string a, string b, bool expected) =>
        Assert.Equal(expected, ArchiveIndex.OneEditApart(a, b));
}
