Namespace Controls
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class SmartPanel
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
        Me.grpModes = New System.Windows.Forms.GroupBox()
        Me.chkMotion = New System.Windows.Forms.CheckBox()
        Me.chkBehavior = New System.Windows.Forms.CheckBox()
        Me.chkAutoTrack = New System.Windows.Forms.CheckBox()
        Me.chkLearning = New System.Windows.Forms.CheckBox()
        Me.btnRelease = New System.Windows.Forms.Button()
        Me.lblStatus = New System.Windows.Forms.Label()
        Me.grpGeometry = New System.Windows.Forms.GroupBox()
        Me.btnZone = New System.Windows.Forms.Button()
        Me.btnLine = New System.Windows.Forms.Button()
        Me.btnMask = New System.Windows.Forms.Button()
        Me.btnClear = New System.Windows.Forms.Button()
        Me.lblHint = New System.Windows.Forms.Label()
        Me.grpThresholds = New System.Windows.Forms.GroupBox()
        Me.lblLoiter = New System.Windows.Forms.Label()
        Me.nudLoiter = New System.Windows.Forms.NumericUpDown()
        Me.lblRun = New System.Windows.Forms.Label()
        Me.nudRun = New System.Windows.Forms.NumericUpDown()
        Me.lblStationary = New System.Windows.Forms.Label()
        Me.nudStationary = New System.Windows.Forms.NumericUpDown()
        Me.lblGather = New System.Windows.Forms.Label()
        Me.nudGather = New System.Windows.Forms.NumericUpDown()
        Me.lblCooldown = New System.Windows.Forms.Label()
        Me.nudCooldown = New System.Windows.Forms.NumericUpDown()
        Me.btnApplyThresholds = New System.Windows.Forms.Button()
        Me.grpModel = New System.Windows.Forms.GroupBox()
        Me.btnSaveModel = New System.Windows.Forms.Button()
        Me.lblModelHint = New System.Windows.Forms.Label()
        Me.grpModes.SuspendLayout()
        Me.grpGeometry.SuspendLayout()
        Me.grpThresholds.SuspendLayout()
        CType(Me.nudLoiter, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudRun, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudStationary, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudGather, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudCooldown, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpModel.SuspendLayout()
        Me.SuspendLayout()
        '
        'grpModes
        '
        Me.grpModes.Name = "grpModes"
        Me.grpModes.Location = New System.Drawing.Point(8, 8)
        Me.grpModes.Size = New System.Drawing.Size(384, 190)
        Me.grpModes.Text = "الأنظمة الذكية"
        '
        'chkMotion
        '
        Me.chkMotion.Name = "chkMotion"
        Me.chkMotion.AutoSize = True
        Me.chkMotion.Location = New System.Drawing.Point(14, 28)
        Me.chkMotion.Text = "كشف الحركة (فصل الخلفية + تتبع الأجسام)"
        Me.chkMotion.UseVisualStyleBackColor = True
        Me.chkMotion.Checked = True
        '
        'chkBehavior
        '
        Me.chkBehavior.Name = "chkBehavior"
        Me.chkBehavior.AutoSize = True
        Me.chkBehavior.Location = New System.Drawing.Point(14, 56)
        Me.chkBehavior.Text = "تحليل السلوك (اقتحام/عبور/تسكّع/ركض/تجمّع)"
        Me.chkBehavior.UseVisualStyleBackColor = True
        Me.chkBehavior.Checked = True
        '
        'chkAutoTrack
        '
        Me.chkAutoTrack.Name = "chkAutoTrack"
        Me.chkAutoTrack.AutoSize = True
        Me.chkAutoTrack.Location = New System.Drawing.Point(14, 84)
        Me.chkAutoTrack.Text = "التتبع الآلي للأجسام المتحركة"
        Me.chkAutoTrack.UseVisualStyleBackColor = True
        Me.chkAutoTrack.Checked = False
        '
        'chkLearning
        '
        Me.chkLearning.Name = "chkLearning"
        Me.chkLearning.AutoSize = True
        Me.chkLearning.Location = New System.Drawing.Point(14, 112)
        Me.chkLearning.Text = "وضع تعلّم النمط الطبيعي للحركة"
        Me.chkLearning.UseVisualStyleBackColor = True
        Me.chkLearning.Checked = False
        '
        'btnRelease
        '
        Me.btnRelease.Name = "btnRelease"
        Me.btnRelease.Location = New System.Drawing.Point(14, 142)
        Me.btnRelease.Size = New System.Drawing.Size(170, 28)
        Me.btnRelease.Text = "إلغاء قفل الهدف"
        Me.btnRelease.UseVisualStyleBackColor = True
        '
        'lblStatus
        '
        Me.lblStatus.Name = "lblStatus"
        Me.lblStatus.AutoSize = True
        Me.lblStatus.Location = New System.Drawing.Point(196, 148)
        Me.lblStatus.Text = "—"
        Me.lblStatus.Tag = "muted"
        '
        'grpGeometry
        '
        Me.grpGeometry.Name = "grpGeometry"
        Me.grpGeometry.Location = New System.Drawing.Point(8, 206)
        Me.grpGeometry.Size = New System.Drawing.Size(384, 150)
        Me.grpGeometry.Text = "المناطق والخطوط"
        '
        'btnZone
        '
        Me.btnZone.Name = "btnZone"
        Me.btnZone.Location = New System.Drawing.Point(14, 26)
        Me.btnZone.Size = New System.Drawing.Size(170, 28)
        Me.btnZone.Text = "منطقة محظورة"
        Me.btnZone.UseVisualStyleBackColor = True
        '
        'btnLine
        '
        Me.btnLine.Name = "btnLine"
        Me.btnLine.Location = New System.Drawing.Point(196, 26)
        Me.btnLine.Size = New System.Drawing.Size(170, 28)
        Me.btnLine.Text = "خط عبور"
        Me.btnLine.UseVisualStyleBackColor = True
        '
        'btnMask
        '
        Me.btnMask.Name = "btnMask"
        Me.btnMask.Location = New System.Drawing.Point(14, 60)
        Me.btnMask.Size = New System.Drawing.Size(170, 28)
        Me.btnMask.Text = "منطقة إخفاء (تجاهل الحركة)"
        Me.btnMask.UseVisualStyleBackColor = True
        '
        'btnClear
        '
        Me.btnClear.Name = "btnClear"
        Me.btnClear.Location = New System.Drawing.Point(196, 60)
        Me.btnClear.Size = New System.Drawing.Size(170, 28)
        Me.btnClear.Text = "مسح كل المناطق"
        Me.btnClear.UseVisualStyleBackColor = True
        '
        'lblHint
        '
        Me.lblHint.Name = "lblHint"
        Me.lblHint.AutoSize = True
        Me.lblHint.Location = New System.Drawing.Point(14, 100)
        Me.lblHint.Text = "نقر أيمن على الفيديو = قفل هدف للتتبع · سحب = تكبير على منطقة"
        Me.lblHint.Tag = "muted"
        Me.lblHint.MaximumSize = New System.Drawing.Size(360, 0)
        '
        'grpThresholds
        '
        Me.grpThresholds.Name = "grpThresholds"
        Me.grpThresholds.Location = New System.Drawing.Point(8, 364)
        Me.grpThresholds.Size = New System.Drawing.Size(384, 224)
        Me.grpThresholds.Text = "عتبات تحليل السلوك"
        '
        'lblLoiter
        '
        Me.lblLoiter.Name = "lblLoiter"
        Me.lblLoiter.AutoSize = True
        Me.lblLoiter.Location = New System.Drawing.Point(14, 32)
        Me.lblLoiter.Text = "التسكّع (ثانية)"
        '
        'nudLoiter
        '
        Me.nudLoiter.Name = "nudLoiter"
        Me.nudLoiter.Location = New System.Drawing.Point(250, 28)
        Me.nudLoiter.Size = New System.Drawing.Size(110, 23)
        Me.nudLoiter.Minimum = New Decimal(New Integer() {5, 0, 0, 0})
        Me.nudLoiter.Maximum = New Decimal(New Integer() {600, 0, 0, 0})
        Me.nudLoiter.Value = New Decimal(New Integer() {20, 0, 0, 0})
        '
        'lblRun
        '
        Me.lblRun.Name = "lblRun"
        Me.lblRun.AutoSize = True
        Me.lblRun.Location = New System.Drawing.Point(14, 66)
        Me.lblRun.Text = "سرعة الركض (إطار/ث)"
        '
        'nudRun
        '
        Me.nudRun.Name = "nudRun"
        Me.nudRun.Location = New System.Drawing.Point(250, 62)
        Me.nudRun.Size = New System.Drawing.Size(110, 23)
        Me.nudRun.Minimum = New Decimal(New Integer() {10, 0, 0, 131072})
        Me.nudRun.Maximum = New Decimal(New Integer() {3, 0, 0, 0})
        Me.nudRun.Value = New Decimal(New Integer() {45, 0, 0, 131072})
        Me.nudRun.DecimalPlaces = 2
        Me.nudRun.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        '
        'lblStationary
        '
        Me.lblStationary.Name = "lblStationary"
        Me.lblStationary.AutoSize = True
        Me.lblStationary.Location = New System.Drawing.Point(14, 100)
        Me.lblStationary.Text = "السكون (ثانية)"
        '
        'nudStationary
        '
        Me.nudStationary.Name = "nudStationary"
        Me.nudStationary.Location = New System.Drawing.Point(250, 96)
        Me.nudStationary.Size = New System.Drawing.Size(110, 23)
        Me.nudStationary.Minimum = New Decimal(New Integer() {10, 0, 0, 0})
        Me.nudStationary.Maximum = New Decimal(New Integer() {600, 0, 0, 0})
        Me.nudStationary.Value = New Decimal(New Integer() {45, 0, 0, 0})
        '
        'lblGather
        '
        Me.lblGather.Name = "lblGather"
        Me.lblGather.AutoSize = True
        Me.lblGather.Location = New System.Drawing.Point(14, 134)
        Me.lblGather.Text = "عدد التجمّع"
        '
        'nudGather
        '
        Me.nudGather.Name = "nudGather"
        Me.nudGather.Location = New System.Drawing.Point(250, 130)
        Me.nudGather.Size = New System.Drawing.Size(110, 23)
        Me.nudGather.Minimum = New Decimal(New Integer() {2, 0, 0, 0})
        Me.nudGather.Maximum = New Decimal(New Integer() {20, 0, 0, 0})
        Me.nudGather.Value = New Decimal(New Integer() {4, 0, 0, 0})
        '
        'lblCooldown
        '
        Me.lblCooldown.Name = "lblCooldown"
        Me.lblCooldown.AutoSize = True
        Me.lblCooldown.Location = New System.Drawing.Point(14, 168)
        Me.lblCooldown.Text = "فاصل تكرار الإنذار (ث)"
        '
        'nudCooldown
        '
        Me.nudCooldown.Name = "nudCooldown"
        Me.nudCooldown.Location = New System.Drawing.Point(250, 164)
        Me.nudCooldown.Size = New System.Drawing.Size(110, 23)
        Me.nudCooldown.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudCooldown.Maximum = New Decimal(New Integer() {120, 0, 0, 0})
        Me.nudCooldown.Value = New Decimal(New Integer() {10, 0, 0, 0})
        '
        'btnApplyThresholds
        '
        Me.btnApplyThresholds.Name = "btnApplyThresholds"
        Me.btnApplyThresholds.Location = New System.Drawing.Point(250, 190)
        Me.btnApplyThresholds.Size = New System.Drawing.Size(110, 28)
        Me.btnApplyThresholds.Text = "تطبيق العتبات"
        Me.btnApplyThresholds.UseVisualStyleBackColor = True
        '
        'grpModel
        '
        Me.grpModel.Name = "grpModel"
        Me.grpModel.Location = New System.Drawing.Point(8, 596)
        Me.grpModel.Size = New System.Drawing.Size(384, 88)
        Me.grpModel.Text = "نموذج الشذوذ (تعلّم آلي)"
        '
        'btnSaveModel
        '
        Me.btnSaveModel.Name = "btnSaveModel"
        Me.btnSaveModel.Location = New System.Drawing.Point(14, 28)
        Me.btnSaveModel.Size = New System.Drawing.Size(170, 28)
        Me.btnSaveModel.Text = "حفظ النموذج المتعلَّم"
        Me.btnSaveModel.UseVisualStyleBackColor = True
        '
        'lblModelHint
        '
        Me.lblModelHint.Name = "lblModelHint"
        Me.lblModelHint.AutoSize = True
        Me.lblModelHint.Location = New System.Drawing.Point(14, 62)
        Me.lblModelHint.Text = "يتعلّم السرعة الطبيعية لكل منطقة ثم ينذر بالانحراف."
        Me.lblModelHint.Tag = "muted"
        Me.Controls.Add(Me.grpModes)
        Me.Controls.Add(Me.grpGeometry)
        Me.Controls.Add(Me.grpThresholds)
        Me.Controls.Add(Me.grpModel)
        Me.grpModes.Controls.Add(Me.chkMotion)
        Me.grpModes.Controls.Add(Me.chkBehavior)
        Me.grpModes.Controls.Add(Me.chkAutoTrack)
        Me.grpModes.Controls.Add(Me.chkLearning)
        Me.grpModes.Controls.Add(Me.btnRelease)
        Me.grpModes.Controls.Add(Me.lblStatus)
        Me.grpGeometry.Controls.Add(Me.btnZone)
        Me.grpGeometry.Controls.Add(Me.btnLine)
        Me.grpGeometry.Controls.Add(Me.btnMask)
        Me.grpGeometry.Controls.Add(Me.btnClear)
        Me.grpGeometry.Controls.Add(Me.lblHint)
        Me.grpThresholds.Controls.Add(Me.lblLoiter)
        Me.grpThresholds.Controls.Add(Me.nudLoiter)
        Me.grpThresholds.Controls.Add(Me.lblRun)
        Me.grpThresholds.Controls.Add(Me.nudRun)
        Me.grpThresholds.Controls.Add(Me.lblStationary)
        Me.grpThresholds.Controls.Add(Me.nudStationary)
        Me.grpThresholds.Controls.Add(Me.lblGather)
        Me.grpThresholds.Controls.Add(Me.nudGather)
        Me.grpThresholds.Controls.Add(Me.lblCooldown)
        Me.grpThresholds.Controls.Add(Me.nudCooldown)
        Me.grpThresholds.Controls.Add(Me.btnApplyThresholds)
        Me.grpModel.Controls.Add(Me.btnSaveModel)
        Me.grpModel.Controls.Add(Me.lblModelHint)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoScroll = True
        Me.Name = "SmartPanel"
        Me.Size = New System.Drawing.Size(400, 720)
        Me.grpModel.ResumeLayout(False)
        Me.grpModel.PerformLayout()
        CType(Me.nudCooldown, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudGather, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudStationary, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudRun, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudLoiter, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpThresholds.ResumeLayout(False)
        Me.grpThresholds.PerformLayout()
        Me.grpGeometry.ResumeLayout(False)
        Me.grpGeometry.PerformLayout()
        Me.grpModes.ResumeLayout(False)
        Me.grpModes.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents grpModes As System.Windows.Forms.GroupBox
    Friend WithEvents chkMotion As System.Windows.Forms.CheckBox
    Friend WithEvents chkBehavior As System.Windows.Forms.CheckBox
    Friend WithEvents chkAutoTrack As System.Windows.Forms.CheckBox
    Friend WithEvents chkLearning As System.Windows.Forms.CheckBox
    Friend WithEvents btnRelease As System.Windows.Forms.Button
    Friend WithEvents lblStatus As System.Windows.Forms.Label
    Friend WithEvents grpGeometry As System.Windows.Forms.GroupBox
    Friend WithEvents btnZone As System.Windows.Forms.Button
    Friend WithEvents btnLine As System.Windows.Forms.Button
    Friend WithEvents btnMask As System.Windows.Forms.Button
    Friend WithEvents btnClear As System.Windows.Forms.Button
    Friend WithEvents lblHint As System.Windows.Forms.Label
    Friend WithEvents grpThresholds As System.Windows.Forms.GroupBox
    Friend WithEvents lblLoiter As System.Windows.Forms.Label
    Friend WithEvents nudLoiter As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblRun As System.Windows.Forms.Label
    Friend WithEvents nudRun As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblStationary As System.Windows.Forms.Label
    Friend WithEvents nudStationary As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblGather As System.Windows.Forms.Label
    Friend WithEvents nudGather As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblCooldown As System.Windows.Forms.Label
    Friend WithEvents nudCooldown As System.Windows.Forms.NumericUpDown
    Friend WithEvents btnApplyThresholds As System.Windows.Forms.Button
    Friend WithEvents grpModel As System.Windows.Forms.GroupBox
    Friend WithEvents btnSaveModel As System.Windows.Forms.Button
    Friend WithEvents lblModelHint As System.Windows.Forms.Label

End Class
End Namespace
