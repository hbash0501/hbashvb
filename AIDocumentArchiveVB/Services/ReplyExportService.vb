Imports System
Imports System.IO
Imports System.IO.Compression
Imports System.Text

Public Class ReplyExportService
    Public Shared Sub ExportReplyAsDocx(document As DocumentRecord, replyText As String, outputPath As String)
        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("مسار الحفظ غير صحيح.")
        If File.Exists(outputPath) Then File.Delete(outputPath)

        Using archive As ZipArchive = ZipFile.Open(outputPath, ZipArchiveMode.Create)
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml())
            WriteEntry(archive, "_rels/.rels", RootRelsXml())
            WriteEntry(archive, "word/_rels/document.xml.rels", DocumentRelsXml())
            WriteEntry(archive, "word/styles.xml", StylesXml())
            WriteEntry(archive, "word/document.xml", DocumentXml(document, replyText))
        End Using
    End Sub

    Public Shared Sub ExportReplyAsHtml(document As DocumentRecord, replyText As String, outputPath As String)
        Dim sb As New StringBuilder()
        sb.AppendLine("<!doctype html><html lang='ar' dir='rtl'><head><meta charset='utf-8'>")
        sb.AppendLine("<style>body{font-family:Tahoma,Arial;background:#f6f8f7;color:#1f2d2a;padding:34px} .page{background:white;max-width:900px;margin:auto;padding:40px;border:1px solid #dde5e1;box-shadow:0 8px 30px rgba(0,0,0,.08)} h1{color:#123b36} .meta{background:#f0f5f3;padding:14px;border-right:6px solid #c69f4c;margin:16px 0}.reply{white-space:pre-wrap;line-height:2;font-size:16px}</style>")
        sb.AppendLine("</head><body><div class='page'>")
        sb.AppendLine("<h1>مسودة رد على خطاب</h1>")
        sb.AppendLine("<div class='meta'>")
        If document IsNot Nothing Then
            sb.AppendLine("<b>المستند:</b> " & H(document.Title) & "<br>")
            sb.AppendLine("<b>رقم الأرشيف:</b> " & H(document.ArchiveNumber) & "<br>")
            sb.AppendLine("<b>الجهة:</b> " & H(document.SourceEntity) & " ← " & H(document.DestinationEntity))
        End If
        sb.AppendLine("</div><div class='reply'>" & H(If(replyText, "")) & "</div>")
        sb.AppendLine("</div></body></html>")
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8)
    End Sub

    Private Shared Function DocumentXml(document As DocumentRecord, replyText As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>")
        sb.AppendLine("<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">")
        sb.AppendLine("<w:body>")
        AddParagraph(sb, "مسودة رد على خطاب", True, 32)
        If document IsNot Nothing Then
            AddParagraph(sb, "المستند: " & Safe(document.Title), False, 24)
            AddParagraph(sb, "رقم الأرشيف: " & Safe(document.ArchiveNumber) & "   |   رقم المرجع: " & Safe(document.ReferenceNumber), False, 22)
            AddParagraph(sb, "الجهة الصادرة: " & Safe(document.SourceEntity) & "   |   الجهة المستقبلة: " & Safe(document.DestinationEntity), False, 22)
            AddParagraph(sb, "", False, 22)
        End If
        Dim lines() As String = If(replyText, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(ControlChars.Lf)
        For Each line As String In lines
            AddParagraph(sb, line, False, 24)
        Next
        sb.AppendLine("<w:sectPr><w:pgSz w:w=""11906"" w:h=""16838""/><w:pgMar w:top=""1440"" w:right=""1440"" w:bottom=""1440"" w:left=""1440"" w:header=""708"" w:footer=""708"" w:gutter=""0""/></w:sectPr>")
        sb.AppendLine("</w:body></w:document>")
        Return sb.ToString()
    End Function

    Private Shared Sub AddParagraph(sb As StringBuilder, text As String, bold As Boolean, sizeHalfPoints As Integer)
        sb.Append("<w:p><w:pPr><w:bidi/><w:jc w:val=""right""/></w:pPr><w:r><w:rPr>")
        sb.Append("<w:rFonts w:ascii=""Tahoma"" w:hAnsi=""Tahoma"" w:cs=""Tahoma""/><w:rtl/><w:sz w:val=""" & sizeHalfPoints.ToString() & """/><w:szCs w:val=""" & sizeHalfPoints.ToString() & """/>")
        If bold Then sb.Append("<w:b/><w:bCs/>")
        sb.Append("</w:rPr><w:t xml:space=""preserve"">")
        sb.Append(X(text))
        sb.AppendLine("</w:t></w:r></w:p>")
    End Sub

    Private Shared Sub WriteEntry(archive As ZipArchive, name As String, content As String)
        Dim entry As ZipArchiveEntry = archive.CreateEntry(name, CompressionLevel.Optimal)
        Using writer As New StreamWriter(entry.Open(), New UTF8Encoding(False))
            writer.Write(content)
        End Using
    End Sub

    Private Shared Function ContentTypesXml() As String
        Return "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/><Default Extension=""xml"" ContentType=""application/xml""/><Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/><Override PartName=""/word/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml""/></Types>"
    End Function

    Private Shared Function RootRelsXml() As String
        Return "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/></Relationships>"
    End Function

    Private Shared Function DocumentRelsXml() As String
        Return "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""/>"
    End Function

    Private Shared Function StylesXml() As String
        Return "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><w:styles xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""><w:style w:type=""paragraph"" w:default=""1"" w:styleId=""Normal""><w:name w:val=""Normal""/><w:pPr><w:bidi/><w:jc w:val=""right""/></w:pPr><w:rPr><w:rFonts w:ascii=""Tahoma"" w:hAnsi=""Tahoma"" w:cs=""Tahoma""/><w:rtl/><w:sz w:val=""24""/><w:szCs w:val=""24""/></w:rPr></w:style></w:styles>"
    End Function

    Private Shared Function X(value As String) As String
        If value Is Nothing Then Return ""
        Return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;").Replace("'", "&apos;")
    End Function

    Private Shared Function H(value As String) As String
        Return X(value).Replace(vbCrLf, "<br>").Replace(vbLf, "<br>")
    End Function

    Private Shared Function Safe(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "-"
        Return value.Trim()
    End Function
End Class
