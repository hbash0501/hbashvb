using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Basira.Platform.Processing;

public sealed record Extraction(string Text, int Pages, int Quality, string Engine, string Warning);

/// <summary>استخراج النص: ملفات نصية وWord وPDF (طبقة النص أو OCR للصفحات المصورة) والصور عبر Tesseract.</summary>
public sealed class TextExtractor
{
    public static readonly string[] ImageTypes = { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".gif", ".webp" };
    public static readonly string[] TextTypes = { ".txt", ".csv", ".md", ".text" };

    public sealed record Tools(string? Tesseract, string? PdfToPpm, string Languages);

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
            if (tools.Tesseract is null) return new Extraction("", 1, 0, "", "لم يُعثر على Tesseract؛ حدّد مساره من الإعدادات لاستخراج النص من الصور.");
            progress(30, "التعرف الضوئي على الحروف");
            return Finish(await OcrAsync(tools, path, ct), 1, "Tesseract", "");
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
        if (tools.Tesseract is null || tools.PdfToPpm is null)
            return new Extraction(text, pages, Quality(text), "طبقة نص PDF", "الملف مصوّر ويحتاج OCR؛ حدّد مساري Tesseract وpdftoppm من الإعدادات.");
        var dir = Path.Combine(Path.GetTempPath(), "basira-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var ocr = new StringBuilder();
            for (var i = 1; i <= pages; i++)
            {
                progress(10 + 85 * (i - 1) / pages, $"التعرف الضوئي: صفحة {i} من {pages}");
                var prefix = Path.Combine(dir, "p" + i);
                var r = await ProcessRunner.RunAsync(tools.PdfToPpm, new[] { "-r", "300", "-gray", "-png", "-f", i.ToString(CultureInfo.InvariantCulture), "-l", i.ToString(CultureInfo.InvariantCulture), "-singlefile", path, prefix }, TimeSpan.FromMinutes(2), ct);
                if (r.Exit != 0 || !File.Exists(prefix + ".png")) throw new InvalidOperationException("تعذر تحويل صفحة PDF إلى صورة: " + r.Err.Trim());
                ocr.AppendLine(await OcrAsync(tools, prefix + ".png", ct));
                File.Delete(prefix + ".png");
            }
            var t = Clean(ocr.ToString());
            return new Extraction(t, pages, Quality(t), "Tesseract (PDF مصوّر)", "");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    private static async Task<string> OcrAsync(Tools tools, string image, CancellationToken ct)
    {
        var r = await ProcessRunner.RunAsync(tools.Tesseract!, new[] { image, "stdout", "-l", tools.Languages, "--psm", "3" }, TimeSpan.FromMinutes(3), ct);
        if (r.Exit != 0) throw new InvalidOperationException("فشل Tesseract: " + r.Err.Trim());
        return r.Out;
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
