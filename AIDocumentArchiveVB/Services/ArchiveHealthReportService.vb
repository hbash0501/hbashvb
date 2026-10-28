Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text

' يولّد تقرير «صحة الأرشيف» كملف HTML مستقل (عربي، متجاوب، قابل للطباعة)
' من السجلات ونتائج BasiraEngine.Findings دون أي اتصال خارجي.
Public Class HealthMetrics
    Public Property Total As Integer
    Public Property WithText As Integer
    Public Property Categorized As Integer
    Public Property Sensitive As Integer
    Public Property Closed As Integer
    Public Property Overdue As Integer
    Public Property Duplicates As Integer
    Public Property TextPercent As Integer
    Public Property CategoryPercent As Integer
    Public Property Score As Integer
End Class

Public NotInheritable Class ArchiveHealthReportService
    Private Sub New()
    End Sub

    Public Shared Function Compute(documents As IList(Of DocumentRecord), findings As IList(Of BasiraFinding)) As HealthMetrics
        Dim docs As IList(Of DocumentRecord) = If(documents, New List(Of DocumentRecord)())
        Dim items As IList(Of BasiraFinding) = If(findings, New List(Of BasiraFinding)())
        Dim m As New HealthMetrics()
        m.Total = docs.Count
        m.WithText = docs.Where(Function(d) Not String.IsNullOrWhiteSpace(d.TextContent)).Count()
        m.Categorized = docs.Where(Function(d) Not String.IsNullOrWhiteSpace(d.GeneralCategory)).Count()
        m.Sensitive = docs.Where(Function(d) BasiraEngine.IsSensitive(d)).Count()
        m.Closed = docs.Where(Function(d) BasiraEngine.IsClosed(d)).Count()
        m.Overdue = items.Where(Function(f) f.Kind = "موعد" AndAlso f.Severity = 0).Count()
        m.Duplicates = items.Where(Function(f) f.Kind = "نسخة مطابقة").Count()
        m.TextPercent = Pct(m.WithText, m.Total)
        m.CategoryPercent = Pct(m.Categorized, m.Total)
        If m.Total > 0 Then
            m.Score = Math.Max(0, Math.Min(100, CInt(Math.Round((m.TextPercent + m.CategoryPercent) / 2.0R)) - Math.Min(30, m.Overdue * 5) - Math.Min(20, m.Duplicates * 2)))
        End If
        Return m
    End Function

    Private Shared Function H(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function

    Private Shared Function Pct(part As Integer, total As Integer) As Integer
        If total <= 0 Then Return 0
        Return CInt(Math.Round(part * 100.0R / total, MidpointRounding.AwayFromZero))
    End Function

    Public Shared Sub Export(documents As IList(Of DocumentRecord), findings As IList(Of BasiraFinding), preparedBy As String, destination As String)
        If String.IsNullOrWhiteSpace(destination) Then Throw New ArgumentException("مسار التقرير غير صحيح.")
        File.WriteAllText(destination, Build(documents, findings, preparedBy, DateTime.Now), New UTF8Encoding(True))
    End Sub

    Public Shared Function Build(documents As IList(Of DocumentRecord), findings As IList(Of BasiraFinding), preparedBy As String, now As DateTime) As String
        Dim docs As IList(Of DocumentRecord) = If(documents, New List(Of DocumentRecord)())
        Dim items As IList(Of BasiraFinding) = If(findings, New List(Of BasiraFinding)())
        Dim m As HealthMetrics = Compute(docs, items)
        Dim total As Integer = m.Total
        Dim textPct As Integer = m.TextPercent
        Dim catPct As Integer = m.CategoryPercent
        Dim overdue As Integer = m.Overdue
        Dim sensitive As Integer = m.Sensitive
        Dim closed As Integer = m.Closed
        Dim duplicates As Integer = m.Duplicates
        Dim score As Integer = m.Score

        Dim sb As New StringBuilder()
        sb.AppendLine("<!doctype html><html lang='ar' dir='rtl'><head><meta charset='utf-8'>")
        sb.AppendLine("<meta name='viewport' content='width=device-width,initial-scale=1'>")
        sb.AppendLine("<title>تقرير صحة الأرشيف | بصيرة</title><style>")
        sb.AppendLine(":root{--bg:#f3f5f4;--card:#fff;--ink:#0f1f1c;--mut:#667975;--line:#dfe6e4;--teal:#0e6b5e;--gold:#b8862e;--red:#b4443b}")
        sb.AppendLine("@media (prefers-color-scheme:dark){:root{--bg:#10201e;--card:#17302c;--ink:#e7f0ee;--mut:#9fb5b0;--line:#2a4641;--teal:#4fb5a5;--gold:#d9a948;--red:#e0776d}}")
        sb.AppendLine("*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font-family:'IBM Plex Sans Arabic','Segoe UI',Tahoma,sans-serif;line-height:1.7}")
        sb.AppendLine(".brand{display:flex;align-items:center;gap:14px;margin-bottom:6px}.brand svg{width:46px;height:46px;flex:none}")
        sb.AppendLine("main{max-width:1000px;margin:0 auto;padding:24px 16px 48px}h1{margin:0 0 4px;font-size:26px}h2{font-size:18px;margin:28px 0 10px}")
        sb.AppendLine(".sub{color:var(--mut);font-size:13px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px;margin-top:18px}")
        sb.AppendLine(".card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px}.card small{color:var(--mut);display:block}.card b{font-size:26px}")
        sb.AppendLine(".score b{color:var(--teal)}.bar{height:10px;background:var(--line);border-radius:6px;overflow:hidden}.bar i{display:block;height:100%;background:var(--teal)}")
        sb.AppendLine(".row{display:grid;grid-template-columns:minmax(90px,200px) 1fr 48px;gap:10px;align-items:center;margin:6px 0;font-size:14px}")
        sb.AppendLine("table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);border-radius:12px;overflow:hidden;font-size:14px}")
        sb.AppendLine("th,td{padding:9px 12px;text-align:right;border-bottom:1px solid var(--line);vertical-align:top}th{background:var(--bg);color:var(--mut);font-weight:600}")
        sb.AppendLine(".sev0{color:var(--red);font-weight:700}.sev1{color:var(--gold);font-weight:700}.wrap{overflow-x:auto}")
        sb.AppendLine("@media print{body{background:#fff}.card,table{break-inside:avoid}}")
        sb.AppendLine("</style></head><body><main>")
        ' شعار بصيرة (حرف الباء ونقطته الذهبية) كما في المنصة.
        sb.AppendLine("<div class='brand'><svg viewBox='0 0 48 48' aria-hidden='true'><rect width='48' height='48' rx='12' fill='#0e6b5e'/><path d='M38.4 13.6c1 9.2-2.6 14.9-11.4 14.9h-8.6c-5 0-8-2-8.6-6.4' fill='none' stroke='#fff' stroke-width='4.4' stroke-linecap='round' stroke-linejoin='round'/><circle cx='24' cy='36' r='3.1' fill='#e2b45c'/></svg><h1>تقرير صحة الأرشيف</h1></div>")
        sb.AppendLine("<div class='sub'>أُعدّ بواسطة " & H(preparedBy) & " بتاريخ " & now.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture) & " | بصيرة</div>")
        sb.AppendLine("<div class='grid'>")
        sb.AppendLine("<div class='card score'><small>مؤشر الصحة</small><b>" & score.ToString(CultureInfo.InvariantCulture) & "%</b></div>")
        sb.AppendLine("<div class='card'><small>إجمالي المستندات</small><b>" & total.ToString(CultureInfo.InvariantCulture) & "</b></div>")
        sb.AppendLine("<div class='card'><small>نصوص قابلة للبحث</small><b>" & textPct.ToString(CultureInfo.InvariantCulture) & "%</b></div>")
        sb.AppendLine("<div class='card'><small>مصنّفة</small><b>" & catPct.ToString(CultureInfo.InvariantCulture) & "%</b></div>")
        sb.AppendLine("<div class='card'><small>مواعيد متأخرة</small><b>" & overdue.ToString(CultureInfo.InvariantCulture) & "</b></div>")
        sb.AppendLine("<div class='card'><small>مستندات حساسة</small><b>" & sensitive.ToString(CultureInfo.InvariantCulture) & "</b></div>")
        sb.AppendLine("<div class='card'><small>منجزة / مؤرشفة</small><b>" & closed.ToString(CultureInfo.InvariantCulture) & "</b></div>")
        sb.AppendLine("<div class='card'><small>نسخ مطابقة</small><b>" & duplicates.ToString(CultureInfo.InvariantCulture) & "</b></div>")
        sb.AppendLine("</div>")

        AppendDistribution(sb, "التوزيع حسب التصنيف", docs.Select(Function(d) If(String.IsNullOrWhiteSpace(d.GeneralCategory), "غير مصنّف", d.GeneralCategory.Trim())), total)
        AppendDistribution(sb, "التوزيع حسب الحالة", docs.Select(Function(d) If(String.IsNullOrWhiteSpace(d.Status), "غير محددة", d.Status.Trim())), total)

        sb.AppendLine("<h2>ما يحتاج انتباهاً</h2>")
        If items.Count = 0 Then
            sb.AppendLine("<div class='card'>لا توجد تنبيهات حالياً.</div>")
        Else
            sb.AppendLine("<div class='wrap'><table><thead><tr><th>النوع</th><th>المستند</th><th>السبب</th></tr></thead><tbody>")
            For Each f In items.Take(100)
                Dim cls As String = If(f.Severity = 0, "sev0", If(f.Severity = 1, "sev1", ""))
                sb.AppendLine("<tr><td class='" & cls & "'>" & H(f.Kind) & "</td><td>" & H(f.Title) & "</td><td>" & H(f.Reason) & "</td></tr>")
            Next
            sb.AppendLine("</tbody></table></div>")
            If items.Count > 100 Then sb.AppendLine("<div class='sub'>عُرض أول 100 تنبيه من " & items.Count.ToString(CultureInfo.InvariantCulture) & ".</div>")
        End If
        sb.AppendLine("<p class='sub'>يعتمد المؤشر على اكتمال النص والتصنيف مع خصم للمواعيد المتأخرة والنسخ المطابقة. يحتوي التقرير عناوين مستندات فقط دون محتواها.</p>")
        sb.AppendLine("</main></body></html>")
        Return sb.ToString()
    End Function

    Private Shared Sub AppendDistribution(sb As StringBuilder, title As String, values As IEnumerable(Of String), total As Integer)
        sb.AppendLine("<h2>" & H(title) & "</h2><div class='card'>")
        Dim groups = values.GroupBy(Function(v) v).Select(Function(g) New KeyValuePair(Of String, Integer)(g.Key, g.Count())).OrderByDescending(Function(p) p.Value).Take(8).ToList()
        If groups.Count = 0 Then sb.AppendLine("لا توجد بيانات.")
        For Each p In groups
            Dim w As Integer = Pct(p.Value, total)
            sb.AppendLine("<div class='row'><span>" & H(p.Key) & "</span><div class='bar'><i style='width:" & w.ToString(CultureInfo.InvariantCulture) & "%'></i></div><span>" & p.Value.ToString(CultureInfo.InvariantCulture) & "</span></div>")
        Next
        sb.AppendLine("</div>")
    End Sub
End Class
