Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Windows.Forms

Partial Public Class AskArchiveProForm
    Private ReadOnly _service As SemanticArchiveAnswerService
    Private _lastResult As ArchiveAnswerResult

    Public Sub New()
        InitializeComponent()
        _service = New SemanticArchiveAnswerService(AppServices.Settings, AppServices.Repository)
        UiThemeService.StyleDataGrid(dgvEvidence)
        UiThemeService.StyleDataGrid(dgvRelations)
    End Sub

    Private Sub AskArchiveProForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        lblStatus.Text = "اكتب سؤالاً مثل: ما المعاملات المرتبطة بوزارة التعليم؟ أو اعرض المستندات التي تحتوي على مبالغ."
    End Sub



    Private Sub btnAsk_Click(sender As Object, e As EventArgs) Handles btnAsk.Click
        Try
            Dim question As String = txtQuestion.Text
            Dim maxResults As Integer = CInt(numResults.Value)
            Dim includeRelations As Boolean = chkRelations.Checked
            lblStatus.Text = "جاري سؤال الأرشيف وبناء السياق الذكي..."
            _lastResult = ProgressDialogForm.Run(Of ArchiveAnswerResult)(Me,
                "سؤال الأرشيف الذكي",
                "جاري البحث في الفهرس وبناء الإجابة وربط الأدلة.",
                Function(ctx As ProgressDialogForm)
                    ctx.Report("جاري تحليل السؤال والبحث في المستندات...", -1)
                    Return _service.Ask(question, maxResults, includeRelations)
                End Function)
            txtAnswer.Text = _lastResult.Answer
            dgvEvidence.DataSource = _service.EvidenceToDataTable(_lastResult)
            dgvRelations.DataSource = _service.RelationsToDataTable(_lastResult)
            If dgvEvidence.Columns.Contains("Id") Then dgvEvidence.Columns("Id").Visible = False
            lblStatus.Text = "تمت الإجابة. النتائج: " & _lastResult.Evidence.Count & " | العلاقات: " & _lastResult.Relations.Count
            AppServices.Repository.AddAudit("سؤال الأرشيف", "", "", question)
        Catch ex As Exception
            MessageBox.Show("تعذر سؤال الأرشيف: " & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
            lblStatus.Text = "حدث خطأ أثناء السؤال."
        End Try
    End Sub

    Private Sub btnRebuild_Click(sender As Object, e As EventArgs) Handles btnRebuild.Click
        Try
            Dim count = ProgressDialogForm.Run(Of Integer)(Me,
                "بناء فهرس المعرفة",
                "جاري إعادة فهرسة المستندات حتى تصبح قابلة للسؤال والبحث الذكي.",
                Function(ctx As ProgressDialogForm)
                    ctx.Report("جاري بناء الفهرس المحلي...", -1)
                    Return _service.RebuildIndex()
                End Function)
            lblStatus.Text = "تم إعادة بناء فهرس المعرفة: " & count & " مستند."
            txtAnswer.Text = "تم بناء الفهرس بنجاح. يمكنك الآن سؤال الأرشيف الكامل."
            AppServices.Repository.AddAudit("إعادة بناء الفهرس", "", "", "عدد المستندات: " & count)
        Catch ex As Exception
            MessageBox.Show("تعذر بناء الفهرس: " & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnExportCase_Click(sender As Object, e As EventArgs) Handles btnExportCase.Click
        If _lastResult Is Nothing Then
            MessageBox.Show("اسأل الأرشيف أولاً ثم أنشئ ملف المعاملة الذكي.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Using dlg As New FolderBrowserDialog()
            dlg.Description = "اختر مكان إنشاء ملف المعاملة الذكي"
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Dim folderPath = _service.BuildCaseFolder(txtQuestion.Text, _lastResult, dlg.SelectedPath)
                AppServices.Repository.AddAudit("إنشاء ملف معاملة", "", "", folderPath)
                lblStatus.Text = "تم إنشاء ملف المعاملة: " & folderPath
                Process.Start("explorer.exe", folderPath)
            Catch ex As Exception
                MessageBox.Show("تعذر إنشاء ملف المعاملة: " & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub btnExportRelations_Click(sender As Object, e As EventArgs) Handles btnExportRelations.Click
        If _lastResult Is Nothing Then
            MessageBox.Show("اسأل الأرشيف أولاً ثم صدّر شبكة العلاقات.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Using dlg As New FolderBrowserDialog()
            dlg.Description = "اختر مجلداً لحفظ تقرير العلاقات"
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Dim reportPath = _service.ExportRelationsHtml(_lastResult, dlg.SelectedPath)
                AppServices.Repository.AddAudit("تصدير العلاقات", "", "", reportPath)
                lblStatus.Text = "تم إنشاء تقرير العلاقات: " & reportPath
                Process.Start("explorer.exe", "/select,""" & reportPath & """")
            Catch ex As Exception
                MessageBox.Show("تعذر تصدير العلاقات: " & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub dgvEvidence_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs) Handles dgvEvidence.CellDoubleClick
        If dgvEvidence.CurrentRow Is Nothing OrElse Not dgvEvidence.Columns.Contains("Id") Then Return
        Dim id = Convert.ToString(dgvEvidence.CurrentRow.Cells("Id").Value)
        If String.IsNullOrWhiteSpace(id) Then Return
        Using f As New DocumentPreviewForm(id)
            f.ShowDialog(Me)
        End Using
    End Sub
End Class
