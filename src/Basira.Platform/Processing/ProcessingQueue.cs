using System.Collections.Concurrent;
using System.Threading.Channels;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Intelligence;
using Basira.Platform.Processing.Ocr;

namespace Basira.Platform.Processing;

/// <summary>طابور معالجة خلفي: استخراج النص وحقول الترويسة لكل مستند، مع تقدم حي ومهام محفوظة تُستأنف بعد إعادة التشغيل.</summary>
public sealed class ProcessingQueue : BackgroundService
{
    public const string SystemActor = "المعالجة الآلية";
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();
    private readonly ConcurrentDictionary<string, JobInfo> _live = new();
    private readonly Db _db;
    private readonly DocumentStore _docs;
    private readonly FileStore _files;
    private readonly SettingsStore _settings;
    private readonly EventHub _events;
    private readonly ReviewStore _review;
    private readonly ClassificationService _classifier;
    private readonly TextExtractor _extractor = new();
    private readonly PlatformOptions _options;
    private readonly ILogger<ProcessingQueue> _log;

    public ProcessingQueue(Db db, DocumentStore docs, FileStore files, SettingsStore settings, EventHub events, ReviewStore review, ClassificationService classifier, PlatformOptions options, ILogger<ProcessingQueue> log)
    {
        _db = db; _docs = docs; _files = files; _settings = settings; _events = events; _review = review; _classifier = classifier; _options = options; _log = log;
    }

    public void Enqueue(Doc d)
    {
        var job = new JobInfo(Guid.NewGuid().ToString("N"), d.Id, d.Title, "queued", "بانتظار الدور", 0, "", Clock.Now(), null);
        using (var c = _db.Open())
            c.Exec("INSERT INTO jobs(id, document_id, title, state, stage, progress, message, created_at) VALUES(@Id,@DocumentId,@Title,@State,@Stage,@Progress,@Message,@CreatedAt)", job);
        _live[job.Id] = job;
        _events.Publish("job", job);
        _queue.Writer.TryWrite(job.Id);
    }

    public List<JobInfo> Recent(int take)
    {
        using var c = _db.Open();
        return c.Query("SELECT * FROM jobs ORDER BY created_at DESC LIMIT @take", r => new JobInfo(r.Str("id"), r.Str("document_id"), r.Str("title"), r.Str("state"), r.Str("stage"),
            (int)r.Long("progress"), r.Str("message"), r.Str("created_at"), r.Str("finished_at") == "" ? null : r.Str("finished_at")), new { take });
    }

    public int Pending => _live.Values.Count(j => j.State is "queued" or "running");

    private void Report(JobInfo job)
    {
        _live[job.Id] = job;
        using (var c = _db.Open())
            c.Exec("UPDATE jobs SET state=@State, stage=@Stage, progress=@Progress, message=@Message, finished_at=@FinishedAt WHERE id=@Id", job);
        _events.Publish("job", job);
        if (job.State is "done" or "failed") _live.TryRemove(job.Id, out _);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // استئناف المهام التي لم تكتمل قبل إيقاف الخادم.
        using (var c = _db.Open())
            foreach (var id in c.Query("SELECT id FROM jobs WHERE state IN ('queued','running') ORDER BY created_at", r => r.GetString(0)))
            {
                c.Exec("UPDATE jobs SET state='queued', stage='بانتظار الدور', progress=0 WHERE id=@id", new { id });
                _queue.Writer.TryWrite(id);
            }
        var workers = Enumerable.Range(0, _options.Workers).Select(_ => Task.Run(() => WorkAsync(stoppingToken), stoppingToken));
        await Task.WhenAll(workers);
    }

