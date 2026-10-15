Imports System
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

Public Class FileTextExtractor
    Public Shared Function ExtractQuickText(filePath As String) As String
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return ""
        Dim ext = Path.GetExtension(filePath).ToLowerInvariant()
        Try
            Select Case ext
                Case ".txt", ".csv", ".log"
                    Return File.ReadAllText(filePath, Encoding.UTF8)
                Case ".docx"
                    Return ExtractDocx(filePath)
                Case ".xlsx"
                    Return ExtractXlsxSharedStrings(filePath)
                Case ".pdf"
                    Return ExtractBasicPdfText(filePath)
                Case Else
                    Return ""
            End Select
        Catch
            Return ""
        End Try
    End Function

    Private Shared Function ExtractDocx(filePath As String) As String
        Using zip = ZipFile.OpenRead(filePath)
            Dim entry = zip.GetEntry("word/document.xml")
            If entry Is Nothing Then Return ""
            Using sr As New StreamReader(entry.Open(), Encoding.UTF8)
                Dim xml = sr.ReadToEnd()
                xml = Regex.Replace(xml, "<w:tab\s*/>", " ")
                xml = Regex.Replace(xml, "</w:p>", Environment.NewLine)
                Return CleanXmlText(xml)
            End Using
        End Using
    End Function

    Private Shared Function ExtractXlsxSharedStrings(filePath As String) As String
        Using zip = ZipFile.OpenRead(filePath)
            Dim entry = zip.GetEntry("xl/sharedStrings.xml")
            If entry Is Nothing Then Return ""
            Using sr As New StreamReader(entry.Open(), Encoding.UTF8)
                Return CleanXmlText(sr.ReadToEnd())
            End Using
        End Using
    End Function

    Private Shared Function ExtractBasicPdfText(filePath As String) As String
        'استخراج بسيط جداً من PDF النصي فقط، أما ملفات PDF المصورة فتحتاج OCR.
        Dim bytes = File.ReadAllBytes(filePath)
        Dim raw = Encoding.Default.GetString(bytes)
        Dim matches = Regex.Matches(raw, "\(([^\)]{3,})\)\s*Tj")
        Dim sb As New StringBuilder()
        For Each m As Match In matches
            sb.AppendLine(m.Groups(1).Value)
            If sb.Length > 6000 Then Exit For
        Next
        Return Regex.Replace(sb.ToString(), "\s+", " ").Trim()
    End Function

    Private Shared Function CleanXmlText(xml As String) As String
        Dim noTags = Regex.Replace(xml, "<[^>]+>", " ")
        noTags = System.Net.WebUtility.HtmlDecode(noTags)
        Return Regex.Replace(noTags, "\s+", " ").Trim()
    End Function
End Class
