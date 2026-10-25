using SkiaSharp;

namespace Basira.Platform.Processing.Ocr;

/// <summary>
/// تقرير جودة الصورة الممسوحة قبل القراءة. المقاييس فيزيائية ليفهمها المستخدم:
/// الدقة بالنقطة/بوصة، وعرض الحافة بنقاط 300 (حافة الحرف الواضح نحو نقطة)، والتباين بين الحبر والورق، والتشويش.
/// </summary>
public sealed record ScanQuality(int EstimatedDpi, double EdgeWidth, int Contrast, double Noise, double SkewDegrees, double InkRatio, List<string> Issues)
{
    public bool Acceptable => Issues.Count == 0;
}

/// <summary>
/// تحسين الصورة قبل OCR: تسوية الإضاءة (قسمة على خلفية محلية مقدّرة)، وإزالة التشويش عند الحاجة،
/// ومد التباين من مستوى الحبر إلى الورق، وتصحيح الميلان، والتكبير إلى ما يعادل 300 نقطة/بوصة.
/// تعمل على صيغ PNG وJPEG وWebP وBMP وGIF؛ ما عداها (مثل TIFF) يُمرَّر لـ Tesseract كما هو.
/// </summary>
public static class ImagePrep
{
    public const int TargetDpi = 300;

    // حدود بوابة الجودة. مُعايرة على مجموعة القياس الاصطناعية؛ تحتاج تأكيداً على مسوحات الجهة الفعلية.
    // الدقة هنا «فعلية»: تُقدَّر من حجم الحروف بالبكسل، فتصلح لصور الجوال والمقتطعات لا لمسح A4 وحده.
    public const int MinDpi = 130;
    public const double MaxEdgeWidth = 3.0;
    public const int MinContrast = 70;
    public const double MaxNoise = 14;
    public const double MaxSkew = 8;

    public sealed record Gray(byte[] Pixels, int Width, int Height);

    /// <summary>صورة محلَّلة: الأصل بعد تسوية الإضاءة مع قياساتها، جاهزة للكتابة.</summary>
    /// <summary>PageDpi: دقة الصفحة من عرضها إن كانت بأبعاد A4 (مسح صفحة كاملة)، وإلا 0.</summary>
    public sealed record Analysis(Gray Normalized, int InkLevel, int PaperLevel, double Skew, ScanQuality Quality, int PageDpi);

    public static Gray? Load(string path)
    {
        using var bmp = SKBitmap.Decode(path);
        if (bmp is null) return null;
        // التحويل إلى رمادي يدوياً: الرسم على لوحة Gray8 غير مضمون في كل منصات Skia.
        using var rgba = bmp.ColorType == SKColorType.Rgba8888 ? null : bmp.Copy(SKColorType.Rgba8888);
        var src = rgba ?? bmp;
        var bytes = src.Bytes;
        var px = new byte[src.Width * src.Height];
        for (var y = 0; y < src.Height; y++)
        {
            var row = y * src.RowBytes;
            for (var x = 0; x < src.Width; x++)
            {
                var i = row + x * 4;
                var a = bytes[i + 3];
                var lum = (bytes[i] * 299 + bytes[i + 1] * 587 + bytes[i + 2] * 114) / 1000;
                px[y * src.Width + x] = (byte)((lum * a + 255 * (255 - a)) / 255); // الشفافية على خلفية بيضاء
            }
        }
        return new Gray(px, src.Width, src.Height);
    }

    /// <summary>ارتفاع مكونات الحبر الوسيط عند 300 نقطة/بوصة لنص الخطابات المعتاد (12-14 نقطة)؛ معايَر على مجموعة القياس.</summary>
    private const double TextHeightAt300 = 34;

