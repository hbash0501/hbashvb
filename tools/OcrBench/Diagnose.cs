using Basira.Platform.Processing.Ocr;

/// <summary>مقاييس الجودة لكل صورة، مع حفظ الصورة المحسّنة للمعاينة: dotnet run -- &lt;dir&gt; quality [مجلد الإخراج]</summary>
public static class Diagnose
{
    public static void Quality(string dir, IEnumerable<string> files, string? outDir)
    {
        if (outDir is not null) Directory.CreateDirectory(outDir);
        Console.WriteLine("file                         | dpi | edge | contrast | noise | skew | ink    | issues");
        foreach (var f in files)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var g = ImagePrep.Load(Path.Combine(dir, f));
            var tLoad = sw.ElapsedMilliseconds;
            if (g is null) { Console.WriteLine($"{f}: تعذر الفتح"); continue; }
            var a = ImagePrep.Analyze(g);
            var q = a.Quality;
            var tAnalyze = sw.ElapsedMilliseconds - tLoad;
            Console.WriteLine($"{f,-28} | {q.EstimatedDpi,3} | {q.EdgeWidth,4:0.0} | {q.Contrast,8} | {q.Noise,5:0.0} | {q.SkewDegrees,4:0.0} | {q.InkRatio,6:0.0000} | {string.Join("; ", q.Issues.Select(i => i.Split(' ')[0] + " " + i.Split(' ')[1]))}");
            if (outDir is not null) ImagePrep.Prepare(a, Path.Combine(outDir, Path.ChangeExtension(f, ".png")));
            if (Environment.GetEnvironmentVariable("BENCH_TIMING") == "1") Console.WriteLine($"   load={tLoad}ms analyze={tAnalyze}ms prepare={sw.ElapsedMilliseconds - tLoad - tAnalyze}ms");
        }
    }
}
