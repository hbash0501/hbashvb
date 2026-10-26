using System.Text.RegularExpressions;
using AIDocumentArchiveVB;

namespace Basira.Platform.Intelligence;

/// <summary>طلب في الخطاب الوارد يحتاج إلى رد: نص الجملة، ونوعه، وخلاصته القصيرة.</summary>
public sealed record ReplyAsk(string Text, string Kind, string Gist);

/// <summary>نوع رد جاهز (موافقة، إفادة، اعتذار…) بنص يبدأ بعد سطر الإشارة.</summary>
public sealed record ReplyIntent(string Key, string Label, string Body);

/// <summary>رد سابق من الأرشيف على خطاب مشابه: الخطابان، والتشابه وأسبابه، وصياغة الرد مهيأة للخطاب الحالي.</summary>
public sealed record ReplyPrecedent(string IncomingId, string IncomingArchiveNo, string IncomingTitle, string ReplyId, string ReplyArchiveNo, string ReplyTitle,
    string ReplyDate, double Similarity, List<string> SharedTerms, string Body);

/// <summary>أجزاء خطاب الرد كما تحررها الواجهة؛ يُبنى منها النص بترتيب ثابت.</summary>
public sealed record ReplyLetter(string OutgoingNo, string DateHijri, string Subject, string Addressee, string ReferenceLine, string Body, string SignerTitle, string SignerName);

/// <summary>
/// صياغة الرد: يستخرج ما يطلبه الخطاب الوارد، ويبني سطر الإشارة الذي يربط الرد بأصله، ويقترح نوع الرد ونصه،
/// ويأتي بردود الجهة السابقة على خطابات مشابهة («السوابق») مهيأة للخطاب الحالي. كله محلي وبقواعد صريحة.
/// </summary>
public static class ReplyComposer
{
    public const string Greeting = "السلام عليكم ورحمة الله وبركاته، وبعد:";
    public const string Closing = "وتقبلوا خالص التحية والتقدير،،،";

    // ===== ما يطلبه الخطاب =====

    private static readonly Regex AskRx = new(@"(?:نأمل|نامل|نرجو|نرغب|يرجى|يُرجى|أرجو|ارجو|آمل|امل\s|نطلب|الرجاء|نود\s+(?:منكم|من\s+سعادتكم)|نتطلع|تزويدنا|موافاتنا|إفادتنا|افادتنا|الإفادة|الافادة|إشعارنا|اشعارنا|التوجيه\s+ب|اتخاذ\s+(?:اللازم|ما\s+يلزم|الإجراء|الاجراء)|للاطلاع\s+و|لإكمال|لاكمال|استكمال|بشكل\s+عاجل|في\s+موعد\s+أقصاه|خلال\s+\(?\d|\?|؟)", RegexOptions.Compiled);
    private static readonly Regex LeadRx = new(@"^(?:(?:لذا|عليه|وعليه|لذلك|وبناءً\s+عليه|وبناء\s+عليه|و)\s*[،,]?\s*)?(?:فإننا\s+|فاننا\s+|ف)?(?:نأمل|نامل|نرجو|يرجى|يُرجى|أرجو|ارجو|آمل|نطلب|الرجاء|نود)\s+(?:من\s+سعادتكم\s+|منكم\s+)?(?:التكرم\s+|التفضل\s+)?(?:ب|بال)?", RegexOptions.Compiled);
    private static readonly Regex NoiseRx = new(@"^(?:السلام عليكم|بسم الله|تحية طيبة|وبعد|وتقبلوا|وتقبل|والسلام|مع خالص|شاكرين|ولكم جزيل|الرقم|التاريخ|المرفقات|الموضوع|سعادة|معالي|المكرم)", RegexOptions.Compiled);

