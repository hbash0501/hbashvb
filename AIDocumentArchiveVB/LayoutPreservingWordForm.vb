Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Windows.Forms

Partial Public Class LayoutPreservingWordForm
    Public Sub New()
        InitializeComponent()
        cboMode.SelectedIndex = 0
        AppendLog("جاهز. اختر PDF أو صورة ثم حدد نمط التحويل.")
    End Sub

    Private Sub btnBrowseInput_Click(sender As Object, e As EventArgs) Handles btnBrowseInput.Click
        Using ofd As New OpenFileDialog()
            ofd.Title = "اختيار PDF أو صورة"
            ofd.Filter = "PDF أو صور|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|كل الملفات|*.*"
            If ofd.ShowDialog(Me) = DialogResult.OK Then
                txtInput.Text = ofd.FileName
                If String.IsNullOrWhiteSpace(txtOutput.Text) Then
                    txtOutput.Text = Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) & "_Word_Layout.docx")
                End If
            End If
        End Using
    End Sub

    Private Sub btnBrowseOutput_Click(sender As Object, e As EventArgs) Handles btnBrowseOutput.Click
        Using sfd As New SaveFileDialog()
            sfd.Title = "حفظ ملف Word"
            sfd.Filter = "Word DOCX|*.docx"
            sfd.FileName = If(String.IsNullOrWhiteSpace(txtOutput.Text), "Document_Word_Layout.docx", Path.GetFileName(txtOutput.Text))
            If sfd.ShowDialog(Me) = DialogResult.OK Then txtOutput.Text = sfd.FileName
        End Using
    End Sub

    Private Sub btnConvert_Click(sender As Object, e As EventArgs) Handles btnConvert.Click
        If String.IsNullOrWhiteSpace(txtInput.Text) OrElse Not File.Exists(txtInput.Text) Then
            MessageBox.Show("اختر ملف PDF أو صورة أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If String.IsNullOrWhiteSpace(txtOutput.Text) Then
            MessageBox.Show("حدد مسار ملف Word الناتج.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim options As New LayoutWordConversionOptions With {
            .Dpi = CInt(nudDpi.Value),
            .Language = txtLanguage.Text.Trim(),
            .FirstPage = CInt(nudFirstPage.Value),
            .LastPage = CInt(nudLastPage.Value),
            .AddEditableTextLayer = chkEditableLayer.Checked,
            .HighlightDetectedEntities = chkHighlight.Checked,
            .Mode = SelectedMode()
        }

        Try
            btnConvert.Enabled = False
            Dim service As New LayoutPreservingWordExportService(AppServices.Settings)
            Dim result = ProgressDialogForm.Run(Of LayoutWordConversionResult)(Me,
                "تحويل إلى Word مطابق",
                "جاري تحويل الصفحات واستخراج النص دون تجميد الواجهة.",
                Function(ctx) service.ConvertToWord(txtInput.Text, txtOutput.Text, options, ctx))

            btnOpenOutput.Enabled = File.Exists(txtOutput.Text)
            AppendLog("تم إنشاء ملف Word: " & result.OutputPath)
            AppendLog("عدد الصفحات: " & result.PageCount.ToString() & " | مدة التنفيذ: " & result.DurationSeconds.ToString("0.0") & " ثانية")
            If result.Warnings IsNot Nothing AndAlso result.Warnings.Count > 0 Then
                AppendLog("ملاحظات:")
                For Each w In result.Warnings
                    AppendLog("- " & w)
                Next
            End If
            AppServices.Repository.AddAudit("تحويل إلى Word مطابق", "", Path.GetFileName(txtInput.Text), txtOutput.Text)
        Catch ex As OperationCanceledException
            AppendLog("تم إلغاء العملية.")
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر التحويل", MessageBoxButtons.OK, MessageBoxIcon.Error)
            AppendLog("خطأ: " & ex.Message)
        Finally
            btnConvert.Enabled = True
        End Try
    End Sub

    Private Function SelectedMode() As String
        Select Case cboMode.SelectedIndex
            Case 1
                Return "VisualOnly"
            Case 2
                Return "EditableOnly"
            Case Else
                Return "Hybrid"
        End Select
    End Function

    Private Sub btnOpenOutput_Click(sender As Object, e As EventArgs) Handles btnOpenOutput.Click
        If File.Exists(txtOutput.Text) Then Process.Start(New ProcessStartInfo(txtOutput.Text) With {.UseShellExecute = True})
    End Sub

    Private Sub AppendLog(message As String)
        txtLog.AppendText("[" & DateTime.Now.ToString("HH:mm:ss") & "] " & message & Environment.NewLine)
    End Sub
End Class
