using System.Net;
using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using Xunit;
using Xunit.Abstractions;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>سجل الالتزامات: استخراج الوعود، وموعدها، وترجيح الوفاء بها، وقرارات المستخدم.</summary>
public class CommitmentTests
{
    private readonly ITestOutputHelper _out;
    public CommitmentTests(ITestOutputHelper o) => _out = o;

    // مجموعة التصميم: كُتبت قبل القواعد.
    public static readonly string[] Promises =
    {
        "وسنوافيكم بالبيان المطلوب خلال أسبوعين من تاريخه.",
        "سنفيدكم بما يستجد في هذا الشأن.",
        "وسيتم تزويدكم بنسخة من المحضر فور اعتماده.",
        "علماً بأنه جارٍ العمل على استكمال الإجراءات اللازمة.",
        "وسوف نقوم بإرسال الفريق الفني يوم الأحد القادم.",
        "ستقوم الإدارة برفع التقرير إلى معاليه في موعد أقصاه 1448/06/01هـ.",
        "وسيتم الرد على استفساراتكم كتابياً خلال عشرة أيام عمل.",
        "نتعهد بسداد المبلغ المستحق قبل نهاية الشهر الجاري.",
        "وسيتم صرف المستحقات حال ورود الاعتماد المالي.",
        "سنعمل على معالجة الملاحظات الواردة في تقريركم.",
        "وتلتزم الشركة بتسليم الأجهزة خلال (30) يوماً.",
        "وسنتواصل معكم لتحديد موعد الاجتماع.",
        "سيتم إصلاح العطل خلال 48 ساعة.",
        "وجارٍ إعداد الدراسة المطلوبة، وسنرفعها لسعادتكم عند اكتمالها.",
        // أُضيفتا بعد قياس العمياء الثانية (كانتا فائتاً وزائداً فيها).
        "ونعدكم بموافاتكم بالرد خلال يومي عمل.",
    };

    public static readonly string[] NotPromises =
    {
        "نأمل التكرم بتزويدنا بالبيان المطلوب خلال أسبوعين.",
        "وفي حال عدم التقيد سيتم اتخاذ الإجراءات النظامية بحق المخالفين.",
        "سيتم تطبيق اللائحة على جميع الموظفين اعتباراً من الشهر القادم.",
        "تم تزويدكم بالبيان بموجب خطابنا السابق.",
        "سيارة الإدارة في الصيانة منذ أسبوع.",
        "يرجى إرسال المستندات المطلوبة.",
        "سيتم إيقاف النظام للصيانة يوم الخميس.",
        "وقد أُحيل الموضوع إلى الإدارة القانونية.",
        "سعادة مدير عام الشؤون المالية المحترم",
        "ستُفرض غرامة تأخير وفق العقد.",
        "وتلتزم الجهات الحكومية بالتعليمات المنظمة لذلك.",
        "وما لم تُستكمل الإجراءات فسيعاد الطلب إلى مقدمه.",
    };

    [Fact]
    public void Design_set_promises_are_found_and_non_promises_ignored()
    {
        var missed = Promises.Where(p => CommitmentLedger.Extract(p).Count != 1).ToList();
        var falsePositives = NotPromises.Where(p => CommitmentLedger.Extract(p).Count != 0).ToList();
        foreach (var m in missed) _out.WriteLine("فائت: " + m);
        foreach (var f in falsePositives) _out.WriteLine("زائد: " + f + " ← " + CommitmentLedger.Extract(f)[0].Promise);
        Assert.Empty(missed);
        Assert.Empty(falsePositives);
    }

