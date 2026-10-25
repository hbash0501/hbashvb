using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using AIDocumentArchiveVB;
using Basira.Platform.Processing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Basira.Platform.Tests.PlatformFactory;

namespace Basira.Platform.Tests;

public class SetupAndSecurityTests
{
    [Fact]
    public async Task First_run_setup_creates_admin_once_and_requires_session()
    {
        using var f = new PlatformFactory();
        var anon = f.NewClient();
        Assert.True((await Get(anon, "/api/setup")).GetProperty("needsSetup").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/dashboard")).StatusCode);
        var weak = await Send(anon, HttpMethod.Post, "/api/setup", new { fullName = "م", userName = "admin", password = "admin123" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var admin = await f.AdminAsync();
        Assert.Equal("مدير النظام", (await Get(admin, "/api/me")).GetProperty("user").GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(f.NewClient(), HttpMethod.Post, "/api/setup", new { fullName = "x", userName = "x2", password = "Another-Strong-1" })).StatusCode);
    }

    [Fact]
    public async Task Csrf_header_is_required_and_security_headers_are_sent()
    {
        using var f = new PlatformFactory();
        var c = f.NewClient();
        var r = await c.PostAsync("/api/login", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var home = await c.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("default-src 'self'", home.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", home.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Password_change_or_deactivation_revokes_existing_sessions()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var reader = await f.UserAsync(admin, "reader1", "قارئ");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/me")).StatusCode);
        var id = (await Get(admin, "/api/admin/users")).EnumerateArray().Single(u => u.GetProperty("userName").GetString() == "reader1").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await Send(admin, HttpMethod.Put, "/api/admin/users/" + id, new { active = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Last_admin_cannot_be_demoted_and_non_admins_cannot_manage()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var adminId = (await Get(admin, "/api/admin/users")).EnumerateArray().Single().GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, HttpMethod.Put, "/api/admin/users/" + adminId, new { role = "قارئ" })).StatusCode);
        var clerk = await f.UserAsync(admin, "clerk", "موظف أرشفة");
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/api/admin/users")).StatusCode);
    }
}

