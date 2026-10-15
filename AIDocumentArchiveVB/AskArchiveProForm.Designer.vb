<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class AskArchiveProForm
    Inherits System.Windows.Forms.Form

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

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.headerPanel = New System.Windows.Forms.Panel()
        Me.lblSubtitle = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.txtQuestion = New System.Windows.Forms.TextBox()
        Me.btnAsk = New System.Windows.Forms.Button()
        Me.btnRebuild = New System.Windows.Forms.Button()
        Me.btnExportCase = New System.Windows.Forms.Button()
        Me.btnExportRelations = New System.Windows.Forms.Button()
        Me.chkRelations = New System.Windows.Forms.CheckBox()
        Me.numResults = New System.Windows.Forms.NumericUpDown()
        Me.lblResults = New System.Windows.Forms.Label()
        Me.splitMain = New System.Windows.Forms.SplitContainer()
        Me.txtAnswer = New System.Windows.Forms.TextBox()
        Me.tabResults = New System.Windows.Forms.TabControl()
        Me.tabEvidence = New System.Windows.Forms.TabPage()
        Me.dgvEvidence = New System.Windows.Forms.DataGridView()
        Me.tabRelations = New System.Windows.Forms.TabPage()
        Me.dgvRelations = New System.Windows.Forms.DataGridView()
        Me.lblStatus = New System.Windows.Forms.Label()
        Me.headerPanel.SuspendLayout()
        CType(Me.numResults, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.splitMain, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.splitMain.Panel1.SuspendLayout()
        Me.splitMain.Panel2.SuspendLayout()
        Me.splitMain.SuspendLayout()
        Me.tabResults.SuspendLayout()
        Me.tabEvidence.SuspendLayout()
        CType(Me.dgvEvidence, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.tabRelations.SuspendLayout()
        CType(Me.dgvRelations, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'headerPanel
        '
        Me.headerPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.headerPanel.Controls.Add(Me.lblSubtitle)
        Me.headerPanel.Controls.Add(Me.lblTitle)
        Me.headerPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.headerPanel.Location = New System.Drawing.Point(0, 0)
        Me.headerPanel.Name = "headerPanel"
        Me.headerPanel.Size = New System.Drawing.Size(1280, 92)
        Me.headerPanel.TabIndex = 0
        '
        'lblSubtitle
        '
        Me.lblSubtitle.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblSubtitle.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.lblSubtitle.ForeColor = System.Drawing.Color.WhiteSmoke
        Me.lblSubtitle.Location = New System.Drawing.Point(24, 54)
        Me.lblSubtitle.Name = "lblSubtitle"
        Me.lblSubtitle.Size = New System.Drawing.Size(1232, 24)
        Me.lblSubtitle.TabIndex = 1
        Me.lblSubtitle.Text = "اسأل كامل الأرشيف، اكتشف العلاقات، وأنشئ ملف معاملة ذكي من النتائج."
        Me.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 18.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.lblTitle.Location = New System.Drawing.Point(24, 14)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(1232, 34)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "اسأل الأرشيف"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtQuestion
        '
        Me.txtQuestion.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtQuestion.Font = New System.Drawing.Font("Tahoma", 11.0!)
        Me.txtQuestion.Location = New System.Drawing.Point(24, 108)
        Me.txtQuestion.Multiline = True
        Me.txtQuestion.Name = "txtQuestion"
        Me.txtQuestion.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.txtQuestion.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtQuestion.Size = New System.Drawing.Size(1232, 76)
        Me.txtQuestion.TabIndex = 1
        Me.txtQuestion.Text = "اعرض لي المستندات المرتبطة بجهة معينة أو التي تحتوي على مبالغ أو أرقام وارد وصادر"
        '
        'btnAsk
        '
        Me.btnAsk.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnAsk.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnAsk.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAsk.Font = New System.Drawing.Font("Tahoma", 10.0!, System.Drawing.FontStyle.Bold)
        Me.btnAsk.ForeColor = System.Drawing.Color.White
        Me.btnAsk.Location = New System.Drawing.Point(1060, 195)
        Me.btnAsk.Name = "btnAsk"
        Me.btnAsk.Size = New System.Drawing.Size(196, 40)
        Me.btnAsk.TabIndex = 2
        Me.btnAsk.Text = "اسأل الأرشيف"
        Me.btnAsk.UseVisualStyleBackColor = False
        '
        'btnRebuild
        '
        Me.btnRebuild.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRebuild.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnRebuild.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnRebuild.Location = New System.Drawing.Point(884, 195)
        Me.btnRebuild.Name = "btnRebuild"
        Me.btnRebuild.Size = New System.Drawing.Size(170, 40)
        Me.btnRebuild.TabIndex = 3
        Me.btnRebuild.Text = "إعادة بناء الفهرس"
        Me.btnRebuild.UseVisualStyleBackColor = True
        '
        'btnExportCase
        '
        Me.btnExportCase.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnExportCase.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnExportCase.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnExportCase.Location = New System.Drawing.Point(696, 195)
        Me.btnExportCase.Name = "btnExportCase"
        Me.btnExportCase.Size = New System.Drawing.Size(182, 40)
        Me.btnExportCase.TabIndex = 4
        Me.btnExportCase.Text = "إنشاء ملف معاملة"
        Me.btnExportCase.UseVisualStyleBackColor = True
        '
        'btnExportRelations
        '
        Me.btnExportRelations.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnExportRelations.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnExportRelations.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnExportRelations.Location = New System.Drawing.Point(502, 195)
        Me.btnExportRelations.Name = "btnExportRelations"
        Me.btnExportRelations.Size = New System.Drawing.Size(188, 40)
        Me.btnExportRelations.TabIndex = 5
        Me.btnExportRelations.Text = "تصدير شبكة العلاقات"
        Me.btnExportRelations.UseVisualStyleBackColor = True
        '
        'chkRelations
        '
        Me.chkRelations.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.chkRelations.Checked = True
        Me.chkRelations.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkRelations.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.chkRelations.Location = New System.Drawing.Point(318, 201)
        Me.chkRelations.Name = "chkRelations"
        Me.chkRelations.Size = New System.Drawing.Size(178, 28)
        Me.chkRelations.TabIndex = 6
        Me.chkRelations.Text = "اكتشاف العلاقات"
        Me.chkRelations.UseVisualStyleBackColor = True
        '
        'numResults
        '
        Me.numResults.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.numResults.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.numResults.Location = New System.Drawing.Point(235, 202)
        Me.numResults.Maximum = New Decimal(New Integer() {30, 0, 0, 0})
        Me.numResults.Minimum = New Decimal(New Integer() {3, 0, 0, 0})
        Me.numResults.Name = "numResults"
        Me.numResults.Size = New System.Drawing.Size(72, 28)
        Me.numResults.TabIndex = 7
        Me.numResults.Value = New Decimal(New Integer() {10, 0, 0, 0})
        '
        'lblResults
        '
        Me.lblResults.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblResults.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.lblResults.Location = New System.Drawing.Point(138, 204)
        Me.lblResults.Name = "lblResults"
        Me.lblResults.Size = New System.Drawing.Size(92, 24)
        Me.lblResults.TabIndex = 8
        Me.lblResults.Text = "عدد النتائج"
        Me.lblResults.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'splitMain
        '
        Me.splitMain.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.splitMain.Location = New System.Drawing.Point(24, 248)
        Me.splitMain.Name = "splitMain"
        Me.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal
        '
        'splitMain.Panel1
        '
        Me.splitMain.Panel1.Controls.Add(Me.txtAnswer)
        Me.splitMain.Panel1.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        '
        'splitMain.Panel2
        '
        Me.splitMain.Panel2.Controls.Add(Me.tabResults)
        Me.splitMain.Panel2.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.splitMain.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.splitMain.Size = New System.Drawing.Size(1232, 570)
        Me.splitMain.SplitterDistance = 272
        Me.splitMain.TabIndex = 9
        '
        'txtAnswer
        '
        Me.txtAnswer.BackColor = System.Drawing.Color.White
        Me.txtAnswer.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtAnswer.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.txtAnswer.Location = New System.Drawing.Point(0, 0)
        Me.txtAnswer.Multiline = True
        Me.txtAnswer.Name = "txtAnswer"
        Me.txtAnswer.ReadOnly = True
        Me.txtAnswer.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.txtAnswer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtAnswer.Size = New System.Drawing.Size(1232, 272)
        Me.txtAnswer.TabIndex = 0
        '
        'tabResults
        '
        Me.tabResults.Controls.Add(Me.tabEvidence)
        Me.tabResults.Controls.Add(Me.tabRelations)
        Me.tabResults.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tabResults.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.tabResults.Location = New System.Drawing.Point(0, 0)
        Me.tabResults.Name = "tabResults"
        Me.tabResults.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.tabResults.RightToLeftLayout = True
        Me.tabResults.SelectedIndex = 0
        Me.tabResults.Size = New System.Drawing.Size(1232, 294)
        Me.tabResults.TabIndex = 0
        '
        'tabEvidence
        '
        Me.tabEvidence.Controls.Add(Me.dgvEvidence)
        Me.tabEvidence.Location = New System.Drawing.Point(4, 27)
        Me.tabEvidence.Name = "tabEvidence"
        Me.tabEvidence.Padding = New System.Windows.Forms.Padding(3)
        Me.tabEvidence.Size = New System.Drawing.Size(1224, 263)
        Me.tabEvidence.TabIndex = 0
        Me.tabEvidence.Text = "الأدلة والمستندات"
        Me.tabEvidence.UseVisualStyleBackColor = True
        '
        'dgvEvidence
        '
        Me.dgvEvidence.AllowUserToAddRows = False
        Me.dgvEvidence.AllowUserToDeleteRows = False
        Me.dgvEvidence.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvEvidence.BackgroundColor = System.Drawing.Color.White
        Me.dgvEvidence.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.dgvEvidence.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvEvidence.Location = New System.Drawing.Point(3, 3)
        Me.dgvEvidence.MultiSelect = False
        Me.dgvEvidence.Name = "dgvEvidence"
        Me.dgvEvidence.ReadOnly = True
        Me.dgvEvidence.RowHeadersVisible = False
        Me.dgvEvidence.RowHeadersWidth = 51
        Me.dgvEvidence.RowTemplate.Height = 29
        Me.dgvEvidence.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvEvidence.Size = New System.Drawing.Size(1218, 257)
        Me.dgvEvidence.TabIndex = 0
        '
        'tabRelations
        '
        Me.tabRelations.Controls.Add(Me.dgvRelations)
        Me.tabRelations.Location = New System.Drawing.Point(4, 27)
        Me.tabRelations.Name = "tabRelations"
        Me.tabRelations.Padding = New System.Windows.Forms.Padding(3)
        Me.tabRelations.Size = New System.Drawing.Size(1224, 263)
        Me.tabRelations.TabIndex = 1
        Me.tabRelations.Text = "شبكة العلاقات"
        Me.tabRelations.UseVisualStyleBackColor = True
        '
        'dgvRelations
        '
        Me.dgvRelations.AllowUserToAddRows = False
        Me.dgvRelations.AllowUserToDeleteRows = False
        Me.dgvRelations.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvRelations.BackgroundColor = System.Drawing.Color.White
        Me.dgvRelations.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.dgvRelations.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvRelations.Location = New System.Drawing.Point(3, 3)
        Me.dgvRelations.MultiSelect = False
        Me.dgvRelations.Name = "dgvRelations"
        Me.dgvRelations.ReadOnly = True
        Me.dgvRelations.RowHeadersVisible = False
        Me.dgvRelations.RowHeadersWidth = 51
        Me.dgvRelations.RowTemplate.Height = 29
        Me.dgvRelations.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvRelations.Size = New System.Drawing.Size(1218, 257)
        Me.dgvRelations.TabIndex = 0
        '
        'lblStatus
        '
        Me.lblStatus.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblStatus.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.lblStatus.ForeColor = System.Drawing.Color.FromArgb(CType(CType(73, Byte), Integer), CType(CType(88, Byte), Integer), CType(CType(83, Byte), Integer))
        Me.lblStatus.Location = New System.Drawing.Point(24, 826)
        Me.lblStatus.Name = "lblStatus"
        Me.lblStatus.Size = New System.Drawing.Size(1232, 26)
        Me.lblStatus.TabIndex = 10
        Me.lblStatus.Text = "جاهز"
        Me.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'AskArchiveProForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(246, Byte), Integer), CType(CType(248, Byte), Integer), CType(CType(247, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(1280, 860)
        Me.Controls.Add(Me.lblStatus)
        Me.Controls.Add(Me.splitMain)
        Me.Controls.Add(Me.lblResults)
        Me.Controls.Add(Me.numResults)
        Me.Controls.Add(Me.chkRelations)
        Me.Controls.Add(Me.btnExportRelations)
        Me.Controls.Add(Me.btnExportCase)
        Me.Controls.Add(Me.btnRebuild)
        Me.Controls.Add(Me.btnAsk)
        Me.Controls.Add(Me.txtQuestion)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 8.0!)
        Me.MinimumSize = New System.Drawing.Size(1120, 760)
        Me.Name = "AskArchiveProForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "اسأل الأرشيف"
        Me.headerPanel.ResumeLayout(False)
        CType(Me.numResults, System.ComponentModel.ISupportInitialize).EndInit()
        Me.splitMain.Panel1.ResumeLayout(False)
        Me.splitMain.Panel1.PerformLayout()
        Me.splitMain.Panel2.ResumeLayout(False)
        CType(Me.splitMain, System.ComponentModel.ISupportInitialize).EndInit()
        Me.splitMain.ResumeLayout(False)
        Me.tabResults.ResumeLayout(False)
        Me.tabEvidence.ResumeLayout(False)
        CType(Me.dgvEvidence, System.ComponentModel.ISupportInitialize).EndInit()
        Me.tabRelations.ResumeLayout(False)
        CType(Me.dgvRelations, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblSubtitle As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents txtQuestion As System.Windows.Forms.TextBox
    Friend WithEvents btnAsk As System.Windows.Forms.Button
    Friend WithEvents btnRebuild As System.Windows.Forms.Button
    Friend WithEvents btnExportCase As System.Windows.Forms.Button
    Friend WithEvents btnExportRelations As System.Windows.Forms.Button
    Friend WithEvents chkRelations As System.Windows.Forms.CheckBox
    Friend WithEvents numResults As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblResults As System.Windows.Forms.Label
    Friend WithEvents splitMain As System.Windows.Forms.SplitContainer
    Friend WithEvents txtAnswer As System.Windows.Forms.TextBox
    Friend WithEvents tabResults As System.Windows.Forms.TabControl
    Friend WithEvents tabEvidence As System.Windows.Forms.TabPage
    Friend WithEvents dgvEvidence As System.Windows.Forms.DataGridView
    Friend WithEvents tabRelations As System.Windows.Forms.TabPage
    Friend WithEvents dgvRelations As System.Windows.Forms.DataGridView
    Friend WithEvents lblStatus As System.Windows.Forms.Label
End Class
