using System.Text.RegularExpressions;
using AIDocumentArchiveVB;

namespace Basira.Platform.Processing;

/// <summary>حقول ترويسة الخطاب المعتادة: الرقم، التاريخ، الموضوع، المرفقات؛ وأول فقرة بعد التحية كملخص.</summary>
public sealed record LetterHeader(string Number, string DateIso, string DateText, string Subject, string Summary, string Sender = "", string Addressee = "");

public static class LetterFields
{
    private static string Norm(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch0 in s)
        {
            var ch = ch0;
            if ((ch >= 'ً' && ch <= 'ٟ') || ch == 'ٰ' || ch == 'ـ') { sb.Append(' '); continue; }
            if (ch >= '٠' && ch <= '٩') ch = (char)('0' + ch - '٠');
            else if (ch >= '۰' && ch <= '۹') ch = (char)('0' + ch - '۰');
            else if (ch is 'أ' or 'إ' or 'آ') ch = 'ا';
            else if (ch == 'ى') ch = 'ي';
            else if (ch == 'ة') ch = 'ه';
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static readonly Regex NumberRx = new(@"(?m)^[^\n]{0,90}?(?:الرقم|رقم\s+الصادر|رقم\s+الخطاب|رقم\s+القيد|الصادر\s+رقم|رقم\s+الوارد)\s*[:：]\s*(?<v>[A-Za-z0-9]+(?:\s*[/\-]\s*(?:[A-Za-z0-9]+|\p{L}{1,3}(?!\p{L})))*)", RegexOptions.Compiled);
    private static readonly Regex DateRx = new(@"(?m)(?:^|\s{2,}|\b)التاريخ\s*[:：]\s*(?<v>[0-9]{1,4}\s*[/\-.]\s*[0-9]{1,2}\s*[/\-.]\s*[0-9]{1,4}\s*(?:ه|هـ|م)?|[0-9]{1,2}\s+\p{L}+(?:\s+\p{L}+)?\s+[0-9]{3,4}\s*(?:ه|م)?)", RegexOptions.Compiled);
    private static readonly Regex SubjectRx = new(@"(?m)^\s*الموضوع\s*[:：]\s*(?<v>[^\n]{3,160})$", RegexOptions.Compiled);

    public static LetterHeader Parse(string text)
    {
        var n = Norm(text ?? "");
        var number = NumberRx.Match(n) is { Success: true } nm ? Regex.Replace(nm.Groups["v"].Value, @"\s+", "").Trim('/', '-') : "";
        var dateIso = ""; var dateText = "";
        if (DateRx.Match(n) is { Success: true } dm)
        {
            var canonical = ""; DateTime value = default;
            dateText = dm.Groups["v"].Value.Trim();
            if (CorrespondenceReferenceParser.TryParseDate(dateText, ref canonical, ref value)) dateIso = value.ToString("yyyy-MM-dd");
        }
        var subject = "";
        if (SubjectRx.Match(n) is { Success: true } sm)
        {
            // الموضوع من النص الأصلي بالمواضع نفسها (التطبيع يحافظ على الطول).
            subject = Regex.Replace(text!.Substring(sm.Groups["v"].Index, sm.Groups["v"].Length), @"\s+", " ").Trim();
            subject = Regex.Replace(subject, @"^(?:بشأن|بخصوص)\s+", "");
        }
        return new LetterHeader(number, dateIso, dateText, subject, Summarize(text ?? ""), Sender(text ?? ""), Addressee(text ?? ""));
    }

    private static readonly Regex OrgRx = new(@"^(?:وزاره|وزارة|امانه|أمانة|هيئه|هيئة|الهيئه|الهيئة|اداره|إدارة|الاداره|الإدارة|شركه|شركة|مؤسسه|مؤسسة|بلديه|بلدية|جامعه|جامعة|مستشفي|مستشفى|مكتب|المديريه|المديرية|ديوان|مجلس|الرئاسه|الرئاسة|فرع|مركز|المركز|صندوق)", RegexOptions.Compiled);
    private static readonly Regex HeaderLabelRx = new(@"\s*(?:الرقم|التاريخ|المرفقات|الموضوع)\s*[:：].*$", RegexOptions.Compiled);

    /// <summary>الجهة المرسلة: أول سطر في الترويسة يبدأ باسم جهة (وزارة، أمانة، هيئة، إدارة…).</summary>
    public static string Sender(string text)
    {
        foreach (var raw in text.Split('\n').Take(6))
        {
            var line = HeaderLabelRx.Replace(raw, "").Trim();
            if (line.Length is < 4 or > 80) continue;
            if (OrgRx.IsMatch(Norm(line).Replace("ه ", "ة ")) || OrgRx.IsMatch(line)) return line;
        }
        return "";
    }

    private static readonly Regex AddresseeRx = new(@"^\s*(?:صاحب\s+السمو\s+الملكي|صاحب\s+السمو|صاحب\s+المعالي|سعادة|سعاده|معالي|سمو|فضيلة|فضيله|المكرم|المكرمة|الأستاذ|الاستاذ|الدكتور)\s+(?<who>.+?)\s*(?:المحترم|المحترمة|المحترمين|حفظه\s+الله|وفقه\s+الله|سلمه\s+الله|يحفظه\s+الله)?\s*$", RegexOptions.Compiled);

    /// <summary>المخاطَب: سطر «سعادة … المحترم» دون ألقاب التفخيم.</summary>
    public static string Addressee(string text)
    {
        foreach (var raw in text.Split('\n').Take(12))
        {
            var line = raw.Trim();
            if (line.Length > 90) continue;
            var m = AddresseeRx.Match(line);
            if (m.Success)
            {
                var who = m.Groups["who"].Value.Trim().TrimEnd('،', ',');
                return who.Length is >= 3 and <= 70 ? who : "";
            }
        }
        return "";
    }

    /// <summary>أول فقرة جوهرية بعد الترويسة والتحية، مقتطعة إلى 300 حرف.</summary>
    public static string Summarize(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length < 40) continue;
            var n = Norm(line);
            if (Regex.IsMatch(n, @"^(?:السلام عليكم|سعاده|معالي|المكرم|صاحب|الموضوع|الرقم|التاريخ|المرفقات)")) continue;
            return line.Length <= 300 ? line : line[..300].TrimEnd() + "…";
        }
        return "";
    }
}
