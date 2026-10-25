using Basira.Platform.Data;
using Basira.Platform.Infrastructure;

namespace Basira.Platform.Processing;

public sealed record ImportOptions(string Direction = "وارد", string Category = "", string Confidentiality = "عادي", string Title = "");
public sealed record ImportResult(Doc Document, bool Duplicate);

/// <summary>إدراج ملف: حفظه في المخزن، ورفض التكرار ببصمة المحتوى، وإنشاء المستند وإرساله للمعالجة.</summary>
public sealed class InboxService
{
    private readonly FileStore _files;
    private readonly DocumentStore _docs;
    private readonly ProcessingQueue _queue;
    private readonly AuditStore _audit;

    public InboxService(FileStore files, DocumentStore docs, ProcessingQueue queue, AuditStore audit)
    {
        _files = files; _docs = docs; _queue = queue; _audit = audit;
    }

    public static string SafeName(string name)
    {
        name = Path.GetFileName(name ?? "").Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Length == 0 ? "مستند" : name.Length > 180 ? name[^180..] : name;
    }

    public async Task<ImportResult> ImportAsync(Stream content, string fileName, ImportOptions options, string actor, CancellationToken ct = default)
    {
        fileName = SafeName(fileName);
        var (sha, size) = await _files.SaveAsync(content, ct);
        if (size == 0) throw new ArgumentException("الملف فارغ.");
        if (_docs.FindByFile(sha) is { } existing)
        {
            _audit.Append(actor, "إدراج مكرر مرفوض", fileName + " ← " + existing.ArchiveNo);
            return new ImportResult(existing, true);
        }
        var doc = new Doc
        {
            Title = string.IsNullOrWhiteSpace(options.Title) ? Path.GetFileNameWithoutExtension(fileName) : options.Title.Trim(),
            Direction = Roles.Directions.Contains(options.Direction) ? options.Direction : "وارد",
            Category = options.Category.Trim(),
            Confidentiality = Roles.Confidentialities.Contains(options.Confidentiality) ? options.Confidentiality : "عادي",
            FileId = sha, FileName = fileName, FileType = Path.GetExtension(fileName).ToLowerInvariant(), FileSize = size,
            Processing = "queued",
        };
        doc = _docs.Create(doc, actor, "إدراج مستند");
        _queue.Enqueue(doc);
        return new ImportResult(doc, false);
    }
}