public class DocumentTests
{
    [Fact]
    public async Task Text_upload_is_processed_with_header_fields_and_duplicates_are_rejected()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var letter = "وزارة الصحة\nالرقم: 3390\nالتاريخ: 1447/04/02هـ\nالموضوع: طلب تحديث بيانات المنشآت\n\nسعادة المدير العام المحترم\nنأمل التكرم بتحديث بيانات المنشآت الصحية التابعة لكم خلال عشرة أيام عمل من تاريخه وإفادتنا بذلك.";
        var up = await Upload(c, "letter.txt", Utf8(letter));
        Assert.False(up.GetProperty("duplicate").GetBoolean());
        await WaitIdle(c);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        var doc = d.GetProperty("document");
        Assert.Equal("طلب تحديث بيانات المنشآت", doc.GetProperty("title").GetString());
        Assert.Equal("3390", doc.GetProperty("externalNo").GetString());
        Assert.Equal("وزارة الصحة", doc.GetProperty("sourceEntity").GetString());
        Assert.Equal(new UmAlQuraCalendar().ToDateTime(1447, 4, 2, 0, 0, 0, 0).ToString("yyyy-MM-dd"), doc.GetProperty("documentDate").GetString());
        Assert.Equal("done", doc.GetProperty("processing").GetString());
        var deadline = d.GetProperty("deadlines").EnumerateArray().Single();
        Assert.Contains("عمل", deadline.GetProperty("label").GetString());
        Assert.NotEqual("", doc.GetProperty("due").GetString());
        var again = await Upload(c, "copy.txt", Utf8(letter));
        Assert.True(again.GetProperty("duplicate").GetBoolean());
        Assert.Equal(up.GetProperty("id").GetString(), again.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Stale_version_is_rejected_and_revisions_record_changed_fields()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var created = await Json(await Send(c, HttpMethod.Post, "/api/documents", new { title = "مذكرة داخلية", direction = "داخلي", text = "نص المذكرة" }));
        var id = created.GetProperty("id").GetString();
        var ok = await Json(await Send(c, HttpMethod.Put, "/api/documents/" + id, new { version = 1, title = "مذكرة داخلية معدلة", category = "إداري" }));
        Assert.Equal(2, ok.GetProperty("version").GetInt32());
        Assert.Contains("العنوان", ok.GetProperty("changed").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Put, "/api/documents/" + id, new { version = 1, title = "كتابة فوق تعديل زميل" })).StatusCode);
        var revisions = (await Get(c, "/api/documents/" + id)).GetProperty("revisions").EnumerateArray().ToList();
        Assert.Equal(2, revisions.Count);
        Assert.Contains("العنوان", revisions[0].GetProperty("summary").GetString());
        var v1 = await Get(c, $"/api/documents/{id}/revisions/1");
        Assert.Equal("مذكرة داخلية", v1.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Sensitive_documents_hide_text_and_file_from_unauthorized_roles()
    {
        using var f = new PlatformFactory();
        var admin = await f.AdminAsync();
        var up = await Upload(admin, "secret.txt", Utf8("الموضوع: تحقيق إداري\nمحتوى سري للغاية لا يطلع عليه إلا المختصون."), confidentiality: "سري");
        await WaitIdle(admin);
        var id = up.GetProperty("id").GetString();
        var reader = await f.UserAsync(admin, "reader2", "قارئ");
        var d = await Get(reader, "/api/documents/" + id);
        Assert.True(d.GetProperty("restricted").GetBoolean());
        Assert.Equal("", d.GetProperty("text").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/documents/{id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, HttpMethod.Post, "/api/documents", new { title = "x" })).StatusCode);
        Assert.Contains("سري للغاية", (await Get(admin, "/api/documents/" + id)).GetProperty("text").GetString());
        var file = await admin.GetAsync($"/api/documents/{id}/file");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
    }

    [Fact]
    public async Task Docx_upload_extracts_paragraphs()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var up = await Upload(c, "memo.docx", Docx("الموضوع: تعميم بشأن الدوام", "يُعمل بالدوام الجديد اعتباراً من بداية الشهر القادم لجميع الإدارات والأقسام التابعة."));
        await WaitIdle(c);
        var d = await Get(c, "/api/documents/" + up.GetProperty("id").GetString());
        Assert.Equal("تعميم بشأن الدوام", d.GetProperty("document").GetProperty("title").GetString());
        Assert.Contains("بالدوام الجديد", d.GetProperty("text").GetString());
    }

    private static byte[] Docx(params string[] paragraphs)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var e = zip.CreateEntry("word/document.xml");
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
            foreach (var p in paragraphs) w.Write("<w:p><w:r><w:t>" + System.Security.SecurityElement.Escape(p) + "</w:t></w:r></w:p>");
            w.Write("</w:body></w:document>");
        }
        return ms.ToArray();
    }
}

public class TransactionThreadTests
{
    private static string IsoDaysAgo(int days) => DateTime.Today.AddDays(-days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Reply_links_to_original_closes_awaiting_and_forms_a_thread()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var original = await Json(await Send(c, HttpMethod.Post, "/api/documents", new
        {
            title = "طلب بيانات الموظفين", direction = "وارد", externalNo = "9911", documentDate = IsoDaysAgo(30), sourceEntity = "وزارة الموارد البشرية",
            text = "نأمل تزويدنا ببيانات الموظفين خلال (10) أيام من تاريخه.",
        }));
        var dash = await Get(c, "/api/dashboard");
        Assert.Equal(1, dash.GetProperty("awaitingTotal").GetInt32());
        var before = await Get(c, "/api/documents/" + original.GetProperty("id").GetString());
        Assert.Equal(DateTime.Today.AddDays(-20).ToString("yyyy-MM-dd"), before.GetProperty("document").GetProperty("due").GetString());

        var reply = await Json(await Send(c, HttpMethod.Post, "/api/documents", new
        {
            title = "الرد على طلب بيانات الموظفين", direction = "صادر", outgoingNo = "120", documentDate = IsoDaysAgo(2),
            text = "إشارة إلى خطاب سعادتكم رقم ٩٩١١ وتاريخ " + IsoDaysAgo(30).Replace('-', '/') + "م بشأن طلب بيانات الموظفين، نرفق لكم البيانات.",
        }));
        dash = await Get(c, "/api/dashboard");
        Assert.Equal(0, dash.GetProperty("awaitingTotal").GetInt32());

        var detail = await Get(c, "/api/documents/" + reply.GetProperty("id").GetString());
        var r = detail.GetProperty("references").EnumerateArray().Single();
        Assert.Equal(original.GetProperty("id").GetString(), r.GetProperty("target").GetProperty("id").GetString());
        var threadId = detail.GetProperty("document").GetProperty("thread").GetString();
        var thread = await Get(c, "/api/threads/" + threadId);
        Assert.Equal(2, thread.GetProperty("documents").GetArrayLength());
        Assert.Equal(original.GetProperty("id").GetString(), thread.GetProperty("documents")[0].GetProperty("id").GetString());
        var originalDetail = await Get(c, "/api/documents/" + original.GetProperty("id").GetString());
        Assert.Equal(reply.GetProperty("id").GetString(), originalDetail.GetProperty("citedBy")[0].GetProperty("id").GetString());
        // أُجيب على الخطاب، فتسقط المهلة المستخرجة من نصه ولا يعود متأخراً.
        Assert.Equal("", originalDetail.GetProperty("document").GetProperty("due").GetString());
    }

