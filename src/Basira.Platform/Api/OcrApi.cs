using Basira.Platform.Data;
using Basira.Platform.Processing;
using Basira.Platform.Processing.Ocr;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>محركات القراءة (لمدير النظام): الحالة، وفحص خادم القراءة المرئية، وقياس المحركات على العينات المضمّنة.</summary>
public static class OcrApi
{
    private sealed record ProbeBody(string? Url);

    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/ocr", async (SettingsStore settings, OcrBenchmark bench, CancellationToken ct) =>
        {
            var s = settings.All();
            var tools = await OcrEngines.ToolsAsync(settings, ct);
            return Results.Ok(new
            {
                mode = s["ocr_engine"], url = s["vision_ocr_url"], model = s["vision_ocr_model"], prompt = s["vision_ocr_prompt"], defaultPrompt = VisionOptions.DefaultPrompt,
                maxSide = s["vision_ocr_max_side"], timeout = s["vision_ocr_timeout"],
                progressSeconds = bench.Running && bench.ProgressSince != DateTime.MinValue ? (int)(DateTime.UtcNow - bench.ProgressSince).TotalSeconds : 0,
                tesseract = tools.Tesseract, pdftoppm = tools.PdfToPpm,
                vision = tools.Vision is null ? null : new { url = tools.Vision.Url, model = tools.Vision.Model },
                visionNote = tools.VisionNote,
                digitReader = new { available = DigitReader.Available, templates = DigitReader.TemplateCount, problem = DigitReader.Problem },
                benchmark = bench.Last, running = bench.Running, progress = bench.Progress, error = bench.Error,
            });
        });

        admin.MapPost("/ocr/probe", async (ProbeBody b, CancellationToken ct) =>
        {
            var (ok, message, models) = await OcrEngines.ProbeAsync((b.Url ?? "").Trim(), ct, fresh: true);
            return Results.Ok(new { ok, message, models });
        });

        admin.MapPost("/ocr/benchmark", (HttpContext c, OcrBenchmark bench) =>
            bench.Start(U(c).UserName) ? Results.Ok(new { started = true }) : Error("القياس جارٍ بالفعل.", 409));
    }

    /// <summary>تحقق إعدادات القراءة قبل حفظها.</summary>
    public static string? Validate(IDictionary<string, string> body)
    {
        if (body.TryGetValue("ocr_engine", out var mode) && !OcrEngines.Modes.Contains(mode)) return "وضع محرك القراءة غير معروف.";
        if (body.TryGetValue("vision_ocr_url", out var url) && url.Trim() != "" && !VisionOcr.IsLocalUrl(url, out var why)) return why;
        if (body.TryGetValue("vision_ocr_max_side", out var side) && (!int.TryParse(side, out var sv) || sv is < 640 or > 4000)) return "دقة الصورة بين 640 و4000 بكسل.";
        if (body.TryGetValue("vision_ocr_timeout", out var to) && (!int.TryParse(to, out var tv) || tv is < 30 or > 3600)) return "مهلة الصفحة بين 30 و3600 ثانية.";
        // إعدادات جديدة: يُنسى الإيقاف المؤقت ونوع الخادم.
        if (url is not null && url.Trim() != "") VisionOcr.Forget(url.Trim());
        return null;
    }
}
