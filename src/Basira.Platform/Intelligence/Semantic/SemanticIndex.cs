using System.Text.Json;

namespace Basira.Platform.Intelligence.Semantic;

/// <summary>
/// ما يُقارن به متجه المستند: متجه لكل تصنيف عام وفرعي (من اسمه وكلماته الدالة)، ومتجهات المستندات التي صنّفها المستخدمون.
/// التشابه مع التصنيف هو الأعلى بين متجهه ومتوسط أقرب مستندين منه في الأرشيف.
/// </summary>
public sealed class SemanticIndex
{
    public sealed record Example(string DocumentId, string Category, string SubCategory, float[] Vector);

    public string ModelId { get; }
    public string TaxonomyHash { get; }
    private readonly Dictionary<string, float[]> _categories;
    private readonly Dictionary<string, Dictionary<string, float[]>> _subs;
    private List<Example> _archive = new();

    private SemanticIndex(string modelId, string taxonomyHash, Dictionary<string, float[]> categories, Dictionary<string, Dictionary<string, float[]>> subs)
    {
        ModelId = modelId; TaxonomyHash = taxonomyHash; _categories = categories; _subs = subs;
    }

    public int ArchiveCount => _archive.Count;

    public static string HashOf(Taxonomy t) => SemanticService.Hash(t.ToJson());

    public static string Describe(string category, IEnumerable<string> keywords, string? sub = null)
    {
        var words = string.Join("، ", keywords.Select(k => k.TrimStart('!')).Where(k => k != "").Take(24));
        return (sub is null ? category : $"{category}: {sub}") + (words == "" ? "" : ". " + words);
    }

    public static SemanticIndex Build(SemanticModel model, Taxonomy taxonomy)
    {
        var categories = new Dictionary<string, float[]>();
        var subs = new Dictionary<string, Dictionary<string, float[]>>();
        foreach (var c in taxonomy.Categories)
        {
            categories[c.Name] = model.Embed(Describe(c.Name, c.Keywords));
            subs[c.Name] = c.Subs.ToDictionary(s => s.Name, s => model.Embed(Describe(c.Name, s.Keywords.Concat(c.Keywords.Take(4)), s.Name)));
        }
        return new SemanticIndex(model.Id, HashOf(taxonomy), categories, subs);
    }

    public void SetArchive(List<Example> examples) => _archive = examples;

    /// <summary>تشابه المتجه مع كل تصنيف عام، ومع كل فرعي داخل عامه. exclude: مستند لا يُقارن بنفسه.</summary>
    public (Dictionary<string, double> Category, Dictionary<string, Dictionary<string, double>> Subs) Similarities(float[] v, string? exclude = null)
    {
        var neighbors = _archive.Where(e => e.DocumentId != exclude && _categories.ContainsKey(e.Category))
            .Select(e => (e, Sim: (double)SemanticModel.Cosine(v, e.Vector))).ToList();
        double Knn(IEnumerable<double> sims)
        {
            var top = sims.OrderByDescending(x => x).Take(2).ToList();
            return top.Count == 0 ? double.NegativeInfinity : top.Average();
        }
        var cat = new Dictionary<string, double>();
        var subs = new Dictionary<string, Dictionary<string, double>>();
        foreach (var (name, proto) in _categories)
        {
            var subSims = _subs[name].ToDictionary(s => s.Key, s => (double)SemanticModel.Cosine(v, s.Value));
            foreach (var g in neighbors.Where(n => n.e.Category == name && n.e.SubCategory != "").GroupBy(n => n.e.SubCategory))
                subSims[g.Key] = Math.Max(subSims.GetValueOrDefault(g.Key, double.NegativeInfinity), Knn(g.Select(x => x.Sim)));
            var best = Math.Max(SemanticModel.Cosine(v, proto), subSims.Values.DefaultIfEmpty(double.NegativeInfinity).Max());
            cat[name] = Math.Max(best, Knn(neighbors.Where(n => n.e.Category == name).Select(n => n.Sim)));
            subs[name] = subSims;
        }
        return (cat, subs);
    }

    /// <summary>تحويل التشابه إلى احتمالات (softmax بدرجة حرارة معايَرة).</summary>
    public static Dictionary<string, double> Softmax(Dictionary<string, double> sims, double temperature)
    {
        if (sims.Count == 0) return new();
        var max = sims.Values.Max();
        var e = sims.ToDictionary(x => x.Key, x => Math.Exp((x.Value - max) / temperature));
        var sum = e.Values.Sum();
        return e.ToDictionary(x => x.Key, x => x.Value / sum);
    }

    public static SemanticSignal Signal((Dictionary<string, double> Category, Dictionary<string, Dictionary<string, double>> Subs) sims, double temperature, double weight, double confidentAt) =>
        new(Softmax(sims.Category, temperature), sims.Subs.ToDictionary(x => x.Key, x => Softmax(x.Value, temperature)), weight, confidentAt);
}

/// <summary>نتيجة معايرة النموذج الدلالي لنموذج وشجرة تصنيف بعينهما، مع قياس الدقة على عينات لم تُستخدم في المعايرة.</summary>
public sealed record SemanticCalibration(
    string ModelId, string TaxonomyHash, double Weight, double Temperature, double ConfidentAt,
    int TuneCount, int HeldOutCount,
    double TuneRules, double TuneFused,
    double HeldOutRules, double HeldOutFused, double HeldOutSemanticOnly, double HeldOutSubRules, double HeldOutSubFused,
    double ConfidentPrecisionRules, double ConfidentCoverageRules, double ConfidentPrecisionFused, double ConfidentCoverageFused,
    string Note, string At)
{
    public static SemanticCalibration? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<SemanticCalibration>(json); }
        catch (JsonException) { return null; }
    }

    public string ToJson() => JsonSerializer.Serialize(this);
}
