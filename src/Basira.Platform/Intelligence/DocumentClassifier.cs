using System.Text.RegularExpressions;
using AIDocumentArchiveVB;

namespace Basira.Platform.Intelligence;

/// <summary>تقطيع عربي خفيف للتصنيف: توحيد الحروف، ونزع «ال» وحروف العطف والجر الملتصقة بها، وأشهر اللواحق.</summary>
public static class ArabicText
{
    private static readonly string[] Prefixes = { "وال", "بال", "كال", "فال", "لل", "ال" };
    private static readonly string[] Suffixes = { "ات", "ين", "ون", "ها", "هم", "كم", "نا", "ه" };
    // ضمائر الملكية على التاء المربوطة: «استقالتي/استقالته/استقالتها» ← «استقال» كـ«استقالة».
    private static readonly string[] Possessive = { "تهما", "تكما", "تهم", "تكم", "تنا", "تها", "تي", "ته", "تك" };
    private static readonly HashSet<string> Stops = new(new[]
    {
        "في", "من", "علي", "الي", "عن", "مع", "او", "ثم", "ان", "لا", "ما", "قد", "هذا", "هذه", "ذلك", "تلك", "التي", "الذي", "الذين", "بان", "بانه", "كما", "حيث",
        "عند", "لدي", "لدى", "كل", "بعض", "غير", "بين", "حول", "نحو", "تم", "وقد", "وذلك", "حتي", "اذا", "لكم", "لنا", "عليه", "عليها", "منه", "منها", "به", "بها",
        "سعاده", "المحترم", "السلام", "عليكم", "ورحمه", "الله", "وبركاته", "وتقبلوا", "تحياتنا", "تحيه", "نفيدكم", "ناامل", "نامل", "التكرم", "الاطلاع", "الافاده",
        "رقم", "وتاريخ", "تاريخ", "بتاريخ", "الموضوع", "المرفقات", "يوجد", "اشاره", "خطاب", "خطابكم", "خطابنا", "سعادتكم", "عاجل", "شاكرين", "تعاونكم",
    });

    public static string Canonical(string word)
    {
        var n = BasiraEngine.Normalize(word);
        foreach (var p in Prefixes)
            if (n.StartsWith(p, StringComparison.Ordinal) && n.Length - p.Length >= 3) { n = n[p.Length..]; break; }
        foreach (var p in Possessive)
            if (n.EndsWith(p, StringComparison.Ordinal) && n.Length - p.Length >= 4) return n[..^p.Length];
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var s in Suffixes)
                if (n.EndsWith(s, StringComparison.Ordinal) && n.Length - s.Length >= 3) { n = n[..^s.Length]; break; }
            if (n.Length > 4 && n.EndsWith('ي')) n = n[..^1];
        }
        return n;
    }

    /// <summary>كلمات النص بصيغها البديلة (الكلمة بعد نزع «و/ف/ب/ل» الملتصقة إن كانت طويلة).</summary>
    public static List<string[]> Tokens(string text)
    {
        var list = new List<string[]>();
        foreach (Match m in Regex.Matches(BasiraEngine.Normalize(text ?? ""), @"\p{L}+"))
        {
            var w = m.Value;
            if (w.Length < 2) continue;
            var c = Canonical(w);
            if (w.Length >= 5 && "وفبل".Contains(w[0]) && !w.StartsWith("ال", StringComparison.Ordinal))
            {
                var alt = Canonical(w[1..]);
                list.Add(alt == c ? new[] { c } : new[] { c, alt });
            }
            else list.Add(new[] { c });
        }
        return list;
    }

    public static bool IsStop(string canonical) => Stops.Contains(canonical) || canonical.Length < 2;

    public static string[] KeywordKey(string keyword) =>
        Regex.Matches(BasiraEngine.Normalize(keyword.TrimStart('!')), @"\p{L}+").Select(m => Canonical(m.Value)).ToArray();
}

