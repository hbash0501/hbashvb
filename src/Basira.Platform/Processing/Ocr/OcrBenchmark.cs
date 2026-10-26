using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIDocumentArchiveVB;
using Basira.Platform.Data;

namespace Basira.Platform.Processing.Ocr;

/// <summary>نتيجة محرك على العينات: نسبة خطأ الحروف، ومآل الحقول الحرجة (اعتماد آلي صحيح، خطأ صامت، مراجعة، مفقود)، والزمن.</summary>
public sealed record EngineScore(string Mode, string Engine, double Cer, Dictionary<string, double> CerByLevel, int Fields, int AutoCorrect, int SilentWrong, int Review, int Missing, double SecondsPerPage, string Note);

public sealed record OcrBenchmarkResult(string At, int Pages, List<EngineScore> Engines);

/// <summary>
/// قياس محركات القراءة على جهاز المستخدم بعينات مضمّنة نصها الصحيح معروف: Tesseract وحده، ثم الوضع التلقائي (المرئي مع تحقق Tesseract)
/// إن ضُبط. الخطاب الحقيقي يختلف عن هذه العينات، لكنها تكشف الفرق بين المحركين على هذا الجهاز وبإعداداته.
/// </summary>
public sealed class OcrBenchmark
{
    public const string ResultKey = "ocr_benchmark";
    private readonly SettingsStore _settings;
    private readonly ILogger<OcrBenchmark> _log;
    private readonly object _lock = new();
    public bool Running { get; private set; }
    public string Progress { get; private set; } = "";
    public string Error { get; private set; } = "";

    public OcrBenchmark(SettingsStore settings, ILogger<OcrBenchmark> log) { _settings = settings; _log = log; }

    public sealed record Sample(string File, string Level, string Number, string Date, string Subject, string Text);

    public static List<Sample> Samples()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ocr-samples/samples.json")!;
        using var j = JsonDocument.Parse(s);
        return j.RootElement.EnumerateArray().Select(e => new Sample(e.GetProperty("file").GetString()!, e.GetProperty("level").GetString()!,
            e.GetProperty("number").GetString()!, HijriIso(e.GetProperty("date").GetString()!), e.GetProperty("subject").GetString()!, e.GetProperty("text").GetString()!)).ToList();
    }

    /// <summary>«H1447-10-17» ← تاريخ ميلادي ISO (أم القرى).</summary>
    private static string HijriIso(string h)
    {
        var m = Regex.Match(h, @"^H(\d{4})-(\d{2})-(\d{2})$");
        if (!m.Success) return "";
        var d = new UmAlQuraCalendar().ToDateTime(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), 0, 0, 0, 0);
        return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public OcrBenchmarkResult? Last => _settings.GetRaw(ResultKey) is { Length: > 0 } j ? JsonSerializer.Deserialize<OcrBenchmarkResult>(j) : null;

    public bool Start(string actor)
    {
        lock (_lock) { if (Running) return false; Running = true; Error = ""; Progress = "بدء القياس"; }
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await RunAsync(CancellationToken.None);
                _settings.SetRaw(ResultKey, JsonSerializer.Serialize(result), actor, "قياس محركات القراءة");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Error = ex.Message;
                _log.LogWarning(ex, "OCR benchmark failed");
            }
            finally { Running = false; Progress = ""; }
        });
        return true;
    }

    public async Task<OcrBenchmarkResult> RunAsync(CancellationToken ct)
    {
        var samples = Samples();
        var dir = Directory.CreateTempSubdirectory("basira-ocrbench-").FullName;
        try
        {
            var files = new List<string>();
            foreach (var s in samples)
            {
                var path = Path.Combine(dir, s.File);
                await using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("ocr-samples/" + s.File)!)
                await using (var dst = File.Create(path)) await src.CopyToAsync(dst, ct);
                files.Add(path);
            }
            var auto = await OcrEngines.ToolsAsync(_settings, ct);
            var engines = new List<EngineScore>();
            if (auto.Tesseract is not null) engines.Add(await ScoreAsync("tesseract", auto with { Vision = null, VisionNote = "" }, samples, files, dir, ct));
            if (auto.Vision is not null) engines.Add(await ScoreAsync("auto", auto, samples, files, dir, ct));
            if (engines.Count == 0) throw new InvalidOperationException("لا يوجد محرك قراءة متاح للقياس" + (auto.VisionNote == "" ? "." : ": " + auto.VisionNote));
            var note = auto.Vision is null && auto.VisionNote != "" ? auto.VisionNote : "";
            if (note != "") engines[0] = engines[0] with { Note = note };
            return new OcrBenchmarkResult(Infrastructure.Clock.Now(), samples.Count, engines);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    private async Task<EngineScore> ScoreAsync(string mode, TextExtractor.Tools tools, List<Sample> samples, List<string> files, string dir, CancellationToken ct)
    {
        double cerSum = 0; var byLevel = new Dictionary<string, List<double>>();
        int fields = 0, autoCorrect = 0, silent = 0, review = 0, missing = 0;
        var sw = Stopwatch.StartNew();
        var engine = "";
        var notes = new List<string>();
        for (var i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            Progress = $"{(mode == "tesseract" ? "Tesseract" : "المحرك المرئي + Tesseract")}: صفحة {i + 1} من {samples.Count}";
            var work = Directory.CreateDirectory(Path.Combine(dir, mode + i)).FullName;
            var e = await TextExtractor.ReadImagesAsync(tools, new[] { files[i] }, work, (_, _) => { }, ct);
            engine = e.Engine;
            if (e.Warning != "" && !e.Warning.StartsWith("جودة المسح", StringComparison.Ordinal) && !notes.Contains(e.Warning)) notes.Add(e.Warning);
            var cer = Cer(e.Text, s.Text);
            cerSum += cer;
            (byLevel.TryGetValue(s.Level, out var l) ? l : byLevel[s.Level] = new()).Add(cer);
            foreach (var (field, truth) in new[] { ("number", CorrespondenceReferenceParser.NumberKey(s.Number)), ("date", s.Date), ("subject", s.Subject) })
            {
                fields++;
                var k = e.Checks.Select(x => x.Check).FirstOrDefault(x => x.Field == field && x.Value != "");
                if (k is null) { missing++; continue; }
                var right = field == "subject" ? CrossCheck.Similarity(k.Value, truth) >= 0.9 : field == "number" ? CorrespondenceReferenceParser.NumbersMatch(k.Value, truth) : k.Value == truth;
                if (!k.Confident) review++;
                else if (right) autoCorrect++;
                else silent++;
            }
        }
        return new EngineScore(mode, engine, Math.Round(100 * cerSum / samples.Count, 1), byLevel.ToDictionary(x => x.Key, x => Math.Round(100 * x.Value.Average(), 1)),
            fields, autoCorrect, silent, review, missing, Math.Round(sw.Elapsed.TotalSeconds / samples.Count, 1), string.Join(" ", notes.Take(2)));
    }

    /// <summary>نسبة خطأ الحروف: مسافة التحرير بين النصين بعد التوحيد (الهمزات والتشكيل والأرقام والمسافات) مقسومة على طول النص الصحيح.</summary>
    public static double Cer(string read, string truth)
    {
        static string N(string s) => Regex.Replace(BasiraEngine.Normalize(s ?? ""), @"\s+", " ").Trim();
        var a = N(read); var b = N(truth);
        if (b.Length == 0) return a.Length == 0 ? 0 : 1;
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
        return Math.Min(1, (double)d[b.Length] / b.Length);
    }
}
