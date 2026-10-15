Imports System
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class DocumentWorkspaceForm
    Private _selectedDocumentId As String
    Private _currentDocument As DocumentRecord
    Private _busy As Boolean = False

    Public Sub New(Optional documentId As String = "")
        InitializeComponent()
        _selectedDocumentId = If(documentId, "")
        If cboTone.Items.Count > 0 Then cboTone.SelectedIndex = 0
        EnableGridDoubleBuffering(dgvDocs)
        btnOcr.Enabled = AppServices.Auth.HasPermission("edit")
        btnAutomate.Enabled = AppServices.Auth.HasPermission("edit") AndAlso AppServices.Auth.HasPermission("ai")
        btnQuick.Enabled = AppServices.Auth.HasPermission("ai")
        btnDeep.Enabled = AppServices.Auth.HasPermission("ai")
        btnReply.Enabled = AppServices.Auth.HasPermission("ai")
        btnExportReply.Enabled = AppServices.Auth.HasPermission("export")
    End Sub

    Private Async Sub DocumentWorkspaceForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Await LoadDocumentsAsync()
        If Not String.IsNullOrWhiteSpace(_selectedDocumentId) Then
            SelectDocument(_selectedDocumentId)
        ElseIf dgvDocs.Rows.Count > 0 Then
            dgvDocs.Rows(0).Selected = True
            dgvDocs.CurrentCell = dgvDocs.Rows(0).Cells(colTitle.Index)
        End If
    End Sub

    Private Async Function LoadDocumentsAsync() As Task
        If _busy Then Return
        _busy = True
        Try
            lblStatus.Text = "جاري تحميل قائمة المستندات..."
            btnSearch.Enabled = False
            btnClearSearch.Enabled = False
            Dim queryText As String = txtSearch.Text.Trim()
            Dim documents = Await Task.Run(Function() AppServices.Repository.GetDocuments(queryText, "", ""))

            dgvDocs.SuspendLayout()
            dgvDocs.DataSource = Nothing
            dgvDocs.DataSource = documents
            dgvDocs.ResumeLayout()

            lblListCount.Text = documents.Count.ToString() & " مستند"
            lblStatus.Text = If(documents.Count = 0,
                                "لا توجد مستندات مطابقة لعبارة البحث.",
                                "تم تحميل " & documents.Count.ToString() & " مستند. اختر صفاً لعرض التفاصيل.")
        Finally
            btnSearch.Enabled = True
            btnClearSearch.Enabled = True
            _busy = False
        End Try
    End Function

    Private Sub SelectDocument(documentId As String)
        If String.IsNullOrWhiteSpace(documentId) Then Return
        For Each row As DataGridViewRow In dgvDocs.Rows
            Dim document = TryCast(row.DataBoundItem, DocumentRecord)
            If document IsNot Nothing AndAlso String.Equals(document.Id, documentId, StringComparison.OrdinalIgnoreCase) Then
                dgvDocs.ClearSelection()
                row.Selected = True
                dgvDocs.CurrentCell = row.Cells(colTitle.Index)
                LoadCurrentDocument(document)
                Exit For
            End If
        Next
    End Sub

    Private Sub LoadCurrentDocument(document As DocumentRecord)
        _currentDocument = document
        If document Is Nothing Then
            ClearDocumentView()
            Return
        End If

        txtTitle.Text = SafeDisplay(document.Title)
        txtArchiveNo.Text = SafeDisplay(document.ArchiveNumber)
        txtCategory.Text = SafeDisplay(document.GeneralCategory)
        txtSubCategory.Text = SafeDisplay(document.SubCategory)
        txtSource.Text = SafeDisplay(document.SourceEntity)
        txtDestination.Text = SafeDisplay(document.DestinationEntity)
        txtStatus.Text = SafeDisplay(document.Status)
        txtPriority.Text = SafeDisplay(document.Priority)
        TextFormattingService.ApplyFormattedDocument(rtbText, document)
        txtEntities.Text = TextFormattingService.BuildEntitySummary(document)
        txtAi.Text = If(document.OcrStructuredReport, "")
        txtReply.Text = ExtractLastReply(document.OcrStructuredReport)

        Dim quality = AppServices.QualityGate.Assess(document)
        lblStatus.Text = "المستند الحالي: " & SafeDisplay(document.Title) &
                         "   |   الجودة: " & quality.QualityScore.ToString() & "%" &
                         "   |   التقييم: " & quality.Grade
    End Sub

    Private Sub ClearDocumentView()
        txtTitle.Clear()
        txtArchiveNo.Clear()
        txtCategory.Clear()
        txtSubCategory.Clear()
        txtSource.Clear()
        txtDestination.Clear()
        txtStatus.Clear()
        txtPriority.Clear()
        rtbText.Clear()
        txtEntities.Clear()
        txtAi.Clear()
        txtReply.Clear()
    End Sub

    Private Sub dgvDocs_SelectionChanged(sender As Object, e As EventArgs) Handles dgvDocs.SelectionChanged
        If dgvDocs.CurrentRow Is Nothing OrElse dgvDocs.CurrentRow.DataBoundItem Is Nothing Then Return
        Dim document = TryCast(dgvDocs.CurrentRow.DataBoundItem, DocumentRecord)
        If document IsNot Nothing Then LoadCurrentDocument(document)
    End Sub

    Private Sub dgvDocs_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs) Handles dgvDocs.CellFormatting
        If e.RowIndex < 0 Then Return

        If dgvDocs.Columns(e.ColumnIndex).Name = colStatus.Name Then
            Dim statusText As String = Convert.ToString(e.Value)
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            e.CellStyle.Font = New Font(dgvDocs.Font, FontStyle.Bold)

            Select Case statusText.Trim()
                Case "معتمد", "مؤرشف نهائياً", "مكتمل"
                    e.CellStyle.BackColor = Color.FromArgb(222, 244, 232)
                    e.CellStyle.ForeColor = Color.FromArgb(25, 110, 65)
                Case "عاجل", "مرفوض", "متأخر"
                    e.CellStyle.BackColor = Color.FromArgb(253, 230, 230)
                    e.CellStyle.ForeColor = Color.FromArgb(170, 45, 45)
                Case "قيد المراجعة", "بانتظار الاعتماد", "يحتاج استكمال"
                    e.CellStyle.BackColor = Color.FromArgb(255, 244, 214)
                    e.CellStyle.ForeColor = Color.FromArgb(145, 95, 0)
                Case Else
                    e.CellStyle.BackColor = Color.FromArgb(235, 241, 247)
                    e.CellStyle.ForeColor = Color.FromArgb(55, 75, 95)
            End Select
        ElseIf dgvDocs.Columns(e.ColumnIndex).Name = colTitle.Name Then
            e.CellStyle.WrapMode = DataGridViewTriState.True
            e.CellStyle.Padding = New Padding(4, 4, 4, 4)
        ElseIf dgvDocs.Columns(e.ColumnIndex).Name = colArchiveNumber.Name Then
            e.CellStyle.Font = New Font(dgvDocs.Font, FontStyle.Bold)
            e.CellStyle.ForeColor = Color.FromArgb(35, 85, 120)
        End If
    End Sub

    Private Sub dgvDocs_CellToolTipTextNeeded(sender As Object, e As DataGridViewCellToolTipTextNeededEventArgs) Handles dgvDocs.CellToolTipTextNeeded
        If e.RowIndex < 0 OrElse e.RowIndex >= dgvDocs.Rows.Count Then Return
        Dim document = TryCast(dgvDocs.Rows(e.RowIndex).DataBoundItem, DocumentRecord)
        If document Is Nothing Then Return
        e.ToolTipText = SafeDisplay(document.Title) & Environment.NewLine &
                        "رقم الأرشيف: " & SafeDisplay(document.ArchiveNumber) & Environment.NewLine &
                        "الحالة: " & SafeDisplay(document.Status)
    End Sub

    Private Sub dgvDocs_DataError(sender As Object, e As DataGridViewDataErrorEventArgs) Handles dgvDocs.DataError
        e.ThrowException = False
    End Sub

    Private Async Sub btnSearch_Click(sender As Object, e As EventArgs) Handles btnSearch.Click
        Await LoadDocumentsAsync()
    End Sub

    Private Async Sub btnClearSearch_Click(sender As Object, e As EventArgs) Handles btnClearSearch.Click
        txtSearch.Clear()
        Await LoadDocumentsAsync()
        txtSearch.Focus()
    End Sub

    Private Async Sub txtSearch_KeyDown(sender As Object, e As KeyEventArgs) Handles txtSearch.KeyDown
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            Await LoadDocumentsAsync()
        End If
    End Sub

    Private Sub dgvDocs_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs) Handles dgvDocs.CellDoubleClick
        If e.RowIndex < 0 OrElse _currentDocument Is Nothing Then Return
        TabControl1.SelectedTab = tabText
    End Sub

    Private Sub btnAutomate_Click(sender As Object, e As EventArgs) Handles btnAutomate.Click
        If Not EnsureDocument() Then Return
        Try
            Dim job = AppServices.IntelligentAutomation.EnqueueDocumentAutomation(_currentDocument.Id, "المسار الإداري المتوازن")
            lblStatus.Text = "بدأت المعالجة الذكية المتكاملة في الخلفية. المهمة: " & job.Title
            MessageBox.Show("تمت جدولة OCR والكيانات والفهم والتوجيه وبوابة الجودة في مسار واحد.", "المعالجة الذكية", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر بدء المعالجة", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnOcr_Click(sender As Object, e As EventArgs) Handles btnOcr.Click
        If Not EnsureDocument() Then Return
        Try
            AppServices.Orchestrator.EnqueueOcrAndFastUnderstanding(_currentDocument.Id)
            lblStatus.Text = "تم إرسال OCR والمعالجة السريعة للخلفية. تابعها من مركز المعالجة."
            MessageBox.Show("تم إرسال المهمة إلى الخلفية. يمكنك متابعة العمل دون انتظار.", "المعالجة الخلفية", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر بدء OCR", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnQuick_Click(sender As Object, e As EventArgs) Handles btnQuick.Click
        If Not EnsureDocument() Then Return
        AppServices.Speed.EnqueueDeepUnderstanding(_currentDocument.Id, False)
        lblStatus.Text = "تم إرسال الفهم السريع للخلفية."
    End Sub

    Private Sub btnDeep_Click(sender As Object, e As EventArgs) Handles btnDeep.Click
        If Not EnsureDocument() Then Return
        AppServices.Orchestrator.EnqueueDeepUnderstanding(_currentDocument.Id)
        lblStatus.Text = "تم إرسال الفهم العميق للخلفية."
    End Sub

    Private Async Sub btnReply_Click(sender As Object, e As EventArgs) Handles btnReply.Click
        If Not EnsureDocument() OrElse _busy Then Return
        _busy = True
        Try
            btnReply.Enabled = False
            lblStatus.Text = "جاري إنشاء الرد الذكي..."
            Dim tone = If(cboTone.SelectedItem Is Nothing, "رسمي متوازن", cboTone.SelectedItem.ToString())
            Dim reply = Await AppServices.AiGateway.GenerateReplyAsync(_currentDocument, tone, Threading.CancellationToken.None)
            txtReply.Text = reply
            TabControl1.SelectedTab = tabReply
            lblStatus.Text = "تم إنشاء الرد. راجعه ثم صدّره إلى Word."
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر إنشاء الرد", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnReply.Enabled = True
            _busy = False
        End Try
    End Sub

    Private Sub btnExportReply_Click(sender As Object, e As EventArgs) Handles btnExportReply.Click
        If Not EnsureDocument() Then Return
        If String.IsNullOrWhiteSpace(txtReply.Text) Then
            MessageBox.Show("لا يوجد رد لتصديره.", "تصدير Word", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        SaveFileDialog1.FileName = "Reply_" & SafeFile(_currentDocument.ArchiveNumber) & ".docx"
        If SaveFileDialog1.ShowDialog() <> DialogResult.OK Then Return
        Try
            ReplyExportService.ExportReplyAsDocx(_currentDocument, txtReply.Text, SaveFileDialog1.FileName)
            lblStatus.Text = "تم تصدير الرد إلى Word: " & SaveFileDialog1.FileName
            MessageBox.Show("تم التصدير بنجاح.", "تصدير Word", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر التصدير", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function EnsureDocument() As Boolean
        If _currentDocument Is Nothing Then
            MessageBox.Show("اختر مستنداً من القائمة أولاً.", "مركز المستند", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return False
        End If
        Return True
    End Function

    Private Shared Function SafeDisplay(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "—"
        Return value.Trim()
    End Function

    Private Shared Function ExtractLastReply(report As String) As String
        If String.IsNullOrWhiteSpace(report) Then Return ""
        Dim key = "رد ذكي"
        Dim index = report.LastIndexOf(key, StringComparison.OrdinalIgnoreCase)
        If index >= 0 Then Return report.Substring(index)
        Return ""
    End Function

    Private Shared Function SafeFile(value As String) As String
        Dim safeValue = If(value, "document")
        For Each invalidCharacter As Char In Path.GetInvalidFileNameChars()
            safeValue = safeValue.Replace(invalidCharacter, "_"c)
        Next
        If safeValue.Trim() = "" Then safeValue = "document"
        Return safeValue
    End Function

    Private Shared Sub EnableGridDoubleBuffering(grid As DataGridView)
        Try
            Dim propertyInfo = GetType(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance Or BindingFlags.NonPublic)
            If propertyInfo IsNot Nothing Then propertyInfo.SetValue(grid, True, Nothing)
        Catch
            ' تحسين بصري اختياري، ولا يؤثر فشله على تشغيل النموذج.
        End Try
    End Sub
End Class
