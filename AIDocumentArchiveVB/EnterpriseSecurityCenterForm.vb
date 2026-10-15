Imports System
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms

Public Class EnterpriseSecurityCenterForm
    Private _selectedDocument As DocumentRecord
    Private _pipelineCts As CancellationTokenSource

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub EnterpriseSecurityCenterForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        chkValidateSignatures.Checked = AppServices.Settings.EnableFileSignatureValidation
        chkProtectSensitive.Checked = AppServices.Settings.EnableSensitiveDocumentEncryption
        numRiskThreshold.Value = Math.Max(numRiskThreshold.Minimum, Math.Min(numRiskThreshold.Maximum, AppServices.Settings.SensitiveDocumentRiskThresholdPercent))
        cboProfile.Items.Clear()
        For Each profile In AppServices.ProcessingProfiles.GetProfiles()
            cboProfile.Items.Add(profile.Name)
        Next
        If cboProfile.Items.Count > 1 Then
            cboProfile.SelectedIndex = 1
        ElseIf cboProfile.Items.Count > 0 Then
            cboProfile.SelectedIndex = 0
        End If
        RefreshKeyStatus()
        RefreshDocuments()
    End Sub

    Private Sub RefreshKeyStatus()
        lstProviders.Items.Clear()
        For Each provider In New String() {"Groq", "Gemini", "OpenRouter", "OpenAI"}
            Dim status = If(AppServices.Security.HasProviderApiKey(provider), "محفوظ بأمان عبر DPAPI", "غير محفوظ - يمكن استخدام متغير البيئة")
            lstProviders.Items.Add(provider & " : " & status)
        Next
        lblVault.Text = "خزنة المفاتيح: " & AppServices.Security.KeyVaultFolder()
    End Sub

    Private Sub RefreshDocuments()
        Dim docs = AppServices.Repository.GetDocuments()
        cboDocuments.DataSource = Nothing
        cboDocuments.DisplayMember = "Title"
        cboDocuments.ValueMember = "Id"
        cboDocuments.DataSource = docs
        If docs.Count > 0 Then LoadSelectedDocument()
    End Sub

    Private Sub LoadSelectedDocument()
        If cboDocuments.SelectedValue Is Nothing Then Return
        _selectedDocument = AppServices.Repository.GetDocumentById(cboDocuments.SelectedValue.ToString())
        If _selectedDocument Is Nothing Then Return
        txtFilePath.Text = If(File.Exists(_selectedDocument.StoredPath), _selectedDocument.StoredPath, _selectedDocument.OriginalPath)
        ShowDocumentSecurity()
    End Sub

    Private Sub ShowDocumentSecurity()
        If _selectedDocument Is Nothing Then Return
        txtSecurityReport.Text = "العنوان: " & _selectedDocument.Title & Environment.NewLine &
            "بصمة الملف: " & If(_selectedDocument.FileSignatureVerified, "متحقق", "غير متحقق") & " - " & _selectedDocument.FileSignatureType & Environment.NewLine &
            "درجة المخاطر: " & CInt(_selectedDocument.SecurityRiskScore * 100).ToString() & "%" & Environment.NewLine &
            "الحالة الأمنية: " & _selectedDocument.SecurityStatus & Environment.NewLine &
            "النسخة المحمية: " & If(String.IsNullOrWhiteSpace(_selectedDocument.ProtectedFilePath), "لا توجد", _selectedDocument.ProtectedFilePath)
    End Sub

    Private Sub btnValidate_Click(sender As Object, e As EventArgs) Handles btnValidate.Click
        If String.IsNullOrWhiteSpace(txtFilePath.Text) Then Return
        Dim check = AppServices.Security.ValidateFileSignature(txtFilePath.Text)
        txtSecurityReport.Text = check.ToString()
        If _selectedDocument IsNot Nothing Then
            _selectedDocument.FileSignatureVerified = check.IsValid
            _selectedDocument.FileSignatureType = check.DetectedType
            _selectedDocument.SecurityStatus = check.Message
            _selectedDocument.SecurityRiskScore = AppServices.Security.CalculateRiskScore(_selectedDocument)
            AppServices.Repository.SaveDocument(_selectedDocument)
            If AppServices.EnterpriseDb IsNot Nothing Then AppServices.EnterpriseDb.UpsertDocument(_selectedDocument)
            ShowDocumentSecurity()
        End If
    End Sub

    Private Async Sub btnProtect_Click(sender As Object, e As EventArgs) Handles btnProtect.Click
        If _selectedDocument Is Nothing Then
            MessageBox.Show("اختر مستنداً أولاً.")
            Return
        End If
        Try
            Enabled = False
            Cursor = Cursors.WaitCursor
            Dim path = txtFilePath.Text
            Dim protectedPath = Await AppServices.Security.ProtectFileAsync(path, _selectedDocument.Id, CancellationToken.None)
            _selectedDocument.ProtectedFilePath = protectedPath
            _selectedDocument.IsEncrypted = True
            _selectedDocument.SecurityRiskScore = AppServices.Security.CalculateRiskScore(_selectedDocument)
            _selectedDocument.SecurityStatus = "تم إنشاء نسخة محمية AES-256 مع تحقق HMAC."
            AppServices.Repository.SaveDocument(_selectedDocument)
            If AppServices.EnterpriseDb IsNot Nothing Then AppServices.EnterpriseDb.UpsertDocument(_selectedDocument)
            AppServices.Repository.AddAudit("إنشاء نسخة محمية", _selectedDocument.Id, _selectedDocument.Title, protectedPath)
            ShowDocumentSecurity()
            MessageBox.Show("تم إنشاء النسخة المحمية بنجاح.", "الأمان المؤسسي", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر إنشاء النسخة المحمية", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Cursor = Cursors.Default
            Enabled = True
        End Try
    End Sub

    Private Async Sub btnRestore_Click(sender As Object, e As EventArgs) Handles btnRestore.Click
        If _selectedDocument Is Nothing OrElse String.IsNullOrWhiteSpace(_selectedDocument.ProtectedFilePath) OrElse Not File.Exists(_selectedDocument.ProtectedFilePath) Then
            MessageBox.Show("لا توجد نسخة محمية لهذا المستند.")
            Return
        End If
        Using sfd As New SaveFileDialog()
            sfd.Title = "استعادة نسخة قابلة للفتح"
            sfd.FileName = "مستعاد_" & _selectedDocument.FileName
            If sfd.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Enabled = False
                Await AppServices.Security.RestoreProtectedFileAsync(_selectedDocument.ProtectedFilePath, sfd.FileName, CancellationToken.None)
                MessageBox.Show("تمت الاستعادة بعد التحقق من سلامة الملف.", "الأمان المؤسسي", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show(ex.Message, "تعذر الاستعادة", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Enabled = True
            End Try
        End Using
    End Sub

    Private Async Sub btnRunPipeline_Click(sender As Object, e As EventArgs) Handles btnRunPipeline.Click
        If _selectedDocument Is Nothing Then
            MessageBox.Show("اختر مستنداً أولاً.", "المعالجة المؤسسية", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        _pipelineCts = New CancellationTokenSource()
        btnRunPipeline.Enabled = False
        btnCancelPipeline.Enabled = True
        progressPipeline.Value = 0
        lblPipelineStatus.Text = "بدأت المعالجة..."
        Dim progress = New Progress(Of EnterpriseProcessingProgress)(
            Sub(state)
                progressPipeline.Value = Math.Max(0, Math.Min(100, state.Percentage))
                lblPipelineStatus.Text = state.StatusMessage & " (" & state.Percentage.ToString() & "%)"
            End Sub)
        Try
            Dim profileName = If(cboProfile.SelectedItem Is Nothing, "المسار الإداري المتوازن", cboProfile.SelectedItem.ToString())
            Dim result = Await AppServices.EnterprisePipeline.ProcessDocumentAsync(_selectedDocument.Id, profileName, progress, _pipelineCts.Token)
            lblPipelineStatus.Text = result.Message
            _selectedDocument = AppServices.Repository.GetDocumentById(_selectedDocument.Id)
            ShowDocumentSecurity()
            MessageBox.Show(result.Message, "المعالجة المؤسسية", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As OperationCanceledException
            lblPipelineStatus.Text = "تم إلغاء المعالجة بناءً على طلب المستخدم."
        Catch ex As Exception
            lblPipelineStatus.Text = "تعذرت المعالجة: " & ex.Message
            MessageBox.Show(ex.Message, "المعالجة المؤسسية", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnRunPipeline.Enabled = True
            btnCancelPipeline.Enabled = False
            If _pipelineCts IsNot Nothing Then _pipelineCts.Dispose()
            _pipelineCts = Nothing
        End Try
    End Sub

    Private Sub btnCancelPipeline_Click(sender As Object, e As EventArgs) Handles btnCancelPipeline.Click
        If _pipelineCts IsNot Nothing Then
            btnCancelPipeline.Enabled = False
            lblPipelineStatus.Text = "جاري طلب الإلغاء..."
            _pipelineCts.Cancel()
        End If
    End Sub

    Private Sub btnSavePolicy_Click(sender As Object, e As EventArgs) Handles btnSavePolicy.Click
        AppServices.Settings.EnableFileSignatureValidation = chkValidateSignatures.Checked
        AppServices.Settings.EnableSensitiveDocumentEncryption = chkProtectSensitive.Checked
        AppServices.Settings.SensitiveDocumentRiskThresholdPercent = CInt(numRiskThreshold.Value)
        AppServices.Settings.Save()
        MessageBox.Show("تم حفظ سياسة الأمان.", "الأمان المؤسسي", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub cboDocuments_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboDocuments.SelectedIndexChanged
        LoadSelectedDocument()
    End Sub

    Private Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        RefreshKeyStatus()
        RefreshDocuments()
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Close()
    End Sub
End Class
