using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIDocumentArchiveVB;
using Basira.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Basira.Web.Tests;

/// <summary>أرشيف مؤقت بمستخدمين ومستندات (أحدها حساس) وخادم اختبار كامل.</summary>
public sealed class PortalFixture : IDisposable
{
    public string Data { get; } = Path.Combine(Path.GetTempPath(), "basira-web-" + Guid.NewGuid().ToString("N"));
    public WebApplicationFactory<Program> Factory { get; }
    public const string AdminPassword = "Correct-Horse-Battery-9";

    public PortalFixture()
    {
        var settings = new AppSettings { BaseDataFolder = Data, ArchiveRootFolder = Path.Combine(Data, "files"), CurrentEmployeeName = "اختبار" };
        var repo = new ArchiveRepository(settings);
        var auth = new AuthService(settings);
        auth.CreateInitialAdmin(AdminPassword);
        auth.SaveUser(new UserAccount { UserName = "reader", FullName = "قارئ", RoleName = "قارئ" }, "Reader-Pass-2026!");
        repo.SaveDocument(new DocumentRecord { Title = "عقد صيانة الأنظمة", GeneralCategory = "عقود", Status = "قيد المراجعة", ArchiveNumber = "ARC-1", TextContent = "يرجى مراجعة عقد الصيانة قبل الموعد", Confidentiality = "عادي", SourceEntity = "إدارة العقود" });
        repo.SaveDocument(new DocumentRecord { Title = "محضر تحقيق سري", GeneralCategory = "سري", Status = "قيد المراجعة", ArchiveNumber = "ARC-2", TextContent = "محتوى سري للغاية عن التحقيق", Confidentiality = "سري" });
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Basira:DataFolder", Data);
            b.UseSetting("Basira:SessionSecret", "test-secret-value");
        });
    }

    public HttpClient Client() => Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    public static async Task<HttpResponseMessage> Login(HttpClient c, string user, string password)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/login") { Content = JsonContent.Create(new { userName = user, password }) };
        req.Headers.Add("X-Requested-With", "basira");
        return await c.SendAsync(req);
    }

    public void Dispose()
    {
        Factory.Dispose();
        try { Directory.Delete(Data, true); } catch (IOException) { }
    }
}

public class PortalTests : IClassFixture<PortalFixture>
{
    private readonly PortalFixture _f;
    public PortalTests(PortalFixture f) => _f = f;