    /// <summary>جمل الطلب والسؤال والمهلة في الخطاب، بترتيب ورودها (ثمانٍ على الأكثر).</summary>
    public static List<ReplyAsk> ExtractAsks(string text)
    {
        var asks = new List<ReplyAsk>();
        var seen = new HashSet<string>();
        foreach (var raw in Regex.Split(text ?? "", @"(?<=[.؟?!؛])\s+|\n+"))
        {
            var s = Regex.Replace(raw, @"\s+", " ").Trim().Trim('-', '•', '*', ' ');
            if (s.Length < 15 || NoiseRx.IsMatch(s) || !AskRx.IsMatch(s)) continue;
            // بداية الجملة: ما بعد التمهيد («إشارة إلى…،») إن كان الطلب بعده.
            var clauses = Regex.Split(s, @"\s*[،,]\s*(?=(?:لذا|عليه|وعليه|لذلك|ف?نأمل|ف?نرجو|يرجى|نطلب|آمل|أرجو))");
            var core = clauses.Length > 1 ? string.Join("، ", clauses.Skip(clauses.Length - 1)) : s;
            if (core.Length < 15) core = s;
            if (core.Length > 260) core = core[..260].TrimEnd() + "…";
            var key = BasiraEngine.Normalize(core);
            if (!seen.Add(key)) continue;
            asks.Add(new ReplyAsk(core, KindOf(core), Gist(core)));
            if (asks.Count == 8) break;
        }
        return asks;
    }

    private static string KindOf(string s)
    {
        if (s.Contains('؟') || s.Contains('?')) return "سؤال";
        if (Regex.IsMatch(s, @"الموافقة|اعتماد|السماح|منح|التصريح|الإذن|الاذن")) return "موافقة";
        if (Regex.IsMatch(s, @"تزويدنا|موافاتنا|إفادتنا|افادتنا|الإفادة|الافادة|إشعارنا|اشعارنا|إرسال|ارسال|بيان|كشف|تقرير")) return "معلومات";
        if (Regex.IsMatch(s, @"في\s+موعد\s+أقصاه|خلال\s+\(?\d|خلال\s+(?:أسبوع|يوم|شهر|عشرة|خمسة|ثلاثة)|بشكل\s+عاجل")) return "مهلة";
        return "إجراء";
    }

