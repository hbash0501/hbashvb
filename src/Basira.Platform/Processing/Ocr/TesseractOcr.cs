using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Basira.Platform.Processing.Ocr;

public sealed class OcrWord
{
    public string Text { get; set; } = "";
    public float Confidence { get; set; }
    public SKRectI Box { get; set; }
    public int Block { get; init; }
    public int Paragraph { get; init; }
    public int Line { get; init; }
    public int Page { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public bool Corrected { get; set; }
    /// <summary>قرأه قارئ الأرقام المخصص (بدل Tesseract أو مؤكداً له).</summary>
    public bool DigitVerified { get; set; }
}

public sealed class OcrPage
{
    public List<OcrWord> Words { get; } = new();
    public string Text { get; set; } = "";

    public IEnumerable<IGrouping<(int, int, int), OcrWord>> Lines() => Words.GroupBy(w => (w.Block, w.Paragraph, w.Line));

    public static SKRectI Union(IEnumerable<OcrWord> words)
    {
        var list = words.ToList();
        return new SKRectI(list.Min(w => w.Box.Left), list.Min(w => w.Box.Top), list.Max(w => w.Box.Right), list.Max(w => w.Box.Bottom));
    }
}

/// <summary>استدعاء Tesseract بمخرجات TSV: نص بترتيب القراءة مع ثقة كل كلمة وموضعها في الصورة.</summary>
public static class TesseractOcr
{
    public static string? BundledTessdata()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "tessdata");
        return File.Exists(Path.Combine(dir, "ara.traineddata")) ? dir : null;
    }

    public static async Task<OcrPage> ReadAsync(string tesseract, string image, string languages, int psm, string? tessdata, CancellationToken ct, params string[] extra)
    {
        var args = new List<string> { image, "stdout", "-l", languages, "--psm", psm.ToString(CultureInfo.InvariantCulture), "--dpi", "300" };
        if (tessdata is not null) { args.Insert(0, tessdata); args.Insert(0, "--tessdata-dir"); }
        foreach (var e in extra) { args.Add("-c"); args.Add(e); }
        args.Add("-c"); args.Add("tessedit_create_tsv=1"); args.Add("-c"); args.Add("tessedit_create_txt=0"); // لا يعتمد على ملفات إعداد tessdata/configs
        var r = await ProcessRunner.RunAsync(tesseract, args, TimeSpan.FromMinutes(3), ct);
        if (r.Exit != 0) throw new InvalidOperationException("فشل Tesseract: " + r.Err.Trim());
        return Parse(r.Out);
    }

    public static OcrPage Parse(string tsv)
    {
        var page = new OcrPage();
        foreach (var line in tsv.Split('\n').Skip(1))
        {
            var c = line.TrimEnd('\r').Split('\t');
            if (c.Length < 12 || c[0] != "5") continue;
            var text = CleanWord(c[11]);
            if (text.Length == 0) continue;
            int I(int i) => int.Parse(c[i], CultureInfo.InvariantCulture);
            page.Words.Add(new OcrWord
            {
                Text = text, Confidence = float.Parse(c[10], CultureInfo.InvariantCulture),
                Box = new SKRectI(I(6), I(7), I(6) + I(8), I(7) + I(9)), Block = I(2), Paragraph = I(3), Line = I(4),
            });
        }
        return VisualOrder(page);
    }

    private static bool IsArabic(string t) => t.Any(c => c is >= '\u0600' and <= '\u06FF' && !(c is >= '٠' and <= '٩') || c is >= '\uFB50' and <= '\uFEFF');

    /// <summary>
    /// ترتيب الكلمات داخل السطر العربي بموضعها في الصورة (من اليمين)، مع إبقاء الأرقام والكلمات اللاتينية المتتالية بترتيبها من اليسار.
    /// ترتيب Tesseract للسطر المختلط غير موثوق: قد يضع رقم «الرقم: 48144» في أول السطر.
    /// </summary>
    private static OcrPage VisualOrder(OcrPage page)
    {
        // تُجمع بترتيب اليسار الأرقام والعلامات فقط. الكلمة اللاتينية تبقى في موضعها: كثيراً ما تكون كلمة عربية أسيء قراءتها
        // («يوماً» ← Logy)، وضمها إلى الرقم المجاور يقلب «خلال (15) يوماً». (عيب معروف: العبارة الإنجليزية من كلمتين تنعكس.)
        static bool Ltr(OcrWord w) => !IsArabic(w.Text) && (w.Text.Any(char.IsDigit) || !w.Text.Any(char.IsLetter));
        var ordered = new List<OcrWord>(page.Words.Count);
        foreach (var line in page.Words.GroupBy(w => (w.Block, w.Paragraph, w.Line)))
        {
            var ws = line.ToList();
            if (!ws.Any(w => IsArabic(w.Text))) { ordered.AddRange(ws); continue; }
            var visual = ws.OrderByDescending(w => w.Box.Left + w.Box.Right).ToList();
            for (var i = 0; i < visual.Count;)
            {
                if (!Ltr(visual[i])) { ordered.Add(visual[i]); i++; continue; }
                var j = i;
                while (j < visual.Count && Ltr(visual[j])) j++;
                for (var k = j - 1; k >= i; k--) ordered.Add(visual[k]);
                i = j;
            }
        }
        page.Words.Clear();
        page.Words.AddRange(ordered);
        return page;
    }

    private static string CleanWord(string w)
    {
        w = w.Normalize(NormalizationForm.FormKC);
        w = Regex.Replace(w, "[‎‏‪-‮⁦-⁩؜﻿]", "");
        w = Regex.Replace(w, @"^\)([0-9٠-٩][0-9٠-٩/\-]*)\($", "($1)");
        return w.Trim();
    }

    /// <summary>يبني النص من الكلمات بترتيب القراءة، ويسجل موضع كل كلمة فيه (لربط الإشارات بصورتها).</summary>
    public static string Assemble(IEnumerable<OcrPage> pages)
    {
        var sb = new StringBuilder();
        var pageNo = 0;
        foreach (var page in pages)
        {
            if (pageNo > 0) sb.Append("\n\n");
            (int, int)? lastPar = null;
            foreach (var line in page.Lines())
            {
                var par = (line.Key.Item1, line.Key.Item2);
                if (sb.Length > 0 && sb[^1] != '\n') sb.Append(lastPar == par ? "\n" : "\n\n");
                var first = true;
                foreach (var w in line)
                {
                    if (!first) sb.Append(' ');
                    w.Page = pageNo; w.Start = sb.Length; sb.Append(w.Text); w.End = sb.Length;
                    first = false;
                }
                lastPar = par;
            }
            page.Text = sb.ToString();
            pageNo++;
        }
        return sb.ToString();
    }
}