    // مجموعة عمياء: كُتبت بعد القواعد وقيست مرة واحدة دون تعديل؛ تُطبع نتيجتها ولا تُفرض.
    public static readonly string[] BlindPromises =
    {
        "وسنبادر بإبلاغكم بنتيجة الدراسة فور الانتهاء منها.",
        "ونفيدكم بأنه سيتم عقد اجتماع تنسيقي الأسبوع القادم.",
        "وسيُرفع لسعادتكم تقرير مفصل خلال شهر.",
        "سنحرص على تزويدكم بالمستجدات أولاً بأول.",
        "وستتولى الإدارة المختصة معالجة الملاحظات.",
        "وسوف يتم موافاتكم بالعرض المالي المحدث.",
        "كما سنعيد النظر في الطلب بعد استكمال المستندات.",
        "وسيتم الانتهاء من أعمال الصيانة قبل نهاية الشهر.",
        "ونؤكد لكم أن الفريق سيباشر العمل صباح الأحد.",
        "وسنوجه الإدارة المعنية بسرعة الرد على استفساراتكم.",
        "ستصلكم النسخة الأصلية من الشهادة بالبريد الرسمي.",
        "وبإذن الله سيتم تسليم الموقع للمقاول خلال عشرة أيام.",
    };

    public static readonly string[] BlindNotPromises =
    {
        "ونأمل منكم موافاتنا بالعرض المالي المحدث.",
        "سيتم احتساب الغياب دون عذر من رصيد الإجازات.",
        "تم عقد الاجتماع التنسيقي يوم الأحد الماضي.",
        "سيكون الدوام في شهر رمضان من الساعة العاشرة صباحاً.",
        "لذا يرجى التكرم بالرد في أقرب وقت.",
        "ستجدون مرفقاً صورة القرار.",
        "وتقبلوا خالص التحية والتقدير.",
        "وسيلة التواصل المعتمدة هي البريد الرسمي.",
        "ستكون الأولوية للطلبات المكتملة.",
        "إن لم تُستكمل المستندات سيتم إلغاء الطلب.",
    };

    // المجموعة العمياء الثانية: كُتبت بعد آخر تعديل للقواعد، وتُقاس مرة واحدة.
    public static readonly string[] Blind2Promises =
    {
        "وسنزود سعادتكم بنسخة من العقد بعد توقيعه من الطرفين.",
        "ونحيطكم بأن الإدارة ستنهي إجراءات التعيين خلال الأسبوع القادم.",
        "وسيجري تركيب الأجهزة في القاعة الرئيسية قبل موعد الحفل.",
        "وسوف نرفع لكم المقترح النهائي فور اعتماده من اللجنة.",
        "وجاري التنسيق مع الجهات ذات العلاقة لتحديد موعد الزيارة.",
        "كما سيتولى مكتب المشاريع متابعة التنفيذ ورفع التقارير الشهرية.",
        "وتتعهد المؤسسة بإعادة المبالغ المصروفة دون وجه حق.",
        "وسيتم إشعاركم بموعد المقابلة عن طريق البريد الإلكتروني.",
        "ونطمئنكم بأننا سنعالج الخلل في أقرب وقت ممكن.",
        "وستقوم الشركة باستبدال القطع التالفة على نفقتها.",
        "وسنعمم ذلك على فروعنا للعمل بموجبه.",
        "علماً بأن الفريق سيزور الموقع يوم الثلاثاء للمعاينة.",
        "وسنرسل لكم الكشوف المعدلة متى ما اكتملت المراجعة.",
        "ونعدكم بموافاتكم بالرد خلال يومي عمل.",
        "وبعد اكتمال الدراسة سنبلغكم بالنتيجة.",
    };

    public static readonly string[] Blind2NotPromises =
    {
        "نرفق لسعادتكم السيرة الذاتية للمرشح.",
        "تبدأ السنة المالية الجديدة في الأول من يناير.",
        "يمتد العقد لمدة ستة أشهر قابلة للتجديد.",
        "ساعات العمل الرسمية من السابعة والنصف صباحاً.",
        "ونأمل من سعادتكم توجيه من يلزم بسرعة الإنجاز.",
        "وفي حالة التأخير سيتم تطبيق غرامة التأخير المنصوص عليها.",
        "وقد سبق أن زودناكم بالبيان بموجب خطابنا رقم 4512.",
        "تم تسليم الأجهزة للإدارة المختصة.",
        "سيادتكم أعلم بما يحقق المصلحة العامة.",
        "سيتم العمل بهذا التعميم اعتباراً من تاريخه.",
        "وما لم تُستكمل الإجراءات فسيعاد الطلب إلى مقدمه.",
        "ستلاحظون أن البيان يتضمن جميع الفروع.",
        "يرجى العلم بأن سجل الحضور إلكتروني.",
        "السياسة المعتمدة توجب توثيق جميع المراسلات.",
        "اطلعنا على خطابكم وما تضمنه من ملاحظات.",
    };

