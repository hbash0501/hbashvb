using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Basira.Platform.Processing.Ocr;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Basira.Platform.Processing;

public sealed record Extraction(string Text, int Pages, int Quality, string Engine, string Warning)
{
    /// <summary>فحوص الحقول الحرجة (للمستندات المقروءة بـ OCR) مع مقتطع صورة كل حقل ونصه كما ورد.</summary>
    public List<ExtractedCheck> Checks { get; init; } = new();
    /// <summary>عيوب المسح التي رصدتها بوابة الجودة، لكل الصفحات.</summary>
    public List<string> ScanIssues { get; init; } = new();
    public bool UsedOcr { get; init; }
}

public sealed record ExtractedCheck(FieldCheck Check, byte[]? Crop, string SpanText);

/// <summary>استخراج النص: ملفات نصية وWord وPDF (طبقة النص أو OCR للصفحات المصورة) والصور عبر Tesseract.</summary>
public sealed class TextExtractor
{
    public static readonly string[] ImageTypes = { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".gif", ".webp" };
    public static readonly string[] TextTypes = { ".txt", ".csv", ".md", ".text" };

    /// <summary>أدوات القراءة. Vision: محرك القراءة المرئي إن ضُبط وكان خادمه متاحاً؛ VisionNote: سبب عدم استخدامه.</summary>
    public sealed record Tools(string? Tesseract, string? PdfToPpm, string Languages, VisionOptions? Vision = null, string VisionNote = "");

    public static Tools Find(string tesseractPath, string pdftoppmPath, string languages) => new(
        ProcessRunner.Locate(tesseractPath, "tesseract", @"%ProgramFiles%\Tesseract-OCR\tesseract.exe", @"%ProgramFiles(x86)%\Tesseract-OCR\tesseract.exe", @"%LocalAppData%\Programs\Tesseract-OCR\tesseract.exe"),
        ProcessRunner.Locate(pdftoppmPath, "pdftoppm", @"%ProgramFiles%\poppler\Library\bin\pdftoppm.exe", @"%ProgramFiles%\poppler\bin\pdftoppm.exe"),
        string.IsNullOrWhiteSpace(languages) ? "ara+eng" : languages);

    public async Task<Extraction> ExtractAsync(string path, string fileType, Tools tools, Action<int, string> progress, CancellationToken ct)
    {
        var ext = fileType.ToLowerInvariant();
        if (TextTypes.Contains(ext)) return Finish(ReadText(path), 1, "نص", "");
        if (ext == ".docx") return Finish(ReadDocx(path), 1, "Word", "");
        if (ext == ".pdf") return await ReadPdfAsync(path, tools, progress, ct);
        if (ImageTypes.Contains(ext))
        {
            if (tools.Tesseract is null && tools.Vision is null)
                return new Extraction("", 1, 0, "", "لم يُعثر على محرك قراءة؛ ثبّت Tesseract أو اضبط محرك القراءة المرئي من الإعدادات لاستخراج النص من الصور." + Note(tools));
            var dir = Directory.CreateTempSubdirectory("basira-ocr-").FullName;
            try { return await PagesAsync(tools, new[] { path }, dir, progress, "", ct); }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }
        return new Extraction("", 0, 0, "", "نوع الملف غير مدعوم لاستخراج النص؛ حُفظ الأصل ويمكن إدخال النص يدوياً.");
    }

    private static Extraction Finish(string text, int pages, string engine, string warning)
    {
        text = Clean(text);
        return new Extraction(text, pages, Quality(text), engine, warning);
    }

    public static string Clean(string text)
    {
        text = text.Normalize(NormalizationForm.FormKC).Replace("\r\n", "\n").Replace('\r', '\n').Replace('\f', '\n');
        // علامات الاتجاه الخفية التي يضيفها OCR، والأقواس المعكوسة حول الأرقام في النص العربي («)15(» ← «(15)»).
        text = Regex.Replace(text, "[\u200E\u200F\u202A-\u202E\u2066-\u2069\u061C\uFEFF]", "");
        text = Regex.Replace(text, @"\)\s*([0-9\u0660-\u0669][0-9\u0660-\u0669/\-]*)\s*\(", "($1)");
        // رقم ملتصق بكلمة عربية بعده (تجاور كتلتين في ترويسة PDF): «7781الإدارة» ← «7781 الإدارة»؛ لا يمس «هـ» بعد التاريخ.
        text = Regex.Replace(text, @"([0-9])(?=[\u0621-\u064A]{3})", "$1 ");
        text = Regex.Replace(text, "[ \t ]+", " ");
        text = Regex.Replace(text, " *\n *", "\n");
        text = Regex.Replace(text, "\n{3,}", "\n\n");
        return text.Trim();
    }

