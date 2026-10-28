using System.Text.Json;
using Basira.Platform.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Basira.Platform.Data;

/// <summary>
/// فحص حقل حرج قرأه OCR. الحالة: auto (اعتُمد آلياً لاتفاق القراءات وثقتها)، pending (بانتظار مراجع)،
/// confirmed (أكد المراجع القراءة)، corrected (صححها). Span هو النص كما ورد في المستند عند الاستخراج.
/// </summary>
public sealed record FieldCheckRow(long Id, string DocumentId, string Field, string Target, string Value, List<string> Candidates, string State,
    string Reason, int Page, string Box, int Offset, int Length, string SpanText, string CropId, string FinalValue, string ReviewedBy, string ReviewedAt, string CreatedAt)
{
    public bool Pending => State == "pending";
}

public static class CheckStates
{
    public const string Auto = "auto";
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Corrected = "corrected";
}

/// <summary>طابور التحقق البشري: حقول القراءة الآلية غير المؤكدة لا تُبنى عليها قرارات حتى يؤكدها مراجع أو يصححها.</summary>
public sealed class ReviewStore
{
    private readonly Db _db;
    public event Action? Changed;

    public ReviewStore(Db db) => _db = db;

    private static FieldCheckRow Map(SqliteDataReader r) => new(
        r.Long("id"), r.Str("document_id"), r.Str("field"), r.Str("target"), r.Str("value"),
        JsonSerializer.Deserialize<List<string>>(r.Str("candidates") is { Length: > 0 } j ? j : "[]") ?? new(), r.Str("state"), r.Str("reason"),
        (int)r.Long("page"), r.Str("box"), (int)r.Long("text_offset"), (int)r.Long("text_length"), r.Str("span_text"), r.Str("crop_id"),
        r.Str("final_value"), r.Str("reviewed_by"), r.Str("reviewed_at"), r.Str("created_at"));

    /// <summary>يستبدل فحوص المستند بنتيجة معالجة جديدة (داخل معاملة تحديث المستند).</summary>
    public static void Replace(SqliteConnection c, SqliteTransaction tx, string documentId, IEnumerable<FieldCheckRow> checks)
    {
        c.Exec("DELETE FROM field_checks WHERE document_id=@documentId", new { documentId }, tx);
        var now = Clock.Now();
        foreach (var k in checks)
            c.Exec("""
                INSERT INTO field_checks(document_id, field, target, value, candidates, state, reason, page, box, text_offset, text_length, span_text, crop_id, created_at)
                VALUES(@documentId, @Field, @Target, @Value, @candidates, @State, @Reason, @Page, @Box, @Offset, @Length, @SpanText, @CropId, @now)
                """, new { documentId, k.Field, k.Target, k.Value, candidates = JsonSerializer.Serialize(k.Candidates), k.State, k.Reason, k.Page, k.Box, k.Offset, k.Length, k.SpanText, k.CropId, now }, tx);
    }

    public void NotifyChanged() => Changed?.Invoke();

    public FieldCheckRow? Get(long id)
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM field_checks WHERE id=@id", Map, new { id }).FirstOrDefault();
    }

    public List<FieldCheckRow> ForDocument(string documentId)
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM field_checks WHERE document_id=@documentId ORDER BY text_offset, id", Map, new { documentId });
    }

    public List<FieldCheckRow> PendingAll(int take = 500)
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM field_checks WHERE state='pending' AND document_id IN (SELECT id FROM documents) ORDER BY created_at, document_id, text_offset LIMIT @take", Map, new { take });
    }

    public int PendingCount()
    {
        using var c = _db.Open();
        return Convert.ToInt32(c.Scalar("SELECT COUNT(*) FROM field_checks WHERE state='pending' AND document_id IN (SELECT id FROM documents)"));
    }

    /// <summary>الفحوص المعلقة لكل مستند؛ تقرأها لقطة الأرشيف لتعليق القرارات المبنية عليها.</summary>
    public Dictionary<string, List<FieldCheckRow>> PendingByDocument()
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM field_checks WHERE state='pending'", Map).GroupBy(k => k.DocumentId).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>تسجيل قرار المراجع، وإزاحة مواضع الفحوص التالية في النص إن تغير طوله (داخل معاملة تحديث المستند).</summary>
    public static void Resolve(SqliteConnection c, SqliteTransaction tx, FieldCheckRow k, string state, string finalValue, string reviewer, int newOffset, int newLength, int delta)
    {
        c.Exec("UPDATE field_checks SET state=@state, final_value=@finalValue, reviewed_by=@reviewer, reviewed_at=@now, text_offset=@newOffset, text_length=@newLength WHERE id=@Id",
            new { state, finalValue, reviewer, now = Clock.Now(), newOffset, newLength, k.Id }, tx);
        if (delta != 0 && newOffset >= 0)
            c.Exec("UPDATE field_checks SET text_offset = text_offset + @delta WHERE document_id=@DocumentId AND id<>@Id AND text_offset > @newOffset",
                new { delta, k.DocumentId, k.Id, newOffset }, tx);
    }
}