    [Fact]
    public void Blind_set_2_is_reported()
    {
        var found = Blind2Promises.Count(p => CommitmentLedger.Extract(p).Count == 1);
        var falsePositives = Blind2NotPromises.Count(p => CommitmentLedger.Extract(p).Count > 0);
        foreach (var p in Blind2Promises.Where(p => CommitmentLedger.Extract(p).Count == 0)) _out.WriteLine("فائت: " + p);
        foreach (var p in Blind2NotPromises.Where(p => CommitmentLedger.Extract(p).Count > 0)) _out.WriteLine("زائد: " + p + " ← " + CommitmentLedger.Extract(p)[0].Promise);
        _out.WriteLine($"العمياء2: التقط {found} من {Blind2Promises.Length} وعداً، وزائد {falsePositives} من {Blind2NotPromises.Length}");
    }

    [Fact]
    public void Blind_set_is_reported()
    {
        var found = BlindPromises.Count(p => CommitmentLedger.Extract(p).Count == 1);
        var falsePositives = BlindNotPromises.Count(p => CommitmentLedger.Extract(p).Count > 0);
        foreach (var p in BlindPromises.Where(p => CommitmentLedger.Extract(p).Count == 0)) _out.WriteLine("فائت: " + p);
        foreach (var p in BlindNotPromises.Where(p => CommitmentLedger.Extract(p).Count > 0)) _out.WriteLine("زائد: " + p);
        _out.WriteLine($"العمياء: التقط {found} من {BlindPromises.Length} وعداً، وزائد {falsePositives} من {BlindNotPromises.Length}");
    }

    [Fact]
    public void Promise_phrase_and_offsets_point_into_the_original_text()
    {
        const string text = "السلام عليكم ورحمة الله وبركاته\nإشارةً إلى خطابكم، نفيدكم بأنه تم الاطلاع، وسنُوافيكم بالبيان المطلوب خلال أسبوعين من تاريخه.\nوتقبلوا تحياتنا";
        var p = Assert.Single(CommitmentLedger.Extract(text));
        Assert.Equal("وسنُوافيكم بالبيان المطلوب خلال أسبوعين من تاريخه", p.Promise);
        Assert.Equal(p.Sentence, text.Substring(p.Offset, p.Length));
    }

    private static Doc D(string id, string dir, string date, string text, string entity = "وزارة المالية", string status = "جديد", string created = "") => new()
    {
        Id = id, ArchiveNo = "BSR-" + id, Direction = dir, DocumentDate = date, Text = text, Title = id, Status = status, CreatedAt = created == "" ? date + "T08:00:00Z" : created,
        SourceEntity = dir == "وارد" ? entity : "", DestinationEntity = dir == "صادر" ? entity : "",
    };

