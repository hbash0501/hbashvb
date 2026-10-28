using System.Globalization;
using AIDocumentArchiveVB;
using Basira.Platform.Data;
using Basira.Platform.Intelligence;
using Basira.Platform.Intelligence.Semantic;
using Basira.Platform.Processing;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>
/// استوديو الرد: مسودة رد على خطاب وارد (المطلوب فيه، وسطر الإشارة، ونوع الرد، وسوابق الجهة في ردود مشابهة)،
/// وحفظها صادراً مرتبطاً بأصله في المعاملة: مسودة بانتظار الاعتماد، أو رداً صادراً يُغلق انتظار الرد.
/// </summary>
public static class ReplyApi
{
    public sealed record ReplyBody(string? Subject, string? Addressee, string? ReferenceLine, string? Body, string? SignerTitle, string? SignerName,
        string? OutgoingNo, bool Issue, bool MarkDone);

    public sealed record AiBody(string? Stance, string? Tone, string? Instructions, string? PreviousBody, string? Revision);

    private sealed record Context(Doc Incoming, string Subject, string Entity, string ExternalNo, string Registration, bool Register, string DateHijri);

    private static Context ContextOf(Doc d)
    {
        var header = LetterFields.Parse(d.Text);
        var subject = header.Subject != "" ? header.Subject : d.Title;
        var entity = d.SourceEntity != "" ? d.SourceEntity : header.Sender;
        var external = d.ExternalNo != "" ? d.ExternalNo : header.Number;
        // خطاب بلا رقم جهة ولا قيد: يُقيَّد برقم الأرشيف عند حفظ الرد ليرتبط به.
        var register = external == "" && d.IncomingNo == "";
        var registration = d.IncomingNo != "" ? d.IncomingNo : register ? d.ArchiveNo : "";
        return new Context(d, subject, entity, external, registration, register, Hijri(d.DocumentDate));
    }

    private static bool Answers(Doc reply, Context ctx)
    {
        var numbers = new[] { ctx.ExternalNo, ctx.Registration, ctx.Incoming.ExternalNo, ctx.Incoming.IncomingNo }.Where(n => n != "").ToList();
        return CorrespondenceReferenceParser.Parse(reply.Text).Any(r => numbers.Any(n => CorrespondenceReferenceParser.NumbersMatch(n, r.Number)));
    }

    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/documents/{id}/reply", async (string id, HttpContext c, ArchiveIndex index, SettingsStore settings, SemanticService semantic, string? commitment) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "import")) return Forbidden("صياغة الردود");
            var s = await index.GetAsync();
            if (!s.ById.TryGetValue(id, out var d)) return Error("المستند غير موجود.", 404);
            if (Restricted(d, u)) return Forbidden("الاطلاع على مستند حساس");
            if (d.Direction != "وارد") return Error("صياغة الرد للخطابات الواردة.");
            var ctx = ContextOf(d);

            var asks = ReplyComposer.ExtractAsks(d.Text);
            // من سجل الالتزامات: تعقيب على وعد لهم في هذا الخطاب، أو وفاء بوعد قطعناه في ردنا عليه.
            var pledge = commitment is null ? null : s.Commitments.FirstOrDefault(x => x.Key == commitment && (x.DocumentId == d.Id || x.RespondsTo == d.Id));
            var special = new List<ReplyIntent>();
            if (pledge is not null && pledge.Side == CommitmentLedger.Theirs)
                special.Add(new("followup", "تعقيب على وعدهم", ReplyComposer.FollowUp(pledge.Promise, pledge.State == "overdue" ? Hijri(pledge.Due) : "")));
            else if (pledge is not null && s.ById.TryGetValue(pledge.DocumentId, out var ours))
                special.Add(new("fulfil", "وفاء بما وعدنا", ReplyComposer.Fulfil(pledge.Promise, ours.OutgoingNo != "" ? ours.OutgoingNo : ours.ArchiveNo, Hijri(ours.DocumentDate))));
            var signer = ReplyComposer.SignerTitle(d.Text);
            s.EffectiveDue.TryGetValue(d.Id, out var due);
            var today = DateTime.Today;
            int? daysLeft = Snapshot.ParseIso(due.Date ?? "") is { } dd ? (int)(dd.Date - today).TotalDays : null;

