Namespace Views
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmGatewayLogin
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
        Me.lblInfo = New System.Windows.Forms.Label()
        Me.lblUrlCap = New System.Windows.Forms.Label()
        Me.txtUrl = New System.Windows.Forms.TextBox()
        Me.lblPinCap = New System.Windows.Forms.Label()
        Me.txtPin = New System.Windows.Forms.TextBox()
        Me.btnFetch = New System.Windows.Forms.Button()
        Me.chkLoopback = New System.Windows.Forms.CheckBox()
        Me.lblUserCap = New System.Windows.Forms.Label()
        Me.txtUser = New System.Windows.Forms.TextBox()
        Me.lblPassCap = New System.Windows.Forms.Label()
        Me.txtPass = New System.Windows.Forms.TextBox()
        Me.lblTotpCap = New System.Windows.Forms.Label()
        Me.txtTotp = New System.Windows.Forms.TextBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnConnect = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblTitle
        '
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Location = New System.Drawing.Point(24, 20)
        Me.lblTitle.AutoSize = True
        Me.lblTitle.Text = "الاتصال ببوابة بعيدة"
        '
        'lblInfo
        '
        Me.lblInfo.Name = "lblInfo"
        Me.lblInfo.Location = New System.Drawing.Point(24, 54)
        Me.lblInfo.Size = New System.Drawing.Size(470, 44)
        Me.lblInfo.Text = "أدخل عنوان البوابة (عبر VPN أو نفق مشفّر). لا تُخزَّن كلمات المرور ولا رموز التحقق على هذا الجهاز."
        Me.lblInfo.Tag = "muted"
        '
        'lblUrlCap
        '
        Me.lblUrlCap.Name = "lblUrlCap"
        Me.lblUrlCap.AutoSize = True
        Me.lblUrlCap.Location = New System.Drawing.Point(24, 108)
        Me.lblUrlCap.Text = "عنوان البوابة"
        '
        'txtUrl
        '
        Me.txtUrl.Name = "txtUrl"
        Me.txtUrl.Location = New System.Drawing.Point(24, 130)
        Me.txtUrl.Size = New System.Drawing.Size(470, 23)
        Me.txtUrl.MaxLength = 200
        Me.txtUrl.PlaceholderText = "https://gateway.example:8443"
        '
        'lblPinCap
        '
        Me.lblPinCap.Name = "lblPinCap"
        Me.lblPinCap.AutoSize = True
        Me.lblPinCap.Location = New System.Drawing.Point(24, 166)
        Me.lblPinCap.Text = "بصمة الشهادة (spki-sha256:… أو cert-sha256:…)"
        '
        'txtPin
        '
        Me.txtPin.Name = "txtPin"
        Me.txtPin.Location = New System.Drawing.Point(24, 188)
        Me.txtPin.Size = New System.Drawing.Size(360, 23)
        Me.txtPin.MaxLength = 200
        '
        'btnFetch
        '
        Me.btnFetch.Name = "btnFetch"
        Me.btnFetch.Location = New System.Drawing.Point(394, 186)
        Me.btnFetch.Size = New System.Drawing.Size(100, 26)
        Me.btnFetch.Text = "قراءة الشهادة"
        Me.btnFetch.UseVisualStyleBackColor = True
        '
        'chkLoopback
        '
        Me.chkLoopback.Name = "chkLoopback"
        Me.chkLoopback.AutoSize = True
        Me.chkLoopback.Location = New System.Drawing.Point(24, 222)
        Me.chkLoopback.Text = "السماح بـ HTTP على الحلقة المحلية (للتطوير فقط)"
        Me.chkLoopback.UseVisualStyleBackColor = True
        Me.chkLoopback.Checked = False
        '
        'lblUserCap
        '
        Me.lblUserCap.Name = "lblUserCap"
        Me.lblUserCap.AutoSize = True
        Me.lblUserCap.Location = New System.Drawing.Point(24, 256)
        Me.lblUserCap.Text = "اسم المستخدم"
        '
        'txtUser
        '
        Me.txtUser.Name = "txtUser"
        Me.txtUser.Location = New System.Drawing.Point(24, 278)
        Me.txtUser.Size = New System.Drawing.Size(470, 23)
        Me.txtUser.MaxLength = 64
        '
        'lblPassCap
        '
        Me.lblPassCap.Name = "lblPassCap"
        Me.lblPassCap.AutoSize = True
        Me.lblPassCap.Location = New System.Drawing.Point(24, 312)
        Me.lblPassCap.Text = "كلمة المرور"
        '
        'txtPass
        '
        Me.txtPass.Name = "txtPass"
        Me.txtPass.Location = New System.Drawing.Point(24, 334)
        Me.txtPass.Size = New System.Drawing.Size(470, 23)
        Me.txtPass.UseSystemPasswordChar = True
        Me.txtPass.MaxLength = 128
        '
        'lblTotpCap
        '
        Me.lblTotpCap.Name = "lblTotpCap"
        Me.lblTotpCap.AutoSize = True
        Me.lblTotpCap.Location = New System.Drawing.Point(24, 368)
        Me.lblTotpCap.Text = "رمز التحقق من تطبيق المصادقة (6 أرقام)"
        '
        'txtTotp
        '
        Me.txtTotp.Name = "txtTotp"
        Me.txtTotp.Location = New System.Drawing.Point(24, 390)
        Me.txtTotp.Size = New System.Drawing.Size(140, 23)
        Me.txtTotp.MaxLength = 6
        '
        'lblError
        '
        Me.lblError.Name = "lblError"
        Me.lblError.Location = New System.Drawing.Point(24, 424)
        Me.lblError.Size = New System.Drawing.Size(470, 44)
        Me.lblError.Text = ""
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(229, 72, 77)
        '
        'btnConnect
        '
        Me.btnConnect.Name = "btnConnect"
        Me.btnConnect.Location = New System.Drawing.Point(24, 480)
        Me.btnConnect.Size = New System.Drawing.Size(230, 38)
        Me.btnConnect.Text = "اتصال"
        Me.btnConnect.UseVisualStyleBackColor = True
        '
        'btnCancel
        '
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Location = New System.Drawing.Point(264, 480)
        Me.btnCancel.Size = New System.Drawing.Size(230, 38)
        Me.btnCancel.Text = "إلغاء"
        Me.btnCancel.UseVisualStyleBackColor = True
        Me.Controls.Add(Me.lblTitle)
        Me.Controls.Add(Me.lblInfo)
        Me.Controls.Add(Me.lblUrlCap)
        Me.Controls.Add(Me.txtUrl)
        Me.Controls.Add(Me.lblPinCap)
        Me.Controls.Add(Me.txtPin)
        Me.Controls.Add(Me.btnFetch)
        Me.Controls.Add(Me.chkLoopback)
        Me.Controls.Add(Me.lblUserCap)
        Me.Controls.Add(Me.txtUser)
        Me.Controls.Add(Me.lblPassCap)
        Me.Controls.Add(Me.txtPass)
        Me.Controls.Add(Me.lblTotpCap)
        Me.Controls.Add(Me.txtTotp)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.btnConnect)
        Me.Controls.Add(Me.btnCancel)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(520, 540)
        Me.Name = "FrmGatewayLogin"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "الاتصال ببوابة بعيدة"
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.AcceptButton = Me.btnConnect
        Me.CancelButton = Me.btnCancel
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents lblInfo As System.Windows.Forms.Label
    Friend WithEvents lblUrlCap As System.Windows.Forms.Label
    Friend WithEvents txtUrl As System.Windows.Forms.TextBox
    Friend WithEvents lblPinCap As System.Windows.Forms.Label
    Friend WithEvents txtPin As System.Windows.Forms.TextBox
    Friend WithEvents btnFetch As System.Windows.Forms.Button
    Friend WithEvents chkLoopback As System.Windows.Forms.CheckBox
    Friend WithEvents lblUserCap As System.Windows.Forms.Label
    Friend WithEvents txtUser As System.Windows.Forms.TextBox
    Friend WithEvents lblPassCap As System.Windows.Forms.Label
    Friend WithEvents txtPass As System.Windows.Forms.TextBox
    Friend WithEvents lblTotpCap As System.Windows.Forms.Label
    Friend WithEvents txtTotp As System.Windows.Forms.TextBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnConnect As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button

End Class
End Namespace
