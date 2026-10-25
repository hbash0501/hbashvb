using System.Globalization;
using AIDocumentArchiveVB;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence;

namespace Basira.Platform.Api;

public static class ApiCommon
{
    public const string Cookie = "basira_session";

    public static User U(HttpContext c) => (User)c.Items["user"]!;
    public static IResult Error(string message, int status = 400) => Results.Json(new { error = message }, statusCode: status);
    public static IResult Forbidden(string what) => Error("لا تملك صلاحية " + what + ".", 403);
    public static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "?";

    public static RouteGroupBuilder Secured(this RouteGroupBuilder g) => g.AddEndpointFilter(async (ctx, next) =>
    {
        var http = ctx.HttpContext;
        var claims = http.RequestServices.GetRequiredService<SessionTokens>().Validate(http.Request.Cookies[Cookie]);
        var user = claims is null ? null : http.RequestServices.GetRequiredService<UserStore>().Find(claims.UserName);
        if (user is null || !user.Active || user.Stamp != claims!.Stamp) return Error("انتهت الجلسة. سجّل الدخول من جديد.", 401);
        http.Items["user"] = user;
        return await next(ctx);
    });

    public static bool Restricted(Doc d, User u) => BasiraEngine.IsSensitive(ArchiveIndex.ToRecord(d, "")) && !UserStore.CanReadSensitive(u);

    public static string Clip(string? v, int max) => string.IsNullOrEmpty(v) ? "" : v.Length <= max ? v : v[..max];

    public static string Hijri(string iso)
    {
        if (Snapshot.ParseIso(iso) is not { } d) return "";
        var cal = new UmAlQuraCalendar();
        try { return $"{cal.GetYear(d)}/{cal.GetMonth(d):00}/{cal.GetDayOfMonth(d):00}هـ"; }
        catch (ArgumentOutOfRangeException) { return ""; }
    }

    /// <summary>ملخص مستند لقوائم الواجهة؛ يُحجب المحتوى الحساس عن غير المخوَّلين.</summary>
    public static object Summary(Doc d, Snapshot s, User u, string excerpt = "", string matches = "")
    {
        var restricted = Restricted(d, u);
        s.EffectiveDue.TryGetValue(d.Id, out var due);
        var awaiting = s.Awaiting.FirstOrDefault(a => a.DocumentId == d.Id);
        s.ThreadOf.TryGetValue(d.Id, out var thread);
        return new
        {
            id = d.Id, archiveNo = d.ArchiveNo, title = d.Title, category = d.Category, subCategory = d.SubCategory, status = d.Status,
            direction = d.Direction, confidentiality = d.Confidentiality, priority = d.Priority, sourceEntity = d.SourceEntity,
            destinationEntity = d.DestinationEntity, outgoingNo = d.OutgoingNo, incomingNo = d.IncomingNo, externalNo = d.ExternalNo,
            documentDate = d.DocumentDate, documentDateHijri = Hijri(d.DocumentDate), due = due.Date ?? "", dueHijri = Hijri(due.Date ?? ""), dueSource = due.Source ?? "",
            closed = BasiraEngine.IsClosed(ArchiveIndex.ToRecord(d, "")), sensitive = BasiraEngine.IsSensitive(ArchiveIndex.ToRecord(d, "")), restricted,
            summary = restricted ? "" : Clip(d.Summary, 320), excerpt = restricted ? "" : Clip(excerpt, 360), matches = restricted ? "" : matches,
            hasText = d.Text.Length > 0, hasFile = d.FileId != "", fileType = d.FileType, processing = d.Processing,
            thread = thread != null && s.Threads.ContainsKey(thread) ? thread : null,
            references = s.References.TryGetValue(d.Id, out var refs) ? refs.Count : 0,
            awaitingDays = awaiting?.DaysWaiting, createdAt = d.CreatedAt, updatedAt = d.UpdatedAt, version = d.Version,
            pendingChecks = s.PendingChecks.TryGetValue(d.Id, out var pc) ? pc : 0, scanIssues = d.ScanQuality != "",
        };
    }
}
