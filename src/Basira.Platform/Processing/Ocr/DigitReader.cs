using System.IO.Compression;
using SkiaSharp;

namespace Basira.Platform.Processing.Ocr;

/// <summary>رمز قرأه قارئ الأرقام مع موضعه ودرجة الاطمئنان (0..1).</summary>
public sealed record DigitGlyph(char Char, float Confidence, SKRectI Box);

/// <summary>
/// رمز مقروء: النص بترتيب الكتابة (الأرقام من اليسار)، وأدنى ثقة بين رموزه، والخط (هندي أو عربي)،
/// و«كامل» إن كانت كل رموزه أرقاماً أو فواصل (مع «هـ» والعلامات في الطرفين فقط).
/// </summary>
public sealed record DigitRead(string Text, float Confidence, bool Indic, bool Complete, bool HijriSuffix, List<DigitGlyph> Glyphs, SKRectI Box)
{
    /// <summary>صندوق الرمز مع القوسين المحيطين به إن وُجدا.</summary>
    public SKRectI OuterBox { get; init; } = Box;
}

/// <summary>قراءة منطقة: رموزها المفصولة بفراغات، من اليسار إلى اليمين.</summary>
public sealed record DigitReading(List<DigitRead> Tokens, bool Indic)
{
    /// <summary>الرموز الرقمية الكاملة فقط.</summary>
    public IEnumerable<DigitRead> Numbers => Tokens.Where(t => t.Complete);
}

/// <summary>
/// قارئ مخصص للأرقام (الهندية ٠-٩ والعربية 0-9) والتواريخ، لأن نماذج Tesseract العربية تقرأ الأرقام الهندية حروفاً لاتينية.
/// يقطّع المنطقة إلى مكونات متصلة، ويصنّف كل مكون بأقرب نموذج من قوالب مولّدة من خطوط عربية مفتوحة متنوعة،
/// ويقرر الخط للرقم كاملاً (فلا يُخلط «٥» الهندي بـ «0»). الثقة هي هامش الفرق بين أقرب صنف وأقرب صنف بقيمة مختلفة.
/// </summary>
public static class DigitReader
{
    /// <summary>الأصناف بترتيب ملف القوالب. «ه» تمثل «هـ» الهجرية، والأقواس والنقطة والنقطتان والفاصلة علامات تُهمل في الطرفين.</summary>
    public const string Classes = "0123456789٠١٢٣٤٥٦٧٨٩/-ه.:()،";
    public const int Grid = 16;
    public const int FeatureLength = Grid * Grid + 3;
    private const float ExtraWeight = 4f;

    public sealed record Template(int Class, byte[] F);

    private static readonly Lazy<List<Template>> Templates = new(LoadTemplates);

    public static int TemplateCount => Templates.Value.Count;

    private static List<Template> LoadTemplates()
    {
        using var s = typeof(DigitReader).Assembly.GetManifestResourceStream("digits.bin.gz")
            ?? throw new InvalidOperationException("قوالب قارئ الأرقام غير مضمّنة.");
        return ReadTemplates(s);
    }

    public static List<Template> ReadTemplates(Stream compressed)
    {
        using var z = new GZipStream(compressed, CompressionMode.Decompress);
        using var r = new BinaryReader(z);
        if (new string(r.ReadChars(4)) != "BDG1") throw new InvalidDataException("ملف قوالب غير صالح.");
        var n = r.ReadInt32(); var len = r.ReadInt32();
        if (len != FeatureLength) throw new InvalidDataException("طول السمات لا يطابق.");
        var list = new List<Template>(n);
        for (var i = 0; i < n; i++) { var c = r.ReadByte(); list.Add(new Template(c, r.ReadBytes(len))); }
        return list;
    }