    [Fact]
    public async Task Letters_citing_the_same_missing_letter_join_one_thread_with_a_ghost()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        await Send(c, HttpMethod.Post, "/api/documents", new { title = "تعقيب أول", direction = "وارد", text = "إشارة إلى خطابنا رقم 5050 وتاريخ 1447/01/10هـ نفيدكم بالآتي." });
        await Send(c, HttpMethod.Post, "/api/documents", new { title = "تعقيب ثانٍ", direction = "وارد", text = "إلحاقاً لخطابنا رقم ٥٠٥٠ وتاريخ ١٤٤٧/١/١٠هـ نرفق المستندات." });
        var missing = (await Get(c, "/api/missing")).EnumerateArray().Single();
        Assert.Equal(2, missing.GetProperty("citedBy").GetArrayLength());
        var threads = (await Get(c, "/api/threads")).EnumerateArray().ToList();
        Assert.Single(threads);
        Assert.Equal(2, threads[0].GetProperty("documents").GetInt32());
        Assert.Equal(1, threads[0].GetProperty("missing").GetInt32());
    }
}

public class RealFilesTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public async Task Scanned_image_is_ocred_and_pdf_reply_links_to_it()
    {
        if (TextExtractor.Find("", "", "ara+eng").Tesseract is null) return; // يتطلب Tesseract؛ يُثبَّت في CI.
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var incoming = await Upload(c, "incoming-letter.png", await File.ReadAllBytesAsync(Fixture("incoming-letter.png")));
        var reply = await Upload(c, "reply-letter.pdf", await File.ReadAllBytesAsync(Fixture("reply-letter.pdf")), direction: "صادر");
        await WaitIdle(c, 120);

        var a = await Get(c, "/api/documents/" + incoming.GetProperty("id").GetString());
        Assert.Equal("طلب بيانات المشاريع المتعثرة", a.GetProperty("document").GetProperty("title").GetString());
        Assert.Equal("4521", a.GetProperty("document").GetProperty("externalNo").GetString());
        Assert.Equal(new UmAlQuraCalendar().ToDateTime(1447, 3, 12, 0, 0, 0, 0).ToString("yyyy-MM-dd"), a.GetProperty("document").GetProperty("documentDate").GetString());
        Assert.Equal(15, int.Parse(a.GetProperty("deadlines")[0].GetProperty("label").GetString()!.Split(' ')[1]));

        Assert.Equal("وزارة المالية", a.GetProperty("document").GetProperty("sourceEntity").GetString());
        var b = await Get(c, "/api/documents/" + reply.GetProperty("id").GetString());
        Assert.Equal("وكيل وزارة المالية", b.GetProperty("document").GetProperty("destinationEntity").GetString());
        Assert.Contains("المشاريع المتعثرة", b.GetProperty("text").GetString());
        var r = b.GetProperty("references").EnumerateArray().Single();
        Assert.Equal(incoming.GetProperty("id").GetString(), r.GetProperty("target").GetProperty("id").GetString());
        Assert.Equal(b.GetProperty("document").GetProperty("thread").GetString(), a.GetProperty("document").GetProperty("thread").GetString());
    }
}

