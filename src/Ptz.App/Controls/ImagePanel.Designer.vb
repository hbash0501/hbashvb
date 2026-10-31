Namespace Controls
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ImagePanel
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
        Me.grpColor = New System.Windows.Forms.GroupBox()
        Me.lblBrightness = New System.Windows.Forms.Label()
        Me.trkBrightness = New System.Windows.Forms.TrackBar()
        Me.lblBrightnessValue = New System.Windows.Forms.Label()
        Me.lblContrast = New System.Windows.Forms.Label()
        Me.trkContrast = New System.Windows.Forms.TrackBar()
        Me.lblContrastValue = New System.Windows.Forms.Label()
        Me.lblSaturation = New System.Windows.Forms.Label()
        Me.trkSaturation = New System.Windows.Forms.TrackBar()
        Me.lblSaturationValue = New System.Windows.Forms.Label()
        Me.lblSharpness = New System.Windows.Forms.Label()
        Me.trkSharpness = New System.Windows.Forms.TrackBar()
        Me.lblSharpnessValue = New System.Windows.Forms.Label()
        Me.lblNoise = New System.Windows.Forms.Label()
        Me.trkNoise = New System.Windows.Forms.TrackBar()
        Me.lblNoiseValue = New System.Windows.Forms.Label()
        Me.grpExposure = New System.Windows.Forms.GroupBox()
        Me.lblExposure = New System.Windows.Forms.Label()
        Me.cmbExposureType = New System.Windows.Forms.ComboBox()
        Me.lblIrCut = New System.Windows.Forms.Label()
        Me.cmbIrCutMode = New System.Windows.Forms.ComboBox()
        Me.lblWdr = New System.Windows.Forms.Label()
        Me.cmbWdrMode = New System.Windows.Forms.ComboBox()
        Me.lblWdrLevel = New System.Windows.Forms.Label()
        Me.trkWdrLevel = New System.Windows.Forms.TrackBar()
        Me.lblWdrLevelValue = New System.Windows.Forms.Label()
        Me.lblBlc = New System.Windows.Forms.Label()
        Me.cmbBlcMode = New System.Windows.Forms.ComboBox()
        Me.lblWb = New System.Windows.Forms.Label()
        Me.cmbWhiteBalance = New System.Windows.Forms.ComboBox()
        Me.lblDefog = New System.Windows.Forms.Label()
        Me.cmbDefog = New System.Windows.Forms.ComboBox()
        Me.lblSupplement = New System.Windows.Forms.Label()
        Me.cmbSupplement = New System.Windows.Forms.ComboBox()
        Me.lblIrBrightness = New System.Windows.Forms.Label()
        Me.trkIrBrightness = New System.Windows.Forms.TrackBar()
        Me.lblIrBrightnessValue = New System.Windows.Forms.Label()
        Me.grpLens = New System.Windows.Forms.GroupBox()
        Me.lblFocusStyle = New System.Windows.Forms.Label()
        Me.cmbFocusStyle = New System.Windows.Forms.ComboBox()
        Me.btnFocusNear = New System.Windows.Forms.Button()
        Me.btnFocusFar = New System.Windows.Forms.Button()
        Me.lblFocusHint = New System.Windows.Forms.Label()
        Me.btnIrisClose = New System.Windows.Forms.Button()
        Me.btnIrisOpen = New System.Windows.Forms.Button()
        Me.grpAux = New System.Windows.Forms.GroupBox()
        Me.btnWiperOn = New System.Windows.Forms.Button()
        Me.btnWiperOff = New System.Windows.Forms.Button()
        Me.btnLightOn = New System.Windows.Forms.Button()
        Me.btnLightOff = New System.Windows.Forms.Button()
        Me.btnApply = New System.Windows.Forms.Button()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.grpColor.SuspendLayout()
        CType(Me.trkBrightness, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trkContrast, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trkSaturation, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trkSharpness, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trkNoise, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpExposure.SuspendLayout()
        CType(Me.trkWdrLevel, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trkIrBrightness, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpLens.SuspendLayout()
        Me.grpAux.SuspendLayout()
        Me.SuspendLayout()
        '
        'grpColor
        '
        Me.grpColor.Name = "grpColor"
        Me.grpColor.Location = New System.Drawing.Point(8, 8)
        Me.grpColor.Size = New System.Drawing.Size(384, 218)
        Me.grpColor.Text = "الصورة"
        '
        'lblBrightness
        '
        Me.lblBrightness.Name = "lblBrightness"
        Me.lblBrightness.AutoSize = True
        Me.lblBrightness.Location = New System.Drawing.Point(12, 28)
        Me.lblBrightness.Text = "السطوع"
        '
        'trkBrightness
        '
        Me.trkBrightness.Name = "trkBrightness"
        Me.trkBrightness.Location = New System.Drawing.Point(112, 20)
        Me.trkBrightness.Size = New System.Drawing.Size(200, 45)
        Me.trkBrightness.Minimum = 0
        Me.trkBrightness.Maximum = 100
        Me.trkBrightness.TickFrequency = 10
        Me.trkBrightness.Value = 50
        '
        'lblBrightnessValue
        '
        Me.lblBrightnessValue.Name = "lblBrightnessValue"
        Me.lblBrightnessValue.AutoSize = True
        Me.lblBrightnessValue.Location = New System.Drawing.Point(320, 28)
        Me.lblBrightnessValue.Text = "50"
        Me.lblBrightnessValue.Tag = "muted"
        '
        'lblContrast
        '
        Me.lblContrast.Name = "lblContrast"
        Me.lblContrast.AutoSize = True
        Me.lblContrast.Location = New System.Drawing.Point(12, 66)
        Me.lblContrast.Text = "التباين"
        '
        'trkContrast
        '
        Me.trkContrast.Name = "trkContrast"
        Me.trkContrast.Location = New System.Drawing.Point(112, 58)
        Me.trkContrast.Size = New System.Drawing.Size(200, 45)
        Me.trkContrast.Minimum = 0
        Me.trkContrast.Maximum = 100
        Me.trkContrast.TickFrequency = 10
        Me.trkContrast.Value = 50
        '
        'lblContrastValue
        '
        Me.lblContrastValue.Name = "lblContrastValue"
        Me.lblContrastValue.AutoSize = True
        Me.lblContrastValue.Location = New System.Drawing.Point(320, 66)
        Me.lblContrastValue.Text = "50"
        Me.lblContrastValue.Tag = "muted"
        '
        'lblSaturation
        '
        Me.lblSaturation.Name = "lblSaturation"
        Me.lblSaturation.AutoSize = True
        Me.lblSaturation.Location = New System.Drawing.Point(12, 104)
        Me.lblSaturation.Text = "التشبّع"
        '
        'trkSaturation
        '
        Me.trkSaturation.Name = "trkSaturation"
        Me.trkSaturation.Location = New System.Drawing.Point(112, 96)
        Me.trkSaturation.Size = New System.Drawing.Size(200, 45)
        Me.trkSaturation.Minimum = 0
        Me.trkSaturation.Maximum = 100
        Me.trkSaturation.TickFrequency = 10
        Me.trkSaturation.Value = 50
        '
        'lblSaturationValue
        '
        Me.lblSaturationValue.Name = "lblSaturationValue"
        Me.lblSaturationValue.AutoSize = True
        Me.lblSaturationValue.Location = New System.Drawing.Point(320, 104)
        Me.lblSaturationValue.Text = "50"
        Me.lblSaturationValue.Tag = "muted"
        '
        'lblSharpness
        '
        Me.lblSharpness.Name = "lblSharpness"
        Me.lblSharpness.AutoSize = True
        Me.lblSharpness.Location = New System.Drawing.Point(12, 142)
        Me.lblSharpness.Text = "الحدّة"
        '
        'trkSharpness
        '
        Me.trkSharpness.Name = "trkSharpness"
        Me.trkSharpness.Location = New System.Drawing.Point(112, 134)
        Me.trkSharpness.Size = New System.Drawing.Size(200, 45)
        Me.trkSharpness.Minimum = 0
        Me.trkSharpness.Maximum = 100
        Me.trkSharpness.TickFrequency = 10
        Me.trkSharpness.Value = 50
        '
        'lblSharpnessValue
        '
        Me.lblSharpnessValue.Name = "lblSharpnessValue"
        Me.lblSharpnessValue.AutoSize = True
        Me.lblSharpnessValue.Location = New System.Drawing.Point(320, 142)
        Me.lblSharpnessValue.Text = "50"
        Me.lblSharpnessValue.Tag = "muted"
        '
        'lblNoise
        '
        Me.lblNoise.Name = "lblNoise"
        Me.lblNoise.AutoSize = True
        Me.lblNoise.Location = New System.Drawing.Point(12, 180)
        Me.lblNoise.Text = "تقليل الضجيج"
        '
        'trkNoise
        '
        Me.trkNoise.Name = "trkNoise"
        Me.trkNoise.Location = New System.Drawing.Point(112, 172)
        Me.trkNoise.Size = New System.Drawing.Size(200, 45)
        Me.trkNoise.Minimum = 0
        Me.trkNoise.Maximum = 100
        Me.trkNoise.TickFrequency = 10
        Me.trkNoise.Value = 50
        '
        'lblNoiseValue
        '
        Me.lblNoiseValue.Name = "lblNoiseValue"
        Me.lblNoiseValue.AutoSize = True
        Me.lblNoiseValue.Location = New System.Drawing.Point(320, 180)
        Me.lblNoiseValue.Text = "50"
        Me.lblNoiseValue.Tag = "muted"
        '
        'grpExposure
        '
        Me.grpExposure.Name = "grpExposure"
        Me.grpExposure.Location = New System.Drawing.Point(8, 234)
        Me.grpExposure.Size = New System.Drawing.Size(384, 328)
        Me.grpExposure.Text = "التعريض والإضاءة"
        '
        'lblExposure
        '
        Me.lblExposure.Name = "lblExposure"
        Me.lblExposure.AutoSize = True
        Me.lblExposure.Location = New System.Drawing.Point(12, 28)
        Me.lblExposure.Text = "نمط التعريض"
        '
        'cmbExposureType
        '
        Me.cmbExposureType.Name = "cmbExposureType"
        Me.cmbExposureType.Location = New System.Drawing.Point(160, 24)
        Me.cmbExposureType.Size = New System.Drawing.Size(200, 23)
        Me.cmbExposureType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbExposureType.FormattingEnabled = True
        '
        'lblIrCut
        '
        Me.lblIrCut.Name = "lblIrCut"
        Me.lblIrCut.AutoSize = True
        Me.lblIrCut.Location = New System.Drawing.Point(12, 60)
        Me.lblIrCut.Text = "فلتر IR (نهار/ليل)"
        '
        'cmbIrCutMode
        '
        Me.cmbIrCutMode.Name = "cmbIrCutMode"
        Me.cmbIrCutMode.Location = New System.Drawing.Point(160, 56)
        Me.cmbIrCutMode.Size = New System.Drawing.Size(200, 23)
        Me.cmbIrCutMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbIrCutMode.FormattingEnabled = True
        '
        'lblWdr
        '
        Me.lblWdr.Name = "lblWdr"
        Me.lblWdr.AutoSize = True
        Me.lblWdr.Location = New System.Drawing.Point(12, 92)
        Me.lblWdr.Text = "WDR"
        '
        'cmbWdrMode
        '
        Me.cmbWdrMode.Name = "cmbWdrMode"
        Me.cmbWdrMode.Location = New System.Drawing.Point(160, 88)
        Me.cmbWdrMode.Size = New System.Drawing.Size(200, 23)
        Me.cmbWdrMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbWdrMode.FormattingEnabled = True
        '
        'lblWdrLevel
        '
        Me.lblWdrLevel.Name = "lblWdrLevel"
        Me.lblWdrLevel.AutoSize = True
        Me.lblWdrLevel.Location = New System.Drawing.Point(12, 124)
        Me.lblWdrLevel.Text = "مستوى WDR"
        '
        'trkWdrLevel
        '
        Me.trkWdrLevel.Name = "trkWdrLevel"
        Me.trkWdrLevel.Location = New System.Drawing.Point(112, 116)
        Me.trkWdrLevel.Size = New System.Drawing.Size(200, 45)
        Me.trkWdrLevel.Minimum = 0
        Me.trkWdrLevel.Maximum = 100
        Me.trkWdrLevel.TickFrequency = 10
        Me.trkWdrLevel.Value = 50
        '
        'lblWdrLevelValue
        '
        Me.lblWdrLevelValue.Name = "lblWdrLevelValue"
        Me.lblWdrLevelValue.AutoSize = True
        Me.lblWdrLevelValue.Location = New System.Drawing.Point(320, 124)
        Me.lblWdrLevelValue.Text = "50"
        Me.lblWdrLevelValue.Tag = "muted"
        '
        'lblBlc
        '
        Me.lblBlc.Name = "lblBlc"
        Me.lblBlc.AutoSize = True
        Me.lblBlc.Location = New System.Drawing.Point(12, 162)
        Me.lblBlc.Text = "تعويض الضوء الخلفي"
        '
        'cmbBlcMode
        '
        Me.cmbBlcMode.Name = "cmbBlcMode"
        Me.cmbBlcMode.Location = New System.Drawing.Point(160, 158)
        Me.cmbBlcMode.Size = New System.Drawing.Size(200, 23)
        Me.cmbBlcMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbBlcMode.FormattingEnabled = True
        '
        'lblWb
        '
        Me.lblWb.Name = "lblWb"
        Me.lblWb.AutoSize = True
        Me.lblWb.Location = New System.Drawing.Point(12, 194)
        Me.lblWb.Text = "توازن الأبيض"
        '
        'cmbWhiteBalance
        '
        Me.cmbWhiteBalance.Name = "cmbWhiteBalance"
        Me.cmbWhiteBalance.Location = New System.Drawing.Point(160, 190)
        Me.cmbWhiteBalance.Size = New System.Drawing.Size(200, 23)
        Me.cmbWhiteBalance.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbWhiteBalance.FormattingEnabled = True
        '
        'lblDefog
        '
        Me.lblDefog.Name = "lblDefog"
        Me.lblDefog.AutoSize = True
        Me.lblDefog.Location = New System.Drawing.Point(12, 226)
        Me.lblDefog.Text = "إزالة الضباب"
        '
        'cmbDefog
        '
        Me.cmbDefog.Name = "cmbDefog"
        Me.cmbDefog.Location = New System.Drawing.Point(160, 222)
        Me.cmbDefog.Size = New System.Drawing.Size(200, 23)
        Me.cmbDefog.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbDefog.FormattingEnabled = True
        '
        'lblSupplement
        '
        Me.lblSupplement.Name = "lblSupplement"
        Me.lblSupplement.AutoSize = True
        Me.lblSupplement.Location = New System.Drawing.Point(12, 258)
        Me.lblSupplement.Text = "الإضاءة المساعدة"
        '
        'cmbSupplement
        '
        Me.cmbSupplement.Name = "cmbSupplement"
        Me.cmbSupplement.Location = New System.Drawing.Point(160, 254)
        Me.cmbSupplement.Size = New System.Drawing.Size(200, 23)
        Me.cmbSupplement.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbSupplement.FormattingEnabled = True
        '
        'lblIrBrightness
        '
        Me.lblIrBrightness.Name = "lblIrBrightness"
        Me.lblIrBrightness.AutoSize = True
        Me.lblIrBrightness.Location = New System.Drawing.Point(12, 290)
        Me.lblIrBrightness.Text = "سطوع IR"
        '
        'trkIrBrightness
        '
        Me.trkIrBrightness.Name = "trkIrBrightness"
        Me.trkIrBrightness.Location = New System.Drawing.Point(112, 282)
        Me.trkIrBrightness.Size = New System.Drawing.Size(200, 45)
        Me.trkIrBrightness.Minimum = 0
        Me.trkIrBrightness.Maximum = 100
        Me.trkIrBrightness.TickFrequency = 10
        Me.trkIrBrightness.Value = 50
        '
        'lblIrBrightnessValue
        '
        Me.lblIrBrightnessValue.Name = "lblIrBrightnessValue"
        Me.lblIrBrightnessValue.AutoSize = True
        Me.lblIrBrightnessValue.Location = New System.Drawing.Point(320, 290)
        Me.lblIrBrightnessValue.Text = "50"
        Me.lblIrBrightnessValue.Tag = "muted"
        '
        'grpLens
        '
        Me.grpLens.Name = "grpLens"
        Me.grpLens.Location = New System.Drawing.Point(8, 570)
        Me.grpLens.Size = New System.Drawing.Size(384, 132)
        Me.grpLens.Text = "العدسة"
        '
        'lblFocusStyle
        '
        Me.lblFocusStyle.Name = "lblFocusStyle"
        Me.lblFocusStyle.AutoSize = True
        Me.lblFocusStyle.Location = New System.Drawing.Point(12, 28)
        Me.lblFocusStyle.Text = "نمط التركيز"
        '
        'cmbFocusStyle
        '
        Me.cmbFocusStyle.Name = "cmbFocusStyle"
        Me.cmbFocusStyle.Location = New System.Drawing.Point(160, 24)
        Me.cmbFocusStyle.Size = New System.Drawing.Size(200, 23)
        Me.cmbFocusStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbFocusStyle.FormattingEnabled = True
        '
        'btnFocusNear
        '
        Me.btnFocusNear.Name = "btnFocusNear"
        Me.btnFocusNear.Location = New System.Drawing.Point(12, 56)
        Me.btnFocusNear.Size = New System.Drawing.Size(110, 28)
        Me.btnFocusNear.Text = "تركيز قريب"
        Me.btnFocusNear.UseVisualStyleBackColor = True
        '
        'btnFocusFar
        '
        Me.btnFocusFar.Name = "btnFocusFar"
        Me.btnFocusFar.Location = New System.Drawing.Point(128, 56)
        Me.btnFocusFar.Size = New System.Drawing.Size(110, 28)
        Me.btnFocusFar.Text = "تركيز بعيد"
        Me.btnFocusFar.UseVisualStyleBackColor = True
        '
        'lblFocusHint
        '
        Me.lblFocusHint.Name = "lblFocusHint"
        Me.lblFocusHint.AutoSize = True
        Me.lblFocusHint.Location = New System.Drawing.Point(246, 62)
        Me.lblFocusHint.Text = "(اضغط مطولاً)"
        Me.lblFocusHint.Tag = "muted"
        '
        'btnIrisClose
        '
        Me.btnIrisClose.Name = "btnIrisClose"
        Me.btnIrisClose.Location = New System.Drawing.Point(12, 92)
        Me.btnIrisClose.Size = New System.Drawing.Size(110, 28)
        Me.btnIrisClose.Text = "فتحة أصغر"
        Me.btnIrisClose.UseVisualStyleBackColor = True
        '
        'btnIrisOpen
        '
        Me.btnIrisOpen.Name = "btnIrisOpen"
        Me.btnIrisOpen.Location = New System.Drawing.Point(128, 92)
        Me.btnIrisOpen.Size = New System.Drawing.Size(110, 28)
        Me.btnIrisOpen.Text = "فتحة أكبر"
        Me.btnIrisOpen.UseVisualStyleBackColor = True
        '
        'grpAux
        '
        Me.grpAux.Name = "grpAux"
        Me.grpAux.Location = New System.Drawing.Point(8, 710)
        Me.grpAux.Size = New System.Drawing.Size(384, 96)
        Me.grpAux.Text = "الملحقات"
        '
        'btnWiperOn
        '
        Me.btnWiperOn.Name = "btnWiperOn"
        Me.btnWiperOn.Location = New System.Drawing.Point(12, 24)
        Me.btnWiperOn.Size = New System.Drawing.Size(110, 28)
        Me.btnWiperOn.Text = "الممسحة: تشغيل"
        Me.btnWiperOn.UseVisualStyleBackColor = True
        '
        'btnWiperOff
        '
        Me.btnWiperOff.Name = "btnWiperOff"
        Me.btnWiperOff.Location = New System.Drawing.Point(128, 24)
        Me.btnWiperOff.Size = New System.Drawing.Size(110, 28)
        Me.btnWiperOff.Text = "الممسحة: إيقاف"
        Me.btnWiperOff.UseVisualStyleBackColor = True
        '
        'btnLightOn
        '
        Me.btnLightOn.Name = "btnLightOn"
        Me.btnLightOn.Location = New System.Drawing.Point(12, 58)
        Me.btnLightOn.Size = New System.Drawing.Size(110, 28)
        Me.btnLightOn.Text = "الإضاءة: تشغيل"
        Me.btnLightOn.UseVisualStyleBackColor = True
        '
        'btnLightOff
        '
        Me.btnLightOff.Name = "btnLightOff"
        Me.btnLightOff.Location = New System.Drawing.Point(128, 58)
        Me.btnLightOff.Size = New System.Drawing.Size(110, 28)
        Me.btnLightOff.Text = "الإضاءة: إيقاف"
        Me.btnLightOff.UseVisualStyleBackColor = True
        '
        'btnApply
        '
        Me.btnApply.Name = "btnApply"
        Me.btnApply.Location = New System.Drawing.Point(8, 814)
        Me.btnApply.Size = New System.Drawing.Size(190, 34)
        Me.btnApply.Text = "تطبيق التغييرات"
        Me.btnApply.UseVisualStyleBackColor = True
        '
        'btnRefresh
        '
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Location = New System.Drawing.Point(202, 814)
        Me.btnRefresh.Size = New System.Drawing.Size(190, 34)
        Me.btnRefresh.Text = "تحديث من الكاميرا"
        Me.btnRefresh.UseVisualStyleBackColor = True
        Me.Controls.Add(Me.grpColor)
        Me.Controls.Add(Me.grpExposure)
        Me.Controls.Add(Me.grpLens)
        Me.Controls.Add(Me.grpAux)
        Me.Controls.Add(Me.btnApply)
        Me.Controls.Add(Me.btnRefresh)
        Me.grpColor.Controls.Add(Me.lblBrightness)
        Me.grpColor.Controls.Add(Me.trkBrightness)
        Me.grpColor.Controls.Add(Me.lblBrightnessValue)
        Me.grpColor.Controls.Add(Me.lblContrast)
        Me.grpColor.Controls.Add(Me.trkContrast)
        Me.grpColor.Controls.Add(Me.lblContrastValue)
        Me.grpColor.Controls.Add(Me.lblSaturation)
        Me.grpColor.Controls.Add(Me.trkSaturation)
        Me.grpColor.Controls.Add(Me.lblSaturationValue)
        Me.grpColor.Controls.Add(Me.lblSharpness)
        Me.grpColor.Controls.Add(Me.trkSharpness)
        Me.grpColor.Controls.Add(Me.lblSharpnessValue)
        Me.grpColor.Controls.Add(Me.lblNoise)
        Me.grpColor.Controls.Add(Me.trkNoise)
        Me.grpColor.Controls.Add(Me.lblNoiseValue)
        Me.grpExposure.Controls.Add(Me.lblExposure)
        Me.grpExposure.Controls.Add(Me.cmbExposureType)
        Me.grpExposure.Controls.Add(Me.lblIrCut)
        Me.grpExposure.Controls.Add(Me.cmbIrCutMode)
        Me.grpExposure.Controls.Add(Me.lblWdr)
        Me.grpExposure.Controls.Add(Me.cmbWdrMode)
        Me.grpExposure.Controls.Add(Me.lblWdrLevel)
        Me.grpExposure.Controls.Add(Me.trkWdrLevel)
        Me.grpExposure.Controls.Add(Me.lblWdrLevelValue)
        Me.grpExposure.Controls.Add(Me.lblBlc)
        Me.grpExposure.Controls.Add(Me.cmbBlcMode)
        Me.grpExposure.Controls.Add(Me.lblWb)
        Me.grpExposure.Controls.Add(Me.cmbWhiteBalance)
        Me.grpExposure.Controls.Add(Me.lblDefog)
        Me.grpExposure.Controls.Add(Me.cmbDefog)
        Me.grpExposure.Controls.Add(Me.lblSupplement)
        Me.grpExposure.Controls.Add(Me.cmbSupplement)
        Me.grpExposure.Controls.Add(Me.lblIrBrightness)
        Me.grpExposure.Controls.Add(Me.trkIrBrightness)
        Me.grpExposure.Controls.Add(Me.lblIrBrightnessValue)
        Me.grpLens.Controls.Add(Me.lblFocusStyle)
        Me.grpLens.Controls.Add(Me.cmbFocusStyle)
        Me.grpLens.Controls.Add(Me.btnFocusNear)
        Me.grpLens.Controls.Add(Me.btnFocusFar)
        Me.grpLens.Controls.Add(Me.lblFocusHint)
        Me.grpLens.Controls.Add(Me.btnIrisClose)
        Me.grpLens.Controls.Add(Me.btnIrisOpen)
        Me.grpAux.Controls.Add(Me.btnWiperOn)
        Me.grpAux.Controls.Add(Me.btnWiperOff)
        Me.grpAux.Controls.Add(Me.btnLightOn)
        Me.grpAux.Controls.Add(Me.btnLightOff)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoScroll = True
        Me.Name = "ImagePanel"
        Me.Size = New System.Drawing.Size(400, 864)
        Me.grpAux.ResumeLayout(False)
        Me.grpAux.PerformLayout()
        Me.grpLens.ResumeLayout(False)
        Me.grpLens.PerformLayout()
        CType(Me.trkIrBrightness, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trkWdrLevel, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpExposure.ResumeLayout(False)
        Me.grpExposure.PerformLayout()
        CType(Me.trkNoise, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trkSharpness, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trkSaturation, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trkContrast, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trkBrightness, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpColor.ResumeLayout(False)
        Me.grpColor.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents grpColor As System.Windows.Forms.GroupBox
    Friend WithEvents lblBrightness As System.Windows.Forms.Label
    Friend WithEvents trkBrightness As System.Windows.Forms.TrackBar
    Friend WithEvents lblBrightnessValue As System.Windows.Forms.Label
    Friend WithEvents lblContrast As System.Windows.Forms.Label
    Friend WithEvents trkContrast As System.Windows.Forms.TrackBar
    Friend WithEvents lblContrastValue As System.Windows.Forms.Label
    Friend WithEvents lblSaturation As System.Windows.Forms.Label
    Friend WithEvents trkSaturation As System.Windows.Forms.TrackBar
    Friend WithEvents lblSaturationValue As System.Windows.Forms.Label
    Friend WithEvents lblSharpness As System.Windows.Forms.Label
    Friend WithEvents trkSharpness As System.Windows.Forms.TrackBar
    Friend WithEvents lblSharpnessValue As System.Windows.Forms.Label
    Friend WithEvents lblNoise As System.Windows.Forms.Label
    Friend WithEvents trkNoise As System.Windows.Forms.TrackBar
    Friend WithEvents lblNoiseValue As System.Windows.Forms.Label
    Friend WithEvents grpExposure As System.Windows.Forms.GroupBox
    Friend WithEvents lblExposure As System.Windows.Forms.Label
    Friend WithEvents cmbExposureType As System.Windows.Forms.ComboBox
    Friend WithEvents lblIrCut As System.Windows.Forms.Label
    Friend WithEvents cmbIrCutMode As System.Windows.Forms.ComboBox
    Friend WithEvents lblWdr As System.Windows.Forms.Label
    Friend WithEvents cmbWdrMode As System.Windows.Forms.ComboBox
    Friend WithEvents lblWdrLevel As System.Windows.Forms.Label
    Friend WithEvents trkWdrLevel As System.Windows.Forms.TrackBar
    Friend WithEvents lblWdrLevelValue As System.Windows.Forms.Label
    Friend WithEvents lblBlc As System.Windows.Forms.Label
    Friend WithEvents cmbBlcMode As System.Windows.Forms.ComboBox
    Friend WithEvents lblWb As System.Windows.Forms.Label
    Friend WithEvents cmbWhiteBalance As System.Windows.Forms.ComboBox
    Friend WithEvents lblDefog As System.Windows.Forms.Label
    Friend WithEvents cmbDefog As System.Windows.Forms.ComboBox
    Friend WithEvents lblSupplement As System.Windows.Forms.Label
    Friend WithEvents cmbSupplement As System.Windows.Forms.ComboBox
    Friend WithEvents lblIrBrightness As System.Windows.Forms.Label
    Friend WithEvents trkIrBrightness As System.Windows.Forms.TrackBar
    Friend WithEvents lblIrBrightnessValue As System.Windows.Forms.Label
    Friend WithEvents grpLens As System.Windows.Forms.GroupBox
    Friend WithEvents lblFocusStyle As System.Windows.Forms.Label
    Friend WithEvents cmbFocusStyle As System.Windows.Forms.ComboBox
    Friend WithEvents btnFocusNear As System.Windows.Forms.Button
    Friend WithEvents btnFocusFar As System.Windows.Forms.Button
    Friend WithEvents lblFocusHint As System.Windows.Forms.Label
    Friend WithEvents btnIrisClose As System.Windows.Forms.Button
    Friend WithEvents btnIrisOpen As System.Windows.Forms.Button
    Friend WithEvents grpAux As System.Windows.Forms.GroupBox
    Friend WithEvents btnWiperOn As System.Windows.Forms.Button
    Friend WithEvents btnWiperOff As System.Windows.Forms.Button
    Friend WithEvents btnLightOn As System.Windows.Forms.Button
    Friend WithEvents btnLightOff As System.Windows.Forms.Button
    Friend WithEvents btnApply As System.Windows.Forms.Button
    Friend WithEvents btnRefresh As System.Windows.Forms.Button

End Class
End Namespace
