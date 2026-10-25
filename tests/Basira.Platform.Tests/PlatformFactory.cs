using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Basira.Platform.Tests;

/// <summary>منصة كاملة بقاعدة بيانات وطابور معالجة حقيقيين في مجلد مؤقت.</summary>
public sealed class PlatformFactory : WebApplicationFactory<Program>
{
    public string Data { get; } = Path.Combine(Path.GetTempPath(), "basira-platform-" + Guid.NewGuid().ToString("N"));
    public const string AdminPassword = "Correct-Horse-Battery-9";

    public PlatformFactory()
    {
        Environment.SetEnvironmentVariable("BASIRA_NO_BROWSER", "1");
        Directory.CreateDirectory(Data);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Basira:DataFolder", Data);
        builder.UseSetting("Basira:Workers", "2");
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(Data, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public async Task<HttpClient> AdminAsync()
    {
        var c = NewClient();
        var r = await Send(c, HttpMethod.Post, "/api/setup", new { fullName = "مدير المنصة", userName = "admin", password = AdminPassword, organization = "الإدارة العامة" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return c;
    }

    public async Task<HttpClient> UserAsync(HttpClient admin, string userName, string role)
    {
        var r = await Send(admin, HttpMethod.Post, "/api/admin/users", new { userName, fullName = userName, role, password = "Strong-Pass-2026!" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var c = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Post, "/api/login", new { userName, password = "Strong-Pass-2026!" })).StatusCode);
        return c;
    }

    public static Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Requested-With", "basira");
        if (body is not null) req.Content = JsonContent.Create(body);
        return c.SendAsync(req);
    }

    public static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<JsonElement> Get(HttpClient c, string url) => await Json(await c.GetAsync(url));

    public static async Task<JsonElement> Upload(HttpClient c, string fileName, byte[] content, string direction = "وارد", string confidentiality = "عادي", string category = "")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "files", fileName);
        form.Add(new StringContent(direction), "direction");
        form.Add(new StringContent(confidentiality), "confidentiality");
        if (category != "") form.Add(new StringContent(category), "category");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/inbox") { Content = form };
        req.Headers.Add("X-Requested-With", "basira");
        return (await Json(await c.SendAsync(req)))[0];
    }

    public static async Task WaitIdle(HttpClient c, int seconds = 90)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            var jobs = await Get(c, "/api/jobs?take=100");
            if (jobs.EnumerateArray().All(j => j.GetProperty("state").GetString() is "done" or "failed")) return;
            await Task.Delay(200);
        }
        throw new TimeoutException("لم تكتمل المعالجة في الوقت المحدد.");
    }

    public static byte[] Utf8(string s) => System.Text.Encoding.UTF8.GetBytes(s);
}
