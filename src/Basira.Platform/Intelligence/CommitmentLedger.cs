using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AIDocumentArchiveVB;
using Basira.Platform.Data;

namespace Basira.Platform.Intelligence;

/// <summary>وعد في نص خطاب: الجملة كاملة وموضعها، وعبارة الوعد منها.</summary>
public sealed record PromiseSpan(int Offset, int Length, string Sentence, string Promise);

/// <summary>قرار مستخدم على التزام: «fulfilled» تم الوفاء، «dismissed» ليس التزاماً، «open» قائم رغم ما يرجّح الوفاء.</summary>
public sealed record CommitmentDecision(string Key, string DocumentId, string State, string Note, string Actor, string At);

/// <summary>
/// التزام مرصود (الحالة: open قائم، overdue متأخر، fulfilled موفّى، dismissed ليس التزاماً): ما وعدت به جهتنا في صادرها («علينا»)، أو ما وعدتنا به جهة في واردها («لنا»)، وموعده، وحالته،
/// وما يرجّح الوفاء به (خطاب لاحق من الواعد في المعاملة نفسها).
/// </summary>
public sealed record Commitment(
    string Key, string DocumentId, string Side, string Party, string Sentence, string Promise, int Offset, int Length,
    string Due, string DueSource, bool DefaultDue, string State, string? FulfilledBy, string FulfilledReason, int DaysLeft, string? RespondsTo);

/// <summary>
/// سجل الالتزامات: يستخرج الوعود من نصوص المراسلات بقواعد صريحة («سنوافيكم…»، «سيتم تزويدكم…»، «جارٍ العمل على…»)،
/// ويحسب موعد كل وعد من نصه أو بمهلة افتراضية، ويرجّح الوفاء به إن صدر من الواعد خطاب لاحق في المعاملة نفسها.
/// </summary>
public static class CommitmentLedger
{
    public const string Ours = "علينا";
    public const string Theirs = "لنا";

    // صيغ المستقبل: «سنوافيكم»، «سيباشر»، «سوف نرسل»، «سيتم/سوف يتم تزويدكم»، «جارٍ العمل على…»، «نتعهد/تلتزم الشركة بـ…».
    private static readonly Regex FutureRx = new(
        @"(?<![\p{L}])(?<w>(?:و|ف)?س(?:ن|ي|ت)\p{L}{2,})" +
        @"|(?<![\p{L}])(?:و|ف)?سوف\s+(?<w>\p{L}{2,})" +
        @"|(?<![\p{L}])(?:و|ف)?(?:سيتم|ستتم|سيجري|سوف\s+يتم|سيصار\s+الي)\s+(?<m>\p{L}{3,})" +
        @"|(?<![\p{L}])(?:و|ف)?جار(?:ي)?\s+(?:العمل\s+علي\s+)?(?<m>\p{L}{3,})" +
        @"|(?<![\p{L}])(?:و|ف)?(?:(?:ن|ت|ي)(?:تعهد|لتزم)(?:\s+\p{L}+){0,2}|نعدكم)\s+ب(?!ال)(?<m>\p{L}{3,})",
        RegexOptions.Compiled);

    // كلمات تبدأ كفعل مستقبل وهي أسماء: «سيارة»، «سنة»، «سيادتكم»، «سند»…
    private static readonly HashSet<string> Nouns = new(StringComparer.Ordinal)
    {
        "سياره", "سيارات", "سنه", "سنوات", "سنوي", "سنويه", "سنويا", "سنين", "سته", "ستين", "ستون", "ستمايه", "ستمائه", "سياسه", "سياسات", "سيوله", "سياحه", "سياحي",
        "سيره", "سير", "سيد", "سيدي", "سيده", "سياده", "سيادتكم", "سيادته", "سند", "سندات", "ستار", "ستاره", "ستاير", "ستائر", "سيرفر", "سنتر", "سيف", "سيول",
        "سنغافوره", "ستوديو", "سيناريو", "سينما", "سيادي", "سنابل", "سنام", "ستر", "سيئ", "سييه", "سيئه", "سياق", "سياج", "سياسي", "ستراتيجي", "سنتيمتر", "سنت", "سنن",
        "سيله", "سيط", "سيطه", "سيطره", "سنترال", "سيرته", "سنوات", "ستاد", "سيرام", "سيراميك",
    };

