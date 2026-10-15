<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class ArchiveWizardForm
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
        Me.lblHint = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.stepsPanel = New System.Windows.Forms.Panel()
        Me.lblStep4 = New System.Windows.Forms.Label()
        Me.lblStep3 = New System.Windows.Forms.Label()
        Me.lblStep2 = New System.Windows.Forms.Label()
        Me.lblStep1 = New System.Windows.Forms.Label()
        Me.mainPanel = New System.Windows.Forms.Panel()
        Me.lblResultTitle = New System.Windows.Forms.Label()
        Me.txtResult = New System.Windows.Forms.TextBox()
        Me.actionsPanel = New System.Windows.Forms.Panel()
        Me.btnDone = New System.Windows.Forms.Button()
        Me.btnOcrReport = New System.Windows.Forms.Button()
        Me.btnOpenEditor = New System.Windows.Forms.Button()
        Me.btnAnalyzeSave = New System.Windows.Forms.Button()
        Me.sourcePanel = New System.Windows.Forms.Panel()
        Me.txtSourcePath = New System.Windows.Forms.TextBox()
        Me.lblSource = New System.Windows.Forms.Label()
        Me.btnChooseFolder = New System.Windows.Forms.Button()
        Me.btnCamera = New System.Windows.Forms.Button()
        Me.btnScan = New System.Windows.Forms.Button()
        Me.btnChooseFile = New System.Windows.Forms.Button()
        Me.headerPanel.SuspendLayout()
        Me.stepsPanel.SuspendLayout()
        Me.mainPanel.SuspendLayout()
        Me.actionsPanel.SuspendLayout()
        Me.sourcePanel.SuspendLayout()
        Me.SuspendLayout()
        '
        'headerPanel
        '
        Me.headerPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.headerPanel.Controls.Add(Me.lblHint)
        Me.headerPanel.Controls.Add(Me.lblTitle)
        Me.headerPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.headerPanel.Location = New System.Drawing.Point(0, 0)
        Me.headerPanel.Name = "headerPanel"
        Me.headerPanel.Padding = New System.Windows.Forms.Padding(24)
        Me.headerPanel.Size = New System.Drawing.Size(980, 108)
        Me.headerPanel.TabIndex = 0
        '
        'lblHint
        '
        Me.lblHint.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblHint.Font = New System.Drawing.Font("Segoe UI", 9.75!)
        Me.lblHint.ForeColor = System.Drawing.Color.FromArgb(CType(CType(222, Byte), Integer), CType(CType(231, Byte), Integer), CType(CType(229, Byte), Integer))
        Me.lblHint.Location = New System.Drawing.Point(24, 66)
        Me.lblHint.Name = "lblHint"
        Me.lblHint.Size = New System.Drawing.Size(932, 18)
        Me.lblHint.TabIndex = 1
        Me.lblHint.Text = "اختر الملف أو امسح بالسكانر، ثم اضغط إدراج ومعالجة وتابع الإشعار حتى النهاية."
        Me.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Segoe UI", 18.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(24, 24)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(932, 40)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "إدراج الوثائق إلى بصيرة"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'stepsPanel
        '
        Me.stepsPanel.BackColor = System.Drawing.Color.White
        Me.stepsPanel.Controls.Add(Me.lblStep4)
        Me.stepsPanel.Controls.Add(Me.lblStep3)
        Me.stepsPanel.Controls.Add(Me.lblStep2)
        Me.stepsPanel.Controls.Add(Me.lblStep1)
        Me.stepsPanel.Dock = System.Windows.Forms.DockStyle.Right
        Me.stepsPanel.Location = New System.Drawing.Point(730, 108)
        Me.stepsPanel.Name = "stepsPanel"
        Me.stepsPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.stepsPanel.Size = New System.Drawing.Size(250, 532)
        Me.stepsPanel.TabIndex = 1
        '
        'lblStep1
        '
        Me.lblStep1.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.lblStep1.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblStep1.Location = New System.Drawing.Point(18, 28)
        Me.lblStep1.Name = "lblStep1"
        Me.lblStep1.Size = New System.Drawing.Size(214, 62)
        Me.lblStep1.TabIndex = 1
        Me.lblStep1.Text = "1. اختر مصدر المستند"
        Me.lblStep1.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblStep2
        '
        Me.lblStep2.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.lblStep2.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblStep2.Location = New System.Drawing.Point(18, 104)
        Me.lblStep2.Name = "lblStep2"
        Me.lblStep2.Size = New System.Drawing.Size(214, 62)
        Me.lblStep2.TabIndex = 2
        Me.lblStep2.Text = "2. إدراج ومعالجة بالخلفية"
        Me.lblStep2.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblStep3
        '
        Me.lblStep3.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.lblStep3.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblStep3.Location = New System.Drawing.Point(18, 180)
        Me.lblStep3.Name = "lblStep3"
        Me.lblStep3.Size = New System.Drawing.Size(214, 62)
        Me.lblStep3.TabIndex = 3
        Me.lblStep3.Text = "3. متابعة إشعار التقدم"
        Me.lblStep3.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblStep4
        '
        Me.lblStep4.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.lblStep4.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblStep4.Location = New System.Drawing.Point(18, 256)
        Me.lblStep4.Name = "lblStep4"
        Me.lblStep4.Size = New System.Drawing.Size(214, 62)
        Me.lblStep4.TabIndex = 4
        Me.lblStep4.Text = "4. مراجعة البيانات والنص"
        Me.lblStep4.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'mainPanel
        '
        Me.mainPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(244, Byte), Integer), CType(CType(247, Byte), Integer), CType(CType(246, Byte), Integer))
        Me.mainPanel.Controls.Add(Me.lblResultTitle)
        Me.mainPanel.Controls.Add(Me.txtResult)
        Me.mainPanel.Controls.Add(Me.actionsPanel)
        Me.mainPanel.Controls.Add(Me.sourcePanel)
        Me.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.mainPanel.Location = New System.Drawing.Point(0, 108)
        Me.mainPanel.Name = "mainPanel"
        Me.mainPanel.Padding = New System.Windows.Forms.Padding(22)
        Me.mainPanel.Size = New System.Drawing.Size(730, 532)
        Me.mainPanel.TabIndex = 2
        '
        'sourcePanel
        '
        Me.sourcePanel.BackColor = System.Drawing.Color.White
        Me.sourcePanel.Controls.Add(Me.txtSourcePath)
        Me.sourcePanel.Controls.Add(Me.lblSource)
        Me.sourcePanel.Controls.Add(Me.btnChooseFolder)
        Me.sourcePanel.Controls.Add(Me.btnCamera)
        Me.sourcePanel.Controls.Add(Me.btnScan)
        Me.sourcePanel.Controls.Add(Me.btnChooseFile)
        Me.sourcePanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.sourcePanel.Location = New System.Drawing.Point(22, 22)
        Me.sourcePanel.Name = "sourcePanel"
        Me.sourcePanel.Padding = New System.Windows.Forms.Padding(16)
        Me.sourcePanel.Size = New System.Drawing.Size(686, 154)
        Me.sourcePanel.TabIndex = 1
        '
        'btnChooseFile
        '
        Me.btnChooseFile.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.btnChooseFile.FlatAppearance.BorderSize = 0
        Me.btnChooseFile.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnChooseFile.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnChooseFile.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnChooseFile.Location = New System.Drawing.Point(520, 88)
        Me.btnChooseFile.Name = "btnChooseFile"
        Me.btnChooseFile.Size = New System.Drawing.Size(150, 40)
        Me.btnChooseFile.TabIndex = 1
        Me.btnChooseFile.Text = "استيراد ملف"
        Me.btnChooseFile.UseVisualStyleBackColor = False
        '
        'btnCamera
        '
        Me.btnCamera.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnCamera.FlatAppearance.BorderSize = 0
        Me.btnCamera.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnCamera.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnCamera.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnCamera.Location = New System.Drawing.Point(208, 88)
        Me.btnCamera.Name = "btnCamera"
        Me.btnCamera.Size = New System.Drawing.Size(150, 40)
        Me.btnCamera.TabIndex = 6
        Me.btnCamera.Text = "إدراج بالكاميرا"
        Me.btnCamera.UseVisualStyleBackColor = False
        '
        'btnScan
        '
        Me.btnScan.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnScan.FlatAppearance.BorderSize = 0
        Me.btnScan.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnScan.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnScan.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnScan.Location = New System.Drawing.Point(364, 88)
        Me.btnScan.Name = "btnScan"
        Me.btnScan.Size = New System.Drawing.Size(150, 40)
        Me.btnScan.TabIndex = 2
        Me.btnScan.Text = "مسح بالسكانر"
        Me.btnScan.UseVisualStyleBackColor = False
        '
        'btnChooseFolder
        '
        Me.btnChooseFolder.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnChooseFolder.FlatAppearance.BorderSize = 0
        Me.btnChooseFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnChooseFolder.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnChooseFolder.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnChooseFolder.Location = New System.Drawing.Point(52, 88)
        Me.btnChooseFolder.Name = "btnChooseFolder"
        Me.btnChooseFolder.Size = New System.Drawing.Size(150, 40)
        Me.btnChooseFolder.TabIndex = 3
        Me.btnChooseFolder.Text = "استيراد مجلد"
        Me.btnChooseFolder.UseVisualStyleBackColor = False
        '
        'lblSource
        '
        Me.lblSource.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblSource.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.lblSource.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblSource.Location = New System.Drawing.Point(16, 16)
        Me.lblSource.Name = "lblSource"
        Me.lblSource.Size = New System.Drawing.Size(654, 28)
        Me.lblSource.TabIndex = 4
        Me.lblSource.Text = "مصدر المستند"
        Me.lblSource.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtSourcePath
        '
        Me.txtSourcePath.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtSourcePath.BackColor = System.Drawing.Color.White
        Me.txtSourcePath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtSourcePath.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.txtSourcePath.Location = New System.Drawing.Point(16, 51)
        Me.txtSourcePath.Name = "txtSourcePath"
        Me.txtSourcePath.ReadOnly = True
        Me.txtSourcePath.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.txtSourcePath.Size = New System.Drawing.Size(654, 26)
        Me.txtSourcePath.TabIndex = 5
        '
        'actionsPanel
        '
        Me.actionsPanel.Controls.Add(Me.btnDone)
        Me.actionsPanel.Controls.Add(Me.btnOcrReport)
        Me.actionsPanel.Controls.Add(Me.btnOpenEditor)
        Me.actionsPanel.Controls.Add(Me.btnAnalyzeSave)
        Me.actionsPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.actionsPanel.Location = New System.Drawing.Point(22, 176)
        Me.actionsPanel.Name = "actionsPanel"
        Me.actionsPanel.Size = New System.Drawing.Size(686, 64)
        Me.actionsPanel.TabIndex = 2
        '
        'btnAnalyzeSave
        '
        Me.btnAnalyzeSave.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnAnalyzeSave.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnAnalyzeSave.FlatAppearance.BorderSize = 0
        Me.btnAnalyzeSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAnalyzeSave.Font = New System.Drawing.Font("Segoe UI", 9.5!, System.Drawing.FontStyle.Bold)
        Me.btnAnalyzeSave.ForeColor = System.Drawing.Color.White
        Me.btnAnalyzeSave.Location = New System.Drawing.Point(496, 12)
        Me.btnAnalyzeSave.Name = "btnAnalyzeSave"
        Me.btnAnalyzeSave.Size = New System.Drawing.Size(190, 42)
        Me.btnAnalyzeSave.TabIndex = 1
        Me.btnAnalyzeSave.Text = "إدراج ومعالجة"
        Me.btnAnalyzeSave.UseVisualStyleBackColor = False
        '
        'btnOpenEditor
        '
        Me.btnOpenEditor.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnOpenEditor.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnOpenEditor.Enabled = False
        Me.btnOpenEditor.FlatAppearance.BorderSize = 0
        Me.btnOpenEditor.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOpenEditor.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnOpenEditor.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnOpenEditor.Location = New System.Drawing.Point(330, 12)
        Me.btnOpenEditor.Name = "btnOpenEditor"
        Me.btnOpenEditor.Size = New System.Drawing.Size(160, 42)
        Me.btnOpenEditor.TabIndex = 2
        Me.btnOpenEditor.Text = "مراجعة الحقول"
        Me.btnOpenEditor.UseVisualStyleBackColor = False
        '
        'btnOcrReport
        '
        Me.btnOcrReport.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnOcrReport.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnOcrReport.Enabled = False
        Me.btnOcrReport.FlatAppearance.BorderSize = 0
        Me.btnOcrReport.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOcrReport.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnOcrReport.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnOcrReport.Location = New System.Drawing.Point(164, 12)
        Me.btnOcrReport.Name = "btnOcrReport"
        Me.btnOcrReport.Size = New System.Drawing.Size(160, 42)
        Me.btnOcrReport.TabIndex = 3
        Me.btnOcrReport.Text = "تقرير OCR"
        Me.btnOcrReport.UseVisualStyleBackColor = False
        '
        'btnDone
        '
        Me.btnDone.BackColor = System.Drawing.Color.White
        Me.btnDone.FlatAppearance.BorderSize = 0
        Me.btnDone.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnDone.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnDone.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnDone.Location = New System.Drawing.Point(0, 12)
        Me.btnDone.Name = "btnDone"
        Me.btnDone.Size = New System.Drawing.Size(150, 42)
        Me.btnDone.TabIndex = 4
        Me.btnDone.Text = "إنهاء"
        Me.btnDone.UseVisualStyleBackColor = False
        '
        'txtResult
        '
        Me.txtResult.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtResult.BackColor = System.Drawing.Color.White
        Me.txtResult.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtResult.Font = New System.Drawing.Font("Segoe UI", 9.5!)
        Me.txtResult.Location = New System.Drawing.Point(22, 284)
        Me.txtResult.Multiline = True
        Me.txtResult.Name = "txtResult"
        Me.txtResult.ReadOnly = True
        Me.txtResult.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtResult.Size = New System.Drawing.Size(686, 226)
        Me.txtResult.TabIndex = 3
        '
        'lblResultTitle
        '
        Me.lblResultTitle.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.lblResultTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblResultTitle.Location = New System.Drawing.Point(22, 252)
        Me.lblResultTitle.Name = "lblResultTitle"
        Me.lblResultTitle.Size = New System.Drawing.Size(686, 28)
        Me.lblResultTitle.TabIndex = 4
        Me.lblResultTitle.Text = "نتيجة المعالج"
        Me.lblResultTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'ArchiveWizardForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(980, 640)
        Me.Controls.Add(Me.mainPanel)
        Me.Controls.Add(Me.stepsPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(920, 620)
        Me.Name = "ArchiveWizardForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "بصيرة | إدراج الوثائق والسكانر"
        Me.headerPanel.ResumeLayout(False)
        Me.stepsPanel.ResumeLayout(False)
        Me.mainPanel.ResumeLayout(False)
        Me.mainPanel.PerformLayout()
        Me.actionsPanel.ResumeLayout(False)
        Me.sourcePanel.ResumeLayout(False)
        Me.sourcePanel.PerformLayout()
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblHint As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents stepsPanel As System.Windows.Forms.Panel
    Friend WithEvents lblStep4 As System.Windows.Forms.Label
    Friend WithEvents lblStep3 As System.Windows.Forms.Label
    Friend WithEvents lblStep2 As System.Windows.Forms.Label
    Friend WithEvents lblStep1 As System.Windows.Forms.Label
    Friend WithEvents mainPanel As System.Windows.Forms.Panel
    Friend WithEvents lblResultTitle As System.Windows.Forms.Label
    Friend WithEvents txtResult As System.Windows.Forms.TextBox
    Friend WithEvents actionsPanel As System.Windows.Forms.Panel
    Friend WithEvents btnDone As System.Windows.Forms.Button
    Friend WithEvents btnOcrReport As System.Windows.Forms.Button
    Friend WithEvents btnOpenEditor As System.Windows.Forms.Button
    Friend WithEvents btnAnalyzeSave As System.Windows.Forms.Button
    Friend WithEvents sourcePanel As System.Windows.Forms.Panel
    Friend WithEvents txtSourcePath As System.Windows.Forms.TextBox
    Friend WithEvents lblSource As System.Windows.Forms.Label
    Friend WithEvents btnChooseFolder As System.Windows.Forms.Button
    Friend WithEvents btnCamera As System.Windows.Forms.Button
    Friend WithEvents btnScan As System.Windows.Forms.Button
    Friend WithEvents btnChooseFile As System.Windows.Forms.Button
End Class
