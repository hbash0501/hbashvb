Imports System
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading

Public Class OcrService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _token As CancellationToken

    Public Sub New(settings As AppSettings, Optional token As CancellationToken = Nothing)
        _settings = settings
        _token = token
    End Sub

    Public Function ExtractText(filePath As String) As String
        Return ExtractTextProfessional(filePath)
    End Function

    Public Function ExtractTextProfessional(filePath As String) As String
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then
            Throw New FileNotFoundException("لم يتم العثور على الملف المطلوب قراءته.", filePath)
        End If

        Dim ext As String = Path.GetExtension(filePath).ToLowerInvariant()
        If ext = ".pdf" Then
            Dim pdfService As New PdfOcrService(_settings)
            Dim pdfResult = pdfService.Extract(filePath, PdfOcrOptions.FromSettings(_settings), _token)
            Return pdfResult.BestText()
        End If

        If _settings Is Nothing OrElse Not File.Exists(_settings.TesseractExePath) Then
            Return ""
        End If

        Dim targetPath As String = filePath
        Dim tempPath As String = ""
        If IsImage(ext) Then
            tempPath = TryCreateReadableImage(filePath)
            If Not String.IsNullOrWhiteSpace(tempPath) AndAlso File.Exists(tempPath) Then targetPath = tempPath
        End If

        Try
            Return RunTesseractMultiPass(targetPath)
        Finally
            If Not String.IsNullOrWhiteSpace(tempPath) AndAlso File.Exists(tempPath) Then
                Try
                    File.Delete(tempPath)
                Catch
                End Try
            End If
        End Try
    End Function

    Private Function RunTesseractMultiPass(filePath As String) As String
        Dim psmModes As Integer() = New Integer() {6, 4, 3, 11}
        Dim allLines As New List(Of String)()
        Dim bestText As String = ""

        For Each psm In psmModes
            _token.ThrowIfCancellationRequested()
            Dim text = RunTesseract(filePath, psm)
            If text.Length > bestText.Length Then bestText = text
            For Each line In SplitLines(text)
                If Not ContainsLine(allLines, line) Then allLines.Add(line)
            Next
        Next

        Dim merged = String.Join(Environment.NewLine, allLines.ToArray()).Trim()
        If merged.Length >= bestText.Length / 2 Then Return merged
        Return bestText.Trim()
    End Function

    Private Function RunTesseract(filePath As String, psm As Integer) As String
        Dim psi As New ProcessStartInfo()
        psi.FileName = _settings.TesseractExePath
        psi.Arguments = Quote(filePath) & " stdout -l " & SafeLanguage(_settings.OcrLanguage) & " --oem 1 --psm " & psm.ToString() & " -c preserve_interword_spaces=1"
        psi.UseShellExecute = False
        psi.CreateNoWindow = True
        psi.RedirectStandardOutput = True
        psi.RedirectStandardError = True
        psi.StandardOutputEncoding = Encoding.UTF8
        psi.StandardErrorEncoding = Encoding.UTF8

        Return CancellableProcess.Run(psi, 90000, _token).Trim()
    End Function

    Private Shared Function TryCreateReadableImage(filePath As String) As String
        Try
            Dim tempPath = Path.Combine(Path.GetTempPath(), "aidoc_ocr_" & Guid.NewGuid().ToString("N") & ".png")
            Using original As New Bitmap(filePath)
                Dim scale As Double = 1.0
                If original.Width < 1800 Then scale = 1800.0 / Math.Max(1, original.Width)
                If scale > 2.5 Then scale = 2.5
                Dim w As Integer = Math.Max(1, CInt(original.Width * scale))
                Dim h As Integer = Math.Max(1, CInt(original.Height * scale))
                Using resized As New Bitmap(w, h)
                    resized.SetResolution(300, 300)
                    Using g = Graphics.FromImage(resized)
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic
                        g.SmoothingMode = SmoothingMode.HighQuality
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality
                        g.DrawImage(original, 0, 0, w, h)
                    End Using
                    resized.Save(tempPath, ImageFormat.Png)
                End Using
            End Using
            Return tempPath
        Catch
            Return ""
        End Try
    End Function

    Private Shared Function SplitLines(text As String) As List(Of String)
        Dim result As New List(Of String)()
        If String.IsNullOrWhiteSpace(text) Then Return result
        For Each raw In text.Split(New String() {ControlChars.CrLf, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries)
            Dim line = raw.Trim()
            If line.Length >= 2 Then result.Add(line)
        Next
        Return result
    End Function

    Private Shared Function ContainsLine(lines As List(Of String), line As String) As Boolean
        For Each item In lines
            If String.Equals(item, line, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    Private Shared Function IsImage(ext As String) As Boolean
        Return ext = ".jpg" OrElse ext = ".jpeg" OrElse ext = ".png" OrElse ext = ".bmp" OrElse ext = ".tif" OrElse ext = ".tiff"
    End Function

    Private Shared Function SafeLanguage(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "ara+eng"
        Return value.Replace(" ", "")
    End Function

    Private Shared Function Quote(v As String) As String
        Return """" & v & """"
    End Function
End Class
