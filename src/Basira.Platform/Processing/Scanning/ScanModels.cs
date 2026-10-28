namespace Basira.Platform.Processing.Scanning;

/// <summary>ماسح ضوئي: Driver «wia» (تعريف Windows) أو «escl» (ماسح شبكي بلا تعريف عبر eSCL/AirScan).</summary>
public sealed record ScannerDevice(string Id, string Name, string Driver, string Address = "");

/// <summary>
/// قدرات الماسح كما يعلنها: المصادر (flatbed، feeder، duplex)، وأنماط اللون (color، gray، bw)، ودرجات الدقة،
/// وأقصى مساحة مسح بالمليمتر، ودعم السطوع والتباين في الجهاز نفسه (وإلا طُبّقا على الصورة بعد المسح).
/// </summary>
public sealed record ScannerCaps(string Model, List<string> Sources, List<string> ColorModes, List<int> Resolutions,
    double MaxWidthMm, double MaxHeightMm, bool DeviceBrightness, bool DeviceContrast, bool NativeDialog = false);

/// <summary>
/// إعدادات المسح. المنطقة بالمليمتر من الزاوية العليا اليسرى للزجاج؛ عرضها وارتفاعها صفر يعني مقاس الورقة PageSize.
/// السطوع والتباين من -100 إلى 100. MaxPages صفر يعني كل ما في وحدة التغذية.
/// </summary>
public sealed record ScanSettings(
    string Source = "flatbed", string ColorMode = "gray", int Dpi = 300, string PageSize = "A4",
    double LeftMm = 0, double TopMm = 0, double WidthMm = 0, double HeightMm = 0,
    int Brightness = 0, int Contrast = 0, int MaxPages = 0)
{
    public static readonly string[] Sources = { "flatbed", "feeder", "duplex" };
    public static readonly string[] ColorModes = { "color", "gray", "bw" };

    public static readonly Dictionary<string, (double W, double H)> Sizes = new()
    {
        ["A4"] = (210, 297), ["A5"] = (148, 210), ["A3"] = (297, 420), ["B5"] = (176, 250),
        ["Letter"] = (215.9, 279.4), ["Legal"] = (215.9, 355.6),
    };

    /// <summary>المنطقة الفعلية بالمليمتر بعد قصها على حدود الجهاز.</summary>
    public (double Left, double Top, double Width, double Height) Region(ScannerCaps caps)
    {
        double w, h;
        if (WidthMm > 0 && HeightMm > 0) (w, h) = (WidthMm, HeightMm);
        else if (Sizes.TryGetValue(PageSize, out var size)) (w, h) = size;
        else (w, h) = (caps.MaxWidthMm, caps.MaxHeightMm);
        var left = Math.Clamp(LeftMm, 0, Math.Max(0, caps.MaxWidthMm - 10));
        var top = Math.Clamp(TopMm, 0, Math.Max(0, caps.MaxHeightMm - 10));
        return (left, top, Math.Min(w, caps.MaxWidthMm - left), Math.Min(h, caps.MaxHeightMm - top));
    }

    public string? Validate()
    {
        if (!Sources.Contains(Source)) return "مصدر الورق غير معروف.";
        if (!ColorModes.Contains(ColorMode)) return "نمط اللون غير معروف.";
        if (Dpi is < 50 or > 1200) return "الدقة بين 50 و1200 نقطة/بوصة.";
        if (Brightness is < -100 or > 100 || Contrast is < -100 or > 100) return "السطوع والتباين بين -100 و100.";
        if (MaxPages is < 0 or > 500) return "عدد الصفحات بين 0 (الكل) و500.";
        if (WidthMm < 0 || HeightMm < 0 || LeftMm < 0 || TopMm < 0) return "منطقة المسح غير صحيحة.";
        return null;
    }
}

/// <summary>صفحة ممسوحة كما وصلت من الجهاز (JPEG أو PNG أو BMP) بأبعادها ودقتها.</summary>
public sealed record ScannedImage(byte[] Data, int Dpi);

public sealed class ScannerException(string message, Exception? inner = null) : Exception(message, inner);
