using System.Text.Json;
using Basira.Platform.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Basira.Platform.Data;

/// <summary>المستندات: ترقيم أرشيفي، تزامن تفاؤلي بالإصدار، نسخة كاملة لكل تعديل، وتدقيق في المعاملة نفسها.</summary>
public sealed class DocumentStore
{
    private readonly Db _db;
    public event Action? Changed;

    public DocumentStore(Db db) => _db = db;

    private const string Columns = "id, archive_no, title, category, sub_category, status, direction, confidentiality, priority, source_entity, destination_entity, outgoing_no, incoming_no, external_no, document_date, due_date, tags, summary, text, file_id, file_name, file_type, file_size, page_count, ocr_quality, processing, processing_error, scan_quality, auto_fields, classification, created_at, created_by, updated_at, updated_by, version";
    private const string Values = "@Id,@ArchiveNo,@Title,@Category,@SubCategory,@Status,@Direction,@Confidentiality,@Priority,@SourceEntity,@DestinationEntity,@OutgoingNo,@IncomingNo,@ExternalNo,@DocumentDate,@DueDate,@Tags,@Summary,@Text,@FileId,@FileName,@FileType,@FileSize,@PageCount,@OcrQuality,@Processing,@ProcessingError,@ScanQuality,@AutoFields,@Classification,@CreatedAt,@CreatedBy,@UpdatedAt,@UpdatedBy,@Version";

    private static Doc Map(SqliteDataReader r) => new()
    {
        Id = r.Str("id"), ArchiveNo = r.Str("archive_no"), Title = r.Str("title"), Category = r.Str("category"), SubCategory = r.Str("sub_category"),
        Status = r.Str("status"), Direction = r.Str("direction"), Confidentiality = r.Str("confidentiality"), Priority = r.Str("priority"),
        SourceEntity = r.Str("source_entity"), DestinationEntity = r.Str("destination_entity"), OutgoingNo = r.Str("outgoing_no"),
        IncomingNo = r.Str("incoming_no"), ExternalNo = r.Str("external_no"), DocumentDate = r.Str("document_date"), DueDate = r.Str("due_date"),
        Tags = r.Str("tags"), Summary = r.Str("summary"), Text = r.Str("text"), FileId = r.Str("file_id"), FileName = r.Str("file_name"),
        FileType = r.Str("file_type"), FileSize = r.Long("file_size"), PageCount = (int)r.Long("page_count"), OcrQuality = (int)r.Long("ocr_quality"),
        Processing = r.Str("processing"), ProcessingError = r.Str("processing_error"), ScanQuality = r.Str("scan_quality"), AutoFields = r.Str("auto_fields"), Classification = r.Str("classification"), CreatedAt = r.Str("created_at"), CreatedBy = r.Str("created_by"),
        UpdatedAt = r.Str("updated_at"), UpdatedBy = r.Str("updated_by"), Version = (int)r.Long("version"),
    };

