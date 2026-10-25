using System.Globalization;
using System.Text.RegularExpressions;
using AIDocumentArchiveVB;
using SkiaSharp;

namespace Basira.Platform.Processing.Ocr;

public sealed record OcrOptions(string Tesseract, string Languages, string? Tessdata, bool Preprocess = true, bool FieldPass = true, string[]? Params = null, bool DigitPass = true)
{
    public string[] Extra => Params ?? Array.Empty<string>();
}

/// <summary>حقل حرج (رقم الخطاب، تاريخه، إشارة) بقيمته ودرجة الاطمئنان إليه وموضعه في الصورة لعرضه على المراجع.</summary>
public sealed record FieldCheck(string Field, string Value, List<string> Candidates, bool Confident, int Page, SKRectI Box, int Offset, int Length, string Reason);

public sealed class OcrDocument
{
    public List<OcrPage> Pages { get; } = new();
    public List<ScanQuality> Quality { get; } = new();
    public List<string> PreparedImages { get; } = new();
    public List<FieldCheck> Checks { get; } = new();
    public string Text { get; set; } = "";
    public int MeanConfidence => Pages.SelectMany(p => p.Words).Select(w => (double)w.Confidence).DefaultIfEmpty(0).Average() is var a ? (int)Math.Round(a) : 0;
}

/// <summary>
/// خط القراءة: (1) فحص جودة المسح (2) تحسين الصورة والنموذج الدقيق (3) قراءة ثانية مركّزة للحقول الحرجة مع درجات الثقة،
/// وتصحيحات معروفة لأخطاء OCR العربي. الحقل غير المؤكد يُعاد كفحص يحتاج مراجعة بشرية بدل أن يُعتمد بصمت.
/// </summary>
public static class OcrPipeline
{
    public const float WordConfidence = 80;

    public static async Task<OcrDocument> ReadImagesAsync(IReadOnlyList<string> images, OcrOptions o, string workDir, Action<int, string>? progress, CancellationToken ct)
    {
        var doc = new OcrDocument();
        for (var i = 0; i < images.Count; i++)
        {
            progress?.Invoke(15 + 70 * i / Math.Max(1, images.Count), images.Count > 1 ? $"التعرف الضوئي: صفحة {i + 1} من {images.Count}" : "التعرف الضوئي على الحروف");
            var input = images[i];
            ImagePrep.Gray? pageImage = null;
            if (o.Preprocess && ImagePrep.Load(input) is { } gray)
            {
                var analysis = ImagePrep.Analyze(gray);
                doc.Quality.Add(analysis.Quality);
                var prepared = Path.Combine(workDir, $"page-{i + 1}.png");
                pageImage = ImagePrep.Prepare(analysis, prepared);
                input = prepared;
            }
            doc.PreparedImages.Add(input);
            var page = await TesseractOcr.ReadAsync(o.Tesseract, input, o.Languages, 3, o.Tessdata, ct, o.Extra);
            if (o.DigitPass && (pageImage ?? ImagePrep.Load(input)) is { } img) NumberRepair.Apply(page, img);
            doc.Pages.Add(page);
        }
        foreach (var p in doc.Pages) CorrectInContext(p.Words);
        doc.Text = TesseractOcr.Assemble(doc.Pages);
        if (o.FieldPass)
        {
            progress?.Invoke(88, "التحقق من الأرقام والتواريخ");
            await VerifyFieldsAsync(doc, o, workDir, ct);
        }
        return doc;
    }

    // ===== تصحيحات معروفة لأخطاء OCR في الخطابات العربية =====
    private static readonly Regex HijriWithPrefix = new(@"^[2٢](1[34][0-9]{2}/[0-9]{1,2}/[0-9]{1,2})[.,،]?$", RegexOptions.Compiled);
    private static readonly Regex HijriWithDot = new(@"^(1[34][0-9]{2}/[0-9]{1,2}/[0-9]{1,2})[.,،]$", RegexOptions.Compiled);

    /// <summary>«هـ» الملاصقة للتاريخ تُقرأ أحياناً رقماً «2» في أوله أو نقطة في آخره: «21446/08/07.» ← «1446/08/07هـ».</summary>
    public static void Correct(OcrWord w)
    {
        var t = w.Text;
        if (HijriWithPrefix.Match(t) is { Success: true } m && IsHijri(m.Groups[1].Value)) { w.Text = m.Groups[1].Value + "هـ"; w.Corrected = true; }
        else if (HijriWithDot.Match(t) is { Success: true } d && IsHijri(d.Groups[1].Value)) { w.Text = d.Groups[1].Value + "هـ"; w.Corrected = true; }
    }

