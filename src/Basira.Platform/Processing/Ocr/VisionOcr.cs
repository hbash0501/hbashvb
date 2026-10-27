using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Basira.Platform.Processing.Ocr;

/// <summary>
/// إعدادات محرك القراءة المرئي: المزوّد («local» خادم محلي بواجهة OpenAI مثل Ollama وllama.cpp؛ أو خدمة سحابية «gemini» أو «claude»
/// بمفتاح API)، والنموذج. عنوان الخدمة السحابية ثابت في البرنامج لا يُضبط، فلا تُرسل الصور إلا إلى عنوان المزوّد الرسمي.
/// </summary>
public sealed record VisionOptions(string Url, string Model, string Prompt = "", int MaxSide = 1280, int TimeoutSeconds = 600, string Provider = "local", string ApiKey = "")
{
    public bool IsCloud => Provider != "local";

    public string ProviderLabel => Provider switch { "gemini" => "Google Gemini", "claude" => "Anthropic Claude", _ => "محلي" };

    public const string DefaultPrompt =
        "Transcribe all text in this scanned Arabic document image exactly as written, line by line, in natural reading order (right to left). " +
        "Keep every number, date, letter reference and punctuation exactly as printed, including Arabic-Indic digits. " +
        "Do not translate, summarize, correct, explain or add anything. Output plain text only, without Markdown.";

    /// <summary>نماذج OCR المتخصصة تتوقع موجّهاً قصيراً بعينه (PaddleOCR-VL: «OCR:»).</summary>
    public string EffectivePrompt => Prompt.Trim() != "" ? Prompt.Trim() : Model.Contains("paddleocr", StringComparison.OrdinalIgnoreCase) ? "OCR:" : DefaultPrompt;
}

/// <summary>تجاوز قراءة صفحة المهلة المحددة.</summary>
public sealed class VisionTimeoutException : InvalidOperationException
{
    public VisionTimeoutException(string message) : base(message) { }
}

