Imports System.Windows.Forms
Imports Ptz.App.Infrastructure

Namespace Views

    ''' <summary>نافذة صغيرة تطلب رمز TOTP جديداً لإجراء حساس. تُرجع الرمز أو Nothing عند الإلغاء.</summary>
    Public Class FrmStepUp

        Public Sub New(reason As String)
            InitializeComponent()
            Theme.Apply(Me)
            lblReason.Text = reason
        End Sub

        Public ReadOnly Property Code As String
            Get
                Return txtCode.Text.Trim()
            End Get
        End Property

        Private Sub btnOk_Click(sender As Object, e As EventArgs) Handles btnOk.Click
            If System.Text.RegularExpressions.Regex.IsMatch(Code, "^\d{6}\z") Then
                DialogResult = DialogResult.OK
                Close()
            Else
                lblCodeCap.Text = "الرمز 6 أرقام بالضبط:"
                txtCode.Focus()
            End If
        End Sub

        Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
            DialogResult = DialogResult.Cancel
            Close()
        End Sub
    End Class

End Namespace
