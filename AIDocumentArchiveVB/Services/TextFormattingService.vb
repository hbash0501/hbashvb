Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class TextFormattingService
    Private Shared ReadOnly DateColor As Color = Color.FromArgb(72, 96, 164)
    Private Shared ReadOnly NumberColor As Color = Color.FromArgb(36, 120, 110)
    Private Shared ReadOnly NameColor As Color = Color.FromArgb(128, 88, 20)
    Private Shared ReadOnly EntityColor As Color = Color.FromArgb(115, 77, 150)
    Private Shared ReadOnly AmountColor As Color = Color.FromArgb(142, 79, 67)
    Private Shared ReadOnly LightBlue As Color = Color.FromArgb(231, 238, 255)
    Private Shared ReadOnly LightGreen As Color = Color.FromArgb(227, 246, 240)
    Private Shared ReadOnly LightGold As Color = Color.FromArgb(255, 246, 219)
    Private Shared ReadOnly LightPurple As Color = Color.FromArgb(241, 233, 249)
    Private Shared ReadOnly LightRed As Color = Color.FromArgb(253, 235, 232)

    Public Shared Function NormalizeOcrText(rawText As String) As String
        If String.IsNullOrWhiteSpace(rawText) Then Return ""
        Dim text As String = rawText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        text = Regex.Replace(text, "[ \t]{2,}", " ")
        text = Regex.Replace(text, "\n[ \t]+", vbLf)
        text = Regex.Replace(text, "[ \t]+\n", vbLf)
        text = Regex.Replace(text, "\n{3,}", vbLf & vbLf)
        text = Regex.Replace(text, "(?m)^\s*[-–—]{3,}\s*$", "")
        text = text.Replace(" :", ":").Replace(" ،", "،").Replace(" .", ".")
        Return text.Trim()
    End Function

    Public Shared Sub ApplyFormattedDocument(rtb As RichTextBox, document As DocumentRecord)
        If rtb Is Nothing Then Return
        Dim text As String = ""
        If document IsNot Nothing Then text = NormalizeOcrText(document.TextContent)
        If String.IsNullOrWhiteSpace(text) AndAlso document IsNot Nothing Then text = NormalizeOcrText(document.OcrStructuredReport)
        If String.IsNullOrWhiteSpace(text) Then text = "لا يوجد نص مستخرج بعد. شغّل OCR أو PDF OCR ثم عد لهذه الشاشة."

        rtb.SuspendLayout()
        rtb.Clear()
        rtb.RightToLeft = RightToLeft.Yes
        rtb.Font = New Font("Tahoma", 11.0F, FontStyle.Regular)
        rtb.BackColor = Color.White
        rtb.ForeColor = Color.FromArgb(31, 41, 38)
        rtb.Text = text
        rtb.SelectAll()
        rtb.SelectionFont = New Font("Tahoma", 11.0F, FontStyle.Regular)
        rtb.SelectionColor = Color.FromArgb(31, 41, 38)
        rtb.SelectionBackColor = Color.White
        rtb.SelectionAlignment = HorizontalAlignment.Right

        ApplyRegex(rtb, "(?<!\d)(?:[0-3]?\d[/-][01]?\d[/-](?:13|14|15|19|20)\d{2}|(?:13|14|15|19|20)\d{2}[/-][01]?\d[/-][0-3]?\d)(?!\d)", DateColor, LightBlue, True)
        ApplyRegex(rtb, "(?<!\d)(?:[12]\d{9})(?!\d)", NumberColor, LightGreen, True)
        ApplyRegex(rtb, "(?<!\d)(?:05\d{8}|9665\d{8})(?!\d)", NumberColor, LightGreen, False)
        ApplyRegex(rtb, "[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", NumberColor, LightGreen, False)
        ApplyRegex(rtb, "(?<![\d\w])\d+(?:[,.]\d+)?\s*(?:ريال|ر\.س|SAR|دولار)(?![\w])", AmountColor, LightRed, True)
        ApplyRegex(rtb, "(?:رقم|قيد|صادر|وارد|مرجع|خطاب)\s*[:：-]?\s*[A-Za-z0-9\-/]{3,}", NumberColor, LightGreen, True)
        ApplyList(rtb, SplitValues(If(document Is Nothing, "", document.OcrNames)), NameColor, LightGold, True)
        ApplyList(rtb, SplitValues(If(document Is Nothing, "", document.OcrEntities)), EntityColor, LightPurple, True)
        ApplyList(rtb, SplitValues(If(document Is Nothing, "", document.SourceEntity & "|" & document.DestinationEntity)), EntityColor, LightPurple, True)

        rtb.Select(0, 0)
        rtb.ResumeLayout()
    End Sub

    Public Shared Function BuildEntitySummary(document As DocumentRecord) As String
        If document Is Nothing Then Return ""
        Dim sb As New StringBuilder()
        AppendLine(sb, "التواريخ", document.OcrDates)
        AppendLine(sb, "أرقام الصادر", document.OcrOutgoingNumbers)
        AppendLine(sb, "أرقام الوارد", document.OcrIncomingNumbers)
        AppendLine(sb, "المراجع", document.OcrReferenceNumbers)
        AppendLine(sb, "الأسماء", document.OcrNames)
        AppendLine(sb, "الجهات", document.OcrEntities)
        AppendLine(sb, "المبالغ", document.OcrAmounts)
        AppendLine(sb, "الهويات/السجلات", JoinNonEmpty(document.OcrNationalIds, document.OcrIqamaNumbers, document.OcrPassportNumbers))
        AppendLine(sb, "التواصل", JoinNonEmpty(document.OcrPhoneNumbers, document.OcrEmails))
        If sb.Length = 0 Then sb.AppendLine("لم يتم استخراج كيانات منظمة بعد. استخدم OCR الاحترافي أو التحليل العميق.")
        Return sb.ToString()
    End Function

    Public Shared Function BuildPlainReviewText(document As DocumentRecord) As String
        If document Is Nothing Then Return ""
        Dim sb As New StringBuilder()
        sb.AppendLine("العنوان: " & Safe(document.Title))
        sb.AppendLine("رقم الأرشيف: " & Safe(document.ArchiveNumber))
        sb.AppendLine("التصنيف: " & Safe(document.GeneralCategory) & " / " & Safe(document.SubCategory))
        sb.AppendLine("الجهة الصادرة: " & Safe(document.SourceEntity))
        sb.AppendLine("الجهة المستقبلة: " & Safe(document.DestinationEntity))
        sb.AppendLine("السرية/الأولوية: " & Safe(document.Confidentiality) & " / " & Safe(document.Priority))
        sb.AppendLine(New String("-"c, 50))
        sb.AppendLine(BuildEntitySummary(document))
        sb.AppendLine(New String("-"c, 50))
        sb.AppendLine(NormalizeOcrText(document.TextContent))
        Return sb.ToString()
    End Function

    Private Shared Sub ApplyRegex(rtb As RichTextBox, pattern As String, foreColor As Color, backColor As Color, bold As Boolean)
        Try
            For Each m As Match In Regex.Matches(rtb.Text, pattern, RegexOptions.IgnoreCase Or RegexOptions.Multiline)
                Highlight(rtb, m.Index, m.Length, foreColor, backColor, bold)
            Next
        Catch
        End Try
    End Sub

    Private Shared Sub ApplyList(rtb As RichTextBox, values As List(Of String), foreColor As Color, backColor As Color, bold As Boolean)
        If values Is Nothing Then Return
        For Each value As String In values
            If String.IsNullOrWhiteSpace(value) OrElse value.Trim().Length < 3 Then Continue For
            Dim startAt As Integer = 0
            Dim text As String = rtb.Text
            Do
                Dim idx As Integer = text.IndexOf(value.Trim(), startAt, StringComparison.OrdinalIgnoreCase)
                If idx < 0 Then Exit Do
                Highlight(rtb, idx, value.Trim().Length, foreColor, backColor, bold)
                startAt = idx + value.Trim().Length
            Loop While startAt < text.Length
        Next
    End Sub

    Private Shared Sub Highlight(rtb As RichTextBox, index As Integer, length As Integer, foreColor As Color, backColor As Color, bold As Boolean)
        If index < 0 OrElse length <= 0 OrElse index + length > rtb.TextLength Then Return
        rtb.Select(index, length)
        rtb.SelectionColor = foreColor
        rtb.SelectionBackColor = backColor
        rtb.SelectionFont = New Font("Tahoma", 11.0F, If(bold, FontStyle.Bold, FontStyle.Regular))
    End Sub

    Private Shared Function SplitValues(value As String) As List(Of String)
        Dim result As New List(Of String)()
        If String.IsNullOrWhiteSpace(value) Then Return result
        Dim parts() As String = value.Replace(vbCr, "|").Replace(vbLf, "|").Split(New Char() {"|"c, ","c, ";"c, "،"c}, StringSplitOptions.RemoveEmptyEntries)
        For Each p In parts
            Dim v As String = p.Trim()
            If v.Length >= 3 AndAlso Not result.Contains(v) Then result.Add(v)
        Next
        Return result
    End Function

    Private Shared Sub AppendLine(sb As StringBuilder, label As String, value As String)
        If String.IsNullOrWhiteSpace(value) Then Return
        sb.AppendLine(label & ": " & value.Trim())
    End Sub

    Private Shared Function JoinNonEmpty(ParamArray values() As String) As String
        Dim parts As New List(Of String)()
        For Each v In values
            If Not String.IsNullOrWhiteSpace(v) Then parts.Add(v.Trim())
        Next
        Return String.Join(" | ", parts.ToArray())
    End Function

    Private Shared Function Safe(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "-"
        Return value.Trim()
    End Function
End Class