            // ردود هذا الخطاب الموجودة (بانتظار الاعتماد أو صادرة).
            var existing = s.Edges.Where(e => e.To == d.Id && s.ById.TryGetValue(e.From, out var r) && r.Direction == "صادر")
                .Select(e => s.ById[e.From]).DistinctBy(r => r.Id)
                .Select(r => new { id = r.Id, archiveNo = r.ArchiveNo, title = r.Title, status = r.Status, pending = r.Status == ArchiveIndex.PendingApproval }).ToList();

            return Results.Ok(new
            {
                incoming = new
                {
                    id = d.Id, archiveNo = d.ArchiveNo, title = d.Title, subject = ctx.Subject, entity = ctx.Entity, externalNo = ctx.ExternalNo,
                    registration = ctx.Registration, willRegister = ctx.Register, date = d.DocumentDate, dateHijri = ctx.DateHijri, category = d.Category, subCategory = d.SubCategory,
                },
                deadline = due.Date is null ? null : new { date = due.Date, hijri = Hijri(due.Date), source = due.Source, daysLeft },
                asks = asks.Select(a => new { text = a.Text, kind = a.Kind, gist = a.Gist }),
                intents = special.Concat(ReplyComposer.Intents(asks)).Select(i => new { key = i.Key, label = i.Label, body = i.Body }),
                suggestedIntent = special.Count > 0 ? special[0].Key : ReplyComposer.SuggestIntent(d.Text, asks),
                commitment = pledge is null ? null : CommitmentsApi.View(pledge, s, u),
                precedents = Precedents(s, d, ctx, u, semantic).Select(p => new
                {
                    incomingId = p.IncomingId, incomingArchiveNo = p.IncomingArchiveNo, incomingTitle = p.IncomingTitle,
                    replyId = p.ReplyId, replyArchiveNo = p.ReplyArchiveNo, replyTitle = p.ReplyTitle, replyDate = p.ReplyDate,
                    similarity = Math.Round(p.Similarity * 100), sharedTerms = p.SharedTerms, body = p.Body,
                }),
                existing,
                draft = new
                {
                    subject = ctx.Subject,
                    addressee = ReplyComposer.AddresseeLine(signer, ctx.Entity),
                    referenceLine = ReplyComposer.ReferenceLine(ctx.ExternalNo, ctx.Registration, ctx.DateHijri, ctx.Subject),
                    dateHijri = Hijri(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    organization = settings.Get("organization"),
                },
                greeting = ReplyComposer.Greeting,
                closing = ReplyComposer.Closing,
                can = new { issue = UserStore.Can(u, "workflow") },
                ai = AiStatus(settings, d),
            });
        });

        // الرد بالذكاء الاصطناعي: فهم الخطاب بسياقه ثم كتابة نص الرد، بالموقف والنبرة وتعليمات المستخدم.
        g.MapPost("/documents/{id}/reply/ai", async (string id, AiBody b, HttpContext c, ArchiveIndex index, SettingsStore settings, SemanticService semantic, AuditStore audit, CancellationToken ct) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "import")) return Forbidden("صياغة الردود");
            var s = await index.GetAsync();
            if (!s.ById.TryGetValue(id, out var d)) return Error("المستند غير موجود.", 404);
            if (Restricted(d, u)) return Forbidden("الاطلاع على مستند حساس");
            if (d.Direction != "وارد") return Error("صياغة الرد للخطابات الواردة.");
            var (options, reason) = WriterApi.Configured(settings);
            if (options is null) return Error(reason);
            var cloud = options.IsCloud;
            bool Sensitive(Doc x) => BasiraEngine.IsSensitive(ArchiveIndex.ToRecord(x, ""));
            if (cloud && Sensitive(d)) return Error("مستند غير عادي السرية: لا يُرسل إلى خدمة سحابية. اكتب الرد يدوياً أو استخدم نموذجاً محلياً.", 403);
            var ctx = ContextOf(d);
            s.EffectiveDue.TryGetValue(d.Id, out var due);
            // المعاملة: الخطابات السابقة في الخيط نفسه (لا يُرسل منها الحساس إلى السحابة ولا ما لا يحق للمستخدم).
            var thread = s.ThreadOf.TryGetValue(d.Id, out var tid) && s.Threads.TryGetValue(tid, out var th)
                ? th.DocumentIds.Where(x => x != d.Id && s.ById.ContainsKey(x)).Select(x => s.ById[x])
                    .Where(x => !Restricted(x, u) && !(cloud && Sensitive(x)) && Snapshot.DateOf(x) <= Snapshot.DateOf(d).AddDays(1))
                    .OrderBy(Snapshot.DateOf).TakeLast(6)
                    .Select(x => (Hijri(x.DocumentDate), x.Direction, x.Title, x.Text)).ToList()
                : new List<(string, string, string, string)>();
            var precedents = Precedents(s, d, ctx, u, semantic).Where(p => !(cloud && s.ById.TryGetValue(p.ReplyId, out var r) && Sensitive(r))).Take(2).Select(p => p.Body).ToList();
            var input = new WriterInput(settings.Get("organization"), ctx.Entity, ctx.Subject, ctx.ExternalNo, ctx.DateHijri, due.Date is null ? "" : Hijri(due.Date),
                d.Text, thread, precedents, b.Stance ?? "auto", b.Tone ?? "formal", Clip((b.Instructions ?? "").Trim(), 2000),
                b.PreviousBody is { Length: > 0 } prev ? Clip(prev, 20000) : null, Clip((b.Revision ?? "").Trim(), 500));
            try
            {
                var reply = await AiReplyWriter.WriteAsync(options, input, ct);
                audit.Append(u.UserName, "صياغة رد بالذكاء الاصطناعي", $"{d.ArchiveNo}: {options.Label} ({options.Model})" + (input.PreviousBody is null ? "" : " — تعديل"));
                return Results.Ok(reply);
            }
            catch (WriterException ex) { return Error(ex.Message, 502); }
        });

        g.MapPost("/documents/{id}/reply", async (string id, ReplyBody b, HttpContext c, ArchiveIndex index, DocumentStore docs, SettingsStore settings) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "import")) return Forbidden("صياغة الردود");
            if (b.Issue && !UserStore.Can(u, "workflow")) return Forbidden("إصدار الردود");
            var d = docs.Get(id);
            if (d is null) return Error("المستند غير موجود.", 404);
            if (Restricted(d, u)) return Forbidden("الرد على مستند حساس");
            if (d.Direction != "وارد") return Error("صياغة الرد للخطابات الواردة.");
            var ctx = ContextOf(d);

            var body = Clip((b.Body ?? "").Trim(), 20000);
            if (body.Length < 10) return Error("اكتب نص الرد.");
            var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var letter = new ReplyLetter(Clip((b.OutgoingNo ?? "").Trim(), 60), Hijri(today), Clip((b.Subject ?? ctx.Subject).Trim(), 200),
                Clip((b.Addressee ?? "").Trim(), 200), Clip((b.ReferenceLine ?? "").Trim(), 400), body, Clip((b.SignerTitle ?? "").Trim(), 120), Clip((b.SignerName ?? "").Trim(), 120));
            if (letter.Addressee == "") return Error("حدد المخاطَب.");
            var text = ReplyComposer.Compose(letter);
            var missing = ReplyComposer.Placeholders(text);
            if (b.Issue && missing.Count > 0) return Error("أكمل الحقول قبل الإصدار: " + string.Join("، ", missing));

            var reply = new Doc
            {
                Direction = "صادر", Title = Clip("رد: " + letter.Subject, 300), Processing = "done", Text = text,
                Status = b.Issue ? "منجز" : ArchiveIndex.PendingApproval,
                SourceEntity = settings.Get("organization"), DestinationEntity = ctx.Entity, OutgoingNo = letter.OutgoingNo, DocumentDate = today,
                Category = d.Category, SubCategory = d.SubCategory, Confidentiality = d.Confidentiality, Priority = d.Priority,
                Summary = Clip(body.Split('\n')[0], 300),
            };
            // يتبع الرد تصنيف أصله، وبالصفة نفسها (آلي أو مؤكد).
            reply.SetAuto("category", d.IsAuto("category"));
            reply.SetAuto("sub_category", d.IsAuto("sub_category"));
            if (!Answers(reply, ctx)) return Error("سطر الإشارة لا يتضمن رقم الخطاب الوارد، فلن يرتبط الرد به في المعاملة. أعد سطر الإشارة المقترح أو اكتب الرقم فيه.");

            try
            {
                if (ctx.Register)
                    docs.Update(d.Id, null, x => x.IncomingNo = d.ArchiveNo, u.UserName, $"قيد الوارد برقم {d.ArchiveNo} لربط الرد به", "قيد وارد");
                reply = docs.Create(reply, u.UserName, b.Issue ? "إصدار رد" : "مسودة رد بانتظار الاعتماد");
                docs.AddComment(d.Id, u.UserName, b.Issue ? $"صدر الرد {reply.ArchiveNo}." : $"أُعدت مسودة الرد {reply.ArchiveNo} وهي بانتظار الاعتماد.");
                if (b.Issue && b.MarkDone && !BasiraEngine.IsClosed(ArchiveIndex.ToRecord(docs.Get(d.Id)!, "")))
                    docs.Update(d.Id, null, x => x.Status = "منجز", u.UserName, $"الحالة: منجز — صدر الرد {reply.ArchiveNo}", "تغيير حالة");
            }
            catch (ConcurrencyException ex) { return Error(ex.Message, 409); }
            await index.GetAsync(TimeSpan.FromSeconds(5));
            return Results.Ok(new { id = reply.Id, archiveNo = reply.ArchiveNo, status = reply.Status, placeholders = missing });
        });
    }

    private static object AiStatus(SettingsStore settings, Doc d)
    {
        var (o, reason) = WriterApi.Configured(settings);
        var blocked = o is { IsCloud: true } && BasiraEngine.IsSensitive(ArchiveIndex.ToRecord(d, ""));
        return new
        {
            available = o is not null && !blocked, provider = o?.Provider, label = o?.Label, model = o?.Model, cloud = o?.IsCloud ?? false,
            reason = blocked ? "الخطاب غير عادي السرية فلا يُرسل إلى خدمة سحابية." : reason,
        };
    }

    /// <summary>ردود الجهة الصادرة على خطابات واردة تشبه هذا الخطاب، الأحدث لكل خطاب، والأشبه أولاً (ثلاثة على الأكثر).</summary>
    private static List<ReplyPrecedent> Precedents(Snapshot s, Doc d, Context ctx, User u, SemanticService semantic)
    {
        var pairs = s.Edges
            .Where(e => e.To != d.Id && s.ById.TryGetValue(e.From, out var r) && s.ById.TryGetValue(e.To, out var i)
                        && r.Direction == "صادر" && i.Direction == "وارد" && r.Status != ArchiveIndex.PendingApproval && !Restricted(r, u) && !Restricted(i, u))
            .GroupBy(e => e.To)
            .Select(grp => (Incoming: s.ById[grp.Key], Reply: grp.Select(e => s.ById[e.From]).OrderByDescending(Snapshot.DateOf).First()))
            .ToList();
        if (pairs.Count == 0) return new();
        static string Sample(Doc x) => x.Title + "\n" + (x.Text.Length > 4000 ? x.Text[..4000] : x.Text);
        var ranked = ReplyComposer.Rank(Sample(d), pairs.Select(p => Sample(p.Incoming)).ToList()).Where(r => r.Score >= 0.12).Take(12).ToList();

        // إن وُجد النموذج الدلالي يُعاد ترتيب المرشحين بالمعنى (التشابه المعروض يبقى تشابه الكلمات المفسَّر بالكلمات المشتركة).
        if (semantic.Model is { } m && ranked.Count > 1)
        {
            try
            {
                float[] Vec(Doc x)
                {
                    var t = SemanticService.DocumentText(x.Title, x.Text);
                    var h = SemanticService.Hash(t);
                    if (semantic.Stored(x.Id, m.Id, h) is { } v) return v;
                    v = m.Embed(t);
                    semantic.Store(x.Id, m.Id, h, v);
                    return v;
                }
                var q = Vec(d);
                ranked = ranked.OrderByDescending(r => 0.5 * r.Score + 0.5 * SemanticModel.Cosine(q, Vec(pairs[r.Index].Incoming))).ToList();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* يبقى ترتيب الكلمات */ }
        }

        return ranked.Take(3).Select(r =>
        {
            var (inc, rep) = pairs[r.Index];
            var oldCtx = ContextOf(inc);
            var body = ReplyComposer.AdaptPrecedent(rep.Text, inc.Text, new[] { oldCtx.Subject, oldCtx.Entity }, new[] { ctx.Subject, ctx.Entity });
            return new ReplyPrecedent(inc.Id, inc.ArchiveNo, inc.Title, rep.Id, rep.ArchiveNo, rep.Title, rep.DocumentDate, r.Score, r.Shared, body);
        }).Where(p => p.Body.Length >= 10).ToList();
    }
}
