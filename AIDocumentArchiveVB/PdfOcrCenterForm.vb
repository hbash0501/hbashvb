Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Windows.Forms

Partial Public Class PdfOcrCenterForm
    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub PdfOcrCenterForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LoadSettingsToUi()
        txtReport.Text = New PdfOcrService(AppServices.Settings).Diagnose()
    End Sub

    Private Sub LoadSettingsToUi()
        Dim s = AppServices.Settings
        txtTesseract.Text = s.TesseractExePath
        txtPdfToPpm.Text = s.PopplerPdftoppmPath
        txtPdfToText.Text = s.PopplerPdftotextPath
        txtGhostscript.Text = s.GhostscriptExePath
        nudDpi.Value = Math.Max(nudDpi.Minimum, Math.Min(nudDpi.Maximum, s.PdfOcrDpi))
        nudMaxPages.Value = Math.Max(nudMaxPages.Minimum, Math.Min(nudMaxPages.Maximum, s.PdfOcrMaxPages))
        chkDirectText.Checked = s.PdfOcrUseDirectTextFirst
    End Sub

    Private Sub SaveUiToSettings()
        Dim s = AppServices.Settings
        s.TesseractExePath = txtTesseract.Text.Trim()
        s.PopplerPdftoppmPath = txtPdfToPpm.Text.Trim()
        s.PopplerPdftotextPath = txtPdfToText.Text.Trim()
        s.GhostscriptExePath = txtGhostscript.Text.Trim()
        s.PdfOcrDpi = CInt(nudDpi.Value)
        s.PdfOcrMaxPages = CInt(nudMaxPages.Value)
        s.PdfOcrUseDirectTextFirst = chkDirectText.Checked
        s.Save()
    End Sub

    Private Sub btnBrowsePdf_Click(sender As Object, e As EventArgs) Handles btnBrowsePdf.Click
        Using ofd As New OpenFileDialog()
            ofd.Title = "اختر ملف PDF"
            ofd.Filter = "PDF|*.pdf|كل الملفات|*.*"
            If ofd.ShowDialog(Me) = DialogResult.OK Then txtPdfPath.Text = ofd.FileName
        End Using
    End Sub

    Private Sub btnBrowseTess_Click(sender As Object, e As EventArgs) Handles btnBrowseTess.Click
        BrowseExecutable(txtTesseract, "اختر tesseract.exe", "tesseract.exe")
    End Sub

    Private Sub btnBrowsePdfToPpm_Click(sender As Object, e As EventArgs) Handles btnBrowsePdfToPpm.Click
        BrowseExecutable(txtPdfToPpm, "اختر pdftoppm.exe من Poppler", "pdftoppm.exe")
    End Sub

    Private Sub btnBrowsePdfToText_Click(sender As Object, e As EventArgs) Handles btnBrowsePdfToText.Click
        BrowseExecutable(txtPdfToText, "اختر pdftotext.exe من Poppler", "pdftotext.exe")
    End Sub

    Private Sub btnBrowseGhostscript_Click(sender As Object, e As EventArgs) Handles btnBrowseGhostscript.Click
        BrowseExecutable(txtGhostscript, "اختر gswin64c.exe من Ghostscript", "gswin64c.exe")
    End Sub

    Private Sub BrowseExecutable(target As TextBox, title As String, exeName As String)
        Using ofd As New OpenFileDialog()
            ofd.Title = title
            ofd.Filter = exeName & "|" & exeName & "|كل الملفات|*.*"
            If File.Exists(target.Text) Then ofd.InitialDirectory = Path.GetDirectoryName(target.Text)
            If ofd.ShowDialog(Me) = DialogResult.OK Then target.Text = ofd.FileName
        End Using
    End Sub

    Private Sub btnSaveSettings_Click(sender As Object, e As EventArgs) Handles btnSaveSettings.Click
        SaveUiToSettings()
        txtReport.Text = New PdfOcrService(AppServices.Settings).Diagnose()
        MessageBox.Show("تم حفظ إعدادات PDF OCR.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub btnDiagnose_Click(sender As Object, e As EventArgs) Handles btnDiagnose.Click
        SaveUiToSettings()
        txtReport.Text = New PdfOcrService(AppServices.Settings).Diagnose()
    End Sub

    Private Sub btnRun_Click(sender As Object, e As EventArgs) Handles btnRun.Click
        If String.IsNullOrWhiteSpace(txtPdfPath.Text) OrElse Not File.Exists(txtPdfPath.Text) Then
            MessageBox.Show("اختر ملف PDF صحيحاً أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        SaveUiToSettings()
        Try
            btnRun.Enabled = False
            txtExtractedText.Clear()
            txtReport.Text = "بدأت عملية PDF OCR في الخلفية..."

            Dim pdfPath As String = txtPdfPath.Text
            Dim options As PdfOcrOptions = PdfOcrOptions.FromSettings(AppServices.Settings)
            options.ForceOcr = chkForceOcr.Checked
            options.FirstPage = CInt(nudFirstPage.Value)
            options.LastPage = CInt(nudLastPage.Value)
            If options.LastPage = 0 Then options.LastPage = 0
            options.KeepPageHeaders = chkPageHeaders.Checked

            Dim result As PdfOcrResult = ProgressDialogForm.Run(Of PdfOcrResult)(Me,
                "PDF OCR الاحترافي",
                "جاري تحويل صفحات PDF وتشغيل Tesseract. العملية تعمل في الخلفية.",
                Function(ctx As ProgressDialogForm)
                    ctx.Report("جاري قراءة PDF والتحقق من النص المضمن...", 10)
                    Dim ocrResult = New PdfOcrService(AppServices.Settings).Extract(pdfPath, options)
                    ctx.Report("اكتملت عملية PDF OCR.", 100)
                    Return ocrResult
                End Function)

            txtExtractedText.Text = result.BestText()
            txtReport.Text = result.BuildReport()
            If String.IsNullOrWhiteSpace(txtExtractedText.Text) Then
                MessageBox.Show("لم يتم استخراج نص. راجع تقرير التشخيص وتأكد من Poppler/Ghostscript وTesseract.", "PDF OCR", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Else
                MessageBox.Show("تم استخراج نص PDF بنجاح." & Environment.NewLine & "عدد الأحرف: " & txtExtractedText.Text.Length.ToString(), "PDF OCR", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            MessageBox.Show(ex.Message, "خطأ PDF OCR", MessageBoxButtons.OK, MessageBoxIcon.Error)
            txtReport.Text = ex.ToString()
        Finally
            btnRun.Enabled = True
        End Try
    End Sub

    Private Sub btnCopyText_Click(sender As Object, e As EventArgs) Handles btnCopyText.Click
        If Not String.IsNullOrWhiteSpace(txtExtractedText.Text) Then
            Clipboard.SetText(txtExtractedText.Text)
            MessageBox.Show("تم نسخ النص المستخرج.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub btnSaveText_Click(sender As Object, e As EventArgs) Handles btnSaveText.Click
        If String.IsNullOrWhiteSpace(txtExtractedText.Text) Then Return
        Using sfd As New SaveFileDialog()
            sfd.Title = "حفظ النص المستخرج"
            sfd.Filter = "Text UTF-8|*.txt"
            sfd.FileName = "PDF_OCR_" & DateTime.Now.ToString("yyyyMMdd_HHmm") & ".txt"
            If sfd.ShowDialog(Me) = DialogResult.OK Then
                File.WriteAllText(sfd.FileName, txtExtractedText.Text, System.Text.Encoding.UTF8)
                Process.Start(New ProcessStartInfo(sfd.FileName) With {.UseShellExecute = True})
            End If
        End Using
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class
