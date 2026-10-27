using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace Basira.Platform.Processing.Ocr;

/// <summary>القراءة بنموذج Claude عبر حزمة Anthropic الرسمية (خدمة سحابية؛ تُستخدم بموافقة المدير ولغير المستندات الحساسة).</summary>
public static class ClaudeOcr
{
    public static async Task<string> ReadAsync(byte[] png, string model, string apiKey, string prompt, int timeoutSeconds, string baseUrl, CancellationToken ct)
    {
        var client = new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl, Timeout = TimeSpan.FromSeconds(timeoutSeconds), MaxRetries = 2 };
        try
        {
            var response = await client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = model,
                MaxTokens = 8000,
                // النقل الحرفي لا يحتاج تفكيراً عميقاً: جهد منخفض أسرع وأرخص.
                OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
                // إن رفض مصنّف الأمان الطلب خطأً يُعاد بنموذج بديل داخل الطلب نفسه.
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
                Messages =
                [
                    new BetaUserMessageParam
                    {
                        Content = new List<BetaContentBlockParam>
                        {
                            new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(png), MediaType = MediaType.ImagePng } },
                            new BetaTextBlockParam { Text = prompt },
                        },
                    },
                ],
            }, ct);
            if (response.StopReason == "refusal") throw new InvalidOperationException("رفض النموذج قراءة الصفحة.");
            return string.Join("\n", response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        }
        catch (AnthropicRateLimitException) { throw new InvalidOperationException("تجاوزت الطلبات حد الخدمة السحابية؛ أعد المحاولة لاحقاً."); }
        catch (AnthropicUnauthorizedException) { throw new InvalidOperationException("مفتاح Claude مرفوض."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException(); }
        catch (AnthropicIOException ex) { throw new InvalidOperationException("تعذر الاتصال بخدمة Claude: " + ex.Message); }
        catch (AnthropicException ex) { throw new InvalidOperationException("خطأ من خدمة Claude: " + ex.Message); }
        catch (HttpRequestException ex) { throw new InvalidOperationException("تعذر الاتصال بخدمة Claude: " + ex.Message); }
    }

    /// <summary>فحص المفتاح والنماذج المتاحة له.</summary>
    public static async Task<(bool Ok, string Message, List<string> Models)> ModelsAsync(string apiKey, string baseUrl, CancellationToken ct)
    {
        if (apiKey == "") return (false, "احفظ مفتاح API أولاً.", new());
        var client = new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl, Timeout = TimeSpan.FromSeconds(15), MaxRetries = 1 };
        try
        {
            var page = await client.Models.List(cancellationToken: ct);
            return (true, "", page.Items.Select(m => m.ID).ToList());
        }
        catch (AnthropicUnauthorizedException) { return (false, "المفتاح مرفوض", new()); }
        catch (AnthropicException ex) { return (false, "خطأ من خدمة Claude: " + ex.Message, new()); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (false, "تعذر الاتصال بخدمة Claude: " + ex.Message, new()); }
    }
}
