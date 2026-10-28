using SkiaSharp;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace Basira.Platform.Processing.Scanning;

/// <summary>صفحة جاهزة للمراجعة: صورتها النهائية (JPEG للون والرمادي، PNG للأبيض والأسود)، ودقتها، ودورانها، وهل تبدو فارغة.</summary>
public sealed class ScanPage
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..12];
    public required byte[] Data { get; init; }
    public required bool Jpeg { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Dpi { get; init; }
    public required bool Blank { get; init; }
    public required byte[] Thumb { get; init; }
    public int Rotation { get; set; }
}

/// <summary>معالجة الصفحات الممسوحة: نمط اللون، والسطوع والتباين إن لم يدعمهما الجهاز، وكشف الصفحات الفارغة، والتدوير، وجمعها في PDF.</summary>
public static class PageTools
{
    /// <summary>تحويل صورة الجهاز إلى صفحة. adjust: تطبيق السطوع والتباين هنا لأن الجهاز لا يدعمهما.</summary>
    public static ScanPage Prepare(ScannedImage img, string colorMode, int brightness, int contrast, bool adjust)
    {
        using var decoded = SKBitmap.Decode(img.Data) ?? throw new ScannerException("أعاد الماسح صورة لا يمكن قراءتها.");
        var gray = colorMode != "color";
        using var work = gray ? ToGray(decoded) : decoded.Copy(SKColorType.Rgba8888);
        if (adjust && (brightness != 0 || contrast != 0)) Adjust(work, brightness, contrast);
        var blank = IsBlank(work);
        byte[] data;
        var jpeg = colorMode != "bw";
        if (colorMode == "bw")
        {
            Threshold(work, Otsu(work));
            data = Encode(work, SKEncodedImageFormat.Png, 100);
        }
        else data = Encode(work, SKEncodedImageFormat.Jpeg, 85);
        return new ScanPage { Data = data, Jpeg = jpeg, Width = work.Width, Height = work.Height, Dpi = img.Dpi > 0 ? img.Dpi : 300, Blank = blank, Thumb = Thumbnail(work, 360) };
    }

    private static byte[] Encode(SKBitmap b, SKEncodedImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(b);
        using var encoded = image.Encode(format, quality);
        return encoded.ToArray();
    }

    public static SKBitmap ToGray(SKBitmap src)
    {
        var dst = new SKBitmap(new SKImageInfo(src.Width, src.Height, SKColorType.Gray8, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }

    /// <summary>السطوع (-100..100) يزيح القيم، والتباين يمدها حول المنتصف.</summary>
    public static void Adjust(SKBitmap b, int brightness, int contrast)
    {
        var factor = contrast >= 0 ? 1 + contrast / 50.0 : 1 + contrast / 110.0;
        var lut = new byte[256];
        for (var i = 0; i < 256; i++) lut[i] = (byte)Math.Clamp((i - 128) * factor + 128 + brightness * 1.28, 0, 255);
        var px = b.GetPixelSpan();
        var bytes = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(px), px.Length);
        if (b.ColorType == SKColorType.Gray8) { for (var i = 0; i < bytes.Length; i++) bytes[i] = lut[bytes[i]]; return; }
        for (var i = 0; i < bytes.Length; i += 4) { bytes[i] = lut[bytes[i]]; bytes[i + 1] = lut[bytes[i + 1]]; bytes[i + 2] = lut[bytes[i + 2]]; }
    }

    /// <summary>عتبة Otsu لصورة رمادية: تفصل الحبر عن الورق تلقائياً بحسب الصفحة.</summary>
    public static int Otsu(SKBitmap gray)
    {
        var hist = new long[256];
        foreach (var v in gray.GetPixelSpan()) hist[v]++;
        long total = gray.Width * (long)gray.Height, sumAll = 0;
        for (var i = 0; i < 256; i++) sumAll += i * hist[i];
        long wB = 0, sumB = 0;
        double best = -1;
        var threshold = 160;
        for (var t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            var wF = total - wB;
            if (wF == 0) break;
            sumB += t * hist[t];
            double mB = sumB / (double)wB, mF = (sumAll - sumB) / (double)wF;
            var between = wB * (double)wF * (mB - mF) * (mB - mF);
            if (between > best) { best = between; threshold = t; }
        }
        // صفحة بيضاء تقريباً: لا تُسوَّد الضوضاء.
        return Math.Clamp(threshold, 90, 200);
    }

    private static void Threshold(SKBitmap gray, int t)
    {
        var px = gray.GetPixelSpan();
        var bytes = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(px), px.Length);
        for (var i = 0; i < bytes.Length; i++) bytes[i] = bytes[i] < t ? (byte)0 : (byte)255;
    }