    public Doc? Get(string id)
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM documents WHERE id=@id", Map, new { id }).FirstOrDefault();
    }

    public List<Doc> All()
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM documents", Map);
    }

    public int Count()
    {
        using var c = _db.Open();
        return Convert.ToInt32(c.Scalar("SELECT COUNT(*) FROM documents"));
    }

    public Doc? FindByFile(string sha)
    {
        if (string.IsNullOrEmpty(sha)) return null;
        using var c = _db.Open();
        return c.Query("SELECT * FROM documents WHERE file_id=@sha LIMIT 1", Map, new { sha }).FirstOrDefault();
    }

    private static string NextArchiveNo(SqliteConnection c, SqliteTransaction tx)
    {
        var year = DateTime.Now.Year;
        var name = "archive-" + year;
        c.Exec("INSERT INTO counters(name, value) VALUES(@name, 0) ON CONFLICT(name) DO NOTHING", new { name }, tx);
        while (true)
        {
            c.Exec("UPDATE counters SET value = value + 1 WHERE name=@name", new { name }, tx);
            var n = Convert.ToInt64(c.Scalar("SELECT value FROM counters WHERE name=@name", new { name }, tx));
            var no = $"BSR-{year}-{n:00000}";
            if (Convert.ToInt64(c.Scalar("SELECT COUNT(*) FROM documents WHERE archive_no=@no", new { no }, tx)) == 0) return no;
        }
    }

    private static void SaveRevision(SqliteConnection c, SqliteTransaction tx, Doc d, string actor, string summary) =>
        c.Exec("INSERT INTO revisions(document_id, version, changed_at, changed_by, summary, snapshot) VALUES(@id,@v,@at,@by,@summary,@snap)",
            new { id = d.Id, v = d.Version, at = d.UpdatedAt, by = actor, summary, snap = JsonSerializer.Serialize(d) }, tx);

    public Doc Create(Doc d, string actor, string auditAction = "إنشاء مستند")
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        if (string.IsNullOrWhiteSpace(d.Id)) d.Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(d.ArchiveNo)) d.ArchiveNo = NextArchiveNo(c, tx);
        var now = Clock.Now();
        if (string.IsNullOrWhiteSpace(d.CreatedAt)) d.CreatedAt = now;
        if (string.IsNullOrWhiteSpace(d.CreatedBy)) d.CreatedBy = actor;
        d.UpdatedAt = now; d.UpdatedBy = actor; d.Version = 1;
        c.Exec($"INSERT INTO documents({Columns}) VALUES({Values})", d, tx);
        SaveRevision(c, tx, d, actor, auditAction);
        AuditStore.Append(c, tx, actor, auditAction, d.ArchiveNo + " | " + d.Title);
        tx.Commit();
        Changed?.Invoke();
        return d;
    }

    /// <summary>تعديل بتزامن تفاؤلي: إن اختلف الإصدار عن المتوقع يُرفض التعديل. expectedVersion=null للمعالجة الآلية.</summary>
    public Doc Update(string id, int? expectedVersion, Action<Doc> apply, string actor, string summary, string auditAction = "تعديل مستند",
        Action<SqliteConnection, SqliteTransaction, Doc>? alsoInTransaction = null)
    {
        Doc d;
        using (var c = _db.Open())
        using (var tx = c.BeginTransaction())
        {
            d = c.Query("SELECT * FROM documents WHERE id=@id", Map, new { id }, tx).FirstOrDefault() ?? throw new KeyNotFoundException("المستند غير موجود.");
            if (expectedVersion is not null && expectedVersion.Value != d.Version) throw new ConcurrencyException();
            var before = d.Clone();
            apply(d);
            d.Id = before.Id; d.ArchiveNo = before.ArchiveNo; d.CreatedAt = before.CreatedAt; d.CreatedBy = before.CreatedBy;
            d.Version = before.Version + 1; d.UpdatedAt = Clock.Now(); d.UpdatedBy = actor;
            var sets = string.Join(", ", Columns.Split(", ").Where(col => col is not "id" and not "archive_no").Select(col => $"{col}=@{ToProp(col)}"));
            c.Exec($"UPDATE documents SET {sets} WHERE id=@Id", d, tx);
            SaveRevision(c, tx, d, actor, summary);
            AuditStore.Append(c, tx, actor, auditAction, d.ArchiveNo + " | " + summary);
            alsoInTransaction?.Invoke(c, tx, d);
            tx.Commit();
        }
        Changed?.Invoke();
        return d;
    }

    private static string ToProp(string col) => string.Concat(col.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    public List<Revision> Revisions(string id)
    {
        using var c = _db.Open();
        return c.Query("SELECT version, changed_at, changed_by, summary FROM revisions WHERE document_id=@id ORDER BY version DESC",
            r => new Revision((int)r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3)), new { id });
    }

    public Doc? RevisionSnapshot(string id, int version)
    {
        using var c = _db.Open();
        var json = c.Scalar("SELECT snapshot FROM revisions WHERE document_id=@id AND version=@version ORDER BY id DESC LIMIT 1", new { id, version }) as string;
        return json is null ? null : JsonSerializer.Deserialize<Doc>(json);
    }

    // ===== المحذوفات =====

    /// <summary>نقل مستند إلى المحذوفات: يختفي من الأرشيف والبحث والمعاملات، ويبقى بكامل بياناته وسجله قابلاً للاستعادة.</summary>
    public TrashItem MoveToTrash(string id, int? expectedVersion, string actor, string reason)
    {
        TrashItem item;
        using (var c = _db.Open())
        using (var tx = c.BeginTransaction())
        {
            var d = c.Query("SELECT * FROM documents WHERE id=@id", Map, new { id }, tx).FirstOrDefault() ?? throw new KeyNotFoundException("المستند غير موجود.");
            if (expectedVersion is not null && expectedVersion.Value != d.Version) throw new ConcurrencyException();
            item = new TrashItem(d.Id, d.ArchiveNo, d.Title, d.Direction, d.FileId, Clock.Now(), actor, reason.Trim());
            c.Exec("INSERT INTO trash(id, archive_no, title, direction, file_id, snapshot, deleted_at, deleted_by, reason) VALUES(@Id,@ArchiveNo,@Title,@Direction,@FileId,@snap,@DeletedAt,@DeletedBy,@Reason)",
                new { item.Id, item.ArchiveNo, item.Title, item.Direction, item.FileId, snap = JsonSerializer.Serialize(d), item.DeletedAt, item.DeletedBy, item.Reason }, tx);
            c.Exec("DELETE FROM documents WHERE id=@id", new { id }, tx);
            AuditStore.Append(c, tx, actor, "نقل مستند إلى المحذوفات", d.ArchiveNo + " | " + d.Title + (item.Reason == "" ? "" : " | السبب: " + item.Reason));
            tx.Commit();
        }
        Changed?.Invoke();
        return item;
    }

    public List<TrashItem> TrashList()
    {
        using var c = _db.Open();
        return c.Query("SELECT id, archive_no, title, direction, file_id, deleted_at, deleted_by, reason FROM trash ORDER BY deleted_at DESC",
            r => new TrashItem(r.Str("id"), r.Str("archive_no"), r.Str("title"), r.Str("direction"), r.Str("file_id"), r.Str("deleted_at"), r.Str("deleted_by"), r.Str("reason")));
    }

    /// <summary>استعادة مستند من المحذوفات برقمه الأرشيفي نفسه، بإصدار جديد في سجله.</summary>
    public Doc Restore(string id, string actor)
    {
        Doc d;
        using (var c = _db.Open())
        using (var tx = c.BeginTransaction())
        {
            var json = c.Scalar("SELECT snapshot FROM trash WHERE id=@id", new { id }, tx) as string ?? throw new KeyNotFoundException("المستند غير موجود في المحذوفات.");
            d = JsonSerializer.Deserialize<Doc>(json)!;
            d.Version++; d.UpdatedAt = Clock.Now(); d.UpdatedBy = actor;
            c.Exec($"INSERT INTO documents({Columns}) VALUES({Values})", d, tx);
            c.Exec("DELETE FROM trash WHERE id=@id", new { id }, tx);
            SaveRevision(c, tx, d, actor, "استعادة من المحذوفات");
            AuditStore.Append(c, tx, actor, "استعادة مستند من المحذوفات", d.ArchiveNo + " | " + d.Title);
            tx.Commit();
        }
        Changed?.Invoke();
        return d;
    }

    /// <summary>
    /// حذف نهائي من المحذوفات: المستند وسجله وملاحظاته وفحوصه. يُعاد ما لم يعد يشير إليه شيء من ملفات المخزن
    /// (الأصل ومقتطعات التحقق) ليحذفها المستدعي. يبقى أثر الحذف في سجل التدقيق.
    /// </summary>
    public List<string> Purge(string id, string actor)
    {
        var orphans = new List<string>();
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        var row = c.Query("SELECT archive_no, title, file_id FROM trash WHERE id=@id", r => (No: r.GetString(0), Title: r.GetString(1), File: r.GetString(2)), new { id }, tx).FirstOrDefault();
        if (row == default) throw new KeyNotFoundException("المستند غير موجود في المحذوفات.");
        var files = c.Query("SELECT crop_id FROM field_checks WHERE document_id=@id AND crop_id<>''", r => r.GetString(0), new { id }, tx);
        if (row.File != "") files.Add(row.File);
        foreach (var t in new[] { "trash WHERE id", "revisions WHERE document_id", "comments WHERE document_id", "field_checks WHERE document_id", "doc_embeddings WHERE document_id", "commitment_decisions WHERE document_id" })
            c.Exec($"DELETE FROM {t}=@id", new { id }, tx);
        foreach (var sha in files.Distinct())
        {
            var used = Convert.ToInt64(c.Scalar("SELECT (SELECT COUNT(*) FROM documents WHERE file_id=@sha) + (SELECT COUNT(*) FROM trash WHERE file_id=@sha) + (SELECT COUNT(*) FROM field_checks WHERE crop_id=@sha)", new { sha }, tx));
            if (used == 0) orphans.Add(sha);
        }
        AuditStore.Append(c, tx, actor, "حذف نهائي لمستند", row.No + " | " + row.Title);
        tx.Commit();
        return orphans;
    }

    public List<Comment> Comments(string id)
    {
        using var c = _db.Open();
        return c.Query("SELECT id, author, body, created_at FROM comments WHERE document_id=@id ORDER BY id",
            r => new Comment(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3)), new { id });
    }

    public Comment AddComment(string id, string author, string body)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        var archiveNo = c.Scalar("SELECT archive_no FROM documents WHERE id=@id", new { id }, tx) as string ?? throw new KeyNotFoundException("المستند غير موجود.");
        var now = Clock.Now();
        c.Exec("INSERT INTO comments(document_id, author, body, created_at) VALUES(@id,@author,@body,@now)", new { id, author, body, now }, tx);
        var cid = Convert.ToInt64(c.Scalar("SELECT last_insert_rowid()", null, tx));
        AuditStore.Append(c, tx, author, "ملاحظة على مستند", archiveNo);
        tx.Commit();
        return new Comment(cid, author, body, now);
    }
}
