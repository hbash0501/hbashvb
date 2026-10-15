Imports System
Imports System.Collections.Generic
Imports System.Text
Imports System.Text.RegularExpressions

Public Class FastEntityExtractionService
    Public Shared Sub ApplyFastEntities(documentRecord As DocumentRecord)
        If documentRecord Is Nothing Then Return
        Dim text As String = If(documentRecord.TextContent, "")
        If String.IsNullOrWhiteSpace(text) Then Return

        documentRecord.OcrDates = JoinMatches(text, "(?<!\d)(?:\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}[/-]\d{1,2}[/-]\d{1,2}|\d{1,2}\s+(?:محرم|صفر|ربيع|جمادى|رجب|شعبان|رمضان|شوال|ذو القعدة|ذو الحجة)\s+\d{3,4})(?!\d)", 25)
        documentRecord.OcrAmounts = JoinMatches(text, "(?:\d[\d,\.\s]{1,15})\s*(?:ريال|ر\.س|SAR|دولار|USD)", 25)
        documentRecord.OcrNationalIds = JoinMatches(text, "(?<!\d)(?:1|2)\d{9}(?!\d)", 25)
        documentRecord.OcrIqamaNumbers = JoinMatches(text, "(?<!\d)2\d{9}(?!\d)", 25)
        documentRecord.OcrPassportNumbers = JoinMatches(text, "(?<![A-Za-z0-9])[A-Za-z]\d{6,9}(?![A-Za-z0-9])", 25)
        documentRecord.OcrEmails = JoinMatches(text, "[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", 25, RegexOptions.IgnoreCase)
        documentRecord.OcrPhoneNumbers = JoinMatches(text, "(?<!\d)(?:\+966|00966|966|0)?5\d{8}(?!\d)", 25)
        documentRecord.OcrReferenceNumbers = JoinMatches(text, "(?:رقم|قيد|صادر|وارد|المعاملة|الخطاب)\s*[:：\-]??\s*([A-Za-z0-9\-/]{3,30})", 25)
        documentRecord.OcrIncomingNumbers = JoinMatches(text, "(?:وارد|الوارد)\s*[:：\-]??\s*([A-Za-z0-9\-/]{3,30})", 15)
        documentRecord.OcrOutgoingNumbers = JoinMatches(text, "(?:صادر|الصادر)\s*[:：\-]??\s*([A-Za-z0-9\-/]{3,30})", 15)
        documentRecord.OcrEntities = GuessOrganizations(text)
        documentRecord.OcrNames = GuessNames(text)
        If String.IsNullOrWhiteSpace(documentRecord.SourceEntity) Then documentRecord.SourceEntity = GuessAfterLabel(text, "(?:من|الجهة الصادرة|الصادر من|المصدر)")
        If String.IsNullOrWhiteSpace(documentRecord.DestinationEntity) Then documentRecord.DestinationEntity = GuessAfterLabel(text, "(?:إلى|الجهة المستقبلة|الوارد إلى|الموجه إلى)")
        documentRecord.OcrQualityScore = EstimateQualityScore(text)
        documentRecord.OcrEngineInfo = "Fast Regex Entities + Existing OCR"
        documentRecord.UpdatedAt = DateTime.Now
    End Sub

    Private Shared Function JoinMatches(text As String, pattern As String, maxItems As Integer, Optional options As RegexOptions = RegexOptions.None) As String
        Dim list As New List(Of String)()
        For Each m As Match In Regex.Matches(text, pattern, options Or RegexOptions.Singleline)
            Dim v As String = If(m.Groups.Count > 1 AndAlso m.Groups(1).Success, m.Groups(1).Value, m.Value)
            v = Clean(v)
            If v.Length > 0 AndAlso Not list.Contains(v) Then list.Add(v)
            If list.Count >= maxItems Then Exit For
        Next
        Return String.Join("، ", list.ToArray())
    End Function

    Private Shared Function GuessOrganizations(text As String) As String
        Dim list As New List(Of String)()
        Dim pattern As String = "(?:وزارة|إدارة|مديرية|قسم|مكتب|شركة|مؤسسة|مدرسة|جامعة|بلدية|محافظة|شرطة|مركز|بنك)\s+[\u0600-\u06FF\s]{2,60}"
        For Each m As Match In Regex.Matches(text, pattern)
            Dim v = Clean(m.Value)
            If v.Length > 3 AndAlso Not list.Contains(v) Then list.Add(v)
            If list.Count >= 20 Then Exit For
        Next
        Return String.Join("، ", list.ToArray())
    End Function

    Private Shared Function GuessNames(text As String) As String
        Dim list As New List(Of String)()
        Dim pattern As String = "(?:الأستاذ|سعادة|المكرم|السيد|المدير|المواطن|الموظف)\s+([\u0600-\u06FF]{2,}\s+[\u0600-\u06FF]{2,}(?:\s+[\u0600-\u06FF]{2,})?)"
        For Each m As Match In Regex.Matches(text, pattern)
            Dim v = Clean(m.Groups(1).Value)
            If v.Length > 4 AndAlso Not list.Contains(v) Then list.Add(v)
            If list.Count >= 20 Then Exit For
        Next
        Return String.Join("، ", list.ToArray())
    End Function

    Private Shared Function GuessAfterLabel(text As String, labelPattern As String) As String
        Dim m = Regex.Match(text, labelPattern & "\s*[:：\-]?\s*([^\r\n]{3,80})")
        If m.Success Then Return Clean(m.Groups(1).Value)
        Return ""
    End Function

    Private Shared Function EstimateQualityScore(text As String) As Integer
        If String.IsNullOrWhiteSpace(text) Then Return 0
        Dim lenScore As Integer = Math.Min(55, CInt(text.Length / 80))
        Dim arabic = Regex.Matches(text, "[\u0600-\u06FF]").Count
        Dim noise = Regex.Matches(text, "[�□■]{1}").Count
        Dim score As Integer = 35 + lenScore + Math.Min(20, CInt(arabic / 200)) - Math.Min(30, noise * 3)
        If score < 0 Then score = 0
        If score > 100 Then score = 100
        Return score
    End Function

    Private Shared Function Clean(value As String) As String
        If value Is Nothing Then Return ""
        Dim v = Regex.Replace(value, "\s+", " ").Trim(" "c, ":"c, "-"c, "،"c, "."c)
        If v.Length > 120 Then v = v.Substring(0, 120).Trim()
        Return v
    End Function
End Class