    /// <summary>
    /// صفحة فارغة: نسبة الحبر في داخلها (بعد استبعاد هامش 4% يحوي ظلال الحواف وثقوب الملفات) دون 0.15%.
    /// تُعدّ بالدقة الكاملة: تصغير الصورة أولاً يبهت الخطوط الرفيعة فتبدو صفحة مكتوبة فارغة.
    /// </summary>
    public static bool IsBlank(SKBitmap gray)
    {
        if (gray.ColorType != SKColorType.Gray8) { using var g = ToGray(gray); return IsBlank(g); }
        int mx = gray.Width * 4 / 100, my = gray.Height * 4 / 100;
        long dark = 0, count = 0;
        var px = gray.GetPixelSpan();
        for (var y = my; y < gray.Height - my; y++)
        {
            var row = px.Slice(y * gray.RowBytes, gray.Width);
            for (var x = mx; x < gray.Width - mx; x++) if (row[x] < 110) dark++;
            count += gray.Width - 2 * mx;
        }
        return count > 0 && dark / (double)count < 0.0015;
    }

    public static byte[] Thumbnail(SKBitmap b, int width)
    {
        var w = Math.Min(width, b.Width);
        var h = Math.Max(1, (int)Math.Round(b.Height * (w / (double)b.Width)));
        using var small = b.Resize(new SKImageInfo(w, h, b.ColorType == SKColorType.Gray8 ? SKColorType.Gray8 : SKColorType.Rgba8888, SKAlphaType.Opaque), SKFilterQuality.Medium)
            ?? throw new ScannerException("تعذر تصغير الصفحة.");
        return Encode(small, SKEncodedImageFormat.Jpeg, 80);
    }

    /// <summary>صورة الصفحة بعد تدويرها (90 أو 180 أو 270 مع عقارب الساعة).</summary>
    public static (byte[] Data, int Width, int Height) Rotated(ScanPage p)
    {
        if (p.Rotation % 360 == 0) return (p.Data, p.Width, p.Height);
        using var src = SKBitmap.Decode(p.Data) ?? throw new ScannerException("تعذر قراءة الصفحة.");
        var quarter = p.Rotation % 180 != 0;
        using var dst = new SKBitmap(new SKImageInfo(quarter ? src.Height : src.Width, quarter ? src.Width : src.Height, src.ColorType, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(dst))
        {
            canvas.Translate(dst.Width / 2f, dst.Height / 2f);
            canvas.RotateDegrees(p.Rotation);
            canvas.Translate(-src.Width / 2f, -src.Height / 2f);
            canvas.DrawBitmap(src, 0, 0);
        }
        return (Encode(dst, p.Jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, p.Jpeg ? 85 : 100), dst.Width, dst.Height);
    }

    /// <summary>جمع الصفحات في ملف PDF بمقاسها الحقيقي (من دقة المسح)، فيقرؤه خط المعالجة كأي PDF ممسوح.</summary>
    public static byte[] BuildPdf(IEnumerable<ScanPage> pages)
    {
        var builder = new PdfDocumentBuilder();
        foreach (var p in pages)
        {
            var (data, w, h) = Rotated(p);
            double pw = w * 72.0 / p.Dpi, ph = h * 72.0 / p.Dpi;
            var page = builder.AddPage(pw, ph);
            var rect = new PdfRectangle(0, 0, pw, ph);
            if (p.Jpeg) page.AddJpeg(data, rect); else page.AddPng(data, rect);
        }
        return builder.Build();
    }
}
