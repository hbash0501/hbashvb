Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text.RegularExpressions

' يستخرج المهل المطلوبة من نص الخطاب: نسبية («خلال (15) يوماً من تاريخه»، «خلال أسبوعين»، «خلال عشرة أيام عمل»)
' أو مطلقة («في موعد أقصاه 1447/5/5هـ»، «قبل تاريخ 2025/10/10م»)، ويحسب تاريخ الاستحقاق من تاريخ المستند.
Public Class DeadlineHint
    Public Property IsRelative As Boolean
    Public Property Days As Integer
    Public Property BusinessDays As Boolean
    Public Property Due As Nullable(Of DateTime)
    Public Property Evidence As String = ""
    Public Property Offset As Integer
    Public Property Length As Integer

    Public ReadOnly Property Label As String
        Get
            If Not IsRelative Then Return "موعد محدد"
            Return "خلال " & Days.ToString(CultureInfo.InvariantCulture) & If(BusinessDays, " يوم عمل", " يوماً")
        End Get
    End Property
End Class

Public NotInheritable Class DeadlineExtractor
    Private Sub New()
    End Sub

    Private Shared ReadOnly NumberWords As New Dictionary(Of String, Integer)(StringComparer.Ordinal) From {
        {"يوم", 1}, {"يومين", 2}, {"ثلاثه", 3}, {"اربعه", 4}, {"خمسه", 5}, {"سته", 6}, {"سبعه", 7}, {"ثمانيه", 8}, {"تسعه", 9}, {"عشره", 10},
        {"ثلاث", 3}, {"اربع", 4}, {"خمس", 5}, {"ست", 6}, {"سبع", 7}, {"ثماني", 8}, {"تسع", 9}, {"عشر", 10},
        {"خمسه عشر", 15}, {"عشرين", 20}, {"ثلاثين", 30}, {"اربعين", 40}, {"خمسه واربعين", 45}, {"ستين", 60}, {"تسعين", 90}}

    Private Shared ReadOnly RelativeRx As Regex
    Private Shared ReadOnly PeriodRx As New Regex("(?<!\p{L})خلال\s+(?<p>اسبوع|اسبوعين|شهر|شهرين|ثلاثه\s+اشهر)(?!\p{L})", RegexOptions.Compiled)
    Private Shared ReadOnly AbsoluteRx As Regex
    ' «خلال (15) … من تاريخه» حين تشوّه OCR كلمة الوحدة: تُعد أياماً، وهو الاستخدام الغالب في المراسلات.
    Private Shared ReadOnly GarbledUnitRx As New Regex("(?<!\p{L})خلال\s*[\(\[]\s*(?<n>[0-9]{1,3})\s*[\)\]]\s*\S{1,10}\s+من\s+تاريخ", RegexOptions.Compiled)

    Shared Sub New()
        Dim words = String.Join("|", NumberWords.Keys.OrderByDescending(Function(k) k.Length).Select(Function(k) k.Replace(" ", "\s+")))
        RelativeRx = New Regex("(?<!\p{L})(?:خلال|في\s+غضون|في\s+مده\s+لا\s+تتجاوز|بمده\s+لا\s+تتجاوز|في\s+مده\s+اقصاها)\s*[\(\[]?\s*(?:(?<n>[0-9]{1,3})|(?<w>" & words & "))\s*[\)\]]?\s*(?<u>يوما|يوم|ايام)?(?<b>\s+عمل)?(?!\p{L})", RegexOptions.Compiled)
        AbsoluteRx = New Regex("(?<!\p{L})(?:في\s+موعد\s+(?:اقصاه|غايته|لا\s+يتجاوز)|موعدا\s+اقصاه|بحد\s+اقصي|حتي\s+تاريخ|قبل\s+تاريخ|قبل\s+نهايه\s+يوم|في\s+موعد\s+اقصاه\s+يوم)\s*[:：\(]?\s*(?:\p{L}+\s+)?" & CorrespondenceReferenceParser.DateBody, RegexOptions.Compiled)
    End Sub

    ' يضيف أيام عمل بأسبوع عمل من الأحد إلى الخميس.
    Public Shared Function AddBusinessDays(start As DateTime, days As Integer) As DateTime
        Dim d As DateTime = start.Date
        Dim added As Integer = 0
        While added < days
            d = d.AddDays(1)
            If d.DayOfWeek <> DayOfWeek.Friday AndAlso d.DayOfWeek <> DayOfWeek.Saturday Then added += 1
        End While
        Return d
    End Function

    Public Shared Function Extract(text As String, documentDate As Nullable(Of DateTime)) As List(Of DeadlineHint)
        Dim result As New List(Of DeadlineHint)
        If String.IsNullOrWhiteSpace(text) Then Return result
        Dim map As New List(Of Integer)(text.Length)
        Dim norm As String = CorrespondenceReferenceParser.NormalizeWithMap(text, map)

        For Each m As Match In RelativeRx.Matches(norm)
            Dim days As Integer
            If m.Groups("n").Success Then
                days = Integer.Parse(m.Groups("n").Value, CultureInfo.InvariantCulture)
                If Not m.Groups("u").Success Then Continue For ' «خلال 15» دون وحدة غامض.
            Else
                days = NumberWords(Regex.Replace(m.Groups("w").Value, "\s+", " "))
                If Not m.Groups("u").Success AndAlso days > 2 Then Continue For
            End If
            If days <= 0 OrElse days > 365 Then Continue For
            Dim h As New DeadlineHint With {.IsRelative = True, .Days = days, .BusinessDays = m.Groups("b").Success}
            If documentDate.HasValue Then h.Due = If(h.BusinessDays, AddBusinessDays(documentDate.Value, days), documentDate.Value.Date.AddDays(days))
            Place(text, map, m, h)
            result.Add(h)
        Next

        For Each m As Match In GarbledUnitRx.Matches(norm)
            If result.Any(Function(x) x.Offset = map(m.Index)) Then Continue For
            Dim days As Integer = Integer.Parse(m.Groups("n").Value, CultureInfo.InvariantCulture)
            If days <= 0 OrElse days > 90 Then Continue For
            Dim h As New DeadlineHint With {.IsRelative = True, .Days = days}
            If documentDate.HasValue Then h.Due = documentDate.Value.Date.AddDays(days)
            Place(text, map, m, h)
            result.Add(h)
        Next

        For Each m As Match In PeriodRx.Matches(norm)
            Dim p As String = Regex.Replace(m.Groups("p").Value, "\s+", " ")
            Dim days As Integer = If(p = "اسبوع", 7, If(p = "اسبوعين", 14, If(p = "شهر", 30, If(p = "شهرين", 60, 90))))
            Dim h As New DeadlineHint With {.IsRelative = True, .Days = days}
            If documentDate.HasValue Then h.Due = documentDate.Value.Date.AddDays(days)
            Place(text, map, m, h)
            result.Add(h)
        Next

        For Each m As Match In AbsoluteRx.Matches(norm)
            Dim canonical As String = ""
            Dim value As DateTime
            Dim calendar As String = ""
            If Not CorrespondenceReferenceParser.Resolve(m, canonical, value, calendar) Then Continue For
            Dim h As New DeadlineHint With {.IsRelative = False, .Due = value}
            Place(text, map, m, h)
            result.Add(h)
        Next
        Return result.OrderBy(Function(x) x.Offset).ToList()
    End Function

    Private Shared Sub Place(text As String, map As List(Of Integer), m As Match, h As DeadlineHint)
        h.Offset = map(m.Index)
        Dim e As Integer = map(m.Index + m.Length - 1) + 1
        While e < text.Length AndAlso (AscW(text(e)) = &H640 OrElse (AscW(text(e)) >= &H64B AndAlso AscW(text(e)) <= &H65F))
            e += 1
        End While
        h.Length = e - h.Offset
        h.Evidence = Regex.Replace(text.Substring(h.Offset, h.Length), "\s+", " ").Trim()
    End Sub

    ' أقرب موعد استحقاق محسوب، إن وُجد.
    Public Shared Function EarliestDue(text As String, documentDate As Nullable(Of DateTime)) As Nullable(Of DateTime)
        Dim dues = Extract(text, documentDate).Where(Function(h) h.Due.HasValue).Select(Function(h) h.Due.Value).ToList()
        If dues.Count = 0 Then Return Nothing
        Return dues.Min()
    End Function
End Class
