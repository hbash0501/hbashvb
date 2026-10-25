using System.Text.Json.Serialization;

namespace Basira.Platform.Data;

public sealed class Doc
{
    public string Id { get; set; } = "";
    public string ArchiveNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string SubCategory { get; set; } = "";
    public string Status { get; set; } = "جديد";
    public string Direction { get; set; } = "وارد";
    public string Confidentiality { get; set; } = "عادي";
    public string Priority { get; set; } = "عادية";
    public string SourceEntity { get; set; } = "";
    public string DestinationEntity { get; set; } = "";
    public string OutgoingNo { get; set; } = "";
    public string IncomingNo { get; set; } = "";
    public string ExternalNo { get; set; } = "";
    public string DocumentDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Tags { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Text { get; set; } = "";
    public string FileId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";
    public long FileSize { get; set; }
    public int PageCount { get; set; }
    public int OcrQuality { get; set; }
    public string Processing { get; set; } = "none";
    public string ProcessingError { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string UpdatedBy { get; set; } = "";
    public int Version { get; set; } = 1;

    public Doc Clone() => (Doc)MemberwiseClone();
}

public sealed class User
{
    public string Id { get; set; } = "";
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    [JsonIgnore] public string Salt { get; set; } = "";
    [JsonIgnore] public string Hash { get; set; } = "";
    public bool Active { get; set; } = true;
    [JsonIgnore] public string Stamp { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string LastLogin { get; set; } = "";
}

public sealed record Revision(int Version, string ChangedAt, string ChangedBy, string Summary);
public sealed record Comment(long Id, string Author, string Body, string CreatedAt);
public sealed record AuditEntry(long Seq, string Ts, string Actor, string Action, string Detail);
public sealed record JobInfo(string Id, string DocumentId, string Title, string State, string Stage, int Progress, string Message, string CreatedAt, string? FinishedAt);

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException() : base("عدّل مستخدم آخر هذا المستند بعد فتحك له. أعد التحميل لترى آخر نسخة ثم أعد التعديل.") { }
}

public static class Roles
{
    public const string Admin = "مدير النظام";
    public static readonly string[] All = { "مدير النظام", "مدير الأرشيف", "موظف أرشفة", "مراجع", "قارئ" };
    public static readonly string[] Statuses = { "جديد", "قيد الإجراء", "بانتظار الاعتماد", "منجز", "مؤرشف" };
    public static readonly string[] Directions = { "وارد", "صادر", "داخلي" };
    public static readonly string[] Confidentialities = { "عادي", "مقيد", "سري", "سري للغاية" };
}
