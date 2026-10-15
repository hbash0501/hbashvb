<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class UserManagerForm
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
        Me.headerPanel = New System.Windows.Forms.Panel()
        Me.lblSub = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.editorPanel = New System.Windows.Forms.Panel()
        Me.chkActive = New System.Windows.Forms.CheckBox()
        Me.btnNew = New System.Windows.Forms.Button()
        Me.btnDeactivate = New System.Windows.Forms.Button()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.cboRole = New System.Windows.Forms.ComboBox()
        Me.lblRole = New System.Windows.Forms.Label()
        Me.txtPassword = New System.Windows.Forms.TextBox()
        Me.lblPassword = New System.Windows.Forms.Label()
        Me.txtUserName = New System.Windows.Forms.TextBox()
        Me.lblUserName = New System.Windows.Forms.Label()
        Me.txtFullName = New System.Windows.Forms.TextBox()
        Me.lblFullName = New System.Windows.Forms.Label()
        Me.dgvUsers = New System.Windows.Forms.DataGridView()
        Me.headerPanel.SuspendLayout()
        Me.editorPanel.SuspendLayout()
        CType(Me.dgvUsers, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'headerPanel
        '
        Me.headerPanel.BackColor = System.Drawing.Color.White
        Me.headerPanel.Controls.Add(Me.lblSub)
        Me.headerPanel.Controls.Add(Me.lblTitle)
        Me.headerPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.headerPanel.Location = New System.Drawing.Point(0, 0)
        Me.headerPanel.Name = "headerPanel"
        Me.headerPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.headerPanel.Size = New System.Drawing.Size(1080, 86)
        Me.headerPanel.TabIndex = 0
        '
        'lblSub
        '
        Me.lblSub.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblSub.ForeColor = System.Drawing.Color.FromArgb(CType(CType(92, Byte), Integer), CType(CType(102, Byte), Integer), CType(CType(98, Byte), Integer))
        Me.lblSub.Location = New System.Drawing.Point(18, 45)
        Me.lblSub.Name = "lblSub"
        Me.lblSub.Size = New System.Drawing.Size(1044, 23)
        Me.lblSub.TabIndex = 1
        Me.lblSub.Text = "إدارة حسابات الموظفين والأدوار الأساسية: مدير النظام، موظف أرشفة، مراجع، قارئ."
        Me.lblSub.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 14.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblTitle.Location = New System.Drawing.Point(18, 18)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(1044, 32)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "إدارة المستخدمين والصلاحيات"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'editorPanel
        '
        Me.editorPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(248, Byte), Integer), CType(CType(250, Byte), Integer), CType(CType(249, Byte), Integer))
        Me.editorPanel.Controls.Add(Me.chkActive)
        Me.editorPanel.Controls.Add(Me.btnNew)
        Me.editorPanel.Controls.Add(Me.btnDeactivate)
        Me.editorPanel.Controls.Add(Me.btnSave)
        Me.editorPanel.Controls.Add(Me.cboRole)
        Me.editorPanel.Controls.Add(Me.lblRole)
        Me.editorPanel.Controls.Add(Me.txtPassword)
        Me.editorPanel.Controls.Add(Me.lblPassword)
        Me.editorPanel.Controls.Add(Me.txtUserName)
        Me.editorPanel.Controls.Add(Me.lblUserName)
        Me.editorPanel.Controls.Add(Me.txtFullName)
        Me.editorPanel.Controls.Add(Me.lblFullName)
        Me.editorPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.editorPanel.Location = New System.Drawing.Point(0, 86)
        Me.editorPanel.Name = "editorPanel"
        Me.editorPanel.Padding = New System.Windows.Forms.Padding(16)
        Me.editorPanel.Size = New System.Drawing.Size(1080, 128)
        Me.editorPanel.TabIndex = 1
        '
        'chkActive
        '
        Me.chkActive.AutoSize = True
        Me.chkActive.Checked = True
        Me.chkActive.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkActive.Location = New System.Drawing.Point(412, 86)
        Me.chkActive.Name = "chkActive"
        Me.chkActive.Size = New System.Drawing.Size(93, 22)
        Me.chkActive.TabIndex = 11
        Me.chkActive.Text = "حساب نشط"
        Me.chkActive.UseVisualStyleBackColor = True
        '
        'btnNew
        '
        Me.btnNew.BackColor = System.Drawing.Color.FromArgb(CType(CType(240, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(241, Byte), Integer))
        Me.btnNew.FlatAppearance.BorderSize = 0
        Me.btnNew.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnNew.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnNew.Location = New System.Drawing.Point(18, 78)
        Me.btnNew.Name = "btnNew"
        Me.btnNew.Size = New System.Drawing.Size(120, 36)
        Me.btnNew.TabIndex = 10
        Me.btnNew.Text = "جديد"
        Me.btnNew.UseVisualStyleBackColor = False
        '
        'btnDeactivate
        '
        Me.btnDeactivate.BackColor = System.Drawing.Color.FromArgb(CType(CType(251, Byte), Integer), CType(CType(233, Byte), Integer), CType(CType(233, Byte), Integer))
        Me.btnDeactivate.FlatAppearance.BorderSize = 0
        Me.btnDeactivate.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnDeactivate.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnDeactivate.Location = New System.Drawing.Point(144, 78)
        Me.btnDeactivate.Name = "btnDeactivate"
        Me.btnDeactivate.Size = New System.Drawing.Size(120, 36)
        Me.btnDeactivate.TabIndex = 9
        Me.btnDeactivate.Text = "تعطيل"
        Me.btnDeactivate.UseVisualStyleBackColor = False
        '
        'btnSave
        '
        Me.btnSave.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnSave.FlatAppearance.BorderSize = 0
        Me.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSave.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnSave.ForeColor = System.Drawing.Color.White
        Me.btnSave.Location = New System.Drawing.Point(270, 78)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(126, 36)
        Me.btnSave.TabIndex = 8
        Me.btnSave.Text = "حفظ"
        Me.btnSave.UseVisualStyleBackColor = False
        '
        'cboRole
        '
        Me.cboRole.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboRole.FormattingEnabled = True
        Me.cboRole.Location = New System.Drawing.Point(18, 41)
        Me.cboRole.Name = "cboRole"
        Me.cboRole.Size = New System.Drawing.Size(190, 26)
        Me.cboRole.TabIndex = 7
        '
        'lblRole
        '
        Me.lblRole.Location = New System.Drawing.Point(18, 18)
        Me.lblRole.Name = "lblRole"
        Me.lblRole.Size = New System.Drawing.Size(190, 23)
        Me.lblRole.TabIndex = 6
        Me.lblRole.Text = "الدور"
        Me.lblRole.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtPassword
        '
        Me.txtPassword.Location = New System.Drawing.Point(214, 41)
        Me.txtPassword.Name = "txtPassword"
        Me.txtPassword.PasswordChar = Global.Microsoft.VisualBasic.ChrW(9679)
        Me.txtPassword.Size = New System.Drawing.Size(190, 26)
        Me.txtPassword.TabIndex = 5
        '
        'lblPassword
        '
        Me.lblPassword.Location = New System.Drawing.Point(214, 18)
        Me.lblPassword.Name = "lblPassword"
        Me.lblPassword.Size = New System.Drawing.Size(190, 23)
        Me.lblPassword.TabIndex = 4
        Me.lblPassword.Text = "كلمة المرور الجديدة"
        Me.lblPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtUserName
        '
        Me.txtUserName.Location = New System.Drawing.Point(410, 41)
        Me.txtUserName.Name = "txtUserName"
        Me.txtUserName.Size = New System.Drawing.Size(238, 26)
        Me.txtUserName.TabIndex = 3
        '
        'lblUserName
        '
        Me.lblUserName.Location = New System.Drawing.Point(410, 18)
        Me.lblUserName.Name = "lblUserName"
        Me.lblUserName.Size = New System.Drawing.Size(238, 23)
        Me.lblUserName.TabIndex = 2
        Me.lblUserName.Text = "اسم المستخدم"
        Me.lblUserName.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtFullName
        '
        Me.txtFullName.Location = New System.Drawing.Point(654, 41)
        Me.txtFullName.Name = "txtFullName"
        Me.txtFullName.Size = New System.Drawing.Size(408, 26)
        Me.txtFullName.TabIndex = 1
        '
        'lblFullName
        '
        Me.lblFullName.Location = New System.Drawing.Point(654, 18)
        Me.lblFullName.Name = "lblFullName"
        Me.lblFullName.Size = New System.Drawing.Size(408, 23)
        Me.lblFullName.TabIndex = 0
        Me.lblFullName.Text = "اسم الموظف"
        Me.lblFullName.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'dgvUsers
        '
        Me.dgvUsers.AllowUserToAddRows = False
        Me.dgvUsers.AllowUserToDeleteRows = False
        Me.dgvUsers.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvUsers.BackgroundColor = System.Drawing.Color.White
        Me.dgvUsers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.dgvUsers.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvUsers.Location = New System.Drawing.Point(0, 214)
        Me.dgvUsers.MultiSelect = False
        Me.dgvUsers.Name = "dgvUsers"
        Me.dgvUsers.ReadOnly = True
        Me.dgvUsers.RowHeadersVisible = False
        Me.dgvUsers.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvUsers.Size = New System.Drawing.Size(1080, 446)
        Me.dgvUsers.TabIndex = 2
        '
        'UserManagerForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1080, 660)
        Me.Controls.Add(Me.dgvUsers)
        Me.Controls.Add(Me.editorPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(950, 560)
        Me.Name = "UserManagerForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "إدارة المستخدمين"
        Me.headerPanel.ResumeLayout(False)
        Me.editorPanel.ResumeLayout(False)
        Me.editorPanel.PerformLayout()
        CType(Me.dgvUsers, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblSub As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents editorPanel As System.Windows.Forms.Panel
    Friend WithEvents chkActive As System.Windows.Forms.CheckBox
    Friend WithEvents btnNew As System.Windows.Forms.Button
    Friend WithEvents btnDeactivate As System.Windows.Forms.Button
    Friend WithEvents btnSave As System.Windows.Forms.Button
    Friend WithEvents cboRole As System.Windows.Forms.ComboBox
    Friend WithEvents lblRole As System.Windows.Forms.Label
    Friend WithEvents txtPassword As System.Windows.Forms.TextBox
    Friend WithEvents lblPassword As System.Windows.Forms.Label
    Friend WithEvents txtUserName As System.Windows.Forms.TextBox
    Friend WithEvents lblUserName As System.Windows.Forms.Label
    Friend WithEvents txtFullName As System.Windows.Forms.TextBox
    Friend WithEvents lblFullName As System.Windows.Forms.Label
    Friend WithEvents dgvUsers As System.Windows.Forms.DataGridView
End Class
