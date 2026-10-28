using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AIDocumentArchiveVB;
using Basira.Platform.Data;

namespace Basira.Platform.Intelligence;

/// <summary>
/// اسم الجهة كما يُكتب في الخطابات يختلف: «معالي وزير المالية المحترم» و«وزارة الماليه» جهة واحدة.
/// المفتاح يُوحّد الحروف ويحذف التشكيل والألقاب وعبارات التحية، ويحوّل «وزير X» إلى «وزارة X».
/// والدمج اليدوي (من مرصد الجهات) يكمل ما لا تلتقطه القواعد.
/// </summary>
public static class EntityNames
{
    private static readonly Regex Diacritics = new("[ً-ٰٟـ]", RegexOptions.Compiled);
    private static readonly Regex Punct = new(@"[^\p{L}\p{N}\s]", RegexOptions.Compiled);
    private static readonly string[] Honorifics =
    {
        "صاحب السمو الملكي", "صاحب السمو", "صاحب المعالي", "صاحب السعادة", "صاحب الفضيلة", "معالي", "سعادة", "سمو", "فضيلة", "حضرة",
        "السيد", "السيدة", "الاستاذ", "الدكتور", "المهندس", "الشيخ", "المكرم", "الموقر", "الموقرة", "المحترم", "المحترمين", "المحترمون", "المحترمة",
        "حفظه الله", "يحفظه الله", "وفقه الله", "رعاه الله", "سلمه الله", "دام عزه", "دامت معاليه",
    };

    public static string Normalize(string s)
    {
        s = Diacritics.Replace(s ?? "", "");
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي', 'ة' => 'ه', 'ؤ' => 'و', 'ئ' => 'ي', _ => c });
        return Regex.Replace(Punct.Replace(sb.ToString(), " "), @"\s+", " ").Trim();
    }

    /// <summary>الاسم دون الألقاب والتحية (للعرض).</summary>
    public static string Clean(string name)
    {
        var s = " " + Regex.Replace(Diacritics.Replace(name ?? "", ""), @"\s+", " ").Trim() + " ";
        foreach (var h in Honorifics.OrderByDescending(x => x.Length))
            s = Regex.Replace(s, @"(?<=\s)" + Regex.Escape(h).Replace("ا", "[اأإآ]").Replace("ه", "[هة]") + @"(?=\s)", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim(' ', '،', ',', '-', '/');
        return s;
    }

    public static string Key(string name)
    {
        var n = Normalize(Clean(name));
        n = Regex.Replace(n, @"^وزير ", "وزاره ");
        // منصب قبل اسم جهة («رئيس ديوان المراقبة»، «محافظ الهيئة العامة…») يُحذف؛ ويبقى إن كان هو اسم الإدارة («مدير عام الشؤون الإدارية»).
        // الكلمات بصيغتها بعد التوحيد (ئ وؤ تصيران ي وو).
        n = Regex.Replace(n, @"^(رييس|مدير|مدير عام|امين|امين عام|محافظ|وكيل|نايب|نايب رييس|الرييس التنفيذي ل|الرييس التنفيذي)\s+(?=(ال)?(ديوان|وزاره|هييه|مركز|امانه|موسسه|شركه|جامعه|مجلس|بنك|صندوق|مستشفي|بلديه|اماره|محكمه|نيابه|جهاز|ادار)\b)", "");
        return n;
    }
}

public sealed record EntitySummary(
    string Key, string Name, List<string> Variants, int Incoming, int Outgoing, string LastContact,
    double? OurMedianDays, int OurReplies, double? TheirMedianDays, int TheirReplies,
    int WeOwe, int WeOweOverdue, int TheyOwe, int TheyOweOverdue, int Missing,
    double? OnTimeRate, int Health, string HealthLevel, int[] MonthlyIn, int[] MonthlyOut,
    int CommitmentsOurs, int CommitmentsTheirs);

/// <summary>رد علينا تأديته: هل يُتوقع أن يتأخر؟ Risk احتمال التأخر من سجل ردودنا الفعلي.</summary>
public sealed record RiskItem(string DocumentId, string EntityKey, string Entity, string Received, int DaysWaiting, string Due, bool DueFromText, int DaysLeft,
    double Risk, string Level, string Reason, int? TypicalDays, int Samples);

public sealed record ObservatoryResult(List<EntitySummary> Entities, List<RiskItem> Risks, double? OurMedianDays, double? OnTimeRate,
    int WeOwe, int WeOweOverdue, int TheyOwe, int TheyOweOverdue, string[] Months, Dictionary<string, string> DocEntity, int Unassigned);

/// <summary>
/// مرصد الجهات: علاقة الجهة بكل جهة تراسلها، من الأرشيف نفسه دون إدخال يدوي.
/// - زمن الرد: من تاريخ الوارد إلى تاريخ أول صادر يشير إليه (ردودنا)، والعكس (ردودهم).
/// - ما علينا: وارد غير منجز لم يشر إليه خطاب لاحق (والوارد الذي هو رد على صادر منا لا يُعد إلا إن طلب شيئاً بمهلة). وما لنا: صادر طلبنا فيه شيئاً بمهلة ولم يرد عليه وارد.
/// - الالتزام بالمهل: نسبة ما رددنا عليه قبل موعده (المقروء من النص أو مهلة الرد الافتراضية).
/// - خطر التأخر: لكل رد مطلوب منا، نسبة ردودنا السابقة (على الجهة نفسها إن كفى سجلها) التي استغرقت أطول من المهلة المتاحة.
/// </summary>
public static class EntityObservatory
{
    public const int MinSamples = 4;

