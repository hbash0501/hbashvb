using System.Reflection;
using System.Text.Json;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence.Semantic;
using Basira.Platform.Processing;

namespace Basira.Platform.Intelligence;

public sealed record ClassificationStats(int Reviewed, int Confirmed, int Changed, int ConfidentReviewed, int ConfidentCorrect, int AutoPending);

/// <summary>
/// التصنيف الآلي في المنصة: شجرة التصنيف (من الإعدادات)، ونموذج يتعلم من المستندات المصنفة يدوياً أو المؤكَّد تصنيفها،
/// والنموذج الدلالي المحلي إن وُجد (بعد معايرته)، وقرارات المستخدمين على التصنيف الآلي التي تُحسب منها الدقة الفعلية.
/// </summary>
public sealed class ClassificationService
{
    public const string TaxonomyKey = "taxonomy";
    public const string CalibrationKey = "semantic_calibration";
    private readonly SettingsStore _settings;
    private readonly DocumentStore _docs;
    private readonly Db _db;
    private readonly SemanticService _semantic;
    private readonly ILogger<ClassificationService> _log;
    private readonly object _lock = new();
    private ArchiveModel? _model;
    private DateTime _builtAt;
    private bool _stale;
    private Taxonomy? _taxonomy;
    private SemanticIndex? _index;
    private bool _indexStale = true;
    private readonly SemaphoreSlim _prepare = new(1, 1);
    private volatile bool _preparePending;

    /// <summary>حالة تجهيز النموذج الدلالي (حساب متجهات الأرشيف والمعايرة) لعرضها للمدير.</summary>
    public string SemanticActivity { get; private set; } = "";
    public string SemanticError { get; private set; } = "";

