Imports System.Drawing
Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class ProgressDialogForm
    Inherits Form

    Private components As System.ComponentModel.IContainer
    Friend WithEvents panelTop As Panel
    Friend WithEvents lblTitle As Label
    Friend WithEvents lblMessage As Label
    Friend WithEvents progressMain As ProgressBar
    Friend WithEvents lblPercent As Label
    Friend WithEvents lblHint As Label
    Friend WithEvents btnCancel As Button
    Friend WithEvents timerElapsed As Timer

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then components.Dispose()
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.panelTop = New System.Windows.Forms.Panel()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblMessage = New System.Windows.Forms.Label()
        Me.progressMain = New System.Windows.Forms.ProgressBar()
        Me.lblPercent = New System.Windows.Forms.Label()
        Me.lblHint = New System.Windows.Forms.Label()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.timerElapsed = New System.Windows.Forms.Timer(Me.components)
        Me.panelTop.SuspendLayout()
        Me.SuspendLayout()
        '
        'panelTop
        '
        Me.panelTop.BackColor = System.Drawing.Color.FromArgb(23, 58, 54)
        Me.panelTop.Controls.Add(Me.lblTitle)
        Me.panelTop.Dock = System.Windows.Forms.DockStyle.Top
        Me.panelTop.Location = New System.Drawing.Point(0, 0)
        Me.panelTop.Name = "panelTop"
        Me.panelTop.Size = New System.Drawing.Size(560, 62)
        Me.panelTop.TabIndex = 0
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 14.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(0, 0)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Padding = New System.Windows.Forms.Padding(18, 0, 18, 0)
        Me.lblTitle.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.lblTitle.Size = New System.Drawing.Size(560, 62)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "جاري تنفيذ العملية"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'lblMessage
        '
        Me.lblMessage.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblMessage.Font = New System.Drawing.Font("Tahoma", 10.0!, System.Drawing.FontStyle.Bold)
        Me.lblMessage.ForeColor = System.Drawing.Color.FromArgb(38, 48, 45)
        Me.lblMessage.Location = New System.Drawing.Point(24, 82)
        Me.lblMessage.Name = "lblMessage"
        Me.lblMessage.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.lblMessage.Size = New System.Drawing.Size(512, 54)
        Me.lblMessage.TabIndex = 1
        Me.lblMessage.Text = "يرجى الانتظار..."
        Me.lblMessage.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'progressMain
        '
        Me.progressMain.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.progressMain.Location = New System.Drawing.Point(24, 148)
        Me.progressMain.Name = "progressMain"
        Me.progressMain.Size = New System.Drawing.Size(512, 28)
        Me.progressMain.Style = System.Windows.Forms.ProgressBarStyle.Marquee
        Me.progressMain.TabIndex = 2
        '
        'lblPercent
        '
        Me.lblPercent.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblPercent.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.lblPercent.ForeColor = System.Drawing.Color.FromArgb(84, 98, 94)
        Me.lblPercent.Location = New System.Drawing.Point(24, 185)
        Me.lblPercent.Name = "lblPercent"
        Me.lblPercent.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.lblPercent.Size = New System.Drawing.Size(512, 25)
        Me.lblPercent.TabIndex = 3
        Me.lblPercent.Text = "العملية تعمل في الخلفية، والواجهة لم تتوقف."
        Me.lblPercent.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'lblHint
        '
        Me.lblHint.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblHint.Font = New System.Drawing.Font("Tahoma", 8.5!)
        Me.lblHint.ForeColor = System.Drawing.Color.FromArgb(100, 110, 106)
        Me.lblHint.Location = New System.Drawing.Point(24, 224)
        Me.lblHint.Name = "lblHint"
        Me.lblHint.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.lblHint.Size = New System.Drawing.Size(512, 39)
        Me.lblHint.TabIndex = 4
        Me.lblHint.Text = "لا تغلق البرنامج أثناء الحفظ أو OCR. يمكنك طلب الإلغاء في العمليات التي تدعم ذلك."
        Me.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'btnCancel
        '
        Me.btnCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnCancel.BackColor = System.Drawing.Color.White
        Me.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnCancel.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnCancel.ForeColor = System.Drawing.Color.FromArgb(23, 58, 54)
        Me.btnCancel.Location = New System.Drawing.Point(24, 276)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(130, 34)
        Me.btnCancel.TabIndex = 5
        Me.btnCancel.Text = "طلب الإلغاء"
        Me.btnCancel.UseVisualStyleBackColor = False
        '
        'timerElapsed
        '
        Me.timerElapsed.Interval = 1000
        '
        'ProgressDialogForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.ClientSize = New System.Drawing.Size(560, 330)
        Me.ControlBox = False
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.lblHint)
        Me.Controls.Add(Me.lblPercent)
        Me.Controls.Add(Me.progressMain)
        Me.Controls.Add(Me.lblMessage)
        Me.Controls.Add(Me.panelTop)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "ProgressDialogForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "جاري التنفيذ"
        Me.panelTop.ResumeLayout(False)
        Me.ResumeLayout(False)
    End Sub
End Class