    public static string? EntityOf(Doc d) => d.Direction switch
    {
        "وارد" => d.SourceEntity,
        "صادر" => d.DestinationEntity,
        _ => null,
    } is { Length: > 0 } e ? e : null;

    public static ObservatoryResult Build(Snapshot s, IReadOnlyDictionary<string, string> aliases, DateTime today)
    {
        string Canon(string key) { var seen = 0; while (aliases.TryGetValue(key, out var to) && seen++ < 10) key = to; return key; }
        var docEntity = new Dictionary<string, string>();
        var raw = new Dictionary<string, Dictionary<string, int>>();
        var unassigned = 0;
        foreach (var d in s.Docs)
        {
            var name = EntityOf(d);
            if (name is null) { if (d.Direction != "داخلي") unassigned++; continue; }
            var key = Canon(EntityNames.Key(name));
            if (key == "") continue;
            docEntity[d.Id] = key;
            var display = EntityNames.Clean(name);
            if (!raw.TryGetValue(key, out var names)) raw[key] = names = new();
            names[display] = names.GetValueOrDefault(display) + 1;
        }

        // من يشير إلى من: أقدم رد لكل خطاب.
        var citedBy = new Dictionary<string, List<Doc>>();
        foreach (var e in s.Edges)
            if (s.ById.TryGetValue(e.From, out var from) && s.ById.ContainsKey(e.To))
                (citedBy.TryGetValue(e.To, out var l) ? l : citedBy[e.To] = new()).Add(from);
        Doc? FirstReply(Doc d, string direction) => citedBy.TryGetValue(d.Id, out var l)
            ? l.Where(x => x.Direction == direction && Snapshot.DateOf(x) >= Snapshot.DateOf(d)).OrderBy(Snapshot.DateOf).FirstOrDefault() : null;
        var answersOurs = s.Edges.Where(e => s.ById.TryGetValue(e.From, out var f) && f.Direction == "وارد" && s.ById.TryGetValue(e.To, out var t) && t.Direction == "صادر")
            .Select(e => e.From).ToHashSet();
        DateTime? DueOf(Doc d) => s.EffectiveDue.TryGetValue(d.Id, out var x) ? Snapshot.ParseIso(x.Date) : null;

        var ourDays = new Dictionary<string, List<int>>();
        var theirDays = new Dictionary<string, List<int>>();
        var onTime = new Dictionary<string, (int On, int Total)>();
        var weOwe = new Dictionary<string, List<(Doc Doc, DateTime Due, bool FromText)>>();
        var theyOwe = new Dictionary<string, List<(Doc Doc, DateTime Due)>>();
        foreach (var d in s.Docs)
        {
            if (!docEntity.TryGetValue(d.Id, out var key)) continue;
            var date = Snapshot.DateOf(d);
            if (date == DateTime.MinValue) continue;
            if (d.Direction == "وارد")
            {
                var reply = FirstReply(d, "صادر");
                var textDue = DueOf(d);
                var due = textDue ?? date.AddDays(s.ReplyDays);
                if (reply is not null)
                {
                    var days = (int)(Snapshot.DateOf(reply) - date).TotalDays;
                    if (days is >= 0 and <= 365) (ourDays.TryGetValue(key, out var l) ? l : ourDays[key] = new()).Add(days);
                    var o = onTime.GetValueOrDefault(key);
                    onTime[key] = (o.On + (Snapshot.DateOf(reply) <= due ? 1 : 0), o.Total + 1);
                }
                // وارد هو نفسه رد على صادر منا لا ينتظر رداً، إلا إن طلب شيئاً بمهلة في نصه.
                else if (!BasiraEngine.IsClosed(ArchiveIndex.ToRecord(d, "")) && (textDue is not null || !answersOurs.Contains(d.Id)))
                {
                    (weOwe.TryGetValue(key, out var l) ? l : weOwe[key] = new()).Add((d, due, textDue is not null));
                    if (due < today) { var o = onTime.GetValueOrDefault(key); onTime[key] = (o.On, o.Total + 1); }
                }
            }
            else if (d.Direction == "صادر")
            {
                var reply = FirstReply(d, "وارد");
                if (reply is not null)
                {
                    var days = (int)(Snapshot.DateOf(reply) - date).TotalDays;
                    if (days is >= 0 and <= 365) (theirDays.TryGetValue(key, out var l) ? l : theirDays[key] = new()).Add(days);
                }
                else if (DueOf(d) is { } asked && !BasiraEngine.IsClosed(ArchiveIndex.ToRecord(d, "")))
                    (theyOwe.TryGetValue(key, out var l) ? l : theyOwe[key] = new()).Add((d, asked));
            }
        }

        var months = Enumerable.Range(0, 12).Select(i => new DateTime(today.Year, today.Month, 1).AddMonths(i - 11)).ToArray();
        var missing = new Dictionary<string, int>();
        foreach (var g in s.Ghosts)
            foreach (var key in g.CitedBy.Select(id => docEntity.GetValueOrDefault(id)).Where(k => k is not null).Distinct())
                missing[key!] = missing.GetValueOrDefault(key!) + 1;
        var commitments = s.Commitments.Where(c => c.State is "open" or "overdue").Select(c => (c, key: docEntity.GetValueOrDefault(c.DocumentId))).Where(x => x.key is not null).ToList();

        var entities = new List<EntitySummary>();
        foreach (var (key, names) in raw)
        {
            var docs = s.Docs.Where(d => docEntity.GetValueOrDefault(d.Id) == key).ToList();
            var owe = weOwe.GetValueOrDefault(key) ?? new();
            var owed = theyOwe.GetValueOrDefault(key) ?? new();
            var ours = ourDays.GetValueOrDefault(key) ?? new();
            var theirs = theirDays.GetValueOrDefault(key) ?? new();
            var ot = onTime.GetValueOrDefault(key);
            double? rate = ot.Total > 0 ? ot.On / (double)ot.Total : null;
            var overdue = owe.Count(o => o.Due < today);
            // الصحة: انتظامنا مع الجهة. من نسبة الالتزام بالمهل (إن وُجد سجل)، ويُخصم لكل رد متأخر الآن.
            var health = (int)Math.Clamp((rate is { } r ? 40 + 60 * r : 100) - Math.Min(48, overdue * 12), 0, 100);
            int[] Monthly(string dir) => months.Select(m => docs.Count(d => d.Direction == dir && Snapshot.DateOf(d) is var x && x.Year == m.Year && x.Month == m.Month)).ToArray();
            entities.Add(new EntitySummary(
                key, names.OrderByDescending(n => n.Value).ThenBy(n => n.Key.Length).First().Key, names.Keys.OrderByDescending(n => names[n]).ToList(),
                docs.Count(d => d.Direction == "وارد"), docs.Count(d => d.Direction == "صادر"),
                docs.Select(Snapshot.DateOf).Where(x => x != DateTime.MinValue).DefaultIfEmpty().Max().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Median(ours), ours.Count, Median(theirs), theirs.Count,
                owe.Count, overdue, owed.Count, owed.Count(o => o.Due < today), missing.GetValueOrDefault(key),
                rate, health, health >= 80 ? "good" : health >= 55 ? "warn" : "bad", Monthly("وارد"), Monthly("صادر"),
                commitments.Count(c => c.key == key && c.c.Side == "علينا"), commitments.Count(c => c.key == key && c.c.Side == "لنا")));
        }
        entities = entities.OrderByDescending(e => e.WeOweOverdue).ThenByDescending(e => e.Incoming + e.Outgoing).ToList();

        // خطر التأخر لكل رد مطلوب منا.
        var allOurs = ourDays.Values.SelectMany(x => x).ToList();
        var risks = new List<RiskItem>();
        foreach (var (key, list) in weOwe)
        {
            var name = entities.First(e => e.Key == key).Name;
            var history = ourDays.GetValueOrDefault(key) is { Count: >= MinSamples } own ? own : allOurs.Count >= MinSamples ? allOurs : null;
            var scope = history is null ? "" : ReferenceEquals(history, ourDays.GetValueOrDefault(key)) ? "على " + name : "على كل الجهات";
            foreach (var (d, due, fromText) in list)
            {
                var received = Snapshot.DateOf(d);
                var elapsed = (int)(today - received).TotalDays;
                var allowed = (int)(due - received).TotalDays;
                var left = (int)(due - today).TotalDays;
                double risk; string reason;
                if (left < 0)
                {
                    risk = 1;
                    reason = $"وصل قبل {Days(elapsed)} وحلّ موعده {(fromText ? "المذكور في نصه" : $"(مهلة الرد {Days(s.ReplyDays)})")}، ولم يصدر ما يشير إليه.";
                }
                else if (history is not null)
                {
                    // احتمال مشروط: من الردود التي استغرقت على الأقل ما مضى حتى الآن، كم تجاوز المهلة المتاحة؟
                    var still = history.Where(x => x >= elapsed).ToList();
                    if (still.Count < 2) still = history;
                    risk = still.Count(x => x > allowed) / (double)still.Count;
                    reason = $"ردودكم {scope} تستغرق عادةً {Days(Median(history) ?? 0)}، ويتبقى {Days(left)}؛ {Math.Round(risk * 100)}% من الردود التي تجاوزت مدة الانتظار الحالية تخطت هذه المهلة.";
                }
                else
                {
                    risk = left <= 2 ? 0.6 : left <= 5 ? 0.3 : 0.1;
                    reason = $"لا سجل ردود سابقة كافٍ؛ التقدير من المهلة المتبقية ({Days(left)}) وحدها.";
                }
                var level = left < 0 ? "late" : risk >= 0.5 ? "high" : risk >= 0.2 ? "medium" : "low";
                risks.Add(new RiskItem(d.Id, key, name, received.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), elapsed, due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), fromText,
                    left, Math.Round(risk, 2), level, reason, history is null ? null : (int)Math.Round(Median(history) ?? 0), history?.Count ?? 0));
            }
        }
        risks = risks.OrderBy(r => r.Level switch { "late" => 0, "high" => 1, "medium" => 2, _ => 3 }).ThenBy(r => r.DaysLeft).ThenByDescending(r => r.Risk).ToList();

        var totalOn = onTime.Values.Sum(v => v.On);
        var total = onTime.Values.Sum(v => v.Total);
        return new ObservatoryResult(entities, risks, Median(allOurs), total > 0 ? totalOn / (double)total : null,
            weOwe.Values.Sum(l => l.Count), entities.Sum(e => e.WeOweOverdue), theyOwe.Values.Sum(l => l.Count), entities.Sum(e => e.TheyOweOverdue),
            months.Select(m => m.ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToArray(), docEntity, unassigned);
    }

    /// <summary>عدد الأيام بصيغته العربية: يوم واحد، يومان، 3 أيام، 11 يوماً.</summary>
    public static string Days(double value)
    {
        var n = (int)Math.Round(Math.Abs(value));
        return n switch { 0 => "أقل من يوم", 1 => "يوم واحد", 2 => "يومان", <= 10 => $"{n} أيام", _ => $"{n} يوماً" };
    }

    public static double? Median(List<int> values)
    {
        if (values.Count == 0) return null;
        var v = values.OrderBy(x => x).ToList();
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2.0;
    }
}
