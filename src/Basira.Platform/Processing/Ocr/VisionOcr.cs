using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Basira.Platform.Processing.Ocr;

/// <summary>إعدادات محرك القراءة المرئي: خادم محلي بواجهة OpenAI (Ollama أو llama.cpp أو LM Studio) والنموذج.</summary>
public sealed record VisionOptions(string Url, string Model, string Prompt = "", int MaxSide = 1800, int TimeoutSeconds = 300)
{
    public const string DefaultPrompt =
        "Transcribe all text in this scanned Arabic document image exactly as written, line by line, in natural reading order (right to left). " +
        "Keep every number, date, letter reference and punctuation exactly as printed, including Arabic-Indic digits. " +
        "Do not translate, summarize, correct, explain or add anything. Output plain text only, without Markdown.";

    /// <summary>نماذج OCR المتخصصة تتوقع موجّهاً قصيراً بعينه (PaddleOCR-VL: «OCR:»).</summary>
    public string EffectivePrompt => Prompt.Trim() != "" ? Prompt.Trim() : Model.Contains("paddleocr", StringComparison.OrdinalIgnoreCase) ? "OCR:" : DefaultPrompt;
}

/// <summary>
/// القراءة بنموذج رؤية لغوي يعمل على خادم محلي. الخادم يجب أن يكون على هذا الجهاز أو شبكة المؤسسة الداخلية
/// (لا يُرسل أي مستند إلى عنوان على الإنترنت). النص الناتج يُدقق قبل اعتماده (التكرار الحلقي، والمخالفة الكبيرة لـ Tesseract).
/// </summary>
public static class VisionOcr
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan };

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

    /// <summary>فحص الخادم: متاح، والنماذج المتوفرة فيه.</summary>
    public static async Task<(bool Ok, string Message, List<string> Models)> ProbeAsync(string url, CancellationToken ct)
    {
        if (!IsLocalUrl(url, out var why)) return (false, why, new());
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var r = await Http.GetAsync(Endpoint(url, "models"), cts.Token);
            if (!r.IsSuccessStatusCode) return (false, $"الخادم ردّ برمز {(int)r.StatusCode}", new());
            using var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync(cts.Token));
            var models = j.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().Select(m => m.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "").Where(x => x != "").ToList()
                : new List<string>();
            return (true, "", models);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (false, "تعذر الاتصال بخادم القراءة: " + (ex is TaskCanceledException ? "انتهت المهلة" : ex.Message), new());
        }
    }

    /// <summary>صورة الصفحة مصغرة إلى أطول ضلع MaxSide (لا تكبير)، بصيغة PNG.</summary>
    public static byte[] Encode(string imagePath, int maxSide)
    {
        using var src = SKBitmap.Decode(imagePath) ?? throw new InvalidDataException("تعذر قراءة صورة الصفحة.");
        var scale = Math.Min(1.0, (double)maxSide / Math.Max(src.Width, src.Height));
        using var resized = scale < 1 ? src.Resize(new SKImageInfo((int)(src.Width * scale), (int)(src.Height * scale)), SKFilterQuality.High) : null;
        using var img = SKImage.FromBitmap(resized ?? src);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static async Task<string> ReadAsync(string imagePath, VisionOptions o, CancellationToken ct)
    {
        if (!IsLocalUrl(o.Url, out var why)) throw new InvalidOperationException(why);
        var png = Encode(imagePath, o.MaxSide);
        var body = new
        {
            model = o.Model,
            temperature = 0,
            max_tokens = 4096,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = new { url = "data:image/png;base64," + Convert.ToBase64String(png) } },
                        new { type = "text", text = o.EffectivePrompt },
                    },
                },
            },
        };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
        try
        {
            // جسم بطول معلن (لا ترميز مقطّع): بعض الخوادم المحلية لا تقبل غيره.
            using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var r = await Http.PostAsync(Endpoint(o.Url, "chat/completions"), content, cts.Token);
            var json = await r.Content.ReadAsStringAsync(cts.Token);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"خادم القراءة ردّ برمز {(int)r.StatusCode}: {Short(json)}");
            return Tidy(Content(json));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException($"تجاوزت قراءة الصفحة {o.TimeoutSeconds} ثانية."); }
        catch (HttpRequestException ex) { throw new InvalidOperationException("تعذر الاتصال بخادم القراءة: " + ex.Message); }
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
