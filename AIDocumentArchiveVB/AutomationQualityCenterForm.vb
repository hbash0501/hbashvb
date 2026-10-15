Imports System
Imports System.Collections.Generic
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class AutomationQualityCenterForm
    Private _items As List(Of DocumentQualityAssessment)
    Private _loading As Boolean

    Private Async Sub AutomationQualityCenterForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        cboProfile.DataSource = AppServices.ProcessingProfiles.GetProfiles()
        cboProfile.DisplayMember = "Name"
        txtGuide.Text = BuildGuideText()
        If AppServices.ProcessingQueue IsNot Nothing Then AddHandler AppServices.ProcessingQueue.JobChanged, AddressOf QueueJobChanged
        Await RefreshAssessmentsAsync()
    End Sub


    Private Sub AutomationQualityCenterForm_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        If AppServices.ProcessingQueue IsNot Nothing Then RemoveHandler AppServices.ProcessingQueue.JobChanged, AddressOf QueueJobChanged
    End Sub

    Private Sub QueueJobChanged(job As ProcessingJob)
        If job Is Nothing OrElse Not String.Equals(job.OperationKind, "INTELLIGENT_AUTOMATION", StringComparison.OrdinalIgnoreCase) Then Return
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
        Try
            Me.BeginInvoke(New Action(Async Sub()
                lblStatus.Text = job.Title & " | " & job.State & " | " & job.ProgressPercent.ToString() & "% - " & job.Message
                If job.State = "مكتملة" OrElse job.State = "فاشلة" Then
                    Await RefreshAssessmentsAsync()
                End If
            End Sub))
        Catch
        End Try
    End Sub

    Private Async Function RefreshAssessmentsAsync() As Task
        If _loading Then Return
        _loading = True
        Try
            lblStatus.Text = "جاري تقييم جودة المستندات..."
            Dim searchText = txtSearch.Text.Trim()
            _items = Await Task.Run(Function() AppServices.QualityGate.AssessAll(searchText))
            dgvDocuments.DataSource = Nothing
            dgvDocuments.DataSource = _items
            PrepareDocumentsGrid()
            lblStatus.Text = "تم تقييم " & _items.Count.ToString() & " مستند."
            ShowSelectedAssessment()
        Catch ex As Exception
            lblStatus.Text = "تعذر التقييم: " & ex.Message
        Finally
            _loading = False
        End Try
    End Function

    Private Sub PrepareDocumentsGrid()
        If dgvDocuments.Columns.Contains("DocumentId") Then dgvDocuments.Columns("DocumentId").Visible = False
        If dgvDocuments.Columns.Contains("Title") Then dgvDocuments.Columns("Title").HeaderText = "المستند"
        If dgvDocuments.Columns.Contains("QualityScore") Then dgvDocuments.Columns("QualityScore").HeaderText = "الجودة %"
        If dgvDocuments.Columns.Contains("Grade") Then dgvDocuments.Columns("Grade").HeaderText = "التصنيف"
        If dgvDocuments.Columns.Contains("RiskLevel") Then dgvDocuments.Columns("RiskLevel").HeaderText = "المخاطر"
        If dgvDocuments.Columns.Contains("MissingFields") Then dgvDocuments.Columns("MissingFields").Visible = False
        If dgvDocuments.Columns.Contains("RecommendedAction") Then dgvDocuments.Columns("RecommendedAction").Visible = False
        If dgvDocuments.Columns.Contains("NeedsHumanReview") Then dgvDocuments.Columns("NeedsHumanReview").Visible = False
        If dgvDocuments.Columns.Contains("IsDuplicateSuspected") Then dgvDocuments.Columns("IsDuplicateSuspected").Visible = False
        dgvDocuments.ClearSelection()
    End Sub

    Private Sub ShowSelectedAssessment()
        Dim item = SelectedAssessment()
        If item Is Nothing Then
            lblScore.Text = "الجودة: --"
            lblGrade.Text = "التصنيف: --"
            lblRisk.Text = "المخاطر: --"
            lblMissing.Text = "النواقص: اختر مستنداً"
            lblRecommendation.Text = "التوصية: --"
            txtQualityDetails.Text = ""
            Return
        End If

        lblScore.Text = "الجودة: " & item.QualityScore.ToString() & "%"
        lblGrade.Text = "التصنيف: " & item.Grade
        lblRisk.Text = "المخاطر: " & item.RiskLevel
        lblMissing.Text = "النواقص: " & item.MissingFields
        lblRecommendation.Text = "التوصية: " & item.RecommendedAction
        txtQualityDetails.Text = BuildAssessmentText(item)
    End Sub

    Private Function SelectedAssessment() As DocumentQualityAssessment
        If dgvDocuments.CurrentRow Is Nothing OrElse dgvDocuments.CurrentRow.DataBoundItem Is Nothing Then Return Nothing
        Return TryCast(dgvDocuments.CurrentRow.DataBoundItem, DocumentQualityAssessment)
    End Function

    Private Async Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        Await RefreshAssessmentsAsync()
    End Sub

    Private Async Sub txtSearch_KeyDown(sender As Object, e As KeyEventArgs) Handles txtSearch.KeyDown
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            Await RefreshAssessmentsAsync()
        End If
    End Sub

    Private Sub dgvDocuments_SelectionChanged(sender As Object, e As EventArgs) Handles dgvDocuments.SelectionChanged
        ShowSelectedAssessment()
    End Sub

    Private Sub dgvDocuments_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs) Handles dgvDocuments.CellDoubleClick
        OpenSelectedWorkspace()
    End Sub

    Private Sub btnRunSelected_Click(sender As Object, e As EventArgs) Handles btnRunSelected.Click
        Dim item = SelectedAssessment()
        If item Is Nothing Then
            MessageBox.Show("اختر مستنداً أولاً.", "المعالجة الذكية", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim profile = TryCast(cboProfile.SelectedItem, ProcessingProfile)
        Dim profileName = If(profile Is Nothing, "المسار الإداري المتوازن", profile.Name)
        Dim job = AppServices.IntelligentAutomation.EnqueueDocumentAutomation(item.DocumentId, profileName)
        lblStatus.Text = "تمت إضافة المهمة: " & job.Title & " - يمكنك متابعة التقدم من مركز المعالجة."
        MessageBox.Show("بدأت المعالجة في الخلفية دون إيقاف الواجهة.", "تم بدء المسار", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub btnRunLowQuality_Click(sender As Object, e As EventArgs) Handles btnRunLowQuality.Click
        If _items Is Nothing OrElse _items.Count = 0 Then Return
        Dim profile = TryCast(cboProfile.SelectedItem, ProcessingProfile)
        Dim profileName = If(profile Is Nothing, "المسار الإداري المتوازن", profile.Name)
        Dim count As Integer = 0
        For Each item In _items
            If item.QualityScore < 80 Then
                AppServices.IntelligentAutomation.EnqueueDocumentAutomation(item.DocumentId, profileName)
                count += 1
                If count >= 20 Then Exit For
            End If
        Next
        lblStatus.Text = "تمت جدولة " & count.ToString() & " مستند للمعالجة الخلفية."
        If count = 0 Then MessageBox.Show("لا توجد مستندات منخفضة الجودة ضمن القائمة الحالية.", "بوابة الجودة", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Async Sub btnProviderHealth_Click(sender As Object, e As EventArgs) Handles btnProviderHealth.Click
        btnProviderHealth.Enabled = False
        Try
            lblStatus.Text = "جاري فحص Groq وGemini وOpenRouter..."
            Dim results = Await AppServices.ResilientAi.TestAllProvidersAsync(CancellationToken.None)
            dgvProviders.DataSource = Nothing
            dgvProviders.DataSource = results
            PrepareProvidersGrid()
            TabControl1.SelectedTab = tabProviders
            lblStatus.Text = "اكتمل فحص مزودات الذكاء."
        Catch ex As Exception
            lblStatus.Text = "تعذر فحص المزودات: " & ex.Message
        Finally
            btnProviderHealth.Enabled = True
        End Try
    End Sub

    Private Sub PrepareProvidersGrid()
        If dgvProviders.Columns.Contains("ProviderName") Then dgvProviders.Columns("ProviderName").HeaderText = "المزود"
        If dgvProviders.Columns.Contains("ModelName") Then dgvProviders.Columns("ModelName").HeaderText = "النموذج"
        If dgvProviders.Columns.Contains("IsConfigured") Then dgvProviders.Columns("IsConfigured").HeaderText = "مهيأ"
        If dgvProviders.Columns.Contains("IsAvailable") Then dgvProviders.Columns("IsAvailable").HeaderText = "متاح"
        If dgvProviders.Columns.Contains("ResponseMs") Then dgvProviders.Columns("ResponseMs").HeaderText = "الاستجابة ms"
        If dgvProviders.Columns.Contains("StatusText") Then dgvProviders.Columns("StatusText").HeaderText = "الحالة"
        If dgvProviders.Columns.Contains("CheckedAt") Then dgvProviders.Columns("CheckedAt").HeaderText = "وقت الفحص"
    End Sub

    Private Sub btnOpenWorkspace_Click(sender As Object, e As EventArgs) Handles btnOpenWorkspace.Click
        OpenSelectedWorkspace()
    End Sub

    Private Sub OpenSelectedWorkspace()
        Dim item = SelectedAssessment()
        If item Is Nothing Then
            MessageBox.Show("اختر مستنداً أولاً.", "فتح المستند", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Using f As New DocumentWorkspaceForm(item.DocumentId)
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnProcessingCenter_Click(sender As Object, e As EventArgs) Handles btnProcessingCenter.Click
        Using f As New ProcessingCenterForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Shared Function BuildAssessmentText(item As DocumentQualityAssessment) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("المستند: " & item.Title)
        sb.AppendLine("درجة الجودة: " & item.QualityScore.ToString() & "%")
        sb.AppendLine("التصنيف: " & item.Grade)
        sb.AppendLine("مستوى المخاطر: " & item.RiskLevel)
        sb.AppendLine("يحتاج مراجعة بشرية: " & If(item.NeedsHumanReview, "نعم", "لا"))
        sb.AppendLine("اشتباه تكرار: " & If(item.IsDuplicateSuspected, "نعم", "لا"))
        sb.AppendLine()
        sb.AppendLine("النواقص:")
        sb.AppendLine(item.MissingFields)
        sb.AppendLine()
        sb.AppendLine("الإجراء المقترح:")
        sb.AppendLine(item.RecommendedAction)
        Return sb.ToString()
    End Function

    Private Shared Function BuildGuideText() As String
        Return "مسار العمل المقترح:" & Environment.NewLine & Environment.NewLine &
               "1. اختر المستند من القائمة؛ تظهر درجة الجودة والنواقص فوراً." & Environment.NewLine &
               "2. اختر ملف المعالجة المناسب: سريع، إداري متوازن، أو مراجعة حساسة." & Environment.NewLine &
               "3. اضغط تشغيل المسار المحدد. تتم المعالجة في الخلفية دون تجميد الواجهة." & Environment.NewLine &
               "4. يتولى النظام OCR عند الحاجة، ثم استخراج الكيانات محلياً، ثم يستخدم أول مزود ذكاء متاح." & Environment.NewLine &
               "5. إذا تعذر مزود، ينتقل تلقائياً إلى المزود التالي، ثم إلى القواعد المحلية عند عدم توفرها." & Environment.NewLine &
               "6. بوابة الجودة تحدد هل المستند جاهز للاعتماد أم يحتاج مراجعة بشرية." & Environment.NewLine & Environment.NewLine &
               "ملاحظة: المسودات الآلية لا تعد معتمدة حتى يراجعها الموظف المختص."
    End Function
End Class
