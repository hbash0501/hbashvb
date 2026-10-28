using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

/// <summary>
/// حذف المستندات: إلى المحذوفات أولاً (يختفي من الأرشيف ويبقى قابلاً للاستعادة برقمه)، والحذف النهائي لمدير النظام وحده
/// ويحذف الملف الأصلي إن لم يشاركه مستند آخر. كل خطوة في سجل التدقيق.
/// </summary>
public class TrashTests
{
    private const string Letter = """
        الرقم: 7781
        التاريخ: 1448/02/10هـ
        الموضوع: طلب تزويدنا بجدول الصيانة الدورية
        نأمل تزويدنا بجدول الصيانة الدورية للمباني خلال (10) أيام من تاريخه.
        """;

    [Fact]
    public async Task Deleted_document_leaves_the_archive_and_can_be_restored_or_purged()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var bytes = Encoding.UTF8.GetBytes(Letter);
        var up = await Upload(admin, "letter.txt", bytes);
        await WaitIdle(admin);
        var id = up.GetProperty("id").GetString()!;
        var doc = (await Get(admin, "/api/documents/" + id)).GetProperty("document");
        var archiveNo = doc.GetProperty("archiveNo").GetString();
        var version = doc.GetProperty("version").GetInt32();

        // موظف الأرشفة لا يحذف؛ ومدير الأرشيف يحذف ولا يحذف نهائياً.
        var clerk = await f.UserAsync(admin, "clerk1", "موظف أرشفة");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(clerk, HttpMethod.Post, $"/api/documents/{id}/delete", new { version })).StatusCode);
        Assert.False((await Get(clerk, "/api/documents/" + id)).GetProperty("can").GetProperty("delete").GetBoolean());
        var manager = await f.UserAsync(admin, "manager1", "مدير الأرشيف");
        Assert.True((await Get(manager, "/api/documents/" + id)).GetProperty("can").GetProperty("delete").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(manager, HttpMethod.Post, $"/api/documents/{id}/delete", new { version = version - 1 })).StatusCode);
        await Json(await Send(manager, HttpMethod.Post, $"/api/documents/{id}/delete", new { version, reason = "إدراج مكرر" }));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/documents/" + id)).StatusCode);
        Assert.Equal(0, (await Get(admin, "/api/documents")).GetProperty("total").GetInt32());
        var trash = await Get(manager, "/api/trash");
        var item = Assert.Single(trash.GetProperty("items").EnumerateArray());
        Assert.Equal("إدراج مكرر", item.GetProperty("reason").GetString());
        Assert.Equal("manager1", item.GetProperty("deletedBy").GetString());
        Assert.False(trash.GetProperty("canPurge").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(manager, HttpMethod.Post, $"/api/trash/{id}/purge")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/api/trash")).StatusCode);

        // الاستعادة برقم الأرشيف نفسه وبالنص والسجل.
        await Json(await Send(manager, HttpMethod.Post, $"/api/trash/{id}/restore"));
        var back = await Get(admin, "/api/documents/" + id);
        Assert.Equal(archiveNo, back.GetProperty("document").GetProperty("archiveNo").GetString());
        Assert.Contains("الصيانة الدورية", back.GetProperty("text").GetString());
        Assert.Contains(back.GetProperty("revisions").EnumerateArray(), r => r.GetProperty("summary").GetString() == "استعادة من المحذوفات");
        Assert.Empty((await Get(admin, "/api/trash")).GetProperty("items").EnumerateArray());

        // الحذف النهائي: يزول المستند وملفه، ويبقى أثره في سجل التدقيق.
        version = back.GetProperty("document").GetProperty("version").GetInt32();
        await Json(await Send(admin, HttpMethod.Post, $"/api/documents/{id}/delete", new { version }));
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var stored = Path.Combine(f.Data, "files", sha[..2], sha);
        Assert.True(File.Exists(stored));
        await Json(await Send(admin, HttpMethod.Post, $"/api/trash/{id}/purge"));
        Assert.False(File.Exists(stored));
        Assert.Empty((await Get(admin, "/api/trash")).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await Send(admin, HttpMethod.Post, $"/api/trash/{id}/restore")).StatusCode);
        var audit = await admin.GetStringAsync("/api/audit?take=50");
        foreach (var action in new[] { "نقل مستند إلى المحذوفات", "استعادة مستند من المحذوفات", "حذف نهائي لمستند" }) Assert.Contains(action, audit);
        Assert.True((await Get(admin, "/api/audit?take=1")).GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Purge_keeps_a_file_still_used_by_another_document()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var bytes = Encoding.UTF8.GetBytes(Letter);
        var a = (await Upload(admin, "a.txt", bytes)).GetProperty("id").GetString()!;
        await WaitIdle(admin);
        var va = (await Get(admin, "/api/documents/" + a)).GetProperty("document").GetProperty("version").GetInt32();
        await Json(await Send(admin, HttpMethod.Post, $"/api/documents/{a}/delete", new { version = va }));
        // الملف نفسه يُدرج من جديد بعد حذف الأول (لا يعدّه الإدراج مكرراً لأن الأول في المحذوفات).
        var b = (await Upload(admin, "b.txt", bytes)).GetProperty("id").GetString()!;
        await WaitIdle(admin);
        await Json(await Send(admin, HttpMethod.Post, $"/api/trash/{a}/purge"));
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.True(File.Exists(Path.Combine(f.Data, "files", sha[..2], sha)));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/documents/{b}/file")).StatusCode);
    }
}
