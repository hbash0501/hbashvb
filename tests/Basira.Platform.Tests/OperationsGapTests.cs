using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Basira.Platform.Infrastructure;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>ما يحتاجه التشغيل اليومي: تغيير كلمة المرور، وتصدير القائمة، والإجراءات الجماعية، والنسخ الاحتياطي والاستعادة.</summary>
public class OperationsGapTests
{
    private static string Letter(string no, string subject) => $"""
        الرقم: {no}
        التاريخ: 1448/02/10هـ
        الموضوع: {subject}
        نأمل تزويدنا بالبيانات المطلوبة خلال (10) أيام من تاريخه.
        """;

    [Fact]
    public async Task Users_change_their_own_password_and_other_sessions_end()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var clerk = await f.UserAsync(admin, "clerk1", "موظف أرشفة");
        var other = f.NewClient();
        Assert.Equal(HttpStatusCode.OK, (await Send(other, HttpMethod.Post, "/api/login", new { userName = "clerk1", password = "Strong-Pass-2026!" })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(clerk, HttpMethod.Post, "/api/me/password", new { current = "wrong-password-1", next = "Brand-New-Pass-77" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(clerk, HttpMethod.Post, "/api/me/password", new { current = "Strong-Pass-2026!", next = "short" })).StatusCode);
        await Json(await Send(clerk, HttpMethod.Post, "/api/me/password", new { current = "Strong-Pass-2026!", next = "Brand-New-Pass-77" }));

        Assert.Equal(HttpStatusCode.OK, (await clerk.GetAsync("/api/me")).StatusCode); // جلسة جديدة لمن غيّرها
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/me")).StatusCode); // وانتهت الأخرى
        var fresh = f.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(fresh, HttpMethod.Post, "/api/login", new { userName = "clerk1", password = "Strong-Pass-2026!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(fresh, HttpMethod.Post, "/api/login", new { userName = "clerk1", password = "Brand-New-Pass-77" })).StatusCode);
        var audit = await admin.GetStringAsync("/api/audit?take=50");
        Assert.Contains("تغيير كلمة المرور", audit);
        Assert.Contains("فشل تغيير كلمة المرور", audit);
    }

    [Fact]
    public async Task Document_list_exports_to_excel_csv_and_bulk_actions_respect_permissions()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var ids = new List<string>();
        foreach (var (no, subject) in new[] { ("1101", "طلب تزويدنا بجدول الصيانة"), ("1102", "=HYPERLINK(\"http://x\") تقرير"), ("1103", "طلب بيانات الموظفين") })
            ids.Add((await Upload(admin, $"l{no}.txt", Encoding.UTF8.GetBytes(Letter(no, subject)))).GetProperty("id").GetString()!);
        await WaitIdle(admin);

        var r = await admin.GetAsync("/api/documents/export?filter=all");
        Assert.Equal("text/csv", r.Content.Headers.ContentType?.MediaType);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]); // BOM: يفتحه Excel بالعربية
        var csv = Encoding.UTF8.GetString(bytes[3..]);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length > 1 ? csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries) : csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        Assert.StartsWith("رقم الأرشيف,العنوان", lines[0]);
        Assert.Contains("طلب تزويدنا بجدول الصيانة", csv);
        Assert.DoesNotContain(",=HYPERLINK", csv); // لا تُنفَّذ صيغة في Excel
        var one = await admin.GetStringAsync("/api/documents/export?ids=" + ids[0]);
        Assert.Equal(2, one.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

        var reader = await f.UserAsync(admin, "reader1", "قارئ");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/documents/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, HttpMethod.Post, "/api/documents/bulk", new { ids, action = "status", value = "منجز" })).StatusCode);
        var clerk = await f.UserAsync(admin, "clerk1", "موظف أرشفة");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(clerk, HttpMethod.Post, "/api/documents/bulk", new { ids, action = "delete" })).StatusCode);

        var done = await Json(await Send(clerk, HttpMethod.Post, "/api/documents/bulk", new { ids = ids.Take(2), action = "status", value = "منجز" }));
        Assert.Equal(2, done.GetProperty("done").GetInt32());
        Assert.Equal("منجز", (await Get(admin, "/api/documents/" + ids[0])).GetProperty("document").GetProperty("status").GetString());
        await Json(await Send(clerk, HttpMethod.Post, "/api/documents/bulk", new { ids = ids.Take(1), action = "category", value = "الشؤون الإدارية" }));
        Assert.Equal("الشؤون الإدارية", (await Get(admin, "/api/documents/" + ids[0])).GetProperty("document").GetProperty("category").GetString());
        await Json(await Send(admin, HttpMethod.Post, "/api/documents/bulk", new { ids = ids.Skip(1), action = "delete", reason = "تجربة" }));
        Assert.Equal(1, (await Get(admin, "/api/documents")).GetProperty("total").GetInt32());
        Assert.Equal(2, (await Get(admin, "/api/trash")).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Backup_contains_everything_and_restore_is_applied_on_next_start()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var a = (await Upload(admin, "a.txt", Encoding.UTF8.GetBytes(Letter("2201", "طلب تزويدنا بالخطة السنوية")))).GetProperty("id").GetString()!;
        await WaitIdle(admin);

        // الإعدادات: المجلد مسار كامل، والنسخة إلى المجلد تحفظ الملف وتحذف الزائد على العدد.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["backup_folder"] = "relative\\path" })).StatusCode);
        var folder = Path.Combine(f.Data, "..", "basira-backups-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Json(await Send(admin, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["backup_folder"] = Path.GetFullPath(folder), ["backup_keep"] = "2" }));
            for (var i = 0; i < 3; i++) { await Json(await Send(admin, HttpMethod.Post, "/api/admin/backup/run")); await Task.Delay(1100); }
            Assert.Equal(2, Directory.GetFiles(folder, BackupService.Prefix + "*.zip").Length);
            var status = await Get(admin, "/api/admin/backup");
            Assert.True(status.GetProperty("last").GetProperty("size").GetInt64() > 0);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        // التنزيل: قاعدة بيانات سليمة وكل الملفات وبيان، دون مفتاح الأسرار.
        var zipBytes = await admin.GetByteArrayAsync("/api/admin/backup/download");
        var temp = Path.Combine(Path.GetTempPath(), "basira-test-backup-" + Guid.NewGuid().ToString("N") + ".zip");
        await File.WriteAllBytesAsync(temp, zipBytes);
        try
        {
            using (var zip = ZipFile.OpenRead(temp))
            {
                Assert.NotNull(zip.GetEntry("basira.db"));
                Assert.NotNull(zip.GetEntry("manifest.json"));
                Assert.Contains(zip.Entries, e => e.FullName.StartsWith("files/", StringComparison.Ordinal));
                Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("session.key"));
            }
            var manifest = BackupService.Inspect(temp);
            Assert.Equal(1, manifest.Documents);

            // ملف ليس نسخة: يُرفض.
            Assert.Equal(HttpStatusCode.BadRequest, (await PostFile(admin, "/api/admin/backup/restore", "x.zip", Encoding.UTF8.GetBytes("not a zip"))).StatusCode);

            // تغيير بعد النسخ، ثم تجهيز الاستعادة: لا يتغير شيء حتى إعادة التشغيل.
            var b = (await Upload(admin, "b.txt", Encoding.UTF8.GetBytes(Letter("2202", "طلب بيانات العقود")))).GetProperty("id").GetString()!;
            await WaitIdle(admin);
            var staged = await Json(await PostFile(admin, "/api/admin/backup/restore", "backup.zip", zipBytes));
            Assert.Equal(1, staged.GetProperty("documents").GetInt32());
            Assert.Equal(2, (await Get(admin, "/api/documents")).GetProperty("total").GetInt32());
            Assert.Equal(1, (await Get(admin, "/api/admin/backup")).GetProperty("pending").GetProperty("documents").GetInt32());

            // ما يحدث عند التشغيل التالي (والمنصة متوقفة): تُستعاد النسخة وتُحفظ البيانات السابقة جانباً.
            var opts = new PlatformOptions { DataFolder = f.Data };
            f.Dispose();
            var data = Path.Combine(Path.GetTempPath(), "basira-restore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(data);
            try
            {
                File.Copy(temp, Path.Combine(data, "restore-pending.zip"));
                File.WriteAllText(Path.Combine(data, "basira.db"), "current");
                var message = BackupService.ApplyPending(new PlatformOptions { DataFolder = data });
                Assert.Contains("استُعيدت", message);
                Assert.False(File.Exists(Path.Combine(data, "restore-pending.zip")));
                Assert.True(new FileInfo(Path.Combine(data, "basira.db")).Length > 1000);
                Assert.True(Directory.GetFiles(Path.Combine(data, "files"), "*", SearchOption.AllDirectories).Length >= 1);
                var aside = Assert.Single(Directory.GetDirectories(data, "before-restore-*"));
                Assert.Equal("current", File.ReadAllText(Path.Combine(aside, "basira.db")));
                Assert.Null(BackupService.ApplyPending(new PlatformOptions { DataFolder = data })); // لا استعادة مجهزة
            }
            finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(data, true); }
            _ = opts; _ = a; _ = b;
        }
        finally { File.Delete(temp); }
    }

    private static Task<HttpResponseMessage> PostFile(HttpClient c, string url, string name, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", name);
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        req.Headers.Add("X-Requested-With", "basira");
        return c.SendAsync(req);
    }
}
