Namespace Views
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmDs2Se7c432mw
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
        Me.splitMain = New System.Windows.Forms.SplitContainer()
        Me.splitLeft = New System.Windows.Forms.SplitContainer()
        Me.videoView = New Ptz.App.Controls.VideoView()
        Me.alarmPanel = New Ptz.App.Controls.AlarmPanel()
        Me.tabs = New Ptz.App.Controls.ThemedTabControl()
        Me.tabPtz = New System.Windows.Forms.TabPage()
        Me.ptzPanel = New Ptz.App.Controls.PtzPanel()
        Me.tabImage = New System.Windows.Forms.TabPage()
        Me.imagePanel = New Ptz.App.Controls.ImagePanel()
        Me.tabSmart = New System.Windows.Forms.TabPage()
        Me.smartPanel = New Ptz.App.Controls.SmartPanel()
        Me.ssMain = New System.Windows.Forms.StatusStrip()
        Me.lblConn = New System.Windows.Forms.ToolStripStatusLabel()
        Me.lblUser = New System.Windows.Forms.ToolStripStatusLabel()
        Me.lblDevice = New System.Windows.Forms.ToolStripStatusLabel()
        Me.lblFps = New System.Windows.Forms.ToolStripStatusLabel()
        Me.lblReconnect = New System.Windows.Forms.ToolStripStatusLabel()
        Me.lblMessage = New System.Windows.Forms.ToolStripStatusLabel()
        CType(Me.splitMain, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.splitMain.Panel1.SuspendLayout()
        Me.splitMain.Panel2.SuspendLayout()
        Me.splitMain.SuspendLayout()
        CType(Me.splitLeft, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.splitLeft.Panel1.SuspendLayout()
        Me.splitLeft.Panel2.SuspendLayout()
        Me.splitLeft.SuspendLayout()
        Me.tabPtz.SuspendLayout()
        Me.tabImage.SuspendLayout()
        Me.tabSmart.SuspendLayout()
        Me.ssMain.SuspendLayout()
        Me.SuspendLayout()
        '
        'splitMain
        '
        Me.splitMain.Name = "splitMain"
        Me.splitMain.Dock = System.Windows.Forms.DockStyle.Fill
        Me.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2
        Me.splitMain.Location = New System.Drawing.Point(0, 0)
        Me.splitMain.Size = New System.Drawing.Size(1440, 640)
        Me.splitMain.SplitterDistance = 1000
        Me.splitMain.Panel2MinSize = 400
        '
        'splitLeft
        '
        Me.splitLeft.Name = "splitLeft"
        Me.splitLeft.Dock = System.Windows.Forms.DockStyle.Fill
        Me.splitLeft.Orientation = System.Windows.Forms.Orientation.Horizontal
        Me.splitLeft.FixedPanel = System.Windows.Forms.FixedPanel.Panel2
        Me.splitLeft.Location = New System.Drawing.Point(0, 0)
        Me.splitLeft.Size = New System.Drawing.Size(1000, 640)
        Me.splitLeft.SplitterDistance = 430
        Me.splitLeft.Panel2MinSize = 120
        '
        'videoView
        '
        Me.videoView.Name = "videoView"
        Me.videoView.Dock = System.Windows.Forms.DockStyle.Fill
        Me.videoView.Location = New System.Drawing.Point(0, 0)
        Me.videoView.Size = New System.Drawing.Size(1000, 430)
        '
        'alarmPanel
        '
        Me.alarmPanel.Name = "alarmPanel"
        Me.alarmPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.alarmPanel.Location = New System.Drawing.Point(0, 0)
        Me.alarmPanel.Size = New System.Drawing.Size(1000, 206)
        '
        'tabs
        '
        Me.tabs.Name = "tabs"
        Me.tabs.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tabs.Location = New System.Drawing.Point(0, 0)
        Me.tabs.SelectedIndex = 0
        Me.tabs.Size = New System.Drawing.Size(430, 600)
        '
        'tabPtz
        '
        Me.tabPtz.Name = "tabPtz"
        Me.tabPtz.Text = "PTZ"
        Me.tabPtz.UseVisualStyleBackColor = True
        Me.tabPtz.Padding = New System.Windows.Forms.Padding(0, 0, 0, 0)
        '
        'ptzPanel
        '
        Me.ptzPanel.Name = "ptzPanel"
        Me.ptzPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.ptzPanel.Location = New System.Drawing.Point(0, 0)
        Me.ptzPanel.Size = New System.Drawing.Size(422, 562)
        '
        'tabImage
        '
        Me.tabImage.Name = "tabImage"
        Me.tabImage.Text = "الصورة"
        Me.tabImage.UseVisualStyleBackColor = True
        Me.tabImage.Padding = New System.Windows.Forms.Padding(0, 0, 0, 0)
        '
        'imagePanel
        '
        Me.imagePanel.Name = "imagePanel"
        Me.imagePanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.imagePanel.Location = New System.Drawing.Point(0, 0)
        Me.imagePanel.Size = New System.Drawing.Size(422, 562)
        '
        'tabSmart
        '
        Me.tabSmart.Name = "tabSmart"
        Me.tabSmart.Text = "الذكاء"
        Me.tabSmart.UseVisualStyleBackColor = True
        Me.tabSmart.Padding = New System.Windows.Forms.Padding(0, 0, 0, 0)
        '
        'smartPanel
        '
        Me.smartPanel.Name = "smartPanel"
        Me.smartPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.smartPanel.Location = New System.Drawing.Point(0, 0)
        Me.smartPanel.Size = New System.Drawing.Size(422, 562)
        '
        'ssMain
        '
        Me.ssMain.Name = "ssMain"
        Me.ssMain.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.ssMain.Location = New System.Drawing.Point(0, 640)
        Me.ssMain.Size = New System.Drawing.Size(1440, 24)
        Me.ssMain.SizingGrip = False
        '
        'lblConn
        '
        Me.lblConn.Name = "lblConn"
        Me.lblConn.Text = "غير متصل"
        Me.lblConn.ForeColor = System.Drawing.Color.FromArgb(229, 72, 77)
        '
        'lblUser
        '
        Me.lblUser.Name = "lblUser"
        Me.lblUser.Text = "—"
        '
        'lblDevice
        '
        Me.lblDevice.Name = "lblDevice"
        Me.lblDevice.Text = "—"
        '
        'lblFps
        '
        Me.lblFps.Name = "lblFps"
        Me.lblFps.Text = "0 إطار/ث"
        '
        'lblReconnect
        '
        Me.lblReconnect.Name = "lblReconnect"
        Me.lblReconnect.Text = "إعادة الاتصال"
        Me.lblReconnect.IsLink = True
        Me.lblReconnect.Visible = False
        '
        'lblMessage
        '
        Me.lblMessage.Name = "lblMessage"
        Me.lblMessage.Spring = True
        Me.lblMessage.Text = ""
        Me.lblMessage.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Me.Controls.Add(Me.splitMain)
        Me.Controls.Add(Me.ssMain)
        Me.splitMain.Panel1.Controls.Add(Me.splitLeft)
        Me.splitMain.Panel2.Controls.Add(Me.tabs)
        Me.splitLeft.Panel1.Controls.Add(Me.videoView)
        Me.splitLeft.Panel2.Controls.Add(Me.alarmPanel)
        Me.tabs.Controls.Add(Me.tabPtz)
        Me.tabs.Controls.Add(Me.tabImage)
        Me.tabs.Controls.Add(Me.tabSmart)
        Me.tabPtz.Controls.Add(Me.ptzPanel)
        Me.tabImage.Controls.Add(Me.imagePanel)
        Me.tabSmart.Controls.Add(Me.smartPanel)
        Me.ssMain.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.lblConn, Me.lblUser, Me.lblDevice, Me.lblFps, Me.lblReconnect, Me.lblMessage})
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1440, 664)
        Me.Name = "FrmDs2Se7c432mw"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "DS-2SE7C432MW-AEB — لوحة التحكم"
        Me.MinimumSize = New System.Drawing.Size(1100, 640)
        Me.ssMain.ResumeLayout(False)
        Me.ssMain.PerformLayout()
        Me.tabSmart.ResumeLayout(False)
        Me.tabImage.ResumeLayout(False)
        Me.tabPtz.ResumeLayout(False)
        Me.splitLeft.Panel1.ResumeLayout(False)
        Me.splitLeft.Panel2.ResumeLayout(False)
        CType(Me.splitLeft, System.ComponentModel.ISupportInitialize).EndInit()
        Me.splitLeft.ResumeLayout(False)
        Me.splitMain.Panel1.ResumeLayout(False)
        Me.splitMain.Panel2.ResumeLayout(False)
        CType(Me.splitMain, System.ComponentModel.ISupportInitialize).EndInit()
        Me.splitMain.ResumeLayout(False)
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents splitMain As System.Windows.Forms.SplitContainer
    Friend WithEvents splitLeft As System.Windows.Forms.SplitContainer
    Friend WithEvents videoView As Ptz.App.Controls.VideoView
    Friend WithEvents alarmPanel As Ptz.App.Controls.AlarmPanel
    Friend WithEvents tabs As Ptz.App.Controls.ThemedTabControl
    Friend WithEvents tabPtz As System.Windows.Forms.TabPage
    Friend WithEvents ptzPanel As Ptz.App.Controls.PtzPanel
    Friend WithEvents tabImage As System.Windows.Forms.TabPage
    Friend WithEvents imagePanel As Ptz.App.Controls.ImagePanel
    Friend WithEvents tabSmart As System.Windows.Forms.TabPage
    Friend WithEvents smartPanel As Ptz.App.Controls.SmartPanel
    Friend WithEvents ssMain As System.Windows.Forms.StatusStrip
    Friend WithEvents lblConn As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents lblUser As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents lblDevice As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents lblFps As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents lblReconnect As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents lblMessage As System.Windows.Forms.ToolStripStatusLabel

End Class
End Namespace
