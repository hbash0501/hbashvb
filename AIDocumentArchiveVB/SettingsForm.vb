
Imports System
Imports System.IO
Imports System.Windows.Forms

Partial Public Class SettingsForm
    Private Const SecureKeyPlaceholder As String = "•••••••• (محفوظ بأمان)"
    Private txtOpenAiApiKeyRuntime As TextBox
    Private cboAiProviderRuntime As ComboBox
    Private lblProviderRuntime As Label
    Private lblApiKeyRuntime As Label
    Private lblEnvRuntime As Label
    Private chkFailoverRuntime As CheckBox
    Private txtProviderPriorityRuntime As TextBox
    Private chkAutomationRuntime As CheckBox
    Private numQualityRuntime As NumericUpDown
    Private numTimeoutRuntime As NumericUpDown

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub SettingsForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        EnsureCloudAiControls()
        Dim s = AppServices.Settings
        txtArchive.Text = s.ArchiveRootFolder
        txtTesseract.Text = s.TesseractExePath
        txtOcrLang.Text = s.OcrLanguage
        txtEmployee.Text = s.CurrentEmployeeName
        cboAiProviderRuntime.Text = If(String.IsNullOrWhiteSpace(s.AiProvider), "Groq", s.AiProvider)
        txtOpenAiEndpoint.Text = If(String.IsNullOrWhiteSpace(s.CloudAiEndpoint), s.OpenAiEndpoint, s.CloudAiEndpoint)
        txtOpenAiModel.Text = If(String.IsNullOrWhiteSpace(s.CloudAiModel), s.OpenAiModel, s.CloudAiModel)
        chkOpenAI.Checked = s.EnableOpenAI
        txtOpenAiApiKeyRuntime.Text = If(AppServices.Security IsNot Nothing AndAlso AppServices.Security.HasProviderApiKey(cboAiProviderRuntime.Text), SecureKeyPlaceholder, "")
        chkFailoverRuntime.Checked = s.EnableAiFailover
        txtProviderPriorityRuntime.Text = If(String.IsNullOrWhiteSpace(s.AiProviderPriority), "Groq;Gemini;OpenRouter", s.AiProviderPriority)
        chkAutomationRuntime.Checked = s.EnableAutomaticProcessing
        numQualityRuntime.Value = Math.Max(numQualityRuntime.Minimum, Math.Min(numQualityRuntime.Maximum, CDec(s.MinimumQualityScoreForApproval)))
        numTimeoutRuntime.Value = Math.Max(numTimeoutRuntime.Minimum, Math.Min(numTimeoutRuntime.Maximum, CDec(s.AiRequestTimeoutSeconds)))
        UpdateProviderHelp(False)
    End Sub

    Private Sub btnBrowseTess_Click(sender As Object, e As EventArgs) Handles btnBrowseTess.Click
        Using ofd As New OpenFileDialog()
            ofd.Title = "اختر tesseract.exe"
            ofd.Filter = "Tesseract|tesseract.exe|كل الملفات|*.*"
            If File.Exists(txtTesseract.Text) Then ofd.InitialDirectory = Path.GetDirectoryName(txtTesseract.Text)
            If ofd.ShowDialog(Me) = DialogResult.OK Then txtTesseract.Text = ofd.FileName
        End Using
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        Dim s = AppServices.Settings
        Dim provider = If(cboAiProviderRuntime Is Nothing OrElse String.IsNullOrWhiteSpace(cboAiProviderRuntime.Text), "Groq", cboAiProviderRuntime.Text.Trim())
        Dim endpoint = If(String.IsNullOrWhiteSpace(txtOpenAiEndpoint.Text), OpenAIClientService.DefaultEndpoint(provider), txtOpenAiEndpoint.Text.Trim())
        Dim model = If(String.IsNullOrWhiteSpace(txtOpenAiModel.Text), OpenAIClientService.DefaultModel(provider), txtOpenAiModel.Text.Trim())

        s.ArchiveRootFolder = If(String.IsNullOrWhiteSpace(txtArchive.Text), s.ArchiveRootFolder, txtArchive.Text.Trim())
        s.TesseractExePath = txtTesseract.Text.Trim()
        s.OcrLanguage = If(String.IsNullOrWhiteSpace(txtOcrLang.Text), "ara+eng", txtOcrLang.Text.Trim())
        s.CurrentEmployeeName = If(String.IsNullOrWhiteSpace(txtEmployee.Text), Environment.UserName, txtEmployee.Text.Trim())
        s.EnableOpenAI = chkOpenAI.Checked
        s.AiProvider = provider
        s.CloudAiEndpoint = endpoint
        s.CloudAiModel = model
        Dim enteredKey As String = If(txtOpenAiApiKeyRuntime Is Nothing, "", txtOpenAiApiKeyRuntime.Text.Trim())
        If enteredKey <> "" AndAlso enteredKey <> SecureKeyPlaceholder Then AppServices.Security.SaveProviderApiKey(provider, enteredKey)
        s.CloudAiApiKey = ""
        s.CloudAiApiKeyEnvironmentName = DefaultEnvName(provider)
        s.EnableAiFailover = chkFailoverRuntime.Checked
        s.AiProviderPriority = If(String.IsNullOrWhiteSpace(txtProviderPriorityRuntime.Text), "Groq;Gemini;OpenRouter", txtProviderPriorityRuntime.Text.Trim())
        s.EnableAutomaticProcessing = chkAutomationRuntime.Checked
        s.MinimumQualityScoreForApproval = CInt(numQualityRuntime.Value)
        s.AiRequestTimeoutSeconds = CInt(numTimeoutRuntime.Value)

        ' إبقاء الخصائص القديمة محدثة لتوافق الخدمات التي لم تُعاد تسميتها.
        s.OpenAiEndpoint = endpoint
        s.OpenAiModel = model
        s.OpenAiApiKey = ""
        s.Save()
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub btnTestOpenAI_Click(sender As Object, e As EventArgs) Handles btnTestOpenAI.Click
        Try
            Dim provider = If(cboAiProviderRuntime Is Nothing OrElse String.IsNullOrWhiteSpace(cboAiProviderRuntime.Text), "Groq", cboAiProviderRuntime.Text.Trim())
            Dim temp As New AppSettings With {
                .BaseDataFolder = AppServices.Settings.BaseDataFolder,
                .ArchiveRootFolder = AppServices.Settings.ArchiveRootFolder,
                .TesseractExePath = txtTesseract.Text.Trim(),
                .OcrLanguage = If(String.IsNullOrWhiteSpace(txtOcrLang.Text), "ara+eng", txtOcrLang.Text.Trim()),
                .CurrentEmployeeName = If(String.IsNullOrWhiteSpace(txtEmployee.Text), Environment.UserName, txtEmployee.Text.Trim()),
                .EnableRuleBasedAi = True,
                .EnableOpenAI = True,
                .AiProvider = provider,
                .CloudAiEndpoint = If(String.IsNullOrWhiteSpace(txtOpenAiEndpoint.Text), OpenAIClientService.DefaultEndpoint(provider), txtOpenAiEndpoint.Text.Trim()),
                .CloudAiModel = If(String.IsNullOrWhiteSpace(txtOpenAiModel.Text), OpenAIClientService.DefaultModel(provider), txtOpenAiModel.Text.Trim()),
                .CloudAiApiKey = If(txtOpenAiApiKeyRuntime Is Nothing OrElse txtOpenAiApiKeyRuntime.Text.Trim() = SecureKeyPlaceholder, "", txtOpenAiApiKeyRuntime.Text.Trim()),
                .CloudAiApiKeyEnvironmentName = DefaultEnvName(provider),
                .OpenAiEndpoint = If(String.IsNullOrWhiteSpace(txtOpenAiEndpoint.Text), OpenAIClientService.DefaultEndpoint(provider), txtOpenAiEndpoint.Text.Trim()),
                .OpenAiModel = If(String.IsNullOrWhiteSpace(txtOpenAiModel.Text), OpenAIClientService.DefaultModel(provider), txtOpenAiModel.Text.Trim()),
                .OpenAiApiKey = If(txtOpenAiApiKeyRuntime Is Nothing OrElse txtOpenAiApiKeyRuntime.Text.Trim() = SecureKeyPlaceholder, "", txtOpenAiApiKeyRuntime.Text.Trim())
            }
            Dim client As New OpenAIClientService(temp)
            Dim answer = client.TestConnection()
            MessageBox.Show("رد مزود الذكاء السحابي (" & client.ProviderDisplayName() & "):" & Environment.NewLine & answer, "اختبار الذكاء السحابي", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("تعذر الاتصال بمزود الذكاء السحابي:" & Environment.NewLine & ex.Message, "اختبار الذكاء السحابي", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub EnsureCloudAiControls()
        If cboAiProviderRuntime IsNot Nothing Then Return

        lblProviderRuntime = New Label()
        lblProviderRuntime.Name = "lblProviderRuntime"
        lblProviderRuntime.Text = "مزود الذكاء السحابي"
        lblProviderRuntime.TextAlign = Drawing.ContentAlignment.MiddleRight
        lblProviderRuntime.Location = New Drawing.Point(24, 213)
        lblProviderRuntime.Size = New Drawing.Size(712, 22)
        lblProviderRuntime.Font = New Drawing.Font("Tahoma", 8.5!, Drawing.FontStyle.Bold)

        cboAiProviderRuntime = New ComboBox()
        cboAiProviderRuntime.Name = "cboAiProviderRuntime"
        cboAiProviderRuntime.DropDownStyle = ComboBoxStyle.DropDownList
        cboAiProviderRuntime.Items.AddRange(New Object() {"Groq", "Gemini", "OpenRouter", "OpenAI", "OpenAI-Compatible"})
        cboAiProviderRuntime.Location = New Drawing.Point(24, 238)
        cboAiProviderRuntime.Size = New Drawing.Size(180, 26)
        AddHandler cboAiProviderRuntime.SelectedIndexChanged, AddressOf ProviderChanged

        lblOpenAiEndpoint.Text = "رابط API"
        lblOpenAiEndpoint.Location = New Drawing.Point(24, 270)
        txtOpenAiEndpoint.Location = New Drawing.Point(24, 294)
        lblOpenAiModel.Text = "اسم النموذج"
        lblOpenAiModel.Location = New Drawing.Point(24, 326)
        txtOpenAiModel.Location = New Drawing.Point(24, 350)

        lblApiKeyRuntime = New Label()
        lblApiKeyRuntime.Name = "lblCloudAiApiKeyRuntime"
        lblApiKeyRuntime.Text = "مفتاح API - يحفظ مشفراً لحساب ويندوز"
        lblApiKeyRuntime.TextAlign = Drawing.ContentAlignment.MiddleRight
        lblApiKeyRuntime.Location = New Drawing.Point(24, 382)
        lblApiKeyRuntime.Size = New Drawing.Size(712, 22)
        lblApiKeyRuntime.Font = New Drawing.Font("Tahoma", 8.5!, Drawing.FontStyle.Bold)

        txtOpenAiApiKeyRuntime = New TextBox()
        txtOpenAiApiKeyRuntime.Name = "txtCloudAiApiKeyRuntime"
        txtOpenAiApiKeyRuntime.Location = New Drawing.Point(24, 406)
        txtOpenAiApiKeyRuntime.Size = New Drawing.Size(712, 26)
        txtOpenAiApiKeyRuntime.PasswordChar = "*"c
        txtOpenAiApiKeyRuntime.RightToLeft = RightToLeft.No

        lblEnvRuntime = New Label()
        lblEnvRuntime.Name = "lblCloudAiEnvRuntime"
        lblEnvRuntime.Text = ""
        lblEnvRuntime.Location = New Drawing.Point(24, 436)
        lblEnvRuntime.Size = New Drawing.Size(712, 22)
        lblEnvRuntime.ForeColor = Drawing.Color.DimGray
        lblEnvRuntime.TextAlign = Drawing.ContentAlignment.MiddleRight

        chkOpenAI.Text = "تفعيل الذكاء السحابي"
        chkOpenAI.Top = 462
        btnTestOpenAI.Text = "اختبار المزود"
        btnTestOpenAI.Top = 456

        chkFailoverRuntime = New CheckBox()
        chkFailoverRuntime.Text = "التبديل التلقائي عند تعذر المزود"
        chkFailoverRuntime.Location = New Drawing.Point(430, 492)
        chkFailoverRuntime.Size = New Drawing.Size(306, 24)

        Dim lblPriority As New Label()
        lblPriority.Text = "ترتيب المزودات (يفصل بينها ;)"
        lblPriority.Location = New Drawing.Point(24, 520)
        lblPriority.Size = New Drawing.Size(712, 22)
        lblPriority.TextAlign = Drawing.ContentAlignment.MiddleRight
        txtProviderPriorityRuntime = New TextBox()
        txtProviderPriorityRuntime.Location = New Drawing.Point(24, 544)
        txtProviderPriorityRuntime.Size = New Drawing.Size(712, 26)
        txtProviderPriorityRuntime.RightToLeft = RightToLeft.No

        chkAutomationRuntime = New CheckBox()
        chkAutomationRuntime.Text = "تفعيل المعالجة الخلفية التلقائية بعد الإدراج"
        chkAutomationRuntime.Location = New Drawing.Point(430, 578)
        chkAutomationRuntime.Size = New Drawing.Size(306, 24)

        Dim lblQuality As New Label()
        lblQuality.Text = "الحد الأدنى للجودة قبل الاعتماد"
        lblQuality.Location = New Drawing.Point(400, 610)
        lblQuality.Size = New Drawing.Size(336, 22)
        lblQuality.TextAlign = Drawing.ContentAlignment.MiddleRight
        numQualityRuntime = New NumericUpDown()
        numQualityRuntime.Minimum = 50
        numQualityRuntime.Maximum = 100
        numQualityRuntime.Location = New Drawing.Point(620, 636)
        numQualityRuntime.Size = New Drawing.Size(116, 26)

        Dim lblTimeout As New Label()
        lblTimeout.Text = "مهلة طلب الذكاء بالثواني"
        lblTimeout.Location = New Drawing.Point(24, 610)
        lblTimeout.Size = New Drawing.Size(336, 22)
        lblTimeout.TextAlign = Drawing.ContentAlignment.MiddleRight
        numTimeoutRuntime = New NumericUpDown()
        numTimeoutRuntime.Minimum = 10
        numTimeoutRuntime.Maximum = 180
        numTimeoutRuntime.Location = New Drawing.Point(24, 636)
        numTimeoutRuntime.Size = New Drawing.Size(116, 26)

        contentPanel.Controls.Add(lblProviderRuntime)
        contentPanel.Controls.Add(cboAiProviderRuntime)
        contentPanel.Controls.Add(lblApiKeyRuntime)
        contentPanel.Controls.Add(txtOpenAiApiKeyRuntime)
        contentPanel.Controls.Add(lblEnvRuntime)
        contentPanel.Controls.Add(chkFailoverRuntime)
        contentPanel.Controls.Add(lblPriority)
        contentPanel.Controls.Add(txtProviderPriorityRuntime)
        contentPanel.Controls.Add(chkAutomationRuntime)
        contentPanel.Controls.Add(lblQuality)
        contentPanel.Controls.Add(numQualityRuntime)
        contentPanel.Controls.Add(lblTimeout)
        contentPanel.Controls.Add(numTimeoutRuntime)
        If contentPanel.Height < 690 Then contentPanel.Height = 690
        Me.Height = 790
        footerPanel.Height = 62
    End Sub

    Private Sub ProviderChanged(sender As Object, e As EventArgs)
        UpdateProviderHelp(True)
    End Sub

    Private Sub UpdateProviderHelp(applyDefaults As Boolean)
        If cboAiProviderRuntime Is Nothing Then Return
        Dim provider = If(String.IsNullOrWhiteSpace(cboAiProviderRuntime.Text), "Groq", cboAiProviderRuntime.Text)
        If applyDefaults OrElse String.IsNullOrWhiteSpace(txtOpenAiEndpoint.Text) Then txtOpenAiEndpoint.Text = OpenAIClientService.DefaultEndpoint(provider)
        If applyDefaults OrElse String.IsNullOrWhiteSpace(txtOpenAiModel.Text) Then txtOpenAiModel.Text = OpenAIClientService.DefaultModel(provider)
        lblEnvRuntime.Text = "يمكن ترك المفتاح فارغاً إذا كان مضبوطاً في ويندوز باسم: " & DefaultEnvName(provider)
        If txtOpenAiApiKeyRuntime IsNot Nothing Then
            txtOpenAiApiKeyRuntime.Text = If(AppServices.Security IsNot Nothing AndAlso AppServices.Security.HasProviderApiKey(provider), SecureKeyPlaceholder, "")
        End If
    End Sub

    Private Shared Function DefaultEnvName(provider As String) As String
        Select Case OpenAIClientService.NormalizeProvider(provider)
            Case "groq"
                Return "GROQ_API_KEY"
            Case "gemini"
                Return "GEMINI_API_KEY"
            Case "openrouter"
                Return "OPENROUTER_API_KEY"
            Case "openai"
                Return "OPENAI_API_KEY"
            Case Else
                Return "AI_API_KEY"
        End Select
    End Function

End Class
