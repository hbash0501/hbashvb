using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Basira.Platform.Intelligence.Semantic;

/// <summary>
/// وصف النموذج (basira-model.json اختياري بجوار الملفات): البادئة التي يتوقعها النموذج (multilingual-e5 يتوقع «query: »)،
/// وأقصى عدد رموز. إن غاب الملف تُستنتج القيم من اسم المجلد وملف config.json.
/// </summary>
public sealed record SemanticManifest(string Name, string Prefix, int MaxTokens)
{
    public static SemanticManifest Read(string folder)
    {
        var path = Path.Combine(folder, "basira-model.json");
        if (File.Exists(path))
        {
            using var j = JsonDocument.Parse(File.ReadAllText(path));
            var r = j.RootElement;
            return new SemanticManifest(
                r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : Path.GetFileName(folder),
                r.TryGetProperty("prefix", out var p) ? p.GetString() ?? "" : "",
                r.TryGetProperty("maxTokens", out var m) && m.TryGetInt32(out var mt) ? Math.Clamp(mt, 16, 512) : 256);
        }
        var hint = Path.GetFileName(folder) + " " + (File.Exists(Path.Combine(folder, "config.json")) ? File.ReadAllText(Path.Combine(folder, "config.json")) : "");
        var e5 = hint.Contains("e5", StringComparison.OrdinalIgnoreCase);
        return new SemanticManifest(Path.GetFileName(folder), e5 ? "query: " : "", 256);
    }
}

/// <summary>
/// نموذج تمثيل دلالي بصيغة ONNX يعمل محلياً على المعالج: نص ← متجه مُطبَّع (متوسط تمثيلات الرموز بقناع الانتباه).
/// آمن للاستدعاء المتزامن من عدة خيوط.
/// </summary>
public sealed class SemanticModel : IDisposable
{
    private readonly InferenceSession _session;
    private readonly HfTokenizer _tokenizer;
    private readonly HashSet<string> _inputs;
    private readonly string _output;
    public SemanticManifest Manifest { get; }
    public string Folder { get; }
    public string ModelFile { get; }
    /// <summary>معرّف ثابت للنموذج (اسم الملف وحجمه)؛ تُربط به المتجهات المحفوظة والمعايرة.</summary>
    public string Id { get; }
    public int Dimension { get; private set; }

    private SemanticModel(string folder, string modelFile, InferenceSession session, HfTokenizer tokenizer, SemanticManifest manifest)
    {
        Folder = folder; ModelFile = modelFile; _session = session; _tokenizer = tokenizer; Manifest = manifest;
        _inputs = session.InputMetadata.Keys.ToHashSet();
        _output = session.OutputMetadata.Keys.FirstOrDefault(k => k is "sentence_embedding" or "last_hidden_state" or "token_embeddings") ?? session.OutputMetadata.Keys.First();
        Id = $"{manifest.Name}:{Path.GetFileName(modelFile)}:{new FileInfo(modelFile).Length}";
    }

    /// <summary>ملف النموذج في المجلد: model.onnx، أو model_quantized.onnx، أو أي ملف onnx (بما فيها المجلد الفرعي onnx).</summary>
    public static string? FindModelFile(string folder)
    {
        foreach (var name in new[] { "model.onnx", "model_quantized.onnx", Path.Combine("onnx", "model_quantized.onnx"), Path.Combine("onnx", "model.onnx") })
            if (File.Exists(Path.Combine(folder, name))) return Path.Combine(folder, name);
        // ملفات التنزيل المؤقتة (.download) لا تُعد نموذجاً.
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.onnx", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".download" + Path.DirectorySeparatorChar)).OrderBy(f => f.Length).FirstOrDefault()
            : null;
    }

    public static SemanticModel Load(string folder)
    {
        var modelFile = FindModelFile(folder) ?? throw new FileNotFoundException("لا يوجد ملف نموذج ‎.onnx في " + folder);
        var tokenizerFile = Path.Combine(folder, "tokenizer.json");
        if (!File.Exists(tokenizerFile)) throw new FileNotFoundException("لا يوجد tokenizer.json بجوار النموذج في " + folder);
        var options = new Microsoft.ML.OnnxRuntime.SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4) };
        var session = new InferenceSession(modelFile, options);
        var model = new SemanticModel(folder, modelFile, session, HfTokenizer.Load(tokenizerFile), SemanticManifest.Read(folder));
        model.Dimension = model.Embed("اختبار").Length;
        return model;
    }

    public float[] Embed(string text)
    {
        var ids = _tokenizer.Encode(Manifest.Prefix + (text ?? ""), Manifest.MaxTokens);
        var n = ids.Length;
        var shape = new[] { 1, n };
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, shape)),
        };
        if (_inputs.Contains("attention_mask")) inputs.Add(NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, n).ToArray(), shape)));
        if (_inputs.Contains("token_type_ids")) inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[n], shape)));
        using var results = _session.Run(inputs, new[] { _output });
        var tensor = results.First().AsTensor<float>();
        float[] vec;
        if (tensor.Rank == 2) vec = tensor.ToArray();
        else
        {
            // متوسط تمثيلات الرموز (كلها فعالة لأن الإدخال نص واحد بلا حشو).
            var d = tensor.Dimensions[2];
            vec = new float[d];
            for (var t = 0; t < n; t++) for (var k = 0; k < d; k++) vec[k] += tensor[0, t, k];
            for (var k = 0; k < d; k++) vec[k] /= n;
        }
        Normalize(vec);
        return vec;
    }

    public static void Normalize(float[] v)
    {
        double s = 0; foreach (var x in v) s += x * x;
        var norm = (float)Math.Sqrt(s);
        if (norm > 0) for (var i = 0; i < v.Length; i++) v[i] /= norm;
    }

    public static float Cosine(float[] a, float[] b)
    {
        float s = 0; var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++) s += a[i] * b[i];
        return s;
    }

    public void Dispose() => _session.Dispose();
}