    private async Task WorkAsync(CancellationToken ct)
    {
        await foreach (var jobId in _queue.Reader.ReadAllAsync(ct))
        {
            JobInfo? job;
            using (var c = _db.Open())
                job = c.Query("SELECT * FROM jobs WHERE id=@jobId", r => new JobInfo(r.Str("id"), r.Str("document_id"), r.Str("title"), r.Str("state"), r.Str("stage"), 0, "", r.Str("created_at"), null), new { jobId }).FirstOrDefault();
            if (job is null) continue;
            try { await ProcessAsync(job, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Processing failed for {Doc}", job.DocumentId);
                try { _docs.Update(job.DocumentId, null, d => { d.Processing = "failed"; d.ProcessingError = ex.Message; }, SystemActor, "تعذرت المعالجة", "تعذرت معالجة مستند"); }
                catch (KeyNotFoundException) { }
                Report(job with { State = "failed", Stage = "تعذرت المعالجة", Progress = 100, Message = ex.Message, FinishedAt = Clock.Now() });
            }
        }
    }

    private async Task ProcessAsync(JobInfo job, CancellationToken ct)
    {
        var current = job with { State = "running", Stage = "قراءة الملف", Progress = 5 };
        Report(current);
        var doc = _docs.Get(job.DocumentId) ?? throw new KeyNotFoundException("المستند غير موجود.");
        if (doc.FileId == "" || !_files.Exists(doc.FileId)) throw new FileNotFoundException("ملف المستند الأصلي غير موجود في المخزن.");
        var s = _settings.All();
        var tools = await OcrEngines.ToolsAsync(_settings, ct);
        var lastReport = DateTime.MinValue;
        var extraction = await _extractor.ExtractAsync(_files.PathOf(doc.FileId), doc.FileType, tools, (p, stage) =>
        {
            current = current with { Progress = Math.Clamp(p, 5, 90), Stage = stage };
            if ((DateTime.UtcNow - lastReport).TotalMilliseconds > 300) { lastReport = DateTime.UtcNow; Report(current); }
        }, ct);

        Report(current with { Stage = "قراءة الترويسة والإشارات", Progress = 92 });
        var header = LetterFields.Parse(extraction.Text);
        var stem = Path.GetFileNameWithoutExtension(doc.FileName);
        var rows = new List<FieldCheckRow>();
        foreach (var x in extraction.Checks)
        {
            var cropId = "";
            if (x.Crop is not null) { using var ms = new MemoryStream(x.Crop); cropId = (await _files.SaveAsync(ms, ct)).Sha; }
            var k = x.Check;
            var target = k.Field switch { "number" => doc.Direction == "صادر" ? "outgoing_no" : "external_no", "date" => "document_date", "subject" => "title", _ => "" };
            rows.Add(new FieldCheckRow(0, doc.Id, k.Field, target, k.Value, k.Candidates, k.Confident ? CheckStates.Auto : CheckStates.Pending, k.Reason, k.Page,
                k.Box.IsEmpty ? "" : $"{k.Box.Left},{k.Box.Top},{k.Box.Right},{k.Box.Bottom}", k.Offset, k.Length, x.SpanText, cropId, "", "", "", ""));
        }
        var pending = rows.Count(r => r.Pending);
        // التصنيف بالنص وعنوانه المستخرج، ثم العنوان: من المحتوى، وإلا اسم ملف سمّاه المستخدم، وإلا وصف من البيانات
        // (لا يبقى اسم ملف السكانر عنواناً). الحقول التي حددها المستخدم لا تُمس.
        var titleAuto = doc.Title == "" || doc.Title == stem || doc.IsAuto("title");
        var contentTitle = TitleBuilder.Build(extraction.Text, header.Subject);
        Report(current with { Stage = "التصنيف", Progress = 95 });
        var classification = extraction.Text.Length > 0 && (doc.Category == "" || doc.IsAuto("category")) ? _classifier.Classify(extraction.Text, titleAuto ? contentTitle : doc.Title, doc.Id) : ClassificationResult.None;
        var category = classification.Category != "" ? classification.Category : doc.Category;
        var title = !titleAuto ? doc.Title : TitleBuilder.ForDocument(extraction.Text, header.Subject, doc.FileName, doc.Direction,
            doc.Direction == "صادر" ? doc.DestinationEntity : doc.SourceEntity != "" ? doc.SourceEntity : header.Sender,
            category, classification.Category != "" ? classification.SubCategory : doc.SubCategory, Clock.Hijri(doc.DocumentDate != "" ? doc.DocumentDate : header.DateIso));
        if (title == "") title = doc.Title;
        _docs.Update(doc.Id, null, d =>
        {
            if (titleAuto && title != "") { d.Title = title; d.SetAuto("title", true); }
            _classifier.Apply(d, classification);
            d.Text = extraction.Text;
            d.PageCount = extraction.Pages;
            d.OcrQuality = extraction.Quality;
            d.Processing = extraction.Text.Length > 0 ? "done" : "needs_text";
            d.ProcessingError = extraction.Warning;
            d.ScanQuality = string.Join("؛ ", extraction.ScanIssues);
            if (d.DocumentDate == "" && header.DateIso != "") d.DocumentDate = header.DateIso;
            if (header.Number != "")
            {
                // رقم الترويسة رقمنا الصادر في خطاباتنا، ورقم الجهة المرسلة في الوارد.
                if (d.Direction == "صادر") { if (d.OutgoingNo == "") d.OutgoingNo = header.Number; }
                else if (d.ExternalNo == "") d.ExternalNo = header.Number;
            }
            if (d.Summary == "") d.Summary = header.Summary;
            // الوارد: المرسل جهة خارجية والمخاطَب نحن. الصادر: العكس.
            if (d.Direction == "صادر")
            {
                if (d.DestinationEntity == "") d.DestinationEntity = header.Addressee;
                if (d.SourceEntity == "") d.SourceEntity = header.Sender;
            }
            else if (d.SourceEntity == "") d.SourceEntity = header.Sender;
        }, SystemActor, $"استخراج النص ({extraction.Engine}، {extraction.Text.Length} حرفاً)", "معالجة مستند",
            (c, tx, _) => ReviewStore.Replace(c, tx, doc.Id, rows));
        _review.NotifyChanged();
        var message = string.Join(" ", new[] { pending > 0 ? $"{pending} من الحقول الحرجة بانتظار التحقق." : "", extraction.Warning }.Where(m => m != ""));
        Report(current with { State = "done", Stage = extraction.Text.Length > 0 ? "اكتملت المعالجة" : "اكتمل الحفظ دون نص", Progress = 100, Message = message, FinishedAt = Clock.Now() });
    }
}
