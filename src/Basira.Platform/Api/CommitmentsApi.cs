using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>سجل الالتزامات: ما وعدنا به («علينا») وما وُعدنا به («لنا»)، وحالة كل التزام، وقرارات المستخدمين عليه.</summary>
public static class CommitmentsApi
{
    private sealed record DecisionBody(string? State, string? Note);

    public static object View(Commitment c, Snapshot s, User u)
    {
        var d = s.ById[c.DocumentId];
        object? Ref(string? id) => id is not null && s.ById.TryGetValue(id, out var x) ? new { id = x.Id, archiveNo = x.ArchiveNo, title = x.Title } : null;
        return new
        {
            key = c.Key, side = c.Side, party = c.Party, sentence = c.Sentence, promise = c.Promise, offset = c.Offset, length = c.Length,
            due = c.Due, dueHijri = Hijri(c.Due), dueSource = c.DueSource, defaultDue = c.DefaultDue, state = c.State, daysLeft = c.DaysLeft,
            fulfilledBy = Ref(c.FulfilledBy), fulfilledReason = c.FulfilledReason, respondsTo = Ref(c.RespondsTo),
            doc = Summary(d, s, u),
        };
    }

    public static IEnumerable<Commitment> Visible(Snapshot s, User u) => s.Commitments.Where(c => s.ById.TryGetValue(c.DocumentId, out var d) && !Restricted(d, u));

    public static object Counts(IEnumerable<Commitment> list)
    {
        var l = list.ToList();
        object Side(string side) => new
        {
            open = l.Count(c => c.Side == side && c.State == "open"), overdue = l.Count(c => c.Side == side && c.State == "overdue"),
            fulfilled = l.Count(c => c.Side == side && c.State == "fulfilled"), dismissed = l.Count(c => c.Side == side && c.State == "dismissed"),
        };
        return new { ours = Side(CommitmentLedger.Ours), theirs = Side(CommitmentLedger.Theirs) };
    }

    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/commitments", async (HttpContext c, ArchiveIndex index, string? side, string? state) =>
        {
            var u = U(c);
            var s = await index.GetAsync();
            var all = Visible(s, u).ToList();
            var sideName = side == "theirs" ? CommitmentLedger.Theirs : CommitmentLedger.Ours;
            var items = all.Where(x => x.Side == sideName).Where(x => state switch
            {
                "overdue" => x.State == "overdue",
                "fulfilled" => x.State == "fulfilled",
                "dismissed" => x.State == "dismissed",
                "all" => true,
                _ => x.State is "open" or "overdue",
            }).Take(500).Select(x => View(x, s, u)).ToList();
            return Results.Ok(new { counts = Counts(all), items });
        });

        g.MapPost("/commitments/{key}", async (string key, DecisionBody b, HttpContext c, ArchiveIndex index, CommitmentStore store) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "workflow")) return Forbidden("تحديث الالتزامات");
            if (b.State is null || !CommitmentStore.States.Contains(b.State)) return Error("حالة غير معروفة.");
            var s = await index.GetAsync();
            var item = s.Commitments.FirstOrDefault(x => x.Key == key);
            if (item is null) return Error("الالتزام غير موجود؛ ربما تغيّر نص المستند.", 404);
            if (Restricted(s.ById[item.DocumentId], u)) return Forbidden("مستند حساس");
            var label = b.State switch { "fulfilled" => "تم الوفاء", "dismissed" => "ليس التزاماً", _ => "قائم" };
            store.Set(key, item.DocumentId, b.State, Clip((b.Note ?? "").Trim(), 300), u.UserName, $"{s.ById[item.DocumentId].ArchiveNo} | {label} | {item.Promise}");
            var updated = (await index.GetAsync(TimeSpan.FromSeconds(5))).Commitments.FirstOrDefault(x => x.Key == key);
            return Results.Ok(new { state = updated?.State ?? b.State });
        });
    }
}
