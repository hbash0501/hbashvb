using System.Globalization;
using AIDocumentArchiveVB;
using Basira.Platform.Data;

namespace Basira.Platform.Intelligence;

public sealed record RefInfo(CorrespondenceReference Reference, string? TargetId, string GhostKey)
{
    /// <summary>قراءة الإشارة غير مؤكدة: لا رابط ولا خطاب غائب حتى يتحقق منها مراجع.</summary>
    public bool Pending { get; init; }
    /// <summary>مستند في الأرشيف قد يكون المقصود (رقمه مطابق لكنه غير مؤكد، أو يختلف بحرف واحد).</summary>
    public string? SuggestedId { get; init; }
    public string SuggestionReason { get; init; } = "";
}
public sealed record Edge(string From, string To, string Kind);
public sealed record Ghost(string Key, string Number, string DateText, string Kind, string Owner, string Issuer, List<string> CitedBy);
public sealed record AwaitingItem(string DocumentId, int DaysWaiting, string Since);

public sealed class ThreadInfo
{
    public string Id { get; init; } = "";
    public List<string> DocumentIds { get; init; } = new();
    public List<Ghost> Ghosts { get; init; } = new();
    public string Subject { get; set; } = "";
    public string Started { get; set; } = "";
    public string LastActivity { get; set; } = "";
    public string State { get; set; } = "مفتوحة";
    public List<string> Parties { get; init; } = new();
    public int AwaitingCount { get; set; }
}

