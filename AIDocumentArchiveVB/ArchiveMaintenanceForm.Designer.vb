<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class ArchiveMaintenanceForm
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
        Me.lblSubtitle = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.mainPanel = New System.Windows.Forms.Panel()
        Me.dgvDiagnostics = New System.Windows.Forms.DataGridView()
        Me.txtOutput = New System.Windows.Forms.TextBox()
        Me.commandsPanel = New System.Windows.Forms.Panel()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.btnOpenData = New System.Windows.Forms.Button()
        Me.btnIndex = New System.Windows.Forms.Button()
        Me.btnInventory = New System.Windows.Forms.Button()
        Me.btnBackup = New System.Windows.Forms.Button()
        Me.btnRun = New System.Windows.Forms.Button()
        Me.headerPanel.SuspendLayout()
        Me.mainPanel.SuspendLayout()
        CType(Me.dgvDiagnostics, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.commandsPanel.SuspendLayout()
        Me.SuspendLayout()
        '
        'headerPanel
        '
        Me.headerPanel.BackColor = System.Drawing.Color.White
        Me.headerPanel.Controls.Add(Me.lblSubtitle)
        Me.headerPanel.Controls.Add(Me.lblTitle)
        Me.headerPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.headerPanel.Location = New System.Drawing.Point(0, 0)
        Me.headerPanel.Name = "headerPanel"
        Me.headerPanel.Padding = New System.Windows.Forms.Padding(22)
        Me.headerPanel.Size = New System.Drawing.Size(1080, 92)
        Me.headerPanel.TabIndex = 0
        '
        'lblSubtitle
        '
        Me.lblSubtitle.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblSubtitle.Font = New System.Drawing.Font("Tahoma", 9.5!)
        Me.lblSubtitle.ForeColor = System.Drawing.Color.DimGray
        Me.lblSubtitle.Location = New System.Drawing.Point(22, 55)
        Me.lblSubtitle.Name = "lblSubtitle"
        Me.lblSubtitle.Size = New System.Drawing.Size(1036, 15)
        Me.lblSubtitle.TabIndex = 1
        Me.lblSubtitle.Text = "فحص سلامة البيانات، إنشاء نسخة احتياطية، تصدير فهرس، وبناء ملف فهرسة نصية."
        Me.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 15.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.lblTitle.Location = New System.Drawing.Point(22, 22)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(1036, 34)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "مركز الصيانة والنسخ الاحتياطي"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'mainPanel
        '
        Me.mainPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(245, Byte), Integer), CType(CType(247, Byte), Integer), CType(CType(246, Byte), Integer))
        Me.mainPanel.Controls.Add(Me.dgvDiagnostics)
        Me.mainPanel.Controls.Add(Me.txtOutput)
        Me.mainPanel.Controls.Add(Me.commandsPanel)
        Me.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.mainPanel.Location = New System.Drawing.Point(0, 92)
        Me.mainPanel.Name = "mainPanel"
        Me.mainPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.mainPanel.Size = New System.Drawing.Size(1080, 548)
        Me.mainPanel.TabIndex = 1
        '
        'dgvDiagnostics
        '
        Me.dgvDiagnostics.AllowUserToAddRows = False
        Me.dgvDiagnostics.AllowUserToDeleteRows = False
        Me.dgvDiagnostics.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvDiagnostics.BackgroundColor = System.Drawing.Color.White
        Me.dgvDiagnostics.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.dgvDiagnostics.ColumnHeadersHeight = 36
        Me.dgvDiagnostics.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvDiagnostics.Location = New System.Drawing.Point(18, 18)
        Me.dgvDiagnostics.Name = "dgvDiagnostics"
        Me.dgvDiagnostics.ReadOnly = True
        Me.dgvDiagnostics.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.dgvDiagnostics.RowHeadersVisible = False
        Me.dgvDiagnostics.RowTemplate.Height = 34
        Me.dgvDiagnostics.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvDiagnostics.Size = New System.Drawing.Size(1044, 342)
        Me.dgvDiagnostics.TabIndex = 2
        '
        'txtOutput
        '
        Me.txtOutput.BackColor = System.Drawing.Color.White
        Me.txtOutput.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.txtOutput.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.txtOutput.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.txtOutput.Location = New System.Drawing.Point(18, 360)
        Me.txtOutput.Multiline = True
        Me.txtOutput.Name = "txtOutput"
        Me.txtOutput.ReadOnly = True
        Me.txtOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtOutput.Size = New System.Drawing.Size(1044, 110)
        Me.txtOutput.TabIndex = 1
        Me.txtOutput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        '
        'commandsPanel
        '
        Me.commandsPanel.Controls.Add(Me.btnClose)
        Me.commandsPanel.Controls.Add(Me.btnOpenData)
        Me.commandsPanel.Controls.Add(Me.btnIndex)
        Me.commandsPanel.Controls.Add(Me.btnInventory)
        Me.commandsPanel.Controls.Add(Me.btnBackup)
        Me.commandsPanel.Controls.Add(Me.btnRun)
        Me.commandsPanel.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.commandsPanel.Location = New System.Drawing.Point(18, 470)
        Me.commandsPanel.Name = "commandsPanel"
        Me.commandsPanel.Padding = New System.Windows.Forms.Padding(0, 12, 0, 0)
        Me.commandsPanel.Size = New System.Drawing.Size(1044, 60)
        Me.commandsPanel.TabIndex = 0
        '
        'btnClose
        '
        Me.btnClose.BackColor = System.Drawing.Color.FromArgb(CType(CType(238, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(239, Byte), Integer))
        Me.btnClose.FlatAppearance.BorderSize = 0
        Me.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnClose.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnClose.Location = New System.Drawing.Point(14, 13)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(100, 38)
        Me.btnClose.TabIndex = 5
        Me.btnClose.Text = "إغلاق"
        Me.btnClose.UseVisualStyleBackColor = False
        '
        'btnOpenData
        '
        Me.btnOpenData.BackColor = System.Drawing.Color.White
        Me.btnOpenData.FlatAppearance.BorderColor = System.Drawing.Color.Gainsboro
        Me.btnOpenData.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOpenData.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnOpenData.Location = New System.Drawing.Point(120, 13)
        Me.btnOpenData.Name = "btnOpenData"
        Me.btnOpenData.Size = New System.Drawing.Size(140, 38)
        Me.btnOpenData.TabIndex = 4
        Me.btnOpenData.Text = "فتح مجلد البيانات"
        Me.btnOpenData.UseVisualStyleBackColor = False
        '
        'btnIndex
        '
        Me.btnIndex.BackColor = System.Drawing.Color.White
        Me.btnIndex.FlatAppearance.BorderColor = System.Drawing.Color.Gainsboro
        Me.btnIndex.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnIndex.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnIndex.Location = New System.Drawing.Point(266, 13)
        Me.btnIndex.Name = "btnIndex"
        Me.btnIndex.Size = New System.Drawing.Size(145, 38)
        Me.btnIndex.TabIndex = 3
        Me.btnIndex.Text = "بناء فهرس نصي"
        Me.btnIndex.UseVisualStyleBackColor = False
        '
        'btnInventory
        '
        Me.btnInventory.BackColor = System.Drawing.Color.White
        Me.btnInventory.FlatAppearance.BorderColor = System.Drawing.Color.Gainsboro
        Me.btnInventory.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnInventory.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnInventory.Location = New System.Drawing.Point(417, 13)
        Me.btnInventory.Name = "btnInventory"
        Me.btnInventory.Size = New System.Drawing.Size(150, 38)
        Me.btnInventory.TabIndex = 2
        Me.btnInventory.Text = "تصدير فهرس CSV"
        Me.btnInventory.UseVisualStyleBackColor = False
        '
        'btnBackup
        '
        Me.btnBackup.BackColor = System.Drawing.Color.FromArgb(CType(CType(23, Byte), Integer), CType(CType(58, Byte), Integer), CType(CType(54, Byte), Integer))
        Me.btnBackup.FlatAppearance.BorderSize = 0
        Me.btnBackup.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnBackup.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnBackup.ForeColor = System.Drawing.Color.White
        Me.btnBackup.Location = New System.Drawing.Point(573, 13)
        Me.btnBackup.Name = "btnBackup"
        Me.btnBackup.Size = New System.Drawing.Size(160, 38)
        Me.btnBackup.TabIndex = 1
        Me.btnBackup.Text = "نسخة احتياطية ZIP"
        Me.btnBackup.UseVisualStyleBackColor = False
        '
        'btnRun
        '
        Me.btnRun.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.btnRun.FlatAppearance.BorderSize = 0
        Me.btnRun.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnRun.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnRun.Location = New System.Drawing.Point(739, 13)
        Me.btnRun.Name = "btnRun"
        Me.btnRun.Size = New System.Drawing.Size(150, 38)
        Me.btnRun.TabIndex = 0
        Me.btnRun.Text = "فحص الآن"
        Me.btnRun.UseVisualStyleBackColor = False
        '
        'ArchiveMaintenanceForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(245, Byte), Integer), CType(CType(247, Byte), Integer), CType(CType(246, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(1080, 640)
        Me.Controls.Add(Me.mainPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(900, 580)
        Me.Name = "ArchiveMaintenanceForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "مركز الصيانة والنسخ الاحتياطي"
        Me.headerPanel.ResumeLayout(False)
        Me.mainPanel.ResumeLayout(False)
        Me.mainPanel.PerformLayout()
        CType(Me.dgvDiagnostics, System.ComponentModel.ISupportInitialize).EndInit()
        Me.commandsPanel.ResumeLayout(False)
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblSubtitle As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents mainPanel As System.Windows.Forms.Panel
    Friend WithEvents dgvDiagnostics As System.Windows.Forms.DataGridView
    Friend WithEvents txtOutput As System.Windows.Forms.TextBox
    Friend WithEvents commandsPanel As System.Windows.Forms.Panel
    Friend WithEvents btnClose As System.Windows.Forms.Button
    Friend WithEvents btnOpenData As System.Windows.Forms.Button
    Friend WithEvents btnIndex As System.Windows.Forms.Button
    Friend WithEvents btnInventory As System.Windows.Forms.Button
    Friend WithEvents btnBackup As System.Windows.Forms.Button
    Friend WithEvents btnRun As System.Windows.Forms.Button
End Class
