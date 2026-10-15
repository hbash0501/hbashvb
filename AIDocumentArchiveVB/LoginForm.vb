Imports System
Imports System.Windows.Forms

Partial Public Class LoginForm
    Public Sub New()
        InitializeComponent()
        Me.Text = "بصيرة | تسجيل الدخول"
        UiThemeService.StylePrimaryButton(btnLogin, "green")
    End Sub

    Private Sub btnLogin_Click(sender As Object, e As EventArgs) Handles btnLogin.Click
        If AppServices.Auth.Authenticate(txtUserName.Text, txtPassword.Text) Then
            If txtUserName.Text.Trim().ToLowerInvariant() = "admin" AndAlso txtPassword.Text = "admin123" Then
                Using passwordForm As New FirstPasswordForm()
                    If passwordForm.ShowDialog(Me) <> DialogResult.OK Then
                        AppServices.Auth.CurrentUser = Nothing
                        Return
                    End If
                    AppServices.Auth.SaveUser(AppServices.Auth.CurrentUser, passwordForm.PasswordText)
                End Using
            End If
            AppServices.Repository.AddAudit("دخول للنظام", "", "", "المستخدم: " & AppServices.Auth.CurrentUser.FullName)
            Me.DialogResult = DialogResult.OK
            Me.Close()
        Else
            MessageBox.Show("اسم المستخدم أو كلمة المرور غير صحيحة، أو أن الحساب غير نشط.", "تعذر الدخول", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtPassword.SelectAll()
            txtPassword.Focus()
        End If
    End Sub

    Private Sub btnExit_Click(sender As Object, e As EventArgs) Handles btnExit.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
End Class