/// <summary>
/// القراءة بنموذج رؤية لغوي يعمل على خادم محلي. الخادم يجب أن يكون على هذا الجهاز أو شبكة المؤسسة الداخلية
/// (لا يُرسل أي مستند إلى عنوان على الإنترنت). النص الناتج يُدقق قبل اعتماده (التكرار الحلقي، والمخالفة الكبيرة لـ Tesseract).
/// </summary>
public static class VisionOcr
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan };
    // الخدمات السحابية عبر وكيل النظام إن وُجد (شبكات المؤسسات).
    private static readonly HttpClient CloudHttp = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan };

    public static readonly string[] Providers = { "local", "gemini", "claude" };

    /// <summary>عناوين المزوّدين الرسمية (تُستبدل في الاختبارات وحدها).</summary>
    public static string GeminiUrl { get; internal set; } = "https://generativelanguage.googleapis.com/v1beta/openai";
    public static string ClaudeUrl { get; internal set; } = "https://api.anthropic.com";

    /// <summary>النموذج الموصى به لكل مزوّد سحابي (يُعدَّل من الإعدادات، و«فحص الخادم» يعرض المتاح).</summary>
    public static string DefaultModel(string provider) => provider switch { "gemini" => "gemini-3.8-flash", "claude" => "claude-opus-5-5", _ => "" };

    public static string UrlFor(string provider, string localUrl) => provider switch { "gemini" => GeminiUrl, "claude" => ClaudeUrl, _ => localUrl };

    /// <summary>عنوان الخادم محلي: هذا الجهاز، أو عنوان شبكة خاصة (10/8، 172.16/12، 192.168/16، fc00::/7)، أو اسم جهاز داخلي بلا نطاق.</summary>
    public static bool IsLocalUrl(string url, out string reason)
    {
        reason = "";
        if (!Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https"))
        { reason = "العنوان يجب أن يكون رابط http أو https، مثل http://127.0.0.1:11434/v1"; return false; }
        var host = u.IdnHost.Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) || !host.Contains('.') && !IPAddress.TryParse(host, out _)) return true;
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168) return true;
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6 && (ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal)) return true;
        }
        reason = "خادم القراءة يجب أن يكون على هذا الجهاز أو شبكة المؤسسة الداخلية؛ لا تُرسل المستندات إلى عنوان على الإنترنت.";
        return false;
    }

    private static string Endpoint(string baseUrl, string path) => baseUrl.TrimEnd('/') + "/" + path;

    /// <summary>فحص الخادم أو الخدمة: متاحة (والمفتاح صالح)، والنماذج المتوفرة.</summary>
    public static async Task<(bool Ok, string Message, List<string> Models)> ProbeAsync(string url, CancellationToken ct, string provider = "local", string apiKey = "")
    {
        if (provider == "claude") return await ClaudeOcr.ModelsAsync(apiKey, ClaudeUrl, ct);
        var cloud = provider != "local";
        if (!cloud && !IsLocalUrl(url, out var why)) return (false, why, new());
        if (cloud && apiKey == "") return (false, "احفظ مفتاح API أولاً.", new());
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(cloud ? 15 : 5));
            using var req = new HttpRequestMessage(HttpMethod.Get, Endpoint(url, "models"));
            if (cloud) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            using var r = await (cloud ? CloudHttp : Http).SendAsync(req, cts.Token);
            if (!r.IsSuccessStatusCode) return (false, (int)r.StatusCode is 401 or 403 ? "المفتاح مرفوض (رمز " + (int)r.StatusCode + ")" : $"الخادم ردّ برمز {(int)r.StatusCode}", new());
            using var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync(cts.Token));
            var models = j.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().Select(m => m.TryGetProperty("id", out var id) ? (id.GetString() ?? "").Replace("models/", "") : "").Where(x => x != "").ToList()
                : new List<string>();
            return (true, "", models);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (false, "تعذر الاتصال بخادم القراءة: " + (ex is TaskCanceledException ? "انتهت المهلة" : ex.Message), new());
        }
    }

    /// <summary>صورة الصفحة مصغرة إلى أطول ضلع MaxSide (لا تكبير)، بصيغة PNG، مع أبعادها.</summary>
    public static (byte[] Png, int Width, int Height) Encode(string imagePath, int maxSide)
    {
        using var src = SKBitmap.Decode(imagePath) ?? throw new InvalidDataException("تعذر قراءة صورة الصفحة.");
        var scale = Math.Min(1.0, (double)maxSide / Math.Max(src.Width, src.Height));
        using var resized = scale < 1 ? src.Resize(new SKImageInfo((int)(src.Width * scale), (int)(src.Height * scale)), SKFilterQuality.High) : null;
        var bmp = resized ?? src;
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return (data.ToArray(), bmp.Width, bmp.Height);
    }

    /// <summary>
    /// سياق يتسع للصورة والموجّه والنص المقروء. نماذج Qwen-VL تمثل كل مربع 28×28 بكسل تقريباً برمز واحد؛ صفحة A4 بعرض 1280 نحو 2300 رمز،
    /// وبعرض 1800 نحو 4500 رمز تتجاوز سياق Ollama الافتراضي (4096) فيُقتطع الإدخال أو يتعثر.
    /// </summary>
    public static int ContextFor(int width, int height) => (int)Math.Min(32768, Math.Ceiling(((width / 28.0) * (height / 28.0) + 3500) / 1024) * 1024);

    // خوادم Ollama تُكتشف مرة لكل عنوان: واجهتها الأصلية تقبل حجم السياق وإبقاء النموذج محمّلاً بين الصفحات.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> OllamaServers = new();

    private static string Root(string url) => Regex.Replace(url.TrimEnd('/'), @"/v1$", "");

    public static async Task<bool> IsOllamaAsync(string url, CancellationToken ct)
    {
        if (OllamaServers.TryGetValue(url, out var known)) return known;
        var ollama = false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var r = await Http.GetAsync(Root(url) + "/api/version", cts.Token);
            if (r.IsSuccessStatusCode)
            {
                using var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync(cts.Token));
                ollama = j.RootElement.TryGetProperty("version", out _);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { }
        OllamaServers[url] = ollama;
        return ollama;
    }

    // ===== صحة الخادم: تجاوز المهلة مرتين متتاليتين يوقف المحرك المرئي مؤقتاً حتى لا تنتظر كل وثيقة المهلة كاملة =====
    public const int SuspendMinutes = 20;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Timeouts, DateTime Until)> Health = new();

    public static void Record(string url, bool timedOut)
    {
        if (!timedOut) { Health.TryRemove(url, out _); return; }
        Health.AddOrUpdate(url, _ => (1, DateTime.MinValue), (_, h) => h.Timeouts + 1 >= 2 ? (0, DateTime.UtcNow.AddMinutes(SuspendMinutes)) : (h.Timeouts + 1, h.Until));
    }

    /// <summary>المحرك المرئي موقوف مؤقتاً لتكرار تجاوز المهلة، وحتى متى.</summary>
    public static bool Suspended(string url, out DateTime until)
    {
        until = Health.TryGetValue(url, out var h) ? h.Until : DateTime.MinValue;
        return until > DateTime.UtcNow;
    }

    public static void Resume(string url) => Health.TryRemove(url, out _);

    public static async Task<string> ReadAsync(string imagePath, VisionOptions o, CancellationToken ct)
    {
        if (!o.IsCloud && !IsLocalUrl(o.Url, out var why)) throw new InvalidOperationException(why);
        if (o.IsCloud && o.ApiKey == "") throw new InvalidOperationException("لم يُحفظ مفتاح API للخدمة السحابية.");
        var (png, width, height) = Encode(imagePath, o.MaxSide);
        if (o.Provider == "claude") return await CloudAsync(o, () => ClaudeOcr.ReadAsync(png, o.Model, o.ApiKey, o.EffectivePrompt, o.TimeoutSeconds, ClaudeUrl, ct), ct);
        var ollama = !o.IsCloud && await IsOllamaAsync(o.Url, ct);
        object body = ollama
            ? new
            {
                model = o.Model,
                stream = false,
                keep_alive = "30m",
                options = new { temperature = 0, num_ctx = ContextFor(width, height), num_predict = 3072 },
                messages = new object[] { new { role = "user", content = o.EffectivePrompt, images = new[] { Convert.ToBase64String(png) } } },
            }
            : o.Provider == "gemini"
                // Gemini يفكر قبل الرد، وتفكيره يُحسب من حد الرموز: جهد منخفض (النقل الحرفي لا يحتاجه) وحدّ يتسع للتفكير والصفحة معاً.
                // ودرجة الحرارة تبقى على افتراضيها: خفضها في نماذج Gemini 3 قد يسبب التكرار الحلقي.
                ? new { model = o.Model, max_tokens = 12000, reasoning_effort = "low", messages = OpenAiMessages(png, o.EffectivePrompt) }
                : new { model = o.Model, temperature = 0, max_tokens = 4096, messages = OpenAiMessages(png, o.EffectivePrompt) };
        if (o.IsCloud) return await CloudAsync(o, () => PostAsync(o, body, false, ct), ct);
        // طلب واحد في كل مرة: خوادم النماذج المحلية تعالج طلباً واحداً افتراضياً، والمهلة تُحسب من بدء المعالجة لا من الانتظار في الطابور.
        await Gate.WaitAsync(ct);
        try { return await PostAsync(o, body, ollama, ct); }
        finally { Gate.Release(); }
    }

    private static object[] OpenAiMessages(byte[] png, string prompt) => new object[]
    {
        new
        {
            role = "user",
            content = new object[]
            {
                new { type = "image_url", image_url = new { url = "data:image/png;base64," + Convert.ToBase64String(png) } },
                new { type = "text", text = prompt },
            },
        },
    };

    /// <summary>الخدمات السحابية: حتى ثلاثة طلبات معاً، ومهلتها وإيقافها المؤقت كالخادم المحلي.</summary>
    private static async Task<string> CloudAsync(VisionOptions o, Func<Task<string>> call, CancellationToken ct)
    {
        await CloudGate.WaitAsync(ct);
        try
        {
            var text = Tidy(await call().WaitAsync(TimeSpan.FromSeconds(o.TimeoutSeconds + 5), ct));
            Record(o.Url, timedOut: false);
            return text;
        }
        catch (Exception ex) when (ex is TimeoutException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            Record(o.Url, timedOut: true);
            throw new VisionTimeoutException($"تجاوزت قراءة الصفحة {o.TimeoutSeconds} ثانية لدى {o.ProviderLabel}.");
        }
        finally { CloudGate.Release(); }
    }

    private static readonly SemaphoreSlim CloudGate = new(3, 3);

    private static async Task<string> PostAsync(VisionOptions o, object body, bool ollama, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
        try
        {
            // جسم بطول معلن (لا ترميز مقطّع): بعض الخوادم المحلية لا تقبل غيره.
            using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, ollama ? Root(o.Url) + "/api/chat" : Endpoint(o.Url, "chat/completions")) { Content = content };
            if (o.ApiKey != "") request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", o.ApiKey);
            using var r = await (o.IsCloud ? CloudHttp : Http).SendAsync(request, cts.Token);
            var json = await r.Content.ReadAsStringAsync(cts.Token);
            if (o.IsCloud && (int)r.StatusCode is 401 or 403) throw new InvalidOperationException($"مفتاح {o.ProviderLabel} مرفوض (رمز {(int)r.StatusCode}).");
            if (o.IsCloud && (int)r.StatusCode == 429) throw new InvalidOperationException("تجاوزت الطلبات حد الخدمة السحابية؛ أعد المحاولة لاحقاً.");
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"خادم القراءة ردّ برمز {(int)r.StatusCode}: {Short(json)}");
            if (o.IsCloud) return ollama ? OllamaContent(json) : Content(json);
            var text = Tidy(ollama ? OllamaContent(json) : Content(json));
            Record(o.Url, timedOut: false);
            return text;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested && !o.IsCloud)
        {
            Record(o.Url, timedOut: true);
            throw new VisionTimeoutException($"تجاوزت قراءة الصفحة {o.TimeoutSeconds} ثانية. النموذج بطيء على هذا الجهاز: جرّب نموذجاً أصغر أو دقة صورة أقل أو مهلة أطول، أو خادماً ببطاقة رسومية.");
        }
        catch (HttpRequestException ex) { throw new InvalidOperationException("تعذر الاتصال بخادم القراءة: " + ex.Message); }
    }

    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>تنسى المنصة ما عرفته عن الخادم (نوعه وإيقافه المؤقت) بعد تغيير الإعدادات أو فحصه.</summary>
    public static void Forget(string url) { OllamaServers.TryRemove(url, out _); Health.TryRemove(url, out _); }

    /// <summary>نص الرد من واجهة Ollama الأصلية (message.content).</summary>
    private static string OllamaContent(string json)
    {
        try
        {
            using var j = JsonDocument.Parse(json);
            return j.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException("رد غير متوقع من خادم القراءة: " + Short(json));
        }
    }

    /// <summary>نص الرد من choices[0].message.content (نصاً أو أجزاء).</summary>
    private static string Content(string json)
    {
        try
        {
            using var j = JsonDocument.Parse(json);
            var content = j.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content");
            return content.ValueKind == JsonValueKind.String ? content.GetString() ?? ""
                : string.Join("\n", content.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            throw new InvalidOperationException("رد غير متوقع من خادم القراءة: " + Short(json));
        }
    }

    private static string Short(string s) => s.Length > 200 ? s[..200] + "…" : s;

    /// <summary>
    /// تنظيف رد النموذج: أسوار Markdown وعلاماته، والتكرار الحلقي (سطر يتكرر بلا نهاية، وهو عطل معروف في نماذج الرؤية).
    /// </summary>
    public static string Tidy(string text)
    {
        text = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        text = Regex.Replace(text, @"^\s*```[a-zA-Z]*\s*$", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s{0,3}#{1,6}\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = Regex.Replace(text, @"<think>.*?</think>", "", RegexOptions.Singleline);
        var lines = text.Split('\n').Select(l => l.TrimEnd()).ToList();
        var kept = new List<string>();
        var counts = new Dictionary<string, int>();
        foreach (var line in lines)
        {
            var key = line.Trim();
            if (key.Length > 0)
            {
                counts[key] = counts.GetValueOrDefault(key) + 1;
                // سطر جوهري يتكرر أكثر من ثلاث مرات: بداية حلقة؛ يُقطع ما بعده.
                if (key.Length >= 8 && counts[key] > 3) break;
            }
            kept.Add(line);
        }
        var result = string.Join("\n", kept);
        // عبارة تتكرر داخل السطر نفسه (حلقة بلا فواصل أسطر).
        result = Regex.Replace(result, @"(.{12,80}?)(?:\s*\1){3,}", "$1", RegexOptions.Singleline);
        return Regex.Replace(result, @"\n{3,}", "\n\n").Trim();
    }

    /// <summary>
    /// اتفاق قراءتين على مستوى الكلمات (بعد التوحيد): نسبة كلمات القراءة الأقصر الموجودة في الأخرى. يكشف هلوسة النموذج
    /// (نص لا يشبه الصفحة) حين تكون قراءة Tesseract مقبولة.
    /// </summary>
    public static double Agreement(string a, string b)
    {
        static List<string> Words(string s) => Regex.Matches(AIDocumentArchiveVB.BasiraEngine.Normalize(s ?? ""), @"[\p{L}\p{Nd}]{3,}").Select(m => m.Value).ToList();
        var wa = Words(a); var wb = Words(b);
        if (wa.Count == 0 || wb.Count == 0) return 0;
        var (small, big) = wa.Count <= wb.Count ? (wa, new HashSet<string>(wb)) : (wb, new HashSet<string>(wa));
        return (double)small.Count(big.Contains) / small.Count;
    }
}
