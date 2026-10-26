using System.Collections.Concurrent;
using Basira.Platform.Data;

namespace Basira.Platform.Processing.Ocr;

/// <summary>
/// اختيار محرك القراءة من الإعدادات: «auto» (المرئي إن ضُبط وكان خادمه متاحاً، ومعه Tesseract قراءةً ثانية للتحقق؛ وإلا Tesseract وحده)،
/// أو «tesseract» (Tesseract وحده).
/// حالة الخادم تُفحص مرة كل دقيقة على الأكثر.
/// </summary>
public static class OcrEngines
{
    public static readonly string[] Modes = { "auto", "tesseract" };
    private static readonly ConcurrentDictionary<string, (DateTime At, bool Ok, string Message, List<string> Models)> Probes = new();

    public static VisionOptions? Configured(SettingsStore s)
    {
        var url = s.Get("vision_ocr_url").Trim();
        var model = s.Get("vision_ocr_model").Trim();
        return url == "" || model == "" ? null : new VisionOptions(url, model, s.Get("vision_ocr_prompt"), s.GetInt("vision_ocr_max_side", 1800, 640, 4000), s.GetInt("vision_ocr_timeout", 300, 30, 1800));
    }

    public static async Task<(bool Ok, string Message, List<string> Models)> ProbeAsync(string url, CancellationToken ct, bool fresh = false)
    {
        if (!fresh && Probes.TryGetValue(url, out var p) && DateTime.UtcNow - p.At < TimeSpan.FromMinutes(1)) return (p.Ok, p.Message, p.Models);
        var r = await VisionOcr.ProbeAsync(url, ct);
        Probes[url] = (DateTime.UtcNow, r.Ok, r.Message, r.Models);
        return r;
    }

    /// <summary>أدوات القراءة لمستند: Tesseract وpdftoppm، والمحرك المرئي بحسب الوضع وحالة خادمه.</summary>
    public static async Task<TextExtractor.Tools> ToolsAsync(SettingsStore s, CancellationToken ct)
    {
        var all = s.All();
        var mode = Modes.Contains(all.GetValueOrDefault("ocr_engine", "auto")) ? all["ocr_engine"] : "auto";
        var tools = TextExtractor.Find(all["tesseract_path"], all["pdftoppm_path"], all["ocr_languages"]);
        if (mode == "tesseract") return tools;
        var vision = Configured(s);
        if (vision is null) return tools;
        var (ok, message, models) = await ProbeAsync(vision.Url, ct);
        if (!ok) return tools with { VisionNote = "خادم القراءة المرئية غير متاح: " + message };
        if (models.Count > 0 && !models.Any(m => m.Equals(vision.Model, StringComparison.OrdinalIgnoreCase) || m.StartsWith(vision.Model + ":", StringComparison.OrdinalIgnoreCase)))
            return tools with { VisionNote = $"النموذج «{vision.Model}» غير موجود في الخادم (المتوفر: {string.Join("، ", models.Take(6))})" };
        return tools with { Vision = vision };
    }
}