    [Fact]
    public void Due_dates_states_fulfilment_and_decisions()
    {
        var today = new DateTime(2026, 10, 26);
        var docs = new List<Doc>
        {
            D("o1", "صادر", "2026-10-01", "وسنوافيكم بالبيان المطلوب خلال أسبوعين من تاريخه."),   // موعد من النص: 10-15 ← متأخر
            D("o2", "صادر", "2026-10-20", "سنفيدكم بما يستجد."),                                      // افتراضي: 11-19 ← قائم
            D("i1", "وارد", "2026-09-01", "وسيتم تزويدكم بنسخة من المحضر فور اعتماده."),             // وارد منهم لاحقاً ← موفّى
            D("i2", "وارد", "2026-09-20", "مرفق المحضر المعتمد."),
            D("o3", "صادر", "2026-10-02", "وسنرسل الفريق الفني يوم الأحد القادم."),                   // رد لاحق مسودة لا يُعد وفاءً
            D("o4", "صادر", "2026-10-10", "تم إرسال الفريق.", status: ArchiveIndex.PendingApproval),
            D("x1", "داخلي", "2026-10-01", "سنرفع التقرير للإدارة العليا."),
        };
        var threadOf = new Dictionary<string, string> { ["o1"] = "T1", ["o2"] = "T2", ["i1"] = "T3", ["i2"] = "T3", ["o3"] = "T4", ["o4"] = "T4", ["x1"] = "T5" };
        var none = new Dictionary<string, CommitmentDecision>();
        var list = CommitmentLedger.Build(docs, threadOf, new List<Edge>(), none, 30, today);
        Assert.Equal(4, list.Count);
        var c1 = list.Single(c => c.DocumentId == "o1");
        Assert.Equal((CommitmentLedger.Ours, "2026-10-15", "overdue", false, -11), (c1.Side, c1.Due, c1.State, c1.DefaultDue, c1.DaysLeft));
        Assert.Equal("وزارة المالية", c1.Party);
        var c2 = list.Single(c => c.DocumentId == "o2");
        Assert.Equal(("2026-11-19", "open", true), (c2.Due, c2.State, c2.DefaultDue));
        var c3 = list.Single(c => c.DocumentId == "i1");
        Assert.Equal((CommitmentLedger.Theirs, "fulfilled", "i2"), (c3.Side, c3.State, c3.FulfilledBy));
        var c4 = list.Single(c => c.DocumentId == "o3");
        Assert.Equal(("open", (string?)null), (c4.State, c4.FulfilledBy)); // المسودة اللاحقة لا تُعد وفاءً
        Assert.Equal("overdue", list[0].State); // المتأخر أولاً

        // قرارات المستخدم: «تم الوفاء»، و«ليس التزاماً» يُخرجه، و«قائم» يلغي ترجيح الوفاء الآلي.
        var decisions = new Dictionary<string, CommitmentDecision>
        {
            [c1.Key] = new(c1.Key, "o1", "fulfilled", "سُلّم البيان باليد", "admin", ""),
            [c2.Key] = new(c2.Key, "o2", "dismissed", "", "admin", ""),
            [c3.Key] = new(c3.Key, "i1", "open", "المحضر المرفق غير المطلوب", "admin", ""),
        };
        list = CommitmentLedger.Build(docs, threadOf, new List<Edge>(), decisions, 30, today);
        Assert.Equal("dismissed", list.Single(c => c.DocumentId == "o2").State);
        Assert.Equal("dismissed", list[^1].State); // المستبعد آخراً
        Assert.Equal(("fulfilled", "سُلّم البيان باليد"), (list.Single(c => c.DocumentId == "o1").State, list.Single(c => c.DocumentId == "o1").FulfilledReason));
        Assert.Equal("overdue", list.Single(c => c.DocumentId == "i1").State);
        // المفتاح ثابت مع اختلاف التشكيل والمسافات.
        Assert.Equal(CommitmentLedger.KeyOf("o1", "وسنوافيكم بالبيان"), CommitmentLedger.KeyOf("o1", "وسنُوافيكم   بالبيان"));
    }

    [Fact]
    public async Task Ledger_in_the_platform_tracks_promises_both_ways()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        // وارد فيه وعد منهم، ثم ردنا وفيه وعد منا بموعد مضى.
        var theirs = await Upload(c, "in.txt", Utf8("وزارة المالية\nالرقم: 3300/م   التاريخ: 1448/01/10هـ\nالموضوع: اعتماد الميزانية التشغيلية\nنفيدكم بأنه جارٍ دراسة طلبكم، وسيتم تزويدكم بقرار الاعتماد خلال أسبوعين من تاريخه."));
        await WaitIdle(c);
        await Upload(c, "out.txt", Utf8("الرقم: 7100\nالتاريخ: 1448/01/20هـ\nالموضوع: اعتماد الميزانية التشغيلية\nإشارةً إلى خطابكم رقم (3300/م) وتاريخ 1448/01/10هـ، نشكر لكم ذلك، وسنوافيكم بالبيانات التفصيلية للبنود خلال عشرة أيام من تاريخه."), direction: "صادر");
        await WaitIdle(c);

