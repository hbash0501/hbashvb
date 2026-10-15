using AIDocumentArchiveVB;

namespace Basira.Web.Services;

/// <summary>لقطة غير قابلة للتغيير من الأرشيف مع فهرس البحث ونتائج التحليل.</summary>
public sealed class ArchiveSnapshot
{
    public required IReadOnlyList<DocumentRecord> Documents { get; init; }
    public required IReadOnlyDictionary<string, DocumentRecord> ById { get; init; }
    public required BasiraEngine Engine { get; init; }
    public required IReadOnlyList<BasiraFinding> Findings { get; init; }
    public required HealthMetrics Metrics { get; init; }
    public required DateTime LoadedAt { get; init; }
}

/// <summary>
/// يقرأ archive-db.xml (الذي يكتبه التطبيق المكتبي) ويعيد بناء الفهرس تلقائياً عند تغيّر الملف.
/// البوابة للقراءة فقط: لا تكتب في الأرشيف، فلا تعارض مع التطبيق المكتبي.
/// </summary>
public sealed class ArchiveSnapshots
{
    private readonly PortalOptions _options;
    private readonly object _lock = new();
    private ArchiveSnapshot? _current;
    private DateTime _stamp = DateTime.MinValue;
    private long _length = -1;
    private DateTime _lastCheck = DateTime.MinValue;

    public ArchiveSnapshots(PortalOptions options) => _options = options;

    private string DbPath => Path.Combine(_options.DataFolder, "archive-db.xml");

    public ArchiveSnapshot Current()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_current is not null && now - _lastCheck < TimeSpan.FromSeconds(2)) return _current;
            _lastCheck = now;
            var info = new FileInfo(DbPath);
            var stamp = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
            var length = info.Exists ? info.Length : -1;
            if (_current is null || stamp != _stamp || length != _length)
            {
                _current = Build();
                _stamp = stamp;
                _length = length;
            }
            return _current;
        }
    }

    private ArchiveSnapshot Build()
    {
        var settings = new AppSettings
        {
            BaseDataFolder = _options.DataFolder,
            ArchiveRootFolder = _options.ArchiveRootFolder,
            CurrentEmployeeName = "بوابة الويب",
        };
        var docs = File.Exists(DbPath)
            ? new ArchiveRepository(settings).GetDocuments("", "", "")
            : new List<DocumentRecord>();
        var engine = new BasiraEngine(docs);
        var findings = engine.Findings();
        return new ArchiveSnapshot
        {
            Documents = docs,
            ById = docs.Where(d => !string.IsNullOrEmpty(d.Id)).GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First()),
            Engine = engine,
            Findings = findings,
            Metrics = ArchiveHealthReportService.Compute(docs, findings),
            LoadedAt = DateTime.Now,
        };
    }
}
