Imports System
Imports System.Text
Imports System.Text.RegularExpressions

Public Class SpeedPromptService
    Public Shared Function BuildLeanDocumentContext(documentRecord As DocumentRecord, maxChars As Integer) As String
        If documentRecord Is Nothing Then Return ""
        Dim raw As String = If(documentRecord.TextContent, "")
        raw = NormalizeText(raw)
        Dim important As String = ExtractImportantSlices(raw, maxChars)
        Dim sb As New StringBuilder()
        sb.AppendLine("بيانات مختصرة للمستند:")
        sb.AppendLine("العنوان: " & Safe(documentRecord.Title))
        sb.AppendLine("رقم الأرشيف: " & Safe(documentRecord.ArchiveNumber))
        sb.AppendLine("رقم المرجع: " & Safe(documentRecord.ReferenceNumber))
        sb.AppendLine("الصادر: " & Safe(documentRecord.SourceEntity))
        sb.AppendLine("الوارد: " & Safe(documentRecord.DestinationEntity))
        sb.AppendLine("التصنيف: " & Safe(documentRecord.GeneralCategory) & " / " & Safe(documentRecord.SubCategory))
        sb.AppendLine("السرية: " & Safe(documentRecord.Confidentiality) & " | الأولوية: " & Safe(documentRecord.Priority))
        sb.AppendLine()
        sb.AppendLine("كيانات مستخرجة محلياً:")
        sb.AppendLine("التواريخ: " & Safe(documentRecord.OcrDates))
        sb.AppendLine("الأرقام: " & Safe(documentRecord.OcrReferenceNumbers))
        sb.AppendLine("المبالغ: " & Safe(documentRecord.OcrAmounts))
        sb.AppendLine("الأسماء: " & Safe(documentRecord.OcrNames))
        sb.AppendLine("الجهات: " & Safe(documentRecord.OcrEntities))
        sb.AppendLine()
        sb.AppendLine("مقاطع مختارة من النص:")
        sb.AppendLine(important)
        Return sb.ToString()
    End Function

    Public Shared Function NormalizeText(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return ""
        Dim t = text.Replace(ControlChars.Tab, " ")
        t = Regex.Replace(t, "[ ]{2,}", " ")
        t = Regex.Replace(t, "(\r?\n){3,}", Environment.NewLine & Environment.NewLine)
        Return t.Trim()
    End Function

    Public Shared Function ExtractImportantSlices(text As String, maxChars As Integer) As String
        If String.IsNullOrWhiteSpace(text) Then Return ""
        If maxChars < 1000 Then maxChars = 1000
        If text.Length <= maxChars Then Return text

        Dim firstLen As Integer = Math.Min(CInt(maxChars * 0.55), text.Length)
        Dim lastLen As Integer = Math.Min(CInt(maxChars * 0.2), Math.Max(0, text.Length - firstLen))
        Dim keywords As String = "عاجل|هام|سري|المطلوب|نأمل|نفيدكم|بناء|إشارة|رقم|تاريخ|المبلغ|اعتماد|استكمال|شكوى|تعميم|إفادة|إحالة|رد"
        Dim middle As New StringBuilder()
        For Each m As Match In Regex.Matches(text, ".{0,180}(" & keywords & ").{0,220}", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
            If middle.Length > CInt(maxChars * 0.25) Then Exit For
            middle.AppendLine(m.Value.Trim())
            middle.AppendLine("---")
        Next

        Dim sb As New StringBuilder()
        sb.AppendLine(text.Substring(0, firstLen).Trim())
        If middle.Length > 0 Then
            sb.AppendLine()
            sb.AppendLine("[مقاطع مهمة]")
            sb.AppendLine(middle.ToString())
        End If
        If lastLen > 0 Then
            sb.AppendLine()
            sb.AppendLine("[آخر المستند]")
            sb.AppendLine(text.Substring(text.Length - lastLen).Trim())
        End If
        Dim result = sb.ToString()
        If result.Length > maxChars Then result = result.Substring(0, maxChars)
        Return result
    End Function

    Private Shared Function Safe(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "-"
        Return value.Trim()
    End Function
End Class
