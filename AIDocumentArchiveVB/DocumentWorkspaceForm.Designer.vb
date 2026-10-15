<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class DocumentWorkspaceForm
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
        Dim headerStyle As New System.Windows.Forms.DataGridViewCellStyle()
        Dim rowsStyle As New System.Windows.Forms.DataGridViewCellStyle()
        Dim alternatingStyle As New System.Windows.Forms.DataGridViewCellStyle()
        Me.pnlToolbar = New System.Windows.Forms.Panel()
        Me.lblStatus = New System.Windows.Forms.Label()
        Me.flpActions = New System.Windows.Forms.FlowLayoutPanel()
        Me.btnAutomate = New System.Windows.Forms.Button()
        Me.btnOcr = New System.Windows.Forms.Button()
        Me.btnQuick = New System.Windows.Forms.Button()
        Me.btnDeep = New System.Windows.Forms.Button()
        Me.cboTone = New System.Windows.Forms.ComboBox()
        Me.btnReply = New System.Windows.Forms.Button()
        Me.btnExportReply = New System.Windows.Forms.Button()
        Me.SplitContainer1 = New System.Windows.Forms.SplitContainer()
        Me.dgvDocs = New System.Windows.Forms.DataGridView()
        Me.colArchiveNumber = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.colTitle = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.colStatus = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.colUpdatedAt = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.pnlListFooter = New System.Windows.Forms.Panel()
        Me.lblListCount = New System.Windows.Forms.Label()
        Me.pnlSearch = New System.Windows.Forms.Panel()
        Me.tlpSearch = New System.Windows.Forms.TableLayoutPanel()
        Me.btnClearSearch = New System.Windows.Forms.Button()
        Me.btnSearch = New System.Windows.Forms.Button()
        Me.txtSearch = New System.Windows.Forms.TextBox()
        Me.lblDocumentsTitle = New System.Windows.Forms.Label()
        Me.TabControl1 = New System.Windows.Forms.TabControl()
        Me.tabInfo = New System.Windows.Forms.TabPage()
        Me.pnlInfoSurface = New System.Windows.Forms.Panel()
        Me.TableLayoutPanel1 = New System.Windows.Forms.TableLayoutPanel()
        Me.lblTitleCaption = New System.Windows.Forms.Label()
        Me.txtTitle = New System.Windows.Forms.TextBox()
        Me.lblArchiveCaption = New System.Windows.Forms.Label()
        Me.txtArchiveNo = New System.Windows.Forms.TextBox()
        Me.lblCategoryCaption = New System.Windows.Forms.Label()
        Me.txtCategory = New System.Windows.Forms.TextBox()
        Me.lblSubCategoryCaption = New System.Windows.Forms.Label()
        Me.txtSubCategory = New System.Windows.Forms.TextBox()
        Me.lblSourceCaption = New System.Windows.Forms.Label()
        Me.txtSource = New System.Windows.Forms.TextBox()
        Me.lblDestinationCaption = New System.Windows.Forms.Label()
        Me.txtDestination = New System.Windows.Forms.TextBox()
        Me.lblStatusCaption = New System.Windows.Forms.Label()
        Me.txtStatus = New System.Windows.Forms.TextBox()
        Me.lblPriorityCaption = New System.Windows.Forms.Label()
        Me.txtPriority = New System.Windows.Forms.TextBox()
        Me.lblInfoTitle = New System.Windows.Forms.Label()
        Me.tabText = New System.Windows.Forms.TabPage()
        Me.rtbText = New System.Windows.Forms.RichTextBox()
        Me.tabEntities = New System.Windows.Forms.TabPage()
        Me.txtEntities = New System.Windows.Forms.TextBox()
        Me.tabAi = New System.Windows.Forms.TabPage()
        Me.txtAi = New System.Windows.Forms.TextBox()
        Me.tabReply = New System.Windows.Forms.TabPage()
        Me.txtReply = New System.Windows.Forms.TextBox()
        Me.SaveFileDialog1 = New System.Windows.Forms.SaveFileDialog()
        Me.pnlToolbar.SuspendLayout()
        Me.flpActions.SuspendLayout()
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SplitContainer1.Panel1.SuspendLayout()
        Me.SplitContainer1.Panel2.SuspendLayout()
        Me.SplitContainer1.SuspendLayout()
        CType(Me.dgvDocs, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.pnlListFooter.SuspendLayout()
        Me.pnlSearch.SuspendLayout()
        Me.tlpSearch.SuspendLayout()
        Me.TabControl1.SuspendLayout()
        Me.tabInfo.SuspendLayout()
        Me.pnlInfoSurface.SuspendLayout()
        Me.TableLayoutPanel1.SuspendLayout()
        Me.tabText.SuspendLayout()
        Me.tabEntities.SuspendLayout()
        Me.tabAi.SuspendLayout()
        Me.tabReply.SuspendLayout()
        Me.SuspendLayout()
        '
        'pnlToolbar
        '
        Me.pnlToolbar.BackColor = System.Drawing.Color.White
        Me.pnlToolbar.Controls.Add(Me.lblStatus)
        Me.pnlToolbar.Controls.Add(Me.flpActions)
        Me.pnlToolbar.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlToolbar.Location = New System.Drawing.Point(0, 0)
        Me.pnlToolbar.Name = "pnlToolbar"
        Me.pnlToolbar.Padding = New System.Windows.Forms.Padding(12, 10, 12, 10)
        Me.pnlToolbar.Size = New System.Drawing.Size(1360, 68)
        Me.pnlToolbar.TabIndex = 0
        '
        'lblStatus
        '
        Me.lblStatus.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblStatus.Font = New System.Drawing.Font("Tahoma", 9.5!, System.Drawing.FontStyle.Bold)
        Me.lblStatus.ForeColor = System.Drawing.Color.FromArgb(63, 72, 82)
        Me.lblStatus.Location = New System.Drawing.Point(12, 10)
        Me.lblStatus.Name = "lblStatus"
        Me.lblStatus.Padding = New System.Windows.Forms.Padding(8, 0, 8, 0)
        Me.lblStatus.Size = New System.Drawing.Size(473, 48)
        Me.lblStatus.TabIndex = 1
        Me.lblStatus.Text = "اختر مستنداً لعرض تفاصيله وإجراء المعالجة المطلوبة."
        Me.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'flpActions
        '
        Me.flpActions.AutoSize = True
        Me.flpActions.Controls.Add(Me.btnAutomate)
        Me.flpActions.Controls.Add(Me.btnOcr)
        Me.flpActions.Controls.Add(Me.btnQuick)
        Me.flpActions.Controls.Add(Me.btnDeep)
        Me.flpActions.Controls.Add(Me.cboTone)
        Me.flpActions.Controls.Add(Me.btnReply)
        Me.flpActions.Controls.Add(Me.btnExportReply)
        Me.flpActions.Dock = System.Windows.Forms.DockStyle.Right
        Me.flpActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft
        Me.flpActions.Location = New System.Drawing.Point(485, 10)
        Me.flpActions.Name = "flpActions"
        Me.flpActions.Size = New System.Drawing.Size(863, 48)
        Me.flpActions.TabIndex = 0
        Me.flpActions.WrapContents = False
        '
        'btnAutomate
        '
        Me.btnAutomate.BackColor = System.Drawing.Color.FromArgb(26, 122, 89)
        Me.btnAutomate.FlatAppearance.BorderSize = 0
        Me.btnAutomate.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAutomate.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnAutomate.ForeColor = System.Drawing.Color.White
        Me.btnAutomate.Location = New System.Drawing.Point(735, 3)
        Me.btnAutomate.Margin = New System.Windows.Forms.Padding(4, 3, 0, 3)
        Me.btnAutomate.Name = "btnAutomate"
        Me.btnAutomate.Size = New System.Drawing.Size(128, 42)
        Me.btnAutomate.TabIndex = 0
        Me.btnAutomate.Text = "معالجة ذكية"
        Me.btnAutomate.UseVisualStyleBackColor = False
        '
        'btnOcr
        '
        Me.btnOcr.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOcr.Location = New System.Drawing.Point(624, 3)
        Me.btnOcr.Margin = New System.Windows.Forms.Padding(4, 3, 0, 3)
        Me.btnOcr.Name = "btnOcr"
        Me.btnOcr.Size = New System.Drawing.Size(107, 42)
        Me.btnOcr.TabIndex = 1
        Me.btnOcr.Text = "OCR خلفي"
        Me.btnOcr.UseVisualStyleBackColor = True
        '
        'btnQuick
        '
        Me.btnQuick.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnQuick.Location = New System.Drawing.Point(513, 3)
        Me.btnQuick.Margin = New System.Windows.Forms.Padding(4, 3, 0, 3)
        Me.btnQuick.Name = "btnQuick"
        Me.btnQuick.Size = New System.Drawing.Size(107, 42)
        Me.btnQuick.TabIndex = 2
        Me.btnQuick.Text = "فهم سريع"
        Me.btnQuick.UseVisualStyleBackColor = True
        '
        'btnDeep
        '
        Me.btnDeep.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnDeep.Location = New System.Drawing.Point(402, 3)
        Me.btnDeep.Margin = New System.Windows.Forms.Padding(4, 3, 0, 3)
        Me.btnDeep.Name = "btnDeep"
        Me.btnDeep.Size = New System.Drawing.Size(107, 42)
        Me.btnDeep.TabIndex = 3
        Me.btnDeep.Text = "فهم عميق"
        Me.btnDeep.UseVisualStyleBackColor = True
        '
        'cboTone
        '
        Me.cboTone.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboTone.FormattingEnabled = True
        Me.cboTone.Items.AddRange(New Object() {"رسمي متوازن", "قوي وحازم", "متعاطف ومتفهم", "مختصر تنفيذي", "رسمي دقيق"})
        Me.cboTone.Location = New System.Drawing.Point(248, 10)
        Me.cboTone.Margin = New System.Windows.Forms.Padding(4, 10, 0, 3)
        Me.cboTone.Name = "cboTone"
        Me.cboTone.Size = New System.Drawing.Size(150, 22)
        Me.cboTone.TabIndex = 4
        '
        'btnReply
        '
        Me.btnReply.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnReply.Location = New System.Drawing.Point(137, 3)
        Me.btnReply.Margin = New System.Windows.Forms.Padding(4, 3, 0, 3)
        Me.btnReply.Name = "btnReply"
        Me.btnReply.Size = New System.Drawing.Size(107, 42)
        Me.btnReply.TabIndex = 5
        Me.btnReply.Text = "إنشاء رد"
        Me.btnReply.UseVisualStyleBackColor = True
        '
        'btnExportReply
        '
        Me.btnExportReply.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnExportReply.Location = New System.Drawing.Point(4, 3)
        Me.btnExportReply.Margin = New System.Windows.Forms.Padding(4, 3, 0, 3)
        Me.btnExportReply.Name = "btnExportReply"
        Me.btnExportReply.Size = New System.Drawing.Size(129, 42)
        Me.btnExportReply.TabIndex = 6
        Me.btnExportReply.Text = "تصدير Word"
        Me.btnExportReply.UseVisualStyleBackColor = True
        '
        'SplitContainer1
        '
        Me.SplitContainer1.BackColor = System.Drawing.Color.FromArgb(226, 231, 236)
        Me.SplitContainer1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.SplitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1
        Me.SplitContainer1.IsSplitterFixed = False
        Me.SplitContainer1.Location = New System.Drawing.Point(0, 68)
        Me.SplitContainer1.Name = "SplitContainer1"
        Me.SplitContainer1.Panel1.BackColor = System.Drawing.Color.White
        Me.SplitContainer1.Panel1.Controls.Add(Me.dgvDocs)
        Me.SplitContainer1.Panel1.Controls.Add(Me.pnlListFooter)
        Me.SplitContainer1.Panel1.Controls.Add(Me.pnlSearch)
        Me.SplitContainer1.Panel1MinSize = 400
        Me.SplitContainer1.Panel2.BackColor = System.Drawing.Color.White
        Me.SplitContainer1.Panel2.Controls.Add(Me.TabControl1)
        Me.SplitContainer1.Panel2MinSize = 620
        Me.SplitContainer1.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.SplitContainer1.Size = New System.Drawing.Size(1360, 732)
        Me.SplitContainer1.SplitterDistance = 455
        Me.SplitContainer1.SplitterWidth = 6
        Me.SplitContainer1.TabIndex = 1
        '
        'dgvDocs
        '
        Me.dgvDocs.AllowUserToAddRows = False
        Me.dgvDocs.AllowUserToDeleteRows = False
        Me.dgvDocs.AllowUserToResizeRows = False
        alternatingStyle.BackColor = System.Drawing.Color.FromArgb(248, 250, 252)
        Me.dgvDocs.AlternatingRowsDefaultCellStyle = alternatingStyle
        Me.dgvDocs.AutoGenerateColumns = False
        Me.dgvDocs.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None
        Me.dgvDocs.BackgroundColor = System.Drawing.Color.White
        Me.dgvDocs.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.dgvDocs.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal
        Me.dgvDocs.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None
        headerStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter
        headerStyle.BackColor = System.Drawing.Color.FromArgb(42, 57, 72)
        headerStyle.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        headerStyle.ForeColor = System.Drawing.Color.White
        headerStyle.SelectionBackColor = System.Drawing.Color.FromArgb(42, 57, 72)
        headerStyle.SelectionForeColor = System.Drawing.Color.White
        headerStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True
        Me.dgvDocs.ColumnHeadersDefaultCellStyle = headerStyle
        Me.dgvDocs.ColumnHeadersHeight = 42
        Me.dgvDocs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        Me.dgvDocs.Columns.AddRange(New System.Windows.Forms.DataGridViewColumn() {Me.colArchiveNumber, Me.colTitle, Me.colStatus, Me.colUpdatedAt})
        rowsStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight
        rowsStyle.BackColor = System.Drawing.Color.White
        rowsStyle.Font = New System.Drawing.Font("Tahoma", 9.0!)
        rowsStyle.ForeColor = System.Drawing.Color.FromArgb(45, 55, 65)
        rowsStyle.SelectionBackColor = System.Drawing.Color.FromArgb(218, 237, 249)
        rowsStyle.SelectionForeColor = System.Drawing.Color.FromArgb(20, 60, 85)
        rowsStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.False
        Me.dgvDocs.DefaultCellStyle = rowsStyle
        Me.dgvDocs.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvDocs.EnableHeadersVisualStyles = False
        Me.dgvDocs.GridColor = System.Drawing.Color.FromArgb(230, 234, 238)
        Me.dgvDocs.Location = New System.Drawing.Point(0, 96)
        Me.dgvDocs.MultiSelect = False
        Me.dgvDocs.Name = "dgvDocs"
        Me.dgvDocs.ReadOnly = True
        Me.dgvDocs.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.dgvDocs.RowHeadersVisible = False
        Me.dgvDocs.RowTemplate.Height = 54
        Me.dgvDocs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvDocs.Size = New System.Drawing.Size(455, 602)
        Me.dgvDocs.TabIndex = 1
        '
        'colArchiveNumber
        '
        Me.colArchiveNumber.DataPropertyName = "ArchiveNumber"
        Me.colArchiveNumber.HeaderText = "رقم الأرشيف"
        Me.colArchiveNumber.MinimumWidth = 105
        Me.colArchiveNumber.Name = "colArchiveNumber"
        Me.colArchiveNumber.ReadOnly = True
        Me.colArchiveNumber.Width = 112
        '
        'colTitle
        '
        Me.colTitle.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill
        Me.colTitle.DataPropertyName = "Title"
        Me.colTitle.HeaderText = "عنوان المستند"
        Me.colTitle.MinimumWidth = 135
        Me.colTitle.Name = "colTitle"
        Me.colTitle.ReadOnly = True
        '
        'colStatus
        '
        Me.colStatus.DataPropertyName = "Status"
        Me.colStatus.HeaderText = "الحالة"
        Me.colStatus.MinimumWidth = 78
        Me.colStatus.Name = "colStatus"
        Me.colStatus.ReadOnly = True
        Me.colStatus.Width = 86
        '
        'colUpdatedAt
        '
        Me.colUpdatedAt.DataPropertyName = "UpdatedAt"
        Me.colUpdatedAt.DefaultCellStyle.Format = "dd/MM/yy"
        Me.colUpdatedAt.HeaderText = "آخر تحديث"
        Me.colUpdatedAt.MinimumWidth = 78
        Me.colUpdatedAt.Name = "colUpdatedAt"
        Me.colUpdatedAt.ReadOnly = True
        Me.colUpdatedAt.Width = 82
        '
        'pnlListFooter
        '
        Me.pnlListFooter.BackColor = System.Drawing.Color.FromArgb(247, 249, 251)
        Me.pnlListFooter.Controls.Add(Me.lblListCount)
        Me.pnlListFooter.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.pnlListFooter.Location = New System.Drawing.Point(0, 698)
        Me.pnlListFooter.Name = "pnlListFooter"
        Me.pnlListFooter.Padding = New System.Windows.Forms.Padding(12, 0, 12, 0)
        Me.pnlListFooter.Size = New System.Drawing.Size(455, 34)
        Me.pnlListFooter.TabIndex = 2
        '
        'lblListCount
        '
        Me.lblListCount.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblListCount.ForeColor = System.Drawing.Color.FromArgb(88, 99, 110)
        Me.lblListCount.Location = New System.Drawing.Point(12, 0)
        Me.lblListCount.Name = "lblListCount"
        Me.lblListCount.Size = New System.Drawing.Size(431, 34)
        Me.lblListCount.TabIndex = 0
        Me.lblListCount.Text = "0 مستند"
        Me.lblListCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'pnlSearch
        '
        Me.pnlSearch.BackColor = System.Drawing.Color.White
        Me.pnlSearch.Controls.Add(Me.tlpSearch)
        Me.pnlSearch.Controls.Add(Me.lblDocumentsTitle)
        Me.pnlSearch.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlSearch.Location = New System.Drawing.Point(0, 0)
        Me.pnlSearch.Name = "pnlSearch"
        Me.pnlSearch.Padding = New System.Windows.Forms.Padding(12, 10, 12, 10)
        Me.pnlSearch.Size = New System.Drawing.Size(455, 96)
        Me.pnlSearch.TabIndex = 0
        '
        'tlpSearch
        '
        Me.tlpSearch.ColumnCount = 3
        Me.tlpSearch.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 72.0!))
        Me.tlpSearch.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 82.0!))
        Me.tlpSearch.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.tlpSearch.Controls.Add(Me.btnClearSearch, 0, 0)
        Me.tlpSearch.Controls.Add(Me.btnSearch, 1, 0)
        Me.tlpSearch.Controls.Add(Me.txtSearch, 2, 0)
        Me.tlpSearch.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.tlpSearch.Location = New System.Drawing.Point(12, 48)
        Me.tlpSearch.Name = "tlpSearch"
        Me.tlpSearch.RowCount = 1
        Me.tlpSearch.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.tlpSearch.Size = New System.Drawing.Size(431, 38)
        Me.tlpSearch.TabIndex = 1
        '
        'btnClearSearch
        '
        Me.btnClearSearch.Dock = System.Windows.Forms.DockStyle.Fill
        Me.btnClearSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnClearSearch.Location = New System.Drawing.Point(3, 3)
        Me.btnClearSearch.Name = "btnClearSearch"
        Me.btnClearSearch.Size = New System.Drawing.Size(66, 32)
        Me.btnClearSearch.TabIndex = 2
        Me.btnClearSearch.Text = "مسح"
        Me.btnClearSearch.UseVisualStyleBackColor = True
        '
        'btnSearch
        '
        Me.btnSearch.Dock = System.Windows.Forms.DockStyle.Fill
        Me.btnSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSearch.Location = New System.Drawing.Point(75, 3)
        Me.btnSearch.Name = "btnSearch"
        Me.btnSearch.Size = New System.Drawing.Size(76, 32)
        Me.btnSearch.TabIndex = 1
        Me.btnSearch.Text = "بحث"
        Me.btnSearch.UseVisualStyleBackColor = True
        '
        'txtSearch
        '
        Me.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtSearch.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.txtSearch.Location = New System.Drawing.Point(157, 6)
        Me.txtSearch.Margin = New System.Windows.Forms.Padding(3, 6, 3, 3)
        Me.txtSearch.Name = "txtSearch"
        Me.txtSearch.Size = New System.Drawing.Size(271, 24)
        Me.txtSearch.TabIndex = 0
        '
        'lblDocumentsTitle
        '
        Me.lblDocumentsTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblDocumentsTitle.Font = New System.Drawing.Font("Tahoma", 11.0!, System.Drawing.FontStyle.Bold)
        Me.lblDocumentsTitle.ForeColor = System.Drawing.Color.FromArgb(42, 57, 72)
        Me.lblDocumentsTitle.Location = New System.Drawing.Point(12, 10)
        Me.lblDocumentsTitle.Name = "lblDocumentsTitle"
        Me.lblDocumentsTitle.Size = New System.Drawing.Size(431, 28)
        Me.lblDocumentsTitle.TabIndex = 0
        Me.lblDocumentsTitle.Text = "المستندات المؤرشفة"
        Me.lblDocumentsTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'TabControl1
        '
        Me.TabControl1.Controls.Add(Me.tabInfo)
        Me.TabControl1.Controls.Add(Me.tabText)
        Me.TabControl1.Controls.Add(Me.tabEntities)
        Me.TabControl1.Controls.Add(Me.tabAi)
        Me.TabControl1.Controls.Add(Me.tabReply)
        Me.TabControl1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.TabControl1.Font = New System.Drawing.Font("Tahoma", 10.0!, System.Drawing.FontStyle.Bold)
        Me.TabControl1.Location = New System.Drawing.Point(0, 0)
        Me.TabControl1.Name = "TabControl1"
        Me.TabControl1.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.TabControl1.RightToLeftLayout = True
        Me.TabControl1.SelectedIndex = 0
        Me.TabControl1.Size = New System.Drawing.Size(899, 732)
        Me.TabControl1.TabIndex = 0
        '
        'tabInfo
        '
        Me.tabInfo.BackColor = System.Drawing.Color.FromArgb(246, 248, 250)
        Me.tabInfo.Controls.Add(Me.pnlInfoSurface)
        Me.tabInfo.Location = New System.Drawing.Point(4, 25)
        Me.tabInfo.Name = "tabInfo"
        Me.tabInfo.Padding = New System.Windows.Forms.Padding(18)
        Me.tabInfo.Size = New System.Drawing.Size(891, 703)
        Me.tabInfo.TabIndex = 0
        Me.tabInfo.Text = "بيانات المستند"
        '
        'pnlInfoSurface
        '
        Me.pnlInfoSurface.BackColor = System.Drawing.Color.White
        Me.pnlInfoSurface.Controls.Add(Me.TableLayoutPanel1)
        Me.pnlInfoSurface.Controls.Add(Me.lblInfoTitle)
        Me.pnlInfoSurface.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlInfoSurface.Location = New System.Drawing.Point(18, 18)
        Me.pnlInfoSurface.Name = "pnlInfoSurface"
        Me.pnlInfoSurface.Padding = New System.Windows.Forms.Padding(18)
        Me.pnlInfoSurface.Size = New System.Drawing.Size(855, 330)
        Me.pnlInfoSurface.TabIndex = 0
        '
        'TableLayoutPanel1
        '
        Me.TableLayoutPanel1.ColumnCount = 4
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 34.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 34.0!))
        Me.TableLayoutPanel1.Controls.Add(Me.lblTitleCaption, 0, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.txtTitle, 1, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.lblArchiveCaption, 2, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.txtArchiveNo, 3, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.lblCategoryCaption, 0, 1)
        Me.TableLayoutPanel1.Controls.Add(Me.txtCategory, 1, 1)
        Me.TableLayoutPanel1.Controls.Add(Me.lblSubCategoryCaption, 2, 1)
        Me.TableLayoutPanel1.Controls.Add(Me.txtSubCategory, 3, 1)
        Me.TableLayoutPanel1.Controls.Add(Me.lblSourceCaption, 0, 2)
        Me.TableLayoutPanel1.Controls.Add(Me.txtSource, 1, 2)
        Me.TableLayoutPanel1.Controls.Add(Me.lblDestinationCaption, 2, 2)
        Me.TableLayoutPanel1.Controls.Add(Me.txtDestination, 3, 2)
        Me.TableLayoutPanel1.Controls.Add(Me.lblStatusCaption, 0, 3)
        Me.TableLayoutPanel1.Controls.Add(Me.txtStatus, 1, 3)
        Me.TableLayoutPanel1.Controls.Add(Me.lblPriorityCaption, 2, 3)
        Me.TableLayoutPanel1.Controls.Add(Me.txtPriority, 3, 3)
        Me.TableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.TableLayoutPanel1.Location = New System.Drawing.Point(18, 62)
        Me.TableLayoutPanel1.Name = "TableLayoutPanel1"
        Me.TableLayoutPanel1.RowCount = 4
        Me.TableLayoutPanel1.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.0!))
        Me.TableLayoutPanel1.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.0!))
        Me.TableLayoutPanel1.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.0!))
        Me.TableLayoutPanel1.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.0!))
        Me.TableLayoutPanel1.Size = New System.Drawing.Size(819, 250)
        Me.TableLayoutPanel1.TabIndex = 1
        '
        'labels and fields
        '
        ConfigureCaptionLabel(Me.lblTitleCaption, "عنوان المستند")
        ConfigureReadOnlyField(Me.txtTitle)
        ConfigureCaptionLabel(Me.lblArchiveCaption, "رقم الأرشيف")
        ConfigureReadOnlyField(Me.txtArchiveNo)
        ConfigureCaptionLabel(Me.lblCategoryCaption, "التصنيف العام")
        ConfigureReadOnlyField(Me.txtCategory)
        ConfigureCaptionLabel(Me.lblSubCategoryCaption, "التصنيف الفرعي")
        ConfigureReadOnlyField(Me.txtSubCategory)
        ConfigureCaptionLabel(Me.lblSourceCaption, "الجهة الصادرة")
        ConfigureReadOnlyField(Me.txtSource)
        ConfigureCaptionLabel(Me.lblDestinationCaption, "الجهة الواردة")
        ConfigureReadOnlyField(Me.txtDestination)
        ConfigureCaptionLabel(Me.lblStatusCaption, "حالة المعاملة")
        ConfigureReadOnlyField(Me.txtStatus)
        ConfigureCaptionLabel(Me.lblPriorityCaption, "الأولوية")
        ConfigureReadOnlyField(Me.txtPriority)
        '
        'lblInfoTitle
        '
        Me.lblInfoTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblInfoTitle.Font = New System.Drawing.Font("Tahoma", 12.0!, System.Drawing.FontStyle.Bold)
        Me.lblInfoTitle.ForeColor = System.Drawing.Color.FromArgb(42, 57, 72)
        Me.lblInfoTitle.Location = New System.Drawing.Point(18, 18)
        Me.lblInfoTitle.Name = "lblInfoTitle"
        Me.lblInfoTitle.Size = New System.Drawing.Size(819, 44)
        Me.lblInfoTitle.TabIndex = 0
        Me.lblInfoTitle.Text = "البيانات الأساسية للمستند"
        Me.lblInfoTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'tabText
        '
        Me.tabText.Controls.Add(Me.rtbText)
        Me.tabText.Location = New System.Drawing.Point(4, 25)
        Me.tabText.Name = "tabText"
        Me.tabText.Padding = New System.Windows.Forms.Padding(10)
        Me.tabText.Size = New System.Drawing.Size(891, 703)
        Me.tabText.TabIndex = 1
        Me.tabText.Text = "النص المنسق"
        Me.tabText.UseVisualStyleBackColor = True
        '
        'rtbText
        '
        Me.rtbText.BackColor = System.Drawing.Color.White
        Me.rtbText.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.rtbText.Dock = System.Windows.Forms.DockStyle.Fill
        Me.rtbText.Font = New System.Drawing.Font("Tahoma", 11.0!)
        Me.rtbText.Location = New System.Drawing.Point(10, 10)
        Me.rtbText.Name = "rtbText"
        Me.rtbText.ReadOnly = True
        Me.rtbText.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.rtbText.Size = New System.Drawing.Size(871, 683)
        Me.rtbText.TabIndex = 0
        Me.rtbText.Text = ""
        '
        'tabEntities
        '
        Me.tabEntities.Controls.Add(Me.txtEntities)
        Me.tabEntities.Location = New System.Drawing.Point(4, 25)
        Me.tabEntities.Name = "tabEntities"
        Me.tabEntities.Padding = New System.Windows.Forms.Padding(10)
        Me.tabEntities.Size = New System.Drawing.Size(891, 703)
        Me.tabEntities.TabIndex = 2
        Me.tabEntities.Text = "الكيانات"
        Me.tabEntities.UseVisualStyleBackColor = True
        '
        'txtEntities
        '
        Me.txtEntities.BackColor = System.Drawing.Color.White
        Me.txtEntities.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtEntities.Font = New System.Drawing.Font("Tahoma", 10.5!)
        Me.txtEntities.Location = New System.Drawing.Point(10, 10)
        Me.txtEntities.Multiline = True
        Me.txtEntities.Name = "txtEntities"
        Me.txtEntities.ReadOnly = True
        Me.txtEntities.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.txtEntities.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtEntities.Size = New System.Drawing.Size(871, 683)
        Me.txtEntities.TabIndex = 0
        '
        'tabAi
        '
        Me.tabAi.Controls.Add(Me.txtAi)
        Me.tabAi.Location = New System.Drawing.Point(4, 25)
        Me.tabAi.Name = "tabAi"
        Me.tabAi.Padding = New System.Windows.Forms.Padding(10)
        Me.tabAi.Size = New System.Drawing.Size(891, 703)
        Me.tabAi.TabIndex = 3
        Me.tabAi.Text = "الفهم العميق"
        Me.tabAi.UseVisualStyleBackColor = True
        '
        'txtAi
        '
        Me.txtAi.BackColor = System.Drawing.Color.White
        Me.txtAi.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtAi.Font = New System.Drawing.Font("Tahoma", 10.5!)
        Me.txtAi.Location = New System.Drawing.Point(10, 10)
        Me.txtAi.Multiline = True
        Me.txtAi.Name = "txtAi"
        Me.txtAi.ReadOnly = True
        Me.txtAi.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.txtAi.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtAi.Size = New System.Drawing.Size(871, 683)
        Me.txtAi.TabIndex = 0
        '
        'tabReply
        '
        Me.tabReply.Controls.Add(Me.txtReply)
        Me.tabReply.Location = New System.Drawing.Point(4, 25)
        Me.tabReply.Name = "tabReply"
        Me.tabReply.Padding = New System.Windows.Forms.Padding(10)
        Me.tabReply.Size = New System.Drawing.Size(891, 703)
        Me.tabReply.TabIndex = 4
        Me.tabReply.Text = "الردود"
        Me.tabReply.UseVisualStyleBackColor = True
        '
        'txtReply
        '
        Me.txtReply.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtReply.Font = New System.Drawing.Font("Tahoma", 11.0!)
        Me.txtReply.Location = New System.Drawing.Point(10, 10)
        Me.txtReply.Multiline = True
        Me.txtReply.Name = "txtReply"
        Me.txtReply.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.txtReply.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtReply.Size = New System.Drawing.Size(871, 683)
        Me.txtReply.TabIndex = 0
        '
        'SaveFileDialog1
        '
        Me.SaveFileDialog1.Filter = "Word Document|*.docx|Text|*.txt"
        '
        'DocumentWorkspaceForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 14.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(238, 242, 245)
        Me.ClientSize = New System.Drawing.Size(1360, 800)
        Me.Controls.Add(Me.SplitContainer1)
        Me.Controls.Add(Me.pnlToolbar)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(1180, 720)
        Me.Name = "DocumentWorkspaceForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "مركز المستند الموحد"
        Me.WindowState = System.Windows.Forms.FormWindowState.Maximized
        Me.pnlToolbar.ResumeLayout(False)
        Me.pnlToolbar.PerformLayout()
        Me.flpActions.ResumeLayout(False)
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.SplitContainer1.Panel1.ResumeLayout(False)
        Me.SplitContainer1.Panel2.ResumeLayout(False)
        Me.SplitContainer1.ResumeLayout(False)
        CType(Me.dgvDocs, System.ComponentModel.ISupportInitialize).EndInit()
        Me.pnlListFooter.ResumeLayout(False)
        Me.pnlSearch.ResumeLayout(False)
        Me.tlpSearch.ResumeLayout(False)
        Me.tlpSearch.PerformLayout()
        Me.TabControl1.ResumeLayout(False)
        Me.tabInfo.ResumeLayout(False)
        Me.pnlInfoSurface.ResumeLayout(False)
        Me.TableLayoutPanel1.ResumeLayout(False)
        Me.TableLayoutPanel1.PerformLayout()
        Me.tabText.ResumeLayout(False)
        Me.tabEntities.ResumeLayout(False)
        Me.tabEntities.PerformLayout()
        Me.tabAi.ResumeLayout(False)
        Me.tabAi.PerformLayout()
        Me.tabReply.ResumeLayout(False)
        Me.tabReply.PerformLayout()
        Me.ResumeLayout(False)
    End Sub

    Private Sub ConfigureCaptionLabel(target As System.Windows.Forms.Label, caption As String)
        target.Dock = System.Windows.Forms.DockStyle.Fill
        target.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        target.ForeColor = System.Drawing.Color.FromArgb(78, 89, 100)
        target.Margin = New System.Windows.Forms.Padding(8, 8, 8, 8)
        target.Text = caption
        target.TextAlign = System.Drawing.ContentAlignment.MiddleRight
    End Sub

    Private Sub ConfigureReadOnlyField(target As System.Windows.Forms.TextBox)
        target.BackColor = System.Drawing.Color.FromArgb(249, 250, 252)
        target.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        target.Dock = System.Windows.Forms.DockStyle.Fill
        target.Font = New System.Drawing.Font("Tahoma", 10.0!)
        target.Margin = New System.Windows.Forms.Padding(8, 12, 8, 12)
        target.ReadOnly = True
        target.RightToLeft = System.Windows.Forms.RightToLeft.Yes
    End Sub

    Friend WithEvents pnlToolbar As Panel
    Friend WithEvents lblStatus As Label
    Friend WithEvents flpActions As FlowLayoutPanel
    Friend WithEvents btnAutomate As Button
    Friend WithEvents btnOcr As Button
    Friend WithEvents btnQuick As Button
    Friend WithEvents btnDeep As Button
    Friend WithEvents cboTone As ComboBox
    Friend WithEvents btnReply As Button
    Friend WithEvents btnExportReply As Button
    Friend WithEvents SplitContainer1 As SplitContainer
    Friend WithEvents dgvDocs As DataGridView
    Friend WithEvents colArchiveNumber As DataGridViewTextBoxColumn
    Friend WithEvents colTitle As DataGridViewTextBoxColumn
    Friend WithEvents colStatus As DataGridViewTextBoxColumn
    Friend WithEvents colUpdatedAt As DataGridViewTextBoxColumn
    Friend WithEvents pnlListFooter As Panel
    Friend WithEvents lblListCount As Label
    Friend WithEvents pnlSearch As Panel
    Friend WithEvents tlpSearch As TableLayoutPanel
    Friend WithEvents btnClearSearch As Button
    Friend WithEvents btnSearch As Button
    Friend WithEvents txtSearch As TextBox
    Friend WithEvents lblDocumentsTitle As Label
    Friend WithEvents TabControl1 As TabControl
    Friend WithEvents tabInfo As TabPage
    Friend WithEvents pnlInfoSurface As Panel
    Friend WithEvents TableLayoutPanel1 As TableLayoutPanel
    Friend WithEvents lblTitleCaption As Label
    Friend WithEvents txtTitle As TextBox
    Friend WithEvents lblArchiveCaption As Label
    Friend WithEvents txtArchiveNo As TextBox
    Friend WithEvents lblCategoryCaption As Label
    Friend WithEvents txtCategory As TextBox
    Friend WithEvents lblSubCategoryCaption As Label
    Friend WithEvents txtSubCategory As TextBox
    Friend WithEvents lblSourceCaption As Label
    Friend WithEvents txtSource As TextBox
    Friend WithEvents lblDestinationCaption As Label
    Friend WithEvents txtDestination As TextBox
    Friend WithEvents lblStatusCaption As Label
    Friend WithEvents txtStatus As TextBox
    Friend WithEvents lblPriorityCaption As Label
    Friend WithEvents txtPriority As TextBox
    Friend WithEvents lblInfoTitle As Label
    Friend WithEvents tabText As TabPage
    Friend WithEvents rtbText As RichTextBox
    Friend WithEvents tabEntities As TabPage
    Friend WithEvents txtEntities As TextBox
    Friend WithEvents tabAi As TabPage
    Friend WithEvents txtAi As TextBox
    Friend WithEvents tabReply As TabPage
    Friend WithEvents txtReply As TextBox
    Friend WithEvents SaveFileDialog1 As SaveFileDialog
End Class
