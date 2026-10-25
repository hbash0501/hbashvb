using System.Globalization;
using System.Text.RegularExpressions;
using AIDocumentArchiveVB;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>
/// طابور التحقق: كل حقل حرج قرأه OCR ولم تتفق عليه القراءات أو انخفضت ثقته يُعرض هنا بمقتطع صورته،
/// فيؤكده المراجع أو يصححه. لا تُبنى عليه روابط ولا مهل ولا تنبيهات قبل ذلك.
/// </summary>
public static class ReviewApi
{
    private sealed record ReviewBody(string? Action, string? Value);

    public static string FieldLabel(string field) => field switch
    {
        "number" => "رقم الخطاب", "date" => "تاريخ الخطاب", "subject" => "الموضوع", "reference" => "إشارة إلى خطاب", "deadline" => "مهلة", _ => field,
    };

    public static object Describe(FieldCheckRow k) => new
    {
        id = k.Id, field = k.Field, fieldLabel = FieldLabel(k.Field), value = k.Value, candidates = k.Candidates, state = k.State, reason = k.Reason,
        spanText = k.SpanText, offset = k.Offset, length = k.Length, hasCrop = k.CropId != "", finalValue = k.FinalValue,
        reviewedBy = k.ReviewedBy, reviewedAt = k.ReviewedAt,
    };

    /// <summary>مستندات قد تكون المقصودة بإشارة غير مؤكدة: رقم مطابق، أو يختلف بحرف واحد.</summary>
    private static List<object> Suggestions(FieldCheckRow k, Snapshot s)
    {
        if (k.Field != "reference") return new();
        var parsed = CorrespondenceReferenceParser.Parse(k.SpanText).FirstOrDefault();
        var number = parsed?.Number ?? CorrespondenceReferenceParser.NumberKey(k.Value.Split('@')[0]);
        if (number == "") return new();
        var list = new List<object>();
        foreach (var d in s.Docs)
        {
            if (d.Id == k.DocumentId) continue;
            foreach (var n in new[] { d.OutgoingNo, d.IncomingNo, d.ExternalNo, d.ArchiveNo }.Where(x => x != "").Distinct())
            {
                var key = CorrespondenceReferenceParser.NumberKey(n);
                var exact = CorrespondenceReferenceParser.NumbersMatch(key, number);
                if (!exact && !ArchiveIndex.OneEditApart(key, number)) continue;
                list.Add(new { id = d.Id, archiveNo = d.ArchiveNo, title = d.Title, number = n, exact, documentDate = d.DocumentDate, documentDateHijri = Hijri(d.DocumentDate) });
                break;
            }
            if (list.Count >= 5) break;
        }
        return list;
    }

    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/review/count", (ReviewStore review) => Results.Ok(new { pending = review.PendingCount() }));

