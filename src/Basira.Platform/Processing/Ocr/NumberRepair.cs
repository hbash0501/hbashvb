using System.Text.RegularExpressions;

namespace Basira.Platform.Processing.Ocr;

/// <summary>
/// إصلاح الأرقام في قراءة Tesseract بقارئ الأرقام المخصص: نماذج Tesseract العربية تقرأ الأرقام الهندية (٢٢٠٥٢)
/// حروفاً لاتينية (YY OY) وتخطئ أحياناً في العربية. كل كلمة مشتبه بها تُقرأ ثانية من صورتها:
/// الاتفاق يرفع الثقة، والاختلاف يستبدل القراءة أو يخفض ثقتها لتُعرض على المراجع، فلا يمر رقم مشكوك فيه بصمت.
/// </summary>
public static class NumberRepair
{
    /// <summary>أدنى ثقة لقارئ الأرقام ليُعتمد رقمه دون مراجعة (معايرة على مجموعة القياس).</summary>
    public const float TrustedDigitConfidence = 0.30f;

    private static readonly Regex HasDigit = new("[0-9٠-٩]", RegexOptions.Compiled);
    private static readonly Regex NumberShape = new(@"^[0-9٠-٩]+(?:[/\-][0-9٠-٩]+)*(?: [0-9٠-٩]+(?:[/\-][0-9٠-٩]+)*)*$", RegexOptions.Compiled);
    private static readonly string[] ContextWords = { "رقم", "الرقم", "برقم", "ورقم", "تاريخ", "وتاريخ", "بتاريخ", "التاريخ", "وتاريخه", "خلال", "لمدة", "مدة" };

    private static string Norm(string s) => s.Replace('أ', 'ا').Replace('إ', 'ا').Replace('ة', 'ه').Replace("ـ", "").Trim(':', '：', '،', ',', '.', '(', ')');
    private static bool IsContext(OcrWord w) => ContextWords.Contains(Norm(w.Text));
    private static bool IsLatinJunk(string t) => t.Length <= 14 && t.All(c => c < 128) && t.Any(char.IsLetter);
    private static string Key(string s) => new(s.Where(c => char.IsDigit(c) || c is '/' or '-').Select(c => c is >= '٠' and <= '٩' ? (char)('0' + (c - '٠')) : c == '-' ? '/' : c).ToArray());

    /// <summary>تتبع اختياري لأدوات القياس.</summary>
    public static Action<string>? Trace { get; set; }