public sealed record ClassAlternative(string Category, string SubCategory, double Score);

/// <summary>نتيجة التصنيف مع درجة الاطمئنان وأسبابها (الكلمات الدالة ومستندات الأرشيف المشابهة).</summary>
public sealed record ClassificationResult(string Category, string SubCategory, double Confidence, double SubConfidence, bool Confident,
    List<ClassAlternative> Alternatives, List<string> Evidence, string Method)
{
    public static readonly ClassificationResult None = new("", "", 0, 0, false, new(), new(), "");
}

/// <summary>
/// نموذج تعلّم من الأرشيف (Naive Bayes متعدد الحدود على كلمات ثنائية الحضور): يتعلم من المستندات التي صنّفها المستخدمون
/// أو أكدوا تصنيفها، فيتكيّف مع أسلوب الجهة ومصطلحاتها.
/// </summary>
public sealed class ArchiveModel
{
    private sealed class Label
    {
        public int Docs;
        public int Total;
        public Dictionary<string, int> Counts = new();
    }

    private readonly Dictionary<string, Label> _categories = new();
    private readonly Dictionary<string, Dictionary<string, Label>> _subs = new();
    private readonly HashSet<string> _vocab = new();
    public int TrainingDocs { get; private set; }

    public static readonly ArchiveModel Empty = new();

    public static ArchiveModel Build(IEnumerable<(string Text, string Category, string SubCategory)> examples)
    {
        var m = new ArchiveModel();
        foreach (var (text, cat, sub) in examples)
        {
            var feats = Features(text);
            if (feats.Count == 0) continue;
            m.TrainingDocs++;
            Add(m._categories, cat, feats, m._vocab);
            if (sub != "")
            {
                if (!m._subs.TryGetValue(cat, out var subs)) m._subs[cat] = subs = new();
                Add(subs, sub, feats, null);
            }
        }
        return m;
    }

    private static void Add(Dictionary<string, Label> labels, string name, HashSet<string> feats, HashSet<string>? vocab)
    {
        if (!labels.TryGetValue(name, out var l)) labels[name] = l = new Label();
        l.Docs++;
        foreach (var f in feats)
        {
            l.Counts[f] = l.Counts.GetValueOrDefault(f) + 1;
            l.Total++;
            vocab?.Add(f);
        }
    }

    public static HashSet<string> Features(string text) =>
        ArabicText.Tokens(text).Select(t => t[0]).Where(t => !ArabicText.IsStop(t)).ToHashSet();

    public int DocsIn(string category) => _categories.TryGetValue(category, out var l) ? l.Docs : 0;
    public int LabelCount => _categories.Count;

    public Dictionary<string, double> CategoryPosterior(HashSet<string> feats) => Posterior(_categories, feats);

    public Dictionary<string, double> SubPosterior(string category, HashSet<string> feats) =>
        _subs.TryGetValue(category, out var subs) ? Posterior(subs, feats) : new();

    /// <summary>احتمالات لاحقة مُلطَّفة: متوسط لوغاريتم الاحتمال لكل كلمة (NB مفرط الثقة في النصوص الطويلة).</summary>
    private Dictionary<string, double> Posterior(Dictionary<string, Label> labels, HashSet<string> feats)
    {
        var result = new Dictionary<string, double>();
        if (labels.Count < 2 || feats.Count == 0) return result;
        var docs = labels.Values.Sum(l => l.Docs);
        var v = Math.Max(1, _vocab.Count);
        var known = feats.Where(_vocab.Contains).ToList();
        if (known.Count == 0) return result;
        var scores = new Dictionary<string, double>();
        foreach (var (name, l) in labels)
        {
            double s = Math.Log((l.Docs + 1.0) / (docs + labels.Count));
            foreach (var f in known) s += Math.Log((l.Counts.GetValueOrDefault(f) + 0.5) / (l.Total + 0.5 * v));
            scores[name] = s / known.Count;
        }
        var max = scores.Values.Max();
        var z = scores.Values.Sum(s => Math.Exp(6 * (s - max)));
        foreach (var (name, s) in scores) result[name] = Math.Exp(6 * (s - max)) / z;
        return result;
    }
}