        var ours = await Get(c, "/api/commitments?side=ours");
        var mine = Assert.Single(ours.GetProperty("items").EnumerateArray());
        Assert.Equal("overdue", mine.GetProperty("state").GetString());
        Assert.Equal("وسنوافيكم بالبيانات التفصيلية للبنود خلال عشرة أيام من تاريخه", mine.GetProperty("promise").GetString());
        Assert.Equal(theirs.GetProperty("id").GetString(), mine.GetProperty("respondsTo").GetProperty("id").GetString());
        var them = Assert.Single((await Get(c, "/api/commitments?side=theirs")).GetProperty("items").EnumerateArray());
        Assert.Equal("overdue", them.GetProperty("state").GetString());
        var dash = await Get(c, "/api/dashboard");
        Assert.Equal(1, dash.GetProperty("commitments").GetProperty("ours").GetProperty("overdue").GetInt32());
        Assert.Single((await Get(c, "/api/documents/" + theirs.GetProperty("id").GetString())).GetProperty("commitments").EnumerateArray());

        // استوديو الرد: تعقيب على وعدهم، والوفاء بوعدنا على خطابهم.
        var follow = await Get(c, $"/api/documents/{theirs.GetProperty("id").GetString()}/reply?commitment={Uri.EscapeDataString(them.GetProperty("key").GetString()!)}");
        Assert.Equal("followup", follow.GetProperty("suggestedIntent").GetString());
        Assert.Contains("«جارٍ دراسة طلبكم، وسيتم تزويدكم بقرار الاعتماد خلال أسبوعين من تاريخه»، علماً بأن الموعد المحدد لذلك", follow.GetProperty("intents")[0].GetProperty("body").GetString());
        var fulfil = await Get(c, $"/api/documents/{theirs.GetProperty("id").GetString()}/reply?commitment={Uri.EscapeDataString(mine.GetProperty("key").GetString()!)}");
        Assert.Equal("fulfil", fulfil.GetProperty("suggestedIntent").GetString());
        Assert.StartsWith("وإلحاقاً لخطابنا رقم (7100)", fulfil.GetProperty("intents")[0].GetProperty("body").GetString());

        // إصدار خطاب الوفاء من الاستوديو: يرجّح الوفاء آلياً لأنه صادر لاحق في المعاملة.
        var d = fulfil.GetProperty("draft");
        await Json(await Send(c, HttpMethod.Post, $"/api/documents/{theirs.GetProperty("id").GetString()}/reply", new
        {
            subject = d.GetProperty("subject").GetString(), addressee = d.GetProperty("addressee").GetString(), referenceLine = d.GetProperty("referenceLine").GetString(),
            body = "وإلحاقاً لخطابنا رقم (7100)، نرفق لكم البيانات التفصيلية للبنود.", signerTitle = "المدير المالي", signerName = "", outgoingNo = "7155", issue = true, markDone = false,
        }));
        mine = Assert.Single((await Get(c, "/api/commitments?side=ours&state=fulfilled")).GetProperty("items").EnumerateArray());
        Assert.Contains("صدر بعده في المعاملة", mine.GetProperty("fulfilledReason").GetString());

        // قرار يدوي: وعدهم وُفّي خارج الأرشيف؛ ولا يقرر القارئ.
        var key = Uri.EscapeDataString(them.GetProperty("key").GetString()!);
        var reader = await f.UserAsync(c, "reader2", "قارئ");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, HttpMethod.Post, "/api/commitments/" + key, new { state = "fulfilled", note = "" })).StatusCode);
        Assert.Equal("fulfilled", (await Json(await Send(c, HttpMethod.Post, "/api/commitments/" + key, new { state = "fulfilled", note = "وصل القرار بالبريد الإلكتروني" }))).GetProperty("state").GetString());
        Assert.Equal(0, (await Get(c, "/api/dashboard")).GetProperty("commitments").GetProperty("theirs").GetProperty("overdue").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Post, "/api/commitments/" + key, new { state = "maybe" })).StatusCode);
    }
}
