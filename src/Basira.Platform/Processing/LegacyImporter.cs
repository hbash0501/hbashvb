using System.Globalization;
using AIDocumentArchiveVB;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;

namespace Basira.Platform.Processing;

public sealed record LegacyImportResult(int Documents, int Skipped, int Files, int MissingFiles, int Users);

/// <summary>ترحيل أرشيف التطبيق المكتبي السابق (archive-db.xml وusers.xml والملفات) دون تعديل المصدر؛ آمن للتكرار.</summary>
public sealed class LegacyImporter
{
    private readonly DocumentStore _docs;
    private readonly UserStore _users;
    private readonly FileStore _files;

    public LegacyImporter(DocumentStore docs, UserStore users, FileStore files)
    {
        _docs = docs; _users = users; _files = files;
    }

    public async Task<LegacyImportResult> ImportAsync(string folder, string actor, CancellationToken ct = default)
    {
        folder = Path.GetFullPath(folder);
        var dbFile = Path.Combine(folder, "archive-db.xml");
        if (!File.Exists(dbFile)) throw new FileNotFoundException("لم يُعثر على archive-db.xml في المجلد المحدد.", dbFile);
        var settings = new AppSettings { BaseDataFolder = folder, ArchiveRootFolder = Path.Combine(folder, "ArchiveFiles"), CurrentEmployeeName = actor };

        var users = 0;
        if (File.Exists(Path.Combine(folder, "users.xml")))
            foreach (var u in new AuthService(settings).GetUsers())
                if (_users.ImportLegacy(u, actor)) users++;

        var existing = _docs.All().Select(d => d.Id).ToHashSet();
        var existingNumbers = _docs.All().Select(d => d.ArchiveNo).ToHashSet();
        int imported = 0, skipped = 0, files = 0, missing = 0;
        foreach (var r in new ArchiveRepository(settings).GetDocuments("", "", ""))
        {
            ct.ThrowIfCancellationRequested();
            if (existing.Contains(r.Id)) { skipped++; continue; }
            var d = new Doc
            {
                Id = r.Id, ArchiveNo = string.IsNullOrWhiteSpace(r.ArchiveNumber) || existingNumbers.Contains(r.ArchiveNumber) ? "" : r.ArchiveNumber,
                Title = r.Title ?? "", Category = r.GeneralCategory ?? "", SubCategory = r.SubCategory ?? "", Status = string.IsNullOrWhiteSpace(r.Status) ? "جديد" : r.Status,
                Direction = !string.IsNullOrWhiteSpace(r.OcrOutgoingNumbers) && string.IsNullOrWhiteSpace(r.OcrIncomingNumbers) ? "صادر" : "وارد",
                Confidentiality = string.IsNullOrWhiteSpace(r.Confidentiality) ? "عادي" : r.Confidentiality, Priority = string.IsNullOrWhiteSpace(r.Priority) ? "عادية" : r.Priority,
                SourceEntity = r.SourceEntity ?? "", DestinationEntity = r.DestinationEntity ?? "", OutgoingNo = First(r.OcrOutgoingNumbers), IncomingNo = First(r.OcrIncomingNumbers),
                ExternalNo = r.ExternalNumber ?? "", DocumentDate = DateOf(r.DocumentDateText), DueDate = DateOf(r.DueDateText), Tags = r.Tags ?? "", Summary = r.Summary ?? "",
                Text = r.TextContent ?? "", FileName = r.FileName ?? "", FileType = (r.FileType ?? "").ToLowerInvariant(), PageCount = r.PageCount, OcrQuality = r.OcrQualityScore,
                Processing = string.IsNullOrWhiteSpace(r.TextContent) ? "needs_text" : "done",
                CreatedAt = r.CreatedAt == DateTime.MinValue ? Clock.Now() : r.CreatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                CreatedBy = string.IsNullOrWhiteSpace(r.EmployeeName) ? actor : r.EmployeeName,
            };
            var source = new[] { r.StoredPath, r.OriginalPath }.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
            if (source is not null)
            {
                await using var s = File.OpenRead(source);
                var (sha, size) = await _files.SaveAsync(s, ct);
                d.FileId = sha; d.FileSize = size;
                if (d.FileType == "") d.FileType = Path.GetExtension(source).ToLowerInvariant();
                if (d.FileName == "") d.FileName = Path.GetFileName(source);
                files++;
            }
            else if (!string.IsNullOrWhiteSpace(r.StoredPath)) missing++;
            _docs.Create(d, actor, "ترحيل مستند من النسخة السابقة");
            existingNumbers.Add(d.ArchiveNo);
            imported++;
        }
        return new LegacyImportResult(imported, skipped, files, missing, users);
    }

    private static string First(string? multi) =>
        (multi ?? "").Split(new[] { '،', ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";

    private static string DateOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var canonical = ""; DateTime value = default;
        if (CorrespondenceReferenceParser.TryParseDate(text, ref canonical, ref value)) return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
    }
}