    public static void Apply(OcrPage page, ImagePrep.Gray image)
    {
        var words = page.Words;
        var consumed = new HashSet<OcrWord>();
        for (var i = 0; i < words.Count; i++)
        {
            var w = words[i];
            if (w.Text.Length == 0 || consumed.Contains(w)) continue;
            var context = i > 0 && IsContext(words[i - 1]) || i > 1 && IsContext(words[i - 2]);
            var digits = HasDigit.IsMatch(w.Text);
            var junk = !digits && IsLatinJunk(w.Text);
            if (!digits && !junk && !(context && i > 0 && IsContext(words[i - 1]))) continue;
            // ضم الكلمات المجاورة في السطر نفسه التي تبدو جزءاً من الرقم نفسه (YY OY).
            var group = new List<OcrWord> { w };
            var j = i + 1;
            while (j < words.Count && SameLine(words[j], w) && (HasDigit.IsMatch(words[j].Text) || IsLatinJunk(words[j].Text)) && Near(group, words[j])) { group.Add(words[j]); j++; }
            var line = words.Where(x => SameLine(x, w) && x.Text.Length > 0).ToList();
            var box = OcrPage.Union(group);
            // صناديق Tesseract للكلمات المشوهة تمتد أحياناً إلى ما فوق السطر وتحته: تُقيَّد المنطقة بشريط السطر.
            var lineWords = line.Where(x => !group.Contains(x) && x.Text.Count(char.IsLetterOrDigit) >= 2).ToList();
            var lh = box.Height;
            if (lineWords.Count > 0)
            {
                var tops = lineWords.Select(x => x.Box.Top).OrderBy(v => v).ToList();
                var bottoms = lineWords.Select(x => x.Box.Bottom).OrderBy(v => v).ToList();
                int lt = tops[tops.Count / 2], lb = bottoms[bottoms.Count / 2];
                lh = Math.Max(1, lb - lt);
                box = new SkiaSharp.SKRectI(box.Left, Math.Max(box.Top, lt - lh / 4), box.Right, Math.Min(box.Bottom, lb + lh / 4));
                if (box.Height < lh / 2) box = new SkiaSharp.SKRectI(box.Left, lt, box.Right, lb);
            }
            // المنطقة تمتد أفقياً: الرقم يحدده الحبر لا صناديق الكلمات (قد يقسم Tesseract التاريخ الواحد كلمتين).
            var h = box.Height;
            // حدّا المنطقة: أقرب كلمة عربية غير رقمية من كل جهة (صناديق Tesseract للكلمات العربية موثوقة).
            var others = line.Where(x => !Numeric(words, x)).ToList();
            var leftBound = others.Where(x => x.Box.Right <= box.Left + h / 4).Select(x => x.Box.Right + 1).DefaultIfEmpty(0).Max();
            var rightBound = others.Where(x => x.Box.Left >= box.Right - h / 4).Select(x => x.Box.Left - 1).DefaultIfEmpty(image.Width).Min();
            leftBound = Math.Max(leftBound, box.Left - 4 * lh); rightBound = Math.Min(rightBound, box.Right + 4 * lh);
            var region = new SkiaSharp.SKRectI(Math.Max(leftBound, box.Left - lh / 2), box.Top - h / 5, Math.Min(rightBound, box.Right + lh / 2), box.Bottom + h / 5);
            var reading = DigitReader.Read(image, region);
            // رمز يلامس حافة المنطقة قد يكون مقطوعاً: تُوسَّع المنطقة من جهته (حتى الحد) وتُعاد القراءة.
            for (var grow = 0; grow < 6 && reading is not null; grow++)
            {
                var near = reading.Tokens.Where(t => t.Box.Right > box.Left - lh && t.Box.Left < box.Right + lh).ToList();
                // «قريب من الحافة» بمسافة فصل الرموز نفسها: رمز خلف الحافة أقرب من ذلك كان سيلتحق بالرقم.
                var reach = Math.Max(2, h * 9 / 10);
                var cutLeft = region.Left > leftBound && near.Any(t => t.Box.Left - region.Left <= reach);
                var cutRight = region.Right < rightBound && near.Any(t => region.Right - t.Box.Right <= reach);
                if (!cutLeft && !cutRight) break;
                region = new SkiaSharp.SKRectI(cutLeft ? Math.Max(leftBound, region.Left - lh) : region.Left, region.Top, cutRight ? Math.Min(rightBound, region.Right + lh) : region.Right, region.Bottom);
                reading = DigitReader.Read(image, region);
            }
            Trace?.Invoke($"«{string.Join(" ", group.Select(x => x.Text))}» box={box} → {string.Join(" | ", reading?.Tokens.Select(t => $"«{t.Text}» conf={t.Confidence:0.00} complete={t.Complete} [{string.Join(" ", t.Glyphs.Select(x => $"{x.Char}:{x.Confidence:0.00}"))}]") ?? Array.Empty<string>())}");
            var covered = new HashSet<OcrWord>();
            foreach (var token in reading?.Numbers.Where(t => NumberShape.IsMatch(t.Text) && t.Box.Right > box.Left && t.Box.Left < box.Right) ?? Enumerable.Empty<DigitRead>())
            {
                // كلمات السطر التي يقع مركزها داخل الرمز المقروء، بترتيبها في النص.
                var mine = line.Where(x => !consumed.Contains(x) && Numeric(words, x) && x.Box.MidX >= token.Box.Left - h / 3 && x.Box.MidX <= token.Box.Right + h / 3).ToList();
                if (mine.Count == 0) continue;
                covered.UnionWith(mine);
                consumed.UnionWith(mine);
                Resolve(mine, token, context);
            }
            // رقم لم يُقرأ يقيناً: لا يُعتمد ما قرأه Tesseract إن كان حروفاً لاتينية مكان أرقام.
            foreach (var g in group.Where(x => !covered.Contains(x) && x.Text.Length > 0 && context && IsLatinJunk(x.Text))) g.Confidence = Math.Min(g.Confidence, 20);
        }
        words.RemoveAll(x => x.Text.Length == 0);
    }

    /// <summary>كلمة قد تكون رقماً: فيها أرقام، أو حروف لاتينية مشوهة، أو علامات فقط، أو تلي كلمة سياق رقمي مباشرة.</summary>
    private static bool Numeric(List<OcrWord> words, OcrWord x)
    {
        if (HasDigit.IsMatch(x.Text) || IsLatinJunk(x.Text) || !x.Text.Any(char.IsLetter)) return true;
        var k = words.IndexOf(x);
        return k > 0 && IsContext(words[k - 1]);
    }

