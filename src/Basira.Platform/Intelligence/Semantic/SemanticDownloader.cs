using System.Text.Json;
using Basira.Platform.Data;

namespace Basira.Platform.Intelligence.Semantic;

/// <summary>
/// تنزيل النموذج الدلالي الموصى به (multilingual-e5-small بصيغة ONNX مضغوطة، نحو 120 ميجابايت، رخصة MIT) إلى مجلد
/// models/semantic بجوار الملف التنفيذي. يُنزَّل النموذج مرة واحدة؛ ولا يُرسَل أي مستند إلى أي جهة.
/// يمكن تغيير المصدر (مرآة داخلية للمؤسسة) بالإعداد semantic_model_url.
/// </summary>
public sealed class SemanticDownloader
{
    public const string DefaultSource = "https://huggingface.co/Xenova/multilingual-e5-small/resolve/main/";
    public const string ModelName = "multilingual-e5-small";

    /// <summary>الملفات: مسارها في المصدر، واسمها في المجلد، وهل هي لازمة.</summary>
    public static readonly (string Remote, string Local, bool Required)[] Files =
    {
        ("tokenizer.json", "tokenizer.json", true),
        ("config.json", "config.json", false),
        ("onnx/model_quantized.onnx", "model_quantized.onnx", true),
    };

    private readonly SemanticService _semantic;
    private readonly SettingsStore _settings;
    private readonly ILogger<SemanticDownloader> _log;
    private readonly object _lock = new();

    public bool Running { get; private set; }
    public string File { get; private set; } = "";
    public long Received { get; private set; }
    public long Total { get; private set; }
    public string Error { get; private set; } = "";
    public string Completed { get; private set; } = "";

    public SemanticDownloader(SemanticService semantic, SettingsStore settings, ILogger<SemanticDownloader> log)
    {
        _semantic = semantic; _settings = settings; _log = log;
    }

    public string Source
    {
        get
        {
            var s = _settings.Get("semantic_model_url").Trim();
            return s == "" ? DefaultSource : s.EndsWith('/') ? s : s + "/";
        }
    }

    /// <summary>يبدأ التنزيل في الخلفية؛ false إن كان تنزيل جارياً.</summary>
    public bool Start()
    {
        lock (_lock)
        {
            if (Running) return false;
            Running = true; Error = ""; Completed = ""; Received = 0; Total = 0;
        }
        _ = Task.Run(async () =>
        {
            try { await DownloadAsync(_semantic.DownloadTarget(), null, CancellationToken.None); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Error = ex is HttpRequestException or TaskCanceledException ? "تعذر الاتصال بمصدر النموذج: " + ex.Message : ex.Message;
                _log.LogWarning(ex, "Semantic model download failed");
            }
            finally { Running = false; }
        });
        return true;
    }

    /// <summary>ينزّل الملفات إلى مجلد مؤقت ثم ينقلها إلى المجلد الهدف بعد اكتمالها كلها.</summary>
    public async Task DownloadAsync(string target, Action<string>? report, CancellationToken ct)
    {
        Directory.CreateDirectory(target);
        var staging = Path.Combine(target, ".download");
        Directory.CreateDirectory(staging);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Basira/3");
        var fetched = new List<string>();
        foreach (var (remote, local, required) in Files)
        {
            File = local; Received = 0; Total = 0;
            report?.Invoke($"تنزيل {local} …");
            using var response = await http.GetAsync(Source + remote, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (!required) continue;
                throw new InvalidOperationException($"تعذر تنزيل {remote} من {Source} (رمز {(int)response.StatusCode}).");
            }
            Total = response.Content.Headers.ContentLength ?? 0;
            var part = Path.Combine(staging, local);
            await using (var src = await response.Content.ReadAsStreamAsync(ct))
            await using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[1 << 16];
                int n;
                var lastReport = 0L;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    Received += n;
                    if (report is not null && Received - lastReport > 10 << 20) { lastReport = Received; report($"  {Received >> 20} / {Total >> 20} ميجابايت"); }
                }
            }
            if (Total > 0 && new FileInfo(part).Length != Total) throw new IOException($"اكتمل تنزيل {local} ناقصاً.");
            fetched.Add(local);
        }
        // يُتحقق من الملفات بتحميلها قبل نقلها، حتى لا يحل نموذج تالف محل نموذج سليم.
        System.IO.File.WriteAllText(Path.Combine(staging, "basira-model.json"),
            JsonSerializer.Serialize(new { name = ModelName, prefix = "query: ", maxTokens = 256, source = Source }));
        report?.Invoke("التحقق من النموذج …");
        using (SemanticModel.Load(staging)) { }
        _semantic.Reset();
        foreach (var f in fetched.Append("basira-model.json"))
            System.IO.File.Move(Path.Combine(staging, f), Path.Combine(target, f), overwrite: true);
        Directory.Delete(staging, true);
        Completed = target;
        report?.Invoke("تم: " + target);
    }
}
