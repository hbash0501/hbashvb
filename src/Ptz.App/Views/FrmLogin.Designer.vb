Namespace Views
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmLogin
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblMode = New System.Windows.Forms.Label()
        Me.lblUserCap = New System.Windows.Forms.Label()
        Me.txtUser = New System.Windows.Forms.TextBox()
        Me.lblPassCap = New System.Windows.Forms.Label()
        Me.txtPass = New System.Windows.Forms.TextBox()
        Me.lblConfirmCap = New System.Windows.Forms.Label()
        Me.txtConfirm = New System.Windows.Forms.TextBox()
        Me.lblPolicy = New System.Windows.Forms.Label()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnSubmit = New System.Windows.Forms.Button()
        Me.btnExit = New System.Windows.Forms.Button()
        Me.lblRemoteHint = New System.Windows.Forms.Label()
        Me.btnRemote = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblTitle
        '
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Location = New System.Drawing.Point(24, 20)
        Me.lblTitle.AutoSize = True
        Me.lblTitle.Text = "منصة التحكم بكاميرات PTZ"
        '
        'lblMode
        '
        Me.lblMode.Name = "lblMode"
        Me.lblMode.Location = New System.Drawing.Point(24, 54)
        Me.lblMode.Size = New System.Drawing.Size(430, 44)
        Me.lblMode.Text = ""
        Me.lblMode.Tag = "muted"
        '
        'lblUserCap
        '
        Me.lblUserCap.Name = "lblUserCap"
        Me.lblUserCap.AutoSize = True
        Me.lblUserCap.Location = New System.Drawing.Point(24, 112)
        Me.lblUserCap.Text = "اسم المستخدم"
        '
        'txtUser
        '
        Me.txtUser.Name = "txtUser"
        Me.txtUser.Location = New System.Drawing.Point(24, 134)
        Me.txtUser.Size = New System.Drawing.Size(430, 23)
        Me.txtUser.MaxLength = 32
        '
        'lblPassCap
        '
        Me.lblPassCap.Name = "lblPassCap"
        Me.lblPassCap.AutoSize = True
        Me.lblPassCap.Location = New System.Drawing.Point(24, 172)
        Me.lblPassCap.Text = "كلمة المرور"
        '
        'txtPass
        '
        Me.txtPass.Name = "txtPass"
        Me.txtPass.Location = New System.Drawing.Point(24, 194)
        Me.txtPass.Size = New System.Drawing.Size(430, 23)
        Me.txtPass.UseSystemPasswordChar = True
        Me.txtPass.MaxLength = 128
        '
        'lblConfirmCap
        '
        Me.lblConfirmCap.Name = "lblConfirmCap"
        Me.lblConfirmCap.AutoSize = True
        Me.lblConfirmCap.Location = New System.Drawing.Point(24, 232)
        Me.lblConfirmCap.Text = "تأكيد كلمة المرور"
        Me.lblConfirmCap.Visible = False
        '
        'txtConfirm
        '
        Me.txtConfirm.Name = "txtConfirm"
        Me.txtConfirm.Location = New System.Drawing.Point(24, 254)
        Me.txtConfirm.Size = New System.Drawing.Size(430, 23)
        Me.txtConfirm.UseSystemPasswordChar = True
        Me.txtConfirm.MaxLength = 128
        Me.txtConfirm.Visible = False
        '
        'lblPolicy
        '
        Me.lblPolicy.Name = "lblPolicy"
        Me.lblPolicy.Location = New System.Drawing.Point(24, 288)
        Me.lblPolicy.Size = New System.Drawing.Size(430, 50)
        Me.lblPolicy.Text = ""
        Me.lblPolicy.Tag = "muted"
        Me.lblPolicy.Visible = False
        '
        'lblError
        '
        Me.lblError.Name = "lblError"
        Me.lblError.Location = New System.Drawing.Point(24, 340)
        Me.lblError.Size = New System.Drawing.Size(430, 40)
        Me.lblError.Text = ""
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(229, 72, 77)
        '
        'btnSubmit
        '
        Me.btnSubmit.Name = "btnSubmit"
        Me.btnSubmit.Location = New System.Drawing.Point(24, 392)
        Me.btnSubmit.Size = New System.Drawing.Size(210, 38)
        Me.btnSubmit.Text = "دخول"
        Me.btnSubmit.UseVisualStyleBackColor = True
        '
        'btnExit
        '
        Me.btnExit.Name = "btnExit"
        Me.btnExit.Location = New System.Drawing.Point(244, 392)
        Me.btnExit.Size = New System.Drawing.Size(210, 38)
        Me.btnExit.Text = "خروج"
        Me.btnExit.UseVisualStyleBackColor = True
        '
        'lblRemoteHint
        '
        Me.lblRemoteHint.Name = "lblRemoteHint"
        Me.lblRemoteHint.Location = New System.Drawing.Point(24, 446)
        Me.lblRemoteHint.Size = New System.Drawing.Size(430, 20)
        Me.lblRemoteHint.Text = "أو تحكّم عن بُعد عبر بوابة آمنة داخل شبكة الكاميرات:"
        Me.lblRemoteHint.Tag = "muted"
        '
        'btnRemote
        '
        Me.btnRemote.Name = "btnRemote"
        Me.btnRemote.Location = New System.Drawing.Point(24, 472)
        Me.btnRemote.Size = New System.Drawing.Size(430, 38)
        Me.btnRemote.Text = "اتصال ببوابة بعيدة…"
        Me.btnRemote.UseVisualStyleBackColor = True
        Me.Controls.Add(Me.lblTitle)
        Me.Controls.Add(Me.lblMode)
        Me.Controls.Add(Me.lblUserCap)
        Me.Controls.Add(Me.txtUser)
        Me.Controls.Add(Me.lblPassCap)
        Me.Controls.Add(Me.txtPass)
        Me.Controls.Add(Me.lblConfirmCap)
        Me.Controls.Add(Me.txtConfirm)
        Me.Controls.Add(Me.lblPolicy)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.btnSubmit)
        Me.Controls.Add(Me.btnExit)
        Me.Controls.Add(Me.lblRemoteHint)
        Me.Controls.Add(Me.btnRemote)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(480, 530)
        Me.Name = "FrmLogin"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "تسجيل الدخول"
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.AcceptButton = Me.btnSubmit
        Me.CancelButton = Me.btnExit
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents lblMode As System.Windows.Forms.Label
    Friend WithEvents lblUserCap As System.Windows.Forms.Label
    Friend WithEvents txtUser As System.Windows.Forms.TextBox
    Friend WithEvents lblPassCap As System.Windows.Forms.Label
    Friend WithEvents txtPass As System.Windows.Forms.TextBox
    Friend WithEvents lblConfirmCap As System.Windows.Forms.Label
    Friend WithEvents txtConfirm As System.Windows.Forms.TextBox
    Friend WithEvents lblPolicy As System.Windows.Forms.Label
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnSubmit As System.Windows.Forms.Button
    Friend WithEvents btnExit As System.Windows.Forms.Button
    Friend WithEvents lblRemoteHint As System.Windows.Forms.Label
    Friend WithEvents btnRemote As System.Windows.Forms.Button

End Class
End Namespace
