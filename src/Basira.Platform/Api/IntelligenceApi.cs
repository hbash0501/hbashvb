using System.Globalization;
using System.Text;
using AIDocumentArchiveVB;
using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using Basira.Platform.Processing;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

public static class IntelligenceApi
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/dashboard", async (HttpContext c, ArchiveIndex index, ProcessingQueue queue) =>
        {
            var u = U(c);
            var s = await index.GetAsync();
            var today = DateTime.Today;
            var open = s.Docs.Where(d => !BasiraEngine.IsClosed(ArchiveIndex.ToRecord(d, ""))).ToList();
            var dueSoon = open.Where(d => s.EffectiveDue.ContainsKey(d.Id))
                .Select(d => (Doc: d, Due: Snapshot.ParseIso(s.EffectiveDue[d.Id].Date)))
                .Where(x => x.Due is not null && x.Due.Value <= today.AddDays(7)).OrderBy(x => x.Due).Take(12)
                .Select(x => new { doc = Summary(x.Doc, s, u), days = (int)(x.Due!.Value - today).TotalDays }).ToList();
            var weeks = Enumerable.Range(0, 12).Select(i => today.AddDays(-7 * (11 - i) - (int)today.DayOfWeek)).ToList();
            var activity = weeks.Select(w => new
            {
                week = w.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                incoming = s.Docs.Count(d => d.Direction == "وارد" && InWeek(d, w)),
                outgoing = s.Docs.Count(d => d.Direction == "صادر" && InWeek(d, w)),
            }).ToList();
            return Results.Ok(new
            {
                metrics = s.Metrics, total = s.Docs.Count, open = open.Count,
                awaitingTotal = s.Awaiting.Count, threadsTotal = s.Threads.Count, ghostsTotal = s.Ghosts.Count,
                activeThreads = s.Threads.Values.Count(t => t.State != "منجزة"),
                awaiting = s.Awaiting.Take(8).Select(a => new { doc = Summary(s.ById[a.DocumentId], s, u), days = a.DaysWaiting }),
                dueSoon, activity,
                findings = s.Findings.Take(12).Select(f => new { f.DocumentId, f.Title, f.Kind, f.Severity, f.Reason }),
                categories = s.Docs.GroupBy(d => d.Category == "" ? "غير مصنّف" : d.Category).OrderByDescending(x => x.Count()).Take(8).Select(x => new { name = x.Key, count = x.Count() }),
                statuses = s.Docs.GroupBy(d => d.Status).OrderByDescending(x => x.Count()).Select(x => new { name = x.Key, count = x.Count() }),
                pending = queue.Pending, replyDays = s.ReplyDays, builtAt = s.BuiltAt,
            });
        });

        g.MapGet("/threads", async (HttpContext c, ArchiveIndex index, string? state, string? q) =>
        {
            var u = U(c);
            var s = await index.GetAsync();
            IEnumerable<ThreadInfo> list = s.Threads.Values;
            if (!string.IsNullOrEmpty(state)) list = list.Where(t => t.State == state);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var hits = s.Engine.Search(q, "", "all", 500).Select(h => h.Document.Id).ToHashSet();
                list = list.Where(t => t.DocumentIds.Any(hits.Contains));
            }
            return Results.Ok(list.OrderByDescending(t => t.LastActivity, StringComparer.Ordinal).ThenByDescending(t => t.DocumentIds.Count).Take(200).Select(t => new
            {
                t.Id, subject = Restricted(s.ById[t.DocumentIds[0]], u) ? s.ById[t.DocumentIds[0]].ArchiveNo : t.Subject, t.State, t.Started, t.LastActivity, t.Parties, t.AwaitingCount,
                documents = t.DocumentIds.Count, missing = t.Ghosts.Count,
                directions = t.DocumentIds.Select(id => s.ById[id].Direction).ToList(),
            }));
        });

        g.MapGet("/threads/{id}", async (string id, HttpContext c, ArchiveIndex index) =>
        {
            var u = U(c);
            var s = await index.GetAsync();
            if (!s.Threads.TryGetValue(id, out var t)) return Error("المعاملة غير موجودة.", 404);
            var members = t.DocumentIds.ToHashSet();
            return Results.Ok(new
            {
                t.Id, t.Subject, t.State, t.Started, t.LastActivity, t.Parties, t.AwaitingCount,
                documents = t.DocumentIds.Select(d => Summary(s.ById[d], s, u)),
                missing = t.Ghosts.Select(gh => new { gh.Key, gh.Number, gh.DateText, gh.Kind, gh.Owner, gh.Issuer, citedBy = gh.CitedBy.Where(members.Contains) }),
                edges = s.Edges.Where(e => members.Contains(e.From)).Select(e => new { from = e.From, to = e.To, kind = e.Kind })
                    .Concat(t.Ghosts.SelectMany(gh => gh.CitedBy.Where(members.Contains).Select(from => new { from, to = gh.Key, kind = "يشير إلى" }))),
            });
        });

        g.MapGet("/missing", async (HttpContext c, ArchiveIndex index) =>
        {
            var s = await index.GetAsync();
            return Results.Ok(s.Ghosts.OrderByDescending(gh => gh.CitedBy.Count).Take(200).Select(gh => new
            {
                gh.Key, gh.Number, gh.DateText, gh.Kind, gh.Owner, gh.Issuer,
                citedBy = gh.CitedBy.Select(id => new { id, archiveNo = s.ById[id].ArchiveNo, title = s.ById[id].Title }),
            }));
        });

        g.MapGet("/ask", async (HttpContext c, ArchiveIndex index, AuditStore audit, string? q) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "search")) return Forbidden("البحث");
            q = Clip((q ?? "").Trim(), 300);
            if (q == "") return Error("اكتب سؤالاً.");
            var s = await index.GetAsync();
            audit.Append(u.UserName, "سؤال", q);
            var answer = s.Engine.LocalAnswer(q);
            return Results.Ok(new
            {
                question = q, found = answer.Evidence.Count > 0,
                evidence = answer.Evidence.Select(e =>
                {
                    var d = s.ById[e.DocumentId];
                    var restricted = Restricted(d, u);
                    return new { e.Number, documentId = e.DocumentId, e.Title, archiveNo = d.ArchiveNo, restricted, quote = restricted ? "" : e.Quote, thread = s.ThreadOf.TryGetValue(d.Id, out var th) && s.Threads.ContainsKey(th) ? th : null };
                }),
            });
        });

        g.MapGet("/report", async (HttpContext c, ArchiveIndex index, AuditStore audit) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "export")) return Forbidden("التصدير");
            var s = await index.GetAsync();
            audit.Append(u.UserName, "تقرير صحة الأرشيف", "");
            var records = s.Docs.Select(d => ArchiveIndex.ToRecord(d, s.EffectiveDue.TryGetValue(d.Id, out var x) ? x.Date : "")).ToList();
            var html = ArchiveHealthReportService.Build(records, s.Findings.ToList(), u.FullName, DateTime.Now);
            c.Response.Headers.ContentDisposition = "attachment; filename=\"Basira_Health_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".html\"";
            return Results.Text(html, "text/html", Encoding.UTF8);
        });

        g.MapGet("/meta", async (ArchiveIndex index, ClassificationService classifier) =>
        {
            var s = await index.GetAsync();
            var tax = classifier.Taxonomy;
            return Results.Ok(new
            {
                statuses = Roles.Statuses, directions = Roles.Directions, confidentialities = Roles.Confidentialities, roles = Roles.All,
                categories = tax.Categories.Select(c => c.Name).Concat(s.Docs.Select(d => d.Category).Where(x => x != "" && tax.Find(x) is null).Distinct().OrderBy(x => x)),
                taxonomy = tax.Categories.Select(c => new { name = c.Name, subs = c.Subs.Select(x => x.Name) }),
                entities = s.Docs.SelectMany(d => new[] { d.SourceEntity, d.DestinationEntity }).Where(x => x != "").GroupBy(x => x).OrderByDescending(x => x.Count()).Take(60).Select(x => x.Key),
            });
        });

        g.MapGet("/audit", (HttpContext c, AuditStore audit, int? take, string? q) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "audit")) return Forbidden("سجل التدقيق");
            var v = audit.Verify();
            return Results.Ok(new { valid = v.Valid, count = v.Count, firstBroken = v.FirstBroken, message = v.Message, lastHash = v.LastHash, entries = audit.Recent(Math.Clamp(take ?? 100, 1, 500), null, q) });
        });
    }

    private static bool InWeek(Doc doc, DateTime weekStart) { var d = Snapshot.DateOf(doc); return d >= weekStart && d < weekStart.AddDays(7); }
}
