using System.Globalization;
using System.Text.RegularExpressions;
using AIDocumentArchiveVB;
using SkiaSharp;

namespace Basira.Platform.Processing.Ocr;

/// <summary>
/// التحقق بمحركين: الحقول الحرجة (الرقم، والتاريخ، والموضوع، والإشارات، والمهل) تُقرأ من نص المحرك المرئي وتُطابق بقراءة
/// Tesseract المستقلة (وفحوصه ومقتطعاته). ما اتفق عليه المحركان يُعتمد آلياً؛ وما اختلفا فيه أو قرأه محرك واحد يُعرض على المراجع.
/// اختلاف طبيعة الخطأ بين المحركين (نموذج لغوي قد يختلق، وقارئ حروف قد يشوّه) يجعل اتفاقهما دليلاً قوياً.
/// </summary>
public static class CrossCheck
{
    public const string SingleEngine = "قراءة محرك واحد؛ ثبّت Tesseract ليُتحقق منها آلياً";
    public const string Disagree = "المحركان قرآ الحقل قراءتين مختلفتين";

    private static string Squash(string s) => Regex.Replace(BasiraEngine.Normalize(s ?? ""), @"[^\p{L}\p{Nd}]", "");

    /// <summary>تشابه نصين قصيرين (1 - مسافة التحرير النسبية) بعد التوحيد.</summary>
    public static double Similarity(string a, string b)
    {
        a = Squash(a); b = Squash(b);
        if (a.Length == 0 || b.Length == 0) return a == b ? 1 : 0;
        var d = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) d[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            var prev = d[0]; d[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var t = d[j];
                d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), prev + (a[i - 1] == b[j - 1] ? 0 : 1));
                prev = t;
            }
        }
        return 1 - (double)d[b.Length] / Math.Max(a.Length, b.Length);
    }

    public static List<FieldCheck> Build(string primary, OcrDocument? second)
    {
        var checks = new List<FieldCheck>();
        var head = LetterFields.Locate(primary);
        var secondHead = second is null ? new List<LetterFields.HeaderSpan>() : LetterFields.Locate(second.Text);
        FieldCheck? Of(string field) => second?.Checks.FirstOrDefault(k => k.Field == field);

        foreach (var field in new[] { "number", "date", "subject" })
        {
            var mine = head.FirstOrDefault(h => h.Field == field);
            var theirs = Of(field);
            // قراءات المحرك الثاني: قيمة فحصه ومرشحاته، وما في نصه الكامل.
            var readings = new List<string>();
            if (theirs is not null) readings.AddRange(new[] { theirs.Value }.Concat(theirs.Candidates).Where(v => v != ""));
            if (secondHead.FirstOrDefault(h => h.Field == field) is { } sh) readings.Add(sh.Value);
            readings = readings.Distinct().ToList();
            var box = theirs?.Box ?? SKRectI.Empty;
            var page = theirs?.Page ?? 0;
            if (mine is null)
            {
                // قرأه Tesseract ولم يقرأه المحرك المرئي: يُعرض للتأكيد دون موضع في النص.
                if (readings.Count > 0) checks.Add(new FieldCheck(field, readings[0], readings, false, page, box, -1, 0, "لم يقرأه المحرك المرئي"));
                continue;
            }
            bool Same(string r) => field == "subject" ? Similarity(r, mine.Value) >= 0.9 : field == "number" ? CorrespondenceReferenceParser.NumbersMatch(r, mine.Value) : r == mine.Value;
            var agree = readings.Any(Same);
            var candidates = new[] { mine.Value }.Concat(readings.Where(r => !Same(r))).Distinct().ToList();
            checks.Add(new FieldCheck(field, mine.Value, candidates, agree, page, box, mine.Offset, mine.Length,
                agree ? "" : second is null ? SingleEngine : readings.Count == 0 ? "لم يقرأه Tesseract" : Disagree));
        }

        var theirRefs = second is null ? new List<CorrespondenceReference>() : CorrespondenceReferenceParser.Parse(second.Text);
        foreach (var r in CorrespondenceReferenceParser.Parse(primary))
        {
            var key = r.Number + "@" + (r.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
            var match = theirRefs.FirstOrDefault(x => CorrespondenceReferenceParser.NumbersMatch(x.Number, r.Number) && x.Date == r.Date);
            var near = match ?? theirRefs.FirstOrDefault(x => CorrespondenceReferenceParser.NumbersMatch(x.Number, r.Number));
            var check = second?.Checks.FirstOrDefault(k => k.Field == "reference" && k.Value.Split('@')[0] is var n && CorrespondenceReferenceParser.NumbersMatch(n, r.Number));
            var candidates = new List<string> { key };
            if (near is not null && match is null) candidates.Add(near.Number + "@" + (near.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""));
            var dateOk = r.DateText == "" || r.Date.HasValue;
            var ok = match is not null && dateOk;
            checks.Add(new FieldCheck("reference", key, candidates, ok, check?.Page ?? 0, check?.Box ?? SKRectI.Empty, r.Offset, r.Length,
                ok ? "" : second is null ? SingleEngine : !dateOk ? "تاريخ الإشارة غير صالح" : near is not null ? "اختلف المحركان في تاريخ الإشارة" : "لم يقرأ Tesseract هذه الإشارة"));
        }

        var theirDeadlines = second is null ? new List<DeadlineHint>() : DeadlineExtractor.Extract(second.Text, null);
        foreach (var h in DeadlineExtractor.Extract(primary, null))
        {
            var value = h.IsRelative ? h.Days.ToString(CultureInfo.InvariantCulture) : h.Due?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
            var ok = theirDeadlines.Any(x => x.IsRelative == h.IsRelative && (h.IsRelative ? x.Days == h.Days && x.BusinessDays == h.BusinessDays : x.Due == h.Due));
            var check = second?.Checks.FirstOrDefault(k => k.Field == "deadline" && k.Value == value);
            checks.Add(new FieldCheck("deadline", value, new List<string>(), ok, check?.Page ?? 0, check?.Box ?? SKRectI.Empty, h.Offset, h.Length,
                ok ? "" : second is null ? SingleEngine : "اختلف المحركان في المهلة"));
        }
        // مسح رديء: لا اعتماد آلي مهما اتفقت القراءتان (كما في قراءة المحرك الواحد).
        if (second is not null && second.Quality.Any(q => !q.Acceptable))
            for (var i = 0; i < checks.Count; i++)
                if (checks[i].Confident) checks[i] = checks[i] with { Confident = false, Reason = "جودة المسح دون الحد المقبول" };
        return checks;
    }
}
