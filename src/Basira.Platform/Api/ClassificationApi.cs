using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence;
using Basira.Platform.Processing;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>التصنيف الآلي: تأكيده أو تغييره لكل مستند، وشجرة التصنيف، وإعادة تصنيف الأرشيف، والدقة الفعلية من قرارات المستخدمين.</summary>
public static class ClassificationApi
{
    private sealed record ClassBody(string? Action, string? Category, string? SubCategory, int? Version);
    private sealed record ReclassifyBody(string? Scope);

    public static object TaxonomyView(Taxonomy t) => t.Categories.Select(c => new { name = c.Name, keywords = c.Keywords, subs = c.Subs.Select(s => new { name = s.Name, keywords = s.Keywords }) });

    public static void Map(RouteGroupBuilder g)
    {
        g.MapPost("/documents/{id}/classification", (string id, ClassBody b, HttpContext c, DocumentStore docs, ClassificationService classifier) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("تعديل التصنيف");
            var d = docs.Get(id);
            if (d is null) return Error("المستند غير موجود.", 404);
            if (Restricted(d, u)) return Forbidden("تعديل مستند حساس");
            var confirm = b.Action == "confirm";
            if (!confirm && b.Action != "set") return Error("إجراء غير معروف.");
            var category = confirm ? d.Category : ApiCommon.Clip((b.Category ?? "").Trim(), 80);
            var sub = confirm ? d.SubCategory : ApiCommon.Clip((b.SubCategory ?? "").Trim(), 80);
            if (category == "") return Error("اختر التصنيف العام.");
            try
            {
                var updated = docs.Update(id, b.Version, x =>
                {
                    x.Category = category; x.SubCategory = sub;
                    x.SetAuto("category", false); x.SetAuto("sub_category", false);
                }, u.UserName, confirm ? $"تأكيد التصنيف الآلي: {category} / {sub}" : $"تصنيف: {category} / {sub}", confirm ? "تأكيد تصنيف" : "تعديل تصنيف");
                classifier.RecordFeedback(d, category, sub, confirm ? "confirmed" : "changed", u.UserName);
                classifier.Invalidate();
                return Results.Ok(new { version = updated.Version });
            }
            catch (ConcurrencyException ex) { return Error(ex.Message, 409); }
        });

        g.MapGet("/taxonomy", (ClassificationService classifier) => Results.Ok(TaxonomyView(classifier.Taxonomy)));

        g.MapGet("/classification/overview", (HttpContext c, ClassificationService classifier, DocumentStore docs) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "manage_categories")) return Forbidden("إدارة التصنيفات");
            var t = classifier.Taxonomy;
            var all = docs.All();
            var unmapped = all.Where(d => d.Category != "" && t.Find(d.Category) is null).GroupBy(d => d.Category)
                .Select(x => new { name = x.Key, count = x.Count() }).OrderByDescending(x => x.count).ToList();
            var s = classifier.Stats();
            return Results.Ok(new
            {
                taxonomy = TaxonomyView(t), unmapped,
                counts = new { total = all.Count, unclassified = all.Count(d => d.Category == ""), auto = s.AutoPending, training = classifier.Model.TrainingDocs },
                feedback = new
                {
                    reviewed = s.Reviewed, confirmed = s.Confirmed, changed = s.Changed,
                    accuracy = s.Reviewed == 0 ? (double?)null : Math.Round(100.0 * s.Confirmed / s.Reviewed, 1),
                    confidentReviewed = s.ConfidentReviewed,
                    confidentAccuracy = s.ConfidentReviewed == 0 ? (double?)null : Math.Round(100.0 * s.ConfidentCorrect / s.ConfidentReviewed, 1),
                },
            });
        });

        g.MapPut("/taxonomy", (Taxonomy body, HttpContext c, ClassificationService classifier) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "manage_categories")) return Forbidden("إدارة التصنيفات");
            try { classifier.SaveTaxonomy(body, u.UserName); return Results.Ok(TaxonomyView(classifier.Taxonomy)); }
            catch (ArgumentException ex) { return Error(ex.Message); }
        });

        // يصنّف المستندات غير المصنفة (أو المصنفة آلياً فقط) دون مساس بما صنّفه المستخدمون.
        g.MapPost("/classification/reclassify", (ReclassifyBody b, HttpContext c, ClassificationService classifier, DocumentStore docs) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "manage_categories")) return Forbidden("إدارة التصنيفات");
            var includeAuto = b.Scope == "auto";
            int changed = 0, skipped = 0;
            foreach (var d in docs.All())
            {
                if (!(d.Category == "" || includeAuto && d.IsAuto("category")) || d.Text == "") { skipped++; continue; }
                var r = classifier.Classify(d.Text, d.Title, d.Id);
                if (r.Category == "" || r.Category == d.Category && r.SubCategory == d.SubCategory) { skipped++; continue; }
                docs.Update(d.Id, null, x => classifier.Apply(x, r), ProcessingQueue.SystemActor, $"تصنيف آلي: {r.Category} / {r.SubCategory}", "تصنيف آلي");
                changed++;
            }
            return Results.Ok(new { changed, skipped });
        });

        // يعيد توليد العناوين الآلية (وما بقي باسم الملف) من المحتوى؛ لا يمس عنواناً كتبه مستخدم.
        g.MapPost("/titles/rebuild", (HttpContext c, DocumentStore docs) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("تعديل المستندات");
            int changed = 0, kept = 0;
            foreach (var d in docs.All())
            {
                var stem = Path.GetFileNameWithoutExtension(d.FileName);
                if (!(d.IsAuto("title") || d.Title == "" || stem != "" && d.Title == stem)) { kept++; continue; }
                var header = LetterFields.Parse(d.Text);
                var title = TitleBuilder.ForDocument(d.Text, header.Subject, d.FileName, d.Direction,
                    d.Direction == "صادر" ? d.DestinationEntity : d.SourceEntity != "" ? d.SourceEntity : header.Sender,
                    d.Category, d.SubCategory, Clock.Hijri(d.DocumentDate));
                if (title == "" || title == d.Title) { kept++; continue; }
                docs.Update(d.Id, null, x => { x.Title = title; x.SetAuto("title", true); }, u.UserName, "عنوان من المحتوى: " + title, "تحديث عنوان");
                changed++;
            }
            return Results.Ok(new { changed, kept });
        });
    }
}
