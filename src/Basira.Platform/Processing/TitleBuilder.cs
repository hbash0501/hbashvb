using System.Text.RegularExpressions;

namespace Basira.Platform.Processing;

/// <summary>
/// عنوان المستند من محتواه: سطر «الموضوع»، وإلا عنوان نوع المستند (محضر، فاتورة، تعميم…) مع ما يوضحه،
/// وإلا عبارة «بشأن…»، وإلا أول جملة ذات معنى بعد حذف عبارات الافتتاح المعتادة.
/// </summary>
public static class TitleBuilder
{
    public const int MaxWords = 12;
    public const int MaxLength = 100;

    private static readonly string[] Types =
    {
        "محضر اجتماع", "محضر", "تقرير", "عقد", "فاتورة ضريبية", "فاتورة", "قرار إداري", "قرار", "تعميم", "مذكرة داخلية", "مذكرة", "شهادة", "اتفاقية",
        "مذكرة تفاهم", "عرض سعر", "أمر شراء", "أمر صرف", "أمر دفع", "بلاغ صيانة", "بلاغ", "شكوى", "دعوة", "إشعار", "بيان إحصائي", "بيان", "خطة", "دليل",
        "نموذج", "طلب", "إقرار", "تفويض", "تنبيه أمني", "تنبيه", "التقرير الربعي", "التقرير السنوي", "رصد إعلامي", "خبر صحفي", "محضر استلام", "محضر فتح العروض",
    };

    private static readonly Regex Salutation = new(@"^(?:بسم الله|السلام عليكم|سعادة|معالي|صاحب|فضيلة|المكرم|الأستاذ|الاستاذ|الدكتور|إلى|الى|حفظه الله|وتقبلوا|وتقبل|مع خالص|والسلام|شاكرين|ولكم جزيل|المحترم|المرفقات|الرقم|التاريخ|رقم الفاتورة|سري|عاجل|يعتمد)", RegexOptions.Compiled);
    private static readonly Regex About = new(@"(?:بشأن|بخصوص)\s+([^،,.؛\n]{6,120})", RegexOptions.Compiled);
    private static readonly Regex Filler = new(@"^(?:(?:إشارة|اشارة|بالإشارة|بالاشارة|إلحاقاً|الحاقا|رداً|ردا|بناءً|بناء|عطفاً|عطفا)\s[^،,]*[،,]\s*)?(?:نفيدكم\s+(?:بأن|بانه|بأنه|علماً|علما)?|نحيطكم\s+علماً\s+بأن|نحيطكم\s+بأن|نود\s+(?:الإحاطة|الافادة|إفادتكم)\s+(?:بأن)?|يسرنا\s+(?:إفادتكم|افادتكم)\s+(?:بأن)?|نرفق\s+(?:لكم|لسعادتكم)?|مرفق\s+(?:لكم)?|نأمل\s+(?:التكرم\s+)?(?:ب)?|أرجو\s+(?:التكرم\s+)?(?:ب)?|ارجو\s+|نرجو\s+|آمل\s+|يرجى\s+|أتقدم\s+(?:لسعادتكم|إليكم)?\s*(?:ب)?|تم\s+|لقد\s+|كما\s+)\s*", RegexOptions.Compiled);
    private static readonly Regex RequestVerb = new(@"^(?:نأمل|أرجو|ارجو|نرجو|آمل|يرجى|أتقدم)", RegexOptions.Compiled);

