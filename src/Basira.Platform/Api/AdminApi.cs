using Basira.Platform.Data;
using Basira.Platform.Intelligence.Semantic;
using Basira.Platform.Processing;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

public static class AdminApi
{
    private sealed record NewUser(string? UserName, string? FullName, string? Role, string? Password);
    private sealed record EditUser(string? FullName, string? Role, bool? Active, string? Password);
    private sealed record ImportBody(string? Folder);

    public static void Map(RouteGroupBuilder g)
    {
        var admin = g.MapGroup("/admin").AddEndpointFilter(async (ctx, next) =>
            UserStore.IsAdmin(U(ctx.HttpContext)) ? await next(ctx) : Forbidden("الإدارة (مدير النظام فقط)"));

        admin.MapGet("/users", (UserStore users) => Results.Ok(users.List()));

        admin.MapPost("/users", (NewUser b, HttpContext c, UserStore users) =>
        {
            try { return Results.Ok(users.Create(b.UserName ?? "", b.FullName ?? "", b.Role ?? "", b.Password ?? "", U(c).UserName)); }
            catch (ArgumentException ex) { return Error(ex.Message); }
        });

        admin.MapPut("/users/{id}", (string id, EditUser b, HttpContext c, UserStore users) =>
        {
            try { users.Update(id, b.FullName, b.Role, b.Active, b.Password, U(c).UserName); return Results.Ok(new { ok = true }); }
            catch (ArgumentException ex) { return Error(ex.Message); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        admin.MapGet("/settings", (SettingsStore settings) =>
        {
            var s = settings.All();
            var tools = TextExtractor.Find(s["tesseract_path"], s["pdftoppm_path"], s["ocr_languages"]);
            return Results.Ok(new
            {
                settings = s,
                detected = new { tesseract = tools.Tesseract, pdftoppm = tools.PdfToPpm, inboxExists = s["inbox_folder"] != "" && Directory.Exists(s["inbox_folder"]) },
            });
        });

        admin.MapPut("/settings", (Dictionary<string, string> body, HttpContext c, SettingsStore settings, SemanticService semantic) =>
        {
            if (body.TryGetValue("reply_days", out var rd) && (!int.TryParse(rd, out var n) || n is < 1 or > 120)) return Error("مهلة الرد بين 1 و120 يوماً.");
            if (body.TryGetValue("commitment_days", out var cd) && (!int.TryParse(cd, out var cdn) || cdn is < 1 or > 365)) return Error("المهلة الافتراضية للالتزامات بين 1 و365 يوماً.");
            if (body.TryGetValue("default_direction", out var dir) && !Roles.Directions.Contains(dir)) return Error("اتجاه غير معروف.");
            if (body.TryGetValue("semantic_model_url", out var url) && url.Trim() != "" && !(Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"))
                return Error("مصدر النموذج يجب أن يكون رابط http أو https.");
            if (OcrApi.Validate(body, settings) is { } ocrError) return Error(ocrError);
            var modelPathChanged = body.TryGetValue("semantic_model_path", out var mp) && mp.Trim() != settings.Get("semantic_model_path");
            settings.Set(body, U(c).UserName);
            if (modelPathChanged) semantic.Reset();
            return Results.Ok(new { ok = true });
        });
        SemanticApi.Map(admin);
        OcrApi.Map(admin);

        admin.MapPost("/import-legacy", async (ImportBody b, HttpContext c, LegacyImporter importer) =>
        {
            if (string.IsNullOrWhiteSpace(b.Folder)) return Error("حدّد مجلد بيانات النسخة السابقة.");
            try { return Results.Ok(await importer.ImportAsync(b.Folder, U(c).UserName, c.RequestAborted)); }
            catch (FileNotFoundException ex) { return Error(ex.Message, 404); }
        });
    }
}