/// <summary>لقطة غير قابلة للتغيير من الأرشيف ومعارفه المستنتجة.</summary>
public sealed class Snapshot
{
    public IReadOnlyList<Doc> Docs { get; init; } = Array.Empty<Doc>();
    public IReadOnlyDictionary<string, Doc> ById { get; init; } = new Dictionary<string, Doc>();
    public BasiraEngine Engine { get; init; } = new(new List<DocumentRecord>());
    public IReadOnlyDictionary<string, List<RefInfo>> References { get; init; } = new Dictionary<string, List<RefInfo>>();
    public IReadOnlyDictionary<string, List<DeadlineHint>> Deadlines { get; init; } = new Dictionary<string, List<DeadlineHint>>();
    public IReadOnlyDictionary<string, (string Date, string Source)> EffectiveDue { get; init; } = new Dictionary<string, (string, string)>();
    public IReadOnlyList<Edge> Edges { get; init; } = Array.Empty<Edge>();
    public IReadOnlyDictionary<string, string> ThreadOf { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, ThreadInfo> Threads { get; init; } = new Dictionary<string, ThreadInfo>();
    public IReadOnlyList<AwaitingItem> Awaiting { get; init; } = Array.Empty<AwaitingItem>();
    public IReadOnlyList<Ghost> Ghosts { get; init; } = Array.Empty<Ghost>();
    public IReadOnlyList<BasiraFinding> Findings { get; init; } = Array.Empty<BasiraFinding>();
    public HealthMetrics Metrics { get; init; } = new();
    public DateTime BuiltAt { get; init; } = DateTime.MinValue;
    public int ReplyDays { get; init; } = 7;
    /// <summary>عدد الحقول الحرجة غير المؤكدة لكل مستند.</summary>
    public IReadOnlyDictionary<string, int> PendingChecks { get; init; } = new Dictionary<string, int>();

    public static DateTime? ParseIso(string v) =>
        DateTime.TryParseExact(v, new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-ddTHH:mm:ssZ", "o" }, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    /// <summary>تاريخ المستند للترتيب الزمني: تاريخه المسجل، وإلا تاريخ إدراجه.</summary>
    public static DateTime DateOf(Doc d) => (ParseIso(d.DocumentDate) ?? ParseIso(d.CreatedAt) ?? DateTime.MinValue).Date;
}

/// <summary>يبني اللقطة في الخلفية بعد كل تغيير؛ القراءات تنتظر البناء الجاري بحد أقصى ثم تعيد أحدث لقطة.</summary>
public sealed class ArchiveIndex
{
    private readonly DocumentStore _docs;
    private readonly SettingsStore _settings;
    private readonly ReviewStore _review;
    private readonly object _lock = new();
    private Snapshot _current = new();
    private bool _dirty = true;
    private Task? _building;

    public ArchiveIndex(DocumentStore docs, SettingsStore settings, ReviewStore review)
    {
        _docs = docs;
        _settings = settings;
        _review = review;
        docs.Changed += () => { lock (_lock) _dirty = true; };
        review.Changed += () => { lock (_lock) _dirty = true; };
    }

    public Snapshot Current => _current;

    public async Task<Snapshot> GetAsync(TimeSpan? wait = null)
    {
        Task? t;
        lock (_lock)
        {
            if (_dirty && _building is null)
            {
                _dirty = false;
                _building = Task.Run(() =>
                {
                    try { _current = Build(_docs.All(), _settings.GetInt("reply_days", 7, 1, 120), DateTime.Today, _review.PendingByDocument()); }
                    finally { lock (_lock) _building = null; }
                });
            }
            t = _building;
        }
        if (t is not null) await Task.WhenAny(t, Task.Delay(wait ?? TimeSpan.FromSeconds(8)));
        bool again;
        lock (_lock) again = _dirty && _building is null && t is not null;
        // تغيّر الأرشيف أثناء البناء: ابدأ بناءً جديداً لتعكس القراءة التالية آخر حالة.
        if (again) _ = GetAsync(TimeSpan.Zero);
        return _current;
    }

    public static DocumentRecord ToRecord(Doc d, string effectiveDue) => new()
    {
        Id = d.Id, Title = d.Title, TextContent = d.Text, ArchiveNumber = d.ArchiveNo,
        ReferenceNumber = d.Direction == "صادر" ? d.OutgoingNo : (d.IncomingNo != "" ? d.IncomingNo : d.OutgoingNo),
        ExternalNumber = d.ExternalNo, OcrOutgoingNumbers = d.OutgoingNo, OcrIncomingNumbers = d.IncomingNo,
        SourceEntity = d.SourceEntity, DestinationEntity = d.DestinationEntity, GeneralCategory = d.Category, SubCategory = d.SubCategory,
        Status = d.Status, Confidentiality = d.Confidentiality, Tags = d.Tags, Summary = d.Summary, FileHashSha256 = d.FileId,
        DueDateText = effectiveDue.Replace('-', '/'), DocumentDateText = d.DocumentDate,
        CreatedAt = Snapshot.ParseIso(d.CreatedAt) ?? DateTime.MinValue, UpdatedAt = Snapshot.ParseIso(d.UpdatedAt) ?? DateTime.MinValue,
    };

    /// <summary>
    /// يبني اللقطة. الحقول المقروءة آلياً وغير المؤكدة (pending) تُعلِّق القرارات المبنية عليها:
    /// الإشارة غير المؤكدة لا تربط ولا تنشئ خطاباً غائباً، والتاريخ غير المؤكد لا تُحسب منه مهلة،
    /// والرقم غير المؤكد لا يُعتمد هوية للمستند (فلا تُربط به إشارات المستندات الأخرى إلا اقتراحاً).
    /// </summary>
    public static Snapshot Build(List<Doc> docs, int replyDays, DateTime today, IReadOnlyDictionary<string, List<FieldCheckRow>>? pending = null)
    {
        pending ??= new Dictionary<string, List<FieldCheckRow>>();
        bool PendingField(string id, string field) => pending.TryGetValue(id, out var l) && l.Any(k => k.Field == field);
        bool PendingSpan(string id, string field, int off, int len) =>
            pending.TryGetValue(id, out var l) && l.Any(k => k.Field == field && k.Offset >= 0 && k.Offset < off + len && off < k.Offset + Math.Max(1, k.Length));
        Doc Gate(Doc d)
        {
            if (!pending.TryGetValue(d.Id, out var l) || !l.Any(k => k.Field == "number")) return d;
            var g = d.Clone();
            foreach (var k in l.Where(k => k.Field == "number"))
                if (k.Target == "outgoing_no") g.OutgoingNo = ""; else if (k.Target == "external_no") g.ExternalNo = "";
            return g;
        }
        var byId = docs.ToDictionary(d => d.Id);
        // فهرس الأرقام لا يعتمد على المواعيد؛ يُبنى أولاً لاستخراج الروابط.
        var refIndex = new CorrespondenceIndex(docs.Select(d => ToRecord(Gate(d), "")).ToList());
        // أرقام الأرشيف للاقتراح: المؤكدة، وغير المؤكدة (رقم الترويسة المعلق).
        var pendingNumbers = pending.Values.SelectMany(l => l).Where(k => k.Field == "number" && k.Value != "" && byId.ContainsKey(k.DocumentId))
            .Select(k => (k.DocumentId, Key: CorrespondenceReferenceParser.NumberKey(k.Value))).ToList();
        var knownNumbers = docs.Select(Gate).SelectMany(d => new[] { d.OutgoingNo, d.IncomingNo, d.ExternalNo }.Where(n => n != "").Select(n => (DocumentId: d.Id, Key: CorrespondenceReferenceParser.NumberKey(n)))).ToList();

        // الإشارات وروابطها، والخطابات المشار إليها غير الموجودة في الأرشيف («أشباح»).
        var references = new Dictionary<string, List<RefInfo>>();
        var edges = new List<Edge>();
        var ghosts = new Dictionary<string, Ghost>();
        foreach (var d in docs)
        {
            var list = new List<RefInfo>();
            foreach (var m in refIndex.Resolve(d.Text, d.Id))
            {
                var r = m.Reference;
                var target = m.Documents.FirstOrDefault()?.Id;
                // الاقتراح: رقم مطابق لرقم معلق التحقق، أو رقم يختلف بحرف واحد عن رقم في الأرشيف (خطأ قراءة محتمل في أحدهما).
                string? suggested = null; var why = "";
                if (target is null)
                {
                    if (pendingNumbers.FirstOrDefault(p => p.DocumentId != d.Id && CorrespondenceReferenceParser.NumbersMatch(p.Key, r.Number)) is { DocumentId: not null } p1)
                    { suggested = p1.DocumentId; why = "رقمه مطابق لكنه بانتظار التحقق"; }
                    else if (knownNumbers.Concat(pendingNumbers).FirstOrDefault(p => p.DocumentId != d.Id && OneEditApart(p.Key, r.Number)) is { DocumentId: not null } p2)
                    { suggested = p2.DocumentId; why = "رقمه يختلف بحرف واحد"; }
                }
                if (PendingSpan(d.Id, "reference", r.Offset, r.Length))
                {
                    list.Add(new RefInfo(r, null, "") { Pending = true, SuggestedId = suggested ?? target, SuggestionReason = target is not null ? "يطابق بعد التحقق من القراءة" : why });
                    continue;
                }
                var ghostKey = "";
                if (target is not null) edges.Add(new Edge(d.Id, target, r.KindLabel));
                else if (r.Kind != ReferenceKind.LegalBasis && r.Kind != ReferenceKind.Registration && !(suggested is not null && why.StartsWith("رقمه مطابق", StringComparison.Ordinal)))
                {
                    ghostKey = "ghost:" + r.Number;
                    if (!ghosts.TryGetValue(ghostKey, out var g))
                        ghosts[ghostKey] = g = new Ghost(ghostKey, r.RawNumber, r.DateText, (r.DocumentType == "" ? "مستند" : r.DocumentType), r.OwnerLabel, r.Issuer, new List<string>());
                    if (!g.CitedBy.Contains(d.Id)) g.CitedBy.Add(d.Id);
                }
                list.Add(new RefInfo(r, target, ghostKey) { SuggestedId = suggested, SuggestionReason = why });
            }
            references[d.Id] = list;
        }

        // المواعيد: اليدوي أولاً، وإلا أقرب مهلة في النص؛ وتسقط مهلة النص عن الوارد الذي صدر ما يشير إليه (أُجيب عليه).
        var answered = edges.Select(e => e.To).ToHashSet();
        var deadlines = new Dictionary<string, List<DeadlineHint>>();
        var due = new Dictionary<string, (string, string)>();
        foreach (var d in docs)
        {
            // تاريخ الخطاب غير المؤكد لا يصلح أساساً للمهل النسبية («خلال 15 يوماً من تاريخه»).
            var baseDate = PendingField(d.Id, "date") || Snapshot.DateOf(d) == DateTime.MinValue ? (DateTime?)null : Snapshot.DateOf(d);
            var hints = DeadlineExtractor.Extract(d.Text, baseDate);
            deadlines[d.Id] = hints;
            var usable = hints.Where(h => h.Due.HasValue && !PendingSpan(d.Id, "deadline", h.Offset, h.Length) && !PendingSpan(d.Id, "date", h.Offset, h.Length)).ToList();
            if (d.DueDate != "") due[d.Id] = (d.DueDate, "محدد يدوياً");
            else if (!answered.Contains(d.Id) && usable.Count > 0)
            {
                var earliest = usable.Min(h => h.Due!.Value);
                due[d.Id] = (earliest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "مستخرج من النص: " + usable.First(h => h.Due == earliest).Evidence);
            }
        }
        var records = docs.Select(d => ToRecord(d, due.TryGetValue(d.Id, out var x) ? x.Item1 : "")).ToList();
        var engine = new BasiraEngine(records);

        // خيوط المعاملات: اتحاد المستندات المترابطة بالإشارات، ومن يشير إلى الخطاب الغائب نفسه.
        var parent = docs.ToDictionary(d => d.Id, d => d.Id);
        string Find(string x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(string a, string b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }
        foreach (var e in edges) Union(e.From, e.To);
        foreach (var g in ghosts.Values) for (var i = 1; i < g.CitedBy.Count; i++) Union(g.CitedBy[0], g.CitedBy[i]);

        // بانتظار رد: وارد غير منجز لم يشر إليه أي مستند لاحق، وتجاوز مهلة الرد.
        var cited = edges.Select(e => e.To).ToHashSet();
        var awaiting = new List<AwaitingItem>();
        foreach (var d in docs)
        {
            if (d.Direction != "وارد" || BasiraEngine.IsClosed(ToRecord(d, "")) || cited.Contains(d.Id)) continue;
            // تاريخ غير مؤكد: يُحسب الانتظار من تاريخ الإدراج (حد أدنى مضمون) لا من التاريخ المقروء.
            var since = PendingField(d.Id, "date") ? (Snapshot.ParseIso(d.CreatedAt)?.Date ?? DateTime.MinValue) : Snapshot.DateOf(d);
            if (since == DateTime.MinValue) continue;
            var days = (int)(today - since).TotalDays;
            if (days >= replyDays) awaiting.Add(new AwaitingItem(d.Id, days, since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
        awaiting = awaiting.OrderByDescending(a => a.DaysWaiting).ToList();
        var awaitingIds = awaiting.Select(a => a.DocumentId).ToHashSet();

        var threadOf = new Dictionary<string, string>();
        var groups = docs.GroupBy(d => Find(d.Id)).ToList();
        var threads = new Dictionary<string, ThreadInfo>();
        foreach (var grp in groups)
        {
            var members = grp.OrderBy(Snapshot.DateOf).ThenBy(d => d.CreatedAt, StringComparer.Ordinal).ToList();
            var id = "T-" + members[0].ArchiveNo;
            foreach (var m in members) threadOf[m.Id] = id;
            var memberSet = members.Select(m => m.Id).ToHashSet();
            var threadGhosts = ghosts.Values.Where(g => g.CitedBy.Any(memberSet.Contains)).ToList();
            if (members.Count + threadGhosts.Count < 2) continue;
            var info = new ThreadInfo
            {
                Id = id, DocumentIds = members.Select(m => m.Id).ToList(), Ghosts = threadGhosts,
                Subject = members[0].Title, Started = Snapshot.DateOf(members[0]).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LastActivity = members.Max(m => Snapshot.DateOf(m)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Parties = members.SelectMany(m => new[] { m.SourceEntity, m.DestinationEntity }).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(6).ToList(),
                AwaitingCount = members.Count(m => awaitingIds.Contains(m.Id)),
            };
            info.State = info.AwaitingCount > 0 ? "بانتظار رد" : members.All(m => BasiraEngine.IsClosed(ToRecord(m, ""))) ? "منجزة" : "مفتوحة";
            threads[id] = info;
        }

        var findings = engine.Findings(today);
        foreach (var a in awaiting)
            findings.Add(new BasiraFinding { DocumentId = a.DocumentId, Title = byId[a.DocumentId].Title, Kind = "بانتظار رد", Severity = a.DaysWaiting >= replyDays * 2 ? 0 : 1, Reason = $"وارد منذ {a.DaysWaiting} يوماً ولم يصدر ما يشير إليه" });
        findings = findings.OrderBy(f => f.Severity).ThenBy(f => f.Due ?? DateTime.MaxValue).ThenBy(f => f.Title).ToList();

        return new Snapshot
        {
            Docs = docs, ById = byId, Engine = engine, References = references, Deadlines = deadlines, EffectiveDue = due,
            Edges = edges, ThreadOf = threadOf, Threads = threads, Awaiting = awaiting, Ghosts = ghosts.Values.ToList(),
            Findings = findings, Metrics = ArchiveHealthReportService.Compute(records, findings), BuiltAt = DateTime.Now, ReplyDays = replyDays,
            PendingChecks = pending.Where(p => byId.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value.Count),
        };
    }

    /// <summary>رقمان (بعد التوحيد) يختلفان بإبدال حرف أو حذفه أو إضافته، بطول 4 فأكثر (الأرقام القصيرة تتشابه صدفة).</summary>
    public static bool OneEditApart(string a, string b)
    {
        if (a.Length < 4 || b.Length < 4 || a == b || Math.Abs(a.Length - b.Length) > 1) return false;
        int i = 0, j = 0, edits = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j]) { i++; j++; continue; }
            if (++edits > 1) return false;
            if (a.Length > b.Length) i++; else if (b.Length > a.Length) j++; else { i++; j++; }
        }
        return edits + (a.Length - i) + (b.Length - j) <= 1;
    }
}