    private static readonly Regex YearWithPrefix = new(@"^[2٢](1[34][0-9]{2}|١[٣٤][٠-٩]{2})[.,،]?$", RegexOptions.Compiled);
    private static readonly Regex SlashAlef = new(@"^([0-9٠-٩]+)/?ا(\p{L})$", RegexOptions.Compiled);
    private static readonly string[] HijriMonths = { "محرم", "صفر", "الاول", "الاولي", "الاخر", "الثاني", "الاخره", "الثانيه", "رجب", "شعبان", "رمضان", "شوال", "القعده", "الحجه" };

    /// <summary>تصحيحات تحتاج سياق الكلمات المجاورة، ثم التصحيحات المفردة لكل كلمة.</summary>
    public static void CorrectInContext(List<OcrWord> words)
    {
        for (var i = 0; i < words.Count; i++)
        {
            var w = words[i];
            // «10 ذو القعدة 21446» ← «1446هـ»: الهاء الملاصقة للسنة بعد اسم شهر هجري.
            if (i > 0 && YearWithPrefix.Match(w.Text) is { Success: true } y && HijriMonths.Contains(Norm(words[i - 1].Text)))
            { w.Text = y.Groups[1].Value + "هـ"; w.Corrected = true; continue; }
            // «534/ات» و«534اص» ← «534/ت»: الشرطة المائلة تُقرأ ألفاً (مع نفسها أو بدلاً منها).
            if (SlashAlef.Match(w.Text) is { Success: true } s) { w.Text = s.Groups[1].Value + "/" + s.Groups[2].Value; w.Corrected = true; continue; }
            Correct(w);
        }
    }

    private static bool IsHijri(string ymd)
    {
        var c = ""; DateTime v = default;
        return CorrespondenceReferenceParser.TryParseDate(ymd + "هـ", ref c, ref v);
    }

    // ===== الطبقة 3: قراءة ثانية مركّزة للحقول الحرجة =====
    private static string Norm(string s) => s.Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ة', 'ه').Replace('ى', 'ي').Replace("ـ", "");

    private static async Task VerifyFieldsAsync(OcrDocument doc, OcrOptions o, string workDir, CancellationToken ct)
    {
        if (doc.Pages.Count == 0) return;
        var page = doc.Pages[0];
        var image = doc.PreparedImages[0];
        var lines = page.Lines().Select(g => g.ToList()).ToList();
        var gray = o.DigitPass ? ImagePrep.Load(image) : null;
        await HeaderFieldAsync(doc, o, workDir, image, gray, lines, "number", new[] { "الرقم", "رقم الصادر", "رقم الخطاب", "رقم القيد", "رقم الوارد" }, ct);
        await HeaderFieldAsync(doc, o, workDir, image, gray, lines, "date", new[] { "التاريخ" }, ct);
        Subject(doc, lines);
        await ReferencesAsync(doc, o, workDir, ct);
        Deadlines(doc);
        // صورة رفضتها بوابة الجودة: لا يُعتمد منها حقل دون مراجعة، والمطلوب إعادة المسح.
        if (doc.Quality.Any(q => !q.Acceptable))
            for (var i = 0; i < doc.Checks.Count; i++)
                if (doc.Checks[i].Confident) doc.Checks[i] = doc.Checks[i] with { Confident = false, Reason = "جودة المسح دون الحد المقبول" };
    }

    public const float SubjectConfidence = 85;

    /// <summary>الموضوع: يُعتمد إن قُرئت كل كلماته بثقة عالية؛ وإلا يُعرض على المراجع (الخطأ فيه يضلل البحث).</summary>
    private static void Subject(OcrDocument doc, List<List<OcrWord>> lines)
    {
        foreach (var line in lines.Take(20))
        {
            var li = LabelIndex(line, new[] { "الموضوع" });
            if (li < 0) continue;
            var words = line.Skip(li + 1).ToList();
            if (words.Count == 0) return;
            var weak = words.Where(w => w.Confidence < SubjectConfidence).ToList();
            doc.Checks.Add(new FieldCheck("subject", string.Join(" ", words.Select(w => w.Text)), new List<string>(), weak.Count == 0, 0, OcrPage.Union(words), words[0].Start, words[^1].End - words[0].Start,
                weak.Count == 0 ? "" : "كلمات غير مؤكدة: " + string.Join("، ", weak.Select(w => w.Text))));
            return;
        }
    }

