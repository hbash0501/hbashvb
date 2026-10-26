using System.Net;
using Basira.Platform.Intelligence;
using Basira.Platform.Processing;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>استوديو الرد: استخراج المطلوب، وسطر الإشارة، والسوابق، ودورة المسودة والاعتماد والإصدار وأثرها على «بانتظار الرد».</summary>
public class ReplyStudioTests
{
    private const string OldRequest = """
        وزارة المالية
        الرقم: 4410/م   التاريخ: 1448/02/03هـ
        الموضوع: طلب بيان بالوظائف الشاغرة
        سعادة مدير عام الموارد البشرية المحترم
        السلام عليكم ورحمة الله وبركاته، وبعد:
        نأمل التكرم بتزويدنا ببيان الوظائف الشاغرة لديكم موزعة حسب المرتبة، وذلك في موعد أقصاه أسبوعان من تاريخه.
        وتقبلوا تحياتنا،،،
        مدير عام الميزانية
        """;

    private const string OldReply = """
        الرقم: 9001
        التاريخ: 1448/02/10هـ
        الموضوع: طلب بيان بالوظائف الشاغرة
        سعادة مدير عام الميزانية بوزارة المالية    المحترم
        السلام عليكم ورحمة الله وبركاته، وبعد:
        إشارةً إلى خطابكم رقم (4410/م) وتاريخ 1448/02/03هـ بشأن طلب بيان بالوظائف الشاغرة، نرفق لكم بيان الوظائف الشاغرة لدينا موزعة حسب المرتبة كما في 1448/02/09هـ، وعددها 37 وظيفة، ونأمل الإحاطة.
        وتقبلوا خالص التحية والتقدير،،،
        مدير عام الموارد البشرية
        """;

    private const string NewRequest = """
        وزارة المالية
        الرقم: 5120/م   التاريخ: 1448/04/01هـ
        الموضوع: تحديث بيان الوظائف الشاغرة
        سعادة مدير عام الموارد البشرية المحترم
        السلام عليكم ورحمة الله وبركاته، وبعد:
        نأمل التكرم بتزويدنا ببيان محدث بالوظائف الشاغرة لديكم حسب المرتبة. وهل تم شغل أي منها منذ البيان السابق؟
        وتقبلوا تحياتنا،،،
        مدير عام الميزانية
        """;

    [Fact]
    public void Asks_intent_reference_and_signer_are_read_from_the_letter()
    {
        var asks = ReplyComposer.ExtractAsks(NewRequest);
        Assert.Equal(2, asks.Count);
        Assert.Equal("معلومات", asks[0].Kind);
        Assert.StartsWith("تزويدنا ببيان محدث بالوظائف الشاغرة", asks[0].Gist);
        Assert.Equal("سؤال", asks[1].Kind);
        Assert.Equal("inform", ReplyComposer.SuggestIntent(NewRequest, asks));
        Assert.Contains("1. بخصوص تزويدنا ببيان محدث", ReplyComposer.Intents(asks).Single(i => i.Key == "inform").Body);

        Assert.Equal("approve", ReplyComposer.SuggestIntent("نأمل التكرم بالموافقة على منح الموظف إجازة اضطرارية.", ReplyComposer.ExtractAsks("نأمل التكرم بالموافقة على منح الموظف إجازة اضطرارية.")));
        Assert.Equal("acknowledge", ReplyComposer.SuggestIntent("نحيطكم علماً بأن الدوام سيبدأ الساعة الثامنة.", ReplyComposer.ExtractAsks("نحيطكم علماً بأن الدوام سيبدأ الساعة الثامنة.")));
        Assert.Contains("نفيدكم بالموافقة على منح الموظف إجازة اضطرارية", ReplyComposer.Intents(ReplyComposer.ExtractAsks("نأمل التكرم بالموافقة على منح الموظف إجازة اضطرارية.")).Single(i => i.Key == "approve").Body);

        Assert.Equal("إشارةً إلى خطابكم رقم (5120/م) وتاريخ 1448/04/01هـ بشأن تحديث بيان الوظائف الشاغرة",
            ReplyComposer.ReferenceLine("5120/م", "", "1448/04/01هـ", "تحديث بيان الوظائف الشاغرة"));
        Assert.Equal("إشارةً إلى خطابكم المقيد لدينا برقم (BSR-2026-00007) بشأن x", ReplyComposer.ReferenceLine("", "BSR-2026-00007", "", "x"));

        // المهلة ليست من خلاصة الطلب، والطلب الموقوت يبقى طلب معلومات.
        var timed = ReplyComposer.ExtractAsks("إلحاقاً لخطابنا رقم (4410/م)، نأمل التكرم بتزويدنا ببيان محدث بالوظائف الشاغرة حسب المرتبة، في موعد أقصاه عشرة أيام من تاريخه.");
        Assert.Equal("تزويدنا ببيان محدث بالوظائف الشاغرة حسب المرتبة", Assert.Single(timed).Gist);
        Assert.Equal("معلومات", timed[0].Kind);

        Assert.Equal("مدير عام الميزانية", ReplyComposer.SignerTitle(NewRequest));
        Assert.Equal("سعادة مدير عام الميزانية بوزارة المالية", ReplyComposer.AddresseeLine("مدير عام الميزانية", "وزارة المالية"));
        Assert.Equal("سعادة/ [المخاطَب]", ReplyComposer.AddresseeLine("", ""));
    }