    [Fact]
    public async Task Api_requires_a_session()
    {
        var c = _f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/pulse")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/documents?q=عقد")).StatusCode);
    }

    [Fact]
    public async Task Login_rejects_bad_credentials_and_accepts_good_ones()
    {
        var c = _f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await PortalFixture.Login(c, "admin", "wrong-password-1")).StatusCode);
        var ok = await PortalFixture.Login(c, "admin", PortalFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("HttpOnly", ok.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", ok.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/pulse")).StatusCode);
    }

    [Fact]
    public async Task Post_without_csrf_header_is_rejected()
    {
        var c = _f.Client();
        var r = await c.PostAsJsonAsync("/api/login", new { userName = "admin", password = PortalFixture.AdminPassword });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Search_finds_documents_with_arabic_normalization()
    {
        var c = _f.Client();
        await PortalFixture.Login(c, "admin", PortalFixture.AdminPassword);
        var json = await c.GetFromJsonAsync<JsonElement>("/api/documents?q=" + Uri.EscapeDataString("عقود الصيانه"));
        Assert.Contains(json.GetProperty("items").EnumerateArray(), i => i.GetProperty("archiveNumber").GetString() == "ARC-1");
    }

    [Fact]
    public async Task Sensitive_text_is_hidden_from_readers_and_visible_to_admins()
    {
        var reader = _f.Client();
        Assert.Equal(HttpStatusCode.OK, (await PortalFixture.Login(reader, "reader", "Reader-Pass-2026!")).StatusCode);
        var list = await reader.GetFromJsonAsync<JsonElement>("/api/documents?q=" + Uri.EscapeDataString("تحقيق"));
        var item = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("archiveNumber").GetString() == "ARC-2");
        Assert.True(item.GetProperty("restricted").GetBoolean());
        Assert.Equal("", item.GetProperty("excerpt").GetString());
        var detail = await reader.GetFromJsonAsync<JsonElement>("/api/documents/" + item.GetProperty("id").GetString());
        Assert.Equal("", detail.GetProperty("text").GetString());
        var ask = await reader.GetFromJsonAsync<JsonElement>("/api/ask?q=" + Uri.EscapeDataString("تحقيق"));
        Assert.DoesNotContain("للغاية", ask.GetRawText());

        var admin = _f.Client();
        await PortalFixture.Login(admin, "admin", PortalFixture.AdminPassword);
        var adminDetail = await admin.GetFromJsonAsync<JsonElement>("/api/documents/" + item.GetProperty("id").GetString());
        Assert.Contains("سري للغاية", adminDetail.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Permissions_are_enforced_per_role()
    {
        var reader = _f.Client();
        await PortalFixture.Login(reader, "reader", "Reader-Pass-2026!");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/report")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/audit")).StatusCode);
        var admin = _f.Client();
        await PortalFixture.Login(admin, "admin", PortalFixture.AdminPassword);
        var report = await admin.GetAsync("/api/report");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Contains("تقرير صحة الأرشيف", await report.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Access_log_chain_is_valid_and_security_headers_are_present()
    {
        var c = _f.Client();
        var login = await PortalFixture.Login(c, "admin", PortalFixture.AdminPassword);
        Assert.True(login.IsSuccessStatusCode);
        await c.GetAsync("/api/documents?q=عقد");
        var audit = await c.GetFromJsonAsync<JsonElement>("/api/audit");
        Assert.True(audit.GetProperty("valid").GetBoolean());
        Assert.True(audit.GetProperty("count").GetInt32() >= 2);
        var resp = await c.GetAsync("/api/pulse");
        Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'self'", resp.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("no-store", resp.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Static_ui_is_served()
    {
        var c = _f.Client();
        var html = await c.GetStringAsync("/");
        Assert.Contains("بصيرة", html);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/app.js")).StatusCode);
    }
}

public class SessionTokenTests
{
    private static PortalOptions Opts() => new() { SessionKey = new byte[32].Select((_, i) => (byte)(i + 1)).ToArray(), SessionLifetime = TimeSpan.FromHours(1) };

    [Fact]
    public void Token_round_trips_and_expires()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-15T10:00:00Z"));
        var tokens = new SessionTokens(Opts(), time);
        var t = tokens.Issue("admin");
        Assert.Equal("admin", tokens.Validate(t)?.UserName);
        time.Advance(TimeSpan.FromHours(2));
        Assert.Null(tokens.Validate(t));
    }

    [Fact]
    public void Tampered_or_foreign_tokens_are_rejected()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-15T10:00:00Z"));
        var tokens = new SessionTokens(Opts(), time);
        var t = tokens.Issue("reader");
        var parts = t.Split('.');
        var forged = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"UserName\":\"admin\",\"ExpiresUnix\":9999999999}")).TrimEnd('=') + "." + parts[1];
        Assert.Null(tokens.Validate(forged));
        Assert.Null(tokens.Validate(t + "x"));
        var other = new SessionTokens(new PortalOptions { SessionKey = new byte[32], SessionLifetime = TimeSpan.FromHours(1) }, time);
        Assert.Null(other.Validate(t));
        Assert.Null(tokens.Validate(""));
        Assert.Null(tokens.Validate("garbage"));
    }
}

public class LoginThrottleTests
{
    [Fact]
    public void Locks_after_max_failures_and_unlocks_after_window()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-15T10:00:00Z"));
        var throttle = new LoginThrottle(new PortalOptions { MaxFailedLogins = 3, LockoutWindow = TimeSpan.FromMinutes(10) }, time);
        for (var i = 0; i < 2; i++) throttle.RecordFailure("Admin", "1.1.1.1");
        Assert.Null(throttle.LockedFor("admin", "1.1.1.1"));
        throttle.RecordFailure("admin", "1.1.1.1");
        Assert.NotNull(throttle.LockedFor("ADMIN", "1.1.1.1"));
        Assert.Null(throttle.LockedFor("admin", "2.2.2.2"));
        time.Advance(TimeSpan.FromMinutes(11));
        Assert.Null(throttle.LockedFor("admin", "1.1.1.1"));
    }

    [Fact]
    public void Success_clears_failures()
    {
        var time = new FakeTimeProvider();
        var throttle = new LoginThrottle(new PortalOptions { MaxFailedLogins = 3 }, time);
        throttle.RecordFailure("u", "ip"); throttle.RecordFailure("u", "ip");
        throttle.RecordSuccess("u", "ip");
        throttle.RecordFailure("u", "ip");
        Assert.Null(throttle.LockedFor("u", "ip"));
    }
}
