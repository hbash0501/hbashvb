using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Basira.Platform.Intelligence.Semantic;

/// <summary>
/// قارئ ملف tokenizer.json (صيغة Hugging Face) لنماذج التمثيل الدلالي: نموذج Unigram (SentencePiece، كنماذج XLM-R وmultilingual-e5)
/// أو WordPiece (نماذج BERT)، مع الرموز الخاصة في أول التسلسل وآخره. يكفي للتمثيل؛ لا يعيد بناء النص.
/// </summary>
public sealed class HfTokenizer
{
    private enum Kind { Unigram, WordPiece }

    private readonly Kind _kind;
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _scores = new(StringComparer.Ordinal);
    private readonly int _maxPiece;
    private readonly int _unk;
    private readonly double _unkScore;
    private readonly bool _lowercase;
    private readonly string _subwordPrefix = "##";
    private readonly int[] _prefix = Array.Empty<int>();
    private readonly int[] _suffix = Array.Empty<int>();

    public int VocabSize => _ids.Count;
    public string ModelType => _kind.ToString();

    public HfTokenizer(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var model = root.GetProperty("model");
        var type = model.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type == "Unigram" || type is null && model.TryGetProperty("vocab", out var v0) && v0.ValueKind == JsonValueKind.Array)
        {
            _kind = Kind.Unigram;
            var i = 0;
            foreach (var entry in model.GetProperty("vocab").EnumerateArray())
            {
                var piece = entry[0].GetString()!;
                _ids.TryAdd(piece, i);
                _scores.TryAdd(piece, entry[1].GetDouble());
                i++;
            }
            _unk = model.TryGetProperty("unk_id", out var u) && u.ValueKind == JsonValueKind.Number ? u.GetInt32() : 0;
            _unkScore = _scores.Values.DefaultIfEmpty(0).Min() - 10;
        }
        else if (type == "WordPiece")
        {
            _kind = Kind.WordPiece;
            foreach (var p in model.GetProperty("vocab").EnumerateObject()) _ids[p.Name] = p.Value.GetInt32();
            var unkToken = model.TryGetProperty("unk_token", out var ut) ? ut.GetString()! : "[UNK]";
            _unk = _ids.TryGetValue(unkToken, out var uid) ? uid : 0;
            if (model.TryGetProperty("continuing_subword_prefix", out var cp) && cp.GetString() is { } cps) _subwordPrefix = cps;
            _lowercase = root.TryGetProperty("normalizer", out var n) && n.ValueKind == JsonValueKind.Object && n.TryGetProperty("lowercase", out var lc) && lc.ValueKind == JsonValueKind.True;
        }
        else throw new NotSupportedException($"نوع المقطِّع غير مدعوم: {type} (المدعوم: Unigram وWordPiece).");
        _maxPiece = _ids.Keys.Select(k => k.Length).DefaultIfEmpty(1).Max();
        (_prefix, _suffix) = Specials(root);
    }

    public static HfTokenizer Load(string path) => new(File.ReadAllText(path));

    /// <summary>الرموز الخاصة من post_processor: TemplateProcessing أو RobertaProcessing أو BertProcessing.</summary>
    private (int[] Prefix, int[] Suffix) Specials(JsonElement root)
    {
        if (!root.TryGetProperty("post_processor", out var pp) || pp.ValueKind != JsonValueKind.Object) return Defaults();
        var type = pp.TryGetProperty("type", out var t) ? t.GetString() : "";
        if (type is "RobertaProcessing" or "BertProcessing")
            return (new[] { pp.GetProperty("cls")[1].GetInt32() }, new[] { pp.GetProperty("sep")[1].GetInt32() });
        if (type == "TemplateProcessing" && pp.TryGetProperty("single", out var single))
        {
            var specials = pp.TryGetProperty("special_tokens", out var st) ? st : default;
            var before = new List<int>(); var after = new List<int>(); var seen = false;
            foreach (var item in single.EnumerateArray())
            {
                if (item.TryGetProperty("Sequence", out _)) { seen = true; continue; }
                if (!item.TryGetProperty("SpecialToken", out var sp)) continue;
                var name = sp.GetProperty("id").GetString()!;
                var ids = specials.ValueKind == JsonValueKind.Object && specials.TryGetProperty(name, out var def)
                    ? def.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).ToList()
                    : _ids.TryGetValue(name, out var id) ? new List<int> { id } : new List<int>();
                (seen ? after : before).AddRange(ids);
            }
            return (before.ToArray(), after.ToArray());
        }
        if (type == "Sequence" && pp.TryGetProperty("processors", out var list))
            foreach (var p in list.EnumerateArray())
                if (p.TryGetProperty("type", out var pt) && pt.GetString() is "TemplateProcessing" or "RobertaProcessing" or "BertProcessing")
                {
                    using var one = JsonDocument.Parse("{\"post_processor\":" + p.GetRawText() + "}");
                    return Specials(one.RootElement);
                }
        return Defaults();
    }

    private (int[], int[]) Defaults()
    {
        int Id(params string[] names) { foreach (var n in names) if (_ids.TryGetValue(n, out var id)) return id; return -1; }
        var cls = Id("<s>", "[CLS]"); var sep = Id("</s>", "[SEP]");
        return (cls >= 0 ? new[] { cls } : Array.Empty<int>(), sep >= 0 ? new[] { sep } : Array.Empty<int>());
    }

    /// <summary>معرّفات الرموز لنص واحد مع الرموز الخاصة، مقصوصة إلى maxTokens.</summary>
    public long[] Encode(string text, int maxTokens)
    {
        var body = new List<int>();
        var budget = Math.Max(1, maxTokens - _prefix.Length - _suffix.Length);
        foreach (var word in Words(text))
        {
            if (_kind == Kind.Unigram) Viterbi("▁" + word, body);
            else WordPieces(word, body);
            if (body.Count >= budget) break;
        }
        if (body.Count > budget) body.RemoveRange(budget, body.Count - budget);
        return _prefix.Concat(body).Concat(_suffix).Select(x => (long)x).ToArray();
    }

    private IEnumerable<string> Words(string text)
    {
        text = (text ?? "").Normalize(NormalizationForm.FormKC);
        text = Regex.Replace(text, "[​-‏‪-‮⁦-⁩﻿ـ]", "");
        if (_lowercase) text = text.ToLowerInvariant();
        if (_kind == Kind.Unigram) return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        // WordPiece: فصل علامات الترقيم كلمات مستقلة كما في BertPreTokenizer.
        return Regex.Matches(text, @"[\p{L}\p{M}\p{N}]+|[^\s\p{L}\p{M}\p{N}]").Select(m => m.Value);
    }

    /// <summary>تقطيع Unigram بأعلى مجموع احتمالات (خوارزمية فيتربي)؛ الحرف غير المعروف رمز مجهول.</summary>
    private void Viterbi(string s, List<int> output)
    {
        var n = s.Length;
        var best = new double[n + 1];
        var from = new int[n + 1];
        var piece = new int[n + 1];
        Array.Fill(best, double.NegativeInfinity);
        best[0] = 0;
        for (var i = 0; i < n; i++)
        {
            if (double.IsNegativeInfinity(best[i])) continue;
            for (var len = 1; len <= _maxPiece && i + len <= n; len++)
            {
                var sub = s.Substring(i, len);
                if (!_scores.TryGetValue(sub, out var score)) continue;
                if (best[i] + score > best[i + len]) { best[i + len] = best[i] + score; from[i + len] = i; piece[i + len] = _ids[sub]; }
            }
            // حرف لا تغطيه أي قطعة: رمز مجهول بعقوبة كبيرة حتى لا ينقطع المسار.
            if (best[i] + _unkScore > best[i + 1] && !_scores.ContainsKey(s.Substring(i, 1)))
            { best[i + 1] = best[i] + _unkScore; from[i + 1] = i; piece[i + 1] = _unk; }
        }
        var stack = new Stack<int>();
        for (var pos = n; pos > 0; pos = from[pos]) stack.Push(piece[pos]);
        var lastUnk = false;
        foreach (var id in stack)
        {
            if (id == _unk && lastUnk) continue; // دمج المجاهيل المتتالية كما في fuse_unk
            output.Add(id);
            lastUnk = id == _unk;
        }
    }

    /// <summary>WordPiece: أطول قطعة من البداية، ثم القطع اللاحقة بالبادئة «##».</summary>
    private void WordPieces(string word, List<int> output)
    {
        if (word.Length > 100) { output.Add(_unk); return; }
        var pieces = new List<int>();
        var start = 0;
        while (start < word.Length)
        {
            var end = word.Length; var found = -1;
            while (start < end)
            {
                var sub = (start > 0 ? _subwordPrefix : "") + word[start..end];
                if (_ids.TryGetValue(sub, out var id)) { found = id; break; }
                end--;
            }
            if (found < 0) { output.Add(_unk); return; }
            pieces.Add(found);
            start = end;
        }
        output.AddRange(pieces);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{_kind} vocab={VocabSize} special={_prefix.Length}+{_suffix.Length}");
}
