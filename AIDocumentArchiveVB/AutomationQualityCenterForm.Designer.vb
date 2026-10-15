<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class AutomationQualityCenterForm
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then components.Dispose()
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.pnlHeader = New System.Windows.Forms.Panel()
        Me.lblSubtitle = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.pnlCommands = New System.Windows.Forms.Panel()
        Me.btnProcessingCenter = New System.Windows.Forms.Button()
        Me.btnOpenWorkspace = New System.Windows.Forms.Button()
        Me.btnProviderHealth = New System.Windows.Forms.Button()
        Me.btnRunLowQuality = New System.Windows.Forms.Button()
        Me.btnRunSelected = New System.Windows.Forms.Button()
        Me.cboProfile = New System.Windows.Forms.ComboBox()
        Me.lblProfile = New System.Windows.Forms.Label()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.txtSearch = New System.Windows.Forms.TextBox()
        Me.lblSearch = New System.Windows.Forms.Label()
        Me.SplitContainer1 = New System.Windows.Forms.SplitContainer()
        Me.dgvDocuments = New System.Windows.Forms.DataGridView()
        Me.TabControl1 = New System.Windows.Forms.TabControl()
        Me.tabQuality = New System.Windows.Forms.TabPage()
        Me.txtQualityDetails = New System.Windows.Forms.TextBox()
        Me.pnlScore = New System.Windows.Forms.Panel()
        Me.lblRecommendation = New System.Windows.Forms.Label()
        Me.lblMissing = New System.Windows.Forms.Label()
        Me.lblRisk = New System.Windows.Forms.Label()
        Me.lblGrade = New System.Windows.Forms.Label()
        Me.lblScore = New System.Windows.Forms.Label()
        Me.tabProviders = New System.Windows.Forms.TabPage()
        Me.dgvProviders = New System.Windows.Forms.DataGridView()
        Me.tabGuide = New System.Windows.Forms.TabPage()
        Me.txtGuide = New System.Windows.Forms.TextBox()
        Me.lblStatus = New System.Windows.Forms.Label()
        Me.pnlHeader.SuspendLayout()
        Me.pnlCommands.SuspendLayout()
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SplitContainer1.Panel1.SuspendLayout()
        Me.SplitContainer1.Panel2.SuspendLayout()
        Me.SplitContainer1.SuspendLayout()
        CType(Me.dgvDocuments, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.TabControl1.SuspendLayout()
        Me.tabQuality.SuspendLayout()
        Me.pnlScore.SuspendLayout()
        Me.tabProviders.SuspendLayout()
        CType(Me.dgvProviders, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.tabGuide.SuspendLayout()
        Me.SuspendLayout()
        '
        'pnlHeader
        '
        Me.pnlHeader.BackColor = System.Drawing.Color.FromArgb(CType(CType(17, Byte), Integer), CType(CType(47, Byte), Integer), CType(CType(61, Byte), Integer))
        Me.pnlHeader.Controls.Add(Me.lblSubtitle)
        Me.pnlHeader.Controls.Add(Me.lblTitle)
        Me.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlHeader.Location = New System.Drawing.Point(0, 0)
        Me.pnlHeader.Name = "pnlHeader"
        Me.pnlHeader.Padding = New System.Windows.Forms.Padding(24, 14, 24, 10)
        Me.pnlHeader.Size = New System.Drawing.Size(1260, 86)
        Me.pnlHeader.TabIndex = 0
        '
        'lblSubtitle
        '
        Me.lblSubtitle.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblSubtitle.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(204, Byte), Integer), CType(CType(221, Byte), Integer), CType(CType(225, Byte), Integer))
        Me.lblSubtitle.Location = New System.Drawing.Point(24, 50)
        Me.lblSubtitle.Name = "lblSubtitle"
        Me.lblSubtitle.Size = New System.Drawing.Size(1212, 26)
        Me.lblSubtitle.TabIndex = 1
        Me.lblSubtitle.Text = "مسار واحد يجمع OCR والكيانات والذكاء متعدد المزودات والتوجيه وبوابة الجودة."
        Me.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 16.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(24, 14)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(1212, 36)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "مركز الأتمتة والجودة"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'pnlCommands
        '
        Me.pnlCommands.BackColor = System.Drawing.Color.White
        Me.pnlCommands.Controls.Add(Me.btnProcessingCenter)
        Me.pnlCommands.Controls.Add(Me.btnOpenWorkspace)
        Me.pnlCommands.Controls.Add(Me.btnProviderHealth)
        Me.pnlCommands.Controls.Add(Me.btnRunLowQuality)
        Me.pnlCommands.Controls.Add(Me.btnRunSelected)
        Me.pnlCommands.Controls.Add(Me.cboProfile)
        Me.pnlCommands.Controls.Add(Me.lblProfile)
        Me.pnlCommands.Controls.Add(Me.btnRefresh)
        Me.pnlCommands.Controls.Add(Me.txtSearch)
        Me.pnlCommands.Controls.Add(Me.lblSearch)
        Me.pnlCommands.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlCommands.Location = New System.Drawing.Point(0, 86)
        Me.pnlCommands.Name = "pnlCommands"
        Me.pnlCommands.Padding = New System.Windows.Forms.Padding(12)
        Me.pnlCommands.Size = New System.Drawing.Size(1260, 78)
        Me.pnlCommands.TabIndex = 1
        '
        'btnProcessingCenter
        '
        Me.btnProcessingCenter.Location = New System.Drawing.Point(14, 39)
        Me.btnProcessingCenter.Name = "btnProcessingCenter"
        Me.btnProcessingCenter.Size = New System.Drawing.Size(138, 32)
        Me.btnProcessingCenter.TabIndex = 9
        Me.btnProcessingCenter.Text = "مراقبة المهام"
        Me.btnProcessingCenter.UseVisualStyleBackColor = True
        '
        'btnOpenWorkspace
        '
        Me.btnOpenWorkspace.Location = New System.Drawing.Point(158, 39)
        Me.btnOpenWorkspace.Name = "btnOpenWorkspace"
        Me.btnOpenWorkspace.Size = New System.Drawing.Size(138, 32)
        Me.btnOpenWorkspace.TabIndex = 8
        Me.btnOpenWorkspace.Text = "فتح المستند"
        Me.btnOpenWorkspace.UseVisualStyleBackColor = True
        '
        'btnProviderHealth
        '
        Me.btnProviderHealth.Location = New System.Drawing.Point(302, 39)
        Me.btnProviderHealth.Name = "btnProviderHealth"
        Me.btnProviderHealth.Size = New System.Drawing.Size(154, 32)
        Me.btnProviderHealth.TabIndex = 7
        Me.btnProviderHealth.Text = "فحص مزودات الذكاء"
        Me.btnProviderHealth.UseVisualStyleBackColor = True
        '
        'btnRunLowQuality
        '
        Me.btnRunLowQuality.BackColor = System.Drawing.Color.FromArgb(CType(CType(174, Byte), Integer), CType(CType(112, Byte), Integer), CType(CType(35, Byte), Integer))
        Me.btnRunLowQuality.FlatAppearance.BorderSize = 0
        Me.btnRunLowQuality.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnRunLowQuality.ForeColor = System.Drawing.Color.White
        Me.btnRunLowQuality.Location = New System.Drawing.Point(462, 39)
        Me.btnRunLowQuality.Name = "btnRunLowQuality"
        Me.btnRunLowQuality.Size = New System.Drawing.Size(170, 32)
        Me.btnRunLowQuality.TabIndex = 6
        Me.btnRunLowQuality.Text = "معالجة الأقل جودة"
        Me.btnRunLowQuality.UseVisualStyleBackColor = False
        '
        'btnRunSelected
        '
        Me.btnRunSelected.BackColor = System.Drawing.Color.FromArgb(CType(CType(29, Byte), Integer), CType(CType(118, Byte), Integer), CType(CType(87, Byte), Integer))
        Me.btnRunSelected.FlatAppearance.BorderSize = 0
        Me.btnRunSelected.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnRunSelected.ForeColor = System.Drawing.Color.White
        Me.btnRunSelected.Location = New System.Drawing.Point(638, 39)
        Me.btnRunSelected.Name = "btnRunSelected"
        Me.btnRunSelected.Size = New System.Drawing.Size(166, 32)
        Me.btnRunSelected.TabIndex = 5
        Me.btnRunSelected.Text = "تشغيل المسار المحدد"
        Me.btnRunSelected.UseVisualStyleBackColor = False
        '
        'cboProfile
        '
        Me.cboProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboProfile.FormattingEnabled = True
        Me.cboProfile.Location = New System.Drawing.Point(810, 43)
        Me.cboProfile.Name = "cboProfile"
        Me.cboProfile.Size = New System.Drawing.Size(214, 23)
        Me.cboProfile.TabIndex = 4
        '
        'lblProfile
        '
        Me.lblProfile.Location = New System.Drawing.Point(810, 12)
        Me.lblProfile.Name = "lblProfile"
        Me.lblProfile.Size = New System.Drawing.Size(214, 24)
        Me.lblProfile.TabIndex = 3
        Me.lblProfile.Text = "ملف المعالجة"
        Me.lblProfile.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'btnRefresh
        '
        Me.btnRefresh.Location = New System.Drawing.Point(1030, 39)
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Size = New System.Drawing.Size(92, 32)
        Me.btnRefresh.TabIndex = 2
        Me.btnRefresh.Text = "تحديث"
        Me.btnRefresh.UseVisualStyleBackColor = True
        '
        'txtSearch
        '
        Me.txtSearch.Location = New System.Drawing.Point(1128, 43)
        Me.txtSearch.Name = "txtSearch"
        Me.txtSearch.Size = New System.Drawing.Size(120, 22)
        Me.txtSearch.TabIndex = 1
        '
        'lblSearch
        '
        Me.lblSearch.Location = New System.Drawing.Point(1128, 12)
        Me.lblSearch.Name = "lblSearch"
        Me.lblSearch.Size = New System.Drawing.Size(120, 24)
        Me.lblSearch.TabIndex = 0
        Me.lblSearch.Text = "بحث"
        Me.lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'SplitContainer1
        '
        Me.SplitContainer1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.SplitContainer1.Location = New System.Drawing.Point(0, 164)
        Me.SplitContainer1.Name = "SplitContainer1"
        '
        'SplitContainer1.Panel1
        '
        Me.SplitContainer1.Panel1.Controls.Add(Me.dgvDocuments)
        '
        'SplitContainer1.Panel2
        '
        Me.SplitContainer1.Panel2.Controls.Add(Me.TabControl1)
        Me.SplitContainer1.Size = New System.Drawing.Size(1260, 572)
        Me.SplitContainer1.SplitterDistance = 650
        Me.SplitContainer1.TabIndex = 2
        '
        'dgvDocuments
        '
        Me.dgvDocuments.AllowUserToAddRows = False
        Me.dgvDocuments.AllowUserToDeleteRows = False
        Me.dgvDocuments.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvDocuments.BackgroundColor = System.Drawing.Color.White
        Me.dgvDocuments.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.dgvDocuments.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.dgvDocuments.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvDocuments.Location = New System.Drawing.Point(0, 0)
        Me.dgvDocuments.MultiSelect = False
        Me.dgvDocuments.Name = "dgvDocuments"
        Me.dgvDocuments.ReadOnly = True
        Me.dgvDocuments.RowHeadersVisible = False
        Me.dgvDocuments.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvDocuments.Size = New System.Drawing.Size(650, 572)
        Me.dgvDocuments.TabIndex = 0
        '
        'TabControl1
        '
        Me.TabControl1.Controls.Add(Me.tabQuality)
        Me.TabControl1.Controls.Add(Me.tabProviders)
        Me.TabControl1.Controls.Add(Me.tabGuide)
        Me.TabControl1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.TabControl1.Location = New System.Drawing.Point(0, 0)
        Me.TabControl1.Name = "TabControl1"
        Me.TabControl1.SelectedIndex = 0
        Me.TabControl1.Size = New System.Drawing.Size(606, 572)
        Me.TabControl1.TabIndex = 0
        '
        'tabQuality
        '
        Me.tabQuality.Controls.Add(Me.txtQualityDetails)
        Me.tabQuality.Controls.Add(Me.pnlScore)
        Me.tabQuality.Location = New System.Drawing.Point(4, 24)
        Me.tabQuality.Name = "tabQuality"
        Me.tabQuality.Padding = New System.Windows.Forms.Padding(8)
        Me.tabQuality.Size = New System.Drawing.Size(598, 544)
        Me.tabQuality.TabIndex = 0
        Me.tabQuality.Text = "بوابة الجودة"
        Me.tabQuality.UseVisualStyleBackColor = True
        '
        'txtQualityDetails
        '
        Me.txtQualityDetails.BackColor = System.Drawing.Color.White
        Me.txtQualityDetails.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtQualityDetails.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.txtQualityDetails.Location = New System.Drawing.Point(8, 196)
        Me.txtQualityDetails.Multiline = True
        Me.txtQualityDetails.Name = "txtQualityDetails"
        Me.txtQualityDetails.ReadOnly = True
        Me.txtQualityDetails.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtQualityDetails.Size = New System.Drawing.Size(582, 340)
        Me.txtQualityDetails.TabIndex = 1
        '
        'pnlScore
        '
        Me.pnlScore.BackColor = System.Drawing.Color.FromArgb(CType(CType(246, Byte), Integer), CType(CType(249, Byte), Integer), CType(CType(250, Byte), Integer))
        Me.pnlScore.Controls.Add(Me.lblRecommendation)
        Me.pnlScore.Controls.Add(Me.lblMissing)
        Me.pnlScore.Controls.Add(Me.lblRisk)
        Me.pnlScore.Controls.Add(Me.lblGrade)
        Me.pnlScore.Controls.Add(Me.lblScore)
        Me.pnlScore.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlScore.Location = New System.Drawing.Point(8, 8)
        Me.pnlScore.Name = "pnlScore"
        Me.pnlScore.Padding = New System.Windows.Forms.Padding(12)
        Me.pnlScore.Size = New System.Drawing.Size(582, 188)
        Me.pnlScore.TabIndex = 0
        '
        'lblRecommendation
        '
        Me.lblRecommendation.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblRecommendation.Location = New System.Drawing.Point(12, 126)
        Me.lblRecommendation.Name = "lblRecommendation"
        Me.lblRecommendation.Size = New System.Drawing.Size(558, 50)
        Me.lblRecommendation.TabIndex = 4
        Me.lblRecommendation.Text = "التوصية:"
        Me.lblRecommendation.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblMissing
        '
        Me.lblMissing.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblMissing.Location = New System.Drawing.Point(12, 86)
        Me.lblMissing.Name = "lblMissing"
        Me.lblMissing.Size = New System.Drawing.Size(558, 40)
        Me.lblMissing.TabIndex = 3
        Me.lblMissing.Text = "النواقص:"
        Me.lblMissing.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblRisk
        '
        Me.lblRisk.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblRisk.Location = New System.Drawing.Point(12, 62)
        Me.lblRisk.Name = "lblRisk"
        Me.lblRisk.Size = New System.Drawing.Size(558, 24)
        Me.lblRisk.TabIndex = 2
        Me.lblRisk.Text = "المخاطر:"
        Me.lblRisk.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblGrade
        '
        Me.lblGrade.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblGrade.Location = New System.Drawing.Point(12, 38)
        Me.lblGrade.Name = "lblGrade"
        Me.lblGrade.Size = New System.Drawing.Size(558, 24)
        Me.lblGrade.TabIndex = 1
        Me.lblGrade.Text = "التصنيف:"
        Me.lblGrade.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblScore
        '
        Me.lblScore.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblScore.Font = New System.Drawing.Font("Tahoma", 14.0!, System.Drawing.FontStyle.Bold)
        Me.lblScore.ForeColor = System.Drawing.Color.FromArgb(CType(CType(29, Byte), Integer), CType(CType(118, Byte), Integer), CType(CType(87, Byte), Integer))
        Me.lblScore.Location = New System.Drawing.Point(12, 12)
        Me.lblScore.Name = "lblScore"
        Me.lblScore.Size = New System.Drawing.Size(558, 26)
        Me.lblScore.TabIndex = 0
        Me.lblScore.Text = "الجودة: --"
        Me.lblScore.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'tabProviders
        '
        Me.tabProviders.Controls.Add(Me.dgvProviders)
        Me.tabProviders.Location = New System.Drawing.Point(4, 24)
        Me.tabProviders.Name = "tabProviders"
        Me.tabProviders.Padding = New System.Windows.Forms.Padding(8)
        Me.tabProviders.Size = New System.Drawing.Size(598, 544)
        Me.tabProviders.TabIndex = 1
        Me.tabProviders.Text = "مزودات الذكاء"
        Me.tabProviders.UseVisualStyleBackColor = True
        '
        'dgvProviders
        '
        Me.dgvProviders.AllowUserToAddRows = False
        Me.dgvProviders.AllowUserToDeleteRows = False
        Me.dgvProviders.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvProviders.BackgroundColor = System.Drawing.Color.White
        Me.dgvProviders.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.dgvProviders.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.dgvProviders.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvProviders.Location = New System.Drawing.Point(8, 8)
        Me.dgvProviders.Name = "dgvProviders"
        Me.dgvProviders.ReadOnly = True
        Me.dgvProviders.RowHeadersVisible = False
        Me.dgvProviders.Size = New System.Drawing.Size(582, 528)
        Me.dgvProviders.TabIndex = 0
        '
        'tabGuide
        '
        Me.tabGuide.Controls.Add(Me.txtGuide)
        Me.tabGuide.Location = New System.Drawing.Point(4, 24)
        Me.tabGuide.Name = "tabGuide"
        Me.tabGuide.Padding = New System.Windows.Forms.Padding(8)
        Me.tabGuide.Size = New System.Drawing.Size(598, 544)
        Me.tabGuide.TabIndex = 2
        Me.tabGuide.Text = "طريقة العمل"
        Me.tabGuide.UseVisualStyleBackColor = True
        '
        'txtGuide
        '
        Me.txtGuide.BackColor = System.Drawing.Color.White
        Me.txtGuide.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtGuide.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.txtGuide.Location = New System.Drawing.Point(8, 8)
        Me.txtGuide.Multiline = True
        Me.txtGuide.Name = "txtGuide"
        Me.txtGuide.ReadOnly = True
        Me.txtGuide.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtGuide.Size = New System.Drawing.Size(582, 528)
        Me.txtGuide.TabIndex = 0
        '
        'lblStatus
        '
        Me.lblStatus.BackColor = System.Drawing.Color.White
        Me.lblStatus.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblStatus.Location = New System.Drawing.Point(0, 736)
        Me.lblStatus.Name = "lblStatus"
        Me.lblStatus.Padding = New System.Windows.Forms.Padding(12, 0, 12, 0)
        Me.lblStatus.Size = New System.Drawing.Size(1260, 34)
        Me.lblStatus.TabIndex = 3
        Me.lblStatus.Text = "جاهز"
        Me.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'AutomationQualityCenterForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1260, 770)
        Me.Controls.Add(Me.SplitContainer1)
        Me.Controls.Add(Me.lblStatus)
        Me.Controls.Add(Me.pnlCommands)
        Me.Controls.Add(Me.pnlHeader)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.Name = "AutomationQualityCenterForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "مركز الأتمتة والجودة"
        Me.WindowState = System.Windows.Forms.FormWindowState.Maximized
        Me.pnlHeader.ResumeLayout(False)
        Me.pnlCommands.ResumeLayout(False)
        Me.pnlCommands.PerformLayout()
        Me.SplitContainer1.Panel1.ResumeLayout(False)
        Me.SplitContainer1.Panel2.ResumeLayout(False)
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.SplitContainer1.ResumeLayout(False)
        CType(Me.dgvDocuments, System.ComponentModel.ISupportInitialize).EndInit()
        Me.TabControl1.ResumeLayout(False)
        Me.tabQuality.ResumeLayout(False)
        Me.tabQuality.PerformLayout()
        Me.pnlScore.ResumeLayout(False)
        Me.tabProviders.ResumeLayout(False)
        CType(Me.dgvProviders, System.ComponentModel.ISupportInitialize).EndInit()
        Me.tabGuide.ResumeLayout(False)
        Me.tabGuide.PerformLayout()
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents pnlHeader As Panel
    Friend WithEvents lblTitle As Label
    Friend WithEvents lblSubtitle As Label
    Friend WithEvents pnlCommands As Panel
    Friend WithEvents lblSearch As Label
    Friend WithEvents txtSearch As TextBox
    Friend WithEvents btnRefresh As Button
    Friend WithEvents lblProfile As Label
    Friend WithEvents cboProfile As ComboBox
    Friend WithEvents btnRunSelected As Button
    Friend WithEvents btnRunLowQuality As Button
    Friend WithEvents btnProviderHealth As Button
    Friend WithEvents btnOpenWorkspace As Button
    Friend WithEvents btnProcessingCenter As Button
    Friend WithEvents SplitContainer1 As SplitContainer
    Friend WithEvents dgvDocuments As DataGridView
    Friend WithEvents TabControl1 As TabControl
    Friend WithEvents tabQuality As TabPage
    Friend WithEvents pnlScore As Panel
    Friend WithEvents lblScore As Label
    Friend WithEvents lblGrade As Label
    Friend WithEvents lblRisk As Label
    Friend WithEvents lblMissing As Label
    Friend WithEvents lblRecommendation As Label
    Friend WithEvents txtQualityDetails As TextBox
    Friend WithEvents tabProviders As TabPage
    Friend WithEvents dgvProviders As DataGridView
    Friend WithEvents tabGuide As TabPage
    Friend WithEvents txtGuide As TextBox
    Friend WithEvents lblStatus As Label
End Class
