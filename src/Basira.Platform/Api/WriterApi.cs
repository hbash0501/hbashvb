using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using Basira.Platform.Processing.Ocr;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>
/// إعداد مساعد الكتابة بالذكاء الاصطناعي (لمدير النظام). المفتاح هو مفتاح الخدمة نفسه المحفوظ مشفّراً في محرك القراءة،
/// والإقرار مستقل: إرسال نصوص الخطابات لكتابة الردود غير إرسال صورها للقراءة.
/// </summary>
public static class WriterApi
{
    public static readonly string[] Providers = { "", "claude", "gemini", "local" };
    public static string ConsentName(string provider) => $"writer_consent_{provider}";
    private sealed record ConsentBody(string? Provider, bool Consent);

    /// <summary>المزوّد المضبوط وإعداداته، أو سبب عدم توفره.</summary>
    public static (WriterOptions? Options, string Reason) Configured(SettingsStore s)
    {
        var provider = Providers.Contains(s.Get("writer_provider")) ? s.Get("writer_provider") : "";
        if (provider == "") return (null, "مساعد الكتابة غير مفعّل. يفعّله مدير النظام من الإدارة ← الإعدادات.");
        var model = s.Get("writer_model").Trim();
        if (model == "") model = WriterOptions.DefaultModel(provider);
        if (provider == "local")
        {
            var url = s.Get("vision_ocr_url").Trim();
            if (url == "" || model == "") return (null, "النموذج المحلي يحتاج عنوان الخادم (من محرك القراءة) واسم نموذج الكتابة.");
            return (new WriterOptions(provider, model, LocalUrl: url, TimeoutSeconds: 600), "");
        }
        var key = s.GetSecret(OcrEngines.KeyName(provider));
        if (key == "") return (null, $"لم يُحفظ مفتاح {(provider == "claude" ? "Claude" : "Gemini")}؛ يُحفظ من محرك القراءة في الإعدادات.");
        if (s.GetRaw(ConsentName(provider)) == "") return (null, "لم يُقَر إرسال نصوص الخطابات إلى الخدمة لكتابة الردود.");
        return (new WriterOptions(provider, model, key), "");
    }

    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/writer", (SettingsStore s) =>
        {
            var (options, reason) = Configured(s);
            return Results.Ok(new
            {
                provider = s.Get("writer_provider"), model = s.Get("writer_model"), available = options is not null, reason,
                defaults = new { claude = WriterOptions.DefaultModel("claude"), gemini = WriterOptions.DefaultModel("gemini") },
                cloud = new[] { "claude", "gemini" }.Select(p => new
                {
                    provider = p, hasKey = s.GetSecret(OcrEngines.KeyName(p)) != "",
                    consentBy = s.GetRaw(ConsentName(p)).Split('|')[0], consentAt = s.GetRaw(ConsentName(p)).Split('|').ElementAtOrDefault(1) ?? "",
                }),
                localUrl = s.Get("vision_ocr_url"),
            });
        });

        admin.MapPost("/writer/consent", (ConsentBody b, HttpContext c, SettingsStore s) =>
        {
            if (b.Provider is not ("claude" or "gemini")) return Error("مزوّد غير معروف.");
            var who = U(c).UserName;
            if (b.Consent) s.SetRaw(ConsentName(b.Provider), $"{who}|{DateTime.UtcNow:O}", who, "إقرار بإرسال نصوص الخطابات العادية إلى خدمة سحابية لكتابة الردود");
            else
            {
                s.SetRaw(ConsentName(b.Provider), "", who, "سحب إقرار الكتابة بالخدمة السحابية");
                if (s.Get("writer_provider") == b.Provider) s.Set(new Dictionary<string, string> { ["writer_provider"] = "" }, who);
            }
            return Results.Ok(new { ok = true });
        });
    }

    public static string? Validate(IDictionary<string, string> body, SettingsStore s)
    {
        if (body.TryGetValue("writer_provider", out var p))
        {
            if (!Providers.Contains(p)) return "مزوّد الكتابة غير معروف.";
            if (p is "claude" or "gemini" && s.GetRaw(ConsentName(p)) == "") return "يلزم الإقرار بإرسال نصوص الخطابات إلى الخدمة قبل اختيارها للكتابة.";
        }
        if (body.TryGetValue("writer_model", out var m) && m.Trim() != "" && !System.Text.RegularExpressions.Regex.IsMatch(m.Trim(), @"^[A-Za-z0-9][A-Za-z0-9._:\-/]{1,80}$")) return "اسم النموذج غير صحيح.";
        return null;
    }
}
