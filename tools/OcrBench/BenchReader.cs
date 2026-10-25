using Basira.Platform.Processing;

public sealed record BenchReading(string Text, string? Number = null, bool NumberFlagged = false, string? DateIso = null, bool DateFlagged = false, HashSet<string>? Flagged = null, bool DeadlineFlagged = false, Basira.Platform.Processing.Ocr.ScanQuality? Quality = null, string Notes = "", bool SubjectFlagged = false)
{
    public HashSet<string> FlaggedRefs => Flagged ?? new();
}

public static class BenchReader
{
    public static async Task<BenchReading> ReadAsync(string mode, string tesseract, string path, CancellationToken ct)
    {
        if (mode == "baseline")
        {
            // ما تفعله المنصة حالياً: النموذج المثبت مع Tesseract، دون معالجة للصورة، تقسيم تلقائي للصفحة.
            var r = await ProcessRunner.RunAsync(tesseract, new[] { path, "stdout", "-l", "ara+eng", "--psm", "3" }, TimeSpan.FromMinutes(3), ct);
            return new BenchReading(TextExtractor.Clean(r.Out));
        }
        var tessdata = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "tessdata"));
        var options = mode switch
        {
            "best" => new Basira.Platform.Processing.Ocr.OcrOptions(tesseract, "ara+eng", tessdata, Preprocess: false, FieldPass: false, DigitPass: false),
            "prep" => new Basira.Platform.Processing.Ocr.OcrOptions(tesseract, "ara+eng", tessdata, Preprocess: true, FieldPass: false, DigitPass: false),
            "digits" => new Basira.Platform.Processing.Ocr.OcrOptions(tesseract, "ara+eng", tessdata, Preprocess: true, FieldPass: false),
            "sauvola" => new Basira.Platform.Processing.Ocr.OcrOptions(tesseract, "ara+eng", tessdata, Preprocess: false, FieldPass: false, Params: new[] { "thresholding_method=2" }, DigitPass: false),
            "prep-sauvola" => new Basira.Platform.Processing.Ocr.OcrOptions(tesseract, "ara+eng", tessdata, Preprocess: true, FieldPass: false, Params: new[] { "thresholding_method=2" }, DigitPass: false),
            "pipeline" => new Basira.Platform.Processing.Ocr.OcrOptions(tesseract, "ara+eng", tessdata, Preprocess: true, FieldPass: true),
            _ => throw new ArgumentException("طريقة غير معروفة: " + mode),
        };
        var work = Directory.CreateTempSubdirectory("ocrbench-").FullName;
        try
        {
            var doc = await Basira.Platform.Processing.Ocr.OcrPipeline.ReadImagesAsync(new[] { path }, options, work, null, ct);
            if (mode != "pipeline") return new BenchReading(doc.Text, Quality: doc.Quality.FirstOrDefault());
            var number = doc.Checks.FirstOrDefault(c => c.Field == "number");
            var date = doc.Checks.FirstOrDefault(c => c.Field == "date");
            var flagged = doc.Checks.Where(c => c.Field == "reference" && !c.Confident).Select(c => c.Value.Split('@')[0]).ToHashSet();
            var deadlineFlag = doc.Checks.Any(c => c.Field == "deadline" && !c.Confident);
            var subjectFlag = doc.Checks.Any(c => c.Field == "subject" && !c.Confident) || !doc.Checks.Any(c => c.Field == "subject");
            var notes = string.Join(" ; ", doc.Checks.Where(c => !c.Confident).Select(c => $"{c.Field}:{c.Reason} [{string.Join(",", c.Candidates)}]"));
            return new BenchReading(doc.Text, number?.Value ?? "", number is not null && !number.Confident, date?.Value ?? "", date is not null && !date.Confident, flagged, deadlineFlag, doc.Quality.FirstOrDefault(), notes, subjectFlag);
        }
        finally { Directory.Delete(work, true); }
    }
}
