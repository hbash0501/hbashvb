Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Security
Imports Ptz.Presentation

Namespace Views

    ''' <summary>الشاشة الرئيسية: إعداد اتصال كل كاميرا وفتح نافذتها. كل بطاقة مستقلة بإعداداتها.</summary>
    Public Class FrmHub
        Implements IHubView

        Private NotInheritable Class Card
            Public Key As String
            Public Name, Host, User, Pass, Fingerprint As TextBox
            Public Port As NumericUpDown
            Public Https, Insecure As CheckBox
            Public Fetch, Save, Open As Button
            Public Group As GroupBox
            Public Status As Label
        End Class

        Private ReadOnly _cards As New Dictionary(Of String, Card)()
        Private ReadOnly _dispatcher As IUiDispatcher

        Public Event OpenCameraRequested As Action(Of String) Implements IHubView.OpenCameraRequested
        Public Event SaveConfigRequested As Action(Of CameraConfig, String) Implements IHubView.SaveConfigRequested
        Public Event FetchCertificateRequested As Action(Of String) Implements IHubView.FetchCertificateRequested
        Public Event ManageUsersRequested As Action Implements IHubView.ManageUsersRequested
        Public Event ViewAuditRequested As Action Implements IHubView.ViewAuditRequested
        Public Event LogoutRequested As Action Implements IHubView.LogoutRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
            lblTitle.Font = Theme.TitleFont
            _dispatcher = New WinFormsDispatcher(Me)
            RegisterCard(HubPresenter.KeySe7c432mw, grpSe, lblModelSe, txtNameSe, txtHostSe, nudPortSe, chkHttpsSe, chkInsecureSe, txtUserSe, txtPassSe, txtFpSe, btnFetchSe, btnSaveSe, btnOpenSe)
            RegisterCard(HubPresenter.KeyDe7232iw, grpDe, lblModelDe, txtNameDe, txtHostDe, nudPortDe, chkHttpsDe, chkInsecureDe, txtUserDe, txtPassDe, txtFpDe, btnFetchDe, btnSaveDe, btnOpenDe)
        End Sub

        Public ReadOnly Property Dispatcher As IUiDispatcher
            Get
                Return _dispatcher
            End Get
        End Property

        Private Sub RegisterCard(key As String, grp As GroupBox, status As Label, name As TextBox, host As TextBox, port As NumericUpDown, https As CheckBox,
                                 insecure As CheckBox, user As TextBox, pass As TextBox, fp As TextBox, fetch As Button, save As Button, open As Button)
            Dim c As New Card With {.Key = key, .Group = grp, .Status = status, .Name = name, .Host = host, .Port = port, .Https = https, .Insecure = insecure,
                                    .User = user, .Pass = pass, .Fingerprint = fp, .Fetch = fetch, .Save = save, .Open = open}
            _cards(key) = c
            AddHandler open.Click, Sub() RaiseEvent OpenCameraRequested(key)
            AddHandler fetch.Click, Sub() RaiseEvent FetchCertificateRequested(key)
            AddHandler save.Click, Sub() OnSave(c)
            AddHandler https.CheckedChanged, Sub()
                                                 insecure.Enabled = Not https.Checked
                                                 If https.Checked Then insecure.Checked = False
                                                 fetch.Enabled = https.Checked
                                             End Sub
        End Sub

        Private Sub OnSave(c As Card)
            Dim cfg As New CameraConfig With {
                .Key = c.Key, .DisplayName = c.Name.Text.Trim(), .Host = c.Host.Text.Trim(), .Port = CInt(c.Port.Value),
                .UseHttps = c.Https.Checked, .AllowInsecureHttp = c.Insecure.Checked, .Username = c.User.Text.Trim(),
                .PinnedCertSha256 = c.Fingerprint.Text.Trim()}
            RaiseEvent SaveConfigRequested(cfg, c.Pass.Text)
            c.Pass.Clear()
        End Sub

        Public Sub SetUser(userName As String, role As String) Implements IHubView.SetUser
            lblSession.Text = $"المستخدم: {userName} — الدور: {role}"
        End Sub

        Public Sub ApplyPermissions(permissions As Permission) Implements IHubView.ApplyPermissions
            btnUsers.Enabled = (permissions And Permission.ManageUsers) <> 0
            btnAudit.Enabled = (permissions And Permission.ViewAudit) <> 0
            Dim canConfigure As Boolean = (permissions And Permission.ManageConnections) <> 0
            For Each c As Card In _cards.Values
                c.Save.Enabled = canConfigure
                c.Fetch.Enabled = canConfigure AndAlso c.Https.Checked
                For Each t As Control In {c.Name, c.Host, c.User, c.Pass}
                    t.Enabled = canConfigure
                Next
                c.Port.Enabled = canConfigure
                c.Https.Enabled = canConfigure
                c.Open.Enabled = (permissions And Permission.ViewLive) <> 0
            Next
        End Sub

        Public Sub ShowCameras(cameras As IReadOnlyList(Of CameraConfig)) Implements IHubView.ShowCameras
            For Each cfg As CameraConfig In cameras
                Dim c As Card = Nothing
                If Not _cards.TryGetValue(cfg.Key, c) Then Continue For
                c.Name.Text = cfg.DisplayName
                c.Host.Text = cfg.Host
                c.Port.Value = Math.Min(c.Port.Maximum, Math.Max(c.Port.Minimum, cfg.Port))
                c.Https.Checked = cfg.UseHttps
                c.Insecure.Checked = cfg.AllowInsecureHttp
                c.User.Text = cfg.Username
                c.Fingerprint.Text = cfg.PinnedCertSha256
                c.Pass.PlaceholderText = If(cfg.PasswordProtected <> "", "●●●●●●●● (محفوظة — اتركها فارغة للإبقاء عليها)", "أدخل كلمة المرور")
            Next
        End Sub

        Public Sub ShowCertificateFingerprint(key As String, sha256 As String) Implements IHubView.ShowCertificateFingerprint
            _dispatcher.Post(Sub()
                                 Dim msg As String = "بصمة SHA-256 لشهادة الكاميرا:" & vbCrLf & vbCrLf & sha256 & vbCrLf & vbCrLf &
                                     "تحقّق منها من واجهة الكاميرا أو من مصدر موثوق قبل التثبيت. تثبيتها يمنع هجمات الوسيط." & vbCrLf & "تثبيت هذه البصمة؟"
                                 If MessageBox.Show(Me, msg, "تأكيد شهادة الكاميرا", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                                     _cards(key).Fingerprint.Text = sha256
                                     OnSave(_cards(key))
                                 End If
                             End Sub)
        End Sub

        ''' <summary>وضع البوابة: إعدادات الاتصال بالكاميرات تُدار على البوابة (أداة PtzGateway)، فلا تُعرض هنا حقولها ولا إدارة المستخدمين.</summary>
        Public Sub SetGatewayMode(endpointText As String) Implements IHubView.SetGatewayMode
            lblTitle.Text = UiText.AppTitle & " — عبر البوابة " & endpointText
            btnUsers.Visible = False
            For Each c As Card In _cards.Values
                For Each ctl As Control In c.Group.Controls
                    If ctl IsNot c.Open AndAlso ctl IsNot c.Status Then ctl.Visible = False
                Next
                c.Open.Location = New System.Drawing.Point(14, 64)
                c.Open.Width = c.Group.Width - 28
                c.Status.Location = New System.Drawing.Point(14, 32)
                c.Status.Visible = True
                c.Group.Height = 120
            Next
            MinimumSize = New System.Drawing.Size(MinimumSize.Width, 330)
            ClientSize = New System.Drawing.Size(ClientSize.Width, 330)
        End Sub

        Public Sub SetCameraStatus(key As String, text As String, online As Boolean) Implements IHubView.SetCameraStatus
            _dispatcher.Post(Sub()
                                 Dim c As Card = Nothing
                                 If Not _cards.TryGetValue(key, c) Then Return
                                 c.Status.Text = text
                                 c.Status.ForeColor = If(online, Theme.Ok, Theme.Danger)
                             End Sub)
        End Sub

        Public Sub ShowError(message As String) Implements IMessageSink.ShowError
            _dispatcher.Post(Sub() MessageBox.Show(Me, message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error))
        End Sub

        Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
            _dispatcher.Post(Sub() lblSession.Text = message)
        End Sub

        Private Sub btnUsers_Click(sender As Object, e As EventArgs) Handles btnUsers.Click
            RaiseEvent ManageUsersRequested()
        End Sub

        Private Sub btnAudit_Click(sender As Object, e As EventArgs) Handles btnAudit.Click
            RaiseEvent ViewAuditRequested()
        End Sub

        Private Sub btnLogout_Click(sender As Object, e As EventArgs) Handles btnLogout.Click
            RaiseEvent LogoutRequested()
        End Sub
    End Class

End Namespace
