using Basira.Platform.Processing.Ocr;
using SkiaSharp;

// يولّد قوالب قارئ الأرقام من خطوط TrueType: كل رمز يُرسم بين أرقام مرجعية بعدة أحجام وتشويهات،
// ثم يمر بالتقطيع والسمات نفسها التي تستخدمها المنصة عند القراءة.
// dotnet run --project tools/DigitTemplates -- <مجلد الخطوط> <ملف الإخراج digits.bin.gz>
if (args.Length < 2) { Console.Error.WriteLine("الاستخدام: <مجلد الخطوط> <ملف الإخراج>"); return 1; }
var fonts = Directory.GetFiles(args[0], "*.ttf").OrderBy(f => f).ToList();
var output = args[1];
var templates = new List<(int, byte[])>();
var sizes = new[] { 26f, 36f, 48f, 64f };
var augments = new[] { "plain", "blur", "bold", "lowres", "blur-bold" };
const string presentationHeh = "ﻫـ"; // «هـ»: هاء أولية + تطويل (الرسم دون تشكيل نصي)
foreach (var path in fonts)
{
    using var tf = SKTypeface.FromFile(path);
    if (tf is null) { Console.Error.WriteLine("تعذر فتح " + path); continue; }
    var added = 0;
    foreach (var indic in new[] { true, false })
    {
        var reference = indic ? "٢٣٤" : "234";
        for (var cls = 0; cls < DigitReader.Classes.Length; cls++)
        {
            var c = DigitReader.Classes[cls];
            if (cls < 10 && indic || cls is >= 10 and < 20 && !indic) continue;
            var glyph = c == 'ه' ? presentationHeh : c.ToString();
            foreach (var size in sizes)
            {
                using var font = new SKFont(tf, size);
                if (!tf.ContainsGlyphs(reference + glyph)) continue;
                foreach (var aug in augments)
                {
                    var f = Render(font, reference + "  " + glyph + "  " + reference, aug, size, reference.Length);
                    if (f is null) continue;
                    templates.Add((cls, f)); added++;
                }
            }
        }
    }
    Console.WriteLine($"{Path.GetFileName(path),-36} {added}");
}
using (var fs = File.Create(output)) DigitReader.WriteTemplates(fs, templates);
Console.WriteLine($"القوالب: {templates.Count} → {output} ({new FileInfo(output).Length / 1024} ك.ب)");
return 0;

static byte[]? Render(SKFont font, string text, string aug, float size, int refCount)
{
    var width = (int)(text.Length * size * 1.1f + size * 2);
    var height = (int)(size * 2.2f);
    var scale = aug == "lowres" ? 0.45f : 1f;
    using var bmp = new SKBitmap(new SKImageInfo((int)(width * scale), (int)(height * scale), SKColorType.Rgba8888, SKAlphaType.Opaque));
    using (var canvas = new SKCanvas(bmp))
    using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
    {
        canvas.Clear(SKColors.White);
        canvas.Scale(scale);
        if (aug.Contains("bold")) { paint.Style = SKPaintStyle.StrokeAndFill; paint.StrokeWidth = size * 0.035f; }
        if (aug.Contains("blur")) paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, size * 0.03f);
        canvas.DrawText(text, size, size * 1.45f, font, paint);
    }
    using var big = scale == 1f ? null : bmp.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque), SKFilterQuality.Medium);
    var src = big ?? bmp;
    var px = new byte[src.Width * src.Height];
    var bytes = src.Bytes;
    for (var y = 0; y < src.Height; y++)
        for (var x = 0; x < src.Width; x++)
        {
            var i = y * src.RowBytes + x * 4;
            px[y * src.Width + x] = (byte)((bytes[i] * 299 + bytes[i + 1] * 587 + bytes[i + 2] * 114) / 1000);
        }
    var blobs = DigitReader.Segment(px, src.Width, src.Height, new SKRectI(0, 0, src.Width, src.Height), 128);
    if (blobs.Count != refCount * 2 + 1) return null; // الرمز انقسم أو التصق بجاره
    var (top, h) = DigitReader.Band(blobs);
    return DigitReader.Features(blobs[refCount], src.Width, top, h);
}
