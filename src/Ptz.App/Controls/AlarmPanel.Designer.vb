Namespace Controls
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class AlarmPanel
    Inherits System.Windows.Forms.UserControl

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
        Me.lvAlarms = New Ptz.App.Controls.BufferedListView()
        Me.pnlBottom = New System.Windows.Forms.Panel()
        Me.btnAck = New System.Windows.Forms.Button()
        Me.btnClearList = New System.Windows.Forms.Button()
        Me.lblCount = New System.Windows.Forms.Label()
        Me.pnlBottom.SuspendLayout()
        Me.SuspendLayout()
        '
        'lvAlarms
        '
        Me.lvAlarms.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lvAlarms.Location = New System.Drawing.Point(0, 0)
        Me.lvAlarms.Name = "lvAlarms"
        Me.lvAlarms.Size = New System.Drawing.Size(900, 180)
        Me.lvAlarms.TabIndex = 0
        Me.lvAlarms.UseCompatibleStateImageBehavior = False
        Me.lvAlarms.View = System.Windows.Forms.View.Details
        Me.lvAlarms.FullRowSelect = True
        '
        'pnlBottom
        '
        Me.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.pnlBottom.Location = New System.Drawing.Point(0, 180)
        Me.pnlBottom.Size = New System.Drawing.Size(900, 40)
        Me.pnlBottom.Name = "pnlBottom"
        '
        'btnAck
        '
        Me.btnAck.Name = "btnAck"
        Me.btnAck.Location = New System.Drawing.Point(6, 6)
        Me.btnAck.Size = New System.Drawing.Size(140, 28)
        Me.btnAck.Text = "إقرار بالمحدد"
        Me.btnAck.UseVisualStyleBackColor = True
        '
        'btnClearList
        '
        Me.btnClearList.Name = "btnClearList"
        Me.btnClearList.Location = New System.Drawing.Point(152, 6)
        Me.btnClearList.Size = New System.Drawing.Size(140, 28)
        Me.btnClearList.Text = "تفريغ القائمة"
        Me.btnClearList.UseVisualStyleBackColor = True
        '
        'lblCount
        '
        Me.lblCount.Name = "lblCount"
        Me.lblCount.AutoSize = True
        Me.lblCount.Location = New System.Drawing.Point(306, 12)
        Me.lblCount.Text = "الإنذارات: 0"
        Me.lblCount.Tag = "muted"
        Me.Controls.Add(Me.lvAlarms)
        Me.Controls.Add(Me.pnlBottom)
        Me.pnlBottom.Controls.Add(Me.btnAck)
        Me.pnlBottom.Controls.Add(Me.btnClearList)
        Me.pnlBottom.Controls.Add(Me.lblCount)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoScroll = True
        Me.Name = "AlarmPanel"
        Me.Size = New System.Drawing.Size(900, 220)
        Me.pnlBottom.ResumeLayout(False)
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lvAlarms As Ptz.App.Controls.BufferedListView
    Friend WithEvents pnlBottom As System.Windows.Forms.Panel
    Friend WithEvents btnAck As System.Windows.Forms.Button
    Friend WithEvents btnClearList As System.Windows.Forms.Button
    Friend WithEvents lblCount As System.Windows.Forms.Label

End Class
End Namespace