    public static Analysis Analyze(Gray g)
    {
        var norm = NormalizeBackground(g);
        var hist = new int[256];
        for (var i = 0; i < norm.Pixels.Length; i += 3) hist[norm.Pixels[i]]++;
        // الحبر أقلية في الصفحة: الورق هو الوسيط، وتشويشه الانحراف المطلق الوسيط. الحبر ما يقل عن الورق بفارق واضح.
        // (Otsu يفشل هنا: مع 1% حبر وتشويش مرتفع يقسم التشويش لا الحبر.)
        var paper = Percentile(hist, 0.5);
        var mad = new int[256];
        for (var v = 0; v < 256; v++) mad[Math.Abs(v - paper)] += hist[v];
        var noise = 1.4826 * Percentile(mad, 0.5);
        var cut = (int)Math.Round(paper - Math.Max(4 * noise, 30));
        long inkCount = 0, total = 0;
        for (var v = 0; v < 256; v++) { total += hist[v]; if (v <= cut) inkCount += hist[v]; }
        var inkRatio = total == 0 ? 0 : (double)inkCount / total;
        var ink = cut < 0 || inkCount == 0 ? paper : Median(hist, 0, Math.Max(0, cut));
        var contrast = paper - ink;
        // الدقة الفعلية للقراءة: حجم الحروف بالبكسل لا مقاس الورقة (صورة جوال أو مقتطع لا يملآن عرض A4).
        var textHeight = inkRatio < 0.002 ? 0 : MedianComponentHeight(norm, cut);
        var dpi = textHeight > 0 ? (int)Math.Round(300 * textHeight / TextHeightAt300) : (int)Math.Round(g.Width / (g.Width > g.Height ? 11.69 : 8.27));
        var edge = inkRatio < 0.002 ? 0 : EdgeWidth(norm, ink, paper) * TargetDpi / Math.Max(50, dpi);
        var skew = EstimateSkew(norm, Math.Max(0, cut));
        var issues = new List<string>();
        if (inkRatio < 0.002) issues.Add("الصفحة تبدو فارغة");
        else
        {
            if (dpi < MinDpi) issues.Add($"دقة المسح منخفضة (نحو {dpi} نقطة/بوصة)؛ امسح بدقة 300");
            if (edge > MaxEdgeWidth) issues.Add("الحروف غير حادة (الصورة مهتزة أو خارج التركيز أو مصغّرة)");
            if (contrast < MinContrast) issues.Add("التباين ضعيف بين الحبر والورق (حبر باهت أو خلفية داكنة)");
            if (noise > MaxNoise) issues.Add("تشويش مرتفع في الخلفية (ضغط شديد أو إضاءة ضعيفة)");
            if (Math.Abs(skew) > MaxSkew) issues.Add($"الورقة مائلة بشدة ({Math.Abs(skew):0}°)");
        }
        var q = new ScanQuality(dpi, Math.Round(edge, 2), contrast, Math.Round(noise, 1), skew, Math.Round(inkRatio, 4), issues);
        var ratio = (double)Math.Max(g.Width, g.Height) / Math.Max(1, Math.Min(g.Width, g.Height));
        var pageDpi = Math.Abs(ratio - 1.414) < 0.12 ? (int)Math.Round(g.Width / (g.Width > g.Height ? 11.69 : 8.27)) : 0;
        return new Analysis(norm, ink, paper, skew, q, pageDpi);
    }

