Imports System.Drawing
Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class FirstPasswordForm
    Inherits Form
    Private components As System.ComponentModel.IContainer
    Friend txtNew As TextBox
    Friend txtConfirm As TextBox
    Friend lblNote As Label
    Friend WithEvents btnSave As Button
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then components.Dispose()
        MyBase.Dispose(disposing)
    End Sub
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
        ClientSize = New Size(480, 305)
        RightToLeft = RightToLeft.Yes
        Font = New Font("Segoe UI", 11.0F)
        BackColor = BasiraTheme.Canvas
        Text = "بصيرة — تعيين كلمة مرور المدير"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        MaximizeBox = False
        MinimizeBox = False
        Dim title As New Label With {.Text = "بداية جديدة وآمنة", .Location = New Point(24, 20), .Size = New Size(432, 35), .Font = New Font("Segoe UI", 18.0F, FontStyle.Bold), .ForeColor = BasiraTheme.Ink}
        lblNote = New Label With {.Text = "عيّن كلمة مرور جديدة للحساب الافتراضي قبل الدخول.", .Location = New Point(24, 65), .Size = New Size(432, 45), .ForeColor = BasiraTheme.Muted}
        txtNew = New TextBox With {.Location = New Point(24, 122), .Size = New Size(432, 32), .UseSystemPasswordChar = True}
        txtConfirm = New TextBox With {.Location = New Point(24, 168), .Size = New Size(432, 32), .UseSystemPasswordChar = True}
        btnSave = BasiraTheme.Button("حفظ ودخول", True)
        btnSave.Location = New Point(292, 224)
        Controls.AddRange(New Control() {title, lblNote, txtNew, txtConfirm, btnSave})
        AcceptButton = btnSave
    End Sub
End Class
