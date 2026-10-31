Namespace Views
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmUsers
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
        Me.lvUsers = New Ptz.App.Controls.BufferedListView()
        Me.grpCreate = New System.Windows.Forms.GroupBox()
        Me.lblNewUser = New System.Windows.Forms.Label()
        Me.txtNewUser = New System.Windows.Forms.TextBox()
        Me.lblNewPass = New System.Windows.Forms.Label()
        Me.txtNewPass = New System.Windows.Forms.TextBox()
        Me.lblNewRole = New System.Windows.Forms.Label()
        Me.cmbNewRole = New System.Windows.Forms.ComboBox()
        Me.btnCreate = New System.Windows.Forms.Button()
        Me.grpSelected = New System.Windows.Forms.GroupBox()
        Me.lblSel = New System.Windows.Forms.Label()
        Me.cmbRole = New System.Windows.Forms.ComboBox()
        Me.btnSetRole = New System.Windows.Forms.Button()
        Me.btnToggle = New System.Windows.Forms.Button()
        Me.txtResetPass = New System.Windows.Forms.TextBox()
        Me.btnReset = New System.Windows.Forms.Button()
        Me.lblPolicy = New System.Windows.Forms.Label()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.grpCreate.SuspendLayout()
        Me.grpSelected.SuspendLayout()
        Me.SuspendLayout()
        '
        'lvUsers
        '
        Me.lvUsers.Name = "lvUsers"
        Me.lvUsers.Location = New System.Drawing.Point(12, 12)
        Me.lvUsers.Size = New System.Drawing.Size(440, 440)
        Me.lvUsers.UseCompatibleStateImageBehavior = False
        Me.lvUsers.MultiSelect = False
        Me.lvUsers.View = System.Windows.Forms.View.Details
        Me.lvUsers.FullRowSelect = True
        '
        'grpCreate
        '
        Me.grpCreate.Name = "grpCreate"
        Me.grpCreate.Location = New System.Drawing.Point(464, 12)
        Me.grpCreate.Size = New System.Drawing.Size(384, 170)
        Me.grpCreate.Text = "إنشاء مستخدم"
        '
        'lblNewUser
        '
        Me.lblNewUser.Name = "lblNewUser"
        Me.lblNewUser.AutoSize = True
        Me.lblNewUser.Location = New System.Drawing.Point(14, 32)
        Me.lblNewUser.Text = "الاسم"
        '
        'txtNewUser
        '
        Me.txtNewUser.Name = "txtNewUser"
        Me.txtNewUser.Location = New System.Drawing.Point(120, 28)
        Me.txtNewUser.Size = New System.Drawing.Size(250, 23)
        Me.txtNewUser.MaxLength = 32
        '
        'lblNewPass
        '
        Me.lblNewPass.Name = "lblNewPass"
        Me.lblNewPass.AutoSize = True
        Me.lblNewPass.Location = New System.Drawing.Point(14, 64)
        Me.lblNewPass.Text = "كلمة المرور"
        '
        'txtNewPass
        '
        Me.txtNewPass.Name = "txtNewPass"
        Me.txtNewPass.Location = New System.Drawing.Point(120, 60)
        Me.txtNewPass.Size = New System.Drawing.Size(250, 23)
        Me.txtNewPass.UseSystemPasswordChar = True
        Me.txtNewPass.MaxLength = 128
        '
        'lblNewRole
        '
        Me.lblNewRole.Name = "lblNewRole"
        Me.lblNewRole.AutoSize = True
        Me.lblNewRole.Location = New System.Drawing.Point(14, 96)
        Me.lblNewRole.Text = "الدور"
        '
        'cmbNewRole
        '
        Me.cmbNewRole.Name = "cmbNewRole"
        Me.cmbNewRole.Location = New System.Drawing.Point(120, 92)
        Me.cmbNewRole.Size = New System.Drawing.Size(250, 23)
        Me.cmbNewRole.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        '
        'btnCreate
        '
        Me.btnCreate.Name = "btnCreate"
        Me.btnCreate.Location = New System.Drawing.Point(120, 126)
        Me.btnCreate.Size = New System.Drawing.Size(250, 30)
        Me.btnCreate.Text = "إنشاء"
        Me.btnCreate.UseVisualStyleBackColor = True
        '
        'grpSelected
        '
        Me.grpSelected.Name = "grpSelected"
        Me.grpSelected.Location = New System.Drawing.Point(464, 192)
        Me.grpSelected.Size = New System.Drawing.Size(384, 220)
        Me.grpSelected.Text = "المستخدم المحدد"
        '
        'lblSel
        '
        Me.lblSel.Name = "lblSel"
        Me.lblSel.AutoSize = True
        Me.lblSel.Location = New System.Drawing.Point(14, 28)
        Me.lblSel.Text = "—"
        Me.lblSel.Tag = "muted"
        '
        'cmbRole
        '
        Me.cmbRole.Name = "cmbRole"
        Me.cmbRole.Location = New System.Drawing.Point(14, 56)
        Me.cmbRole.Size = New System.Drawing.Size(230, 23)
        Me.cmbRole.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        '
        'btnSetRole
        '
        Me.btnSetRole.Name = "btnSetRole"
        Me.btnSetRole.Location = New System.Drawing.Point(252, 54)
        Me.btnSetRole.Size = New System.Drawing.Size(118, 28)
        Me.btnSetRole.Text = "تغيير الدور"
        Me.btnSetRole.UseVisualStyleBackColor = True
        '
        'btnToggle
        '
        Me.btnToggle.Name = "btnToggle"
        Me.btnToggle.Location = New System.Drawing.Point(14, 92)
        Me.btnToggle.Size = New System.Drawing.Size(356, 28)
        Me.btnToggle.Text = "تعطيل / تفعيل الحساب"
        Me.btnToggle.UseVisualStyleBackColor = True
        '
        'txtResetPass
        '
        Me.txtResetPass.Name = "txtResetPass"
        Me.txtResetPass.Location = New System.Drawing.Point(14, 134)
        Me.txtResetPass.Size = New System.Drawing.Size(230, 23)
        Me.txtResetPass.UseSystemPasswordChar = True
        Me.txtResetPass.MaxLength = 128
        Me.txtResetPass.PlaceholderText = "كلمة مرور جديدة"
        '
        'btnReset
        '
        Me.btnReset.Name = "btnReset"
        Me.btnReset.Location = New System.Drawing.Point(252, 132)
        Me.btnReset.Size = New System.Drawing.Size(118, 28)
        Me.btnReset.Text = "تعيين كلمة المرور"
        Me.btnReset.UseVisualStyleBackColor = True
        '
        'lblPolicy
        '
        Me.lblPolicy.Name = "lblPolicy"
        Me.lblPolicy.Location = New System.Drawing.Point(464, 420)
        Me.lblPolicy.Size = New System.Drawing.Size(384, 56)
        Me.lblPolicy.Text = "السياسة: 12 حرفاً على الأقل، وحرف كبير وصغير ورقم ورمز، ولا تحوي اسم المستخدم."
        Me.lblPolicy.Tag = "muted"
        '
        'btnClose
        '
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Location = New System.Drawing.Point(748, 478)
        Me.btnClose.Size = New System.Drawing.Size(100, 30)
        Me.btnClose.Text = "إغلاق"
        Me.btnClose.UseVisualStyleBackColor = True
        Me.Controls.Add(Me.lvUsers)
        Me.Controls.Add(Me.grpCreate)
        Me.Controls.Add(Me.grpSelected)
        Me.Controls.Add(Me.lblPolicy)
        Me.Controls.Add(Me.btnClose)
        Me.grpCreate.Controls.Add(Me.lblNewUser)
        Me.grpCreate.Controls.Add(Me.txtNewUser)
        Me.grpCreate.Controls.Add(Me.lblNewPass)
        Me.grpCreate.Controls.Add(Me.txtNewPass)
        Me.grpCreate.Controls.Add(Me.lblNewRole)
        Me.grpCreate.Controls.Add(Me.cmbNewRole)
        Me.grpCreate.Controls.Add(Me.btnCreate)
        Me.grpSelected.Controls.Add(Me.lblSel)
        Me.grpSelected.Controls.Add(Me.cmbRole)
        Me.grpSelected.Controls.Add(Me.btnSetRole)
        Me.grpSelected.Controls.Add(Me.btnToggle)
        Me.grpSelected.Controls.Add(Me.txtResetPass)
        Me.grpSelected.Controls.Add(Me.btnReset)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(860, 520)
        Me.Name = "FrmUsers"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "إدارة المستخدمين"
        Me.MinimumSize = New System.Drawing.Size(860, 520)
        Me.grpSelected.ResumeLayout(False)
        Me.grpSelected.PerformLayout()
        Me.grpCreate.ResumeLayout(False)
        Me.grpCreate.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lvUsers As Ptz.App.Controls.BufferedListView
    Friend WithEvents grpCreate As System.Windows.Forms.GroupBox
    Friend WithEvents lblNewUser As System.Windows.Forms.Label
    Friend WithEvents txtNewUser As System.Windows.Forms.TextBox
    Friend WithEvents lblNewPass As System.Windows.Forms.Label
    Friend WithEvents txtNewPass As System.Windows.Forms.TextBox
    Friend WithEvents lblNewRole As System.Windows.Forms.Label
    Friend WithEvents cmbNewRole As System.Windows.Forms.ComboBox
    Friend WithEvents btnCreate As System.Windows.Forms.Button
    Friend WithEvents grpSelected As System.Windows.Forms.GroupBox
    Friend WithEvents lblSel As System.Windows.Forms.Label
    Friend WithEvents cmbRole As System.Windows.Forms.ComboBox
    Friend WithEvents btnSetRole As System.Windows.Forms.Button
    Friend WithEvents btnToggle As System.Windows.Forms.Button
    Friend WithEvents txtResetPass As System.Windows.Forms.TextBox
    Friend WithEvents btnReset As System.Windows.Forms.Button
    Friend WithEvents lblPolicy As System.Windows.Forms.Label
    Friend WithEvents btnClose As System.Windows.Forms.Button

End Class
End Namespace
