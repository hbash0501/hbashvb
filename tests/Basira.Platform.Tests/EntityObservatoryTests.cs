using System.Net;
using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>
/// مرصد الجهات: توحيد أسماء الجهة الواحدة، وزمن الرد في الاتجاهين، وما علينا وما لنا، والالتزام بالمهل،
/// وخطر تأخر الرد محسوباً من سجل ردودنا الفعلي.
/// </summary>
public class EntityObservatoryTests
{
    private static readonly DateTime Today = new(2026, 10, 28);
    private static string D(int daysAgo) => Today.AddDays(-daysAgo).ToString("yyyy-MM-dd");

    [Theory]
    [InlineData("معالي وزير المالية المحترم", "وزارة المالية")]
    [InlineData("وزارة الماليه", "وزارة المالية")]
    [InlineData("سعادة مدير عام الشؤون الإدارية حفظه الله", "مدير عام الشؤون الإدارية")]
    [InlineData("ديوانُ المراقبةِ العامة", "ديوان المراقبة العامة")]
    [InlineData("سعادة رئيس ديوان المراقبة العامة", "ديوان المراقبة العامة")]
    [InlineData("محافظ الهيئة العامة للإحصاء", "الهيئة العامة للإحصاء")]
    public void Spellings_of_one_entity_share_a_key(string a, string b) => Assert.Equal(EntityNames.Key(b), EntityNames.Key(a));

    [Fact]
    public void Different_entities_stay_apart()
    {
        Assert.NotEqual(EntityNames.Key("وزارة المالية"), EntityNames.Key("وزارة الصحة"));
        // المنصب الذي هو اسم الإدارة نفسها لا يُحذف.
        Assert.Equal("مدير عام الشوون الاداريه", EntityNames.Key("مدير عام الشؤون الإدارية"));
        Assert.Equal(("يوم واحد", "يومان", "5 أيام", "12 يوماً"), (EntityObservatory.Days(1), EntityObservatory.Days(2), EntityObservatory.Days(5), EntityObservatory.Days(12)));
    }

    private sealed class Archive
    {
        public List<Doc> Docs = new();
        public List<Edge> Edges = new();
        public Dictionary<string, (string, string)> Due = new();
        public Doc In(string entity, int daysAgo, string? dueIn = null)
        {
            var d = new Doc { Id = "d" + Docs.Count, ArchiveNo = "N" + Docs.Count, Title = "وارد " + Docs.Count, Direction = "وارد", SourceEntity = entity, DocumentDate = D(daysAgo), CreatedAt = D(daysAgo), Status = "جديد" };
            Docs.Add(d);
            if (dueIn is not null) Due[d.Id] = (dueIn, "نص");
            return d;
        }
        public Doc Out(string entity, int daysAgo, Doc? replyTo = null, string? asks = null)
        {
            var d = new Doc { Id = "d" + Docs.Count, ArchiveNo = "N" + Docs.Count, Title = "صادر " + Docs.Count, Direction = "صادر", DestinationEntity = entity, DocumentDate = D(daysAgo), CreatedAt = D(daysAgo), Status = "جديد" };
            Docs.Add(d);
            if (replyTo is not null) Edges.Add(new Edge(d.Id, replyTo.Id, "إشارة"));
            if (asks is not null) Due[d.Id] = (asks, "نص");
            return d;
        }
        public Snapshot Snapshot() => new() { Docs = Docs, ById = Docs.ToDictionary(d => d.Id), Edges = Edges, EffectiveDue = Due, ReplyDays = 10 };
    }

