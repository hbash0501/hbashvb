using System.Security.Cryptography;
using System.Text;
using Basira.Platform.Data;
using Basira.Platform.Infrastructure;

namespace Basira.Platform.Intelligence.Semantic;

/// <summary>
/// النموذج الدلالي في المنصة: يُبحث عنه في المسار المحدد في الإعدادات، ثم مجلد models/semantic بجوار الملف التنفيذي،
/// ثم المجلد نفسه في مجلد البيانات. يُحمَّل عند أول حاجة، ويعمل التصنيف بدونه بالقواعد والتعلم وحدهما.
/// </summary>
public sealed class SemanticService : IDisposable
{
    private readonly PlatformOptions _options;
    private readonly SettingsStore _settings;
    private readonly Db _db;
    private readonly ILogger<SemanticService> _log;
    private readonly object _lock = new();
    private SemanticModel? _model;
    private string? _loadedFrom;
    private DateTime _checkedAt = DateTime.MinValue;
    public string Error { get; private set; } = "";
    public event Action? Loaded;

    public SemanticService(PlatformOptions options, SettingsStore settings, Db db, ILogger<SemanticService> log)
    {
        _options = options; _settings = settings; _db = db; _log = log;
    }

    public IEnumerable<string> Candidates()
    {
        var configured = _settings.Get("semantic_model_path");
        if (configured != "") yield return configured;
        yield return Path.Combine(AppContext.BaseDirectory, "models", "semantic");
        yield return Path.Combine(_options.DataFolder, "models", "semantic");
    }

    /// <summary>أول مجلد فيه ملف onnx وtokenizer.json.</summary>
    public string? Locate() => Candidates().FirstOrDefault(f => Directory.Exists(f) && SemanticModel.FindModelFile(f) is not null && File.Exists(Path.Combine(f, "tokenizer.json")));

    /// <summary>المجلد الذي يُنزَّل إليه النموذج: المحدد في الإعدادات، وإلا بجوار الملف التنفيذي إن أمكنت الكتابة فيه، وإلا مجلد البيانات.</summary>
    public string DownloadTarget()
    {
        var configured = _settings.Get("semantic_model_path");
        if (configured != "") return configured;
        var beside = Path.Combine(AppContext.BaseDirectory, "models", "semantic");
        try
        {
            Directory.CreateDirectory(beside);
            var probe = Path.Combine(beside, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return beside;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return Path.Combine(_options.DataFolder, "models", "semantic"); }
    }

    /// <summary>النموذج إن وُجد؛ يُعاد الفحص كل دقيقة على الأكثر حتى يُلتقط نموذج وُضع بعد التشغيل.</summary>
    public SemanticModel? Model
    {
        get
        {
            lock (_lock)
            {
                if (_model is not null) return _model;
                if (DateTime.UtcNow - _checkedAt < TimeSpan.FromMinutes(1)) return null;
                _checkedAt = DateTime.UtcNow;
                var folder = Locate();
                if (folder is null) { Error = ""; return null; }
                try
                {
                    _model = SemanticModel.Load(folder);
                    _loadedFrom = folder;
                    Error = "";
                    _log.LogInformation("Semantic model loaded from {Folder} ({Id}, dim {Dim})", folder, _model.Id, _model.Dimension);
                }
                catch (Exception ex)
                {
                    Error = "تعذر تحميل النموذج من " + folder + ": " + ex.Message;
                    _log.LogWarning(ex, "Semantic model failed to load from {Folder}", folder);
                    return null;
                }
            }
            Loaded?.Invoke();
            return _model;
        }
    }

    public string? LoadedFrom => _loadedFrom;

    /// <summary>النموذج المحمّل الآن دون محاولة تحميل.</summary>
    public SemanticModel? Current => _model;

    /// <summary>
    /// يفرّغ النموذج المحمّل ليُعاد البحث عنه وتحميله (بعد تنزيل أو تغيير المسار). القديم لا يُغلق الآن لأن عملاً في الخلفية
    /// قد يستخدمه؛ يُغلق عند إيقاف المنصة.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            if (_model is not null) _retired.Add(_model);
            _model = null; _loadedFrom = null; _checkedAt = DateTime.MinValue; Error = "";
        }
    }

    private readonly List<SemanticModel> _retired = new();
    public bool Disposed { get; private set; }

    // ===== متجهات المستندات المحفوظة =====

    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    public static string DocumentText(string title, string text) => (title + "\n" + (text.Length > 2000 ? text[..2000] : text)).Trim();

    public float[]? Stored(string documentId, string modelId, string textHash)
    {
        using var c = _db.Open();
        var blob = c.Scalar("SELECT vector FROM doc_embeddings WHERE document_id=@documentId AND model_id=@modelId AND text_hash=@textHash", new { documentId, modelId, textHash }) as byte[];
        return blob is null ? null : FromBytes(blob);
    }

    public void Store(string documentId, string modelId, string textHash, float[] vector)
    {
        using var c = _db.Open();
        c.Exec("INSERT INTO doc_embeddings(document_id, model_id, text_hash, vector) VALUES(@documentId,@modelId,@textHash,@blob) ON CONFLICT(document_id, model_id) DO UPDATE SET text_hash=@textHash, vector=@blob",
            new { documentId, modelId, textHash, blob = ToBytes(vector) });
    }

    public Dictionary<string, float[]> StoredAll(string modelId)
    {
        using var c = _db.Open();
        return c.Query("SELECT document_id, vector FROM doc_embeddings WHERE model_id=@modelId", r => (r.GetString(0), FromBytes((byte[])r.GetValue(1))), new { modelId })
            .ToDictionary(x => x.Item1, x => x.Item2);
    }

    private static byte[] ToBytes(float[] v) { var b = new byte[v.Length * 4]; Buffer.BlockCopy(v, 0, b, 0, b.Length); return b; }
    private static float[] FromBytes(byte[] b) { var v = new float[b.Length / 4]; Buffer.BlockCopy(b, 0, v, 0, b.Length); return v; }

    public void Dispose()
    {
        lock (_lock)
        {
            Disposed = true;
            _model?.Dispose();
            _retired.ForEach(m => m.Dispose());
        }
    }
}
