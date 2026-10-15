Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text

Public Class PdfOcrResult
    Public Property FilePath As String
    Public Property DirectText As String = ""
    Public Property OcrText As String = ""
    Public Property PageResults As New List(Of PdfOcrPageResult)()
    Public Property RenderTool As String = ""
    Public Property TextTool As String = ""
    Public Property Dpi As Integer
    Public Property Warnings As New List(Of String)()

    Public ReadOnly Property RenderedPageCount As Integer
        Get
            If PageResults Is Nothing Then Return 0
            Return PageResults.Count
        End Get
    End Property

    Public Function BestText() As String
        Dim direct As String = If(DirectText, "").Trim()
        Dim ocr As String = If(OcrText, "").Trim()
        If direct = "" Then Return ocr
        If ocr = "" Then Return direct
        If direct.Length > ocr.Length * 2 Then Return direct
        If ocr.Length > direct.Length * 2 Then Return ocr
        If direct.Contains(ocr) Then Return direct
        If ocr.Contains(direct) Then Return ocr
        Return direct & Environment.NewLine & Environment.NewLine & ocr
    End Function

    Public Function EngineSummary() As String
        Dim parts As New List(Of String)()
        If Not String.IsNullOrWhiteSpace(TextTool) Then parts.Add(TextTool)
        If Not String.IsNullOrWhiteSpace(RenderTool) Then parts.Add(RenderTool & " + Tesseract PDF-OCR")
        If parts.Count = 0 Then Return "PDF OCR غير مكتمل"
        Return String.Join(" + ", parts.ToArray())
    End Function

    Public Function BuildReport() As String
        Dim sb As New StringBuilder()
        sb.AppendLine("تقرير OCR لملف PDF")
        sb.AppendLine("الملف: " & If(FilePath, ""))
        sb.AppendLine("الدقة: " & Dpi.ToString() & " DPI")
        sb.AppendLine("استخراج نص مباشر: " & If(String.IsNullOrWhiteSpace(DirectText), "لا", "نعم"))
        sb.AppendLine("محرك النص: " & If(String.IsNullOrWhiteSpace(TextTool), "غير متاح", TextTool))
        sb.AppendLine("محرك تحويل الصفحات: " & If(String.IsNullOrWhiteSpace(RenderTool), "غير متاح", RenderTool))
        sb.AppendLine("عدد الصفحات التي تم OCR لها: " & RenderedPageCount.ToString())
        If Warnings IsNot Nothing AndAlso Warnings.Count > 0 Then
            sb.AppendLine()
            sb.AppendLine("تنبيهات:")
            For Each w In Warnings
                sb.AppendLine("- " & w)
            Next
        End If
        If PageResults IsNot Nothing AndAlso PageResults.Count > 0 Then
            sb.AppendLine()
            sb.AppendLine("تفصيل الصفحات:")
            For Each p In PageResults
                sb.AppendLine("- صفحة " & p.PageNumber.ToString() & ": " & If(String.IsNullOrWhiteSpace(p.Text), "لا يوجد نص", p.Text.Length.ToString() & " حرف") & " / " & p.DurationMs.ToString() & "ms" & If(String.IsNullOrWhiteSpace(p.Warning), "", " / " & p.Warning))
            Next
        End If
        Return sb.ToString()
    End Function
End Class
