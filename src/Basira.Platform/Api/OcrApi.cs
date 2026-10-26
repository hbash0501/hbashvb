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
                tesseract = tools.Tesseract, pdftoppm = tools.PdfToPpm,
                vision = tools.Vision is null ? null : new { url = tools.Vision.Url, model = tools.Vision.Model },
                visionNote = tools.VisionNote,
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
        return null;
    }
}
