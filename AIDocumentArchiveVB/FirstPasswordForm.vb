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
        Dim reason = PasswordPolicy.Validate(txtNew.Text, "admin")
        If reason <> "" Then
            lblNote.Text = reason
            Return
        End If
        If txtNew.Text <> txtConfirm.Text Then
            lblNote.Text = "تأكيد كلمة المرور غير مطابق."
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class
