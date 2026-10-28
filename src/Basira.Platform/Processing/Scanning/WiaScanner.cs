using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Basira.Platform.Processing.Scanning;

/// <summary>
/// الماسحات المعرّفة في Windows عبر WIA 2.0 (مكتبة WIA Automation المضمنة في Windows). كل استدعاء في خيط STA مستقل
/// كما تتطلب COM. أرقام الخصائص من توثيق WIA: إعدادات الجهاز (3xxx) وإعدادات عنصر المسح (4xxx و6xxx).
/// </summary>
[SupportedOSPlatform("windows")]
public static class WiaScanner
{
    private const int DocumentHandlingCapabilities = 3086, DocumentHandlingStatus = 3087, DocumentHandlingSelect = 3088, Pages = 3096;
    private const int BedWidth = 3074, BedHeight = 3075; // بالألف من البوصة
    private const int Intent = 6146, XRes = 6147, YRes = 6148, XStart = 6149, YStart = 6150, XExtent = 6151, YExtent = 6152, Brightness = 6154, Contrast = 6155;
    private const int DataType = 4103;
    private const int Feeder = 1, Flatbed = 2, Duplex = 4, FeedReady = 1;
    private const string FormatPng = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}", FormatBmp = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";
    private const int PaperEmpty = unchecked((int)0x80210003), Busy = unchecked((int)0x80210006), Offline = unchecked((int)0x80210005), Cancelled = 1;

    public static bool Available => OperatingSystem.IsWindows() && Type.GetTypeFromProgID("WIA.DeviceManager") is not null;

    private static T Sta<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        t.Join();
        if (error is not null) throw Translate(error);
        return result;
    }

    private static Exception Translate(Exception ex) => ex switch
    {
        ScannerException => ex,
        COMException c when c.HResult == Busy => new ScannerException("الماسح مشغول؛ أعد المحاولة بعد قليل.", c),
        COMException c when c.HResult == Offline => new ScannerException("الماسح غير متصل أو مطفأ.", c),
        COMException c when c.HResult == PaperEmpty => new ScannerException("لا أوراق في وحدة التغذية.", c),
        COMException c => new ScannerException($"خطأ من تعريف الماسح (0x{c.HResult:X8}): {c.Message}", c),
        _ => new ScannerException("تعذر المسح: " + ex.Message, ex),
    };

    private static dynamic Create(string progId) => Activator.CreateInstance(Type.GetTypeFromProgID(progId) ?? throw new ScannerException("مكتبة WIA غير متوفرة في Windows."))!;

    private static dynamic? Prop(dynamic props, int id)
    {
        for (var i = 1; i <= (int)props.Count; i++)
        {
            var p = props[i];
            if ((int)p.PropertyID == id) return p;
        }
        return null;
    }

    private static void Set(dynamic props, int id, object value)
    {
        var p = Prop(props, id);
        if (p is null) return;
        try { p.Value = value; }
        catch (COMException) { } // قيمة لا يقبلها التعريف: تبقى قيمته الحالية
    }

    private static int Int(dynamic props, int id, int fallback) { var p = Prop(props, id); try { return p is null ? fallback : Convert.ToInt32(p.Value); } catch (COMException) { return fallback; } }

    public static List<ScannerDevice> List() => Sta<List<ScannerDevice>>(() =>
    {
        var list = new List<ScannerDevice>();
        var manager = Create("WIA.DeviceManager");
        var infos = manager.DeviceInfos;
        for (var i = 1; i <= (int)infos.Count; i++)
        {
            var info = infos[i];
            if ((int)info.Type != 1) continue; // ScannerDeviceType
            string name = "ماسح";
            var p = Prop(info.Properties, 7); // WIA_DIP_DEV_NAME
            if (p is not null) name = Convert.ToString(p.Value) ?? name;
            list.Add(new ScannerDevice("wia:" + (string)info.DeviceID, name, "wia"));
        }
        return list;
    });

    private static dynamic Connect(string deviceId)
    {
        var manager = Create("WIA.DeviceManager");
        var infos = manager.DeviceInfos;
        for (var i = 1; i <= (int)infos.Count; i++)
            if ((string)infos[i].DeviceID == deviceId) return infos[i].Connect();
        throw new ScannerException("الماسح غير موجود؛ تأكد أنه متصل ومشغّل.");
    }

    private static List<int> Values(dynamic? p, IEnumerable<int> wanted)
    {
        if (p is null) return new();
        try
        {
            int subType = (int)p.SubType;
            if (subType == 2) // قائمة
            {
                var list = new List<int>();
                var values = p.SubTypeValues;
                for (var i = 1; i <= (int)values.Count; i++) list.Add(Convert.ToInt32(values[i]));
                return list;
            }
            if (subType == 1) { int min = (int)p.SubTypeMin, max = (int)p.SubTypeMax; return wanted.Where(v => v >= min && v <= max).ToList(); }
        }
        catch (COMException) { }
        return new() { Convert.ToInt32(p.Value) };
    }

    public static ScannerCaps Capabilities(string deviceId) => Sta<ScannerCaps>(() =>
    {
        var device = Connect(deviceId);
        var dp = device.Properties;
        var handling = Int(dp, DocumentHandlingCapabilities, Flatbed);
        var sources = new List<string>();
        if ((handling & Flatbed) != 0 || Prop(dp, DocumentHandlingCapabilities) is null) sources.Add("flatbed");
        if ((handling & Feeder) != 0) sources.Add("feeder");
        if ((handling & Duplex) != 0) sources.Add("duplex");
        var item = device.Items[1];
        var ip = item.Properties;
        dynamic? resProp = Prop(ip, XRes);
        List<int> raw = Values(resProp, new[] { 75, 100, 150, 200, 300, 400, 600, 1200 });
        var res = raw.Where(v => v >= 50).Distinct().OrderBy(v => v).ToList();
        string name = "ماسح WIA";
        var np = Prop(dp, 7);
        if (np is not null) name = Convert.ToString(np.Value) ?? name;
        int bedW = Int(dp, BedWidth, 8500), bedH = Int(dp, BedHeight, 11690);
        bool hasBrightness = Prop(ip, Brightness) is not null, hasContrast = Prop(ip, Contrast) is not null;
        return new ScannerCaps(name, sources, new List<string> { "color", "gray", "bw" }, res.Count > 0 ? res : new List<int> { 150, 300 },
            Math.Round(bedW / 1000.0 * 25.4, 1), Math.Round(bedH / 1000.0 * 25.4, 1), hasBrightness, hasContrast, NativeDialog: true);
    });

    private static int Scale(dynamic? p, int v)
    {
        if (p is null) return 0;
        try
        {
            int min = (int)p.SubTypeMin, max = (int)p.SubTypeMax;
            var mid = (min + max) / 2;
            return v >= 0 ? mid + v * (max - mid) / 100 : mid + v * (mid - min) / 100;
        }
        catch (COMException) { return v * 10; } // المدى المعتاد -1000..1000
    }

    public static List<ScannedImage> Scan(string deviceId, ScanSettings s, ScannerCaps caps, Action<int> onPage, CancellationToken ct) => Sta<List<ScannedImage>>(() =>
    {
        var device = Connect(deviceId);
        var dp = device.Properties;
        var feeder = s.Source != "flatbed";
        if (Prop(dp, DocumentHandlingSelect) is not null) Set(dp, DocumentHandlingSelect, s.Source == "duplex" ? Feeder | Duplex : feeder ? Feeder : Flatbed);
        if (feeder && Prop(dp, DocumentHandlingStatus) is not null && (Int(dp, DocumentHandlingStatus, FeedReady) & FeedReady) == 0)
            throw new ScannerException("لا أوراق في وحدة التغذية.");
        Set(dp, Pages, 1);
        var item = device.Items[1];
        var ip = item.Properties;
        Set(ip, Intent, s.ColorMode == "color" ? 1 : s.ColorMode == "gray" ? 2 : 4);
        Set(ip, DataType, s.ColorMode == "color" ? 3 : 2); // الأبيض والأسود من الرمادي بعتبة تلقائية أدق من عتبة التعريف الثابتة
        Set(ip, XRes, s.Dpi);
        Set(ip, YRes, s.Dpi);
        var (left, top, width, height) = s.Region(caps);
        int Px(double mm) => (int)Math.Round(mm / 25.4 * s.Dpi);
        Set(ip, XStart, Px(left));
        Set(ip, YStart, Px(top));
        Set(ip, XExtent, Px(width));
        Set(ip, YExtent, Px(height));
        if (s.Brightness != 0) Set(ip, Brightness, Scale(Prop(ip, Brightness), s.Brightness));
        if (s.Contrast != 0) Set(ip, Contrast, Scale(Prop(ip, Contrast), s.Contrast));
        var pages = new List<ScannedImage>();
        while (s.MaxPages == 0 || pages.Count < s.MaxPages)
        {
            ct.ThrowIfCancellationRequested();
            dynamic image;
            try { image = Transfer(item); }
            catch (COMException c) when (c.HResult == PaperEmpty && pages.Count > 0) { break; }
            pages.Add(new ScannedImage((byte[])image.FileData.BinaryData, s.Dpi));
            onPage(pages.Count);
            if (!feeder) break;
        }
        return pages;
    });

    private static dynamic Transfer(dynamic item)
    {
        try { return item.Transfer(FormatPng); }
        catch (COMException c) when (c.HResult != PaperEmpty && c.HResult != Busy && c.HResult != Offline) { return item.Transfer(FormatBmp); }
    }

    /// <summary>
    /// نافذة إعدادات الماسح الأصلية (من الشركة المصنعة) لكل خياراته الخاصة. تظهر على شاشة الجهاز الذي يعمل عليه البرنامج،
    /// وتعيد صفحة واحدة في كل مرة. null إن أُلغيت النافذة.
    /// </summary>
    public static ScannedImage? NativeDialog(string deviceId) => Sta<ScannedImage?>(() =>
    {
        var device = Connect(deviceId);
        var dialog = Create("WIA.CommonDialog");
        try
        {
            var items = dialog.ShowSelectItems(device, 0, 0, true, false, false); // false: نافذة الشركة المصنعة إن وُجدت
            if (items is null || (int)items.Count == 0) return null;
            var image = dialog.ShowTransfer(items[1], FormatPng, false);
            return image is null ? null : new ScannedImage((byte[])image.FileData.BinaryData, Convert.ToInt32(image.HorizontalResolution));
        }
        catch (COMException c) when (c.HResult == Cancelled || (uint)c.HResult == 0x80210064) { return null; } // WIA_S_NO_DEVICE_AVAILABLE / إلغاء
    });
}
