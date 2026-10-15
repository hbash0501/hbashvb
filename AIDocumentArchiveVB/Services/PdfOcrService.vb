Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Text.RegularExpressions

Public Class PdfOcrService
    Private ReadOnly _settings As AppSettings
    Private _token As CancellationToken
    Private _progress As Action(Of String, Integer)

    Public Sub New(settings As AppSettings)
        _settings = settings
    End Sub

    Public Function Extract(pdfPath As String, Optional options As PdfOcrOptions = Nothing, Optional token As CancellationToken = Nothing, Optional progress As Action(Of String, Integer) = Nothing) As PdfOcrResult
        _token = token
        _progress = progress
        _token.ThrowIfCancellationRequested()
        If String.IsNullOrWhiteSpace(pdfPath) OrElse Not File.Exists(pdfPath) Then
            Throw New FileNotFoundException("ملف PDF غير موجود.", pdfPath)
        End If
        If options Is Nothing Then options = PdfOcrOptions.FromSettings(_settings)
        NormalizeOptions(options)

        Dim result As New PdfOcrResult With {
            .FilePath = pdfPath,
            .Dpi = options.Dpi
        }

        If Not options.ForceOcr AndAlso ShouldUseDirectTextFirst() Then
            Report("قراءة النص المباشر من PDF...", 10)
            TryExtractDirectPdfText(pdfPath, result)
        End If

        Dim directLooksGood As Boolean = LooksLikeUsefulText(result.DirectText)
        If directLooksGood AndAlso Not options.ForceOcr Then
            result.Warnings.Add("تم العثور على نص مباشر داخل PDF؛ تم حفظه قبل OCR. يمكن تفعيل Force OCR عند الحاجة للتأكد من الصفحات المصورة.")
        End If

        If options.ForceOcr OrElse Not directLooksGood Then
            TryRenderAndOcr(pdfPath, options, result)
        End If

        If String.IsNullOrWhiteSpace(result.BestText()) Then
            result.Warnings.Add("لم يتم استخراج نص من PDF. تأكد من تثبيت Tesseract وPoppler أو Ghostscript، أو افتح مركز PDF OCR لاختبار المسارات.")
        End If

        Report("انتهت قراءة صفحات PDF؛ جارٍ التحقق من النص...", 100)
        Return result
    End Function

    Private Sub Report(message As String, percent As Integer)
        _token.ThrowIfCancellationRequested()
        If _progress IsNot Nothing Then _progress(message, Math.Max(0, Math.Min(100, percent)))
    End Sub

    Public Function Diagnose() As String
        Dim sb As New StringBuilder()
        sb.AppendLine("تشخيص بيئة PDF OCR")
        sb.AppendLine("Tesseract: " & If(HasExecutable(GetTesseractPath()), GetTesseractPath(), "غير موجود"))
        sb.AppendLine("pdftoppm: " & If(HasExecutable(GetPdfToPpmPath()), GetPdfToPpmPath(), "غير موجود"))
        sb.AppendLine("pdftotext: " & If(HasExecutable(GetPdfToTextPath()), GetPdfToTextPath(), "غير موجود"))
        sb.AppendLine("Ghostscript: " & If(HasExecutable(GetGhostscriptPath()), GetGhostscriptPath(), "غير موجود"))
        sb.AppendLine("DPI: " & If(_settings Is Nothing, "300", Math.Max(150, Math.Min(600, _settings.PdfOcrDpi)).ToString()))
        sb.AppendLine("الحد الأقصى للصفحات: " & If(_settings Is Nothing, "100", Math.Max(1, Math.Min(500, _settings.PdfOcrMaxPages)).ToString()))
        sb.AppendLine()
        sb.AppendLine("أفضل نتيجة عملية: Poppler pdftoppm + pdftotext + Tesseract ara+eng. إذا لم يتوفر Poppler يمكن استخدام Ghostscript لتحويل الصفحات إلى صور.")
        Return sb.ToString()
    End Function

    Private Sub TryExtractDirectPdfText(pdfPath As String, result As PdfOcrResult)
        Dim pdftotext = GetPdfToTextPath()
        If HasExecutable(pdftotext) Then
            Dim outFile = Path.Combine(Path.GetTempPath(), "aidoc_pdftext_" & Guid.NewGuid().ToString("N") & ".txt")
            Try
                Dim args = "-enc UTF-8 -layout " & Quote(pdfPath) & " " & Quote(outFile)
                RunProcess(pdftotext, args, 120000)
                If File.Exists(outFile) Then
                    result.DirectText = File.ReadAllText(outFile, Encoding.UTF8).Trim()
                    result.TextTool = "Poppler pdftotext"
                End If
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                result.Warnings.Add("تعذر استخراج النص المباشر بواسطة pdftotext: " & ex.Message)
            Finally
                TryDelete(outFile)
            End Try
        End If

        If String.IsNullOrWhiteSpace(result.DirectText) Then
            Try
                result.DirectText = FileTextExtractor.ExtractQuickText(pdfPath)
                If Not String.IsNullOrWhiteSpace(result.DirectText) Then result.TextTool = "استخراج PDF داخلي بسيط"
            Catch
            End Try
        End If
    End Sub

    Private Sub TryRenderAndOcr(pdfPath As String, options As PdfOcrOptions, result As PdfOcrResult)
        If Not HasExecutable(GetTesseractPath()) Then
            result.Warnings.Add("لم يتم العثور على tesseract.exe. اضبط مساره من الإعدادات أو مركز PDF OCR.")
            Return
        End If

        Dim tempFolder = Path.Combine(Path.GetTempPath(), "aidoc_pdfocr_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(tempFolder)
        Try
            Report("تهيئة صفحات PDF المصورة...", 20)
            Dim pageImages As List(Of String) = RenderPdfToImages(pdfPath, tempFolder, options, result)
            If pageImages Is Nothing OrElse pageImages.Count = 0 Then
                result.Warnings.Add("تعذر تحويل صفحات PDF إلى صور. ثبّت Poppler أو Ghostscript ثم حدد المسار.")
                Return
            End If

            Dim pageNo As Integer = Math.Max(1, options.FirstPage)
            Dim sb As New StringBuilder()
            Dim ocr As New OcrService(_settings, _token)
            For Each imagePath In pageImages
                Report("استخراج نص الصفحة " & pageNo.ToString() & " — " & (result.PageResults.Count + 1).ToString() & " من " & pageImages.Count.ToString(), 35 + CInt(60.0 * result.PageResults.Count / pageImages.Count))
                Dim sw = Stopwatch.StartNew()
                Dim pageText As String = ""
                Dim warning As String = ""
                Try
                    pageText = ocr.ExtractTextProfessional(imagePath)
                Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                    warning = ex.Message
                    result.Warnings.Add("تعذر OCR للصفحة " & pageNo.ToString() & ": " & ex.Message)
                Finally
                    sw.Stop()
                End Try

                result.PageResults.Add(New PdfOcrPageResult With {
                    .PageNumber = pageNo,
                    .ImagePath = imagePath,
                    .Text = pageText,
                    .DurationMs = sw.ElapsedMilliseconds,
                    .Warning = warning
                })

                If Not String.IsNullOrWhiteSpace(pageText) Then
                    If options.KeepPageHeaders Then sb.AppendLine("===== صفحة " & pageNo.ToString() & " =====")
                    sb.AppendLine(pageText.Trim())
                    sb.AppendLine()
                End If
                pageNo += 1
                Report("اكتملت قراءة " & result.PageResults.Count.ToString() & " من " & pageImages.Count.ToString() & " صفحة.", 35 + CInt(60.0 * result.PageResults.Count / pageImages.Count))
            Next
            result.OcrText = sb.ToString().Trim()
        Finally
            Try
                Directory.Delete(tempFolder, True)
            Catch
            End Try
        End Try
    End Sub

    Private Function RenderPdfToImages(pdfPath As String, tempFolder As String, options As PdfOcrOptions, result As PdfOcrResult) As List(Of String)
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
                If images.Count > 0 Then
                    result.RenderTool = "Poppler pdftoppm"
                    Return images
                End If
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                result.Warnings.Add("فشل تحويل PDF عبر Poppler pdftoppm: " & ex.Message)
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
                If images.Count > 0 Then
                    result.RenderTool = "Ghostscript"
                    Return images
                End If
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                result.Warnings.Add("فشل تحويل PDF عبر Ghostscript: " & ex.Message)
            End Try
        End If

        Return images
    End Function

    Private Shared Function NaturalSortKey(name As String) As String
        If name Is Nothing Then Return ""
        Return Regex.Replace(name, "\d+", Function(m) m.Value.PadLeft(10, "0"c))
    End Function

    Private Sub NormalizeOptions(options As PdfOcrOptions)
        If options.Dpi < 150 Then options.Dpi = 150
        If options.Dpi > 600 Then options.Dpi = 600
        If options.FirstPage < 1 Then options.FirstPage = 1
        If options.MaxPages < 1 Then options.MaxPages = 100
        If options.MaxPages > 500 Then options.MaxPages = 500
        If options.LastPage > 0 AndAlso options.LastPage < options.FirstPage Then options.LastPage = options.FirstPage
    End Sub

    Private Function ShouldUseDirectTextFirst() As Boolean
        If _settings Is Nothing Then Return True
        Return _settings.PdfOcrUseDirectTextFirst
    End Function

    Private Shared Function LooksLikeUsefulText(text As String) As Boolean
        If String.IsNullOrWhiteSpace(text) Then Return False
        Dim clean = Regex.Replace(text, "\s+", " ").Trim()
        If clean.Length < 80 Then Return False
        Dim letters = clean.Count(Function(ch) Char.IsLetter(ch))
        Return letters >= 30
    End Function

    Private Function GetTesseractPath() As String
        If _settings IsNot Nothing AndAlso HasExecutable(_settings.TesseractExePath) Then Return _settings.TesseractExePath
        Return AutoFind(New String() {
            "C:\Program Files\Tesseract-OCR\tesseract.exe",
            "C:\Program Files (x86)\Tesseract-OCR\tesseract.exe"
        }, "tesseract.exe")
    End Function

    Private Function GetPdfToPpmPath() As String
        If _settings IsNot Nothing AndAlso HasExecutable(_settings.PopplerPdftoppmPath) Then Return _settings.PopplerPdftoppmPath
        Return AutoFind(New String() {
            "C:\Program Files\poppler\Library\bin\pdftoppm.exe",
            "C:\Program Files\poppler\bin\pdftoppm.exe",
            "C:\poppler\Library\bin\pdftoppm.exe",
            "C:\poppler\bin\pdftoppm.exe"
        }, "pdftoppm.exe")
    End Function

    Private Function GetPdfToTextPath() As String
        If _settings IsNot Nothing AndAlso HasExecutable(_settings.PopplerPdftotextPath) Then Return _settings.PopplerPdftotextPath
        Return AutoFind(New String() {
            "C:\Program Files\poppler\Library\bin\pdftotext.exe",
            "C:\Program Files\poppler\bin\pdftotext.exe",
            "C:\poppler\Library\bin\pdftotext.exe",
            "C:\poppler\bin\pdftotext.exe"
        }, "pdftotext.exe")
    End Function

    Private Function GetGhostscriptPath() As String
        If _settings IsNot Nothing AndAlso HasExecutable(_settings.GhostscriptExePath) Then Return _settings.GhostscriptExePath
        Dim candidates As New List(Of String)()
        candidates.Add("C:\Program Files\gs\gs10.05.1\bin\gswin64c.exe")
        candidates.Add("C:\Program Files\gs\gs10.04.0\bin\gswin64c.exe")
        candidates.Add("C:\Program Files\gs\gs10.03.1\bin\gswin64c.exe")
        candidates.Add("C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe")
        Try
            Dim gsRoot = "C:\Program Files\gs"
            If Directory.Exists(gsRoot) Then
                For Each p In Directory.GetFiles(gsRoot, "gswin64c.exe", SearchOption.AllDirectories)
                    candidates.Add(p)
                Next
            End If
        Catch
        End Try
        Return AutoFind(candidates.ToArray(), "gswin64c.exe")
    End Function

    Private Shared Function AutoFind(candidates() As String, exeName As String) As String
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

    Private Shared Function HasExecutable(path As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(path) AndAlso File.Exists(path)
    End Function

    Private Sub RunProcess(exePath As String, arguments As String, timeoutMs As Integer)
        Dim psi As New ProcessStartInfo()
        psi.FileName = exePath
        psi.Arguments = arguments
        psi.UseShellExecute = False
        psi.CreateNoWindow = True
        psi.RedirectStandardOutput = True
        psi.RedirectStandardError = True
        psi.StandardOutputEncoding = Encoding.UTF8
        psi.StandardErrorEncoding = Encoding.UTF8
        CancellableProcess.Run(psi, timeoutMs, _token)
    End Sub

    Private Shared Function Quote(value As String) As String
        Return """" & value & """"
    End Function

    Private Shared Sub TryDelete(path As String)
        If String.IsNullOrWhiteSpace(path) Then Return
        Try
            If File.Exists(path) Then File.Delete(path)
        Catch
        End Try
    End Sub
End Class
