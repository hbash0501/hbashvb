<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class AuditLogForm
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
        Me.dgvAudit = New System.Windows.Forms.DataGridView()
        Me.footerPanel = New System.Windows.Forms.Panel()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.headerPanel.SuspendLayout()
        CType(Me.dgvAudit, System.ComponentModel.ISupportInitialize).BeginInit()
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
        Me.headerPanel.Size = New System.Drawing.Size(900, 74)
        Me.headerPanel.TabIndex = 0
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 14.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(18, 18)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(864, 38)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "سجل العمليات"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'dgvAudit
        '
        Me.dgvAudit.AllowUserToAddRows = False
        Me.dgvAudit.AllowUserToDeleteRows = False
        Me.dgvAudit.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvAudit.BackgroundColor = System.Drawing.Color.White
        Me.dgvAudit.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.dgvAudit.ColumnHeadersHeight = 34
        Me.dgvAudit.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvAudit.Location = New System.Drawing.Point(0, 74)
        Me.dgvAudit.MultiSelect = False
        Me.dgvAudit.Name = "dgvAudit"
        Me.dgvAudit.ReadOnly = True
        Me.dgvAudit.RowHeadersVisible = False
        Me.dgvAudit.RowTemplate.Height = 32
        Me.dgvAudit.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvAudit.Size = New System.Drawing.Size(900, 426)
        Me.dgvAudit.TabIndex = 1
        '
        'footerPanel
        '
        Me.footerPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(247, Byte), Integer), CType(CType(249, Byte), Integer), CType(CType(247, Byte), Integer))
        Me.footerPanel.Controls.Add(Me.btnClose)
        Me.footerPanel.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.footerPanel.Location = New System.Drawing.Point(0, 500)
        Me.footerPanel.Name = "footerPanel"
        Me.footerPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.footerPanel.Size = New System.Drawing.Size(900, 72)
        Me.footerPanel.TabIndex = 2
        '
        'btnClose
        '
        Me.btnClose.BackColor = System.Drawing.Color.FromArgb(CType(CType(238, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(239, Byte), Integer))
        Me.btnClose.FlatAppearance.BorderSize = 0
        Me.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnClose.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnClose.Location = New System.Drawing.Point(18, 16)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(130, 40)
        Me.btnClose.TabIndex = 0
        Me.btnClose.Text = "إغلاق"
        Me.btnClose.UseVisualStyleBackColor = False
        '
        'AuditLogForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(900, 572)
        Me.Controls.Add(Me.dgvAudit)
        Me.Controls.Add(Me.footerPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(820, 520)
        Me.Name = "AuditLogForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "سجل العمليات"
        Me.headerPanel.ResumeLayout(False)
        CType(Me.dgvAudit, System.ComponentModel.ISupportInitialize).EndInit()
        Me.footerPanel.ResumeLayout(False)
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents dgvAudit As System.Windows.Forms.DataGridView
    Friend WithEvents footerPanel As System.Windows.Forms.Panel
    Friend WithEvents btnClose As System.Windows.Forms.Button
End Class
