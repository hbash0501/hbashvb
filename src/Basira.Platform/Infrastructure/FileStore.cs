using System.Security.Cryptography;

namespace Basira.Platform.Infrastructure;

/// <summary>مخزن ملفات بعنوان المحتوى (بصمة SHA-256): الملف المكرر يُحفظ مرة واحدة ولا يُستبدل أبداً.</summary>
public sealed class FileStore
{
    private readonly string _root;

    public FileStore(PlatformOptions options)
    {
        _root = options.FilesFolder;
        Directory.CreateDirectory(_root);
    }

    public string PathOf(string sha) =>
        sha.Length == 64 && sha.All(Uri.IsHexDigit) ? Path.Combine(_root, sha[..2], sha) : throw new ArgumentException("بصمة ملف غير صالحة.");

    public bool Exists(string sha) => sha.Length == 64 && File.Exists(PathOf(sha));

    /// <summary>حذف ملف من المخزن (عند الحذف النهائي لمستند لا يشاركه غيره في الملف).</summary>
    public void Delete(string sha)
    {
        if (Exists(sha)) File.Delete(PathOf(sha));
    }

    public async Task<(string Sha, long Size)> SaveAsync(Stream input, CancellationToken ct = default)
    {
        var temp = Path.Combine(_root, "tmp-" + Guid.NewGuid().ToString("N"));
        long size;
        string sha;
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            await using (var output = File.Create(temp))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                size = output.Length;
            }
            sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        var target = PathOf(sha);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target)) File.Delete(temp);
        else File.Move(temp, target);
        return (sha, size);
    }
}
