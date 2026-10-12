Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Presentation

Namespace Views

    ''' <summary>نافذة الاتصال ببوابة بعيدة. بلا منطق؛ المقدِّم GatewayLoginPresenter يقرّر.</summary>
    Public Class FrmGatewayLogin
        Implements IGatewayLoginView

        Public Event ConnectRequested As Action(Of GatewayLoginInput) Implements IGatewayLoginView.ConnectRequested
        Public Event FetchCertificateRequested As Action(Of String) Implements IGatewayLoginView.FetchCertificateRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
            lblTitle.Font = Theme.TitleFont
        End Sub

        Public Sub SetSettings(settings As GatewaySettings) Implements IGatewayLoginView.SetSettings
            txtUrl.Text = settings.BaseUrl
            txtPin.Text = settings.PinnedCert
            chkLoopback.Checked = settings.AllowInsecureLoopbackHttp
        End Sub

        Public Sub SetBusy(busy As Boolean) Implements IGatewayLoginView.SetBusy
            btnConnect.Enabled = Not busy
            btnFetch.Enabled = Not busy
            UseWaitCursor = busy
        End Sub

        Public Sub OfferCertificate(host As String, certSha256 As String) Implements IGatewayLoginView.OfferCertificate
            Dim msg As String = $"بصمة SHA-256 لشهادة {host}:" & vbCrLf & vbCrLf & certSha256 & vbCrLf & vbCrLf &
                "قارنها حرفاً بحرف بمخرجات الأمر «PtzGateway cert show» على مضيف البوابة (سطر cert-sha256) قبل القبول." & vbCrLf &
                "تثبيتها يمنع هجمات الوسيط. تثبيت هذه البصمة؟"
            If MessageBox.Show(Me, msg, "تأكيد شهادة البوابة", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                txtPin.Text = "cert-sha256:" & certSha256
            End If
        End Sub

        Public Sub CloseWithSuccess() Implements IGatewayLoginView.CloseWithSuccess
            DialogResult = DialogResult.OK
            Close()
        End Sub

        Public Sub ShowError(message As String) Implements IMessageSink.ShowError
            lblError.Text = message
            txtPass.Clear()
            txtTotp.Clear()
            txtPass.Focus()
        End Sub

        Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
            lblError.Text = message
        End Sub

        Private Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
            lblError.Text = ""
            RaiseEvent ConnectRequested(New GatewayLoginInput With {
                .Url = txtUrl.Text.Trim(), .Pin = txtPin.Text.Trim(), .AllowLoopbackHttp = chkLoopback.Checked,
                .UserName = txtUser.Text.Trim(), .Password = txtPass.Text, .Totp = txtTotp.Text.Trim()})
        End Sub

        Private Sub btnFetch_Click(sender As Object, e As EventArgs) Handles btnFetch.Click
            RaiseEvent FetchCertificateRequested(txtUrl.Text.Trim())
        End Sub

        Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
            DialogResult = DialogResult.Cancel
            Close()
        End Sub
    End Class

End Namespace
