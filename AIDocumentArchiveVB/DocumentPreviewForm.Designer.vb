<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class DocumentPreviewForm
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
        Me.btnOpenFolder = New System.Windows.Forms.Button()
        Me.btnOpenExternal = New System.Windows.Forms.Button()
        Me.lblSub = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.tabs = New System.Windows.Forms.TabControl()
        Me.tabPreview = New System.Windows.Forms.TabPage()
        Me.lblPreviewHint = New System.Windows.Forms.Label()
        Me.previewBrowser = New System.Windows.Forms.WebBrowser()
        Me.picPreview = New System.Windows.Forms.PictureBox()
        Me.tabText = New System.Windows.Forms.TabPage()
        Me.txtExtracted = New System.Windows.Forms.TextBox()
        Me.tabInfo = New System.Windows.Forms.TabPage()
        Me.txtMeta = New System.Windows.Forms.TextBox()
        Me.headerPanel.SuspendLayout()
        Me.tabs.SuspendLayout()
        Me.tabPreview.SuspendLayout()
        CType(Me.picPreview, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.tabText.SuspendLayout()
        Me.tabInfo.SuspendLayout()
        Me.SuspendLayout()
        '
        'headerPanel
        '
        Me.headerPanel.BackColor = System.Drawing.Color.White
        Me.headerPanel.Controls.Add(Me.btnOpenFolder)
        Me.headerPanel.Controls.Add(Me.btnOpenExternal)
        Me.headerPanel.Controls.Add(Me.lblSub)
        Me.headerPanel.Controls.Add(Me.lblTitle)
        Me.headerPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.headerPanel.Location = New System.Drawing.Point(0, 0)
        Me.headerPanel.Name = "headerPanel"
        Me.headerPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.headerPanel.Size = New System.Drawing.Size(1040, 92)
        Me.headerPanel.TabIndex = 0
        '
        'btnOpenFolder
        '
        Me.btnOpenFolder.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnOpenFolder.BackColor = System.Drawing.Color.FromArgb(CType(CType(240, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(241, Byte), Integer))
        Me.btnOpenFolder.FlatAppearance.BorderSize = 0
        Me.btnOpenFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOpenFolder.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnOpenFolder.Location = New System.Drawing.Point(18, 47)
        Me.btnOpenFolder.Name = "btnOpenFolder"
        Me.btnOpenFolder.Size = New System.Drawing.Size(148, 34)
        Me.btnOpenFolder.TabIndex = 3
        Me.btnOpenFolder.Text = "فتح المجلد"
        Me.btnOpenFolder.UseVisualStyleBackColor = False
        '
        'btnOpenExternal
        '
        Me.btnOpenExternal.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnOpenExternal.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnOpenExternal.FlatAppearance.BorderSize = 0
        Me.btnOpenExternal.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOpenExternal.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnOpenExternal.ForeColor = System.Drawing.Color.White
        Me.btnOpenExternal.Location = New System.Drawing.Point(18, 11)
        Me.btnOpenExternal.Name = "btnOpenExternal"
        Me.btnOpenExternal.Size = New System.Drawing.Size(148, 34)
        Me.btnOpenExternal.TabIndex = 2
        Me.btnOpenExternal.Text = "فتح الملف"
        Me.btnOpenExternal.UseVisualStyleBackColor = False
        '
        'lblSub
        '
        Me.lblSub.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblSub.ForeColor = System.Drawing.Color.FromArgb(CType(CType(92, Byte), Integer), CType(CType(102, Byte), Integer), CType(CType(98, Byte), Integer))
        Me.lblSub.Location = New System.Drawing.Point(181, 48)
        Me.lblSub.Name = "lblSub"
        Me.lblSub.Size = New System.Drawing.Size(841, 28)
        Me.lblSub.TabIndex = 1
        Me.lblSub.Text = "تفاصيل المستند"
        Me.lblSub.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 14.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblTitle.Location = New System.Drawing.Point(181, 15)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(841, 34)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "معاينة المستند"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'tabs
        '
        Me.tabs.Controls.Add(Me.tabPreview)
        Me.tabs.Controls.Add(Me.tabText)
        Me.tabs.Controls.Add(Me.tabInfo)
        Me.tabs.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tabs.Location = New System.Drawing.Point(0, 92)
        Me.tabs.Name = "tabs"
        Me.tabs.RightToLeftLayout = True
        Me.tabs.SelectedIndex = 0
        Me.tabs.Size = New System.Drawing.Size(1040, 588)
        Me.tabs.TabIndex = 1
        '
        'tabPreview
        '
        Me.tabPreview.BackColor = System.Drawing.Color.FromArgb(CType(CType(245, Byte), Integer), CType(CType(247, Byte), Integer), CType(CType(246, Byte), Integer))
        Me.tabPreview.Controls.Add(Me.lblPreviewHint)
        Me.tabPreview.Controls.Add(Me.previewBrowser)
        Me.tabPreview.Controls.Add(Me.picPreview)
        Me.tabPreview.Location = New System.Drawing.Point(4, 27)
        Me.tabPreview.Name = "tabPreview"
        Me.tabPreview.Padding = New System.Windows.Forms.Padding(12)
        Me.tabPreview.Size = New System.Drawing.Size(1032, 557)
        Me.tabPreview.TabIndex = 0
        Me.tabPreview.Text = "المعاينة"
        '
        'lblPreviewHint
        '
        Me.lblPreviewHint.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblPreviewHint.Font = New System.Drawing.Font("Tahoma", 12.0!, System.Drawing.FontStyle.Bold)
        Me.lblPreviewHint.ForeColor = System.Drawing.Color.FromArgb(CType(CType(80, Byte), Integer), CType(CType(88, Byte), Integer), CType(CType(84, Byte), Integer))
        Me.lblPreviewHint.Location = New System.Drawing.Point(12, 12)
        Me.lblPreviewHint.Name = "lblPreviewHint"
        Me.lblPreviewHint.Size = New System.Drawing.Size(1008, 533)
        Me.lblPreviewHint.TabIndex = 2
        Me.lblPreviewHint.Text = "اختر مستنداً قابلاً للمعاينة."
        Me.lblPreviewHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'previewBrowser
        '
        Me.previewBrowser.Dock = System.Windows.Forms.DockStyle.Fill
        Me.previewBrowser.Location = New System.Drawing.Point(12, 12)
        Me.previewBrowser.MinimumSize = New System.Drawing.Size(20, 20)
        Me.previewBrowser.Name = "previewBrowser"
        Me.previewBrowser.Size = New System.Drawing.Size(1008, 533)
        Me.previewBrowser.TabIndex = 1
        Me.previewBrowser.Visible = False
        '
        'picPreview
        '
        Me.picPreview.BackColor = System.Drawing.Color.White
        Me.picPreview.Dock = System.Windows.Forms.DockStyle.Fill
        Me.picPreview.Location = New System.Drawing.Point(12, 12)
        Me.picPreview.Name = "picPreview"
        Me.picPreview.Size = New System.Drawing.Size(1008, 533)
        Me.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
        Me.picPreview.TabIndex = 0
        Me.picPreview.TabStop = False
        Me.picPreview.Visible = False
        '
        'tabText
        '
        Me.tabText.Controls.Add(Me.txtExtracted)
        Me.tabText.Location = New System.Drawing.Point(4, 27)
        Me.tabText.Name = "tabText"
        Me.tabText.Padding = New System.Windows.Forms.Padding(12)
        Me.tabText.Size = New System.Drawing.Size(1032, 557)
        Me.tabText.TabIndex = 1
        Me.tabText.Text = "النص المستخرج"
        Me.tabText.UseVisualStyleBackColor = True
        '
        'txtExtracted
        '
        Me.txtExtracted.BackColor = System.Drawing.Color.White
        Me.txtExtracted.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtExtracted.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.txtExtracted.Location = New System.Drawing.Point(12, 12)
        Me.txtExtracted.Multiline = True
        Me.txtExtracted.Name = "txtExtracted"
        Me.txtExtracted.ReadOnly = True
        Me.txtExtracted.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtExtracted.Size = New System.Drawing.Size(1008, 533)
        Me.txtExtracted.TabIndex = 0
        '
        'tabInfo
        '
        Me.tabInfo.Controls.Add(Me.txtMeta)
        Me.tabInfo.Location = New System.Drawing.Point(4, 27)
        Me.tabInfo.Name = "tabInfo"
        Me.tabInfo.Padding = New System.Windows.Forms.Padding(12)
        Me.tabInfo.Size = New System.Drawing.Size(1032, 557)
        Me.tabInfo.TabIndex = 2
        Me.tabInfo.Text = "بطاقة المستند"
        Me.tabInfo.UseVisualStyleBackColor = True
        '
        'txtMeta
        '
        Me.txtMeta.BackColor = System.Drawing.Color.White
        Me.txtMeta.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtMeta.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.txtMeta.Location = New System.Drawing.Point(12, 12)
        Me.txtMeta.Multiline = True
        Me.txtMeta.Name = "txtMeta"
        Me.txtMeta.ReadOnly = True
        Me.txtMeta.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtMeta.Size = New System.Drawing.Size(1008, 533)
        Me.txtMeta.TabIndex = 0
        '
        'DocumentPreviewForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1040, 680)
        Me.Controls.Add(Me.tabs)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(900, 600)
        Me.Name = "DocumentPreviewForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "معاينة المستند"
        Me.headerPanel.ResumeLayout(False)
        Me.tabs.ResumeLayout(False)
        Me.tabPreview.ResumeLayout(False)
        CType(Me.picPreview, System.ComponentModel.ISupportInitialize).EndInit()
        Me.tabText.ResumeLayout(False)
        Me.tabText.PerformLayout()
        Me.tabInfo.ResumeLayout(False)
        Me.tabInfo.PerformLayout()
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents btnOpenFolder As System.Windows.Forms.Button
    Friend WithEvents btnOpenExternal As System.Windows.Forms.Button
    Friend WithEvents lblSub As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents tabs As System.Windows.Forms.TabControl
    Friend WithEvents tabPreview As System.Windows.Forms.TabPage
    Friend WithEvents lblPreviewHint As System.Windows.Forms.Label
    Friend WithEvents previewBrowser As System.Windows.Forms.WebBrowser
    Friend WithEvents picPreview As System.Windows.Forms.PictureBox
    Friend WithEvents tabText As System.Windows.Forms.TabPage
    Friend WithEvents txtExtracted As System.Windows.Forms.TextBox
    Friend WithEvents tabInfo As System.Windows.Forms.TabPage
    Friend WithEvents txtMeta As System.Windows.Forms.TextBox
End Class