/// <summary>
/// المصنِّف: قواعد بكلمات دالة من شجرة التصنيف (أوزان أعلى لما في الموضوع والعنوان)، يُدمج معها نموذج الأرشيف
/// بوزن يزداد مع عدد المستندات المصنفة يدوياً. لا يُرسل أي نص إلى خدمة خارجية.
/// </summary>
public static class DocumentClassifier
{
    /// <summary>حدّا الاطمئنان: دونهما يُعرض التصنيف «مقترحاً» يؤكده المستخدم. معايَران على مجموعة التصميم.</summary>
    public const double ConfidentProbability = 0.55;
    public const double ConfidentMargin = 0.2;
    /// <summary>أدنى قوة دليل للاطمئنان: كلمة قوية في الموضوع (6)، أو قوية وأخرى معها في المتن.</summary>
    public const double MinEvidence = 5;

    private sealed record Scored(double Score, List<string> Evidence);

    public static ClassificationResult Classify(Taxonomy taxonomy, ArchiveModel model, string text, string subject = "")
    {
        text ??= ""; subject ??= "";
        var body = ArabicText.Tokens(text);
        var head = ArabicText.Tokens(subject);
        if (body.Count + head.Count == 0) return ClassificationResult.None;

        // ===== القواعد =====
        var catScores = new Dictionary<string, (double Score, string BestSub, Dictionary<string, Scored> Subs, List<string> Evidence)>();
        foreach (var c in taxonomy.Categories)
        {
            var catEvidence = new List<string>();
            var catKw = Match(c.Keywords, body, head, catEvidence);
            var subs = new Dictionary<string, Scored>();
            foreach (var s in c.Subs)
            {
                var ev = new List<string>();
                subs[s.Name] = new Scored(Match(s.Keywords, body, head, ev), ev);
            }
            var ordered = subs.OrderByDescending(x => x.Value.Score).ToList();
            var best = ordered.FirstOrDefault();
            var score = 0.5 * catKw + (best.Value?.Score ?? 0) + 0.25 * ordered.Skip(1).Sum(x => x.Value.Score);
            catScores[c.Name] = (score, best.Value is { Score: > 0 } ? best.Key : "", subs, catEvidence);
        }
        var ruleTotal = catScores.Values.Sum(x => x.Score);
        var ruleP = catScores.ToDictionary(x => x.Key, x => ruleTotal > 0 ? x.Value.Score / ruleTotal : 0);

        // ===== نموذج الأرشيف =====
        var feats = body.Concat(head).Select(t => t[0]).Where(t => !ArabicText.IsStop(t)).ToHashSet();
        // النموذج مدرَّب على أسماء الشجرة نفسها (يوحّدها من يبنيه).
        var nb = model.CategoryPosterior(feats).Where(x => taxonomy.Find(x.Key) is not null).ToDictionary(x => x.Key, x => x.Value);
        // وزن نموذج الأرشيف بقدر بياناته: يحتاج عشرات الأمثلة لكل تصنيف ليتفوق على القواعد (قيس: 5 أمثلة لكل تصنيف تعطي 32% فقط).
        var perLabel = model.LabelCount == 0 ? 0 : (double)model.TrainingDocs / model.LabelCount;
        var alpha = nb.Count == 0 ? 0 : ruleTotal <= 0 ? 1 : Math.Clamp((perLabel - 5) / 50.0, 0, 0.5);
        var fused = taxonomy.Categories.ToDictionary(c => c.Name, c => (1 - alpha) * ruleP.GetValueOrDefault(c.Name) + alpha * nb.GetValueOrDefault(c.Name));
        var ranked = fused.OrderByDescending(x => x.Value).ToList();
        if (ranked.Count == 0 || ranked[0].Value <= 0) return ClassificationResult.None;

        var cat = ranked[0].Key;
        var p1 = ranked[0].Value; var p2 = ranked.Count > 1 ? ranked[1].Value : 0;

        // ===== التصنيف الفرعي =====
        var subRule = catScores[cat].Subs;
        var subTotal = subRule.Values.Sum(x => x.Score);
        var subNb = model.SubPosterior(cat, feats);
        var subAlpha = subNb.Count == 0 ? 0 : subTotal <= 0 ? 1 : alpha;
        var subFused = subRule.ToDictionary(x => x.Key, x => (1 - subAlpha) * (subTotal > 0 ? x.Value.Score / subTotal : 0) + subAlpha * subNb.GetValueOrDefault(x.Key));
        foreach (var (name, p) in subNb) if (!subFused.ContainsKey(name)) subFused[name] = subAlpha * p; // فرعي تعلّمه الأرشيف خارج الشجرة
        var subRanked = subFused.OrderByDescending(x => x.Value).ToList();
        var sub = subRanked.Count > 0 && subRanked[0].Value > 0 ? subRanked[0].Key : "";
        var s1 = subRanked.Count > 0 ? subRanked[0].Value : 0;

        var evidence = new List<string>();
        if (sub != "" && subRule.TryGetValue(sub, out var se)) evidence.AddRange(se.Evidence);
        evidence.AddRange(catScores[cat].Evidence);
        evidence = evidence.Distinct().Take(5).ToList();
        if (alpha > 0 && nb.GetValueOrDefault(cat) >= 0.5)
            evidence.Add($"يشبه مستندات الأرشيف المصنفة «{cat}» ({model.DocsIn(cat)})");
        // الاطمئنان يحتاج نسبة غالبة ودليلاً كافياً معاً: كلمة واحدة ضعيفة تجعل النسبة 100% دون أن تكفي.
        var evidenceStrength = (1 - alpha) * catScores[cat].Score + alpha * 10 * nb.GetValueOrDefault(cat);
        var confident = p1 >= ConfidentProbability && p1 - p2 >= ConfidentMargin && evidenceStrength >= MinEvidence;
        var alternatives = ranked.Take(3).Where(x => x.Value > 0)
            .Select(x => new ClassAlternative(x.Key, x.Key == cat ? sub : catScores[x.Key].BestSub, Math.Round(x.Value, 3))).ToList();
        var method = alpha <= 0 ? "قواعد" : alpha >= 1 ? "الأرشيف" : "قواعد + الأرشيف";
        return new ClassificationResult(cat, sub, Math.Round(p1, 3), Math.Round(s1, 3), confident, alternatives, evidence, method);
    }

    /// <summary>درجة تطابق الكلمات الدالة: القوية 3 والعبارة 2 والمفردة 1، مضاعفة في الموضوع، مع أثر متناقص للتكرار.</summary>
    private static double Match(List<string> keywords, List<string[]> body, List<string[]> head, List<string> evidence)
    {
        double total = 0;
        foreach (var kw in keywords)
        {
            var key = ArabicText.KeywordKey(kw);
            if (key.Length == 0) continue;
            var weight = kw.StartsWith('!') ? 3.0 : key.Length > 1 ? 2.0 : 1.0;
            var inBody = Count(body, key); var inHead = Count(head, key);
            if (inBody + inHead == 0) continue;
            total += weight * (1 + 0.25 * Math.Min(inBody + inHead - 1, 4)) * (inHead > 0 ? 2 : 1);
            evidence.Add(kw.TrimStart('!'));
        }
        return total;
    }

    private static int Count(List<string[]> tokens, string[] key)
    {
        var n = 0;
        for (var i = 0; i + key.Length <= tokens.Count; i++)
        {
            var ok = true;
            for (var j = 0; j < key.Length && ok; j++) ok = Array.IndexOf(tokens[i + j], key[j]) >= 0;
            if (ok) n++;
        }
        return n;
    }
}
