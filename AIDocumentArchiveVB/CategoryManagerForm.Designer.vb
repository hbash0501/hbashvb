
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class CategoryManagerForm
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
        Me.dgvCategories = New System.Windows.Forms.DataGridView()
        Me.editorPanel = New System.Windows.Forms.Panel()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.btnAdd = New System.Windows.Forms.Button()
        Me.txtSub = New System.Windows.Forms.TextBox()
        Me.lblSub = New System.Windows.Forms.Label()
        Me.txtGeneral = New System.Windows.Forms.TextBox()
        Me.lblGeneral = New System.Windows.Forms.Label()
        Me.headerPanel.SuspendLayout()
        CType(Me.dgvCategories, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.editorPanel.SuspendLayout()
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
        Me.lblTitle.Text = "إدارة التصنيفات"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'dgvCategories
        '
        Me.dgvCategories.AllowUserToAddRows = False
        Me.dgvCategories.AllowUserToDeleteRows = False
        Me.dgvCategories.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.dgvCategories.BackgroundColor = System.Drawing.Color.White
        Me.dgvCategories.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.dgvCategories.ColumnHeadersHeight = 34
        Me.dgvCategories.Dock = System.Windows.Forms.DockStyle.Fill
        Me.dgvCategories.Location = New System.Drawing.Point(0, 74)
        Me.dgvCategories.MultiSelect = False
        Me.dgvCategories.Name = "dgvCategories"
        Me.dgvCategories.ReadOnly = True
        Me.dgvCategories.RowHeadersVisible = False
        Me.dgvCategories.RowTemplate.Height = 32
        Me.dgvCategories.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.dgvCategories.Size = New System.Drawing.Size(760, 316)
        Me.dgvCategories.TabIndex = 1
        '
        'editorPanel
        '
        Me.editorPanel.BackColor = System.Drawing.Color.White
        Me.editorPanel.Controls.Add(Me.btnClose)
        Me.editorPanel.Controls.Add(Me.btnAdd)
        Me.editorPanel.Controls.Add(Me.txtSub)
        Me.editorPanel.Controls.Add(Me.lblSub)
        Me.editorPanel.Controls.Add(Me.txtGeneral)
        Me.editorPanel.Controls.Add(Me.lblGeneral)
        Me.editorPanel.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.editorPanel.Location = New System.Drawing.Point(0, 390)
        Me.editorPanel.Name = "editorPanel"
        Me.editorPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.editorPanel.Size = New System.Drawing.Size(760, 150)
        Me.editorPanel.TabIndex = 2
        '
        'btnClose
        '
        Me.btnClose.BackColor = System.Drawing.Color.FromArgb(CType(CType(238, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(239, Byte), Integer))
        Me.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.btnClose.FlatAppearance.BorderSize = 0
        Me.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnClose.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnClose.Location = New System.Drawing.Point(18, 92)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(130, 38)
        Me.btnClose.TabIndex = 5
        Me.btnClose.Text = "إغلاق"
        Me.btnClose.UseVisualStyleBackColor = False
        '
        'btnAdd
        '
        Me.btnAdd.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.btnAdd.FlatAppearance.BorderSize = 0
        Me.btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAdd.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnAdd.Location = New System.Drawing.Point(154, 92)
        Me.btnAdd.Name = "btnAdd"
        Me.btnAdd.Size = New System.Drawing.Size(150, 38)
        Me.btnAdd.TabIndex = 4
        Me.btnAdd.Text = "إضافة التصنيف"
        Me.btnAdd.UseVisualStyleBackColor = False
        '
        'txtSub
        '
        Me.txtSub.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.txtSub.Location = New System.Drawing.Point(18, 48)
        Me.txtSub.Name = "txtSub"
        Me.txtSub.Size = New System.Drawing.Size(330, 26)
        Me.txtSub.TabIndex = 3
        '
        'lblSub
        '
        Me.lblSub.Location = New System.Drawing.Point(18, 23)
        Me.lblSub.Name = "lblSub"
        Me.lblSub.Size = New System.Drawing.Size(330, 22)
        Me.lblSub.TabIndex = 2
        Me.lblSub.Text = "التصنيف الثانوي"
        '
        'txtGeneral
        '
        Me.txtGeneral.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.txtGeneral.Location = New System.Drawing.Point(384, 48)
        Me.txtGeneral.Name = "txtGeneral"
        Me.txtGeneral.Size = New System.Drawing.Size(358, 26)
        Me.txtGeneral.TabIndex = 1
        '
        'lblGeneral
        '
        Me.lblGeneral.Location = New System.Drawing.Point(384, 23)
        Me.lblGeneral.Name = "lblGeneral"
        Me.lblGeneral.Size = New System.Drawing.Size(358, 22)
        Me.lblGeneral.TabIndex = 0
        Me.lblGeneral.Text = "التصنيف العام"
        '
        'CategoryManagerForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(760, 540)
        Me.Controls.Add(Me.dgvCategories)
        Me.Controls.Add(Me.editorPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimizeBox = False
        Me.Name = "CategoryManagerForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "إدارة التصنيفات"
        Me.headerPanel.ResumeLayout(False)
        CType(Me.dgvCategories, System.ComponentModel.ISupportInitialize).EndInit()
        Me.editorPanel.ResumeLayout(False)
        Me.editorPanel.PerformLayout()
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents dgvCategories As System.Windows.Forms.DataGridView
    Friend WithEvents editorPanel As System.Windows.Forms.Panel
    Friend WithEvents btnClose As System.Windows.Forms.Button
    Friend WithEvents btnAdd As System.Windows.Forms.Button
    Friend WithEvents txtSub As System.Windows.Forms.TextBox
    Friend WithEvents lblSub As System.Windows.Forms.Label
    Friend WithEvents txtGeneral As System.Windows.Forms.TextBox
    Friend WithEvents lblGeneral As System.Windows.Forms.Label
End Class
