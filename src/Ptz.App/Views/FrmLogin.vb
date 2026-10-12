Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Presentation

Namespace Views

    ''' <summary>نافذة الدخول (وإنشاء المدير عند أول تشغيل). بلا منطق؛ المقدِّم LoginPresenter يقرّر.</summary>
    Public Class FrmLogin
        Implements ILoginView

        Public Event SubmitRequested As Action(Of String, String, String) Implements ILoginView.SubmitRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
            lblTitle.Font = Theme.TitleFont
        End Sub

        Public Sub SetMode(mode As LoginMode) Implements ILoginView.SetMode
            Dim first As Boolean = (mode = LoginMode.FirstRunCreateAdmin)
            lblConfirmCap.Visible = first
            txtConfirm.Visible = first
            lblPolicy.Visible = first
            lblMode.Text = If(first, "أول تشغيل: أنشئ حساب المدير. لا توجد كلمات مرور افتراضية في هذا النظام.", "سجّل الدخول للمتابعة.")
            lblPolicy.Text = "كلمة المرور: 12 حرفاً على الأقل، حرف كبير وصغير ورقم ورمز، ولا تحوي اسم المستخدم."
            btnSubmit.Text = If(first, "إنشاء الحساب والدخول", "دخول")
        End Sub

        Public Sub SetBusy(busy As Boolean) Implements ILoginView.SetBusy
            btnSubmit.Enabled = Not busy
            UseWaitCursor = busy
        End Sub

        Public Sub CloseWithSuccess() Implements ILoginView.CloseWithSuccess
            DialogResult = DialogResult.OK
            Close()
        End Sub

        Public Sub ShowError(message As String) Implements IMessageSink.ShowError
            lblError.Text = message
            txtPass.Clear()
            txtConfirm.Clear()
            txtPass.Focus()
        End Sub

        Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
            lblError.Text = message
        End Sub

        Private Sub btnSubmit_Click(sender As Object, e As EventArgs) Handles btnSubmit.Click
            lblError.Text = ""
            RaiseEvent SubmitRequested(txtUser.Text.Trim(), txtPass.Text, txtConfirm.Text)
        End Sub

        Private Sub btnExit_Click(sender As Object, e As EventArgs) Handles btnExit.Click
            DialogResult = DialogResult.Cancel
            Close()
        End Sub
    End Class

End Namespace
