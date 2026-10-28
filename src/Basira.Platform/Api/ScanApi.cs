using Basira.Platform.Data;
using Basira.Platform.Processing;
using Basira.Platform.Processing.Scanning;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>المسح الضوئي: الأجهزة وقدراتها، والمعاينة، والمسح في الخلفية، ومراجعة الصفحات، والإدراج.</summary>
public static class ScanApi
{
    private sealed record AddBody(string? Address);
    private sealed record StartBody(string? DeviceId, ScanSettings? Settings, string? AppendTo);
    private sealed record PageOrder(string Id, int Rotation);
    private sealed record ArrangeBody(List<PageOrder>? Pages);
    private sealed record ImportBody(string? Direction, string? Confidentiality, string? Category, string? Title, string? Split, bool? SkipBlank);

    private static object View(ScanJob j)
    {
        lock (j.Lock)
            return new
            {
                id = j.Id, deviceId = j.DeviceId, state = j.State, message = j.Message, scanned = j.Scanned,
                pages = j.Pages.Select(p => new { id = p.Id, width = p.Width, height = p.Height, dpi = p.Dpi, rotation = p.Rotation, blank = p.Blank, kb = p.Data.Length / 1024 }),
            };
    }

    public static void Map(RouteGroupBuilder g)
    {
        var scan = g.MapGroup("/scan").AddEndpointFilter(async (ctx, next) =>
            UserStore.Can(U(ctx.HttpContext), "scan") && UserStore.Can(U(ctx.HttpContext), "import") ? await next(ctx) : Forbidden("المسح الضوئي"));

        scan.MapGet("/devices", async (ScanService s, HttpContext c, CancellationToken ct) =>
        {
            var (devices, note) = await s.DevicesAsync(ct);
            return Results.Ok(new { devices, note, windows = OperatingSystem.IsWindows(), canManage = UserStore.IsAdmin(U(c)) || UserStore.CanDelete(U(c)) });
        });

        scan.MapGet("/devices/caps", async (string id, ScanService s, CancellationToken ct) =>
        {
            try { return Results.Ok(await s.CapabilitiesAsync(id, ct, fresh: true)); }
            catch (ScannerException ex) { return Error(ex.Message); }
        });

        // الماسحات الشبكية يضيفها مدير النظام أو مدير الأرشيف بعنوانها.
        scan.MapPost("/devices", async (AddBody b, ScanService s, HttpContext c, CancellationToken ct) =>
        {
            var u = U(c);
            if (!UserStore.IsAdmin(u) && !UserStore.CanDelete(u)) return Forbidden("إضافة الماسحات");
            try { return Results.Ok(await s.AddEsclAsync(b.Address ?? "", u.UserName, ct)); }
            catch (ScannerException ex) { return Error(ex.Message); }
        });

        scan.MapPost("/devices/remove", (AddBody b, ScanService s, HttpContext c) =>
        {
            var u = U(c);
            if (!UserStore.IsAdmin(u) && !UserStore.CanDelete(u)) return Forbidden("حذف الماسحات");
            s.RemoveEscl(b.Address ?? "", u.UserName);
            return Results.Ok(new { ok = true });
        });

        scan.MapPost("/preview", async (StartBody b, ScanService s, CancellationToken ct) =>
        {
            try
            {
                var (jpeg, w, h) = await s.PreviewAsync(b.DeviceId ?? "", ct);
                return Results.Ok(new { image = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg), widthMm = w, heightMm = h });
            }
            catch (ScannerException ex) { return Error(ex.Message); }
        });

        scan.MapPost("/jobs", async (StartBody b, ScanService s, HttpContext c, CancellationToken ct) =>
        {
            try { return Results.Ok(View(await s.StartAsync(b.DeviceId ?? "", b.Settings ?? new ScanSettings(), U(c).UserName, b.AppendTo, ct))); }
            catch (ScannerException ex) { return Error(ex.Message); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        scan.MapPost("/native", async (StartBody b, ScanService s, HttpContext c, CancellationToken ct) =>
        {
            try { return Results.Ok(View(await s.NativeAsync(b.DeviceId ?? "", U(c).UserName, b.AppendTo, ct))); }
            catch (ScannerException ex) { return Error(ex.Message); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        scan.MapGet("/jobs/{id}", (string id, ScanService s, HttpContext c) =>
        {
            try { return Results.Ok(View(s.Get(id, U(c).UserName))); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        scan.MapGet("/jobs/{id}/pages/{pageId}", (string id, string pageId, bool? full, ScanService s, HttpContext c) =>
        {
            try
            {
                var job = s.Get(id, U(c).UserName);
                ScanPage? p;
                lock (job.Lock) p = job.Pages.FirstOrDefault(x => x.Id == pageId);
                if (p is null) return Error("الصفحة غير موجودة.", 404);
                c.Response.Headers.CacheControl = "private, max-age=3600";
                return full == true ? Results.File(p.Data, p.Jpeg ? "image/jpeg" : "image/png") : Results.File(p.Thumb, "image/jpeg");
            }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        scan.MapPost("/jobs/{id}/arrange", (string id, ArrangeBody b, ScanService s, HttpContext c) =>
        {
            try { s.Arrange(id, U(c).UserName, (b.Pages ?? new()).Select(p => (p.Id, p.Rotation)).ToList()); return Results.Ok(View(s.Get(id, U(c).UserName))); }
            catch (ScannerException ex) { return Error(ex.Message); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        scan.MapPost("/jobs/{id}/import", async (string id, ImportBody b, ScanService s, HttpContext c, CancellationToken ct) =>
        {
            if (b.Split is not (null or "one" or "each" or "blank")) return Error("طريقة التقسيم غير معروفة.");
            var options = new ImportOptions(b.Direction is { Length: > 0 } dir ? dir : "وارد", b.Category ?? "", b.Confidentiality is { Length: > 0 } conf ? conf : "عادي", b.Title ?? "");
            try
            {
                var results = await s.ImportAsync(id, U(c).UserName, options, b.Split ?? "one", b.SkipBlank ?? true, ct);
                return Results.Ok(results.Select(r => new { id = r.Document.Id, archiveNo = r.Document.ArchiveNo, title = r.Document.Title, duplicate = r.Duplicate }));
            }
            catch (ScannerException ex) { return Error(ex.Message); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        scan.MapPost("/jobs/{id}/cancel", (string id, ScanService s, HttpContext c) =>
        {
            try { s.Cancel(id, U(c).UserName); return Results.Ok(new { ok = true }); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });
    }
}