    /// <summary>المهل: الرقم داخل «خلال (15) يوماً» حرج؛ إن انخفضت ثقته تُعلَّم المهلة للمراجعة.</summary>
    private static void Deadlines(OcrDocument doc)
    {
        var words = doc.Pages.SelectMany(p => p.Words).ToList();
        foreach (var h in DeadlineExtractor.Extract(doc.Text, null))
        {
            var span = words.Where(w => w.End > h.Offset && w.Start < h.Offset + h.Length).ToList();
            if (span.Count == 0) continue;
            var digits = span.Where(w => Regex.IsMatch(w.Text, @"[0-9\u0660-\u0669]")).ToList();
            var minConf = digits.Count == 0 ? span.Min(w => w.Confidence) : digits.Min(w => w.Confidence);
            var confident = minConf >= WordConfidence;
            doc.Checks.Add(new FieldCheck("deadline", h.IsRelative ? h.Days.ToString(CultureInfo.InvariantCulture) : h.Due?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                new List<string>(), confident, span[0].Page, OcrPage.Union(span), h.Offset, h.Length, confident ? "" : "ثقة قراءة المهلة منخفضة"));
        }
    }

    private static int LabelIndex(List<OcrWord> line, string[] labels)
    {
        for (var i = 0; i < line.Count; i++)
            foreach (var label in labels)
            {
                var parts = label.Split(' ');
                if (i + parts.Length > line.Count) continue;
                var ok = true;
                for (var k = 0; k < parts.Length && ok; k++) ok = Norm(line[i + k].Text).TrimEnd(':', '：').StartsWith(Norm(parts[k]), StringComparison.Ordinal) && (k < parts.Length - 1 || Norm(line[i + k].Text).TrimEnd(':', '：').Length <= Norm(parts[k]).Length + 1);
                if (ok) return i + parts.Length - 1;
            }
        return -1;
    }

    private static string FieldValue(string field, string text)
    {
        text = text.Trim().TrimStart(':', '：').Trim();
        if (field == "number")
        {
            var m = Regex.Match(text, @"^[(\[]?\s*([A-Za-z0-9٠-٩]+(?:\s*[/\-]\s*(?:[A-Za-z0-9٠-٩]+|\p{L}{1,3}(?!\p{L})))*)");
            return m.Success ? CorrespondenceReferenceParser.NumberKey(m.Groups[1].Value) : "";
        }
        var c = ""; DateTime v = default;
        var dm = Regex.Match(text, @"[0-9٠-٩]{1,4}\s*[/\-.]\s*[0-9٠-٩]{1,2}\s*[/\-.]\s*[0-9٠-٩]{1,4}\s*(?:هـ|ه|م)?|[0-9٠-٩]{1,2}\s+\p{L}+(?:\s+\p{L}+)?\s+[0-9٠-٩]{3,4}\s*(?:هـ|ه|م)?");
        if (!dm.Success) return "";
        var w = new OcrWord { Text = dm.Value.Replace(" ", "") };
        Correct(w);
        var candidate = dm.Value.Contains(' ') && !Regex.IsMatch(dm.Value, @"^\d+\s*/") ? dm.Value : w.Text;
        return CorrespondenceReferenceParser.TryParseDate(candidate, ref c, ref v) ? v.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
    }

