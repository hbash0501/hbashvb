
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class SettingsForm
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
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.contentPanel = New System.Windows.Forms.Panel()
        Me.chkOpenAI = New System.Windows.Forms.CheckBox()
        Me.btnTestOpenAI = New System.Windows.Forms.Button()
        Me.txtOpenAiModel = New System.Windows.Forms.TextBox()
        Me.lblOpenAiModel = New System.Windows.Forms.Label()
        Me.txtOpenAiEndpoint = New System.Windows.Forms.TextBox()
        Me.lblOpenAiEndpoint = New System.Windows.Forms.Label()
        Me.txtEmployee = New System.Windows.Forms.TextBox()
        Me.lblEmployee = New System.Windows.Forms.Label()
        Me.txtOcrLang = New System.Windows.Forms.TextBox()
        Me.lblOcrLang = New System.Windows.Forms.Label()
        Me.btnBrowseTess = New System.Windows.Forms.Button()
        Me.txtTesseract = New System.Windows.Forms.TextBox()
        Me.lblTesseract = New System.Windows.Forms.Label()
        Me.btnBrowseArchive = New System.Windows.Forms.Button()
        Me.txtArchive = New System.Windows.Forms.TextBox()
        Me.lblArchive = New System.Windows.Forms.Label()
        Me.footerPanel = New System.Windows.Forms.Panel()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.headerPanel.SuspendLayout()
        Me.contentPanel.SuspendLayout()
        Me.footerPanel.SuspendLayout()
        Me.SuspendLayout()
        '
        'headerPanel
        '
        Me.headerPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.headerPanel.Controls.Add(Me.lblTitle)
        Me.headerPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.headerPanel.Location = New System.Drawing.Point(0, 0)
        Me.headerPanel.Name = "headerPanel"
        Me.headerPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.headerPanel.Size = New System.Drawing.Size(760, 74)
        Me.headerPanel.TabIndex = 0
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 14.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(18, 18)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(724, 38)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "إعدادات المنصة"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'contentPanel
        '
        Me.contentPanel.BackColor = System.Drawing.Color.White
        Me.contentPanel.Controls.Add(Me.btnTestOpenAI)
        Me.contentPanel.Controls.Add(Me.chkOpenAI)
        Me.contentPanel.Controls.Add(Me.txtOpenAiModel)
        Me.contentPanel.Controls.Add(Me.lblOpenAiModel)
        Me.contentPanel.Controls.Add(Me.txtOpenAiEndpoint)
        Me.contentPanel.Controls.Add(Me.lblOpenAiEndpoint)
        Me.contentPanel.Controls.Add(Me.txtEmployee)
        Me.contentPanel.Controls.Add(Me.lblEmployee)
        Me.contentPanel.Controls.Add(Me.txtOcrLang)
        Me.contentPanel.Controls.Add(Me.lblOcrLang)
        Me.contentPanel.Controls.Add(Me.btnBrowseTess)
        Me.contentPanel.Controls.Add(Me.txtTesseract)
        Me.contentPanel.Controls.Add(Me.lblTesseract)
        Me.contentPanel.Controls.Add(Me.btnBrowseArchive)
        Me.contentPanel.Controls.Add(Me.txtArchive)
        Me.contentPanel.Controls.Add(Me.lblArchive)
        Me.contentPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.contentPanel.Location = New System.Drawing.Point(0, 74)
        Me.contentPanel.Name = "contentPanel"
        Me.contentPanel.Padding = New System.Windows.Forms.Padding(24)
        Me.contentPanel.Size = New System.Drawing.Size(760, 440)
        Me.contentPanel.TabIndex = 1
        '
        'chkOpenAI
        '
        Me.chkOpenAI.AutoSize = True
        Me.chkOpenAI.Location = New System.Drawing.Point(430, 334)
        Me.chkOpenAI.Name = "chkOpenAI"
        Me.chkOpenAI.Size = New System.Drawing.Size(306, 22)
        Me.chkOpenAI.TabIndex = 14
        Me.chkOpenAI.Text = "تفعيل الذكاء السحابي عبر OpenAI"
        Me.chkOpenAI.UseVisualStyleBackColor = True
        '
        'btnTestOpenAI
        '
        Me.btnTestOpenAI.BackColor = System.Drawing.Color.White
        Me.btnTestOpenAI.FlatAppearance.BorderColor = System.Drawing.Color.Gainsboro
        Me.btnTestOpenAI.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnTestOpenAI.Font = New System.Drawing.Font("Tahoma", 8.5!, System.Drawing.FontStyle.Bold)
        Me.btnTestOpenAI.Location = New System.Drawing.Point(24, 328)
        Me.btnTestOpenAI.Name = "btnTestOpenAI"
        Me.btnTestOpenAI.Size = New System.Drawing.Size(170, 32)
        Me.btnTestOpenAI.TabIndex = 15
        Me.btnTestOpenAI.Text = "اختبار OpenAI"
        Me.btnTestOpenAI.UseVisualStyleBackColor = False
        '
        'txtOpenAiModel
        '
        Me.txtOpenAiModel.Location = New System.Drawing.Point(24, 296)
        Me.txtOpenAiModel.Name = "txtOpenAiModel"
        Me.txtOpenAiModel.Size = New System.Drawing.Size(712, 26)
        Me.txtOpenAiModel.TabIndex = 13
        '
        'lblOpenAiModel
        '
        Me.lblOpenAiModel.Location = New System.Drawing.Point(24, 271)
        Me.lblOpenAiModel.Name = "lblOpenAiModel"
        Me.lblOpenAiModel.Size = New System.Drawing.Size(712, 22)
        Me.lblOpenAiModel.TabIndex = 12
        Me.lblOpenAiModel.Text = "اسم نموذج OpenAI"
        '
        'txtOpenAiEndpoint
        '
        Me.txtOpenAiEndpoint.Location = New System.Drawing.Point(24, 238)
        Me.txtOpenAiEndpoint.Name = "txtOpenAiEndpoint"
        Me.txtOpenAiEndpoint.Size = New System.Drawing.Size(712, 26)
        Me.txtOpenAiEndpoint.TabIndex = 11
        '
        'lblOpenAiEndpoint
        '
        Me.lblOpenAiEndpoint.Location = New System.Drawing.Point(24, 213)
        Me.lblOpenAiEndpoint.Name = "lblOpenAiEndpoint"
        Me.lblOpenAiEndpoint.Size = New System.Drawing.Size(712, 22)
        Me.lblOpenAiEndpoint.TabIndex = 10
        Me.lblOpenAiEndpoint.Text = "رابط OpenAI السحابي"
        '
        'txtEmployee
        '
        Me.txtEmployee.Location = New System.Drawing.Point(24, 180)
        Me.txtEmployee.Name = "txtEmployee"
        Me.txtEmployee.Size = New System.Drawing.Size(712, 26)
        Me.txtEmployee.TabIndex = 9
        '
        'lblEmployee
        '
        Me.lblEmployee.Location = New System.Drawing.Point(24, 155)
        Me.lblEmployee.Name = "lblEmployee"
        Me.lblEmployee.Size = New System.Drawing.Size(712, 22)
        Me.lblEmployee.TabIndex = 8
        Me.lblEmployee.Text = "اسم الموظف الافتراضي"
        '
        'txtOcrLang
        '
        Me.txtOcrLang.Location = New System.Drawing.Point(24, 122)
        Me.txtOcrLang.Name = "txtOcrLang"
        Me.txtOcrLang.Size = New System.Drawing.Size(712, 26)
        Me.txtOcrLang.TabIndex = 7
        '
        'lblOcrLang
        '
        Me.lblOcrLang.Location = New System.Drawing.Point(24, 97)
        Me.lblOcrLang.Name = "lblOcrLang"
        Me.lblOcrLang.Size = New System.Drawing.Size(712, 22)
        Me.lblOcrLang.TabIndex = 6
        Me.lblOcrLang.Text = "لغات OCR مثال: ara+eng"
        '
        'btnBrowseTess
        '
        Me.btnBrowseTess.BackColor = System.Drawing.Color.FromArgb(CType(CType(238, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(239, Byte), Integer))
        Me.btnBrowseTess.FlatAppearance.BorderSize = 0
        Me.btnBrowseTess.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnBrowseTess.Font = New System.Drawing.Font("Tahoma", 8.5!, System.Drawing.FontStyle.Bold)
        Me.btnBrowseTess.Location = New System.Drawing.Point(24, 58)
        Me.btnBrowseTess.Name = "btnBrowseTess"
        Me.btnBrowseTess.Size = New System.Drawing.Size(90, 28)
        Me.btnBrowseTess.TabIndex = 5
        Me.btnBrowseTess.Text = "استعراض"
        Me.btnBrowseTess.UseVisualStyleBackColor = False
        '
        'txtTesseract
        '
        Me.txtTesseract.Location = New System.Drawing.Point(120, 59)
        Me.txtTesseract.Name = "txtTesseract"
        Me.txtTesseract.Size = New System.Drawing.Size(616, 26)
        Me.txtTesseract.TabIndex = 4
        '
        'lblTesseract
        '
        Me.lblTesseract.Location = New System.Drawing.Point(24, 34)
        Me.lblTesseract.Name = "lblTesseract"
        Me.lblTesseract.Size = New System.Drawing.Size(712, 22)
        Me.lblTesseract.TabIndex = 3
        Me.lblTesseract.Text = "مسار tesseract.exe"
        '
        'btnBrowseArchive
        '
        Me.btnBrowseArchive.BackColor = System.Drawing.Color.FromArgb(CType(CType(238, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(239, Byte), Integer))
        Me.btnBrowseArchive.FlatAppearance.BorderSize = 0
        Me.btnBrowseArchive.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnBrowseArchive.Font = New System.Drawing.Font("Tahoma", 8.5!, System.Drawing.FontStyle.Bold)
        Me.btnBrowseArchive.Location = New System.Drawing.Point(24, 0)
        Me.btnBrowseArchive.Name = "btnBrowseArchive"
        Me.btnBrowseArchive.Size = New System.Drawing.Size(90, 28)
        Me.btnBrowseArchive.TabIndex = 2
        Me.btnBrowseArchive.Text = "استعراض"
        Me.btnBrowseArchive.UseVisualStyleBackColor = False
        Me.btnBrowseArchive.Visible = False
        '
        'txtArchive
        '
        Me.txtArchive.Location = New System.Drawing.Point(120, 0)
        Me.txtArchive.Name = "txtArchive"
        Me.txtArchive.Size = New System.Drawing.Size(616, 26)
        Me.txtArchive.TabIndex = 1
        Me.txtArchive.Visible = False
        '
        'lblArchive
        '
        Me.lblArchive.Location = New System.Drawing.Point(24, 0)
        Me.lblArchive.Name = "lblArchive"
        Me.lblArchive.Size = New System.Drawing.Size(712, 22)
        Me.lblArchive.TabIndex = 0
        Me.lblArchive.Text = "مجلد الأرشفة"
        Me.lblArchive.Visible = False
        '
        'footerPanel
        '
        Me.footerPanel.BackColor = System.Drawing.Color.White
        Me.footerPanel.Controls.Add(Me.btnCancel)
        Me.footerPanel.Controls.Add(Me.btnSave)
        Me.footerPanel.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.footerPanel.Location = New System.Drawing.Point(0, 460)
        Me.footerPanel.Name = "footerPanel"
        Me.footerPanel.Padding = New System.Windows.Forms.Padding(18, 10, 18, 10)
        Me.footerPanel.Size = New System.Drawing.Size(760, 62)
        Me.footerPanel.TabIndex = 2
        '
        'btnCancel
        '
        Me.btnCancel.BackColor = System.Drawing.Color.FromArgb(CType(CType(238, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(239, Byte), Integer))
        Me.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.btnCancel.Dock = System.Windows.Forms.DockStyle.Left
        Me.btnCancel.FlatAppearance.BorderSize = 0
        Me.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnCancel.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnCancel.Location = New System.Drawing.Point(18, 10)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(130, 42)
        Me.btnCancel.TabIndex = 1
        Me.btnCancel.Text = "إلغاء"
        Me.btnCancel.UseVisualStyleBackColor = False
        '
        'btnSave
        '
        Me.btnSave.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.btnSave.Dock = System.Windows.Forms.DockStyle.Right
        Me.btnSave.FlatAppearance.BorderSize = 0
        Me.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSave.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnSave.Location = New System.Drawing.Point(592, 10)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(150, 42)
        Me.btnSave.TabIndex = 0
        Me.btnSave.Text = "حفظ الإعدادات"
        Me.btnSave.UseVisualStyleBackColor = False
        '
        'SettingsForm
        '
        Me.AcceptButton = Me.btnSave
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(760, 522)
        Me.Controls.Add(Me.contentPanel)
        Me.Controls.Add(Me.footerPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimizeBox = False
        Me.Name = "SettingsForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "الإعدادات"
        Me.headerPanel.ResumeLayout(False)
        Me.contentPanel.ResumeLayout(False)
        Me.contentPanel.PerformLayout()
        Me.footerPanel.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents contentPanel As System.Windows.Forms.Panel
    Friend WithEvents chkOpenAI As System.Windows.Forms.CheckBox
    Friend WithEvents btnTestOpenAI As System.Windows.Forms.Button
    Friend WithEvents txtOpenAiModel As System.Windows.Forms.TextBox
    Friend WithEvents lblOpenAiModel As System.Windows.Forms.Label
    Friend WithEvents txtOpenAiEndpoint As System.Windows.Forms.TextBox
    Friend WithEvents lblOpenAiEndpoint As System.Windows.Forms.Label
    Friend WithEvents txtEmployee As System.Windows.Forms.TextBox
    Friend WithEvents lblEmployee As System.Windows.Forms.Label
    Friend WithEvents txtOcrLang As System.Windows.Forms.TextBox
    Friend WithEvents lblOcrLang As System.Windows.Forms.Label
    Friend WithEvents btnBrowseTess As System.Windows.Forms.Button
    Friend WithEvents txtTesseract As System.Windows.Forms.TextBox
    Friend WithEvents lblTesseract As System.Windows.Forms.Label
    Friend WithEvents btnBrowseArchive As System.Windows.Forms.Button
    Friend WithEvents txtArchive As System.Windows.Forms.TextBox
    Friend WithEvents lblArchive As System.Windows.Forms.Label
    Friend WithEvents footerPanel As System.Windows.Forms.Panel
    Friend WithEvents btnCancel As System.Windows.Forms.Button
    Friend WithEvents btnSave As System.Windows.Forms.Button
End Class
