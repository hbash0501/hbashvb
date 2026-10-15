Imports System
Imports System.Windows.Forms

Partial Public Class FirstPasswordForm
    Public ReadOnly Property PasswordText As String
        Get
            Return txtNew.Text
        End Get
    End Property
    Public Sub New()
        InitializeComponent()
    End Sub
    Private Sub SaveClick(sender As Object, e As EventArgs) Handles btnSave.Click
        If txtNew.Text.Length < 10 OrElse txtNew.Text = "admin123" OrElse txtNew.Text <> txtConfirm.Text Then
            lblNote.Text = "اكتب كلمة مرور من 10 أحرف على الأقل وأكدها بالنص نفسه."
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class
