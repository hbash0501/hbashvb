using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Encodings.Web;
using Basira.Platform.Api;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence;
using Basira.Platform.Intelligence.Semantic;
using Basira.Platform.Processing;
using Microsoft.Extensions.FileProviders;

const string DefaultUrl = "http://127.0.0.1:5280";
var builder = WebApplication.CreateBuilder(args);
var explicitUrls = builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var interactive = !args.Contains("--no-browser") && Environment.GetEnvironmentVariable("BASIRA_NO_BROWSER") != "1" && !args.Any(a => a.StartsWith("--create-admin") || a == "--import-legacy" || a == "--download-model");

// نسخة تعمل بالفعل على الجهاز: افتح نافذتها بدل تشغيل نسخة ثانية.
if (explicitUrls is null && interactive && PortInUse(5280))
{
    OpenBrowser(DefaultUrl);
    return 0;
}
if (explicitUrls is null) builder.WebHost.UseUrls(DefaultUrl);

var options = PlatformOptions.From(builder.Configuration);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = options.MaxUploadBytes * 4);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(f => f.MultipartBodyLengthLimit = options.MaxUploadBytes * 4);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<AuditStore>();
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<ReviewStore>();
builder.Services.AddSingleton<SemanticService>();
builder.Services.AddSingleton<SemanticDownloader>();
builder.Services.AddSingleton<ClassificationService>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<ArchiveIndex>();
builder.Services.AddSingleton<EventHub>();
builder.Services.AddSingleton<SessionTokens>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<ProcessingQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcessingQueue>());
builder.Services.AddSingleton<InboxService>();
builder.Services.AddSingleton<HotFolderService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HotFolderService>());
builder.Services.AddSingleton<LegacyImporter>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// أوامر سطر الأوامر للخوادم بلا واجهة.
if (args.Contains("--create-admin"))
{
    var users = app.Services.GetRequiredService<UserStore>();
    if (!users.NeedsSetup()) { Console.Error.WriteLine("تم إعداد المنصة مسبقاً."); return 1; }
    var pw = Environment.GetEnvironmentVariable("BASIRA_ADMIN_PASSWORD") ?? Console.ReadLine() ?? "";
    try { users.Create("admin", Environment.GetEnvironmentVariable("BASIRA_ADMIN_NAME") ?? "مدير النظام", Roles.Admin, pw, "سطر الأوامر"); Console.WriteLine("تم إنشاء حساب المدير admin."); return 0; }
    catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
}
if (Array.IndexOf(args, "--import-legacy") is var li and >= 0)
{
    if (li + 1 >= args.Length) { Console.Error.WriteLine("حدّد مجلد بيانات النسخة السابقة."); return 2; }
    var r = await app.Services.GetRequiredService<LegacyImporter>().ImportAsync(args[li + 1], "سطر الأوامر");
    Console.WriteLine($"مستندات: {r.Documents} (تخطي {r.Skipped})، ملفات: {r.Files} (مفقودة {r.MissingFiles})، مستخدمون: {r.Users}");
    return 0;
}
if (args.Contains("--download-model"))
{
    var semantic = app.Services.GetRequiredService<SemanticService>();
    var target = semantic.DownloadTarget();
    var downloader = app.Services.GetRequiredService<SemanticDownloader>();
    Console.WriteLine($"تنزيل النموذج الدلالي من {downloader.Source} إلى {target}");
    try { await downloader.DownloadAsync(target, Console.WriteLine, CancellationToken.None); return 0; }
    catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
    { Console.Error.WriteLine("تعذر التنزيل: " + ex.Message); return 3; }
}

// يُحمَّل النموذج الدلالي (إن وُضع بجوار المنصة) بعد التشغيل في الخلفية، فيُجهَّز ويُعاير دون انتظار أول مستند.
app.Lifetime.ApplicationStarted.Register(() => Task.Run(() => app.Services.GetRequiredService<SemanticService>().Model));

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
    h["X-Frame-Options"] = "SAMEORIGIN";
    // الملف الأصلي (PDF أو صورة أو نص) يُعرض في عارض المتصفح المدمج الذي يحتاج أنماطه الداخلية؛ أنواعه محصورة ومعها nosniff.
    var isOriginal = ctx.Request.Path.StartsWithSegments("/api/documents") && ctx.Request.Path.Value!.EndsWith("/file", StringComparison.Ordinal);
    if (!isOriginal) h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; frame-src 'self'; object-src 'none'; frame-ancestors 'self'; base-uri 'none'; form-action 'self'";
    if (ctx.Request.Path.StartsWithSegments("/api")) h["Cache-Control"] = "no-store";
    await next();
});

var web = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = web });
app.UseStaticFiles(new StaticFileOptions { FileProvider = web });

var api = app.MapGroup("/api");
// حماية CSRF: كل طلب تعديل يحمل ترويسة لا ترسلها نماذج المواقع الأخرى.
api.AddEndpointFilter(async (ctx, next) =>
{
    var r = ctx.HttpContext.Request;
    if (!HttpMethods.IsGet(r.Method) && !HttpMethods.IsHead(r.Method) && r.Headers["X-Requested-With"] != "basira")
        return Results.Json(new { error = "طلب غير مسموح." }, statusCode: 403);
    return await next(ctx);
});
AuthApi.Map(api);
var secured = api.MapGroup("").Secured();
DocumentsApi.Map(secured);
IntelligenceApi.Map(secured);
InboxApi.Map(secured);
AdminApi.Map(secured);
ReviewApi.Map(secured);
ClassificationApi.Map(secured);
app.MapGet("/healthz", () => Results.Text("ok"));
app.MapFallback(async ctx =>
{
    if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 404; return; }
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await using var s = web.GetFileInfo("index.html").CreateReadStream();
    await s.CopyToAsync(ctx.Response.Body);
});

if (interactive && explicitUrls is null && OperatingSystem.IsWindows())
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(DefaultUrl));
app.Logger.LogInformation("بصيرة تعمل على {Urls}، والبيانات في {Data}", explicitUrls ?? DefaultUrl, options.DataFolder);
await app.RunAsync();
return 0;

static bool PortInUse(int port)
{
    try { using var c = new TcpClient(); return c.ConnectAsync("127.0.0.1", port).Wait(300) && c.Connected; }
    catch (Exception) { return false; }
}

static void OpenBrowser(string url)
{
    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
    catch (Exception) { Console.WriteLine("افتح المتصفح على " + url); }
}

public partial class Program { }