        g.MapGet("/review", async (HttpContext c, ReviewStore review, ArchiveIndex index) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("التحقق من القراءة الآلية");
            var s = await index.GetAsync();
            var items = new List<object>();
            var hidden = 0;
            foreach (var k in review.PendingAll())
            {
                if (!s.ById.TryGetValue(k.DocumentId, out var d)) continue;
                if (Restricted(d, u)) { hidden++; continue; }
                items.Add(new
                {
                    check = Describe(k),
                    document = new { id = d.Id, archiveNo = d.ArchiveNo, title = d.Title, direction = d.Direction, scanQuality = d.ScanQuality },
                    suggestions = Suggestions(k, s),
                });
            }
            return Results.Ok(new { items, hidden });
        });

        g.MapGet("/review/{id:long}/crop", (long id, HttpContext c, ReviewStore review, DocumentStore docs, FileStore files) =>
        {
            var u = U(c);
            var k = review.Get(id);
            if (k is null || k.CropId == "" || !files.Exists(k.CropId)) return Error("لا يوجد مقتطع لهذا الحقل.", 404);
            var d = docs.Get(k.DocumentId);
            if (d is null) return Error("المستند غير موجود.", 404);
            if (Restricted(d, u)) return Forbidden("عرض مقتطع من مستند حساس");
            return Results.File(files.PathOf(k.CropId), "image/png");
        });

        g.MapPost("/review/{id:long}", (long id, ReviewBody body, HttpContext c, ReviewStore review, DocumentStore docs) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("التحقق من القراءة الآلية");
            var k = review.Get(id);
            if (k is null) return Error("الفحص غير موجود.", 404);
            if (k.State != CheckStates.Pending) return Error("حُسم هذا الحقل من قبل؛ أعد تحميل الصفحة.", 409);
            var d = docs.Get(k.DocumentId);
            if (d is null) return Error("المستند غير موجود.", 404);
            if (Restricted(d, u)) return Forbidden("تعديل مستند حساس");
            try
            {
                Apply(docs, review, k, body.Action ?? "", body.Value, u);
                return Results.Ok(new { pending = review.PendingCount() });
            }
            catch (ArgumentException ex) { return Error(ex.Message); }
        });
    }

    /// <summary>تأكيد القراءة أو تصحيحها: يُستبدل النص في موضعه، ويُحدَّث الحقل المرتبط، ويُحسم الفحص، في معاملة واحدة مع التدقيق.</summary>
    public static void Apply(DocumentStore docs, ReviewStore review, FieldCheckRow k, string action, string? value, User u)
    {
        if (action is not ("confirm" or "correct")) throw new ArgumentException("إجراء غير معروف.");
        var confirm = action == "confirm";
        var text = confirm ? k.SpanText : Clip((value ?? "").Trim(), 600);
        if (!confirm && text == "") throw new ArgumentException("اكتب القراءة الصحيحة كما في الصورة.");
        string iso = "";
        if (k.Field == "date")
        {
            var canonical = ""; DateTime v = default;
            if (text != "" && !CorrespondenceReferenceParser.TryParseDate(text, ref canonical, ref v))
                throw new ArgumentException("تاريخ غير صالح؛ اكتبه كما في الصورة، مثل 1446/08/07هـ أو 7 شعبان 1446هـ.");
            iso = text == "" ? "" : v.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (k.Field == "number" && !confirm && !Regex.IsMatch(text, "[0-9٠-٩]")) throw new ArgumentException("رقم الخطاب يجب أن يحوي أرقاماً.");
        if (k.Field == "reference" && !confirm && CorrespondenceReferenceParser.Parse(text).Count == 0)
            throw new ArgumentException("لم يُتعرف على إشارة في النص المصحح؛ اكتب العبارة كاملة كما في الصورة («إشارة إلى خطابكم رقم … وتاريخ …»).");
        if (k.Field == "deadline" && !confirm && DeadlineExtractor.Extract(text, null).Count == 0)
            throw new ArgumentException("لم يُتعرف على مهلة في النص المصحح؛ اكتب العبارة كما في الصورة («خلال (15) يوماً»).");
        var label = FieldLabel(k.Field);
        int newOffset = k.Offset, delta = 0;
        docs.Update(k.DocumentId, null, d =>
        {
            if (!confirm && k.SpanText != "")
            {
                var at = k.Offset >= 0 && k.Offset + k.SpanText.Length <= d.Text.Length && string.CompareOrdinal(d.Text, k.Offset, k.SpanText, 0, k.SpanText.Length) == 0
                    ? k.Offset : d.Text.IndexOf(k.SpanText, StringComparison.Ordinal);
                if (at >= 0) { d.Text = d.Text[..at] + text + d.Text[(at + k.SpanText.Length)..]; newOffset = at; delta = text.Length - k.SpanText.Length; }
            }
            switch (k.Target)
            {
                case "outgoing_no" when !confirm: d.OutgoingNo = Clip(text, 60); break;
                case "external_no" when !confirm: d.ExternalNo = Clip(text, 60); break;
                case "document_date" when iso != "": d.DocumentDate = iso; break;
                case "title" when !confirm && (d.Title == k.SpanText || d.Title == k.Value): d.Title = Clip(text, 300); break;
            }
        }, u.UserName, confirm ? $"تأكيد القراءة الآلية: {label}" : $"تصحيح القراءة الآلية: {label} «{Clip(k.SpanText, 80)}» ← «{Clip(text, 80)}»", "تحقق من قراءة آلية",
            (c, tx, _) => ReviewStore.Resolve(c, tx, k, confirm ? CheckStates.Confirmed : CheckStates.Corrected, text, u.UserName, newOffset, confirm ? k.Length : text.Length, delta));
        review.NotifyChanged();
    }
}
