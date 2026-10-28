using AIDocumentArchiveVB;
using System.Text.Json;
using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>مرصد الجهات: العلاقة مع كل جهة، وخطر تأخر الردود، ودمج أسماء الجهة الواحدة.</summary>
public static class EntitiesApi
{
    private const string AliasKey = "entity_aliases";
    private sealed record MergeBody(string? From, string? Into);

    public static Dictionary<string, string> Aliases(SettingsStore s)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(s.GetRaw(AliasKey)) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static object Risk(RiskItem r, Snapshot s, User u)
    {
        var d = s.ById[r.DocumentId];
        var hidden = Restricted(d, u);
        return new { r.DocumentId, archiveNo = d.ArchiveNo, title = hidden ? "مستند مقيد" : d.Title, r.EntityKey, r.Entity, r.Received, r.DaysWaiting, r.Due, r.DueFromText, r.DaysLeft, r.Risk, r.Level, r.Reason, r.TypicalDays, r.Samples };
    }

    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/entities", async (HttpContext c, ArchiveIndex index, SettingsStore settings) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "search")) return Forbidden("عرض الجهات");
            var s = await index.GetAsync();
            var o = EntityObservatory.Build(s, Aliases(settings), DateTime.Today);
            return Results.Ok(new
            {
                organization = settings.Get("organization"),
                o.OurMedianDays, o.OnTimeRate, o.WeOwe, o.WeOweOverdue, o.TheyOwe, o.TheyOweOverdue, o.Months, o.Unassigned,
                entities = o.Entities, risks = o.Risks.Take(40).Select(r => Risk(r, s, u)),
                canMerge = UserStore.Can(u, "edit"),
            });
        });

        g.MapGet("/entities/{key}", async (string key, HttpContext c, ArchiveIndex index, SettingsStore settings) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "search")) return Forbidden("عرض الجهات");
            var s = await index.GetAsync();
            var aliases = Aliases(settings);
            var o = EntityObservatory.Build(s, aliases, DateTime.Today);
            var e = o.Entities.FirstOrDefault(x => x.Key == key);
            if (e is null) return Error("الجهة غير موجودة.", 404);
            var docs = s.Docs.Where(d => o.DocEntity.GetValueOrDefault(d.Id) == key).OrderByDescending(Snapshot.DateOf).ToList();
            var theyOwe = docs.Where(d => d.Direction == "صادر" && s.EffectiveDue.ContainsKey(d.Id) && !BasiraEngine.IsClosed(ArchiveIndex.ToRecord(d, ""))
                && !s.Edges.Any(x => x.To == d.Id && s.ById.TryGetValue(x.From, out var f) && f.Direction == "وارد")).ToList();
            return Results.Ok(new
            {
                entity = e,
                merged = aliases.Where(a => a.Value == key).Select(a => a.Key),
                risks = o.Risks.Where(r => r.EntityKey == key).Select(r => Risk(r, s, u)),
                theyOwe = theyOwe.Select(d => new { doc = Summary(d, s, u), due = s.EffectiveDue[d.Id].Date, daysLeft = (int)((Snapshot.ParseIso(s.EffectiveDue[d.Id].Date) ?? DateTime.Today) - DateTime.Today).TotalDays }),
                recent = docs.Take(40).Select(d => Summary(d, s, u)),
                commitments = s.Commitments.Where(x => x.State is "open" or "overdue" && o.DocEntity.GetValueOrDefault(x.DocumentId) == key)
                    .Select(x => new { x.Key, x.Side, x.Promise, x.Due, x.DaysLeft, x.State, documentId = x.DocumentId }),
                others = o.Entities.Where(x => x.Key != key).Select(x => new { x.Key, x.Name }),
                canMerge = UserStore.Can(u, "edit"),
            });
        });

        // دمج جهتين كُتب اسمهما بصيغتين: كل مستندات «From» تُحسب لـ «Into». ويمكن فك الدمج.
        g.MapPost("/entities/merge", (MergeBody b, HttpContext c, SettingsStore settings) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("دمج الجهات");
            var from = (b.From ?? "").Trim();
            var into = (b.Into ?? "").Trim();
            if (from == "" || into == "" || from == into) return Error("اختر جهتين مختلفتين.");
            var aliases = Aliases(settings);
            // لا حلقات: الجهة الهدف لا تُدمج في المصدر.
            var target = into; var hops = 0;
            while (aliases.TryGetValue(target, out var next) && hops++ < 10) target = next;
            if (target == from) return Error("لا يمكن الدمج: الجهة الهدف مدموجة أصلاً في الجهة الأولى.");
            aliases[from] = target;
            foreach (var k in aliases.Where(a => a.Value == from).Select(a => a.Key).ToList()) aliases[k] = target;
            settings.SetRaw(AliasKey, JsonSerializer.Serialize(aliases), u.UserName, "دمج جهتين");
            return Results.Ok(new { into = target });
        });

        g.MapPost("/entities/unmerge", (MergeBody b, HttpContext c, SettingsStore settings) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "edit")) return Forbidden("دمج الجهات");
            var aliases = Aliases(settings);
            if (b.From is null || !aliases.Remove(b.From)) return Error("هذه الصيغة غير مدموجة.", 404);
            settings.SetRaw(AliasKey, JsonSerializer.Serialize(aliases), u.UserName, "فك دمج جهة");
            return Results.Ok(new { ok = true });
        });
    }
}
