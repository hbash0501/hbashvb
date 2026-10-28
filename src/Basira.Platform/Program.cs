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
var interactive = !args.Contains("--no-browser") && Environment.GetEnvironmentVariable("BASIRA_NO_BROWSER") != "1" && !args.Any(a => a.StartsWith("--create-admin") || a == "--import-legacy" || a == "--download-model" || a == "--restore");

// نسخة تعمل بالفعل على الجهاز: من الإصدار نفسه أو أحدث تُفتح نافذتها بدل تشغيل نسخة ثانية.
// ومن إصدار أقدم (بقيت تعمل في الخلفية بعد تنزيل الجديد) تُغلق لتعمل هذه، وإلا فتح المتصفح الإصدار القديم.
if (explicitUrls is null && interactive && PortInUse(5280))
{
    var running = await RunningVersionAsync();
    var mine = Version.Parse(AuthApi.AppVersion);
    if (running is not null && running >= mine) { OpenBrowser(DefaultUrl); return 0; }
    Console.WriteLine($"يعمل إصدار أقدم من بصيرة ({running?.ToString(3) ?? "قبل 3.7.1"})؛ يُغلق لتشغيل الإصدار {AuthApi.AppVersion}.");
    StopOtherInstances();
    for (var i = 0; i < 40 && PortInUse(5280); i++) Thread.Sleep(250);
    if (PortInUse(5280))
    {
        Console.WriteLine("تعذر إغلاق النسخة الأقدم: أغلق نافذتها (أو أنهِ Basira.exe من مدير المهام) ثم شغّل البرنامج من جديد.");
        OpenBrowser(DefaultUrl);
        return 1;
    }
}
if (explicitUrls is null) builder.WebHost.UseUrls(DefaultUrl);

var options = PlatformOptions.From(builder.Configuration);

// استعادة نسخة احتياطية: من سطر الأوامر، أو مجهزة من الإدارة؛ تُطبَّق قبل فتح قاعدة البيانات.
if (Array.IndexOf(args, "--restore") is var ri and >= 0)
{
    if (ri + 1 >= args.Length) { Console.Error.WriteLine("حدّد ملف النسخة الاحتياطية."); return 2; }
    if (PortInUse(5280)) { Console.Error.WriteLine("أغلق بصيرة أولاً ثم أعد الأمر."); return 2; }
    try { BackupService.Inspect(args[ri + 1]); }
    catch (Exception ex) when (ex is InvalidDataException or IOException) { Console.Error.WriteLine(ex.Message); return 2; }
    Directory.CreateDirectory(options.DataFolder);
    File.Copy(args[ri + 1], Path.Combine(options.DataFolder, "restore-pending.zip"), overwrite: true);
    Console.WriteLine(BackupService.ApplyPending(options));
    return 0;
}
BackupService.RestoredMessage = BackupService.ApplyPending(options);
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
builder.Services.AddSingleton<CommitmentStore>();
builder.Services.AddSingleton<SemanticService>();
builder.Services.AddSingleton<SemanticDownloader>();
builder.Services.AddSingleton<Basira.Platform.Processing.Ocr.OcrBenchmark>();
builder.Services.AddSingleton<ClassificationService>();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<ArchiveIndex>();
builder.Services.AddSingleton<EventHub>();
builder.Services.AddSingleton<SessionTokens>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<ProcessingQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcessingQueue>());
builder.Services.AddSingleton<InboxService>();
builder.Services.AddSingleton<Basira.Platform.Processing.Scanning.ScanService>();
builder.Services.AddSingleton<HotFolderService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HotFolderService>());
builder.Services.AddSingleton<LegacyImporter>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupScheduler>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();
if (BackupService.RestoredMessage is { } restored) app.Services.GetRequiredService<AuditStore>().Append("النظام", "استعادة نسخة احتياطية", restored);

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
// الواجهة تُعاد مطابقتها مع الخادم في كل تحميل (ETag، فالرد غالباً 304 بلا محتوى)، حتى لا يبقى المتصفح على نصوص إصدار سابق بعد الترقية.
app.UseStaticFiles(new StaticFileOptions { FileProvider = web, OnPrepareResponse = f => f.Context.Response.Headers.CacheControl = "no-cache" });

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
ScanApi.Map(secured);
AdminApi.Map(secured);
ReviewApi.Map(secured);
ClassificationApi.Map(secured);
ReplyApi.Map(secured);
CommitmentsApi.Map(secured);
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

// إصدار النسخة العاملة على المنفذ الافتراضي، أو null إن كانت أقدم من أن تعلنه.
static async Task<Version?> RunningVersionAsync()
{
    try
    {
        using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(3) };
        using var d = System.Text.Json.JsonDocument.Parse(await http.GetStringAsync(DefaultUrl + "/api/setup"));
        return d.RootElement.TryGetProperty("version", out var v) && Version.TryParse(v.GetString(), out var parsed) ? parsed : null;
    }
    catch (Exception) { return null; }
}

static void StopOtherInstances()
{
    var self = Process.GetCurrentProcess();
    foreach (var p in Process.GetProcessesByName(self.ProcessName).Where(p => p.Id != self.Id))
        try { p.Kill(); p.WaitForExit(5000); }
        catch (Exception) { }
}

static void OpenBrowser(string url)
{
    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
    catch (Exception) { Console.WriteLine("افتح المتصفح على " + url); }
}

public partial class Program { }
