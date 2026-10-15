<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ProcessingCenterForm
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer
    Friend WithEvents pnlHeader As System.Windows.Forms.Panel
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents lblHint As System.Windows.Forms.Label
    Friend WithEvents pnlToolbar As System.Windows.Forms.Panel
    Friend WithEvents btnRefresh As System.Windows.Forms.Button
    Friend WithEvents btnCancelSelected As System.Windows.Forms.Button
    Friend WithEvents btnRunMissingOcr As System.Windows.Forms.Button
    Friend WithEvents btnExtractEntities As System.Windows.Forms.Button
    Friend WithEvents btnFastUnderstand As System.Windows.Forms.Button
    Friend WithEvents btnOpenWorkspace As System.Windows.Forms.Button
    Friend WithEvents splitMain As System.Windows.Forms.SplitContainer
    Friend WithEvents dgvJobs As System.Windows.Forms.DataGridView
    Friend WithEvents txtDetails As System.Windows.Forms.TextBox
    Friend WithEvents timerRefresh As System.Windows.Forms.Timer
    Friend WithEvents lblStats As System.Windows.Forms.Label

    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then components.Dispose()
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.pnlHeader = New System.Windows.Forms.Panel()
        Me.lblHint = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.pnlToolbar = New System.Windows.Forms.Panel()
        Me.lblStats = New System.Windows.Forms.Label()
        Me.btnOpenWorkspace = New System.Windows.Forms.Button()
        Me.btnFastUnderstand = New System.Windows.Forms.Button()
        Me.btnExtractEntities = New System.Windows.Forms.Button()
        Me.btnRunMissingOcr = New System.Windows.Forms.Button()
        Me.btnCancelSelected = New System.Windows.Forms.Button()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.splitMain = New System.Windows.Forms.SplitContainer()
        Me.dgvJobs = New System.Windows.Forms.DataGridView()
        Me.txtDetails = New System.Windows.Forms.TextBox()
        Me.timerRefresh = New System.Windows.Forms.Timer(Me.components)
        Me.pnlHeader.SuspendLayout()
        Me.pnlToolbar.SuspendLayout()
        CType(Me.splitMain, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.splitMain.Panel1.SuspendLayout()
        Me.splitMain.Panel2.SuspendLayout()
        Me.splitMain.SuspendLayout()
        CType(Me.dgvJobs, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'pnlHeader
        '
        Me.pnlHeader.BackColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.pnlHeader.Controls.Add(Me.lblHint)
        Me.pnlHeader.Controls.Add(Me.lblTitle)
        Me.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlHeader.Location = New System.Drawing.Point(0, 0)
        Me.pnlHeader.Name = "pnlHeader"
        Me.pnlHeader.Padding = New System.Windows.Forms.Padding(18, 12, 18, 10)
        Me.pnlHeader.Size = New System.Drawing.Size(1184, 88)
        Me.pnlHeader.TabIndex = 0
        '
        'lblHint
        '
        Me.lblHint.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblHint.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.lblHint.ForeColor = System.Drawing.Color.FromArgb(CType(CType(220, Byte), Integer), CType(CType(235, Byte), Integer), CType(CType(232, Byte), Integer))
        Me.lblHint.Location = New System.Drawing.Point(18, 46)
        Me.lblHint.Name = "lblHint"
        Me.lblHint.Size = New System.Drawing.Size(1148, 32)
        Me.lblHint.TabIndex = 1
        Me.lblHint.Text = "يتم تنفيذ OCR والذكاء السحابي واستخراج الكيانات في طابور خلفي مع كاش لضمان استجابة الواجهة."
        Me.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 16.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(18, 12)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(1148, 34)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "مركز المعالجة والأداء"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'pnlToolbar
        '
        Me.pnlToolbar.BackColor = System.Drawing.Color.White
        Me.pnlToolbar.Controls.Add(Me.lblStats)
        Me.pnlToolbar.Controls.Add(Me.btnOpenWorkspace)
        Me.pnlToolbar.Controls.Add(Me.btnFastUnderstand)
        Me.pnlToolbar.Controls.Add(Me.btnExtractEntities)
        Me.pnlToolbar.Controls.Add(Me.btnRunMissingOcr)
        Me.pnlToolbar.Controls.Add(Me.btnCancelSelected)
        Me.pnlToolbar.Controls.Add(Me.btnRefresh)
        Me.pnlToolbar.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlToolbar.Location = New System.Drawing.Point(0, 88)
        Me.pnlToolbar.Name = "pnlToolbar"
        Me.pnlToolbar.Padding = New System.Windows.Forms.Padding(12, 10, 12, 10)
        Me.pnlToolbar.Size = New System.Drawing.Size(1184, 66)
        Me.pnlToolbar.TabIndex = 1
        '
        'lblStats
        '
        Me.lblStats.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblStats.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.lblStats.ForeColor = System.Drawing.Color.FromArgb(CType(CType(50, Byte), Integer), CType(CType(72, Byte), Integer), CType(CType(68, Byte), Integer))
        Me.lblStats.Location = New System.Drawing.Point(12, 10)
        Me.lblStats.Name = "lblStats"
        Me.lblStats.Size = New System.Drawing.Size(322, 46)
        Me.lblStats.TabIndex = 6
        Me.lblStats.Text = "جاهز"
        Me.lblStats.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'buttons
        '
        Me.btnOpenWorkspace.Dock = System.Windows.Forms.DockStyle.Right
        Me.btnOpenWorkspace.Location = New System.Drawing.Point(334, 10)
        Me.btnOpenWorkspace.Name = "btnOpenWorkspace"
        Me.btnOpenWorkspace.Size = New System.Drawing.Size(130, 46)
        Me.btnOpenWorkspace.TabIndex = 5
        Me.btnOpenWorkspace.Text = "مركز العمل"
        Me.btnOpenWorkspace.UseVisualStyleBackColor = True
        Me.btnFastUnderstand.Dock = System.Windows.Forms.DockStyle.Right
        Me.btnFastUnderstand.Location = New System.Drawing.Point(464, 10)
        Me.btnFastUnderstand.Name = "btnFastUnderstand"
        Me.btnFastUnderstand.Size = New System.Drawing.Size(150, 46)
        Me.btnFastUnderstand.TabIndex = 4
        Me.btnFastUnderstand.Text = "فهم سريع للمحدد"
        Me.btnFastUnderstand.UseVisualStyleBackColor = True
        Me.btnExtractEntities.Dock = System.Windows.Forms.DockStyle.Right
        Me.btnExtractEntities.Location = New System.Drawing.Point(614, 10)
        Me.btnExtractEntities.Name = "btnExtractEntities"
        Me.btnExtractEntities.Size = New System.Drawing.Size(150, 46)
        Me.btnExtractEntities.TabIndex = 3
        Me.btnExtractEntities.Text = "كيانات للمحدد"
        Me.btnExtractEntities.UseVisualStyleBackColor = True
        Me.btnRunMissingOcr.Dock = System.Windows.Forms.DockStyle.Right
        Me.btnRunMissingOcr.Location = New System.Drawing.Point(764, 10)
        Me.btnRunMissingOcr.Name = "btnRunMissingOcr"
        Me.btnRunMissingOcr.Size = New System.Drawing.Size(170, 46)
        Me.btnRunMissingOcr.TabIndex = 2
        Me.btnRunMissingOcr.Text = "OCR للناقص"
        Me.btnRunMissingOcr.UseVisualStyleBackColor = True
        Me.btnCancelSelected.Dock = System.Windows.Forms.DockStyle.Right
        Me.btnCancelSelected.Location = New System.Drawing.Point(934, 10)
        Me.btnCancelSelected.Name = "btnCancelSelected"
        Me.btnCancelSelected.Size = New System.Drawing.Size(120, 46)
        Me.btnCancelSelected.TabIndex = 1
        Me.btnCancelSelected.Text = "إلغاء المحدد"
        Me.btnCancelSelected.UseVisualStyleBackColor = True
        Me.btnRefresh.Dock = System.Windows.Forms.DockStyle.Right
        Me.btnRefresh.Location = New System.Drawing.Point(1054, 10)
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Size = New System.Drawing.Size(118, 46)
        Me.btnRefresh.TabIndex = 0
        Me.btnRefresh.Text = "تحديث"
        Me.btnRefresh.UseVisualStyleBackColor = True
        '
        'splitMain
        '
        Me.splitMain.Dock = System.Windows.Forms.DockStyle.Fill
        Me.splitMain.Location = New System.Drawing.Point(0, 154)
        Me.splitMain.Name = "splitMain"
        Me.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal
        Me.splitMain.Panel1.Controls.Add(Me.dgvJobs)
        Me.splitMain.Panel2.Controls.Add(Me.txtDetails)
        Me.splitMain.Size = New System.Drawing.Size(1184, 607)
        Me.splitMain.SplitterDistance = 384
        Me.splitMain.TabIndex = 2
        '
        'dgvJobs
        '
        Me.dgvJobs.AllowUserToAddRows = False
        Me.dgvJobs.AllowUserToDeleteRows = False
        Me.dgvJobs.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvJobs.BackgroundColor = System.Drawing.Color.White
        Me.dgvJobs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.dgvJobs.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvJobs.Location = New System.Drawing.Point(0, 0)
        Me.dgvJobs.MultiSelect = False
        Me.dgvJobs.Name = "dgvJobs"
        Me.dgvJobs.ReadOnly = True
        Me.dgvJobs.RowHeadersVisible = False
        Me.dgvJobs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvJobs.Size = New System.Drawing.Size(1184, 384)
        Me.dgvJobs.TabIndex = 0
        '
        'txtDetails
        '
        Me.txtDetails.BackColor = System.Drawing.Color.White
        Me.txtDetails.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtDetails.Font = New System.Drawing.Font("Consolas", 10.0!)
        Me.txtDetails.Location = New System.Drawing.Point(0, 0)
        Me.txtDetails.Multiline = True
        Me.txtDetails.Name = "txtDetails"
        Me.txtDetails.ReadOnly = True
        Me.txtDetails.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtDetails.Size = New System.Drawing.Size(1184, 219)
        Me.txtDetails.TabIndex = 0
        '
        'timerRefresh
        '
        Me.timerRefresh.Interval = 1500
        '
        'ProcessingCenterForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1184, 761)
        Me.Controls.Add(Me.splitMain)
        Me.Controls.Add(Me.pnlToolbar)
        Me.Controls.Add(Me.pnlHeader)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.Name = "ProcessingCenterForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "مركز المعالجة والأداء"
        Me.pnlHeader.ResumeLayout(False)
        Me.pnlToolbar.ResumeLayout(False)
        Me.splitMain.Panel1.ResumeLayout(False)
        Me.splitMain.Panel2.ResumeLayout(False)
        Me.splitMain.Panel2.PerformLayout()
        CType(Me.splitMain, System.ComponentModel.ISupportInitialize).EndInit()
        Me.splitMain.ResumeLayout(False)
        CType(Me.dgvJobs, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
    End Sub
End Class