public class OperationsTests
{
    [Fact]
    public async Task Audit_chain_is_valid_and_detects_database_tampering()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        await Send(c, HttpMethod.Post, "/api/documents", new { title = "مستند للتدقيق", text = "نص" });
        await c.GetAsync("/api/documents?q=مستند");
        var audit = await Get(c, "/api/audit");
        Assert.True(audit.GetProperty("valid").GetBoolean());
        Assert.True(audit.GetProperty("count").GetInt64() >= 3);
        using (var db = new SqliteConnection("Data Source=" + Path.Combine(f.Data, "basira.db") + ";Pooling=False"))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE audit SET detail = 'تعديل خفي' WHERE seq = 2";
            cmd.ExecuteNonQuery();
        }
        audit = await Get(c, "/api/audit");
        Assert.False(audit.GetProperty("valid").GetBoolean());
        Assert.Equal(2, audit.GetProperty("firstBroken").GetInt64());
    }

    [Fact]
    public async Task Legacy_desktop_archive_imports_documents_files_and_users_with_their_passwords()
    {
        using var f = new PlatformFactory();
        var legacy = Path.Combine(f.Data, "legacy");
        Directory.CreateDirectory(legacy);
        var settings = new AppSettings { BaseDataFolder = legacy, ArchiveRootFolder = Path.Combine(legacy, "ArchiveFiles"), CurrentEmployeeName = "اختبار" };
        var auth = new AuthService(settings);
        auth.CreateInitialAdmin("Legacy-Owner-2025!");
        auth.SaveUser(new UserAccount { UserName = "sara", FullName = "سارة", RoleName = "مراجع" }, "Sara-Legacy-2025!");
        var original = Path.Combine(legacy, "scan.txt");
        File.WriteAllText(original, "ملف أصلي");
        var repo = new ArchiveRepository(settings);
        repo.SaveDocument(new DocumentRecord { Title = "خطاب قديم", ArchiveNumber = "ARC-2025-001", TextContent = "إشارة إلى خطابكم رقم 77", StoredPath = original, FileName = "scan.txt", FileType = ".txt", Status = "مؤرشف" });
        repo.SaveDocument(new DocumentRecord { Title = "مستند بلا ملف", ArchiveNumber = "ARC-2025-002" });

        var c = await f.AdminAsync();
        var r = await Json(await Send(c, HttpMethod.Post, "/api/admin/import-legacy", new { folder = legacy }));
        Assert.Equal(2, r.GetProperty("documents").GetInt32());
        Assert.Equal(1, r.GetProperty("files").GetInt32());
        Assert.Equal(1, r.GetProperty("users").GetInt32()); // admin موجود مسبقاً في المنصة فلا يُستبدل؛ تُرحَّل سارة.
        var again = await Json(await Send(c, HttpMethod.Post, "/api/admin/import-legacy", new { folder = legacy }));
        Assert.Equal(0, again.GetProperty("documents").GetInt32());
        Assert.Equal(2, again.GetProperty("skipped").GetInt32());

        var list = await Get(c, "/api/documents?q=" + Uri.EscapeDataString("خطاب قديم"));
        var item = list.GetProperty("items").EnumerateArray().First(i => i.GetProperty("archiveNo").GetString() == "ARC-2025-001");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/documents/{item.GetProperty("id").GetString()}/file")).StatusCode);

        var sara = f.NewClient();
        Assert.Equal(HttpStatusCode.OK, (await Send(sara, HttpMethod.Post, "/api/login", new { userName = "sara", password = "Sara-Legacy-2025!" })).StatusCode);
        Assert.Equal("مراجع", (await Get(sara, "/api/me")).GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Inbox_folder_imports_stable_files_and_moves_them()
    {
        using var f = new PlatformFactory();
        var c = await f.AdminAsync();
        var inbox = Path.Combine(f.Data, "scanner-inbox");
        Directory.CreateDirectory(inbox);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Put, "/api/admin/settings", new Dictionary<string, string> { ["inbox_folder"] = inbox })).StatusCode);
        File.WriteAllText(Path.Combine(inbox, "scan-001.txt"), "الموضوع: مستند من السكانر\nنص مستند وصل عبر مجلد الإدراج من جهاز المسح الضوئي.");
        var hot = f.Services.GetRequiredService<HotFolderService>();
        await hot.ScanAsync(CancellationToken.None);
        Assert.Equal(1, await hot.ScanAsync(CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(inbox, "scan-001.txt")));
        Assert.Single(Directory.GetFiles(Path.Combine(inbox, "Imported"), "*", SearchOption.AllDirectories));
        await WaitIdle(c);
        var list = await Get(c, "/api/documents?q=" + Uri.EscapeDataString("السكانر"));
        Assert.Equal("مستند من السكانر", list.GetProperty("items")[0].GetProperty("title").GetString());
    }
}