    public static void WriteTemplates(Stream output, IEnumerable<(int Class, byte[] Features)> templates)
    {
        var list = templates.ToList();
        using var z = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true);
        using var w = new BinaryWriter(z);
        w.Write("BDG1".ToCharArray()); w.Write(list.Count); w.Write(FeatureLength);
        foreach (var (c, f) in list) { w.Write((byte)c); w.Write(f); }
    }

    // ===== التقطيع والسمات (مشتركة بين توليد القوالب والقراءة) =====

    public sealed class Blob
    {
        public SKRectI Box;
        public List<int> Pixels = new(); // فهارس داخل المنطقة (y * عرض + x)
    }

    /// <summary>المكونات المتصلة (8 جيران) للحبر داخل المنطقة، مع دمج المكونات المتراكبة رأسياً (نقطتا «:»، و«هـ» إن انفصلت).</summary>
    public static List<Blob> Segment(byte[] px, int width, int height, SKRectI region, int threshold)
    {
        region = SKRectI.Intersect(region, new SKRectI(0, 0, width, height));
        int w = region.Width, h = region.Height;
        var blobs = new List<Blob>();
        if (w <= 0 || h <= 0) return blobs;
        var label = new int[w * h];
        var stack = new Stack<int>();
        var id = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (label[i] != 0 || px[(region.Top + y) * width + region.Left + x] > threshold) continue;
                id++;
                var b = new Blob { Box = new SKRectI(x, y, x + 1, y + 1) };
                label[i] = id; stack.Push(i);
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    b.Pixels.Add(p);
                    int py = p / w, pxx = p % w;
                    if (pxx < b.Box.Left) b.Box.Left = pxx;
                    if (pxx + 1 > b.Box.Right) b.Box.Right = pxx + 1;
                    if (py < b.Box.Top) b.Box.Top = py;
                    if (py + 1 > b.Box.Bottom) b.Box.Bottom = py + 1;
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            int ny = py + dy, nx = pxx + dx;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            var q = ny * w + nx;
                            if (label[q] != 0 || px[(region.Top + ny) * width + region.Left + nx] > threshold) continue;
                            label[q] = id; stack.Push(q);
                        }
                }
                blobs.Add(b);
            }
        // إزالة التشويش: مكونات أصغر كثيراً من أكبرها.
        if (blobs.Count == 0) return blobs;
        var maxH = blobs.Max(b => b.Box.Height);
        var minArea = Math.Max(3, (int)(maxH * maxH * 0.004));
        blobs = blobs.Where(b => b.Pixels.Count >= minArea && b.Box.Height >= Math.Max(2, maxH / 12)).OrderBy(b => b.Box.Left).ToList();
        // دمج المتراكب أفقياً المنفصل رأسياً.
        var merged = new List<Blob>();
        foreach (var b in blobs)
        {
            var host = merged.LastOrDefault(m =>
            {
                var overlap = Math.Min(m.Box.Right, b.Box.Right) - Math.Max(m.Box.Left, b.Box.Left);
                var vOverlap = Math.Min(m.Box.Bottom, b.Box.Bottom) - Math.Max(m.Box.Top, b.Box.Top);
                return overlap >= 0.5 * Math.Min(m.Box.Width, b.Box.Width) && vOverlap <= 0;
            });
            if (host is null) { merged.Add(b); continue; }
            host.Pixels.AddRange(b.Pixels);
            host.Box = SKRectI.Union(host.Box, b.Box);
        }
        // إعادة الإحداثيات إلى الصورة كاملة.
        foreach (var b in merged)
        {
            b.Pixels = b.Pixels.Select(p => (region.Top + p / w) * width + region.Left + p % w).ToList();
            b.Box = new SKRectI(b.Box.Left + region.Left, b.Box.Top + region.Top, b.Box.Right + region.Left, b.Box.Bottom + region.Top);
        }
        return merged.OrderBy(b => b.Box.Left).ToList();
    }

    /// <summary>شريط السطر: الوسيط لأعلى وأسفل المكونات الطويلة (≥ 60% من أطولها).</summary>
    public static (int Top, int Height) Band(List<Blob> blobs)
    {
        var maxH = blobs.Max(b => b.Box.Height);
        var tall = blobs.Where(b => b.Box.Height >= maxH * 0.6).ToList();
        var tops = tall.Select(b => b.Box.Top).OrderBy(v => v).ToList();
        var bottoms = tall.Select(b => b.Box.Bottom).OrderBy(v => v).ToList();
        var top = tops[tops.Count / 2]; var bottom = bottoms[bottoms.Count / 2];
        return (top, Math.Max(1, bottom - top));
    }

    /// <summary>السمات: صورة المكون مصغّرة إلى 16×16 بنسبة أبعاده، ونسبة العرض للارتفاع، وارتفاعه وموضعه الرأسي نسبة إلى السطر.</summary>
    public static byte[] Features(Blob b, int imageWidth, int bandTop, int bandHeight)
    {
        int bw = b.Box.Width, bh = b.Box.Height;
        var mask = new bool[bw * bh];
        foreach (var p in b.Pixels) mask[(p / imageWidth - b.Box.Top) * bw + p % imageWidth - b.Box.Left] = true;
        var f = new byte[FeatureLength];
        var scale = (double)Grid / Math.Max(bw, bh);
        var offX = (Grid - bw * scale) / 2; var offY = (Grid - bh * scale) / 2;
        const int sub = 4;
        for (var gy = 0; gy < Grid; gy++)
            for (var gx = 0; gx < Grid; gx++)
            {
                var hits = 0;
                for (var sy = 0; sy < sub; sy++)
                    for (var sx = 0; sx < sub; sx++)
                    {
                        var x = (int)Math.Floor((gx + (sx + 0.5) / sub - offX) / scale);
                        var y = (int)Math.Floor((gy + (sy + 0.5) / sub - offY) / scale);
                        if (x >= 0 && y >= 0 && x < bw && y < bh && mask[y * bw + x]) hits++;
                    }
                f[gy * Grid + gx] = (byte)(hits * 255 / (sub * sub));
            }
        byte Q(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
        f[Grid * Grid] = Q(Math.Clamp(Math.Log((double)bw / bh), -2.5, 2.5) / 5 + 0.5);
        f[Grid * Grid + 1] = Q(Math.Clamp((double)bh / bandHeight, 0, 2) / 2);
        f[Grid * Grid + 2] = Q(Math.Clamp((b.Box.MidY - bandTop) / (double)bandHeight, -0.5, 1.5) / 2 + 0.25);
        return f;
    }

    private static float Distance(byte[] a, byte[] b)
    {
        float d = 0;
        for (var i = 0; i < Grid * Grid; i++) { float t = a[i] - b[i]; d += t * t; }
        float e = 0;
        for (var i = Grid * Grid; i < FeatureLength; i++) { float t = a[i] - b[i]; e += t * t; }
        return (d + ExtraWeight * ExtraWeight * e) / (255f * 255f);
    }

    private static float[] Nearest(byte[] f)
    {
        var d = new float[Classes.Length];
        Array.Fill(d, float.MaxValue);
        foreach (var t in Templates.Value) { var x = Distance(f, t.F); if (x < d[t.Class]) d[t.Class] = x; }
        return d;
    }

    /// <summary>أقرب صنف وهامش الثقة عنه (بين كل الأصناف).</summary>
    private static (int Cls, float Conf) Top(float[] d)
    {
        int c1 = 0; for (var c = 1; c < d.Length; c++) if (d[c] < d[c1]) c1 = c;
        float d2 = float.MaxValue; for (var c = 0; c < d.Length; c++) if (Value(c) != Value(c1) && d[c] < d2) d2 = d[c];
        return (c1, d2 == float.MaxValue || d2 <= 0 ? 0f : Math.Clamp(1 - d[c1] / d2, 0f, 1f));
    }

    /// <summary>
    /// رقمان متلاصقان (يلتصقان مع الاهتزاز والحبر الثقيل) يظهران مكوناً واحداً عريضاً يُصنَّف خطأً.
    /// يُقسم المكون العريض أو ضعيف الثقة عند أقل عمود حبراً في وسطه، ويُقبل التقسيم إن كان نصفاه رقمين أوثق من الكل.
    /// </summary>
    private static void SplitTouching(ImagePrep.Gray g, List<Blob> blobs, List<float[]> best, int top, int height)
    {
        for (var i = 0; i < blobs.Count; i++)
        {
            var b = blobs[i];
            var whole = Top(best[i]);
            if (b.Box.Width < height * 0.6 || b.Box.Height < height * 0.5) continue;
            if (whole.Conf >= 0.35f && Classes[whole.Cls] is not ('ه' or '-')) continue;
            var cols = new int[b.Box.Width];
            foreach (var p in b.Pixels) cols[p % g.Width - b.Box.Left]++;
            int lo = b.Box.Width * 3 / 10, hi = b.Box.Width * 7 / 10, cut = -1;
            for (var x = lo; x <= hi; x++) if (cut < 0 || cols[x] < cols[cut]) cut = x;
            if (cut <= 0) continue;
            Blob Part(Func<int, bool> keep)
            {
                var part = new Blob { Pixels = b.Pixels.Where(p => keep(p % g.Width - b.Box.Left)).ToList() };
                if (part.Pixels.Count == 0) return part;
                int l = int.MaxValue, t = int.MaxValue, r = 0, bt = 0;
                foreach (var p in part.Pixels) { int x = p % g.Width, y = p / g.Width; l = Math.Min(l, x); r = Math.Max(r, x + 1); t = Math.Min(t, y); bt = Math.Max(bt, y + 1); }
                part.Box = new SKRectI(l, t, r, bt);
                return part;
            }
            var left = Part(x => x < cut); var right = Part(x => x >= cut);
            if (left.Pixels.Count < 4 || right.Pixels.Count < 4) continue;
            var dl = Nearest(Features(left, g.Width, top, height)); var dr = Nearest(Features(right, g.Width, top, height));
            var tl = Top(dl); var tr = Top(dr);
            if (!char.IsDigit(Classes[tl.Cls]) || !char.IsDigit(Classes[tr.Cls]) || Math.Min(tl.Conf, tr.Conf) < 0.35f || Math.Min(tl.Conf, tr.Conf) <= whole.Conf + 0.1f) continue;
            blobs[i] = left; best[i] = dl;
            blobs.Insert(i + 1, right); best.Insert(i + 1, dr);
            i++;
        }
    }

    private static int Value(int cls) => cls < 10 ? cls : cls < 20 ? cls - 10 : -cls; // الفواصل والعلامات قيم سالبة مميزة
    private static bool InScript(int cls, bool indic) => cls >= 20 || (indic ? cls is >= 10 and < 20 : cls < 10);

    /// <summary>يقرأ الأرقام في منطقة من صورة رمادية (ورق فاتح وحبر داكن).</summary>
    public static DigitReading? Read(ImagePrep.Gray g, SKRectI box)
    {
        var region = SKRectI.Intersect(box, new SKRectI(0, 0, g.Width, g.Height));
        if (region.Width < 4 || region.Height < 4) return null;
        var threshold = LocalThreshold(g, region);
        if (threshold < 0) return null;
        var blobs = Segment(g.Pixels, g.Width, g.Height, region, threshold);
        if (blobs.Count == 0) return null;
        // أطراف مقطوعة من السطرين المجاورين تلامس حافة المنطقة العليا أو السفلى.
        var tallest = blobs.Max(b => b.Box.Height);
        blobs = blobs.Where(b => !((b.Box.Top <= region.Top || b.Box.Bottom >= region.Bottom) && b.Box.Height < tallest * 0.6)).ToList();
        if (blobs.Count == 0) return null;
        var (top, height) = Band(blobs);
        // لكل مكون: أقرب مسافة لكل صنف.
        var best = blobs.Select(b => Nearest(Features(b, g.Width, top, height))).ToList();
        SplitTouching(g, blobs, best, top, height);
        (List<(int Cls, float Conf)> Pick, float Total) Interpret(bool indic)
        {
            var pick = new List<(int, float)>(); float total = 0;
            foreach (var d in best)
            {
                int c1 = -1; float d1 = float.MaxValue;
                for (var c = 0; c < d.Length; c++) if (InScript(c, indic) && d[c] < d1) { d1 = d[c]; c1 = c; }
                float d2 = float.MaxValue;
                for (var c = 0; c < d.Length; c++) if (InScript(c, indic) && Value(c) != Value(c1) && d[c] < d2) d2 = d[c];
                pick.Add((c1, d2 <= 0 || d2 == float.MaxValue ? 0f : Math.Clamp(1 - d1 / d2, 0f, 1f)));
                total += d1;
            }
            return (pick, total);
        }
        var indicRead = Interpret(true); var arabicRead = Interpret(false);
        var isIndic = indicRead.Total < arabicRead.Total;
        var chosen = isIndic ? indicRead.Pick : arabicRead.Pick;
        if (isIndic)
        {
            // الصفر الهندي نقطة كالنقطة: يفصل بينهما الموضع (الصفر في وسط السطر، والنقطة على خط القاعدة).
            const int dot = 23, zero = 10;
            for (var i = 0; i < blobs.Count; i++)
            {
                if (chosen[i].Cls is not (dot or zero)) continue;
                var rel = (blobs[i].Box.MidY - top) / (double)height;
                var cls = rel < 0.8 ? zero : dot;
                var d = best[i];
                float d2 = float.MaxValue;
                for (var c = 0; c < d.Length; c++) if (InScript(c, true) && c is not (dot or zero) && d[c] < d2) d2 = d[c];
                var d1 = Math.Min(d[dot], d[zero]);
                chosen[i] = (cls, d2 == float.MaxValue ? 0f : Math.Clamp(1 - d1 / d2, 0f, 1f));
            }
        }
        // الخط نفسه موضع شك إن تقاربت المسافتان الكليتان.
        var scriptMargin = Math.Abs(indicRead.Total - arabicRead.Total) / Math.Max(1e-6f, Math.Max(indicRead.Total, arabicRead.Total));
        var glyphs = new List<DigitGlyph>();
        for (var i = 0; i < blobs.Count; i++) glyphs.Add(new DigitGlyph(Classes[chosen[i].Cls], chosen[i].Conf, blobs[i].Box));
        // الفراغ الواسع بين رمزين يفصل رمزين (رقمين، أو رقماً وكلمة).
        // والقوس حد للرقم أيضاً: «(٣٠) يوماً» قد تتقارب فيه الكلمة والقوس.
        var tokens = new List<DigitRead>();
        var start = 0;
        for (var i = 1; i <= glyphs.Count; i++)
        {
            var paren = i < glyphs.Count && (glyphs[i].Char is '(' or ')' || glyphs[i - 1].Char is '(' or ')');
            if (i < glyphs.Count && !paren && glyphs[i].Box.Left - glyphs[i - 1].Box.Right <= height * 0.9) continue;
            tokens.Add(Token(glyphs.GetRange(start, i - start), isIndic, scriptMargin));
            start = i;
        }
        for (var i = 0; i < tokens.Count; i++)
        {
            var outer = tokens[i].Box;
            if (i > 0 && tokens[i - 1].Glyphs is [{ Char: '(' or ')' }]) outer = SKRectI.Union(outer, tokens[i - 1].Box);
            if (i < tokens.Count - 1 && tokens[i + 1].Glyphs is [{ Char: '(' or ')' }]) outer = SKRectI.Union(outer, tokens[i + 1].Box);
            tokens[i] = tokens[i] with { OuterBox = outer };
        }
        return new DigitReading(tokens, isIndic);
    }

    private static DigitRead Token(List<DigitGlyph> glyphs, bool isIndic, float scriptMargin)
    {
        var box = glyphs.Select(x => x.Box).Aggregate(SKRectI.Union);
        // «هـ» والعلامات مقبولة في الطرفين فقط؛ الوسط يجب أن يكون أرقاماً وفواصل.
        int first = 0, last = glyphs.Count - 1;
        static bool Ignorable(char c) => c is 'ه' or '.' or ':' or '(' or ')' or '،';
        while (first <= last && Ignorable(glyphs[first].Char)) first++;
        while (last >= first && Ignorable(glyphs[last].Char)) last--;
        if (first > last) return new DigitRead("", 0, isIndic, false, false, glyphs, box);
        var core = glyphs.Skip(first).Take(last - first + 1).ToList();
        var complete = core.All(x => char.IsDigit(x.Char) || x.Char is '/' or '-') && char.IsDigit(core[0].Char) && char.IsDigit(core[^1].Char);
        var hijri = glyphs.Take(first).Any(x => x.Char == 'ه'); // «هـ» تقع يسار الرقم في السطر العربي
        var conf = core.Min(x => x.Confidence);
        // الدائرة «0» العربية و«٥» الهندية شكل واحد تقريباً: قيمتها تتبع الخط، فتُقيَّد ثقتها بيقين الخط.
        if (core.Any(x => x.Char is '0' or '٥')) conf = Math.Min(conf, Math.Clamp(scriptMargin * 4, 0, 1));
        return new DigitRead(new string(core.Select(x => x.Char).ToArray()), conf, isIndic, complete, hijri, glyphs, box);
    }

    /// <summary>عتبة الحبر داخل المنطقة: منتصف المسافة بين الحبر والورق؛ -1 إن لم يكن فيها تباين كافٍ.</summary>
    private static int LocalThreshold(ImagePrep.Gray g, SKRectI r)
    {
        var hist = new int[256];
        for (var y = r.Top; y < r.Bottom; y++)
            for (var x = r.Left; x < r.Right; x++) hist[g.Pixels[y * g.Width + x]]++;
        long n = (long)r.Width * r.Height, acc = 0;
        int lo = 0, hi = 255;
        for (var v = 0; v < 256; v++) { acc += hist[v]; if (acc >= n * 0.02) { lo = v; break; } }
        acc = 0;
        for (var v = 255; v >= 0; v--) { acc += hist[v]; if (acc >= n * 0.5) { hi = v; break; } }
        return hi - lo < 50 ? -1 : (lo + hi) / 2;
    }
}
