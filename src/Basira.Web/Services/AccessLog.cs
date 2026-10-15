using System.Text;

namespace Basira.Web.Services;

public sealed record AccessEntry(long Sequence, string TimestampUtc, string Actor, string Action, string Detail);

/// <summary>سجل وصول مرتبط بسلسلة تجزئة (AuditChain)؛ أي تعديل لاحق على أسطره يُكتشف عند التحقق.</summary>
public sealed class AccessLog
{
    private readonly string _path;
    private readonly object _lock = new();
    private long _sequence;
    private string _lastHash = AIDocumentArchiveVB.AuditChain.GenesisHash;
    private readonly TimeProvider _time;

    public AccessLog(PortalOptions options, TimeProvider time)
    {
        _time = time;
        Directory.CreateDirectory(options.DataFolder);
        _path = Path.Combine(options.DataFolder, "web-access.chain");
        if (File.Exists(_path))
        {
            var lines = File.ReadAllLines(_path, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (lines.Count > 0)
            {
                var last = lines[^1].Split('\t');
                if (last.Length == 7 && long.TryParse(last[0], out var seq)) { _sequence = seq; _lastHash = last[6]; }
            }
        }
    }

    public void Append(string actor, string action, string detail)
    {
        lock (_lock)
        {
            var seq = _sequence + 1;
            var line = AIDocumentArchiveVB.AuditChain.BuildLine(seq, _lastHash, _time.GetUtcNow().UtcDateTime,
                Clean(actor, 120), Clean(action, 80), Clean(detail, 400));
            File.AppendAllText(_path, line + "\n", new UTF8Encoding(false));
            _sequence = seq;
            _lastHash = line.Split('\t')[6];
        }
    }

    private static string Clean(string value, int max) => value.Length <= max ? value : value[..max];

    public (AIDocumentArchiveVB.AuditChainResult Result, List<AccessEntry> Recent) Verify(int take)
    {
        lock (_lock)
        {
            var lines = File.Exists(_path) ? File.ReadAllLines(_path, Encoding.UTF8) : Array.Empty<string>();
            var result = AIDocumentArchiveVB.AuditChain.Verify(lines);
            var recent = new List<AccessEntry>();
            foreach (var l in lines.Where(x => !string.IsNullOrWhiteSpace(x)).Reverse().Take(take))
            {
                var p = l.Split('\t');
                if (p.Length != 7 || !long.TryParse(p[0], out var seq)) continue;
                recent.Add(new AccessEntry(seq, p[1], Unescape(p[2]), Unescape(p[3]), Unescape(p[4])));
            }
            return (result, recent);
        }
    }

    private static string Unescape(string v) => v.Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\\\", "\\");
}
