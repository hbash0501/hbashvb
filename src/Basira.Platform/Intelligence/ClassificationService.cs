using System.Text.Json;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;

namespace Basira.Platform.Intelligence;

public sealed record ClassificationStats(int Reviewed, int Confirmed, int Changed, int ConfidentReviewed, int ConfidentCorrect, int AutoPending);

/// <summary>
/// التصنيف الآلي في المنصة: شجرة التصنيف (من الإعدادات)، ونموذج يتعلم من المستندات المصنفة يدوياً أو المؤكَّد تصنيفها،
/// وقرارات المستخدمين على التصنيف الآلي التي تُحسب منها الدقة الفعلية.
/// </summary>
public sealed class ClassificationService
{
    public const string TaxonomyKey = "taxonomy";
    private readonly SettingsStore _settings;
    private readonly DocumentStore _docs;
    private readonly Db _db;
    private readonly object _lock = new();
    private ArchiveModel? _model;
    private DateTime _builtAt;
    private bool _stale;
    private Taxonomy? _taxonomy;

    public ClassificationService(SettingsStore settings, DocumentStore docs, Db db)
    {
        _settings = settings; _docs = docs; _db = db;
        // الإدراج الكثيف يغيّر المستندات باستمرار؛ يُعاد بناء النموذج بعد مهلة قصيرة لا مع كل تغيير.
        docs.Changed += () => { lock (_lock) _stale = true; };
    }

    public Taxonomy Taxonomy
    {
        get { lock (_lock) return _taxonomy ??= Taxonomy.FromJson(_settings.GetRaw(TaxonomyKey)); }
    }

    public void SaveTaxonomy(Taxonomy t, string actor)
    {
        foreach (var c in t.Categories)
        {
            c.Name = c.Name.Trim();
            c.Keywords = c.Keywords.Select(k => k.Trim()).Where(k => k != "").Distinct().ToList();
            c.Subs = c.Subs.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
            foreach (var s in c.Subs) { s.Name = s.Name.Trim(); s.Keywords = s.Keywords.Select(k => k.Trim()).Where(k => k != "").Distinct().ToList(); }
        }
        t.Categories = t.Categories.Where(c => c.Name != "").GroupBy(c => Taxonomy.NameKey(c.Name)).Select(g => g.First()).ToList();
        if (t.Categories.Count == 0) throw new ArgumentException("شجرة التصنيف لا تحوي أي تصنيف.");
        _settings.SetRaw(TaxonomyKey, t.ToJson(), actor, "تعديل شجرة التصنيف");
        lock (_lock) { _taxonomy = null; _model = null; }
    }

    /// <summary>أمثلة التعلم: مستندات صنّفها مستخدم أو أكد تصنيفها، وتصنيفها ضمن الشجرة.</summary>
    public ArchiveModel Model
    {
        get
        {
            lock (_lock)
            {
                if (_model is not null && (!_stale || DateTime.UtcNow - _builtAt < TimeSpan.FromSeconds(30))) return _model;
                var t = Taxonomy;
                var examples = new List<(string, string, string)>();
                foreach (var d in _docs.All())
                {
                    if (d.Category == "" || d.IsAuto("category") || t.Find(d.Category) is not { } cat) continue;
                    var sub = d.IsAuto("sub_category") ? "" : d.SubCategory;
                    examples.Add((d.Title + "\n" + d.Text, cat.Name, sub));
                }
                _stale = false; _builtAt = DateTime.UtcNow;
                return _model = ArchiveModel.Build(examples);
            }
        }
    }

    /// <summary>تغيّرت أمثلة التعلم (تأكيد أو تصنيف يدوي): يُعاد بناء النموذج عند الطلب التالي.</summary>
    public void Invalidate() { lock (_lock) _model = null; }

    public ClassificationResult Classify(string text, string title) => DocumentClassifier.Classify(Taxonomy, Model, text, title);

    /// <summary>يملأ التصنيف إن كان فارغاً أو آلياً من قبل (لا يمس ما صنّفه مستخدم).</summary>
    public bool Apply(Doc d, ClassificationResult r)
    {
        if (d.Category != "" && !d.IsAuto("category")) return false;
        if (r.Category == "") return false;
        d.Category = r.Category; d.SetAuto("category", true);
        if (d.SubCategory == "" || d.IsAuto("sub_category")) { d.SubCategory = r.SubCategory; d.SetAuto("sub_category", r.SubCategory != ""); }
        d.Classification = JsonSerializer.Serialize(new
        {
            confidence = r.Confidence, subConfidence = r.SubConfidence, confident = r.Confident, method = r.Method, evidence = r.Evidence,
            alternatives = r.Alternatives.Select(a => new { category = a.Category, sub = a.SubCategory, score = a.Score }),
        });
        return true;
    }

    public static (double Confidence, bool Confident) ReadConfidence(Doc d)
    {
        if (d.Classification == "") return (0, false);
        try
        {
            using var j = JsonDocument.Parse(d.Classification);
            return (j.RootElement.GetProperty("confidence").GetDouble(), j.RootElement.GetProperty("confident").GetBoolean());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return (0, false); }
    }

    /// <summary>يسجل قرار مستخدم على تصنيف آلي (تأكيد أو تغيير) لقياس الدقة الفعلية.</summary>
    public void RecordFeedback(Doc before, string finalCategory, string finalSub, string action, string actor)
    {
        if (!before.IsAuto("category")) return;
        var (conf, confident) = ReadConfidence(before);
        using var c = _db.Open();
        c.Exec("""
            INSERT INTO classification_feedback(document_id, predicted_category, predicted_sub, final_category, final_sub, confidence, confident, action, actor, at)
            VALUES(@Id, @Category, @SubCategory, @finalCategory, @finalSub, @conf, @confident, @action, @actor, @at)
            """, new { before.Id, before.Category, before.SubCategory, finalCategory, finalSub, conf, confident = confident ? 1 : 0, action, actor, at = Clock.Now() });
    }

    public ClassificationStats Stats()
    {
        using var c = _db.Open();
        var rows = c.Query("SELECT predicted_category, final_category, confident FROM classification_feedback",
            r => (Predicted: r.GetString(0), Final: r.GetString(1), Confident: r.GetInt64(2) == 1));
        var pending = Convert.ToInt32(c.Scalar("SELECT COUNT(*) FROM documents WHERE ',' || auto_fields || ',' LIKE '%,category,%'"));
        bool Same(string a, string b) => Taxonomy.NameKey(a) == Taxonomy.NameKey(b);
        return new ClassificationStats(rows.Count, rows.Count(r => Same(r.Predicted, r.Final)), rows.Count(r => !Same(r.Predicted, r.Final)),
            rows.Count(r => r.Confident), rows.Count(r => r.Confident && Same(r.Predicted, r.Final)), pending);
    }
}
