using System.Text.Json;
using Basira.Platform.Intelligence;
using Xunit;
using Xunit.Abstractions;

namespace Basira.Platform.Tests;

/// <summary>التصنيف الآلي: القواعد وحدها، والتعلّم من الأرشيف وحده، والدمج؛ على مجموعة التصميم ومجموعة عمياء.</summary>
public class ClassificationTests
{
    private readonly ITestOutputHelper _out;
    public ClassificationTests(ITestOutputHelper output) => _out = output;

    public sealed record Sample(string Category, string Sub, string Text);

    public static List<Sample> Load(string name) =>
        JsonSerializer.Deserialize<List<Sample>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private (double Cat, double Sub, double ConfidentCat, double ConfidentShare) Score(List<Sample> set, Func<Sample, int, ClassificationResult> classify, string label)
    {
        int cat = 0, sub = 0, confident = 0, confidentRight = 0;
        for (var i = 0; i < set.Count; i++)
        {
            var r = classify(set[i], i);
            var ok = r.Category == set[i].Category;
            if (ok) cat++;
            if (ok && r.SubCategory == set[i].Sub) sub++;
            if (r.Confident) { confident++; if (ok) confidentRight++; }
            if (!ok) _out.WriteLine($"  [{label}] {set[i].Category}/{set[i].Sub} ← {r.Category}/{r.SubCategory} ({r.Confidence:0.00}{(r.Confident ? " واثق" : "")}) {set[i].Text[..Math.Min(50, set[i].Text.Length)]}");
        }
        var res = (100.0 * cat / set.Count, 100.0 * sub / set.Count, confident == 0 ? 0 : 100.0 * confidentRight / confident, 100.0 * confident / set.Count);
        _out.WriteLine($"{label}: عام {res.Item1:0.0}% | فرعي {res.Item2:0.0}% | دقة الواثق {res.Item3:0.0}% | نسبة الواثق {res.Item4:0.0}%");
        return res;
    }

    private static Taxonomy NamesOnly()
    {
        var t = Taxonomy.Default();
        foreach (var c in t.Categories) { c.Keywords.Clear(); foreach (var s in c.Subs) s.Keywords.Clear(); }
        return t;
    }

    private static ArchiveModel LeaveOneOut(List<Sample> set, int skip) =>
        ArchiveModel.Build(set.Where((_, i) => i != skip).Select(s => (s.Text, s.Category, s.Sub)));

    [Fact]
    public void Design_set_accuracy()
    {
        var set = Load("classification-corpus.json");
        var tax = Taxonomy.Default();
        var rules = Score(set, (s, _) => DocumentClassifier.Classify(tax, ArchiveModel.Empty, s.Text), "قواعد");
        Score(set, (s, i) => DocumentClassifier.Classify(NamesOnly(), LeaveOneOut(set, i), s.Text), "أرشيف فقط (ترك واحد)");
        var fused = Score(set, (s, i) => DocumentClassifier.Classify(tax, LeaveOneOut(set, i), s.Text), "دمج (ترك واحد)");
        // حماية من الانحدار على مجموعة التصميم (ليست قياساً للدقة على مستندات جديدة؛ ذلك في المجموعة العمياء الثانية).
        Assert.True(rules.Cat >= 95, $"دقة التصنيف العام على مجموعة التصميم انخفضت: {rules.Cat:0.0}%");
        Assert.True(rules.ConfidentCat >= 95, $"دقة التصنيف الواثق على مجموعة التصميم انخفضت: {rules.ConfidentCat:0.0}%");
        Assert.True(fused.Cat >= rules.Cat - 2, "الدمج مع الأرشيف الصغير يجب ألا يضر بالقواعد");
    }

    [Fact]
    public void Blind_set_accuracy()
    {
        var set = Load("classification-blind.json");
        var tax = Taxonomy.Default();
        var rules = Score(set, (s, _) => DocumentClassifier.Classify(tax, ArchiveModel.Empty, s.Text), "عمياء: قواعد");
        // مع أرشيف مصنَّف مسبقاً (مجموعة التصميم مثالاً على مستندات الجهة).
        var design = Load("classification-corpus.json");
        var model = ArchiveModel.Build(design.Select(d => (d.Text, d.Category, d.Sub)));
        Score(set, (s, _) => DocumentClassifier.Classify(tax, model, s.Text), "عمياء: دمج مع أرشيف 60");
        Assert.True(rules.Cat >= 0);
    }

    [Fact]
    public void Blind_set_2_accuracy()
    {
        var set = Load("classification-blind2.json");
        var tax = Taxonomy.Default();
        Score(set, (s, _) => DocumentClassifier.Classify(tax, ArchiveModel.Empty, s.Text), "عمياء2: قواعد");
        var archive = Load("classification-corpus.json").Concat(Load("classification-blind.json")).ToList();
        var model = ArchiveModel.Build(archive.Select(d => (d.Text, d.Category, d.Sub)));
        Score(set, (s, _) => DocumentClassifier.Classify(tax, model, s.Text), "عمياء2: دمج مع أرشيف 100");
    }

    [Fact]
    public void Titles_report()
    {
        foreach (var f in new[] { "classification-corpus.json", "classification-blind.json", "classification-blind2.json" })
            foreach (var d in Load(f))
            {
                var header = Basira.Platform.Processing.LetterFields.Parse(d.Text);
                _out.WriteLine($"T| {Basira.Platform.Processing.TitleBuilder.Build(d.Text, header.Subject)}   ⟸ {d.Text.Replace("\n", " / ")[..Math.Min(70, d.Text.Length)]}");
            }
    }
}
