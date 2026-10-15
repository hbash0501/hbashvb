using System.Text.Encodings.Web;
using Basira.Web;
using Basira.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var options = PortalOptions.From(builder.Configuration);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionTokens>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<AccessLog>();
builder.Services.AddSingleton<ArchiveSnapshots>();
builder.Services.AddSingleton<UserDirectory>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// إنشاء حساب المدير الأول على خادم بلا واجهة: BASIRA_ADMIN_PASSWORD=... dotnet Basira.Web.dll --create-admin
if (args.Contains("--create-admin"))
{
    var pw = Environment.GetEnvironmentVariable("BASIRA_ADMIN_PASSWORD") ?? Console.ReadLine() ?? "";
    var users = app.Services.GetRequiredService<UserDirectory>();
    if (!users.NeedsSetup) { Console.Error.WriteLine("تم إعداد المنصة مسبقاً."); return 1; }
    try { users.CreateInitialAdmin(pw); Console.WriteLine("تم إنشاء حساب المدير admin."); return 0; }
    catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
}

if (options.SessionKeyIsEphemeral)
    app.Logger.LogWarning("لم يُضبط BASIRA_SESSION_SECRET؛ تنتهي الجلسات عند إعادة تشغيل الخادم.");

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (ctx.Request.Path.StartsWithSegments("/api")) h["Cache-Control"] = "no-store";
    await next();
});

// الملفات الثابتة تُخدَّم من مجلد التطبيق نفسه بغض النظر عن مجلد التشغيل الحالي.
var webRoot = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webRoot });
app.UseStaticFiles(new StaticFileOptions { FileProvider = webRoot });
PortalApi.Map(app);
app.MapGet("/healthz", () => Results.Text("ok"));
app.Run();
return 0;

public partial class Program { }
