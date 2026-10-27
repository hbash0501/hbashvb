using System.Security.Cryptography;
using System.Text;

namespace Basira.Platform.Infrastructure;

/// <summary>
/// تشفير أسرار الإعدادات (مفاتيح API) بـ AES-GCM بمفتاح مشتق من سر المنصة (session.key في مجلد البيانات أو BASIRA_SESSION_SECRET).
/// يحمي المفتاح من قراءة قاعدة البيانات وحدها أو نسخها الاحتياطية؛ ولا يحمي ممن يملك مجلد البيانات كاملاً.
/// </summary>
public sealed class SecretBox
{
    private readonly byte[] _key;
    public SecretBox(PlatformOptions options) => _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, options.SessionKey, 32, info: Encoding.UTF8.GetBytes("basira-secrets-v1"));

    public string Seal(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(_key, 16)) aes.Encrypt(nonce, data, cipher, tag);
        return "v1:" + Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    /// <summary>النص الأصلي، أو فارغ إن تعذر الفك (تغيّر سر المنصة أو تلف القيمة).</summary>
    public string Open(string sealedValue)
    {
        if (string.IsNullOrEmpty(sealedValue) || !sealedValue.StartsWith("v1:", StringComparison.Ordinal)) return "";
        try
        {
            var raw = Convert.FromBase64String(sealedValue[3..]);
            var plain = new byte[raw.Length - 28];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(raw.AsSpan(0, 12), raw.AsSpan(28), raw.AsSpan(12, 16), plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException) { return ""; }
    }

    /// <summary>تلميح لعرض المفتاح المحفوظ دون كشفه: آخر أربعة أحرف.</summary>
    public static string Hint(string key) => key.Length <= 8 ? "••••" : "••••" + key[^4..];
}
