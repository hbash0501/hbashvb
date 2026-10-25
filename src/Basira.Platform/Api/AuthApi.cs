using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

public static class AuthApi
{
    private sealed record LoginBody(string? UserName, string? Password);
    private sealed record SetupBody(string? FullName, string? UserName, string? Password, string? Organization);
    private static readonly string[] PermissionNames = { "view", "search", "import", "edit", "workflow", "export", "audit", "ai" };

    public static object Me(User u) => new
    {
        userName = u.UserName, fullName = u.FullName, role = u.Role, isAdmin = UserStore.IsAdmin(u),
        permissions = PermissionNames.Where(p => UserStore.Can(u, p)).ToArray(), sensitiveAccess = UserStore.CanReadSensitive(u),
    };

    private static void SignIn(HttpContext ctx, User u, SessionTokens tokens, PlatformOptions o) =>
        ctx.Response.Cookies.Append(Cookie, tokens.Issue(u.UserName, u.Stamp), new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = ctx.Request.IsHttps || o.ForceSecureCookies, MaxAge = o.SessionLifetime, Path = "/",
        });

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/setup", (UserStore users, SettingsStore settings) => Results.Ok(new { needsSetup = users.NeedsSetup(), organization = settings.Get("organization") }));

        api.MapPost("/setup", (SetupBody b, HttpContext ctx, UserStore users, SettingsStore settings, SessionTokens tokens, PlatformOptions o) =>
        {
            if (!users.NeedsSetup()) return Error("تم إعداد المنصة مسبقاً.", 409);
            try
            {
                var u = users.Create(b.UserName ?? "", b.FullName ?? "", Roles.Admin, b.Password ?? "", b.UserName ?? "");
                if (!string.IsNullOrWhiteSpace(b.Organization)) settings.Set(new Dictionary<string, string> { ["organization"] = b.Organization! }, u.UserName);
                SignIn(ctx, u, tokens, o);
                return Results.Ok(Me(u));
            }
            catch (ArgumentException ex) { return Error(ex.Message); }
        });

        api.MapPost("/login", (LoginBody? b, HttpContext ctx, UserStore users, SessionTokens tokens, LoginThrottle throttle, AuditStore audit, PlatformOptions o) =>
        {
            var name = (b?.UserName ?? "").Trim();
            var pass = b?.Password ?? "";
            if (name.Length is 0 or > 60 || pass.Length is 0 or > 200) return Error("أدخل اسم المستخدم وكلمة المرور.");
            if (users.NeedsSetup()) return Error("لم يُعدّ حساب المدير بعد.", 409);
            var ip = Ip(ctx);
            if (throttle.LockedFor(name, ip) is { } wait)
            {
                ctx.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();
                return Error($"تجاوزت عدد المحاولات. أعد المحاولة بعد {(int)Math.Ceiling(wait.TotalMinutes)} دقيقة.", 429);
            }
            var u = users.Verify(name, pass);
            if (u is null)
            {
                throttle.Failure(name, ip);
                audit.Append(name, "فشل دخول", "العنوان: " + ip);
                return Error("اسم المستخدم أو كلمة المرور غير صحيحة، أو أن الحساب موقوف.", 401);
            }
            throttle.Success(name, ip);
            SignIn(ctx, u, tokens, o);
            audit.Append(u.UserName, "دخول", "العنوان: " + ip);
            return Results.Ok(Me(u));
        });

        api.MapPost("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(Cookie, new CookieOptions { Path = "/" });
            return Results.Ok(new { ok = true });
        });

        var secured = api.MapGroup("").Secured();
        secured.MapGet("/me", (HttpContext c, SettingsStore s) => Results.Ok(new { user = Me(U(c)), organization = s.Get("organization") }));
    }
}