    /// <summary>تقدير تقريبي لجودة النص: نسبة الكلمات المكوّنة من حروف سليمة.</summary>
    public static int Quality(string text)
    {
        var words = Regex.Matches(text, @"\S+").Select(m => m.Value).ToList();
        if (words.Count == 0) return 0;
        var good = words.Count(w => Regex.IsMatch(w, @"^[\p{L}\p{Nd}][\p{L}\p{Nd}\p{M}\-/.,،:؛()]*$"));
        return (int)Math.Round(100.0 * good / words.Count);
    }

    private static string ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1256).GetString(bytes); // ملفات عربية قديمة بترميز Windows-1256.
        }
    }

    private static string ReadDocx(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("ملف Word تالف أو غير مدعوم.");
        using var s = entry.Open();
        var doc = XDocument.Load(s);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var sb = new StringBuilder();
        foreach (var p in doc.Descendants(w + "p"))
        {
            foreach (var node in p.Descendants())
            {
                if (node.Name == w + "t") sb.Append(node.Value);
                else if (node.Name == w + "tab") sb.Append(' ');
                else if (node.Name == w + "br") sb.Append('\n');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private async Task<Extraction> ReadPdfAsync(string path, Tools tools, Action<int, string> progress, CancellationToken ct)
    {
        var sb = new StringBuilder();
        int pages;
        using (var pdf = PdfDocument.Open(path))
        {
            pages = pdf.NumberOfPages;
            foreach (var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                sb.AppendLine(ContentOrderTextExtractor.GetText(page));
                progress(10 + 40 * page.Number / Math.Max(1, pages), $"قراءة طبقة النص: صفحة {page.Number} من {pages}");
            }
        }
        var text = Clean(FixRtl(sb.ToString()));
        var letters = text.Count(char.IsLetter);
        if (letters >= 25 * pages) return new Extraction(text, pages, Quality(text), "طبقة نص PDF", "");

        // PDF مصوّر: تحويل الصفحات إلى صور ثم OCR.
        if (tools.Tesseract is null && tools.Vision is null || tools.PdfToPpm is null)
            return new Extraction(text, pages, Quality(text), "طبقة نص PDF", "الملف مصوّر ويحتاج قراءة ضوئية؛ حدّد مسار pdftoppm ومحرك قراءة (Tesseract أو المحرك المرئي) من الإعدادات." + Note(tools));
        var dir = Directory.CreateTempSubdirectory("basira-pdf-").FullName;
        try
        {
            var images = new List<string>();
            for (var i = 1; i <= pages; i++)
            {
                progress(10 + 15 * (i - 1) / pages, $"تحويل صفحة {i} من {pages} إلى صورة");
                var prefix = Path.Combine(dir, "p" + i);
                var r = await ProcessRunner.RunAsync(tools.PdfToPpm, new[] { "-r", "300", "-gray", "-png", "-f", i.ToString(CultureInfo.InvariantCulture), "-l", i.ToString(CultureInfo.InvariantCulture), "-singlefile", path, prefix }, TimeSpan.FromMinutes(2), ct);
                if (r.Exit != 0 || !File.Exists(prefix + ".png")) throw new InvalidOperationException("تعذر تحويل صفحة PDF إلى صورة: " + r.Err.Trim());
                images.Add(prefix + ".png");
            }
            return await PagesAsync(tools, images, dir, progress, " (PDF مصوّر)", ct);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    private static string Note(Tools tools) => tools.VisionNote == "" ? "" : " (" + tools.VisionNote + ")";

    /// <summary>
    /// قراءة الصفحات المصورة: بالمحرك المرئي إن ضُبط (ومعه Tesseract قراءةً ثانية تتحقق من الحقول الحرجة)، وإلا خط Tesseract الكامل:
    /// بوابة الجودة، وتحسين الصورة، والنموذج الدقيق، وقارئ الأرقام، وفحص الحقول الحرجة. المقتطعات تُقص هنا قبل حذف الصور المؤقتة.
    /// </summary>
    /// <summary>متوسط ثقة Tesseract في كلماته الذي يؤهل قراءته للحكم على قراءة المحرك المرئي.</summary>
    public const int TrustedTesseract = 75;

    /// <summary>قراءة صور صفحات بخط القراءة نفسه المستخدم في الإدراج (لقياس المحركات).</summary>
    public static Task<Extraction> ReadImagesAsync(Tools tools, IReadOnlyList<string> images, string workDir, Action<int, string> progress, CancellationToken ct) =>
        PagesAsync(tools, images, workDir, progress, "", ct);

    private static async Task<Extraction> PagesAsync(Tools tools, IReadOnlyList<string> images, string workDir, Action<int, string> progress, string suffix, CancellationToken ct)
    {
        OcrDocument? tess = null;
        if (tools.Tesseract is not null)
        {
            var options = new OcrOptions(tools.Tesseract, tools.Languages, Tessdata(tools.Languages));
            Action<int, string> p = tools.Vision is null ? progress : (v, s) => progress(10 + v * 35 / 100, s + " (Tesseract)");
            tess = await OcrPipeline.ReadImagesAsync(images, options, workDir, p, ct);
        }
        if (tools.Vision is null) return Package(tess!.Text, tess.Checks, tess.PreparedImages, tess.Quality, images.Count, "Tesseract" + suffix, Note(tools));

        var vision = tools.Vision;
        var pages = new List<string>();
        try
        {
            for (var i = 0; i < images.Count; i++)
            {
                progress(45 + 40 * i / images.Count, images.Count > 1 ? $"القراءة بالنموذج المرئي: صفحة {i + 1} من {images.Count}" : "القراءة بالنموذج المرئي");
                pages.Add(await VisionOcr.ReadAsync(images[i], vision, ct));
            }
        }
        catch (InvalidOperationException ex)
        {
            if (tess is null) return new Extraction("", images.Count, 0, "", "تعذرت القراءة بالمحرك المرئي: " + ex.Message) { UsedOcr = true };
            return Package(tess.Text, tess.Checks, tess.PreparedImages, tess.Quality, images.Count, "Tesseract" + suffix, "تعذرت القراءة بالمحرك المرئي فاعتُمدت قراءة Tesseract: " + ex.Message);
        }
        var primary = Clean(string.Join("\n\n", pages));
        // قراءة لا تشبه الصفحة (هلوسة) أو ناقصة جداً مقابل قراءة Tesseract واثقة: تُعتمد قراءة Tesseract. ويُحكَم بثقة Tesseract في كلماته
        // لا بشكل نصه، فقراءته الرديئة لمسح تالف قد تبدو نصاً عربياً سليماً، ولا يصح أن تُسقط بها قراءة مرئية صحيحة.
        if (tess is not null && tess.MeanConfidence >= TrustedTesseract && (primary.Length < tess.Text.Length * 0.3 || VisionOcr.Agreement(primary, tess.Text) < 0.35))
            return Package(tess.Text, tess.Checks, tess.PreparedImages, tess.Quality, images.Count, "Tesseract" + suffix,
                "قراءة النموذج المرئي لا تطابق الصفحة (تشابهها مع قراءة Tesseract ضعيف)، فاعتُمدت قراءة Tesseract.");
        var quality = tess?.Quality ?? images.Select(img => ImagePrep.Load(img) is { } g ? ImagePrep.Analyze(g).Quality : null).Where(q => q is not null).Select(q => q!).ToList();
        var checks = CrossCheck.Build(primary, tess);
        var prepared = tess?.PreparedImages ?? images.ToList();
        return Package(primary, checks, prepared, quality, images.Count, vision.Model + (tess is null ? "" : " + Tesseract") + suffix, "");
    }

    private static Extraction Package(string text, List<FieldCheck> fieldChecks, List<string> prepared, List<ScanQuality> quality, int pages, string engine, string note)
    {
        var checks = new List<ExtractedCheck>();
        foreach (var k in fieldChecks)
        {
            byte[]? crop = null;
            if (!k.Box.IsEmpty && k.Page < prepared.Count)
                try { crop = ImagePrep.Crop(prepared[k.Page], k.Box, Math.Max(8, k.Box.Height / 3)); }
                catch (Exception ex) when (ex is ArgumentException or InvalidDataException) { }
            var span = k.Offset >= 0 && k.Offset + k.Length <= text.Length ? text.Substring(k.Offset, k.Length) : "";
            checks.Add(new ExtractedCheck(k, crop, span));
        }
        var issues = quality.SelectMany((q, i) => q.Issues.Select(x => quality.Count > 1 ? $"صفحة {i + 1}: {x}" : x)).ToList();
        var warning = issues.Count == 0 ? note : "جودة المسح دون الحد المقبول: " + string.Join("؛ ", issues) + ". يُنصح بإعادة المسح." + (note == "" ? "" : " " + note);
        // نص خط القراءة نظيف أصلاً، ولا يمر بـ Clean حتى تبقى مواضع الفحوص فيه صحيحة.
        return new Extraction(text, pages, Quality(text), engine, warning) { Checks = checks, ScanIssues = issues, UsedOcr = true };
    }

    /// <summary>النماذج الدقيقة المرفقة إن غطت كل اللغات المطلوبة، وإلا نماذج Tesseract المثبتة.</summary>
    private static string? Tessdata(string languages)
    {
        var dir = TesseractOcr.BundledTessdata();
        return dir is not null && languages.Split('+').All(l => File.Exists(Path.Combine(dir, l + ".traineddata"))) ? dir : null;
    }

    private static readonly HashSet<string> CommonArabic = new(new[]
    {
        "في", "من", "على", "إلى", "الى", "عن", "رقم", "خطاب", "تاريخ", "وتاريخ", "بشأن", "الموضوع", "المحترم", "نفيدكم", "إشارة", "السلام", "عليكم",
        "مع", "التي", "الذي", "هذا", "هذه", "تم", "بأن", "أن", "إن", "ما", "لا", "كما", "وتقبلوا", "تحياتنا", "الله", "ورحمة", "وبركاته", "سعادة",
        "المدير", "العام", "الإدارة", "الرقم", "التاريخ", "المرفقات", "لكم", "لدى", "حول", "بخصوص", "نأمل", "نرفق", "وزارة", "إدارة",
    });

    /// <summary>
    /// بعض مولّدات PDF (ومنها Chromium) تخزن العربية بترتيب بصري معكوس. الانعكاس صفة للملف كله، فيُحسم القرار على مستوى المستند
    /// بعدّ الكلمات الشائعة في اتجاهيها، ثم يُعكس كل سطر عربي مع إعادة الأرقام والنصوص اللاتينية إلى ترتيبها.
    /// </summary>
    public static string FixRtl(string text)
    {
        var lines = text.Split('\n');
        int normal = 0, reversed = 0;
        foreach (var line in lines)
            foreach (var w in line.Normalize(NormalizationForm.FormKC).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = w.Trim('.', '،', ',', ':', '؛');
                if (CommonArabic.Contains(t)) normal++;
                if (CommonArabic.Contains(new string(t.Reverse().ToArray()))) reversed++;
            }
        if (reversed <= normal) return text;
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Any(ch => ch >= '\u0600' && ch <= '\u06FF' || ch >= '\uFB50' && ch <= '\uFEFF')) continue;
            // العكس على السطر الأصلي قبل توحيد الأشكال حتى لا ينقلب ترتيب حرفي الرباط «ﻹ».
            var flipped = new string(lines[i].Reverse().ToArray());
            lines[i] = Regex.Replace(flipped, @"[0-9A-Za-z][0-9A-Za-z/\-.:]*[0-9A-Za-z]|[0-9A-Za-z]", m => new string(m.Value.Reverse().ToArray()));
        }
        return string.Join('\n', lines);
    }
}
