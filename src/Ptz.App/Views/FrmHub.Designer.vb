Namespace Views
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmHub
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
        Me.grpSe = New System.Windows.Forms.GroupBox()
        Me.lblNameSe = New System.Windows.Forms.Label()
        Me.txtNameSe = New System.Windows.Forms.TextBox()
        Me.lblHostSe = New System.Windows.Forms.Label()
        Me.txtHostSe = New System.Windows.Forms.TextBox()
        Me.lblPortSe = New System.Windows.Forms.Label()
        Me.nudPortSe = New System.Windows.Forms.NumericUpDown()
        Me.chkHttpsSe = New System.Windows.Forms.CheckBox()
        Me.chkInsecureSe = New System.Windows.Forms.CheckBox()
        Me.lblUserSe = New System.Windows.Forms.Label()
        Me.txtUserSe = New System.Windows.Forms.TextBox()
        Me.lblPassSe = New System.Windows.Forms.Label()
        Me.txtPassSe = New System.Windows.Forms.TextBox()
        Me.lblFpSe = New System.Windows.Forms.Label()
        Me.txtFpSe = New System.Windows.Forms.TextBox()
        Me.btnFetchSe = New System.Windows.Forms.Button()
        Me.btnSaveSe = New System.Windows.Forms.Button()
        Me.btnOpenSe = New System.Windows.Forms.Button()
        Me.lblModelSe = New System.Windows.Forms.Label()
        Me.grpDe = New System.Windows.Forms.GroupBox()
        Me.lblNameDe = New System.Windows.Forms.Label()
        Me.txtNameDe = New System.Windows.Forms.TextBox()
        Me.lblHostDe = New System.Windows.Forms.Label()
        Me.txtHostDe = New System.Windows.Forms.TextBox()
        Me.lblPortDe = New System.Windows.Forms.Label()
        Me.nudPortDe = New System.Windows.Forms.NumericUpDown()
        Me.chkHttpsDe = New System.Windows.Forms.CheckBox()
        Me.chkInsecureDe = New System.Windows.Forms.CheckBox()
        Me.lblUserDe = New System.Windows.Forms.Label()
        Me.txtUserDe = New System.Windows.Forms.TextBox()
        Me.lblPassDe = New System.Windows.Forms.Label()
        Me.txtPassDe = New System.Windows.Forms.TextBox()
        Me.lblFpDe = New System.Windows.Forms.Label()
        Me.txtFpDe = New System.Windows.Forms.TextBox()
        Me.btnFetchDe = New System.Windows.Forms.Button()
        Me.btnSaveDe = New System.Windows.Forms.Button()
        Me.btnOpenDe = New System.Windows.Forms.Button()
        Me.lblModelDe = New System.Windows.Forms.Label()
        Me.pnlBottom = New System.Windows.Forms.Panel()
        Me.lblSession = New System.Windows.Forms.Label()
        Me.btnUsers = New System.Windows.Forms.Button()
        Me.btnAudit = New System.Windows.Forms.Button()
        Me.btnLogout = New System.Windows.Forms.Button()
        Me.grpSe.SuspendLayout()
        CType(Me.nudPortSe, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpDe.SuspendLayout()
        CType(Me.nudPortDe, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.pnlBottom.SuspendLayout()
        Me.SuspendLayout()
        '
        'lblTitle
        '
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Size = New System.Drawing.Size(1000, 48)
        Me.lblTitle.Text = "منصة التحكم بكاميرات PTZ"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Me.lblTitle.Padding = New System.Windows.Forms.Padding(14, 0, 0, 0)
        '
        'grpSe
        '
        Me.grpSe.Name = "grpSe"
        Me.grpSe.Location = New System.Drawing.Point(12, 60)
        Me.grpSe.Size = New System.Drawing.Size(460, 434)
        Me.grpSe.Text = "DS-2SE7C432MW-AEB (4MP)"
        '
        'lblNameSe
        '
        Me.lblNameSe.Name = "lblNameSe"
        Me.lblNameSe.AutoSize = True
        Me.lblNameSe.Location = New System.Drawing.Point(14, 34)
        Me.lblNameSe.Text = "اسم العرض"
        '
        'txtNameSe
        '
        Me.txtNameSe.Name = "txtNameSe"
        Me.txtNameSe.Location = New System.Drawing.Point(150, 30)
        Me.txtNameSe.Size = New System.Drawing.Size(290, 23)
        '
        'lblHostSe
        '
        Me.lblHostSe.Name = "lblHostSe"
        Me.lblHostSe.AutoSize = True
        Me.lblHostSe.Location = New System.Drawing.Point(14, 68)
        Me.lblHostSe.Text = "عنوان الكاميرا"
        '
        'txtHostSe
        '
        Me.txtHostSe.Name = "txtHostSe"
        Me.txtHostSe.Location = New System.Drawing.Point(150, 64)
        Me.txtHostSe.Size = New System.Drawing.Size(290, 23)
        '
        'lblPortSe
        '
        Me.lblPortSe.Name = "lblPortSe"
        Me.lblPortSe.AutoSize = True
        Me.lblPortSe.Location = New System.Drawing.Point(14, 102)
        Me.lblPortSe.Text = "المنفذ"
        '
        'nudPortSe
        '
        Me.nudPortSe.Name = "nudPortSe"
        Me.nudPortSe.Location = New System.Drawing.Point(150, 98)
        Me.nudPortSe.Size = New System.Drawing.Size(90, 23)
        Me.nudPortSe.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudPortSe.Maximum = New Decimal(New Integer() {65535, 0, 0, 0})
        Me.nudPortSe.Value = New Decimal(New Integer() {443, 0, 0, 0})
        '
        'chkHttpsSe
        '
        Me.chkHttpsSe.Name = "chkHttpsSe"
        Me.chkHttpsSe.AutoSize = True
        Me.chkHttpsSe.Location = New System.Drawing.Point(14, 132)
        Me.chkHttpsSe.Text = "استخدام HTTPS (موصى به)"
        Me.chkHttpsSe.UseVisualStyleBackColor = True
        Me.chkHttpsSe.Checked = True
        '
        'chkInsecureSe
        '
        Me.chkInsecureSe.Name = "chkInsecureSe"
        Me.chkInsecureSe.AutoSize = True
        Me.chkInsecureSe.Location = New System.Drawing.Point(14, 160)
        Me.chkInsecureSe.Text = "السماح بـ HTTP غير المشفّر (غير آمن)"
        Me.chkInsecureSe.UseVisualStyleBackColor = True
        Me.chkInsecureSe.Checked = False
        '
        'lblUserSe
        '
        Me.lblUserSe.Name = "lblUserSe"
        Me.lblUserSe.AutoSize = True
        Me.lblUserSe.Location = New System.Drawing.Point(14, 198)
        Me.lblUserSe.Text = "المستخدم"
        '
        'txtUserSe
        '
        Me.txtUserSe.Name = "txtUserSe"
        Me.txtUserSe.Location = New System.Drawing.Point(150, 194)
        Me.txtUserSe.Size = New System.Drawing.Size(290, 23)
        '
        'lblPassSe
        '
        Me.lblPassSe.Name = "lblPassSe"
        Me.lblPassSe.AutoSize = True
        Me.lblPassSe.Location = New System.Drawing.Point(14, 232)
        Me.lblPassSe.Text = "كلمة المرور"
        '
        'txtPassSe
        '
        Me.txtPassSe.Name = "txtPassSe"
        Me.txtPassSe.Location = New System.Drawing.Point(150, 228)
        Me.txtPassSe.Size = New System.Drawing.Size(290, 23)
        Me.txtPassSe.UseSystemPasswordChar = True
        Me.txtPassSe.PlaceholderText = "اتركها فارغة للإبقاء على الحالية"
        '
        'lblFpSe
        '
        Me.lblFpSe.Name = "lblFpSe"
        Me.lblFpSe.AutoSize = True
        Me.lblFpSe.Location = New System.Drawing.Point(14, 266)
        Me.lblFpSe.Text = "بصمة الشهادة SHA-256"
        '
        'txtFpSe
        '
        Me.txtFpSe.Name = "txtFpSe"
        Me.txtFpSe.Location = New System.Drawing.Point(14, 290)
        Me.txtFpSe.Size = New System.Drawing.Size(426, 23)
        Me.txtFpSe.ReadOnly = True
        '
        'btnFetchSe
        '
        Me.btnFetchSe.Name = "btnFetchSe"
        Me.btnFetchSe.Location = New System.Drawing.Point(14, 320)
        Me.btnFetchSe.Size = New System.Drawing.Size(426, 28)
        Me.btnFetchSe.Text = "قراءة وتثبيت شهادة الكاميرا"
        Me.btnFetchSe.UseVisualStyleBackColor = True
        '
        'btnSaveSe
        '
        Me.btnSaveSe.Name = "btnSaveSe"
        Me.btnSaveSe.Location = New System.Drawing.Point(14, 360)
        Me.btnSaveSe.Size = New System.Drawing.Size(205, 34)
        Me.btnSaveSe.Text = "حفظ الإعدادات"
        Me.btnSaveSe.UseVisualStyleBackColor = True
        '
        'btnOpenSe
        '
        Me.btnOpenSe.Name = "btnOpenSe"
        Me.btnOpenSe.Location = New System.Drawing.Point(235, 360)
        Me.btnOpenSe.Size = New System.Drawing.Size(205, 34)
        Me.btnOpenSe.Text = "فتح لوحة التحكم"
        Me.btnOpenSe.UseVisualStyleBackColor = True
        '
        'lblModelSe
        '
        Me.lblModelSe.Name = "lblModelSe"
        Me.lblModelSe.AutoSize = True
        Me.lblModelSe.Location = New System.Drawing.Point(14, 404)
        Me.lblModelSe.Text = ""
        Me.lblModelSe.Tag = "muted"
        '
        'grpDe
        '
        Me.grpDe.Name = "grpDe"
        Me.grpDe.Location = New System.Drawing.Point(508, 60)
        Me.grpDe.Size = New System.Drawing.Size(460, 434)
        Me.grpDe.Text = "DS-2DE7232IW-AE (2MP)"
        '
        'lblNameDe
        '
        Me.lblNameDe.Name = "lblNameDe"
        Me.lblNameDe.AutoSize = True
        Me.lblNameDe.Location = New System.Drawing.Point(14, 34)
        Me.lblNameDe.Text = "اسم العرض"
        '
        'txtNameDe
        '
        Me.txtNameDe.Name = "txtNameDe"
        Me.txtNameDe.Location = New System.Drawing.Point(150, 30)
        Me.txtNameDe.Size = New System.Drawing.Size(290, 23)
        '
        'lblHostDe
        '
        Me.lblHostDe.Name = "lblHostDe"
        Me.lblHostDe.AutoSize = True
        Me.lblHostDe.Location = New System.Drawing.Point(14, 68)
        Me.lblHostDe.Text = "عنوان الكاميرا"
        '
        'txtHostDe
        '
        Me.txtHostDe.Name = "txtHostDe"
        Me.txtHostDe.Location = New System.Drawing.Point(150, 64)
        Me.txtHostDe.Size = New System.Drawing.Size(290, 23)
        '
        'lblPortDe
        '
        Me.lblPortDe.Name = "lblPortDe"
        Me.lblPortDe.AutoSize = True
        Me.lblPortDe.Location = New System.Drawing.Point(14, 102)
        Me.lblPortDe.Text = "المنفذ"
        '
        'nudPortDe
        '
        Me.nudPortDe.Name = "nudPortDe"
        Me.nudPortDe.Location = New System.Drawing.Point(150, 98)
        Me.nudPortDe.Size = New System.Drawing.Size(90, 23)
        Me.nudPortDe.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudPortDe.Maximum = New Decimal(New Integer() {65535, 0, 0, 0})
        Me.nudPortDe.Value = New Decimal(New Integer() {443, 0, 0, 0})
        '
        'chkHttpsDe
        '
        Me.chkHttpsDe.Name = "chkHttpsDe"
        Me.chkHttpsDe.AutoSize = True
        Me.chkHttpsDe.Location = New System.Drawing.Point(14, 132)
        Me.chkHttpsDe.Text = "استخدام HTTPS (موصى به)"
        Me.chkHttpsDe.UseVisualStyleBackColor = True
        Me.chkHttpsDe.Checked = True
        '
        'chkInsecureDe
        '
        Me.chkInsecureDe.Name = "chkInsecureDe"
        Me.chkInsecureDe.AutoSize = True
        Me.chkInsecureDe.Location = New System.Drawing.Point(14, 160)
        Me.chkInsecureDe.Text = "السماح بـ HTTP غير المشفّر (غير آمن)"
        Me.chkInsecureDe.UseVisualStyleBackColor = True
        Me.chkInsecureDe.Checked = False
        '
        'lblUserDe
        '
        Me.lblUserDe.Name = "lblUserDe"
        Me.lblUserDe.AutoSize = True
        Me.lblUserDe.Location = New System.Drawing.Point(14, 198)
        Me.lblUserDe.Text = "المستخدم"
        '
        'txtUserDe
        '
        Me.txtUserDe.Name = "txtUserDe"
        Me.txtUserDe.Location = New System.Drawing.Point(150, 194)
        Me.txtUserDe.Size = New System.Drawing.Size(290, 23)
        '
        'lblPassDe
        '
        Me.lblPassDe.Name = "lblPassDe"
        Me.lblPassDe.AutoSize = True
        Me.lblPassDe.Location = New System.Drawing.Point(14, 232)
        Me.lblPassDe.Text = "كلمة المرور"
        '
        'txtPassDe
        '
        Me.txtPassDe.Name = "txtPassDe"
        Me.txtPassDe.Location = New System.Drawing.Point(150, 228)
        Me.txtPassDe.Size = New System.Drawing.Size(290, 23)
        Me.txtPassDe.UseSystemPasswordChar = True
        Me.txtPassDe.PlaceholderText = "اتركها فارغة للإبقاء على الحالية"
        '
        'lblFpDe
        '
        Me.lblFpDe.Name = "lblFpDe"
        Me.lblFpDe.AutoSize = True
        Me.lblFpDe.Location = New System.Drawing.Point(14, 266)
        Me.lblFpDe.Text = "بصمة الشهادة SHA-256"
        '
        'txtFpDe
        '
        Me.txtFpDe.Name = "txtFpDe"
        Me.txtFpDe.Location = New System.Drawing.Point(14, 290)
        Me.txtFpDe.Size = New System.Drawing.Size(426, 23)
        Me.txtFpDe.ReadOnly = True
        '
        'btnFetchDe
        '
        Me.btnFetchDe.Name = "btnFetchDe"
        Me.btnFetchDe.Location = New System.Drawing.Point(14, 320)
        Me.btnFetchDe.Size = New System.Drawing.Size(426, 28)
        Me.btnFetchDe.Text = "قراءة وتثبيت شهادة الكاميرا"
        Me.btnFetchDe.UseVisualStyleBackColor = True
        '
        'btnSaveDe
        '
        Me.btnSaveDe.Name = "btnSaveDe"
        Me.btnSaveDe.Location = New System.Drawing.Point(14, 360)
        Me.btnSaveDe.Size = New System.Drawing.Size(205, 34)
        Me.btnSaveDe.Text = "حفظ الإعدادات"
        Me.btnSaveDe.UseVisualStyleBackColor = True
        '
        'btnOpenDe
        '
        Me.btnOpenDe.Name = "btnOpenDe"
        Me.btnOpenDe.Location = New System.Drawing.Point(235, 360)
        Me.btnOpenDe.Size = New System.Drawing.Size(205, 34)
        Me.btnOpenDe.Text = "فتح لوحة التحكم"
        Me.btnOpenDe.UseVisualStyleBackColor = True
        '
        'lblModelDe
        '
        Me.lblModelDe.Name = "lblModelDe"
        Me.lblModelDe.AutoSize = True
        Me.lblModelDe.Location = New System.Drawing.Point(14, 404)
        Me.lblModelDe.Text = ""
        Me.lblModelDe.Tag = "muted"
        '
        'pnlBottom
        '
        Me.pnlBottom.Name = "pnlBottom"
        Me.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.pnlBottom.Size = New System.Drawing.Size(1000, 56)
        '
        'lblSession
        '
        Me.lblSession.Name = "lblSession"
        Me.lblSession.AutoSize = True
        Me.lblSession.Location = New System.Drawing.Point(14, 18)
        Me.lblSession.Text = "—"
        Me.lblSession.Tag = "muted"
        '
        'btnUsers
        '
        Me.btnUsers.Name = "btnUsers"
        Me.btnUsers.Location = New System.Drawing.Point(560, 12)
        Me.btnUsers.Size = New System.Drawing.Size(140, 32)
        Me.btnUsers.Text = "إدارة المستخدمين"
        Me.btnUsers.UseVisualStyleBackColor = True
        '
        'btnAudit
        '
        Me.btnAudit.Name = "btnAudit"
        Me.btnAudit.Location = New System.Drawing.Point(706, 12)
        Me.btnAudit.Size = New System.Drawing.Size(130, 32)
        Me.btnAudit.Text = "سجل التدقيق"
        Me.btnAudit.UseVisualStyleBackColor = True
        '
        'btnLogout
        '
        Me.btnLogout.Name = "btnLogout"
        Me.btnLogout.Location = New System.Drawing.Point(842, 12)
        Me.btnLogout.Size = New System.Drawing.Size(130, 32)
        Me.btnLogout.Text = "تسجيل الخروج"
        Me.btnLogout.UseVisualStyleBackColor = True
        Me.Controls.Add(Me.lblTitle)
        Me.Controls.Add(Me.grpSe)
        Me.Controls.Add(Me.grpDe)
        Me.Controls.Add(Me.pnlBottom)
        Me.grpSe.Controls.Add(Me.lblNameSe)
        Me.grpSe.Controls.Add(Me.txtNameSe)
        Me.grpSe.Controls.Add(Me.lblHostSe)
        Me.grpSe.Controls.Add(Me.txtHostSe)
        Me.grpSe.Controls.Add(Me.lblPortSe)
        Me.grpSe.Controls.Add(Me.nudPortSe)
        Me.grpSe.Controls.Add(Me.chkHttpsSe)
        Me.grpSe.Controls.Add(Me.chkInsecureSe)
        Me.grpSe.Controls.Add(Me.lblUserSe)
        Me.grpSe.Controls.Add(Me.txtUserSe)
        Me.grpSe.Controls.Add(Me.lblPassSe)
        Me.grpSe.Controls.Add(Me.txtPassSe)
        Me.grpSe.Controls.Add(Me.lblFpSe)
        Me.grpSe.Controls.Add(Me.txtFpSe)
        Me.grpSe.Controls.Add(Me.btnFetchSe)
        Me.grpSe.Controls.Add(Me.btnSaveSe)
        Me.grpSe.Controls.Add(Me.btnOpenSe)
        Me.grpSe.Controls.Add(Me.lblModelSe)
        Me.grpDe.Controls.Add(Me.lblNameDe)
        Me.grpDe.Controls.Add(Me.txtNameDe)
        Me.grpDe.Controls.Add(Me.lblHostDe)
        Me.grpDe.Controls.Add(Me.txtHostDe)
        Me.grpDe.Controls.Add(Me.lblPortDe)
        Me.grpDe.Controls.Add(Me.nudPortDe)
        Me.grpDe.Controls.Add(Me.chkHttpsDe)
        Me.grpDe.Controls.Add(Me.chkInsecureDe)
        Me.grpDe.Controls.Add(Me.lblUserDe)
        Me.grpDe.Controls.Add(Me.txtUserDe)
        Me.grpDe.Controls.Add(Me.lblPassDe)
        Me.grpDe.Controls.Add(Me.txtPassDe)
        Me.grpDe.Controls.Add(Me.lblFpDe)
        Me.grpDe.Controls.Add(Me.txtFpDe)
        Me.grpDe.Controls.Add(Me.btnFetchDe)
        Me.grpDe.Controls.Add(Me.btnSaveDe)
        Me.grpDe.Controls.Add(Me.btnOpenDe)
        Me.grpDe.Controls.Add(Me.lblModelDe)
        Me.pnlBottom.Controls.Add(Me.lblSession)
        Me.pnlBottom.Controls.Add(Me.btnUsers)
        Me.pnlBottom.Controls.Add(Me.btnAudit)
        Me.pnlBottom.Controls.Add(Me.btnLogout)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1000, 584)
        Me.Name = "FrmHub"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "منصة التحكم بكاميرات PTZ"
        Me.MinimumSize = New System.Drawing.Size(1000, 584)
        Me.pnlBottom.ResumeLayout(False)
        CType(Me.nudPortDe, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpDe.ResumeLayout(False)
        Me.grpDe.PerformLayout()
        CType(Me.nudPortSe, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpSe.ResumeLayout(False)
        Me.grpSe.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents grpSe As System.Windows.Forms.GroupBox
    Friend WithEvents lblNameSe As System.Windows.Forms.Label
    Friend WithEvents txtNameSe As System.Windows.Forms.TextBox
    Friend WithEvents lblHostSe As System.Windows.Forms.Label
    Friend WithEvents txtHostSe As System.Windows.Forms.TextBox
    Friend WithEvents lblPortSe As System.Windows.Forms.Label
    Friend WithEvents nudPortSe As System.Windows.Forms.NumericUpDown
    Friend WithEvents chkHttpsSe As System.Windows.Forms.CheckBox
    Friend WithEvents chkInsecureSe As System.Windows.Forms.CheckBox
    Friend WithEvents lblUserSe As System.Windows.Forms.Label
    Friend WithEvents txtUserSe As System.Windows.Forms.TextBox
    Friend WithEvents lblPassSe As System.Windows.Forms.Label
    Friend WithEvents txtPassSe As System.Windows.Forms.TextBox
    Friend WithEvents lblFpSe As System.Windows.Forms.Label
    Friend WithEvents txtFpSe As System.Windows.Forms.TextBox
    Friend WithEvents btnFetchSe As System.Windows.Forms.Button
    Friend WithEvents btnSaveSe As System.Windows.Forms.Button
    Friend WithEvents btnOpenSe As System.Windows.Forms.Button
    Friend WithEvents lblModelSe As System.Windows.Forms.Label
    Friend WithEvents grpDe As System.Windows.Forms.GroupBox
    Friend WithEvents lblNameDe As System.Windows.Forms.Label
    Friend WithEvents txtNameDe As System.Windows.Forms.TextBox
    Friend WithEvents lblHostDe As System.Windows.Forms.Label
    Friend WithEvents txtHostDe As System.Windows.Forms.TextBox
    Friend WithEvents lblPortDe As System.Windows.Forms.Label
    Friend WithEvents nudPortDe As System.Windows.Forms.NumericUpDown
    Friend WithEvents chkHttpsDe As System.Windows.Forms.CheckBox
    Friend WithEvents chkInsecureDe As System.Windows.Forms.CheckBox
    Friend WithEvents lblUserDe As System.Windows.Forms.Label
    Friend WithEvents txtUserDe As System.Windows.Forms.TextBox
    Friend WithEvents lblPassDe As System.Windows.Forms.Label
    Friend WithEvents txtPassDe As System.Windows.Forms.TextBox
    Friend WithEvents lblFpDe As System.Windows.Forms.Label
    Friend WithEvents txtFpDe As System.Windows.Forms.TextBox
    Friend WithEvents btnFetchDe As System.Windows.Forms.Button
    Friend WithEvents btnSaveDe As System.Windows.Forms.Button
    Friend WithEvents btnOpenDe As System.Windows.Forms.Button
    Friend WithEvents lblModelDe As System.Windows.Forms.Label
    Friend WithEvents pnlBottom As System.Windows.Forms.Panel
    Friend WithEvents lblSession As System.Windows.Forms.Label
    Friend WithEvents btnUsers As System.Windows.Forms.Button
    Friend WithEvents btnAudit As System.Windows.Forms.Button
    Friend WithEvents btnLogout As System.Windows.Forms.Button

End Class
End Namespace
