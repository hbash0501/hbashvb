<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class LoginForm
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then components.Dispose()
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.cardPanel = New System.Windows.Forms.Panel()
        Me.lblHint = New System.Windows.Forms.Label()
        Me.btnExit = New System.Windows.Forms.Button()
        Me.btnLogin = New System.Windows.Forms.Button()
        Me.txtPassword = New System.Windows.Forms.TextBox()
        Me.lblPassword = New System.Windows.Forms.Label()
        Me.txtUserName = New System.Windows.Forms.TextBox()
        Me.lblUserName = New System.Windows.Forms.Label()
        Me.lblSubTitle = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.logoPanel = New System.Windows.Forms.Panel()
        Me.lblLogo = New System.Windows.Forms.Label()
        Me.cardPanel.SuspendLayout()
        Me.logoPanel.SuspendLayout()
        Me.SuspendLayout()
        '
        'cardPanel
        '
        Me.cardPanel.Anchor = System.Windows.Forms.AnchorStyles.None
        Me.cardPanel.BackColor = System.Drawing.Color.White
        Me.cardPanel.Controls.Add(Me.lblHint)
        Me.cardPanel.Controls.Add(Me.btnExit)
        Me.cardPanel.Controls.Add(Me.btnLogin)
        Me.cardPanel.Controls.Add(Me.txtPassword)
        Me.cardPanel.Controls.Add(Me.lblPassword)
        Me.cardPanel.Controls.Add(Me.txtUserName)
        Me.cardPanel.Controls.Add(Me.lblUserName)
        Me.cardPanel.Controls.Add(Me.lblSubTitle)
        Me.cardPanel.Controls.Add(Me.lblTitle)
        Me.cardPanel.Controls.Add(Me.logoPanel)
        Me.cardPanel.Location = New System.Drawing.Point(218, 52)
        Me.cardPanel.Name = "cardPanel"
        Me.cardPanel.Padding = New System.Windows.Forms.Padding(28)
        Me.cardPanel.Size = New System.Drawing.Size(484, 436)
        Me.cardPanel.TabIndex = 0
        '
        'lblHint
        '
        Me.lblHint.ForeColor = System.Drawing.Color.FromArgb(CType(CType(110, Byte), Integer), CType(CType(120, Byte), Integer), CType(CType(116, Byte), Integer))
        Me.lblHint.Location = New System.Drawing.Point(31, 374)
        Me.lblHint.Name = "lblHint"
        Me.lblHint.Size = New System.Drawing.Size(422, 36)
        Me.lblHint.TabIndex = 9
        Me.lblHint.Text = "للمساعدة في الدخول تواصل مع مسؤول النظام."
        Me.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'btnExit
        '
        Me.btnExit.BackColor = System.Drawing.Color.FromArgb(CType(CType(240, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(241, Byte), Integer))
        Me.btnExit.FlatAppearance.BorderSize = 0
        Me.btnExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnExit.Font = New System.Drawing.Font("Tahoma", 10.0!, System.Drawing.FontStyle.Bold)
        Me.btnExit.ForeColor = System.Drawing.Color.FromArgb(CType(CType(55, Byte), Integer), CType(CType(62, Byte), Integer), CType(CType(59, Byte), Integer))
        Me.btnExit.Location = New System.Drawing.Point(31, 312)
        Me.btnExit.Name = "btnExit"
        Me.btnExit.Size = New System.Drawing.Size(142, 46)
        Me.btnExit.TabIndex = 8
        Me.btnExit.Text = "خروج"
        Me.btnExit.UseVisualStyleBackColor = False
        '
        'btnLogin
        '
        Me.btnLogin.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnLogin.FlatAppearance.BorderSize = 0
        Me.btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnLogin.Font = New System.Drawing.Font("Tahoma", 10.0!, System.Drawing.FontStyle.Bold)
        Me.btnLogin.ForeColor = System.Drawing.Color.White
        Me.btnLogin.Location = New System.Drawing.Point(190, 312)
        Me.btnLogin.Name = "btnLogin"
        Me.btnLogin.Size = New System.Drawing.Size(263, 46)
        Me.btnLogin.TabIndex = 7
        Me.btnLogin.Text = "دخول إلى المنصة"
        Me.btnLogin.UseVisualStyleBackColor = False
        '
        'txtPassword
        '
        Me.txtPassword.Font = New System.Drawing.Font("Tahoma", 11.0!)
        Me.txtPassword.Location = New System.Drawing.Point(31, 260)
        Me.txtPassword.Name = "txtPassword"
        Me.txtPassword.PasswordChar = Global.Microsoft.VisualBasic.ChrW(9679)
        Me.txtPassword.Size = New System.Drawing.Size(422, 30)
        Me.txtPassword.TabIndex = 6
        Me.txtPassword.Text = ""
        '
        'lblPassword
        '
        Me.lblPassword.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.lblPassword.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblPassword.Location = New System.Drawing.Point(31, 232)
        Me.lblPassword.Name = "lblPassword"
        Me.lblPassword.Size = New System.Drawing.Size(422, 25)
        Me.lblPassword.TabIndex = 5
        Me.lblPassword.Text = "كلمة المرور"
        Me.lblPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtUserName
        '
        Me.txtUserName.Font = New System.Drawing.Font("Tahoma", 11.0!)
        Me.txtUserName.Location = New System.Drawing.Point(31, 196)
        Me.txtUserName.Name = "txtUserName"
        Me.txtUserName.Size = New System.Drawing.Size(422, 30)
        Me.txtUserName.TabIndex = 4
        Me.txtUserName.Text = "admin"
        '
        'lblUserName
        '
        Me.lblUserName.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.lblUserName.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblUserName.Location = New System.Drawing.Point(31, 168)
        Me.lblUserName.Name = "lblUserName"
        Me.lblUserName.Size = New System.Drawing.Size(422, 25)
        Me.lblUserName.TabIndex = 3
        Me.lblUserName.Text = "اسم المستخدم"
        Me.lblUserName.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblSubTitle
        '
        Me.lblSubTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(97, Byte), Integer), CType(CType(108, Byte), Integer), CType(CType(104, Byte), Integer))
        Me.lblSubTitle.Location = New System.Drawing.Point(31, 128)
        Me.lblSubTitle.Name = "lblSubTitle"
        Me.lblSubTitle.Size = New System.Drawing.Size(422, 30)
        Me.lblSubTitle.TabIndex = 2
        Me.lblSubTitle.Text = "منصة أرشفة ذكية للموظفين — دخول آمن وصلاحيات واضحة"
        Me.lblSubTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'lblTitle
        '
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 15.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblTitle.Location = New System.Drawing.Point(31, 91)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(422, 37)
        Me.lblTitle.TabIndex = 1
        Me.lblTitle.Text = "الأرشيف الذكي للمستندات"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'logoPanel
        '
        Me.logoPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.logoPanel.Controls.Add(Me.lblLogo)
        Me.logoPanel.Location = New System.Drawing.Point(202, 28)
        Me.logoPanel.Name = "logoPanel"
        Me.logoPanel.Size = New System.Drawing.Size(80, 58)
        Me.logoPanel.TabIndex = 0
        '
        'lblLogo
        '
        Me.lblLogo.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblLogo.Font = New System.Drawing.Font("Tahoma", 22.0!, System.Drawing.FontStyle.Bold)
        Me.lblLogo.ForeColor = System.Drawing.Color.White
        Me.lblLogo.Location = New System.Drawing.Point(0, 0)
        Me.lblLogo.Name = "lblLogo"
        Me.lblLogo.Size = New System.Drawing.Size(80, 58)
        Me.lblLogo.TabIndex = 0
        Me.lblLogo.Text = "AI"
        Me.lblLogo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'LoginForm
        '
        Me.AcceptButton = Me.btnLogin
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(241, Byte), Integer), CType(CType(245, Byte), Integer), CType(CType(243, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(920, 540)
        Me.Controls.Add(Me.cardPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.Name = "LoginForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "تسجيل الدخول"
        Me.cardPanel.ResumeLayout(False)
        Me.cardPanel.PerformLayout()
        Me.logoPanel.ResumeLayout(False)
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents cardPanel As System.Windows.Forms.Panel
    Friend WithEvents lblHint As System.Windows.Forms.Label
    Friend WithEvents btnExit As System.Windows.Forms.Button
    Friend WithEvents btnLogin As System.Windows.Forms.Button
    Friend WithEvents txtPassword As System.Windows.Forms.TextBox
    Friend WithEvents lblPassword As System.Windows.Forms.Label
    Friend WithEvents txtUserName As System.Windows.Forms.TextBox
    Friend WithEvents lblUserName As System.Windows.Forms.Label
    Friend WithEvents lblSubTitle As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents logoPanel As System.Windows.Forms.Panel
    Friend WithEvents lblLogo As System.Windows.Forms.Label
End Class