    // أفعال الكون والصيرورة («سيكون الدوام…») وخطاب المرسل إليه («ستجدون مرفقاً…»): ليست وعداً.
    private static readonly Regex NotVerbRx = new(@"^(?:و|ف)?(?:سيكون|ستكون|سيصبح|ستصبح|سيظل|ستظل|سيبقي|ستبقي|ست\p{L}+(?:ون|ين))$", RegexOptions.Compiled);

    // شرط أو عقوبة أو حكم عام يُعلن («في حال عدم…»، «سيتم تطبيق/احتساب/إلغاء…»): ليس وعداً بتسليم.
    private static readonly Regex NotPromiseRx = new(
        @"في\s+حال|في\s+حاله|(?:^|\s)(?:و|ف)?(?:ان|اذا|ما)\s+لم\s|والا\s|خلاف\s+ذلك|غرام|جزاء|عقوب|مخالف|(?:^|\s)(?:حسم|خصم)\s" +
        @"|(?:سيتم|ستتم|سوف\s+يتم)\s+(?:تطبيق|ايقاف|الغاء|منع|احتساب|اعتبار|العمل\s+ب|اعتماد\s+الدوام|تعطيل)",
        RegexOptions.Compiled);

    private static bool IsPromise(Match m)
    {
        if (m.Groups["m"].Success) return true;
        var w = m.Groups["w"].Value;
        var bare = Regex.Replace(w, "^(?:و|ف)(?=س)", "");
        if (m.Value.StartsWith("سوف", StringComparison.Ordinal) || Regex.IsMatch(m.Value, "^(?:و|ف)سوف")) return !NotVerbRx.IsMatch("س" + w) && !Nouns.Contains(w);
        return !Nouns.Contains(bare) && !NotVerbRx.IsMatch(w);
    }

    /// <summary>جمل الوعد في النص بمواضعها في النص الأصلي.</summary>
    public static List<PromiseSpan> Extract(string text)
    {
        var result = new List<PromiseSpan>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (Match sm in Regex.Matches(text + "\n", @"[^\n.؟?!؛]+(?:[.؟?!؛]+|\n)"))
        {
            var raw = sm.Value;
            var trimmed = raw.Trim();
            if (trimmed.Length < 12) continue;
            var map = new List<int>();
            var norm = NormalizeWithMap(raw, map);
            if (NotPromiseRx.IsMatch(norm)) continue;
            var m = FutureRx.Matches(norm).FirstOrDefault(IsPromise);
            if (m is null) continue;
            var lead = raw.Length - raw.TrimStart().Length;
            var promiseStart = map[m.Index];
            var promise = Regex.Replace(raw[promiseStart..].Trim().TrimEnd('.', '؟', '?', '!', '؛', '،', ','), @"\s+", " ");
            var words = promise.Split(' ');
            if (words.Length > 18) promise = string.Join(' ', words.Take(18)) + "…";
            result.Add(new PromiseSpan(sm.Index + lead, trimmed.Length, Regex.Replace(trimmed, @"\s+", " "), promise));
        }
        return result;
    }