    public ClassificationService(SettingsStore settings, DocumentStore docs, Db db, SemanticService semantic, ILogger<ClassificationService> log)
    {
        _settings = settings; _docs = docs; _db = db; _semantic = semantic; _log = log;
        // الإدراج الكثيف يغيّر المستندات باستمرار؛ يُعاد بناء النموذج بعد مهلة قصيرة لا مع كل تغيير.
        docs.Changed += () => { lock (_lock) _stale = true; };
        semantic.Loaded += () => SchedulePrepare();
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
        lock (_lock) { _taxonomy = null; _model = null; _index = null; }
        SchedulePrepare();
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
                _stale = false; _builtAt = DateTime.UtcNow; _indexStale = true;
                return _model = ArchiveModel.Build(examples);
            }
        }
    }

    /// <summary>تغيّرت أمثلة التعلم (تأكيد أو تصنيف يدوي): يُعاد بناء النموذج عند الطلب التالي، وتُحسب متجهات الجديد منها.</summary>
    public void Invalidate()
    {
        lock (_lock) { _model = null; _indexStale = true; }
        if (_semantic.Current is not null) SchedulePrepare();
    }

    /// <summary>documentId: يُحفظ متجه المستند ليُستفاد منه إن أكد المستخدم تصنيفه.</summary>
    public ClassificationResult Classify(string text, string title, string? documentId = null)
    {
        var model = Model;
        return DocumentClassifier.Classify(Taxonomy, model, text, title, SemanticFor(text, title, documentId));
    }

    // ===== النموذج الدلالي =====

    public SemanticCalibration? Calibration => SemanticCalibration.FromJson(_settings.GetRaw(CalibrationKey));

    /// <summary>المعايرة صالحة للنموذج المحمّل وشجرة التصنيف الحالية.</summary>
    private SemanticCalibration? ValidCalibration(SemanticModel m)
    {
        var c = Calibration;
        return c is not null && c.ModelId == m.Id && c.TaxonomyHash == SemanticIndex.HashOf(Taxonomy) ? c : null;
    }

    private SemanticSignal? SemanticFor(string text, string title, string? documentId)
    {
        var m = _semantic.Model;
        if (m is null || ValidCalibration(m) is not { Weight: > 0 } cal) return null;
        try
        {
            var index = Index(m);
            var docText = SemanticService.DocumentText(title, text);
            var v = m.Embed(docText);
            if (documentId is not null) _semantic.Store(documentId, m.Id, SemanticService.Hash(docText), v);
            return SemanticIndex.Signal(index.Similarities(v, documentId), cal.Temperature, cal.Weight, cal.ConfidentAt);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.LogWarning(ex, "Semantic classification failed; falling back to rules");
            return null;
        }
    }

    /// <summary>متجهات التصنيفات (تُحسب مرة لكل نموذج وشجرة) ومتجهات المستندات المصنفة يدوياً المحفوظة.</summary>
    private SemanticIndex Index(SemanticModel m)
    {
        var t = Taxonomy;
        var hash = SemanticIndex.HashOf(t);
        SemanticIndex? index;
        lock (_lock) index = _index is not null && _index.ModelId == m.Id && _index.TaxonomyHash == hash ? _index : null;
        if (index is null)
        {
            index = SemanticIndex.Build(m, t);
            lock (_lock) { _index = index; _indexStale = true; }
        }
        bool stale;
        lock (_lock) { stale = _indexStale; _indexStale = false; }
        if (stale)
        {
            var stored = _semantic.StoredAll(m.Id);
            var examples = new List<SemanticIndex.Example>();
            foreach (var d in TrainingDocs(t))
                if (stored.TryGetValue(d.Id, out var v)) examples.Add(new(d.Id, t.Find(d.Category)!.Name, d.IsAuto("sub_category") ? "" : d.SubCategory, v));
            index.SetArchive(examples);
        }
        return index;
    }

    private IEnumerable<Doc> TrainingDocs(Taxonomy t) => _docs.All().Where(d => d.Category != "" && !d.IsAuto("category") && t.Find(d.Category) is not null);

    /// <summary>يجدول تجهيز النموذج الدلالي في الخلفية: متجهات المستندات المصنفة يدوياً، ثم المعايرة إن لم تكن صالحة.</summary>
    public void SchedulePrepare()
    {
        _preparePending = true;
        _ = Task.Run(async () =>
        {
            if (!await _prepare.WaitAsync(0)) return; // جارٍ؛ سيلتقط الطلب الجديد عند انتهائه.
            try
            {
                while (_preparePending)
                {
                    _preparePending = false;
                    Prepare();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (_semantic.Disposed) return; // المنصة تتوقف
                SemanticError = ex.Message;
                _log.LogWarning(ex, "Semantic preparation failed");
            }
            finally { SemanticActivity = ""; _prepare.Release(); }
        });
    }

    /// <summary>ينتظر انتهاء أي تجهيز جارٍ في الخلفية.</summary>
    public async Task WaitPreparedAsync()
    {
        await _prepare.WaitAsync();
        _prepare.Release();
    }

    private void Prepare()
    {
        var m = _semantic.Model;
        if (m is null) return;
        SemanticError = "";
        var t = Taxonomy;
        var docs = TrainingDocs(t).ToList();
        var done = 0;
        foreach (var d in docs)
        {
            SemanticActivity = $"حساب متجهات المستندات المصنفة ({++done}/{docs.Count})";
            var text = SemanticService.DocumentText(d.Title, d.Text);
            var hash = SemanticService.Hash(text);
            if (_semantic.Stored(d.Id, m.Id, hash) is not null) continue;
            _semantic.Store(d.Id, m.Id, hash, m.Embed(text));
        }
        lock (_lock) _indexStale = true;
        if (ValidCalibration(m) is null) Calibrate(m, "تلقائي");
    }

    public sealed record Sample(string Category, string Sub, string Text);

    /// <summary>العينات المصنفة المضمّنة: مجموعة التصميم والعمياء الأولى للمعايرة، والعمياء الثانية لقياس لا تمسه المعايرة.</summary>
    public static (List<Sample> Tune, List<Sample> HeldOut) BundledSamples()
    {
        List<Sample> Load(string name)
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("calibration/" + name)!;
            return JsonSerializer.Deserialize<List<Sample>>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        return (Load("classification-corpus.json").Concat(Load("classification-blind.json")).ToList(), Load("classification-blind2.json"));
    }

    public SemanticCalibration Calibrate(string actor)
    {
        var m = _semantic.Model ?? throw new InvalidOperationException("لا يوجد نموذج دلالي محمّل.");
        _prepare.Wait();
        try { return Calibrate(m, actor); }
        finally { SemanticActivity = ""; _prepare.Release(); }
    }

    /// <summary>
    /// يختار وزن النموذج الدلالي ودرجة الحرارة بأعلى منفعة على عينات المعايرة (الدقة، والتصنيف الواثق الصحيح مع عقوبة الواثق الخاطئ)،
    /// ثم أدنى حد اطمئنان (لا يقل عن حد القواعد) دقته 95% فأكثر عليها،
    /// ويقيس الدقة على العينات المحجوزة بالقواعد وحدها وبالدمج. إن لم يبلغ أي حد 95% يبقى التصنيف بالقواعد وحدها.
    /// </summary>
    private SemanticCalibration Calibrate(SemanticModel m, string actor)
    {
        var t = Taxonomy;
        var archive = Model;
        var index = Index(m);
        var (tuneAll, heldAll) = BundledSamples();
        List<(Sample S, string Title, float[] V)> Prep(List<Sample> set, string label)
        {
            var r = new List<(Sample, string, float[])>();
            var i = 0;
            foreach (var s in set.Where(s => t.Find(s.Category) is not null))
            {
                SemanticActivity = $"معايرة النموذج: {label} ({++i}/{set.Count})";
                var title = TitleBuilder.Build(s.Text);
                r.Add((s, title, m.Embed(SemanticService.DocumentText(title, s.Text))));
            }
            return r;
        }
        var tune = Prep(tuneAll, "عينات المعايرة");
        var held = Prep(heldAll, "عينات القياس");
        bool Same(string a, string b) => Taxonomy.NameKey(a) == Taxonomy.NameKey(b);
        var sims = tune.Select(x => index.Similarities(x.V)).ToList();
        var heldSims = held.Select(x => index.Similarities(x.V)).ToList();

        List<ClassificationResult> Run(List<(Sample S, string Title, float[] V)> set, List<(Dictionary<string, double>, Dictionary<string, Dictionary<string, double>>)> sm, double w, double temp, double at) =>
            set.Select((x, i) => DocumentClassifier.Classify(t, archive, x.S.Text, x.Title, w > 0 ? SemanticIndex.Signal(sm[i], temp, w, at) : null)).ToList();
        double Acc(List<(Sample S, string, float[])> set, List<ClassificationResult> r) => set.Count == 0 ? 0 : (double)set.Where((x, i) => Same(r[i].Category, x.S.Category)).Count() / set.Count;
        double SubAcc(List<(Sample S, string, float[])> set, List<ClassificationResult> r) => set.Count == 0 ? 0 : (double)set.Where((x, i) => Same(r[i].Category, x.S.Category) && Same(r[i].SubCategory, x.S.Sub)).Count() / set.Count;
        (double Precision, double Coverage, int N) Conf(List<(Sample S, string, float[])> set, List<ClassificationResult> r)
        {
            var c = set.Select((x, i) => (x, r: r[i])).Where(p => p.r.Confident).ToList();
            return (c.Count == 0 ? 0 : (double)c.Count(p => Same(p.r.Category, p.x.S.Category)) / c.Count, set.Count == 0 ? 0 : (double)c.Count / set.Count, c.Count);
        }

        // الهدف: الدقة العامة، ونصف وزنها للفرعي، وما يوفره «الواثق» من مراجعة (صحيحه) مع عقوبة مضاعفة لخطئه.
        double Utility(List<(Sample S, string, float[])> set, List<ClassificationResult> r)
        {
            var conf = set.Select((x, i) => (x, r: r[i])).Where(p => p.r.Confident).ToList();
            var right = conf.Count(p => Same(p.r.Category, p.x.S.Category));
            return Acc(set, r) + 0.5 * SubAcc(set, r) + 0.5 * (right - 3.0 * (conf.Count - right)) / Math.Max(1, set.Count);
        }
        var rulesTune = Run(tune, sims, 0, 1, 1);
        double bestScore = Utility(tune, rulesTune), bestW = 0, bestT = 0.05;
        foreach (var temp in new[] { 0.01, 0.02, 0.03, 0.05, 0.08, 0.12 })
            foreach (var w in new[] { 0.2, 0.35, 0.5, 0.65, 0.8, 1.0 })
            {
                SemanticActivity = $"معايرة النموذج: تجربة الوزن {w:0.##} والحرارة {temp:0.##}";
                var r = Run(tune, sims, w, temp, DocumentClassifier.ConfidentProbability);
                var score = Utility(tune, r);
                if (score > bestScore + 1e-9) { bestScore = score; bestW = w; bestT = temp; }
            }
        var confidentAt = DocumentClassifier.ConfidentProbability;
        var note = "";
        if (bestW > 0)
        {
            var found = false;
            // لا يُخفَّض الحد دون حد القواعد: عينات المعايرة نفسها بُنيت منها الكلمات الدالة فدقة القواعد عليها متفائلة.
            for (var at = DocumentClassifier.ConfidentProbability; at <= 0.951; at += 0.05)
            {
                var (p, _, n) = Conf(tune, Run(tune, sims, bestW, bestT, at));
                if (n >= 5 && p >= 0.95) { confidentAt = Math.Round(at, 2); found = true; break; }
            }
            if (!found) { note = "لم يبلغ أي حد اطمئنان دقة 95% على عينات المعايرة؛ بقي التصنيف بالقواعد وحدها."; bestW = 0; }
        }
        else note = "لم يرفع النموذج الدقة (مع نسبة التصنيف الواثق) على عينات المعايرة؛ بقي التصنيف بالقواعد وحدها.";

        var heldRules = Run(held, heldSims, 0, 1, 1);
        var heldFused = bestW > 0 ? Run(held, heldSims, bestW, bestT, confidentAt) : heldRules;
        var heldSemantic = Run(held, heldSims, 1.0, bestT, confidentAt);
        var cr = Conf(held, heldRules); var cf = Conf(held, heldFused);
        var cal = new SemanticCalibration(m.Id, SemanticIndex.HashOf(t), bestW, bestT, confidentAt, tune.Count, held.Count,
            R(Acc(tune, rulesTune)), R(Acc(tune, bestW > 0 ? Run(tune, sims, bestW, bestT, confidentAt) : rulesTune)),
            R(Acc(held, heldRules)), R(Acc(held, heldFused)), R(Acc(held, heldSemantic)), R(SubAcc(held, heldRules)), R(SubAcc(held, heldFused)),
            R(cr.Precision), R(cr.Coverage), R(cf.Precision), R(cf.Coverage), note, Clock.Now());
        _settings.SetRaw(CalibrationKey, cal.ToJson(), actor, "معايرة النموذج الدلالي");
        _log.LogInformation("Semantic calibration: weight {W}, temperature {T}, held-out {Rules} -> {Fused}", bestW, bestT, cal.HeldOutRules, cal.HeldOutFused);
        return cal;
    }

    private static double R(double x) => Math.Round(100 * x, 1);

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
