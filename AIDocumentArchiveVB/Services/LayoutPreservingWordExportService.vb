Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions

Public Class LayoutPreservingWordExportService
    Private ReadOnly _settings As AppSettings

    Public Sub New(settings As AppSettings)
        _settings = settings
    End Sub

    Public Function ConvertToWord(inputPath As String, outputPath As String, options As LayoutWordConversionOptions, progress As ProgressDialogForm) As LayoutWordConversionResult
        If String.IsNullOrWhiteSpace(inputPath) OrElse Not File.Exists(inputPath) Then Throw New FileNotFoundException("ملف الإدخال غير موجود.", inputPath)
        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("مسار ملف Word غير صحيح.")
        If options Is Nothing Then options = New LayoutWordConversionOptions()
        NormalizeOptions(options)

        Dim sw = Stopwatch.StartNew()
        Dim result As New LayoutWordConversionResult With {.OutputPath = outputPath}
        Dim tempFolder = Path.Combine(Path.GetTempPath(), "aidoc_layout_word_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(tempFolder)

        Try
            progress.Report("تجهيز الصفحات...", 5)
            Dim pageImages As List(Of String) = PreparePageImages(inputPath, tempFolder, options, result)
            result.PageCount = pageImages.Count
            If pageImages.Count = 0 Then Throw New ApplicationException("لم يتم العثور على صفحات قابلة للتحويل. تأكد من مسارات Poppler أو Ghostscript للـ PDF.")

            Dim pageTexts As New List(Of String)()
            If options.Mode <> "VisualOnly" OrElse options.AddEditableTextLayer Then
                Dim ocr As New OcrService(_settings)
                For i As Integer = 0 To pageImages.Count - 1
                    If progress IsNot Nothing AndAlso progress.IsCancelRequested Then Throw New OperationCanceledException("تم إلغاء العملية بطلب المستخدم.")
                    progress.ReportStep(i + 1, pageImages.Count, "استخراج النص من الصفحة " & (i + 1).ToString())
                    Dim pageText As String = ""
                    Try
                        pageText = ocr.ExtractTextProfessional(pageImages(i))
                    Catch ex As Exception
                        result.Warnings.Add("تعذر OCR للصفحة " & (i + 1).ToString() & ": " & ex.Message)
                    End Try
                    pageTexts.Add(If(pageText, ""))
                Next
            End If

            result.ExtractedText = String.Join(Environment.NewLine & Environment.NewLine, pageTexts.ToArray()).Trim()
            progress.Report("إنشاء ملف Word مع الحفاظ على الشكل...", 88)
            BuildDocx(outputPath, pageImages, pageTexts, options, result)
            sw.Stop()
            result.DurationSeconds = sw.Elapsed.TotalSeconds
            progress.Report("تم إنشاء ملف Word بنجاح.", 100)
            Return result
        Finally
            Try
                Directory.Delete(tempFolder, True)
            Catch
            End Try
        End Try
    End Function

    Private Sub NormalizeOptions(options As LayoutWordConversionOptions)
        If options.Dpi < 150 Then options.Dpi = 150
        If options.Dpi > 600 Then options.Dpi = 600
        If options.FirstPage < 1 Then options.FirstPage = 1
        If options.MaxPages < 1 Then options.MaxPages = 50
        If options.MaxPages > 500 Then options.MaxPages = 500
        If options.LastPage > 0 AndAlso options.LastPage < options.FirstPage Then options.LastPage = options.FirstPage
        If String.IsNullOrWhiteSpace(options.Mode) Then options.Mode = "Hybrid"
    End Sub

    Private Function PreparePageImages(inputPath As String, tempFolder As String, options As LayoutWordConversionOptions, result As LayoutWordConversionResult) As List(Of String)
        Dim ext = Path.GetExtension(inputPath).ToLowerInvariant()
        If ext = ".pdf" Then Return RenderPdfToImages(inputPath, tempFolder, options, result)

        If IsSupportedImage(ext) Then
            Dim outImage = Path.Combine(tempFolder, "page-0001.png")
            Try
                Using img As Image = Image.FromFile(inputPath)
                    img.Save(outImage, System.Drawing.Imaging.ImageFormat.Png)
                End Using
            Catch
                File.Copy(inputPath, outImage, True)
            End Try
            Return New List(Of String) From {outImage}
        End If

        Throw New NotSupportedException("هذه الشاشة تدعم PDF والصور فقط: PNG/JPG/BMP/TIF.")
    End Function

    Private Function RenderPdfToImages(pdfPath As String, tempFolder As String, options As LayoutWordConversionOptions, result As LayoutWordConversionResult) As List(Of String)
        Dim images As New List(Of String)()
        Dim pdftoppm = GetPdfToPpmPath()
        If HasExecutable(pdftoppm) Then
            Try
                Dim prefix = Path.Combine(tempFolder, "page")
                Dim args As New StringBuilder()
                args.Append("-r " & options.Dpi.ToString() & " -png ")
                If options.FirstPage > 1 Then args.Append("-f " & options.FirstPage.ToString() & " ")
                If options.LastPage > 0 Then args.Append("-l " & options.LastPage.ToString() & " ")
                args.Append(Quote(pdfPath) & " " & Quote(prefix))
                RunProcess(pdftoppm, args.ToString(), 300000)
                images = Directory.GetFiles(tempFolder, "page*.png").OrderBy(Function(p) NaturalSortKey(Path.GetFileName(p))).ToList()
                If options.MaxPages > 0 AndAlso images.Count > options.MaxPages Then images = images.Take(options.MaxPages).ToList()
                If images.Count > 0 Then Return images
            Catch ex As Exception
                result.Warnings.Add("فشل تحويل PDF عبر Poppler: " & ex.Message)
            End Try
        End If

        Dim gs = GetGhostscriptPath()
        If HasExecutable(gs) Then
            Try
                Dim outMask = Path.Combine(tempFolder, "page-%04d.png")
                Dim firstLast As String = ""
                If options.FirstPage > 1 Then firstLast &= " -dFirstPage=" & options.FirstPage.ToString()
                If options.LastPage > 0 Then firstLast &= " -dLastPage=" & options.LastPage.ToString()
                Dim args = "-dSAFER -dBATCH -dNOPAUSE -sDEVICE=png16m -dTextAlphaBits=4 -dGraphicsAlphaBits=4 -r" & options.Dpi.ToString() & firstLast & " -sOutputFile=" & Quote(outMask) & " " & Quote(pdfPath)
                RunProcess(gs, args, 300000)
                images = Directory.GetFiles(tempFolder, "page-*.png").OrderBy(Function(p) NaturalSortKey(Path.GetFileName(p))).ToList()
                If options.MaxPages > 0 AndAlso images.Count > options.MaxPages Then images = images.Take(options.MaxPages).ToList()
                If images.Count > 0 Then Return images
            Catch ex As Exception
                result.Warnings.Add("فشل تحويل PDF عبر Ghostscript: " & ex.Message)
            End Try
        End If

        Return images
    End Function

    Private Sub BuildDocx(outputPath As String, pageImages As List(Of String), pageTexts As List(Of String), options As LayoutWordConversionOptions, result As LayoutWordConversionResult)
        If File.Exists(outputPath) Then File.Delete(outputPath)
        Using archive As ZipArchive = ZipFile.Open(outputPath, ZipArchiveMode.Create)
            WriteTextEntry(archive, "[Content_Types].xml", ContentTypesXml(pageImages))
            WriteTextEntry(archive, "_rels/.rels", RootRelsXml())
            WriteTextEntry(archive, "word/_rels/document.xml.rels", DocumentRelsXml(pageImages))
            WriteTextEntry(archive, "word/styles.xml", StylesXml())

            For i As Integer = 0 To pageImages.Count - 1
                Dim ext = Path.GetExtension(pageImages(i)).ToLowerInvariant()
                If ext <> ".jpg" AndAlso ext <> ".jpeg" AndAlso ext <> ".png" Then ext = ".png"
                AddFileEntry(archive, pageImages(i), "word/media/page" & (i + 1).ToString("0000") & ext)
            Next

            WriteTextEntry(archive, "word/document.xml", DocumentXml(pageImages, pageTexts, options))
        End Using
    End Sub

    Private Function DocumentXml(pageImages As List(Of String), pageTexts As List(Of String), options As LayoutWordConversionOptions) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>")
        sb.AppendLine("<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"" xmlns:wp=""http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"" xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" xmlns:pic=""http://schemas.openxmlformats.org/drawingml/2006/picture"">")
        sb.AppendLine("<w:body>")
        AddTitle(sb, "تحويل OCR إلى Word مع الحفاظ على الشكل")

        For i As Integer = 0 To pageImages.Count - 1
            Dim text As String = If(i < pageTexts.Count, pageTexts(i), "")
            If options.Mode <> "EditableOnly" Then
                AddImageParagraph(sb, i + 1, pageImages(i))
            End If
            If options.Mode <> "VisualOnly" AndAlso Not String.IsNullOrWhiteSpace(text) Then
                If options.KeepPageHeaders Then AddSmallHeading(sb, "النص القابل للتحرير - صفحة " & (i + 1).ToString())
                AddFormattedText(sb, text, options.HighlightDetectedEntities)
            End If
            If i < pageImages.Count - 1 Then AddPageBreak(sb)
        Next

        sb.AppendLine("<w:sectPr><w:pgSz w:w=""11906"" w:h=""16838""/><w:pgMar w:top=""360"" w:right=""360"" w:bottom=""360"" w:left=""360"" w:header=""360"" w:footer=""360"" w:gutter=""0""/></w:sectPr>")
        sb.AppendLine("</w:body></w:document>")
        Return sb.ToString()
    End Function

    Private Sub AddTitle(sb As StringBuilder, text As String)
        sb.AppendLine("<w:p><w:pPr><w:bidi/><w:jc w:val=""center""/></w:pPr><w:r><w:rPr><w:rFonts w:ascii=""Tahoma"" w:hAnsi=""Tahoma"" w:cs=""Tahoma""/><w:rtl/><w:b/><w:bCs/><w:sz w:val=""28""/><w:szCs w:val=""28""/></w:rPr><w:t>" & X(text) & "</w:t></w:r></w:p>")
    End Sub

    Private Sub AddSmallHeading(sb As StringBuilder, text As String)
        sb.AppendLine("<w:p><w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:before=""180"" w:after=""80""/></w:pPr><w:r><w:rPr><w:rFonts w:ascii=""Tahoma"" w:hAnsi=""Tahoma"" w:cs=""Tahoma""/><w:rtl/><w:b/><w:bCs/><w:color w:val=""1F5E55""/><w:sz w:val=""22""/><w:szCs w:val=""22""/></w:rPr><w:t>" & X(text) & "</w:t></w:r></w:p>")
    End Sub

    Private Sub AddImageParagraph(sb As StringBuilder, relIndex As Integer, imagePath As String)
        Dim cx As Long = 7100000
        Dim cy As Long = 10000000
        Try
            Using img As Image = Image.FromFile(imagePath)
                Dim ratio As Double = img.Height / Math.Max(1.0R, CDbl(img.Width))
                cy = CLng(cx * ratio)
                If cy > 10000000 Then
                    cy = 10000000
                    cx = CLng(cy / ratio)
                End If
            End Using
        Catch
        End Try

        sb.AppendLine("<w:p><w:pPr><w:jc w:val=""center""/></w:pPr><w:r><w:drawing><wp:inline distT=""0"" distB=""0"" distL=""0"" distR=""0""><wp:extent cx=""" & cx.ToString() & """ cy=""" & cy.ToString() & """/><wp:effectExtent l=""0"" t=""0"" r=""0"" b=""0""/><wp:docPr id=""" & relIndex.ToString() & """ name=""Page " & relIndex.ToString() & """/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=""1""/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri=""http://schemas.openxmlformats.org/drawingml/2006/picture""><pic:pic><pic:nvPicPr><pic:cNvPr id=""0"" name=""page" & relIndex.ToString() & """/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=""rId" & relIndex.ToString() & """/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=""0"" y=""0""/><a:ext cx=""" & cx.ToString() & """ cy=""" & cy.ToString() & """/></a:xfrm><a:prstGeom prst=""rect""><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>")
    End Sub

    Private Sub AddFormattedText(sb As StringBuilder, text As String, highlight As Boolean)
        Dim lines() As String = text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(ControlChars.Lf)
        For Each raw In lines
            Dim line = raw.TrimEnd()
            If String.IsNullOrWhiteSpace(line) Then
                sb.AppendLine("<w:p/>")
            ElseIf LooksLikeTableLine(line) Then
                AddTableLine(sb, line)
            Else
                AddHighlightedParagraph(sb, line, highlight)
            End If
        Next
    End Sub

    Private Function LooksLikeTableLine(line As String) As Boolean
        If String.IsNullOrWhiteSpace(line) Then Return False
        If line.Contains("|") Then Return True
        If Regex.Matches(line, "\s{2,}").Count >= 2 Then Return True
        Return False
    End Function

    Private Sub AddTableLine(sb As StringBuilder, line As String)
        Dim cells As String()
        If line.Contains("|") Then
            cells = line.Split("|"c)
        Else
            cells = Regex.Split(line.Trim(), "\s{2,}")
        End If
        sb.AppendLine("<w:tbl><w:tblPr><w:bidiVisual/><w:tblBorders><w:top w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""B8C7C3""/><w:left w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""B8C7C3""/><w:bottom w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""B8C7C3""/><w:right w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""B8C7C3""/><w:insideH w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""B8C7C3""/><w:insideV w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""B8C7C3""/></w:tblBorders></w:tblPr><w:tr>")
        For Each c In cells
            sb.Append("<w:tc><w:p><w:pPr><w:bidi/><w:jc w:val=""right""/></w:pPr><w:r><w:rPr><w:rFonts w:ascii=""Tahoma"" w:hAnsi=""Tahoma"" w:cs=""Tahoma""/><w:rtl/><w:sz w:val=""20""/><w:szCs w:val=""20""/></w:rPr><w:t xml:space=""preserve"">" & X(c.Trim()) & "</w:t></w:r></w:p></w:tc>")
        Next
        sb.AppendLine("</w:tr></w:tbl>")
    End Sub

    Private Sub AddHighlightedParagraph(sb As StringBuilder, line As String, highlight As Boolean)
        sb.Append("<w:p><w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:after=""60""/></w:pPr>")
        If Not highlight Then
            AddRun(sb, line, "000000", "")
        Else
            Dim pos As Integer = 0
            For Each m As Match In EntityRegex().Matches(line)
                If m.Index > pos Then AddRun(sb, line.Substring(pos, m.Index - pos), "000000", "")
                Dim color As String = "1F5E55"
                Dim fill As String = "FFF2CC"
                If Regex.IsMatch(m.Value, "\d") Then fill = "DDEBFF"
                If Regex.IsMatch(m.Value, "(ريال|ر\.س|SAR)") Then fill = "E2F0D9"
                AddRun(sb, m.Value, color, fill)
                pos = m.Index + m.Length
            Next
            If pos < line.Length Then AddRun(sb, line.Substring(pos), "000000", "")
        End If
        sb.AppendLine("</w:p>")
    End Sub

    Private Function EntityRegex() As Regex
        Return New Regex("(\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b|\b\d{4}[/-]\d{1,2}[/-]\d{1,2}\b|\b\d{10}\b|\b\d+(?:[,.]\d+)?\s*(?:ريال|ر\.س|SAR)?\b|[\w\.-]+@[\w\.-]+\.[A-Za-z]{2,}|(?:وزارة|إدارة|شركة|مؤسسة|مدرسة|جامعة|بلدية|محافظة)\s+[^\s،.]{2,}(?:\s+[^\s،.]{2,}){0,3})", RegexOptions.IgnoreCase)
    End Function

    Private Sub AddRun(sb As StringBuilder, text As String, color As String, fill As String)
        If String.IsNullOrEmpty(text) Then Return
        sb.Append("<w:r><w:rPr><w:rFonts w:ascii=""Tahoma"" w:hAnsi=""Tahoma"" w:cs=""Tahoma""/><w:rtl/><w:sz w:val=""22""/><w:szCs w:val=""22""/>")
        If Not String.IsNullOrWhiteSpace(color) Then sb.Append("<w:color w:val=""" & color & """/>")
        If Not String.IsNullOrWhiteSpace(fill) Then sb.Append("<w:shd w:val=""clear"" w:color=""auto"" w:fill=""" & fill & """/>")
        sb.Append("</w:rPr><w:t xml:space=""preserve"">" & X(text) & "</w:t></w:r>")
    End Sub

    Private Sub AddPageBreak(sb As StringBuilder)
        sb.AppendLine("<w:p><w:r><w:br w:type=""page""/></w:r></w:p>")
    End Sub

    Private Function ContentTypesXml(pageImages As List(Of String)) As String
        Dim hasPng = pageImages.Any(Function(p) Path.GetExtension(p).ToLowerInvariant() = ".png")
        Dim hasJpg = pageImages.Any(Function(p) Path.GetExtension(p).ToLowerInvariant() = ".jpg" OrElse Path.GetExtension(p).ToLowerInvariant() = ".jpeg")
        Dim sb As New StringBuilder()
        sb.Append("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/><Default Extension=""xml"" ContentType=""application/xml""/>")
        If hasPng Then sb.Append("<Default Extension=""png"" ContentType=""image/png""/>")
        If hasJpg Then sb.Append("<Default Extension=""jpg"" ContentType=""image/jpeg""/><Default Extension=""jpeg"" ContentType=""image/jpeg""/>")
        sb.Append("<Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/><Override PartName=""/word/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml""/></Types>")
        Return sb.ToString()
    End Function

    Private Function RootRelsXml() As String
        Return "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/></Relationships>"
    End Function

    Private Function DocumentRelsXml(pageImages As List(Of String)) As String
        Dim sb As New StringBuilder()
        sb.Append("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">")
        For i As Integer = 0 To pageImages.Count - 1
            Dim ext = Path.GetExtension(pageImages(i)).ToLowerInvariant()
            If ext <> ".jpg" AndAlso ext <> ".jpeg" AndAlso ext <> ".png" Then ext = ".png"
            sb.Append("<Relationship Id=""rId" & (i + 1).ToString() & """ Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"" Target=""media/page" & (i + 1).ToString("0000") & ext & """/>")
        Next
        sb.Append("</Relationships>")
        Return sb.ToString()
    End Function

    Private Function StylesXml() As String
        Return "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><w:styles xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""><w:style w:type=""paragraph"" w:default=""1"" w:styleId=""Normal""><w:name w:val=""Normal""/><w:pPr><w:bidi/><w:jc w:val=""right""/></w:pPr><w:rPr><w:rFonts w:ascii=""Tahoma"" w:hAnsi=""Tahoma"" w:cs=""Tahoma""/><w:rtl/><w:sz w:val=""22""/><w:szCs w:val=""22""/></w:rPr></w:style></w:styles>"
    End Function

    Private Sub WriteTextEntry(archive As ZipArchive, name As String, content As String)
        Dim entry = archive.CreateEntry(name, CompressionLevel.Optimal)
        Using writer As New StreamWriter(entry.Open(), New UTF8Encoding(False))
            writer.Write(content)
        End Using
    End Sub

    Private Sub AddFileEntry(archive As ZipArchive, filePath As String, entryName As String)
        Dim entry = archive.CreateEntry(entryName, CompressionLevel.Optimal)
        Using input = File.OpenRead(filePath)
            Using output = entry.Open()
                input.CopyTo(output)
            End Using
        End Using
    End Sub

    Private Function GetPdfToPpmPath() As String
        If _settings IsNot Nothing AndAlso HasExecutable(_settings.PopplerPdftoppmPath) Then Return _settings.PopplerPdftoppmPath
        Return AutoFind(New String() {"C:\Program Files\poppler\Library\bin\pdftoppm.exe", "C:\Program Files\poppler\bin\pdftoppm.exe", "C:\poppler\Library\bin\pdftoppm.exe", "C:\poppler\bin\pdftoppm.exe"}, "pdftoppm.exe")
    End Function

    Private Function GetGhostscriptPath() As String
        If _settings IsNot Nothing AndAlso HasExecutable(_settings.GhostscriptExePath) Then Return _settings.GhostscriptExePath
        Dim candidates As New List(Of String) From {"C:\Program Files\gs\gs10.05.1\bin\gswin64c.exe", "C:\Program Files\gs\gs10.04.0\bin\gswin64c.exe", "C:\Program Files\gs\gs10.03.1\bin\gswin64c.exe"}
        Try
            If Directory.Exists("C:\Program Files\gs") Then
                For Each p In Directory.GetFiles("C:\Program Files\gs", "gswin64c.exe", SearchOption.AllDirectories)
                    candidates.Add(p)
                Next
            End If
        Catch
        End Try
        Return AutoFind(candidates.ToArray(), "gswin64c.exe")
    End Function

    Private Function AutoFind(candidates() As String, exeName As String) As String
        For Each c In candidates
            If HasExecutable(c) Then Return c
        Next
        Try
            Dim pathEnv = Environment.GetEnvironmentVariable("PATH")
            If Not String.IsNullOrWhiteSpace(pathEnv) Then
                For Each folder In pathEnv.Split(";"c)
                    Dim p = Path.Combine(folder.Trim(), exeName)
                    If HasExecutable(p) Then Return p
                Next
            End If
        Catch
        End Try
        Return ""
    End Function

    Private Function HasExecutable(path As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(path) AndAlso File.Exists(path)
    End Function

    Private Sub RunProcess(exePath As String, arguments As String, timeoutMs As Integer)
        Dim psi As New ProcessStartInfo() With {.FileName = exePath, .Arguments = arguments, .UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardOutput = True, .RedirectStandardError = True}
        psi.StandardOutputEncoding = Encoding.UTF8
        psi.StandardErrorEncoding = Encoding.UTF8
        Using p = Process.Start(psi)
            Dim output = p.StandardOutput.ReadToEnd()
            Dim err = p.StandardError.ReadToEnd()
            If Not p.WaitForExit(timeoutMs) Then
                Try
                    p.Kill()
                Catch
                End Try
                Throw New TimeoutException("انتهت مهلة تشغيل أداة تحويل PDF.")
            End If
            If p.ExitCode <> 0 Then Throw New ApplicationException(If(String.IsNullOrWhiteSpace(err), output, err).Trim())
        End Using
    End Sub

    Private Function NaturalSortKey(name As String) As String
        If name Is Nothing Then Return ""
        Return Regex.Replace(name, "\d+", Function(m) m.Value.PadLeft(10, "0"c))
    End Function

    Private Function IsSupportedImage(ext As String) As Boolean
        Return ext = ".png" OrElse ext = ".jpg" OrElse ext = ".jpeg" OrElse ext = ".bmp" OrElse ext = ".tif" OrElse ext = ".tiff"
    End Function

    Private Function Quote(value As String) As String
        Return """" & value & """"
    End Function

    Private Function X(value As String) As String
        If value Is Nothing Then Return ""
        Return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;").Replace("'", "&apos;")
    End Function
End Class