    [Fact]
    public void Reply_times_debts_health_and_late_risk_come_from_the_archive()
    {
        var a = new Archive();
        // وزارة المالية بصيغ مختلفة: ردودنا عليها استغرقت 3 و4 و5 و6 و8 و30 يوماً.
        foreach (var (received, took) in new[] { (100, 3), (90, 4), (80, 5), (70, 6), (60, 8), (50, 30) })
            a.Out("وزارة المالية", received - took, a.In(received % 20 == 0 ? "معالي وزير المالية المحترم" : "وزارة الماليه", received));
        // رد مطلوب منا: وصل قبل 4 أيام ومهلته 10 أيام من نصه.
        var open = a.In("وزارة المالية", 4, D(-6));
        // طلبنا من المالية بمهلة فردّت بعد 12 يوماً، وطلب آخر تجاوز موعده دون رد.
        var asked = a.Out("وزارة المالية", 40, asks: D(25));
        var answer = a.In("وزارة المالية", 28);
        a.Edges.Add(new Edge(answer.Id, asked.Id, "إشارة"));
        var unanswered = a.Out("وزارة المالية", 30, asks: D(5));
        // ديوان المراقبة: وارد قبل 20 يوماً بلا رد (مهلة الرد الافتراضية 10 أيام).
        var late = a.In("ديوان المراقبة العامة", 20);

        var o = EntityObservatory.Build(a.Snapshot(), new Dictionary<string, string>(), Today);
        var mof = o.Entities.Single(e => e.Name == "وزارة المالية");
        Assert.Equal(3, mof.Variants.Count);
        Assert.Equal(8, mof.Incoming);
        Assert.Equal(8, mof.Outgoing);
        Assert.Equal(6, mof.OurReplies);
        Assert.Equal(5.5, mof.OurMedianDays);
        Assert.Equal(12, mof.TheirMedianDays);
        Assert.Equal(1, mof.WeOwe);
        Assert.Equal(0, mof.WeOweOverdue);
        Assert.Equal((1, 1), (mof.TheyOwe, mof.TheyOweOverdue));
        Assert.Equal(5.0 / 6, mof.OnTimeRate!.Value, 3); // رد واحد (30 يوماً) تجاوز مهلة الأيام العشرة
        Assert.Equal("good", mof.HealthLevel);

        var dmo = o.Entities.Single(e => e.Name == "ديوان المراقبة العامة");
        Assert.Equal((1, 1), (dmo.WeOwe, dmo.WeOweOverdue));
        Assert.Equal("bad", dmo.HealthLevel);
        Assert.Equal(dmo.Key, o.Entities[0].Key); // المتأخر أولاً

        // خطر التأخر: المتأخر فعلاً أولاً، ثم رد المالية بتقدير من سجلها: من الردود التي جاوزت 4 أيام، واحد من خمسة تجاوز 10 أيام.
        Assert.Equal(late.Id, o.Risks[0].DocumentId);
        Assert.Equal("late", o.Risks[0].Level);
        var r = o.Risks.Single(x => x.DocumentId == open.Id);
        Assert.Equal(0.2, r.Risk);
        Assert.Equal("medium", r.Level);
        Assert.Equal(6, r.DaysLeft);
        Assert.Equal(6, r.Samples);
        Assert.Contains("وزارة المالية", r.Reason);
        Assert.Contains("20%", r.Reason);
        Assert.Equal(5.5, o.OurMedianDays);
        _ = unanswered;

        // الدمج اليدوي: ديوان المراقبة يُحسب ضمن المالية.
        var merged = EntityObservatory.Build(a.Snapshot(), new Dictionary<string, string> { [dmo.Key] = mof.Key }, Today);
        Assert.Single(merged.Entities);
        Assert.Equal(2, merged.Entities[0].WeOwe);
        Assert.Contains("ديوان المراقبة العامة", merged.Entities[0].Variants);
    }

    [Fact]
    public async Task Observatory_api_reads_real_letters_and_merges_names()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        async Task<string> Create(object body) => (await Json(await Send(admin, HttpMethod.Post, "/api/documents", body))).GetProperty("id").GetString()!;
        var incoming = await Create(new { title = "طلب بيانات المشاريع", direction = "وارد", sourceEntity = "معالي وزير المالية", externalNo = "4521", documentDate = "2026-09-01",
            text = "الرقم: 4521\nالتاريخ: 1448/03/09هـ\nالموضوع: طلب بيانات المشاريع\nنأمل تزويدنا بالبيانات خلال (15) يوماً من تاريخه." });
        await Create(new { title = "الرد على طلب بيانات المشاريع", direction = "صادر", destinationEntity = "وزارة الماليه", documentDate = "2026-09-06",
            text = "إشارة إلى خطاب سعادتكم رقم 4521 وتاريخ 1448/03/09هـ بشأن طلب بيانات المشاريع، نرفق لكم البيانات." });
        await Create(new { title = "استفسار عن العقود", direction = "وارد", sourceEntity = "ديوان المراقبه العامه", documentDate = "2026-10-01", text = "نأمل إفادتنا عن العقود." });

        var o = await Get(admin, "/api/entities");
        var mof = o.GetProperty("entities").EnumerateArray().Single(e => e.GetProperty("name").GetString()!.Contains("المالية"));
        Assert.Equal(1, mof.GetProperty("ourReplies").GetInt32());
        Assert.Equal(5, mof.GetProperty("ourMedianDays").GetDouble());
        Assert.Equal(0, mof.GetProperty("weOwe").GetInt32());
        var risk = o.GetProperty("risks").EnumerateArray().Single();
        Assert.Equal("late", risk.GetProperty("level").GetString()); // استفسار الديوان تجاوز مهلة الرد

        var dmo = o.GetProperty("entities").EnumerateArray().Single(e => e.GetProperty("name").GetString()!.Contains("ديوان"));
        var profile = await Get(admin, "/api/entities/" + Uri.EscapeDataString(mof.GetProperty("key").GetString()!));
        Assert.Equal(2, profile.GetProperty("recent").GetArrayLength());
        Assert.Contains(profile.GetProperty("recent").EnumerateArray(), d => d.GetProperty("id").GetString() == incoming);

        var reader = await f.UserAsync(admin, "reader1", "قارئ");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, HttpMethod.Post, "/api/entities/merge", new { from = dmo.GetProperty("key").GetString(), into = mof.GetProperty("key").GetString() })).StatusCode);
        await Json(await Send(admin, HttpMethod.Post, "/api/entities/merge", new { from = dmo.GetProperty("key").GetString(), into = mof.GetProperty("key").GetString() }));
        Assert.Single((await Get(admin, "/api/entities")).GetProperty("entities").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, HttpMethod.Post, "/api/entities/merge", new { from = mof.GetProperty("key").GetString(), into = dmo.GetProperty("key").GetString() })).StatusCode);
        await Json(await Send(admin, HttpMethod.Post, "/api/entities/unmerge", new { from = dmo.GetProperty("key").GetString() }));
        Assert.Equal(2, (await Get(admin, "/api/entities")).GetProperty("entities").GetArrayLength());
        Assert.Contains("دمج جهتين", await admin.GetStringAsync("/api/audit?take=20"));
    }
}
