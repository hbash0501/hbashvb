using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Processing;
using Basira.Platform.Processing.Ocr;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>
/// محركات القراءة (لمدير النظام): الحالة، وفحص خادم القراءة المرئية، وقياس المحركات على العينات المضمّنة،
/// ومفاتيح الخدمات السحابية وإقرار الإرسال إليها. المفتاح يُحفظ مشفّراً ولا يُعاد منه إلا آخر أربعة أحرف.
/// </summary>
public static class OcrApi
{
    private sealed record ProbeBody(string? Url, string? Provider);
    private sealed record CloudBody(string? Provider, string? Key, bool? Consent);

    private static object Cloud(SettingsStore settings, string provider)
    {
        var key = settings.GetSecret(OcrEngines.KeyName(provider));
        var consent = settings.GetRaw(OcrEngines.ConsentName(provider)).Split('|');
        return new
        {
            provider, label = new VisionOptions("", "", Provider: provider).ProviderLabel, defaultModel = VisionOcr.DefaultModel(provider),
            hasKey = key != "", keyHint = key == "" ? "" : SecretBox.Hint(key),
            consentBy = consent.Length == 2 ? consent[0] : "", consentAt = consent.Length == 2 ? consent[1] : "",
        };
    }

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
                provider = OcrEngines.Provider(settings), cloudModel = s["vision_cloud_model"],
                cloud = new[] { "gemini", "claude" }.Select(p => Cloud(settings, p)),
                progressSeconds = bench.Running && bench.ProgressSince != DateTime.MinValue ? (int)(DateTime.UtcNow - bench.ProgressSince).TotalSeconds : 0,
                tesseract = tools.Tesseract, pdftoppm = tools.PdfToPpm,
                vision = tools.Vision is null ? null : new { url = tools.Vision.Url, model = tools.Vision.Model, cloud = tools.Vision.IsCloud, label = tools.Vision.ProviderLabel },
                visionNote = tools.VisionNote,
                digitReader = new { available = DigitReader.Available, templates = DigitReader.TemplateCount, problem = DigitReader.Problem },
                benchmark = bench.Last, running = bench.Running, progress = bench.Progress, error = bench.Error,
            });
        });

        admin.MapPost("/ocr/probe", async (ProbeBody b, SettingsStore settings, CancellationToken ct) =>
        {
            var provider = VisionOcr.Providers.Contains(b.Provider) ? b.Provider! : "local";
            var url = provider == "local" ? (b.Url ?? "").Trim() : VisionOcr.UrlFor(provider, "");
            var (ok, message, models) = await OcrEngines.ProbeAsync(url, ct, fresh: true, provider, provider == "local" ? "" : settings.GetSecret(OcrEngines.KeyName(provider)));
            return Results.Ok(new { ok, message, models });
        });

        // مفتاح خدمة سحابية و/أو إقرار الإرسال إليها. مفتاح فارغ يحذف المحفوظ.
        admin.MapPost("/ocr/cloud", (HttpContext c, CloudBody b, SettingsStore settings) =>
        {
            if (b.Provider is not ("gemini" or "claude")) return Error("مزوّد غير معروف.");
            var who = U(c).UserName;
            if (b.Key is not null)
            {
                var key = b.Key.Trim();
                if (key != "" && (key.Length is < 20 or > 400 || key.Any(char.IsWhiteSpace))) return Error("صيغة المفتاح غير صحيحة.");
                settings.SetSecret(OcrEngines.KeyName(b.Provider), key, who, key == "" ? "حذف مفتاح خدمة سحابية" : "حفظ مفتاح خدمة سحابية");
                VisionOcr.Forget(VisionOcr.UrlFor(b.Provider, ""));
            }
            if (b.Consent == true && settings.GetRaw(OcrEngines.ConsentName(b.Provider)) == "")
                settings.SetRaw(OcrEngines.ConsentName(b.Provider), $"{who}|{DateTime.UtcNow:O}", who, "إقرار بإرسال صور المستندات العادية إلى خدمة سحابية");
            if (b.Consent == false)
            {
                settings.SetRaw(OcrEngines.ConsentName(b.Provider), "", who, "سحب إقرار الإرسال إلى خدمة سحابية");
                if (OcrEngines.Provider(settings) == b.Provider) settings.Set(new Dictionary<string, string> { ["vision_provider"] = "local" }, who);
            }
            return Results.Ok(Cloud(settings, b.Provider));
        });

        admin.MapPost("/ocr/benchmark", (HttpContext c, OcrBenchmark bench) =>
            bench.Start(U(c).UserName) ? Results.Ok(new { started = true }) : Error("القياس جارٍ بالفعل.", 409));
    }

    /// <summary>تحقق إعدادات القراءة قبل حفظها.</summary>
    public static string? Validate(IDictionary<string, string> body, SettingsStore settings)
    {
        if (body.TryGetValue("ocr_engine", out var mode) && !OcrEngines.Modes.Contains(mode)) return "وضع محرك القراءة غير معروف.";
        if (body.TryGetValue("vision_provider", out var provider))
        {
            if (!VisionOcr.Providers.Contains(provider)) return "مزوّد القراءة غير معروف.";
            if (provider != "local" && settings.GetRaw(OcrEngines.ConsentName(provider)) == "") return "يلزم الإقرار بإرسال صور المستندات إلى الخدمة السحابية قبل اختيارها.";
        }
        if (body.TryGetValue("vision_cloud_model", out var cm) && cm.Trim() != "" && !System.Text.RegularExpressions.Regex.IsMatch(cm.Trim(), @"^[A-Za-z0-9][A-Za-z0-9._\-]{1,80}$")) return "اسم النموذج السحابي غير صحيح.";
        if (body.TryGetValue("vision_ocr_url", out var url) && url.Trim() != "" && !VisionOcr.IsLocalUrl(url, out var why)) return why;
        if (body.TryGetValue("vision_ocr_max_side", out var side) && (!int.TryParse(side, out var sv) || sv is < 640 or > 4000)) return "دقة الصورة بين 640 و4000 بكسل.";
        if (body.TryGetValue("vision_ocr_timeout", out var to) && (!int.TryParse(to, out var tv) || tv is < 30 or > 3600)) return "مهلة الصفحة بين 30 و3600 ثانية.";
        // إعدادات جديدة: يُنسى الإيقاف المؤقت ونوع الخادم.
        if (url is not null && url.Trim() != "") VisionOcr.Forget(url.Trim());
        return null;
    }
}
