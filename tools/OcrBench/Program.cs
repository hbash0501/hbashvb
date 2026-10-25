using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using AIDocumentArchiveVB;
using Basira.Platform.Processing;

// لكل صورة: نقرأ النص (بالطريقة المختارة) ثم نستخرج الحقول بمنطق المنصة نفسه، ونقارنها بالقيم الصحيحة.
// الخطأ الصامت: قيمة خاطئة لم تُعلَّم «غير مؤكدة»؛ هو ما يقود إلى قرار خاطئ.
if (args.Length > 0 && args[0] == "crop") { var a = args.Skip(2).Take(4).Select(int.Parse).ToArray(); File.WriteAllBytes(args[6], Basira.Platform.Processing.Ocr.ImagePrep.Crop(args[1], new SkiaSharp.SKRectI(a[0], a[1], a[2], a[3]), 16, 2.0)); return; }
if (args.Length > 0 && args[0] == "digits") { foreach (var f in args.Skip(1)) { var g = Basira.Platform.Processing.Ocr.ImagePrep.Load(f)!; var r = Basira.Platform.Processing.Ocr.DigitReader.Read(g, new SkiaSharp.SKRectI(0, 0, g.Width, g.Height)); Console.WriteLine($"{Path.GetFileName(f)}: " + string.Join(" | ", r?.Tokens.Select(t => $"«{t.Text}» conf={t.Confidence:0.00} indic={t.Indic} complete={t.Complete} hijri={t.HijriSuffix}") ?? Array.Empty<string>())); } return; }
if (args.Length > 0 && args[0] == "words")
{
    var tess = TextExtractor.Find("", "", "ara+eng").Tesseract!;
    var td = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "tessdata"));
    var work = Directory.CreateTempSubdirectory("ocrwords-").FullName;
    Basira.Platform.Processing.Ocr.NumberRepair.Trace = m => Console.WriteLine("  trace " + m);
    var doc = await Basira.Platform.Processing.Ocr.OcrPipeline.ReadImagesAsync(new[] { args[1] }, new(tess, "ara+eng", td, FieldPass: args.Contains("--fields")), work, null, CancellationToken.None);
    foreach (var line in doc.Pages[0].Lines().Take(args.Length > 2 && int.TryParse(args[2], out var n) ? n : 12))
        Console.WriteLine(string.Join("  ", line.Select(w => $"{w.Text}[{w.Confidence:0}{(w.DigitVerified ? "✓" : "")}{(w.Corrected ? "*" : "")}{(args.Contains("--boxes") ? "@" + w.Box.Left + "," + w.Box.Top + "-" + w.Box.Right + "," + w.Box.Bottom : "")}]")));
    foreach (var c in doc.Checks) Console.WriteLine($"check {c.Field}: {c.Value} confident={c.Confident} cands=[{string.Join(", ", c.Candidates)}] {c.Reason} box={c.Box}");
    return;
}
var dir = args.Length > 0 ? args[0] : "corpus";
var mode = args.Length > 1 ? args[1] : "baseline";
var limit = args.Length > 2 ? int.Parse(args[2]) : int.MaxValue;
var truth = JsonSerializer.Deserialize<List<Truth>>(File.ReadAllText(Path.Combine(dir, "truth.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Take(limit).ToList();
if (mode == "quality") { Diagnose.Quality(dir, truth.Select(t => t.File), args.Length > 3 ? args[3] : null); return; }
var tesseract = TextExtractor.Find("", "", "ara+eng").Tesseract ?? throw new InvalidOperationException("Tesseract غير مثبت");
var results = new ConcurrentBag<Row>();
var sw = System.Diagnostics.Stopwatch.StartNew();
await Parallel.ForEachAsync(truth, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (t, ct) =>
{
    var path = Path.Combine(dir, t.File);
    var reading = await BenchReader.ReadAsync(mode, tesseract, path, ct);
    results.Add(Score(t, reading));
});
var rows = results.OrderBy(r => r.Level).ToList();
Console.WriteLine($"mode={mode} images={rows.Count} time={sw.Elapsed.TotalSeconds:0}s ({sw.Elapsed.TotalSeconds * 4 / Math.Max(1, rows.Count):0.0}s/image/core)");
Console.WriteLine("level     | field    | correct | wrong | missing | silent-wrong | flagged");
foreach (var level in new[] { "clean", "mild", "moderate", "severe", "ALL" })
{
    var set = level == "ALL" ? rows : rows.Where(r => r.Level == level).ToList();
    foreach (var field in new[] { "number", "date", "subject", "refs", "deadline" })
    {
        var cells = set.SelectMany(r => r.Fields.Where(f => f.Field == field)).ToList();
        if (cells.Count == 0) continue;
        int Count(string o) => cells.Count(c => c.Outcome == o);
        var silent = cells.Count(c => c.Outcome == "wrong" && !c.Flagged);
        var flagged = cells.Count(c => c.Flagged);
        Console.WriteLine($"{level,-9} | {field,-8} | {Pct(Count("correct"), cells.Count),7} | {Pct(Count("wrong"), cells.Count),5} | {Pct(Count("missing"), cells.Count),7} | {Pct(silent, cells.Count),12} | {Pct(flagged, cells.Count),7}");
    }
}
if (mode.Contains("prep") || mode is "pipeline" or "digits")
{
    Console.WriteLine("quality gate (rejected for rescan) by level:");
    foreach (var level in new[] { "clean", "mild", "moderate", "severe" })
    {
        var set = rows.Where(r => r.Level == level).ToList();
        Console.WriteLine($"  {level,-9} {set.Count(r => r.Rejected)}/{set.Count}  {string.Join(" | ", set.Where(r => r.Rejected).SelectMany(r => r.Issues).GroupBy(x => x.Split('(')[0].Trim()).Select(g => g.Key + " ×" + g.Count()))}");
    }
}
if (args.Contains("--details"))
    foreach (var r in rows.Where(r => r.Fields.Any(f => f.Outcome != "correct" || f.Flagged)).OrderBy(r => r.File))
        Console.WriteLine(r.File + ": " + string.Join(" ; ", r.Fields.Where(f => f.Outcome != "correct" || f.Flagged).Select(f => $"{f.Field} {f.Outcome}{(f.Flagged ? "(flagged)" : "")} expected={f.Expected} got={f.Got}")) + (r.Notes == "" ? "" : "  || " + r.Notes));

static string Pct(int n, int d) => d == 0 ? "-" : (100.0 * n / d).ToString("0.0", CultureInfo.InvariantCulture) + "%";

static Row Score(Truth t, BenchReading r)
{
    var row = new Row(t.File, t.Level, new List<Cell>(), r.Quality is { Acceptable: false }, r.Quality?.Issues ?? new List<string>(), r.Notes);
    var header = LetterFields.Parse(r.Text);
    string Iso(string canonical) { var c = ""; DateTime v = default; return canonical == "" ? "" : CorrespondenceReferenceParser.TryParseDate(canonical[1..].Replace('-', '/') + (canonical[0] == 'H' ? "هـ" : "م"), ref c, ref v) ? v.ToString("yyyy-MM-dd") : ""; }
    // الرقم والتاريخ: إن قدّم القارئ قيمة مستقلة للحقل (قراءة ثانية) تُستخدم مع علامة الشك.
    var gotNumber = r.Number ?? CorrespondenceReferenceParser.NumberKey(header.Number);
    row.Fields.Add(Cell("number", t.Number, gotNumber, r.NumberFlagged));
    var gotDate = r.DateIso ?? header.DateIso;
    row.Fields.Add(Cell("date", Iso(t.Date), gotDate, r.DateFlagged));
    row.Fields.Add(Cell("subject", Norm(t.Subject), Norm(header.Subject), r.SubjectFlagged));
    var refs = CorrespondenceReferenceParser.Parse(r.Text);
    foreach (var e in t.Refs)
    {
        var hit = refs.FirstOrDefault(x => CorrespondenceReferenceParser.NumbersMatch(x.Number, e.Number) || x.Number == e.Number);
        var expected = e.Number + "@" + Iso(e.Date);
        var got = hit is null ? "" : hit.Number + "@" + (hit.Date?.ToString("yyyy-MM-dd") ?? "");
        // إشارة لم تُلتقط برقمها: هل التُقطت إشارة برقم مشوّه مكانها؟ (خطأ لا فقدان)
        if (hit is null && refs.Count > t.Refs.Count - 0 && refs.Any(x => !t.Refs.Any(tr => CorrespondenceReferenceParser.NumbersMatch(x.Number, tr.Number))))
            got = refs.First(x => !t.Refs.Any(tr => CorrespondenceReferenceParser.NumbersMatch(x.Number, tr.Number))).Number + "@?";
        row.Fields.Add(Cell("refs", expected, got, r.FlaggedRefs.Contains(hit?.Number ?? got.Split('@')[0])));
    }
    if (t.DeadlineDays > 0)
    {
        var d = DeadlineExtractor.Extract(r.Text, null).FirstOrDefault(h => h.IsRelative);
        row.Fields.Add(Cell("deadline", t.DeadlineDays.ToString(), d?.Days.ToString() ?? "", r.DeadlineFlagged));
    }
    return row;
}

static Cell Cell(string field, string expected, string got, bool flagged) =>
    new(field, expected, got, got == "" ? "missing" : got == expected ? "correct" : "wrong", flagged);

static string Norm(string s) => System.Text.RegularExpressions.Regex.Replace((s ?? "").Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ة', 'ه').Replace('ى', 'ي'), @"[\sً-ٟـ]+", " ").Trim();

record Truth(string File, string Level, string Number, string Date, string Subject, List<TruthRef> Refs, int DeadlineDays);
record TruthRef(string Number, string Date);
record Cell(string Field, string Expected, string Got, string Outcome, bool Flagged);
record Row(string File, string Level, List<Cell> Fields, bool Rejected, List<string> Issues, string Notes = "");