    /// <summary>خلاصة الطلب بلا فعل الرجاء: «نأمل التكرم بتزويدنا بكشف…» ← «تزويدنا بكشف…».</summary>
    public static string Gist(string s)
    {
        var g = LeadRx.Replace(s.Trim(), "").Trim();
        // المهلة وما يليها ليست من موضوع الطلب: «… حسب المرتبة، في موعد أقصاه عشرة أيام» ← «… حسب المرتبة».
        g = Regex.Replace(g, @"\s*[،,]?\s*(?:وذلك\s+)?(?:في\s+موعد\s+أقصاه|في\s+موعد\s+اقصاه|خلال\s+\(?\d|خلال\s+(?:أسبوع|يوم|شهر|عشرة|خمسة|ثلاثة|سبعة)|بشكل\s+عاجل|قبل\s+تاريخ).*$", "");
        g = Regex.Replace(g, @"[.؟?!،,؛\s]+$", "");
        var words = g.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 14) g = string.Join(' ', words.Take(14)) + "…";
        return g.Length < 4 ? s : g;
    }

    // ===== نوع الرد =====

    public static string SuggestIntent(string text, IReadOnlyList<ReplyAsk> asks)
    {
        if (asks.Any(a => a.Kind == "موافقة")) return "approve";
        if (asks.Any(a => a.Kind is "معلومات" or "سؤال")) return "inform";
        if (asks.Count == 0 && Regex.IsMatch(text ?? "", @"للإحاطة|للاحاطة|للعلم|نحيطكم|للاطلاع(?!\s+و)")) return "acknowledge";
        if (asks.Count > 0) return "done";
        return "acknowledge";
    }

    public static List<ReplyIntent> Intents(IReadOnlyList<ReplyAsk> asks)
    {
        var approval = asks.FirstOrDefault(a => a.Kind == "موافقة");
        var approveWhat = approval is null ? "طلبكم" : Regex.Replace(approval.Gist, @"^(?:الموافقة|بالموافقة)\s+على\s+", "");
        var points = asks.Where(a => a.Kind is "معلومات" or "سؤال" or "مهلة" or "إجراء").ToList();
        var inform = points.Count == 0
            ? "نفيدكم بأن [الإفادة المطلوبة]."
            : "نفيدكم بما يلي:\n" + string.Join("\n", points.Select((a, i) => $"{i + 1}. بخصوص {a.Gist}: [الإفادة]."));
        return new List<ReplyIntent>
        {
            new("approve", "موافقة", $"نفيدكم بالموافقة على {approveWhat}، وفق الأنظمة والتعليمات المنظمة لذلك."),
            new("inform", "إفادة بالمطلوب", inform),
            new("done", "تم الإجراء", "نفيدكم بأنه تم اتخاذ اللازم حيال ما ورد في خطابكم، حيث [الإجراء المتخذ]."),
            new("complete", "طلب استكمال", "نأمل التكرم بموافاتنا بـ[المستندات أو البيانات المطلوبة]، ليتسنى لنا استكمال دراسة الموضوع."),
            new("refer", "إحالة للمختص", "نفيدكم بأن الموضوع من اختصاص [الجهة المختصة]، وقد أُحيل إليها لاتخاذ ما يلزم حياله، وسنفيدكم بما يستجد."),
            new("decline", "اعتذار", "نفيدكم بأنه يتعذر إجابة طلبكم في الوقت الحالي، وذلك [سبب الاعتذار]، آملين تفهمكم."),
            new("acknowledge", "إحاطة وشكر", "نشكر لكم إحاطتنا بما ورد في خطابكم، وقد أُحيط علماً بمضمونه، وسيُعمل بموجبه."),
        };
    }

    // ===== الإشارة والمخاطَب =====

    /// <summary>
    /// سطر الإشارة الذي يربط الرد بأصله في المعاملة: برقم الجهة إن وُجد، ورقم القيد لدينا إن وُجد، والتاريخ الهجري والموضوع.
    /// </summary>
    public static string ReferenceLine(string externalNo, string registrationNo, string dateHijri, string subject)
    {
        var parts = "إشارةً إلى خطابكم";
        if (externalNo != "") parts += $" رقم ({externalNo})";
        if (dateHijri != "") parts += $" وتاريخ {dateHijri}";
        if (registrationNo != "") parts += $"{(externalNo != "" ? "،" : "")} المقيد لدينا برقم ({registrationNo})";
        if (subject != "") parts += $" بشأن {subject}";
        return parts;
    }

    private static readonly Regex SignerRx = new(@"^(?:مدير|المدير|وكيل|الوكيل|رئيس|الرئيس|مساعد|نائب|محافظ|أمين|الأمين|المشرف|مشرف|المدير\s+العام|الرئيس\s+التنفيذي)\b[^\n]{0,60}$", RegexOptions.Compiled);

    /// <summary>صفة موقّع الخطاب الوارد (آخر الأسطر): «مدير عام الشؤون المالية» ← المخاطَب في الرد.</summary>
    public static string SignerTitle(string text)
    {
        var lines = (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        foreach (var line in lines.Skip(Math.Max(0, lines.Count - 6)).Reverse())
            if (SignerRx.IsMatch(line) && line.Split(' ').Length <= 9) return line.TrimEnd('،', ',', '.');
        return "";
    }

    public static string AddresseeLine(string signerTitle, string entity)
    {
        if (signerTitle != "")
            return "سعادة " + signerTitle + (entity != "" && !BasiraEngine.Normalize(signerTitle).Contains(BasiraEngine.Normalize(entity)) ? " ب" + entity : "");
        return entity != "" ? "سعادة/ " + entity : "سعادة/ [المخاطَب]";
    }

    // ===== السوابق =====

    private static readonly Regex DateRx = new(@"\d{1,4}\s*[/\-.]\s*\d{1,2}\s*[/\-.]\s*\d{1,4}\s*(?:هـ|ه|م)?", RegexOptions.Compiled);

    /// <summary>
    /// نص رد سابق مهيأ للخطاب الحالي: بلا الترويسة والتحية وسطر الإشارة والختام والتوقيع، والموضوع والجهة القديمان
    /// بالحاليين، والتواريخ والأرقام حقولاً بين قوسين، فلا يصدر رد بأعداد الرد القديم دون مراجعتها.
    /// </summary>
    public static string AdaptPrecedent(string replyText, string oldIncomingText, IEnumerable<string> oldTerms, IEnumerable<string> newTerms)
    {
        var lines = (replyText ?? "").Split('\n').Select(l => l.Trim()).ToList();
        var start = lines.FindIndex(l => Regex.IsMatch(l, @"^(?:السلام عليكم|تحية طيبة|وبعد)"));
        var body = (start >= 0 ? lines.Skip(start + 1) : lines.SkipWhile(l => l.Length < 40 || Regex.IsMatch(l, @"^(?:الرقم|التاريخ|المرفقات|الموضوع|سعادة|معالي|المكرم)")))
            .TakeWhile(l => !Regex.IsMatch(l, @"^(?:وتقبلوا|وتقبل|والسلام|مع خالص|ولكم جزيل|شاكرين)"))
            .Where(l => l.Length > 0).ToList();
        var text = string.Join("\n", body);
        // سطر الإشارة إلى الخطاب القديم: أول عبارة حتى الفاصلة.
        text = Regex.Replace(text, @"^(?:إشارةً|إشارة|اشارة|بالإشارة|بالاشارة|رداً|ردا|إلحاقاً|الحاقا|عطفاً|عطفا)\s[^،,\n]*[،,]\s*", "");
        foreach (var (o, n) in oldTerms.Zip(newTerms))
            if (o.Length >= 4 && n != "") text = text.Replace(o, n);
        text = DateRx.Replace(text, "[التاريخ]");
        // أرقام الخطاب القديم حقل «[الرقم]»، وبقية الأرقام (أعداد ومبالغ تخص الرد القديم) بين قوسين لتُراجع قبل الإصدار.
        var oldNumbers = Regex.Matches(oldIncomingText ?? "", @"\d{3,}").Select(m => m.Value).ToHashSet();
        text = Regex.Replace(text, @"(?<![\[\d])\d+(?:[.,]\d+)?(?![\]\d])", m => oldNumbers.Contains(m.Value) ? "[الرقم]" : $"[{m.Value}]");
        if (text.Length > 1500) text = text[..1500].TrimEnd() + "…";
        return text.Trim();
    }

    /// <summary>
    /// ترتيب الخطابات الواردة المجاب عنها بتشابهها مع الخطاب الحالي (TF-IDF على الكلمات بعد التجذير الخفيف)،
    /// مع الكلمات المشتركة الأعلى وزناً بصيغتها في الخطاب الحالي.
    /// </summary>
    public static List<(int Index, double Score, List<string> Shared)> Rank(string current, IReadOnlyList<string> candidates)
    {
        static Dictionary<string, int> Bag(string t)
        {
            var bag = new Dictionary<string, int>();
            foreach (var tok in ArabicText.Tokens(t))
                if (!ArabicText.IsStop(tok[0]) && !Boilerplate.Contains(tok[0]) && tok[0].Length >= 3) bag[tok[0]] = bag.GetValueOrDefault(tok[0]) + 1;
            return bag;
        }
        var bags = candidates.Select(Bag).ToList();
        var mine = Bag(current);
        var df = new Dictionary<string, int>();
        foreach (var b in bags.Append(mine)) foreach (var k in b.Keys) df[k] = df.GetValueOrDefault(k) + 1;
        var n = bags.Count + 1;
        Dictionary<string, double> Vec(Dictionary<string, int> b)
        {
            var v = b.ToDictionary(x => x.Key, x => (1 + Math.Log(x.Value)) * Math.Log(1 + (double)n / df[x.Key]));
            var norm = Math.Sqrt(v.Values.Sum(x => x * x));
            return norm == 0 ? v : v.ToDictionary(x => x.Key, x => x.Value / norm);
        }
        var q = Vec(mine);
        // صيغة الكلمة في الخطاب الحالي لعرضها (الجذع وحده يصعب قراءته).
        var surface = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(current ?? "", @"\p{L}+"))
        {
            var c = ArabicText.Canonical(m.Value);
            surface.TryAdd(c, m.Value);
        }
        var result = new List<(int, double, List<string>)>();
        for (var i = 0; i < bags.Count; i++)
        {
            var v = Vec(bags[i]);
            var shared = q.Keys.Where(v.ContainsKey).Select(k => (k, w: q[k] * v[k])).OrderByDescending(x => x.w).ToList();
            var score = shared.Sum(x => x.w);
            result.Add((i, score, shared.Take(5).Select(x => surface.GetValueOrDefault(x.k, x.k)).ToList()));
        }
        return result.OrderByDescending(x => x.Item2).ToList();
    }

    /// <summary>ألفاظ المراسلات المعتادة (الألقاب والتحية والترويسة): لا تدل على تشابه الموضوع.</summary>
    private static readonly HashSet<string> Boilerplate = new[]
    {
        "مدير", "عام", "العام", "الموضوع", "المحترم", "المحترمين", "سعادة", "معالي", "المكرم", "الأستاذ", "الدكتور", "تحية", "التحية", "التقدير", "خالص", "تحياتنا",
        "وتقبلوا", "وبركاته", "ورحمة", "السلام", "وبعد", "إشارة", "إشارةً", "إلحاقاً", "رداً", "بشأن", "بخصوص", "التاريخ", "الرقم", "المرفقات", "خطابكم", "خطابنا", "المقيد",
        "لدينا", "لديكم", "نفيدكم", "نأمل", "نرجو", "التكرم", "تاريخه", "موعد", "أقصاه", "وزارة", "إدارة", "الإدارة", "العامة", "سعادتكم", "وكيل", "رئيس",
    }.Select(ArabicText.Canonical).ToHashSet();

    // ===== الخطاب =====

    /// <summary>نص خطاب الرد بالترتيب المعتاد؛ الترويسة في أوله تجعل رقمه وتاريخه وموضوعه مقروءة للمنصة كأي خطاب.</summary>
    public static string Compose(ReplyLetter l)
    {
        var head = new List<string>();
        if (l.OutgoingNo.Trim() != "") head.Add("الرقم: " + l.OutgoingNo.Trim());
        if (l.DateHijri.Trim() != "") head.Add("التاريخ: " + l.DateHijri.Trim());
        if (l.Subject.Trim() != "") head.Add("الموضوع: " + l.Subject.Trim());
        var reference = l.ReferenceLine.Trim().TrimEnd('،', ',');
        var body = l.Body.Trim();
        var opening = reference == "" ? body : body == "" ? reference + "." : reference + "، " + body;
        var parts = new List<string>
        {
            string.Join("\n", head),
            l.Addressee.Trim() + "    المحترم",
            Greeting,
            opening,
            Closing,
            string.Join("\n", new[] { l.SignerTitle.Trim(), l.SignerName.Trim() }.Where(x => x != "")),
        };
        return string.Join("\n\n", parts.Where(p => p.Trim() != "")).Trim();
    }

    /// <summary>الحقول التي ما زالت بين قوسين معقوفين وتحتاج إلى استكمال قبل الإصدار.</summary>
    public static List<string> Placeholders(string text) =>
        Regex.Matches(text ?? "", @"\[[^\[\]\n]{1,60}\]").Select(m => m.Value).Distinct().ToList();
}