    public static string Build(string text, string subject = "")
    {
        if (Clean(subject) is { Length: > 2 } s) return Clip(s);
        var lines = (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && Readable(l)).Take(20).ToList();
        if (lines.Count == 0) return "";

        // عنوان نوع المستند في أول الأسطر.
        foreach (var line in lines.Take(4))
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 8) continue;
            var type = Types.FirstOrDefault(t => line.StartsWith(t, StringComparison.Ordinal));
            if (type is null) continue;
            // عنوان وافٍ بذاته («عقد تقديم خدمات نظافة»، «أمر شراء رقم 551»).
            if (words.Length >= 3) return Clip(line.TrimEnd(':', '：'));
            var detail = Detail(text!, lines, line);
            return Clip(detail == "" ? line.TrimEnd(':') : $"{line.TrimEnd(':')}: {detail}");
        }
        if (About.Match(text!) is { Success: true } m && Clean(m.Groups[1].Value) is { Length: > 5 } about) return Clip(about);
        return Clip(FirstSentence(lines));
    }

    /// <summary>ما يوضح عنواناً عاماً كـ«محضر اجتماع» أو «فاتورة ضريبية»: اللجنة، أو البيان، أو الجملة الأولى.</summary>
    private static string Detail(string text, List<string> lines, string heading)
    {
        if (heading.StartsWith("محضر", StringComparison.Ordinal) && Regex.Match(text, @"(لجنة(?:\s+[\p{L}]+){1,4})") is { Success: true } c)
            return Regex.Replace(c.Groups[1].Value, @"\s+(?:اجتماعها|اجتماعاً|اجتماعا|بحضور|في|يوم)(?:\s.*)?$", "");
        foreach (var label in new[] { "البيان", "الموضوع", "المورد", "الجهة" })
            if (Regex.Match(text, label + @"\s*[:：]\s*([^\n]{3,80})") is { Success: true } kv) return Clean(kv.Groups[1].Value);
        return FirstSentence(lines.SkipWhile(l => l != heading).Skip(1).ToList());
    }

    private static readonly Regex Greeting = new(@"^(?:السلام عليكم(?:\s+ورحمة الله(?:\s+وبركاته)?)?|بسم الله الرحمن الرحيم|تحية طيبة(?:\s+وبعد)?|وبعد)\s*[،,.]?\s*", RegexOptions.Compiled);
    private static readonly Regex IntroClause = new(@"^(?:إشارة|اشارة|بالإشارة|بالاشارة|إلحاقاً|الحاقا|رداً|ردا|بناءً|بناء|عطفاً|عطفا|في إطار|في ضوء|نظراً|نظرا|استناداً|استنادا|حرصاً|حرصا|تبعاً|وفقاً|وفقا|بالإحالة|إلحاقا)\b", RegexOptions.Compiled);

    /// <summary>الجملة الأولى ذات المعنى: يُتجاوز التمهيد («إشارة إلى…،»، «نظراً لـ…،») وعبارات الافتتاح، وتؤخذ الجملة الرئيسة.</summary>
    private static string FirstSentence(List<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = Greeting.Replace(raw, "", 1).Trim();
            if (line.Length == 0 || Salutation.IsMatch(line) || line.Split(' ').Length < 4) continue;
            var sentence = Regex.Split(line, @"(?<=[.؟!])\s")[0];
            var clauses = Regex.Split(sentence, @"\s*[،؛]\s*|(?<!\d),(?!\d)\s*").Where(c => c.Length > 0).ToList();
            while (clauses.Count > 1 && IntroClause.IsMatch(clauses[0])) clauses.RemoveAt(0);
            var main = clauses[0];
            if (main.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 4 && clauses.Count > 1) main += "، " + clauses[1];
            var request = RequestVerb.IsMatch(main);
            var core = main;
            for (var i = 0; i < 3; i++) { var next = Filler.Replace(core, "", 1).Trim(); if (next == core) break; core = next; }
            core = Regex.Replace(core, @"^(?:الموافقة\s+على\s+|بالموافقة\s+على\s+|التوجيه\s+ب|بالتوجيه\s+ب|إفادتنا\s+ب)", "");
            // «نفيدكم بانتهاء…» ← «انتهاء…»: باء الجر المتبقية بعد حذف الفعل.
            if (core != main && Regex.IsMatch(core, @"^ب[\p{L}]{3,}")) core = core[1..];
            if (core.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2) continue;
            return (request && !core.StartsWith("طلب", StringComparison.Ordinal) ? "طلب " : "") + Clean(core);
        }
        return "";
    }

    private static string Clean(string? s)
    {
        s = Regex.Replace(s ?? "", @"\s+", " ").Trim().Trim('.', '،', ',', ':', '：', '-', '—', '؛');
        return Regex.Replace(s, @"^(?:بشأن|بخصوص)\s+", "");
    }

    private static string Clip(string s)
    {
        s = Clean(s);
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxWords) s = string.Join(' ', words.Take(MaxWords));
        if (s.Length > MaxLength) s = s[..s.LastIndexOf(' ', MaxLength)];
        s = s.Trim().TrimEnd('،', ',', '؛', ':', '-');
        // لا ينتهي العنوان بحرف جر أو عطف معلق.
        s = Regex.Replace(s, @"(?:\s+(?:في|من|على|إلى|الى|عن|مع|و|أو|ب|ل|حول|بين|لدى|خلال|بعد|قبل|منذ|عند|حتى|أمام|خارج|داخل|تمهيداً|لرفع|المقام|التي|الذي))+$", "");
        return s.Trim();
    }

    /// <summary>سطر مقروء: أغلبه حروف، لا ضجيج OCR.</summary>
    private static bool Readable(string line)
    {
        var letters = line.Count(char.IsLetter);
        var arabic = line.Count(c => c is >= 'ء' and <= 'ي');
        return letters >= 3 && (arabic >= letters * 0.6 || line.Count(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') >= letters * 0.9) && letters >= line.Length * 0.5;
    }
}