    private static async Task HeaderFieldAsync(OcrDocument doc, OcrOptions o, string workDir, string image, ImagePrep.Gray? gray, List<List<OcrWord>> lines, string field, string[] labels, CancellationToken ct)
    {
        foreach (var line in lines.Take(14))
        {
            var li = LabelIndex(line, labels);
            if (li < 0) continue;
            var label = line[li];
            var valueWords = line.Skip(li + 1).TakeWhile(w => LabelIndex(new List<OcrWord> { w }, new[] { "التاريخ", "المرفقات", "الموضوع", "الرقم" }) < 0).Take(field == "number" ? 3 : 4).ToList();
            var first = FieldValue(field, string.Join(" ", valueWords.Select(w => w.Text)));
            // منطقة القيمة: يسار العنوان في السطر نفسه (الكتابة من اليمين)، دون العنوان ودون السطرين المجاورين.
            var h = label.Box.Height;
            var right = label.Box.Left - Math.Max(2, h / 8);
            var left = valueWords.Count > 0 ? Math.Min(valueWords.Min(w => w.Box.Left), right - h * 3) - h / 2 : right - h * 12;
            var box = new SKRectI(Math.Max(0, left), Math.Max(0, label.Box.Top - h / 5), Math.Max(left + h, right), label.Box.Bottom + h / 5);
            var tesseractRead = "";
            var digitRead = "";
            try
            {
                var crop = Path.Combine(workDir, $"{field}-{Guid.NewGuid():N}.png");
                await File.WriteAllBytesAsync(crop, ImagePrep.Crop(image, box, 0, 2.0), ct);
                var plain = await TesseractOcr.ReadAsync(o.Tesseract, crop, o.Languages, 7, o.Tessdata, ct);
                foreach (var w in plain.Words) Correct(w);
                tesseractRead = FieldValue(field, string.Join(" ", plain.Words.Select(w => w.Text)));
                File.Delete(crop);
                if (gray is not null && DigitReader.Read(gray, box) is { } reading)
                {
                    if (field == "date" && reading.Numbers.FirstOrDefault() is { } token && token.Confidence >= NumberRepair.TrustedDigitConfidence)
                        digitRead = FieldValue(field, token.Text + (token.HijriSuffix ? "هـ" : ""));
                    else if (field == "number")
                    {
                        // الجزء الرقمي فقط (الحرف الملحق «534/ت» خارج قدرة قارئ الأرقام): أطول تسلسل أرقام وفواصل.
                        var best = reading.Tokens.Select(t => t.Glyphs.Where(g => char.IsDigit(g.Char) || g.Char == '/').ToList()).OrderByDescending(g => g.Count(x => char.IsDigit(x.Char))).FirstOrDefault();
                        if (best is { Count: > 0 } && best.Where(g => char.IsDigit(g.Char)).Min(g => g.Confidence) >= NumberRepair.TrustedDigitConfidence)
                            digitRead = CorrespondenceReferenceParser.NumberKey(new string(best.Select(g => g.Char).ToArray()).Trim('/'));
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or InvalidDataException) { }
            // ثلاثة مصادر: نص الصفحة (Tesseract، وقد يكون أصلحه قارئ الأرقام)، وقراءة Tesseract ثانية للمنطقة، وقارئ الأرقام للمنطقة.
            // الحقل مؤكد إذا اتفق مصدران مستقلان، أو إذا تحقق قارئ الأرقام من كلماته بثقة كافية.
            var verifiedWords = valueWords.Count > 0 && valueWords.Where(w => HasDigits(w.Text)).DefaultIfEmpty().All(w => w is not null && w.DigitVerified && w.Confidence >= 85);
            var digitPart = field == "number" ? DigitsOf(first) : first;
            var agreeTesseract = first != "" && tesseractRead == first;
            // إن كان قارئ الأرقام هو من كتب القيمة في نص الصفحة فقراءته الثانية ليست مصدراً مستقلاً.
            var readerWrote = valueWords.Any(w => w.Corrected && w.DigitVerified);
            var agreeDigits = !readerWrote && first != "" && digitRead != "" && (field == "number" ? DigitsOf(digitRead).Trim('/') == digitPart.Trim('/') : digitRead == first);
            var lettersOk = field != "number" || !Regex.IsMatch(first, @"\p{L}") || agreeTesseract || valueWords.All(w => w.Confidence >= WordConfidence);
            // بقية حروف ملتصقة بالرقم لم تدخل في القيمة («579اص» ← 579): الرقم ناقص على الأرجح.
            if (field == "number" && valueWords.Count > 0 && Regex.IsMatch(valueWords[0].Text, @"[0-9٠-٩]\p{L}")) lettersOk = false;
            // قراءة المنطقة كاملة بقارئ الأرقام تخالف القيمة: تناقض صريح يمنع الاعتماد مهما كانت ثقة الكلمات منفردة
            // (رقم واحد قد يُقرأ رمزين منفصلين «٠٥ ٨٤٤٠» يتحقق كل منهما وحده).
            var contradicts = first != "" && digitRead != "" && (field == "number" ? DigitsOf(digitRead).Trim('/') != digitPart.Trim('/') : digitRead != first);
            var confident = first != "" && lettersOk && !contradicts && (agreeTesseract || agreeDigits || verifiedWords);
            var candidates = new List<string> { first, tesseractRead, digitRead }.Where(c => c != "").Distinct().ToList();
            var reason = first == "" ? "لم تُقرأ القيمة" : !lettersOk ? "الحرف الملحق بالرقم غير مؤكد" : contradicts ? "قراءة الحقل كاملاً تخالف القيمة" : !confident ? (candidates.Count > 1 ? "القراءات المتعددة غير متطابقة" : "ثقة القراءة منخفضة") : "";
            var value = first != "" ? first : candidates.FirstOrDefault() ?? "";
            doc.Checks.Add(new FieldCheck(field, value, candidates, confident, 0, box, valueWords.FirstOrDefault()?.Start ?? -1, valueWords.Count == 0 ? 0 : valueWords[^1].End - valueWords[0].Start, reason));
            return;
        }
        doc.Checks.Add(new FieldCheck(field, "", new List<string>(), false, 0, SKRectI.Empty, -1, 0, "لم يُعثر على الحقل في الترويسة"));
    }

    private static bool HasDigits(string s) => s.Any(c => c is >= '0' and <= '9' or >= '٠' and <= '٩');
    private static string DigitsOf(string s) => new(s.Where(c => c is >= '0' and <= '9' or '/').ToArray());

    /// <summary>الإشارات في المتن: الكلمات منخفضة الثقة في رقمها أو تاريخها تُعاد قراءتها سطراً كاملاً؛ الاتفاق يؤكدها.</summary>
    private static async Task ReferencesAsync(OcrDocument doc, OcrOptions o, string workDir, CancellationToken ct)
    {
        var words = doc.Pages.SelectMany(p => p.Words).ToList();
        foreach (var r in CorrespondenceReferenceParser.Parse(doc.Text))
        {
            var span = words.Where(w => w.End > r.Offset && w.Start < r.Offset + r.Length).ToList();
            if (span.Count == 0) continue;
            var critical = span.Where(w => Regex.IsMatch(w.Text, @"[0-9٠-٩]")).ToList();
            var minConf = critical.Count == 0 ? 100 : critical.Min(w => w.Confidence);
            // «وتاريخ» بعد الرقم بلا تاريخ مقروء: التاريخ موجود في الصورة لكن لم يُقرأ.
            var after = words.Where(w => w.Start >= r.Offset + r.Length).Take(1).Select(w => Norm(w.Text).Trim(':', '،', ',')).FirstOrDefault() ?? "";
            var dateUnread = r.DateText == "" && after is "وتاريخ" or "بتاريخ" or "تاريخ" or "وتاريخه";
            var dateOk = (r.DateText == "" || r.Date.HasValue) && !dateUnread;
            var expected = r.Number + "@" + (r.Date?.ToString("yyyy-MM-dd") ?? "");
            var candidates = new List<string> { expected };
            // التصحيح بقارئ الأرقام دليل لا شك؛ أما التصحيح بقاعدة نصية وحدها فيُعاد التحقق منه.
            var confident = minConf >= WordConfidence && dateOk && !critical.Any(w => w.Corrected && !w.DigitVerified);
            if (!confident)
            {
                // أعد قراءة الأسطر التي تحوي الإشارة.
                var page = span[0].Page;
                var lineKeys = span.Select(w => (w.Block, w.Paragraph, w.Line)).Distinct().ToList();
                var box = OcrPage.Union(doc.Pages[page].Words.Where(w => lineKeys.Contains((w.Block, w.Paragraph, w.Line))));
                try
                {
                    var crop = Path.Combine(workDir, $"ref-{Guid.NewGuid():N}.png");
                    await File.WriteAllBytesAsync(crop, ImagePrep.Crop(doc.PreparedImages[page], box, Math.Max(6, box.Height / 6), 1.6), ct);
                    var again = await TesseractOcr.ReadAsync(o.Tesseract, crop, o.Languages, lineKeys.Count > 1 ? 6 : 7, o.Tessdata, ct);
                    foreach (var w in again.Words) Correct(w);
                    var text = string.Join(" ", again.Words.Select(w => w.Text));
                    var match = CorrespondenceReferenceParser.Parse(text).FirstOrDefault(x => CorrespondenceReferenceParser.NumbersMatch(x.Number, r.Number) || x.Number == r.Number);
                    if (match is not null) candidates.Add(match.Number + "@" + (match.Date?.ToString("yyyy-MM-dd") ?? ""));
                    File.Delete(crop);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or InvalidDataException) { }
                confident = dateOk && candidates.Count > 1 && candidates[1] == expected && (minConf >= 50 || critical.Any(w => w.Corrected));
            }
            var box2 = OcrPage.Union(span);
            doc.Checks.Add(new FieldCheck("reference", expected, candidates.Distinct().ToList(), confident, span[0].Page, box2, r.Offset, r.Length,
                confident ? "" : dateUnread ? "تاريخ الإشارة لم يُقرأ" : !dateOk ? "تاريخ الإشارة غير صالح" : candidates.Count < 2 ? "ثقة القراءة منخفضة" : "القراءة الثانية لم تطابق"));
        }
    }
}