    [Fact]
    public void Composed_reply_reads_back_as_a_letter_and_precedents_are_adapted()
    {
        var text = ReplyComposer.Compose(new ReplyLetter("7788", "1448/05/04هـ", "تحديث بيان الوظائف الشاغرة", "سعادة مدير عام الميزانية",
            "إشارةً إلى خطابكم رقم (5120/م) وتاريخ 1448/04/01هـ بشأن تحديث بيان الوظائف الشاغرة", "نرفق لكم البيان المحدث.", "مدير عام الموارد البشرية", "سعد"));
        var header = LetterFields.Parse(text);
        Assert.Equal("7788", header.Number);
        Assert.Equal("تحديث بيان الوظائف الشاغرة", header.Subject);
        Assert.Contains("1448/04/01هـ بشأن تحديث بيان الوظائف الشاغرة، نرفق لكم البيان المحدث.", text);
        Assert.EndsWith("مدير عام الموارد البشرية\nسعد", text);
        Assert.Equal(new[] { "[الإفادة]" }, ReplyComposer.Placeholders("1. بخصوص كذا: [الإفادة]. 2. [الإفادة]."));

        // الرد السابق بلا ترويسة ولا إشارة ولا ختام، وتاريخه ورقم الخطاب القديم حقول للمراجعة.
        var adapted = ReplyComposer.AdaptPrecedent(OldReply, OldRequest, new[] { "طلب بيان بالوظائف الشاغرة" }, new[] { "تحديث بيان الوظائف الشاغرة" });
        Assert.Equal("نرفق لكم بيان الوظائف الشاغرة لدينا موزعة حسب المرتبة كما في [التاريخ]، وعددها [37] وظيفة، ونأمل الإحاطة.", adapted);

        // ألفاظ الترويسة والتحية وحدها لا تجعل خطابين متشابهين.
        var boiler = ReplyComposer.Rank(NewRequest, new[] { "وزارة المالية\nسعادة مدير عام الشؤون المالية المحترم\nنأمل التكرم بإفادتنا عن مسوغات صرف بدل الانتداب.\nمدير عام المراجعة" });
        Assert.DoesNotContain("مدير", boiler[0].Shared);
        Assert.True(boiler[0].Score < 0.12, boiler[0].Score.ToString());
        var ranked = ReplyComposer.Rank(NewRequest, new[] { "دعوة لحضور حفل التكريم السنوي للموظفين المتميزين", OldRequest });
        Assert.Equal(1, ranked[0].Index);
        Assert.Contains("الشاغرة", ranked[0].Shared);
        Assert.True(ranked[0].Score > 0.3 && ranked[1].Score < 0.05, $"{ranked[0].Score} {ranked[1].Score}");
    }

    [Fact]
    public async Task Draft_then_issue_links_the_reply_and_closes_awaiting()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        await Upload(c, "old.txt", Utf8(OldRequest));
        await WaitIdle(c);
        await Upload(c, "old-reply.txt", Utf8(OldReply), direction: "صادر");
        var incoming = await Upload(c, "new.txt", Utf8(NewRequest));
        await WaitIdle(c);
        var id = incoming.GetProperty("id").GetString()!;
        Assert.Equal(JsonValueKindNumber, (await Get(c, "/api/documents/" + id)).GetProperty("awaitingDays").ValueKind);

        var draft = await Get(c, $"/api/documents/{id}/reply");
        Assert.Equal("إشارةً إلى خطابكم رقم (5120/م) وتاريخ 1448/04/01هـ بشأن تحديث بيان الوظائف الشاغرة", draft.GetProperty("draft").GetProperty("referenceLine").GetString());
        Assert.Equal("inform", draft.GetProperty("suggestedIntent").GetString());
        Assert.Equal(2, draft.GetProperty("asks").GetArrayLength());
        var precedent = Assert.Single(draft.GetProperty("precedents").EnumerateArray());
        Assert.Contains("نرفق لكم بيان الوظائف الشاغرة", precedent.GetProperty("body").GetString());
        Assert.Contains("[التاريخ]", precedent.GetProperty("body").GetString());