    /// <summary>يكتب الصورة المحسّنة للقراءة (PNG رمادي) ويعيدها في الذاكرة لمراحل القراءة التالية.</summary>
    public static Gray Prepare(Analysis a, string outPath)
    {
        var g = a.Normalized;
        var px = a.Quality.Noise > 6 ? Median3(g) : (byte[])g.Pixels.Clone();
        // مد التباين: مستوى الحبر الوسيط ← أسود، والورق ← أبيض (فيختفي نصف تشويش الخلفية).
        var ink = Math.Min(a.InkLevel, a.PaperLevel - 30);
        var white = a.PaperLevel;
        if (ink > 8 || white < 250)
        {
            var lut = new byte[256];
            for (var v = 0; v < 256; v++) lut[v] = (byte)Math.Clamp((v - ink) * 255 / Math.Max(1, white - ink), 0, 255);
            for (var i = 0; i < px.Length; i++) px[i] = lut[px[i]];
        }
        // التكبير إلى ما يعادل 300 نقطة/بوصة: من عرض الصفحة إن كانت مسحاً لصفحة A4 كاملة، وإلا من حجم الحروف
        // (صورة جوال أو مقتطع)، وحينها للنص الصغير فقط.
        var scale = a.PageDpi > 0 ? Math.Clamp((double)TargetDpi / a.PageDpi, 1.0, 3.0)
            : a.Quality.EstimatedDpi >= 200 ? 1.0 : Math.Clamp(260.0 / Math.Max(50, a.Quality.EstimatedDpi), 1.0, 3.0);
        using var src = ToBitmap(new Gray(px, g.Width, g.Height));
        var w = (int)Math.Round(g.Width * scale); var h = (int)Math.Round(g.Height * scale);
        using var dst = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(dst))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.Low, IsAntialias = true })
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(w / 2f, h / 2f);
            if (Math.Abs(a.Skew) >= 0.2) canvas.RotateDegrees((float)-a.Skew);
            canvas.Scale((float)scale);
            canvas.DrawBitmap(src, -g.Width / 2f, -g.Height / 2f, paint);
        }
        var outPx = new byte[w * h];
        var rgba = dst.Bytes;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) outPx[y * w + x] = rgba[y * dst.RowBytes + x * 4];
        Save(new Gray(outPx, w, h), outPath);
        return new Gray(outPx, w, h);
    }

    /// <summary>PNG رمادي بضغط سريع: الصورة المؤقتة تُقرأ مرة واحدة، والضغط الأقصى يضاعف الزمن على الصور المشوشة.</summary>
    public static void Save(Gray g, string path)
    {
        using var bmp = new SKBitmap(new SKImageInfo(g.Width, g.Height, SKColorType.Gray8, SKAlphaType.Opaque));
        var ptr = bmp.GetPixels();
        for (var y = 0; y < g.Height; y++) System.Runtime.InteropServices.Marshal.Copy(g.Pixels, y * g.Width, ptr + y * bmp.RowBytes, g.Width);
        using var pixmap = bmp.PeekPixels();
        using var data = pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 1));
        using var fs = File.Create(path);
        data.SaveTo(fs);
    }

    /// <summary>مقتطع من صورة (لعرض الحقل على المراجع أو لإعادة قراءته) مع هامش، بتكبير اختياري.</summary>
    public static byte[] Crop(string imagePath, SKRectI box, int pad, double zoom = 1.0)
    {
        using var bmp = SKBitmap.Decode(imagePath) ?? throw new InvalidDataException("تعذر فتح الصورة.");
        var r = SKRectI.Intersect(new SKRectI(box.Left - pad, box.Top - pad, box.Right + pad, box.Bottom + pad), new SKRectI(0, 0, bmp.Width, bmp.Height));
        if (r.Width <= 0 || r.Height <= 0) throw new ArgumentException("منطقة القص خارج الصورة.");
        var w = Math.Max(1, (int)(r.Width * zoom)); var h = Math.Max(1, (int)(r.Height * zoom));
        using var dst = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(dst))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(bmp, r, new SKRect(0, 0, w, h), paint);
        }
        using var image = SKImage.FromBitmap(dst);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // ===== التفاصيل =====

    private static SKBitmap ToBitmap(Gray g)
    {
        var bmp = new SKBitmap(new SKImageInfo(g.Width, g.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var buf = new byte[g.Width * 4];
        var ptr = bmp.GetPixels();
        for (var y = 0; y < g.Height; y++)
        {
            for (var x = 0; x < g.Width; x++)
            {
                var v = g.Pixels[y * g.Width + x];
                buf[x * 4] = v; buf[x * 4 + 1] = v; buf[x * 4 + 2] = v; buf[x * 4 + 3] = 255;
            }
            System.Runtime.InteropServices.Marshal.Copy(buf, 0, ptr + y * bmp.RowBytes, buf.Length);
        }
        return bmp;
    }

    /// <summary>
    /// تسوية الإضاءة: لكل مربع يُقدَّر لون الورق بالمئين 90 (الحبر قليل في أي مربع)، ثم يُنعَّم الحقل ويُقسم عليه كل بكسل.
    /// يزيل التدرج والظل وصفرة الورق دون المساس بالحبر.
    /// </summary>
    private static Gray NormalizeBackground(Gray g)
    {
        var b = Math.Clamp(g.Width / 32, 16, 128);
        int bw = (g.Width + b - 1) / b, bh = (g.Height + b - 1) / b;
        var bg = new double[bw * bh];
        var hist = new int[256];
        for (var by = 0; by < bh; by++)
            for (var bx = 0; bx < bw; bx++)
            {
                Array.Clear(hist);
                var n = 0;
                for (var y = by * b; y < Math.Min(g.Height, (by + 1) * b); y += 2)
                    for (var x = bx * b; x < Math.Min(g.Width, (bx + 1) * b); x += 2) { hist[g.Pixels[y * g.Width + x]]++; n++; }
                int acc = 0, v = 255;
                for (; v > 0; v--) { acc += hist[v]; if (acc >= n * 0.10) break; }
                bg[by * bw + bx] = Math.Max(v, 40);
            }
        // مربع يغلب عليه الحبر (شعار، عنوان عريض) يأخذ أعلى جيرانه، ثم تنعيم.
        var dil = new double[bg.Length];
        for (var by = 0; by < bh; by++)
            for (var bx = 0; bx < bw; bx++)
            {
                double m = 0;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int yy = by + dy, xx = bx + dx;
                        if (yy >= 0 && yy < bh && xx >= 0 && xx < bw) m = Math.Max(m, bg[yy * bw + xx]);
                    }
                dil[by * bw + bx] = m;
            }
        var smooth = new double[bg.Length];
        for (var by = 0; by < bh; by++)
            for (var bx = 0; bx < bw; bx++)
            {
                double s = 0; var c = 0;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int yy = by + dy, xx = bx + dx;
                        if (yy >= 0 && yy < bh && xx >= 0 && xx < bw) { s += dil[yy * bw + xx]; c++; }
                    }
                smooth[by * bw + bx] = s / c;
            }
        var outPx = new byte[g.Pixels.Length];
        Parallel.For(0, g.Height, y =>
        {
            var fy = Math.Clamp((y + 0.5) / b - 0.5, 0, bh - 1);
            int y0 = (int)fy, y1 = Math.Min(bh - 1, y0 + 1); var ty = fy - y0;
            for (var x = 0; x < g.Width; x++)
            {
                var fx = Math.Clamp((x + 0.5) / b - 0.5, 0, bw - 1);
                int x0 = (int)fx, x1 = Math.Min(bw - 1, x0 + 1); var tx = fx - x0;
                var v = (smooth[y0 * bw + x0] * (1 - tx) + smooth[y0 * bw + x1] * tx) * (1 - ty) + (smooth[y1 * bw + x0] * (1 - tx) + smooth[y1 * bw + x1] * tx) * ty;
                outPx[y * g.Width + x] = (byte)Math.Clamp(g.Pixels[y * g.Width + x] * 255.0 / v, 0, 255);
            }
        });
        return new Gray(outPx, g.Width, g.Height);
    }

    private static int Median(int[] hist, int from, int to)
    {
        long n = 0; for (var v = from; v <= to; v++) n += hist[v];
        long acc = 0;
        for (var v = from; v <= to; v++) { acc += hist[v]; if (acc * 2 >= n) return v; }
        return to;
    }

    /// <summary>الارتفاع الوسيط لمكونات الحبر المتصلة (أجزاء الكلمات والأرقام)، مع استبعاد النقاط والتشويش.</summary>
    public static double MedianComponentHeight(Gray g, int cut)
    {
        // تصغير إلى عرض 1600 بحد أقصى لسرعة التقطيع، ثم إعادة القيمة إلى مقياس الصورة.
        var f = Math.Max(1, (int)Math.Ceiling(g.Width / 1600.0));
        int w = g.Width / f, h = g.Height / f;
        var ink = new bool[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var dark = 0;
                for (var dy = 0; dy < f; dy++) for (var dx = 0; dx < f; dx++) if (g.Pixels[(y * f + dy) * g.Width + x * f + dx] <= cut) dark++;
                ink[y * w + x] = dark * 2 >= f * f;
            }
        var seen = new bool[w * h];
        var heights = new List<int>();
        var stack = new Stack<int>();
        for (var i = 0; i < ink.Length; i++)
        {
            if (!ink[i] || seen[i]) continue;
            int top = int.MaxValue, bottom = 0, left = int.MaxValue, right = 0, n = 0;
            seen[i] = true; stack.Push(i);
            while (stack.Count > 0)
            {
                var p = stack.Pop(); n++;
                int py = p / w, px = p % w;
                top = Math.Min(top, py); bottom = Math.Max(bottom, py); left = Math.Min(left, px); right = Math.Max(right, px);
                if (px > 0 && ink[p - 1] && !seen[p - 1]) { seen[p - 1] = true; stack.Push(p - 1); }
                if (px < w - 1 && ink[p + 1] && !seen[p + 1]) { seen[p + 1] = true; stack.Push(p + 1); }
                if (py > 0 && ink[p - w] && !seen[p - w]) { seen[p - w] = true; stack.Push(p - w); }
                if (py < h - 1 && ink[p + w] && !seen[p + w]) { seen[p + w] = true; stack.Push(p + w); }
            }
            int ch = bottom - top + 1, cw = right - left + 1;
            // خطوط الجداول والإطارات والشعارات الكبيرة ليست نصاً.
            if (n >= 6 && ch >= 3 && ch < h / 8 && cw < w / 3) heights.Add(ch);
        }
        if (heights.Count < 30) return 0;
        heights.Sort();
        // المكونات الأطول من الوسيط (أجزاء الكلمات لا النقاط والتشكيل): وسيط النصف الأعلى.
        var upper = heights.Skip(heights.Count / 2).ToList();
        return upper[upper.Count / 2] * (double)f;
    }

    private static int Percentile(int[] hist, double p)
    {
        long n = 0; foreach (var c in hist) n += c;
        long acc = 0;
        for (var v = 0; v < hist.Length; v++) { acc += hist[v]; if (acc >= n * p) return v; }
        return hist.Length - 1;
    }

    /// <summary>
    /// عرض الحافة بالبكسل: المسافة الوسيطة التي ينتقل فيها الصف من 80% الورق إلى 20% الحبر، على صفوف منعّمة أفقياً قليلاً.
    /// حافة الحرف المطبوع الواضح نحو بكسل واحد عند 300 نقطة/بوصة، ويتسع مع الاهتزاز والتصغير.
    /// </summary>
    private static double EdgeWidth(Gray g, int ink, double paper)
    {
        var hi = ink + (paper - ink) * 0.8;
        var lo = ink + (paper - ink) * 0.2;
        var widths = new List<double>();
        var row = new double[g.Width];
        for (var y = 1; y < g.Height - 1 && widths.Count < 40000; y += 3)
        {
            for (var x = 0; x < g.Width; x++)
                row[x] = (g.Pixels[(y - 1) * g.Width + x] + 2 * g.Pixels[y * g.Width + x] + g.Pixels[(y + 1) * g.Width + x]) / 4.0;
            var lastHigh = -1;
            for (var x = 0; x < g.Width; x++)
            {
                if (row[x] >= hi) lastHigh = x;
                else if (row[x] <= lo && lastHigh >= 0)
                {
                    // موضعا العبور بدقة دون البكسل (استيفاء خطي).
                    var xHi = lastHigh + (row[lastHigh] - hi) / Math.Max(1e-6, row[lastHigh] - row[lastHigh + 1]);
                    var xLo = x - 1 + (row[x - 1] - lo) / Math.Max(1e-6, row[x - 1] - row[x]);
                    var w = xLo - xHi;
                    if (w > 0 && w <= 16) widths.Add(w);
                    lastHigh = -1;
                }
            }
        }
        if (widths.Count < 50) return 0;
        widths.Sort();
        return widths[widths.Count / 2];
    }

    /// <summary>مرشح الوسيط 3×3 بشبكة مقارنات ثابتة (يزيل نقاط التشويش ويحفظ حواف الحروف).</summary>
    private static byte[] Median3(Gray g)
    {
        var o = (byte[])g.Pixels.Clone();
        var p = g.Pixels; var W = g.Width;
        Parallel.For(1, g.Height - 1, y =>
        {
            for (var x = 1; x < W - 1; x++)
            {
                var i = y * W + x;
                int p0 = p[i - W - 1], p1 = p[i - W], p2 = p[i - W + 1], p3 = p[i - 1], p4 = p[i], p5 = p[i + 1], p6 = p[i + W - 1], p7 = p[i + W], p8 = p[i + W + 1];
                void S(ref int a, ref int b) { if (a > b) (a, b) = (b, a); }
                S(ref p1, ref p2); S(ref p4, ref p5); S(ref p7, ref p8); S(ref p0, ref p1); S(ref p3, ref p4); S(ref p6, ref p7);
                S(ref p1, ref p2); S(ref p4, ref p5); S(ref p7, ref p8); S(ref p0, ref p3); S(ref p5, ref p8); S(ref p4, ref p7);
                S(ref p3, ref p6); S(ref p1, ref p4); S(ref p2, ref p5); S(ref p4, ref p7); S(ref p4, ref p2); S(ref p6, ref p4);
                S(ref p4, ref p2);
                o[i] = (byte)p4;
            }
        });
        return o;
    }

    private static Gray Downscale(Gray g, int width)
    {
        if (g.Width <= width) return g;
        var h = (int)((long)g.Height * width / g.Width);
        var outPx = new byte[width * h];
        for (var y = 0; y < h; y++)
        {
            var sy = (int)((long)y * g.Height / h);
            for (var x = 0; x < width; x++) outPx[y * width + x] = g.Pixels[sy * g.Width + (int)((long)x * g.Width / width)];
        }
        return new Gray(outPx, width, h);
    }

    /// <summary>زاوية الميلان: الزاوية التي تجعل مسقط نقاط الحبر على المحور الرأسي أكثر حدة (أسطر أفقية).</summary>
    public static double EstimateSkew(Gray g, int threshold)
    {
        var s = Downscale(g, 900);
        var ink = new List<(int X, int Y)>();
        for (var y = 0; y < s.Height; y++)
            for (var x = 0; x < s.Width; x += 2)
                if (s.Pixels[y * s.Width + x] <= threshold) ink.Add((x, y));
        if (ink.Count < 200) return 0;
        if (ink.Count > 60000) ink = ink.Where((_, i) => i % (ink.Count / 60000 + 1) == 0).ToList();
        double Score(double deg)
        {
            var t = Math.Tan(deg * Math.PI / 180);
            var bins = new int[s.Height + s.Width + 2];
            var offset = s.Width;
            foreach (var (x, y) in ink)
            {
                var b = (int)Math.Round(y - x * t) + offset;
                if (b >= 0 && b < bins.Length) bins[b]++;
            }
            double sq = 0; foreach (var c in bins) sq += (double)c * c;
            return sq;
        }
        double best = 0, bestScore = double.MinValue;
        for (var a = -10.0; a <= 10.0; a += 0.5) { var sc = Score(a); if (sc > bestScore) { bestScore = sc; best = a; } }
        var center = best;
        for (var a = center - 0.5; a <= center + 0.5; a += 0.1) { var sc = Score(a); if (sc > bestScore) { bestScore = sc; best = a; } }
        return Math.Round(best, 1);
    }
}
