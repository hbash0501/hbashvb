Namespace Controls
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class PtzPanel
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
        Me.grpMove = New System.Windows.Forms.GroupBox()
        Me.joystick = New Ptz.App.Controls.JoystickControl()
        Me.lblSpeedCaption = New System.Windows.Forms.Label()
        Me.trkSpeed = New System.Windows.Forms.TrackBar()
        Me.lblSpeedValue = New System.Windows.Forms.Label()
        Me.btnZoomIn = New System.Windows.Forms.Button()
        Me.btnZoomOut = New System.Windows.Forms.Button()
        Me.btnHome = New System.Windows.Forms.Button()
        Me.btnSetHome = New System.Windows.Forms.Button()
        Me.lblPosition = New System.Windows.Forms.Label()
        Me.grpPresets = New System.Windows.Forms.GroupBox()
        Me.lstPresets = New System.Windows.Forms.ListBox()
        Me.lblPresetId = New System.Windows.Forms.Label()
        Me.nudPresetId = New System.Windows.Forms.NumericUpDown()
        Me.lblPresetName = New System.Windows.Forms.Label()
        Me.txtPresetName = New System.Windows.Forms.TextBox()
        Me.btnPresetGo = New System.Windows.Forms.Button()
        Me.btnPresetSave = New System.Windows.Forms.Button()
        Me.btnPresetDelete = New System.Windows.Forms.Button()
        Me.grpTours = New System.Windows.Forms.GroupBox()
        Me.lblPatrol = New System.Windows.Forms.Label()
        Me.nudPatrol = New System.Windows.Forms.NumericUpDown()
        Me.btnPatrolStart = New System.Windows.Forms.Button()
        Me.btnPatrolStop = New System.Windows.Forms.Button()
        Me.lblPattern = New System.Windows.Forms.Label()
        Me.nudPattern = New System.Windows.Forms.NumericUpDown()
        Me.btnPatternRec = New System.Windows.Forms.Button()
        Me.btnPatternRecStop = New System.Windows.Forms.Button()
        Me.btnPatternRun = New System.Windows.Forms.Button()
        Me.btnPatternStop = New System.Windows.Forms.Button()
        Me.grpAbs = New System.Windows.Forms.GroupBox()
        Me.lblAz = New System.Windows.Forms.Label()
        Me.nudAz = New System.Windows.Forms.NumericUpDown()
        Me.lblEl = New System.Windows.Forms.Label()
        Me.nudEl = New System.Windows.Forms.NumericUpDown()
        Me.lblZm = New System.Windows.Forms.Label()
        Me.nudZm = New System.Windows.Forms.NumericUpDown()
        Me.btnGoAbs = New System.Windows.Forms.Button()
        Me.grpMove.SuspendLayout()
        CType(Me.trkSpeed, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpPresets.SuspendLayout()
        CType(Me.nudPresetId, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpTours.SuspendLayout()
        CType(Me.nudPatrol, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudPattern, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpAbs.SuspendLayout()
        CType(Me.nudAz, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudEl, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudZm, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'grpMove
        '
        Me.grpMove.Name = "grpMove"
        Me.grpMove.Location = New System.Drawing.Point(8, 8)
        Me.grpMove.Size = New System.Drawing.Size(384, 304)
        Me.grpMove.Text = "التحكم بالحركة"
        '
        'joystick
        '
        Me.joystick.Location = New System.Drawing.Point(12, 26)
        Me.joystick.Size = New System.Drawing.Size(190, 190)
        Me.joystick.Name = "joystick"
        '
        'lblSpeedCaption
        '
        Me.lblSpeedCaption.Name = "lblSpeedCaption"
        Me.lblSpeedCaption.AutoSize = True
        Me.lblSpeedCaption.Location = New System.Drawing.Point(214, 26)
        Me.lblSpeedCaption.Text = "السرعة"
        '
        'trkSpeed
        '
        Me.trkSpeed.Name = "trkSpeed"
        Me.trkSpeed.Location = New System.Drawing.Point(214, 46)
        Me.trkSpeed.Size = New System.Drawing.Size(45, 170)
        Me.trkSpeed.Orientation = System.Windows.Forms.Orientation.Vertical
        Me.trkSpeed.Minimum = 1
        Me.trkSpeed.Maximum = 100
        Me.trkSpeed.Value = 50
        Me.trkSpeed.TickFrequency = 10
        '
        'lblSpeedValue
        '
        Me.lblSpeedValue.Name = "lblSpeedValue"
        Me.lblSpeedValue.AutoSize = True
        Me.lblSpeedValue.Location = New System.Drawing.Point(216, 220)
        Me.lblSpeedValue.Text = "50"
        '
        'btnZoomIn
        '
        Me.btnZoomIn.Name = "btnZoomIn"
        Me.btnZoomIn.Location = New System.Drawing.Point(272, 46)
        Me.btnZoomIn.Size = New System.Drawing.Size(100, 48)
        Me.btnZoomIn.Text = "تكبير ＋"
        Me.btnZoomIn.UseVisualStyleBackColor = True
        '
        'btnZoomOut
        '
        Me.btnZoomOut.Name = "btnZoomOut"
        Me.btnZoomOut.Location = New System.Drawing.Point(272, 100)
        Me.btnZoomOut.Size = New System.Drawing.Size(100, 48)
        Me.btnZoomOut.Text = "تصغير －"
        Me.btnZoomOut.UseVisualStyleBackColor = True
        '
        'btnHome
        '
        Me.btnHome.Name = "btnHome"
        Me.btnHome.Location = New System.Drawing.Point(12, 232)
        Me.btnHome.Size = New System.Drawing.Size(120, 30)
        Me.btnHome.Text = "الرئيسية"
        Me.btnHome.UseVisualStyleBackColor = True
        '
        'btnSetHome
        '
        Me.btnSetHome.Name = "btnSetHome"
        Me.btnSetHome.Location = New System.Drawing.Point(140, 232)
        Me.btnSetHome.Size = New System.Drawing.Size(130, 30)
        Me.btnSetHome.Text = "تعيين كرئيسية"
        Me.btnSetHome.UseVisualStyleBackColor = True
        '
        'lblPosition
        '
        Me.lblPosition.Name = "lblPosition"
        Me.lblPosition.AutoSize = True
        Me.lblPosition.Location = New System.Drawing.Point(12, 274)
        Me.lblPosition.Text = "الموضع: —"
        Me.lblPosition.Tag = "muted"
        '
        'grpPresets
        '
        Me.grpPresets.Name = "grpPresets"
        Me.grpPresets.Location = New System.Drawing.Point(8, 320)
        Me.grpPresets.Size = New System.Drawing.Size(384, 204)
        Me.grpPresets.Text = "المواضع المسبقة"
        '
        'lstPresets
        '
        Me.lstPresets.Name = "lstPresets"
        Me.lstPresets.Location = New System.Drawing.Point(12, 26)
        Me.lstPresets.Size = New System.Drawing.Size(200, 160)
        Me.lstPresets.FormattingEnabled = True
        Me.lstPresets.IntegralHeight = False
        '
        'lblPresetId
        '
        Me.lblPresetId.Name = "lblPresetId"
        Me.lblPresetId.AutoSize = True
        Me.lblPresetId.Location = New System.Drawing.Point(224, 30)
        Me.lblPresetId.Text = "الرقم"
        '
        'nudPresetId
        '
        Me.nudPresetId.Name = "nudPresetId"
        Me.nudPresetId.Location = New System.Drawing.Point(280, 26)
        Me.nudPresetId.Size = New System.Drawing.Size(90, 23)
        Me.nudPresetId.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudPresetId.Maximum = New Decimal(New Integer() {300, 0, 0, 0})
        Me.nudPresetId.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'lblPresetName
        '
        Me.lblPresetName.Name = "lblPresetName"
        Me.lblPresetName.AutoSize = True
        Me.lblPresetName.Location = New System.Drawing.Point(224, 62)
        Me.lblPresetName.Text = "الاسم"
        '
        'txtPresetName
        '
        Me.txtPresetName.Name = "txtPresetName"
        Me.txtPresetName.Location = New System.Drawing.Point(280, 58)
        Me.txtPresetName.Size = New System.Drawing.Size(90, 23)
        Me.txtPresetName.MaxLength = 32
        '
        'btnPresetGo
        '
        Me.btnPresetGo.Name = "btnPresetGo"
        Me.btnPresetGo.Location = New System.Drawing.Point(224, 94)
        Me.btnPresetGo.Size = New System.Drawing.Size(146, 28)
        Me.btnPresetGo.Text = "انتقال"
        Me.btnPresetGo.UseVisualStyleBackColor = True
        '
        'btnPresetSave
        '
        Me.btnPresetSave.Name = "btnPresetSave"
        Me.btnPresetSave.Location = New System.Drawing.Point(224, 126)
        Me.btnPresetSave.Size = New System.Drawing.Size(146, 28)
        Me.btnPresetSave.Text = "حفظ الموضع الحالي"
        Me.btnPresetSave.UseVisualStyleBackColor = True
        '
        'btnPresetDelete
        '
        Me.btnPresetDelete.Name = "btnPresetDelete"
        Me.btnPresetDelete.Location = New System.Drawing.Point(224, 158)
        Me.btnPresetDelete.Size = New System.Drawing.Size(146, 28)
        Me.btnPresetDelete.Text = "حذف"
        Me.btnPresetDelete.UseVisualStyleBackColor = True
        '
        'grpTours
        '
        Me.grpTours.Name = "grpTours"
        Me.grpTours.Location = New System.Drawing.Point(8, 532)
        Me.grpTours.Size = New System.Drawing.Size(384, 140)
        Me.grpTours.Text = "الدوريات والأنماط"
        '
        'lblPatrol
        '
        Me.lblPatrol.Name = "lblPatrol"
        Me.lblPatrol.AutoSize = True
        Me.lblPatrol.Location = New System.Drawing.Point(12, 32)
        Me.lblPatrol.Text = "دورية"
        '
        'nudPatrol
        '
        Me.nudPatrol.Name = "nudPatrol"
        Me.nudPatrol.Location = New System.Drawing.Point(70, 28)
        Me.nudPatrol.Size = New System.Drawing.Size(60, 23)
        Me.nudPatrol.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudPatrol.Maximum = New Decimal(New Integer() {8, 0, 0, 0})
        Me.nudPatrol.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'btnPatrolStart
        '
        Me.btnPatrolStart.Name = "btnPatrolStart"
        Me.btnPatrolStart.Location = New System.Drawing.Point(140, 26)
        Me.btnPatrolStart.Size = New System.Drawing.Size(110, 28)
        Me.btnPatrolStart.Text = "تشغيل"
        Me.btnPatrolStart.UseVisualStyleBackColor = True
        '
        'btnPatrolStop
        '
        Me.btnPatrolStop.Name = "btnPatrolStop"
        Me.btnPatrolStop.Location = New System.Drawing.Point(256, 26)
        Me.btnPatrolStop.Size = New System.Drawing.Size(110, 28)
        Me.btnPatrolStop.Text = "إيقاف"
        Me.btnPatrolStop.UseVisualStyleBackColor = True
        '
        'lblPattern
        '
        Me.lblPattern.Name = "lblPattern"
        Me.lblPattern.AutoSize = True
        Me.lblPattern.Location = New System.Drawing.Point(12, 72)
        Me.lblPattern.Text = "نمط"
        '
        'nudPattern
        '
        Me.nudPattern.Name = "nudPattern"
        Me.nudPattern.Location = New System.Drawing.Point(70, 68)
        Me.nudPattern.Size = New System.Drawing.Size(60, 23)
        Me.nudPattern.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudPattern.Maximum = New Decimal(New Integer() {4, 0, 0, 0})
        Me.nudPattern.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'btnPatternRec
        '
        Me.btnPatternRec.Name = "btnPatternRec"
        Me.btnPatternRec.Location = New System.Drawing.Point(140, 66)
        Me.btnPatternRec.Size = New System.Drawing.Size(110, 28)
        Me.btnPatternRec.Text = "بدء التسجيل"
        Me.btnPatternRec.UseVisualStyleBackColor = True
        '
        'btnPatternRecStop
        '
        Me.btnPatternRecStop.Name = "btnPatternRecStop"
        Me.btnPatternRecStop.Location = New System.Drawing.Point(256, 66)
        Me.btnPatternRecStop.Size = New System.Drawing.Size(110, 28)
        Me.btnPatternRecStop.Text = "إنهاء التسجيل"
        Me.btnPatternRecStop.UseVisualStyleBackColor = True
        '
        'btnPatternRun
        '
        Me.btnPatternRun.Name = "btnPatternRun"
        Me.btnPatternRun.Location = New System.Drawing.Point(140, 100)
        Me.btnPatternRun.Size = New System.Drawing.Size(110, 28)
        Me.btnPatternRun.Text = "تشغيل"
        Me.btnPatternRun.UseVisualStyleBackColor = True
        '
        'btnPatternStop
        '
        Me.btnPatternStop.Name = "btnPatternStop"
        Me.btnPatternStop.Location = New System.Drawing.Point(256, 100)
        Me.btnPatternStop.Size = New System.Drawing.Size(110, 28)
        Me.btnPatternStop.Text = "إيقاف"
        Me.btnPatternStop.UseVisualStyleBackColor = True
        '
        'grpAbs
        '
        Me.grpAbs.Name = "grpAbs"
        Me.grpAbs.Location = New System.Drawing.Point(8, 680)
        Me.grpAbs.Size = New System.Drawing.Size(384, 112)
        Me.grpAbs.Text = "موضع مطلق"
        '
        'lblAz
        '
        Me.lblAz.Name = "lblAz"
        Me.lblAz.AutoSize = True
        Me.lblAz.Location = New System.Drawing.Point(12, 32)
        Me.lblAz.Text = "السمت °"
        '
        'nudAz
        '
        Me.nudAz.Name = "nudAz"
        Me.nudAz.Location = New System.Drawing.Point(80, 28)
        Me.nudAz.Size = New System.Drawing.Size(80, 23)
        Me.nudAz.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        Me.nudAz.Maximum = New Decimal(New Integer() {360, 0, 0, 0})
        Me.nudAz.Value = New Decimal(New Integer() {0, 0, 0, 0})
        '
        'lblEl
        '
        Me.lblEl.Name = "lblEl"
        Me.lblEl.AutoSize = True
        Me.lblEl.Location = New System.Drawing.Point(176, 32)
        Me.lblEl.Text = "الارتفاع °"
        '
        'nudEl
        '
        Me.nudEl.Name = "nudEl"
        Me.nudEl.Location = New System.Drawing.Point(250, 28)
        Me.nudEl.Size = New System.Drawing.Size(80, 23)
        Me.nudEl.Minimum = New Decimal(New Integer() {90, 0, 0, -2147483648})
        Me.nudEl.Maximum = New Decimal(New Integer() {90, 0, 0, 0})
        Me.nudEl.Value = New Decimal(New Integer() {0, 0, 0, 0})
        '
        'lblZm
        '
        Me.lblZm.Name = "lblZm"
        Me.lblZm.AutoSize = True
        Me.lblZm.Location = New System.Drawing.Point(12, 70)
        Me.lblZm.Text = "التكبير ×"
        '
        'nudZm
        '
        Me.nudZm.Name = "nudZm"
        Me.nudZm.Location = New System.Drawing.Point(80, 66)
        Me.nudZm.Size = New System.Drawing.Size(80, 23)
        Me.nudZm.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudZm.Maximum = New Decimal(New Integer() {32, 0, 0, 0})
        Me.nudZm.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'btnGoAbs
        '
        Me.btnGoAbs.Name = "btnGoAbs"
        Me.btnGoAbs.Location = New System.Drawing.Point(176, 64)
        Me.btnGoAbs.Size = New System.Drawing.Size(154, 28)
        Me.btnGoAbs.Text = "انتقال"
        Me.btnGoAbs.UseVisualStyleBackColor = True
        Me.Controls.Add(Me.grpMove)
        Me.Controls.Add(Me.grpPresets)
        Me.Controls.Add(Me.grpTours)
        Me.Controls.Add(Me.grpAbs)
        Me.grpMove.Controls.Add(Me.joystick)
        Me.grpMove.Controls.Add(Me.lblSpeedCaption)
        Me.grpMove.Controls.Add(Me.trkSpeed)
        Me.grpMove.Controls.Add(Me.lblSpeedValue)
        Me.grpMove.Controls.Add(Me.btnZoomIn)
        Me.grpMove.Controls.Add(Me.btnZoomOut)
        Me.grpMove.Controls.Add(Me.btnHome)
        Me.grpMove.Controls.Add(Me.btnSetHome)
        Me.grpMove.Controls.Add(Me.lblPosition)
        Me.grpPresets.Controls.Add(Me.lstPresets)
        Me.grpPresets.Controls.Add(Me.lblPresetId)
        Me.grpPresets.Controls.Add(Me.nudPresetId)
        Me.grpPresets.Controls.Add(Me.lblPresetName)
        Me.grpPresets.Controls.Add(Me.txtPresetName)
        Me.grpPresets.Controls.Add(Me.btnPresetGo)
        Me.grpPresets.Controls.Add(Me.btnPresetSave)
        Me.grpPresets.Controls.Add(Me.btnPresetDelete)
        Me.grpTours.Controls.Add(Me.lblPatrol)
        Me.grpTours.Controls.Add(Me.nudPatrol)
        Me.grpTours.Controls.Add(Me.btnPatrolStart)
        Me.grpTours.Controls.Add(Me.btnPatrolStop)
        Me.grpTours.Controls.Add(Me.lblPattern)
        Me.grpTours.Controls.Add(Me.nudPattern)
        Me.grpTours.Controls.Add(Me.btnPatternRec)
        Me.grpTours.Controls.Add(Me.btnPatternRecStop)
        Me.grpTours.Controls.Add(Me.btnPatternRun)
        Me.grpTours.Controls.Add(Me.btnPatternStop)
        Me.grpAbs.Controls.Add(Me.lblAz)
        Me.grpAbs.Controls.Add(Me.nudAz)
        Me.grpAbs.Controls.Add(Me.lblEl)
        Me.grpAbs.Controls.Add(Me.nudEl)
        Me.grpAbs.Controls.Add(Me.lblZm)
        Me.grpAbs.Controls.Add(Me.nudZm)
        Me.grpAbs.Controls.Add(Me.btnGoAbs)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoScroll = True
        Me.Name = "PtzPanel"
        Me.Size = New System.Drawing.Size(400, 800)
        CType(Me.nudZm, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudEl, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudAz, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpAbs.ResumeLayout(False)
        Me.grpAbs.PerformLayout()
        CType(Me.nudPattern, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudPatrol, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpTours.ResumeLayout(False)
        Me.grpTours.PerformLayout()
        CType(Me.nudPresetId, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpPresets.ResumeLayout(False)
        Me.grpPresets.PerformLayout()
        CType(Me.trkSpeed, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpMove.ResumeLayout(False)
        Me.grpMove.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents grpMove As System.Windows.Forms.GroupBox
    Friend WithEvents joystick As Ptz.App.Controls.JoystickControl
    Friend WithEvents lblSpeedCaption As System.Windows.Forms.Label
    Friend WithEvents trkSpeed As System.Windows.Forms.TrackBar
    Friend WithEvents lblSpeedValue As System.Windows.Forms.Label
    Friend WithEvents btnZoomIn As System.Windows.Forms.Button
    Friend WithEvents btnZoomOut As System.Windows.Forms.Button
    Friend WithEvents btnHome As System.Windows.Forms.Button
    Friend WithEvents btnSetHome As System.Windows.Forms.Button
    Friend WithEvents lblPosition As System.Windows.Forms.Label
    Friend WithEvents grpPresets As System.Windows.Forms.GroupBox
    Friend WithEvents lstPresets As System.Windows.Forms.ListBox
    Friend WithEvents lblPresetId As System.Windows.Forms.Label
    Friend WithEvents nudPresetId As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblPresetName As System.Windows.Forms.Label
    Friend WithEvents txtPresetName As System.Windows.Forms.TextBox
    Friend WithEvents btnPresetGo As System.Windows.Forms.Button
    Friend WithEvents btnPresetSave As System.Windows.Forms.Button
    Friend WithEvents btnPresetDelete As System.Windows.Forms.Button
    Friend WithEvents grpTours As System.Windows.Forms.GroupBox
    Friend WithEvents lblPatrol As System.Windows.Forms.Label
    Friend WithEvents nudPatrol As System.Windows.Forms.NumericUpDown
    Friend WithEvents btnPatrolStart As System.Windows.Forms.Button
    Friend WithEvents btnPatrolStop As System.Windows.Forms.Button
    Friend WithEvents lblPattern As System.Windows.Forms.Label
    Friend WithEvents nudPattern As System.Windows.Forms.NumericUpDown
    Friend WithEvents btnPatternRec As System.Windows.Forms.Button
    Friend WithEvents btnPatternRecStop As System.Windows.Forms.Button
    Friend WithEvents btnPatternRun As System.Windows.Forms.Button
    Friend WithEvents btnPatternStop As System.Windows.Forms.Button
    Friend WithEvents grpAbs As System.Windows.Forms.GroupBox
    Friend WithEvents lblAz As System.Windows.Forms.Label
    Friend WithEvents nudAz As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblEl As System.Windows.Forms.Label
    Friend WithEvents nudEl As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblZm As System.Windows.Forms.Label
    Friend WithEvents nudZm As System.Windows.Forms.NumericUpDown
    Friend WithEvents btnGoAbs As System.Windows.Forms.Button

End Class
End Namespace