        var letter = new
        {
            subject = draft.GetProperty("draft").GetProperty("subject").GetString(),
            addressee = draft.GetProperty("draft").GetProperty("addressee").GetString(),
            referenceLine = draft.GetProperty("draft").GetProperty("referenceLine").GetString(),
            body = "نرفق لكم البيان المحدث للوظائف الشاغرة كما في [التاريخ].",
            signerTitle = "مدير عام الموارد البشرية", signerName = "", outgoingNo = "",
            issue = false, markDone = false,
        };
        // المسودة بانتظار الاعتماد ترتبط بالمعاملة لكنها لا تُغلق انتظار الرد.
        var saved = await Json(await Send(c, HttpMethod.Post, $"/api/documents/{id}/reply", letter));
        Assert.Equal("بانتظار الاعتماد", saved.GetProperty("status").GetString());
        var detail = await Get(c, "/api/documents/" + id);
        Assert.Equal(JsonValueKindNumber, detail.GetProperty("awaitingDays").ValueKind);
        Assert.Contains((await Get(c, $"/api/documents/{id}/reply")).GetProperty("existing").EnumerateArray(), e => e.GetProperty("pending").GetBoolean());

        // الإصدار يرفض الحقول غير المستكملة، ويرفض سطر إشارة بلا رقم الخطاب.
        var r = await Send(c, HttpMethod.Post, $"/api/documents/{id}/reply", letter with { issue = true });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("[التاريخ]", await r.Content.ReadAsStringAsync());
        r = await Send(c, HttpMethod.Post, $"/api/documents/{id}/reply", letter with { issue = true, body = "نرفق لكم البيان المحدث.", referenceLine = "إشارةً إلى خطابكم بشأن البيان" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        var issued = await Json(await Send(c, HttpMethod.Post, $"/api/documents/{id}/reply", letter with { issue = true, markDone = true, body = "نرفق لكم البيان المحدث للوظائف الشاغرة.", outgoingNo = "9100" }));
        detail = await Get(c, "/api/documents/" + id);
        Assert.Equal(JsonValueKindNull, detail.GetProperty("awaitingDays").ValueKind);
        Assert.Equal("منجز", detail.GetProperty("document").GetProperty("status").GetString());
        var reply = (await Get(c, "/api/documents/" + issued.GetProperty("id").GetString())).GetProperty("document");
        Assert.Equal("صادر", reply.GetProperty("direction").GetString());
        Assert.Equal("9100", reply.GetProperty("outgoingNo").GetString());
        Assert.Equal("وزارة المالية", reply.GetProperty("destinationEntity").GetString());
        Assert.Equal(detail.GetProperty("document").GetProperty("category").GetString(), reply.GetProperty("category").GetString());
        Assert.Equal(detail.GetProperty("document").GetProperty("thread").GetString(), reply.GetProperty("thread").GetString());
    }

    [Fact]
    public async Task Letter_without_numbers_is_registered_so_the_reply_links_to_it()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var up = await Upload(c, "plain.txt", Utf8("السلام عليكم ورحمة الله وبركاته\nأرجو التكرم بالموافقة على منحي إجازة اضطرارية لمدة ثلاثة أيام لظروف عائلية.\nمقدمه: سعد"));
        await WaitIdle(c);
        var id = up.GetProperty("id").GetString()!;
        var draft = await Get(c, $"/api/documents/{id}/reply");
        var archiveNo = draft.GetProperty("incoming").GetProperty("archiveNo").GetString()!;
        Assert.True(draft.GetProperty("incoming").GetProperty("willRegister").GetBoolean());
        Assert.Contains($"المقيد لدينا برقم ({archiveNo})", draft.GetProperty("draft").GetProperty("referenceLine").GetString());
        Assert.Equal("approve", draft.GetProperty("suggestedIntent").GetString());
        var body = draft.GetProperty("intents").EnumerateArray().Single(i => i.GetProperty("key").GetString() == "approve").GetProperty("body").GetString();

        await Json(await Send(c, HttpMethod.Post, $"/api/documents/{id}/reply", new
        {
            subject = "إجازة اضطرارية", addressee = "المكرم/ سعد", referenceLine = draft.GetProperty("draft").GetProperty("referenceLine").GetString(), body,
            signerTitle = "مدير الموارد البشرية", signerName = "", outgoingNo = "", issue = true, markDone = false,
        }));
        var doc = await Get(c, "/api/documents/" + id);
        Assert.Equal(archiveNo, doc.GetProperty("document").GetProperty("incomingNo").GetString());
        Assert.Single(doc.GetProperty("citedBy").EnumerateArray());

        // لا رد على صادر، ولا صلاحية للقارئ.
        var reader = await f.UserAsync(c, "reader1", "قارئ");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/documents/{id}/reply")).StatusCode);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNumber = System.Text.Json.JsonValueKind.Number;
    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;
}