    private static readonly Regex ArabicLetter = new("[\u0621-\u063A\u0641-\u064A]", RegexOptions.Compiled);

    /// <summary>حروف عربية غير «هـ» اللاحقة بالتاريخ: قارئ الأرقام لا يقرؤها، فلا يستبدل كلمة تحويها.</summary>
    private static bool HasLetters(string t) => ArabicLetter.IsMatch(Regex.Replace(t, "هـ?$|ه[.,،]?$", ""));

    private static void Resolve(List<OcrWord> mine, DigitRead token, bool context)
    {
        var original = string.Join(" ", mine.Select(x => x.Text));
        var digits = mine.Any(x => HasDigit.IsMatch(x.Text));
        var span = OcrPage.Union(mine);
        if (Key(token.Text) == Key(original) && Key(original).Length > 0)
        {
            foreach (var g in mine) { g.DigitVerified = true; if (token.Confidence >= TrustedDigitConfidence) g.Confidence = Math.Max(g.Confidence, 95); }
            return;
        }
        // رقم بحرف ملحق («534/ت»): يُتحقق من جزئه الرقمي فقط، ولا يُستبدل.
        if (digits && mine.Any(x => HasLetters(x.Text)))
        {
            var part = Key(new string(original.Where(c => !char.IsLetter(c)).ToArray())).Trim('/');
            if (part.Length > 0 && Key(token.Text).Trim('/') == part) { foreach (var g in mine) g.DigitVerified = true; }
            else foreach (var g in mine) g.Confidence = Math.Min(g.Confidence, 40);
            return;
        }
        // رمز يغطي جزءاً من الكلمة فقط: شك لا استبدال.
        if (span.Width > token.OuterBox.Width * 1.4)
        {
            foreach (var g in mine) g.Confidence = Math.Min(g.Confidence, 40);
            return;
        }
        if (digits && !token.Indic && mine.All(g => g.Confidence >= 90))
        {
            // قراءتان واثقتان مختلفتان لرقم عربي: لا يُرجَّح أحدهما بصمت.
            foreach (var g in mine) g.Confidence = Math.Min(g.Confidence, 40);
            return;
        }
        // كلمة بلا أرقام (عربية أو لاتينية مشوهة) لا تُستبدل إلا إذا سبقتها كلمة سياق رقمي («رقم»، «وتاريخ»):
        // بدونه قد تكون كلمة عادية أسيء قراءتها، واستبدالها يختلق رقماً في النص.
        if (!digits && !context) return;
        // قراءة غير واثقة بلا سياق رقمي لا تُكتب في النص (تُدخل رقماً مختلقاً في البحث والربط): يكفي خفض الثقة.
        // أما بعد «الرقم:» أو «وتاريخ» فتُكتب مقترحاً منخفض الثقة يراه المراجع ويؤكده أو يصححه.
        if (token.Confidence < TrustedDigitConfidence && !context)
        {
            foreach (var g in mine) g.Confidence = Math.Min(g.Confidence, 30);
            return;
        }
        // استبدال القراءة مع حفظ الأقواس واللواحق.
        var lead = new string(original.TakeWhile(c => c is '(' or '[').ToArray());
        var trail = new string(original.Reverse().TakeWhile(c => c is ')' or ']' or '.' or '،' or ',').Reverse().ToArray());
        // قوسان حول الرقم (قد يقرأهما OCR معكوسين أو مع رموز غريبة): «)+¥(» ← «(٣٠)».
        if (original.Contains('(') && original.Contains(')')) { lead = "("; trail = ")"; }
        var suffix = token.HijriSuffix || original.Contains('ه') ? "هـ" : "";
        var first = mine[0];
        first.Text = lead + token.Text + suffix + trail;
        first.Box = span;
        first.Confidence = token.Confidence >= TrustedDigitConfidence ? 85 + 10 * token.Confidence : Math.Min(50, 100 * token.Confidence);
        first.Corrected = true; first.DigitVerified = true;
        foreach (var g in mine.Skip(1)) g.Text = "";
    }

    private static bool SameLine(OcrWord a, OcrWord b) => a.Block == b.Block && a.Paragraph == b.Paragraph && a.Line == b.Line;

    private static bool Near(List<OcrWord> group, OcrWord w)
    {
        var u = OcrPage.Union(group);
        var gap = Math.Max(w.Box.Left - u.Right, u.Left - w.Box.Right);
        return gap < u.Height * 0.8;
    }
}