    /// <summary>توحيد الهمزات والياء والتاء المربوطة وحذف التشكيل والتطويل، مع موضع كل حرف في الأصل.</summary>
    private static string NormalizeWithMap(string text, List<int> map)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is >= '\u064B' and <= '\u065F' or '\u0670' or '\u0640') continue;
            ch = ch switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي', 'ة' => 'ه', _ => ch };
            sb.Append(ch);
            map.Add(i);
        }
        return sb.ToString();
    }

    public static string KeyOf(string documentId, string sentence) =>
        documentId + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(BasiraEngine.Normalize(sentence))))[..10].ToLowerInvariant();

    /// <summary>
    /// الالتزامات في الأرشيف كله. الصادر وعود «علينا» والوارد وعود «لنا» (الداخلي لا يُحسب). الموعد من نص الجملة
    /// («خلال أسبوعين»، «في موعد أقصاه…») وإلا تاريخ الخطاب مضافاً إليه المهلة الافتراضية.
    /// </summary>
    public static List<Commitment> Build(IReadOnlyList<Doc> docs, IReadOnlyDictionary<string, string> threadOf, IReadOnlyList<Edge> edges,
        IReadOnlyDictionary<string, CommitmentDecision> decisions, int defaultDays, DateTime today)
    {
        var result = new List<Commitment>();
        var byThread = docs.GroupBy(d => threadOf.TryGetValue(d.Id, out var t) ? t : d.Id).ToDictionary(g => g.Key, g => g.ToList());
        var byId = docs.ToDictionary(d => d.Id);
        static bool After(Doc a, Doc b) =>
            Snapshot.DateOf(a) > Snapshot.DateOf(b) || Snapshot.DateOf(a) == Snapshot.DateOf(b) && string.CompareOrdinal(a.CreatedAt, b.CreatedAt) > 0;

        foreach (var d in docs)
        {
            if (d.Direction is not ("صادر" or "وارد")) continue;
            // مسودة رد لم تُعتمد: وعودها لم تصدر بعد.
            if (d.Direction == "صادر" && d.Status == ArchiveIndex.PendingApproval) continue;
            var promises = Extract(d.Text);
            if (promises.Count == 0) continue;
            var side = d.Direction == "صادر" ? Ours : Theirs;
            var party = d.Direction == "صادر" ? d.DestinationEntity : d.SourceEntity;
            var baseDate = Snapshot.DateOf(d) == DateTime.MinValue ? (Snapshot.ParseIso(d.CreatedAt)?.Date ?? today) : Snapshot.DateOf(d);
            // ما يرجّح الوفاء: خطاب لاحق من الواعد في المعاملة نفسها (الصادر المعتمد لنا، أو الوارد لهم).
            var thread = threadOf.TryGetValue(d.Id, out var tid) && byThread.TryGetValue(tid, out var members) ? members : new List<Doc> { d };
            var later = thread.Where(x => x.Id != d.Id && x.Direction == d.Direction && After(x, d) && !(x.Direction == "صادر" && x.Status == ArchiveIndex.PendingApproval))
                .OrderBy(Snapshot.DateOf).ThenBy(x => x.CreatedAt, StringComparer.Ordinal).FirstOrDefault();
            var respondsTo = d.Direction == "صادر"
                ? edges.Where(e => e.From == d.Id && byId.TryGetValue(e.To, out var t) && t.Direction == "وارد").Select(e => e.To).FirstOrDefault()
                : null;

            foreach (var p in promises)
            {
                var key = KeyOf(d.Id, p.Sentence);
                decisions.TryGetValue(key, out var decision);
                var hints = DeadlineExtractor.Extract(p.Sentence, baseDate).Where(h => h.Due.HasValue).ToList();
                var defaultDue = hints.Count == 0;
                var due = defaultDue ? baseDate.AddDays(defaultDays) : hints.Min(h => h.Due!.Value).Date;
                var dueSource = defaultDue ? $"مهلة افتراضية ({defaultDays} يوماً من تاريخ الخطاب)" : "من النص: " + hints.OrderBy(h => h.Due).First().Evidence;
                string? by = null; var reason = "";
                string state;
                if (decision?.State == "dismissed") { state = "dismissed"; reason = decision.Note; }
                else if (decision?.State == "fulfilled") { state = "fulfilled"; reason = decision.Note == "" ? $"أكده {decision.Actor}" : decision.Note; }
                else if (decision?.State != "open" && later is not null)
                {
                    state = "fulfilled"; by = later.Id;
                    reason = (d.Direction == "صادر" ? "صدر بعده في المعاملة " : "ورد منهم بعده في المعاملة ") + later.ArchiveNo;
                }
                else state = due < today ? "overdue" : "open";
                result.Add(new Commitment(key, d.Id, side, party, p.Sentence, p.Promise, p.Offset, p.Length,
                    due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), dueSource, defaultDue, state, by, reason, (int)(due - today).TotalDays, respondsTo));
            }
        }
        return result.OrderBy(c => c.State switch { "overdue" => 0, "open" => 1, "fulfilled" => 2, _ => 3 }).ThenBy(c => c.Due, StringComparer.Ordinal).ToList();
    }
}
