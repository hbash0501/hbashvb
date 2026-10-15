using System.Text;
using AIDocumentArchiveVB;
using Basira.Web.Services;

namespace Basira.Web;

public static class PortalApi
{
    public const string CookieName = "basira_session";
    private sealed record LoginRequest(string? UserName, string? Password);

    private static readonly string[] Permissions = { "view", "search", "export", "audit", "workflow" };
    private static readonly Dictionary<string, string> Filters = new()
    {
        ["all"] = "الكل", ["review"] = "قيد المراجعة", ["overdue"] = "متأخرة", ["week"] = "هذا الأسبوع", ["ocr"] = "بلا نص", ["sensitive"] = "حساسة",
    };

    private static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "?";

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        // حماية من CSRF: طلبات التعديل يجب أن تحمل ترويسة لا تُرسَل من نماذج الصفحات الخارجية.
        api.AddEndpointFilter(async (ctx, next) =>
        {
            var r = ctx.HttpContext.Request;
            if (!HttpMethods.IsGet(r.Method) && !HttpMethods.IsHead(r.Method) && r.Headers["X-Requested-With"] != "basira")
                return Results.Json(new { error = "طلب غير مسموح." }, statusCode: 403);
            return await next(ctx);
        });

        api.MapPost("/login", (LoginRequest? body, HttpContext ctx, UserDirectory users, SessionTokens tokens, LoginThrottle throttle, AccessLog log, PortalOptions opt) =>
        {
            var name = (body?.UserName ?? "").Trim();
            var pass = body?.Password ?? "";
            if (name.Length is 0 or > 80 || pass.Length is 0 or > 200) return Results.Json(new { error = "أدخل اسم المستخدم وكلمة المرور." }, statusCode: 400);
            if (users.NeedsSetup) return Results.Json(new { error = "لم يُعدّ حساب المدير بعد. أنشئه من التطبيق المكتبي أو بالأمر --create-admin." }, statusCode: 409);
            var ip = Ip(ctx);
            if (throttle.LockedFor(name, ip) is { } wait)
            {
                ctx.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();
                return Results.Json(new { error = $"تجاوزت عدد المحاولات. أعد المحاولة بعد {(int)Math.Ceiling(wait.TotalMinutes)} دقيقة." }, statusCode: 429);
            }
            var user = users.Verify(name, pass);
            if (user is null)
            {
                throttle.RecordFailure(name, ip);
                log.Append(name, "فشل دخول", "العنوان: " + ip);
                return Results.Json(new { error = "اسم المستخدم أو كلمة المرور غير صحيحة، أو أن الحساب غير نشط." }, statusCode: 401);
            }
            throttle.RecordSuccess(name, ip);
            ctx.Response.Cookies.Append(CookieName, tokens.Issue(user.UserName), new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = ctx.Request.IsHttps || opt.ForceSecureCookies,
                MaxAge = opt.SessionLifetime, Path = "/",
            });
            log.Append(user.UserName, "دخول", "العنوان: " + ip);
            return Results.Ok(Me(user));
        });

        api.MapPost("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
            return Results.Ok(new { ok = true });
        });

        var secured = api.MapGroup("").AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            var claims = http.RequestServices.GetRequiredService<SessionTokens>().Validate(http.Request.Cookies[CookieName]);
            var user = claims is null ? null : http.RequestServices.GetRequiredService<UserDirectory>().Find(claims.UserName);
            if (user is null) return Results.Json(new { error = "انتهت الجلسة. سجّل الدخول من جديد." }, statusCode: 401);
            http.Items["user"] = user;
            return await next(ctx);
        });

        secured.MapGet("/me", (HttpContext c) => Results.Ok(Me(User(c))));

        secured.MapGet("/categories", (ArchiveSnapshots s) =>
            Results.Ok(s.Current().Documents.Select(d => d.GeneralCategory).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)));

        secured.MapGet("/pulse", (ArchiveSnapshots s) =>
        {
            var snap = s.Current();
            return Results.Ok(new
            {
                metrics = snap.Metrics,
                categories = Count(snap.Documents.Select(d => Blank(d.GeneralCategory, "غير مصنّف"))),
                statuses = Count(snap.Documents.Select(d => Blank(d.Status, "غير محددة"))),
                findings = snap.Findings.Take(60).Select(f => new { f.DocumentId, f.Title, f.Kind, f.Severity, f.Reason, due = f.Due }),
                findingsTotal = snap.Findings.Count,
                loadedAt = snap.LoadedAt,
            });
        });

        secured.MapGet("/documents", (string? q, string? category, string? filter, int? limit, HttpContext c, ArchiveSnapshots s, AccessLog log) =>
        {
            var user = User(c);
            if (!UserDirectory.Can(user, "search")) return Results.Json(new { error = "لا تملك صلاحية البحث." }, statusCode: 403);
            q = (q ?? "").Trim();
            if (q.Length > 200) q = q[..200];
            var f = filter is not null && Filters.ContainsKey(filter) ? filter : "all";
            var take = Math.Clamp(limit ?? 50, 1, 100);
            var snap = s.Current();
            var hits = snap.Engine.Search(q, category ?? "", f, take);
            if (q.Length > 0) log.Append(user.UserName, "بحث", q);
            var privileged = UserDirectory.CanReadSensitive(user);
            return Results.Ok(new
            {
                total = hits.Count,
                items = hits.Select(h => Summary(h.Document, privileged, h.Excerpt, h.Matches, h.Score)),
            });
        });

        secured.MapGet("/documents/{id}", (string id, HttpContext c, ArchiveSnapshots s, AccessLog log) =>
        {
            var user = User(c);
            var snap = s.Current();
            if (!snap.ById.TryGetValue(id, out var d)) return Results.Json(new { error = "المستند غير موجود." }, statusCode: 404);
            var privileged = UserDirectory.CanReadSensitive(user);
            var restricted = BasiraEngine.IsSensitive(d) && !privileged;
            log.Append(user.UserName, "عرض مستند", d.ArchiveNumber + " | " + d.Title);
            return Results.Ok(new
            {
                summary = Summary(d, privileged, "", "", 0),
                restricted,
                text = restricted ? "" : Limit(d.TextContent, 60000),
                entities = restricted ? "" : string.Join("، ", new[] { d.OcrNames, d.OcrEntities }.Where(x => !string.IsNullOrWhiteSpace(x))),
                links = snap.Engine.Links(id).Select(l => new { l.Document.Id, l.Document.Title, l.Document.ArchiveNumber, l.Reason }),
            });
        });

        secured.MapGet("/ask", (string? q, HttpContext c, ArchiveSnapshots s, AccessLog log) =>
        {
            var user = User(c);
            if (!UserDirectory.Can(user, "search")) return Results.Json(new { error = "لا تملك صلاحية البحث." }, statusCode: 403);
            q = (q ?? "").Trim();
            if (q.Length is 0) return Results.Json(new { error = "اكتب سؤالاً." }, statusCode: 400);
            if (q.Length > 300) q = q[..300];
            log.Append(user.UserName, "سؤال", q);
            var answer = s.Current().Engine.LocalAnswer(q);
            var privileged = UserDirectory.CanReadSensitive(user);
            var evidence = answer.Evidence.Select(e => new
            {
                e.Number, e.DocumentId, e.Title, e.Reference, e.Offset, e.Sensitive,
                restricted = e.Sensitive && !privileged,
                quote = e.Sensitive && !privileged ? "" : e.Quote,
            }).ToList();
            return Results.Ok(new { question = q, mode = answer.Mode, found = evidence.Count > 0, evidence });
        });

        secured.MapGet("/report", (HttpContext c, ArchiveSnapshots s, AccessLog log) =>
        {
            var user = User(c);
            if (!UserDirectory.Can(user, "export")) return Results.Json(new { error = "لا تملك صلاحية التصدير." }, statusCode: 403);
            var snap = s.Current();
            log.Append(user.UserName, "تقرير صحة الأرشيف", "");
            var html = ArchiveHealthReportService.Build(snap.Documents.ToList(), snap.Findings.ToList(), user.FullName, DateTime.Now);
            c.Response.Headers.ContentDisposition = "attachment; filename=\"Basira_Health_" + DateTime.Now.ToString("yyyyMMdd") + ".html\"";
            return Results.Text(html, "text/html", Encoding.UTF8);
        });

        secured.MapGet("/audit", (int? take, HttpContext c, AccessLog log) =>
        {
            var user = User(c);
            if (!UserDirectory.Can(user, "audit")) return Results.Json(new { error = "لا تملك صلاحية سجل التدقيق." }, statusCode: 403);
            var (result, recent) = log.Verify(Math.Clamp(take ?? 50, 1, 200));
            return Results.Ok(new { valid = result.IsValid, count = result.Count, firstBrokenLine = result.FirstBrokenLine, message = result.Message, lastHash = result.LastHash, entries = recent });
        });
    }

    private static UserAccount User(HttpContext c) => (UserAccount)c.Items["user"]!;

    private static object Me(UserAccount u) => new
    {
        userName = u.UserName, fullName = u.FullName, role = u.RoleName,
        permissions = Permissions.Where(p => UserDirectory.Can(u, p)).ToArray(),
        sensitiveAccess = UserDirectory.CanReadSensitive(u),
    };

    private static string Blank(string? v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();

    private static string Limit(string? v, int max) => string.IsNullOrEmpty(v) ? "" : v.Length <= max ? v : v[..max];

    private static IEnumerable<object> Count(IEnumerable<string> values) =>
        values.GroupBy(v => v).OrderByDescending(g => g.Count()).Take(10).Select(g => new { name = g.Key, count = g.Count() });

    private static object Summary(DocumentRecord d, bool privileged, string excerpt, string matches, double score)
    {
        var sensitive = BasiraEngine.IsSensitive(d);
        var restricted = sensitive && !privileged;
        return new
        {
            id = d.Id, archiveNumber = d.ArchiveNumber, referenceNumber = d.ReferenceNumber, title = d.Title,
            category = d.GeneralCategory, subCategory = d.SubCategory, status = d.Status,
            source = d.SourceEntity, destination = d.DestinationEntity, due = d.DueDateText,
            confidentiality = d.Confidentiality, sensitive, restricted,
            summary = restricted ? "" : Limit(d.Summary, 400),
            tags = d.Tags, hasText = !string.IsNullOrWhiteSpace(d.TextContent),
            createdAt = d.CreatedAt, updatedAt = d.UpdatedAt,
            excerpt = restricted ? "" : Limit(excerpt, 400),
            matches = restricted ? "" : matches, score,
        };
    }
}
