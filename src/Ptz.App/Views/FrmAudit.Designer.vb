Namespace Views
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmAudit
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
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

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.lvAudit = New Ptz.App.Controls.BufferedListView()
        Me.pnlTop = New System.Windows.Forms.Panel()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.btnVerify = New System.Windows.Forms.Button()
        Me.lblVerify = New System.Windows.Forms.Label()
        Me.pnlTop.SuspendLayout()
        Me.SuspendLayout()
        '
        'lvAudit
        '
        Me.lvAudit.Name = "lvAudit"
        Me.lvAudit.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lvAudit.Location = New System.Drawing.Point(0, 44)
        Me.lvAudit.Size = New System.Drawing.Size(1080, 576)
        Me.lvAudit.UseCompatibleStateImageBehavior = False
        Me.lvAudit.View = System.Windows.Forms.View.Details
        Me.lvAudit.FullRowSelect = True
        '
        'pnlTop
        '
        Me.pnlTop.Name = "pnlTop"
        Me.pnlTop.Dock = System.Windows.Forms.DockStyle.Top
        Me.pnlTop.Size = New System.Drawing.Size(1080, 44)
        '
        'btnRefresh
        '
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Location = New System.Drawing.Point(8, 8)
        Me.btnRefresh.Size = New System.Drawing.Size(100, 28)
        Me.btnRefresh.Text = "تحديث"
        Me.btnRefresh.UseVisualStyleBackColor = True
        '
        'btnVerify
        '
        Me.btnVerify.Name = "btnVerify"
        Me.btnVerify.Location = New System.Drawing.Point(116, 8)
        Me.btnVerify.Size = New System.Drawing.Size(200, 28)
        Me.btnVerify.Text = "التحقق من سلامة السلسلة"
        Me.btnVerify.UseVisualStyleBackColor = True
        '
        'lblVerify
        '
        Me.lblVerify.Name = "lblVerify"
        Me.lblVerify.AutoSize = True
        Me.lblVerify.Location = New System.Drawing.Point(328, 14)
        Me.lblVerify.Text = ""
        Me.Controls.Add(Me.lvAudit)
        Me.Controls.Add(Me.pnlTop)
        Me.pnlTop.Controls.Add(Me.btnRefresh)
        Me.pnlTop.Controls.Add(Me.btnVerify)
        Me.pnlTop.Controls.Add(Me.lblVerify)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1080, 620)
        Me.Name = "FrmAudit"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "سجل التدقيق"
        Me.MinimumSize = New System.Drawing.Size(900, 400)
        Me.pnlTop.ResumeLayout(False)
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lvAudit As Ptz.App.Controls.BufferedListView
    Friend WithEvents pnlTop As System.Windows.Forms.Panel
    Friend WithEvents btnRefresh As System.Windows.Forms.Button
    Friend WithEvents btnVerify As System.Windows.Forms.Button
    Friend WithEvents lblVerify As System.Windows.Forms.Label

End Class
End Namespace
