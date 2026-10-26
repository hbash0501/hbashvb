using System.Globalization;
using AIDocumentArchiveVB;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence;
using Basira.Platform.Processing;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

public static class DocumentsApi
{
    public sealed record DocBody(int? Version, string? Title, string? Category, string? SubCategory, string? Status, string? Direction, string? Confidentiality,
        string? Priority, string? SourceEntity, string? DestinationEntity, string? OutgoingNo, string? IncomingNo, string? ExternalNo, string? DocumentDate,
        string? DueDate, string? Tags, string? Summary, string? Text);
    private sealed record StatusBody(int Version, string? Status, string? Note);
    private sealed record CommentBody(string? Body);

    private static readonly HashSet<string> InlineTypes = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".txt" };

    private static string Iso(string? v)
    {
        v = (v ?? "").Trim();
        if (v == "") return "";
        if (Snapshot.ParseIso(v) is { } d) return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var canonical = ""; DateTime value = default;
        if (CorrespondenceReferenceParser.TryParseDate(v, ref canonical, ref value)) return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        throw new ArgumentException("تاريخ غير صالح: " + v + " (استخدم 2026-10-25 أو 1448/05/03هـ).");
    }

    private static void Apply(Doc d, DocBody b)
    {
        if (b.Title is not null) d.Title = Clip(b.Title.Trim(), 300);
        if (b.Category is not null) d.Category = Clip(b.Category.Trim(), 80);
        if (b.SubCategory is not null) d.SubCategory = Clip(b.SubCategory.Trim(), 80);
        if (b.Status is not null) d.Status = Roles.Statuses.Contains(b.Status) ? b.Status : throw new ArgumentException("حالة غير معروفة.");
        if (b.Direction is not null) d.Direction = Roles.Directions.Contains(b.Direction) ? b.Direction : throw new ArgumentException("اتجاه غير معروف.");
        if (b.Confidentiality is not null) d.Confidentiality = Roles.Confidentialities.Contains(b.Confidentiality) ? b.Confidentiality : throw new ArgumentException("درجة سرية غير معروفة.");
        if (b.Priority is not null) d.Priority = Clip(b.Priority.Trim(), 30);
        if (b.SourceEntity is not null) d.SourceEntity = Clip(b.SourceEntity.Trim(), 160);
        if (b.DestinationEntity is not null) d.DestinationEntity = Clip(b.DestinationEntity.Trim(), 160);
        if (b.OutgoingNo is not null) d.OutgoingNo = Clip(b.OutgoingNo.Trim(), 60);
        if (b.IncomingNo is not null) d.IncomingNo = Clip(b.IncomingNo.Trim(), 60);
        if (b.ExternalNo is not null) d.ExternalNo = Clip(b.ExternalNo.Trim(), 60);
        if (b.DocumentDate is not null) d.DocumentDate = Iso(b.DocumentDate);
        if (b.DueDate is not null) d.DueDate = Iso(b.DueDate);
        if (b.Tags is not null) d.Tags = Clip(b.Tags.Trim(), 400);
        if (b.Summary is not null) d.Summary = Clip(b.Summary.Trim(), 2000);
        if (b.Text is not null) d.Text = Clip(b.Text, 2_000_000);
    }

    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/documents", async (HttpContext c, ArchiveIndex index, AuditStore audit, string? q, string? category, string? status, string? direction,
            string? filter, string? from, string? to, string? sort, int? page, int? pageSize) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "search")) return Forbidden("البحث");
            var s = await index.GetAsync();
            q = Clip((q ?? "").Trim(), 200);
            var f = filter is "review" or "overdue" or "week" or "ocr" or "sensitive" ? filter : "all";
            var rows = new List<(Doc Doc, double Score, string Excerpt, string Matches)>();
            if (q != "") foreach (var h in s.Engine.Search(q, category ?? "", f, 1000)) rows.Add((s.ById[h.Document.Id], h.Score, h.Excerpt, h.Matches));
            else
            {
                var today = DateTime.Today;
                foreach (var d in s.Docs)
                    if ((string.IsNullOrEmpty(category) || d.Category == category) && BasiraEngine.MatchesFilter(ArchiveIndex.ToRecord(d, s.EffectiveDue.TryGetValue(d.Id, out var due) ? due.Date : ""), f, today))
                        rows.Add((d, 0, "", ""));
            }
            if (filter == "awaiting") { var ids = s.Awaiting.Select(a => a.DocumentId).ToHashSet(); rows = rows.Where(r => ids.Contains(r.Doc.Id)).ToList(); }
            if (filter == "autocat") rows = rows.Where(r => r.Doc.IsAuto("category") && !ClassificationService.ReadConfidence(r.Doc).Confident).ToList();
            if (filter == "unclassified") rows = rows.Where(r => r.Doc.Category == "").ToList();
            if (!string.IsNullOrEmpty(status)) rows = rows.Where(r => r.Doc.Status == status).ToList();
            if (!string.IsNullOrEmpty(direction)) rows = rows.Where(r => r.Doc.Direction == direction).ToList();
            var fromD = Snapshot.ParseIso(from ?? ""); var toD = Snapshot.ParseIso(to ?? "");
            if (fromD is not null) rows = rows.Where(r => Snapshot.DateOf(r.Doc) >= fromD).ToList();
            if (toD is not null) rows = rows.Where(r => Snapshot.DateOf(r.Doc) <= toD).ToList();
            rows = (sort, q) switch
            {
                ("date", _) => rows.OrderByDescending(r => Snapshot.DateOf(r.Doc)).ThenByDescending(r => r.Doc.ArchiveNo).ToList(),
                (_, "") => rows.OrderByDescending(r => r.Doc.CreatedAt, StringComparer.Ordinal).ToList(),
                _ => rows,
            };
            var size = Math.Clamp(pageSize ?? 30, 1, 100);
            var p = Math.Max(1, page ?? 1);
            if (q != "") audit.Append(u.UserName, "بحث", q);
            return Results.Ok(new { total = rows.Count, page = p, pageSize = size, items = rows.Skip((p - 1) * size).Take(size).Select(r => Summary(r.Doc, s, u, r.Excerpt, r.Matches)) });
        });

        g.MapGet("/documents/{id}", async (string id, HttpContext c, ArchiveIndex index, DocumentStore docs, AuditStore audit, ReviewStore review) =>
        {
            var u = U(c);
            var d = docs.Get(id);
            if (d is null) return Error("المستند غير موجود.", 404);
            var s = await index.GetAsync();
            var restricted = Restricted(d, u);
            audit.Append(u.UserName, "عرض مستند", d.ArchiveNo + " | " + d.Title);
            var refs = s.References.TryGetValue(id, out var r) ? r : new List<RefInfo>();
            var citedBy = s.Edges.Where(e => e.To == id && s.ById.ContainsKey(e.From)).Select(e => new { id = e.From, archiveNo = s.ById[e.From].ArchiveNo, title = s.ById[e.From].Title, kind = e.Kind }).ToList();
            var awaiting = s.Awaiting.FirstOrDefault(a => a.DocumentId == id);
            return Results.Ok(new
            {
                document = Summary(d, s, u),
                text = restricted ? "" : d.Text,
                restricted,
                tags = d.Tags, fileName = d.FileName, fileSize = d.FileSize, pageCount = d.PageCount, ocrQuality = d.OcrQuality, processingError = d.ProcessingError,
                scanQuality = d.ScanQuality,
                classification = d.Classification == "" ? (System.Text.Json.JsonElement?)null : System.Text.Json.JsonDocument.Parse(d.Classification).RootElement.Clone(),
                autoFields = d.AutoFields.Split(',', StringSplitOptions.RemoveEmptyEntries),
                checks = restricted ? new List<object>() : review.ForDocument(id).Select(ReviewApi.Describe).ToList(),
                createdBy = d.CreatedBy, updatedBy = d.UpdatedBy, dueDateManual = d.DueDate,
                references = restricted ? new List<object>() : refs.Select(x => (object)new
                {
                    kind = x.Reference.KindCode, kindLabel = x.Reference.KindLabel, ownerLabel = x.Reference.OwnerLabel, documentType = x.Reference.DocumentType,
                    number = x.Reference.Number, rawNumber = x.Reference.RawNumber, dateText = x.Reference.DateText,
                    date = x.Reference.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), calendar = x.Reference.Calendar,
                    evidence = x.Reference.Evidence, offset = x.Reference.Offset, length = x.Reference.Length, confidence = x.Reference.Confidence,
                    target = x.TargetId is null ? null : new { id = x.TargetId, archiveNo = s.ById[x.TargetId].ArchiveNo, title = s.ById[x.TargetId].Title },
                    missing = x.GhostKey != "", pending = x.Pending,
                    suggestion = x.SuggestedId is null || !s.ById.ContainsKey(x.SuggestedId) ? null : new { id = x.SuggestedId, archiveNo = s.ById[x.SuggestedId].ArchiveNo, title = s.ById[x.SuggestedId].Title, reason = x.SuggestionReason },
                }).ToList(),
                deadlines = restricted ? new List<object>() : (s.Deadlines.TryGetValue(id, out var dl) ? dl : new()).Select(h => (object)new
                {
                    label = h.Label, due = h.Due?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), dueHijri = h.Due is null ? "" : Hijri(h.Due.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    evidence = h.Evidence, offset = h.Offset, length = h.Length,
                }).ToList(),
                citedBy,
                commitments = restricted ? new List<object>() : s.Commitments.Where(x => x.DocumentId == id).Select(x => CommitmentsApi.View(x, s, u)).ToList(),
                related = s.Engine.Links(id).Select(l => new { id = l.Document.Id, archiveNo = l.Document.ArchiveNumber, title = l.Document.Title, reason = l.Reason }),
                awaitingDays = awaiting?.DaysWaiting,
                comments = docs.Comments(id),
                revisions = docs.Revisions(id),
                can = new { edit = UserStore.Can(u, "edit"), workflow = UserStore.Can(u, "workflow"), file = !restricted && d.FileId != "" },
            });
        });

        g.MapPost("/documents", (DocBody b, HttpContext c, DocumentStore docs, ClassificationService classifier) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "import")) return Forbidden("إضافة المستندات");
            try
            {
                var d = new Doc { Processing = "done" };
                Apply(d, b);
                // مستند بنص دون عنوان أو تصنيف: يُستخرجان من المحتوى.
                if (d.Title == "" && d.Text != "")
                {
                    d.Title = TitleBuilder.Build(d.Text, LetterFields.Parse(d.Text).Subject);
                    d.SetAuto("title", d.Title != "");
                }
                if (d.Title == "") return Error("اكتب عنوان المستند.");
                if (d.Category == "" && d.Text != "") classifier.Apply(d, classifier.Classify(d.Text, d.Title));
                d = docs.Create(d, u.UserName, "إنشاء مستند");
                return Results.Ok(new { id = d.Id, archiveNo = d.ArchiveNo });
            }
            catch (ArgumentException ex) { return Error(ex.Message); }
        });

        g.MapPut("/documents/{id}", (string id, DocBody b, HttpContext c, DocumentStore docs, ClassificationService classifier) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("التعديل");
            if (b.Version is null) return Error("الإصدار مطلوب لمنع الكتابة فوق تعديل زميل.");
            if (b.Status is not null && !UserStore.Can(u, "workflow")) return Forbidden("تغيير الحالة");
            var existing = docs.Get(id);
            if (existing is null) return Error("المستند غير موجود.", 404);
            if (Restricted(existing, u)) return Forbidden("تعديل مستند حساس");
            try
            {
                // تُحسب الحقول المتغيرة على نسخة مؤقتة لتُسجَّل في وصف النسخة؛ الإصدار يُفحص داخل Update.
                var preview = existing.Clone();
                Apply(preview, b);
                var changed = typeof(Doc).GetProperties().Where(p => p.PropertyType == typeof(string) && !Equals(p.GetValue(existing), p.GetValue(preview))).Select(p => FieldLabel(p.Name)).ToList();
                if (changed.Count == 0) return Results.Ok(new { version = existing.Version, changed });
                // ما عدّله المستخدم لم يعد آلياً: لا تكتب فوقه إعادة المعالجة، ويصبح التصنيف مثالاً يتعلم منه المصنِّف.
                var catChanged = preview.Category != existing.Category || preview.SubCategory != existing.SubCategory;
                var d = docs.Update(id, b.Version, doc =>
                {
                    Apply(doc, b);
                    if (doc.Title != existing.Title) doc.SetAuto("title", false);
                    if (catChanged) { doc.SetAuto("category", false); doc.SetAuto("sub_category", false); }
                }, u.UserName, "تعديل: " + string.Join("، ", changed));
                if (catChanged) { classifier.RecordFeedback(existing, d.Category, d.SubCategory, "changed", u.UserName); classifier.Invalidate(); }
                return Results.Ok(new { version = d.Version, changed });
            }
            catch (ConcurrencyException ex) { return Error(ex.Message, 409); }
            catch (ArgumentException ex) { return Error(ex.Message); }
        });

        g.MapPost("/documents/{id}/status", (string id, StatusBody b, HttpContext c, DocumentStore docs) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "workflow")) return Forbidden("تغيير الحالة");
            if (b.Status is null || !Roles.Statuses.Contains(b.Status)) return Error("حالة غير معروفة.");
            try
            {
                var note = Clip((b.Note ?? "").Trim(), 500);
                var d = docs.Update(id, b.Version, doc => doc.Status = b.Status, u.UserName, "الحالة: " + b.Status + (note == "" ? "" : " — " + note), "تغيير حالة");
                if (note != "") docs.AddComment(id, u.UserName, "غيّر الحالة إلى «" + b.Status + "»: " + note);
                return Results.Ok(new { version = d.Version });
            }
            catch (ConcurrencyException ex) { return Error(ex.Message, 409); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        g.MapPost("/documents/{id}/comments", (string id, CommentBody b, HttpContext c, DocumentStore docs) =>
        {
            var u = U(c);
            var body = (b.Body ?? "").Trim();
            if (body.Length is 0 or > 2000) return Error("اكتب ملاحظة من 1 إلى 2000 حرف.");
            try { return Results.Ok(docs.AddComment(id, u.FullName, body)); }
            catch (KeyNotFoundException ex) { return Error(ex.Message, 404); }
        });

        g.MapPost("/documents/{id}/reprocess", (string id, HttpContext c, DocumentStore docs, ProcessingQueue queue) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("إعادة المعالجة");
            var d = docs.Get(id);
            if (d is null) return Error("المستند غير موجود.", 404);
            if (d.FileId == "") return Error("لا يوجد ملف أصلي لهذا المستند.");
            d = docs.Update(id, null, x => { x.Processing = "queued"; x.ProcessingError = ""; }, u.UserName, "طلب إعادة المعالجة", "إعادة معالجة");
            queue.Enqueue(d);
            return Results.Ok(new { ok = true });
        });

        g.MapGet("/documents/{id}/revisions/{version:int}", (string id, int version, HttpContext c, DocumentStore docs) =>
        {
            var u = U(c);
            var snap = docs.RevisionSnapshot(id, version);
            if (snap is null) return Error("النسخة غير موجودة.", 404);
            if (Restricted(snap, u)) return Forbidden("قراءة نسخ مستند حساس");
            return Results.Ok(new { snap.Version, snap.Title, snap.Status, snap.Category, snap.Summary, snap.Text, snap.UpdatedAt, snap.UpdatedBy });
        });

        g.MapGet("/documents/{id}/file", (string id, HttpContext c, DocumentStore docs, FileStore files, AuditStore audit, bool? download) =>
        {
            var u = U(c);
            var d = docs.Get(id);
            if (d is null || d.FileId == "" || !files.Exists(d.FileId)) return Error("الملف الأصلي غير موجود.", 404);
            if (Restricted(d, u)) return Forbidden("فتح أصل مستند حساس");
            audit.Append(u.UserName, "فتح الأصل", d.ArchiveNo);
            var type = Mime(d.FileType);
            var inline = download != true && InlineTypes.Contains(d.FileType);
            return Results.File(files.PathOf(d.FileId), type, inline ? null : d.FileName, enableRangeProcessing: true);
        });
    }

    public static string Mime(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp",
        ".bmp" => "image/bmp", ".tif" or ".tiff" => "image/tiff", ".txt" => "text/plain; charset=utf-8",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", _ => "application/octet-stream",
    };

    private static string FieldLabel(string p) => p switch
    {
        "Title" => "العنوان", "Category" => "التصنيف", "SubCategory" => "التصنيف الفرعي", "Status" => "الحالة", "Direction" => "الاتجاه",
        "Confidentiality" => "السرية", "Priority" => "الأولوية", "SourceEntity" => "الجهة المرسلة", "DestinationEntity" => "الجهة المستقبلة",
        "OutgoingNo" => "رقم الصادر", "IncomingNo" => "رقم الوارد", "ExternalNo" => "رقم الجهة", "DocumentDate" => "تاريخ المستند",
        "DueDate" => "الموعد", "Tags" => "الوسوم", "Summary" => "الملخص", "Text" => "النص", _ => p,
    };
}
