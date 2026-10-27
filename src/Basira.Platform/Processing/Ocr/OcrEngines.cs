using System.Collections.Concurrent;
using Basira.Platform.Data;

namespace Basira.Platform.Processing.Ocr;

/// <summary>
/// اختيار محرك القراءة من الإعدادات: «auto» (المرئي إن ضُبط وكان خادمه متاحاً، ومعه Tesseract قراءةً ثانية للتحقق؛ وإلا Tesseract وحده)،
/// أو «tesseract» (Tesseract وحده).
/// المحرك المرئي محلي أو خدمة سحابية (Gemini أو Claude) بمفتاح مشفّر وإقرار من المدير؛ والمستندات غير العادية السرية لا تُرسل إليها أبداً.
/// حالة الخادم تُفحص مرة كل دقيقة على الأكثر.
/// </summary>
public static class OcrEngines
{
    public static readonly string[] Modes = { "auto", "tesseract" };
    private static readonly ConcurrentDictionary<string, (DateTime At, bool Ok, string Message, List<string> Models)> Probes = new();

    public static string Provider(SettingsStore s) => VisionOcr.Providers.Contains(s.Get("vision_provider")) ? s.Get("vision_provider") : "local";

    public static string KeyName(string provider) => $"secret_{provider}_key";
    public static string ConsentName(string provider) => $"vision_cloud_consent_{provider}";

    public static VisionOptions? Configured(SettingsStore s)
    {
        var provider = Provider(s);
        var (side, timeout) = (s.GetInt("vision_ocr_max_side", 1280, 640, 4000), s.GetInt("vision_ocr_timeout", 600, 30, 3600));
        if (provider != "local")
        {
            // اسم نموذج المزوّد الآخر (بعد تبديل المزوّد) لا يصلح: يُستخدم الموصى به.
            var cloudModel = s.Get("vision_cloud_model").Trim();
            return new VisionOptions(VisionOcr.UrlFor(provider, ""), cloudModel.StartsWith(provider, StringComparison.OrdinalIgnoreCase) ? cloudModel : VisionOcr.DefaultModel(provider), s.Get("vision_ocr_prompt"), side, timeout, provider, s.GetSecret(KeyName(provider)));
        }
        var url = s.Get("vision_ocr_url").Trim();
        var model = s.Get("vision_ocr_model").Trim();
        return url == "" || model == "" ? null : new VisionOptions(url, model, s.Get("vision_ocr_prompt"), side, timeout);
    }

    public static async Task<(bool Ok, string Message, List<string> Models)> ProbeAsync(string url, CancellationToken ct, bool fresh = false, string provider = "local", string apiKey = "")
    {
        var cacheKey = provider + "|" + url + "|" + apiKey.GetHashCode();
        if (!fresh && Probes.TryGetValue(cacheKey, out var p) && DateTime.UtcNow - p.At < TimeSpan.FromMinutes(1)) return (p.Ok, p.Message, p.Models);
        if (fresh) VisionOcr.Forget(url);
        var r = await VisionOcr.ProbeAsync(url, ct, provider, apiKey);
        Probes[cacheKey] = (DateTime.UtcNow, r.Ok, r.Message, r.Models);
        return r;
    }

    /// <summary>
    /// أدوات القراءة لمستند: Tesseract وpdftoppm، والمحرك المرئي بحسب الوضع وحالة خادمه.
    /// sensitive: مستند غير عادي السرية، فلا يُرسل إلى خدمة سحابية ويُقرأ بـ Tesseract على هذا الجهاز.
    /// </summary>
    public static async Task<TextExtractor.Tools> ToolsAsync(SettingsStore s, CancellationToken ct, bool ignoreSuspension = false, bool sensitive = false)
    {
        var all = s.All();
        var mode = Modes.Contains(all.GetValueOrDefault("ocr_engine", "auto")) ? all["ocr_engine"] : "auto";
        var tools = TextExtractor.Find(all["tesseract_path"], all["pdftoppm_path"], all["ocr_languages"]);
        if (mode == "tesseract") return tools;
        var vision = Configured(s);
        if (vision is null) return tools;
        if (vision.IsCloud)
        {
            if (sensitive) return tools with { VisionNote = "مستند غير عادي السرية: لا يُرسل إلى الخدمة السحابية، فقُرئ على هذا الجهاز" };
            if (s.GetRaw(ConsentName(vision.Provider)) == "") return tools with { VisionNote = $"لم يُقَر إرسال الصور إلى {vision.ProviderLabel}" };
            if (vision.ApiKey == "") return tools with { VisionNote = $"لم يُحفظ مفتاح {vision.ProviderLabel} أو تعذر فك تشفيره" };
        }
        if (!ignoreSuspension && VisionOcr.Suspended(vision.Url, out var until))
            return tools with { VisionNote = $"أُوقف المحرك المرئي مؤقتاً حتى {until.ToLocalTime():HH:mm} لتجاوز القراءة المهلة مرتين متتاليتين" };
        var (ok, message, models) = await ProbeAsync(vision.Url, ct, provider: vision.Provider, apiKey: vision.ApiKey);
        if (!ok) return tools with { VisionNote = (vision.IsCloud ? vision.ProviderLabel + " غير متاح: " : "خادم القراءة المرئية غير متاح: ") + message };
        // أسماء نماذج الخدمات السحابية قد تكون أسماء مستعارة لا تظهر في القائمة، فلا يُحكم بغيابها.
        if (!vision.IsCloud && models.Count > 0 && !models.Any(m => m.Equals(vision.Model, StringComparison.OrdinalIgnoreCase) || m.StartsWith(vision.Model + ":", StringComparison.OrdinalIgnoreCase)))
            return tools with { VisionNote = $"النموذج «{vision.Model}» غير موجود في الخادم (المتوفر: {string.Join("، ", models.Take(6))})" };
        return tools with { Vision = vision };
    }
}
