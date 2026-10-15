<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class CameraImportForm
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
        Me.rightPanel = New System.Windows.Forms.Panel()
        Me.lblSettingsTitle = New System.Windows.Forms.Label()
        Me.groupDevice = New System.Windows.Forms.GroupBox()
        Me.btnCrossbar = New System.Windows.Forms.Button()
        Me.btnNativeSettings = New System.Windows.Forms.Button()
        Me.btnStop = New System.Windows.Forms.Button()
        Me.btnStart = New System.Windows.Forms.Button()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.cboResolution = New System.Windows.Forms.ComboBox()
        Me.lblResolution = New System.Windows.Forms.Label()
        Me.cboCameras = New System.Windows.Forms.ComboBox()
        Me.lblCamera = New System.Windows.Forms.Label()
        Me.groupControls = New System.Windows.Forms.GroupBox()
        Me.btnApplyProps = New System.Windows.Forms.Button()
        Me.chkAutoWhiteBalance = New System.Windows.Forms.CheckBox()
        Me.chkAutoExposure = New System.Windows.Forms.CheckBox()
        Me.chkAutoFocus = New System.Windows.Forms.CheckBox()
        Me.lblWhiteBalance = New System.Windows.Forms.Label()
        Me.trackWhiteBalance = New System.Windows.Forms.TrackBar()
        Me.lblZoom = New System.Windows.Forms.Label()
        Me.trackZoom = New System.Windows.Forms.TrackBar()
        Me.lblFocus = New System.Windows.Forms.Label()
        Me.trackFocus = New System.Windows.Forms.TrackBar()
        Me.lblExposure = New System.Windows.Forms.Label()
        Me.trackExposure = New System.Windows.Forms.TrackBar()
        Me.lblSharpness = New System.Windows.Forms.Label()
        Me.trackSharpness = New System.Windows.Forms.TrackBar()
        Me.lblContrast = New System.Windows.Forms.Label()
        Me.trackContrast = New System.Windows.Forms.TrackBar()
        Me.lblBrightness = New System.Windows.Forms.Label()
        Me.trackBrightness = New System.Windows.Forms.TrackBar()
        Me.groupOutput = New System.Windows.Forms.GroupBox()
        Me.btnOpenFolder = New System.Windows.Forms.Button()
        Me.numJpegQuality = New System.Windows.Forms.NumericUpDown()
        Me.lblQuality = New System.Windows.Forms.Label()
        Me.txtSaveFolder = New System.Windows.Forms.TextBox()
        Me.lblFolder = New System.Windows.Forms.Label()
        Me.mainPanel = New System.Windows.Forms.Panel()
        Me.picturePreview = New System.Windows.Forms.PictureBox()
        Me.bottomPanel = New System.Windows.Forms.Panel()
        Me.lstCaptures = New System.Windows.Forms.ListBox()
        Me.captureButtonsPanel = New System.Windows.Forms.Panel()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.btnAccept = New System.Windows.Forms.Button()
        Me.btnRemoveCapture = New System.Windows.Forms.Button()
        Me.btnCapture = New System.Windows.Forms.Button()
        Me.lblStatus = New System.Windows.Forms.Label()
        Me.headerPanel.SuspendLayout()
        Me.rightPanel.SuspendLayout()
        Me.groupDevice.SuspendLayout()
        Me.groupControls.SuspendLayout()
        CType(Me.trackWhiteBalance, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trackZoom, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trackFocus, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trackExposure, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trackSharpness, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trackContrast, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.trackBrightness, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.groupOutput.SuspendLayout()
        CType(Me.numJpegQuality, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.mainPanel.SuspendLayout()
        CType(Me.picturePreview, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.bottomPanel.SuspendLayout()
        Me.captureButtonsPanel.SuspendLayout()
        Me.SuspendLayout()
        '
        'headerPanel
        '
        Me.headerPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.headerPanel.Controls.Add(Me.lblSubtitle)
        Me.headerPanel.Controls.Add(Me.lblTitle)
        Me.headerPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.headerPanel.Location = New System.Drawing.Point(0, 0)
        Me.headerPanel.Name = "headerPanel"
        Me.headerPanel.Padding = New System.Windows.Forms.Padding(22)
        Me.headerPanel.Size = New System.Drawing.Size(1240, 96)
        Me.headerPanel.TabIndex = 0
        '
        'lblSubtitle
        '
        Me.lblSubtitle.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblSubtitle.Font = New System.Drawing.Font("Tahoma", 9.5!)
        Me.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(213, Byte), Integer), CType(CType(224, Byte), Integer), CType(CType(221, Byte), Integer))
        Me.lblSubtitle.Location = New System.Drawing.Point(22, 55)
        Me.lblSubtitle.Name = "lblSubtitle"
        Me.lblSubtitle.Size = New System.Drawing.Size(1196, 19)
        Me.lblSubtitle.TabIndex = 1
        Me.lblSubtitle.Text = "تصوير المستندات بالكاميرا، ضبط الدقة والخصائص، ثم إرسال اللقطات مباشرة إلى معالج الأرشفة والـ OCR."
        Me.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 16.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(22, 22)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(1196, 34)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "إدراج وثائق بالكاميرا"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'rightPanel
        '
        Me.rightPanel.BackColor = System.Drawing.Color.White
        Me.rightPanel.Controls.Add(Me.lblSettingsTitle)
        Me.rightPanel.Controls.Add(Me.groupDevice)
        Me.rightPanel.Controls.Add(Me.groupControls)
        Me.rightPanel.Controls.Add(Me.groupOutput)
        Me.rightPanel.Dock = System.Windows.Forms.DockStyle.Right
        Me.rightPanel.Location = New System.Drawing.Point(840, 96)
        Me.rightPanel.Name = "rightPanel"
        Me.rightPanel.Padding = New System.Windows.Forms.Padding(16)
        Me.rightPanel.Size = New System.Drawing.Size(400, 664)
        Me.rightPanel.TabIndex = 1
        '
        'lblSettingsTitle
        '
        Me.lblSettingsTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblSettingsTitle.Font = New System.Drawing.Font("Tahoma", 12.0!, System.Drawing.FontStyle.Bold)
        Me.lblSettingsTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.lblSettingsTitle.Location = New System.Drawing.Point(16, 16)
        Me.lblSettingsTitle.Name = "lblSettingsTitle"
        Me.lblSettingsTitle.Size = New System.Drawing.Size(368, 34)
        Me.lblSettingsTitle.TabIndex = 0
        Me.lblSettingsTitle.Text = "التحكم بالكاميرا"
        Me.lblSettingsTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'groupDevice
        '
        Me.groupDevice.Controls.Add(Me.btnCrossbar)
        Me.groupDevice.Controls.Add(Me.btnNativeSettings)
        Me.groupDevice.Controls.Add(Me.btnStop)
        Me.groupDevice.Controls.Add(Me.btnStart)
        Me.groupDevice.Controls.Add(Me.btnRefresh)
        Me.groupDevice.Controls.Add(Me.cboResolution)
        Me.groupDevice.Controls.Add(Me.lblResolution)
        Me.groupDevice.Controls.Add(Me.cboCameras)
        Me.groupDevice.Controls.Add(Me.lblCamera)
        Me.groupDevice.Dock = System.Windows.Forms.DockStyle.Top
        Me.groupDevice.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.groupDevice.Location = New System.Drawing.Point(16, 50)
        Me.groupDevice.Name = "groupDevice"
        Me.groupDevice.Padding = New System.Windows.Forms.Padding(12)
        Me.groupDevice.Size = New System.Drawing.Size(368, 178)
        Me.groupDevice.TabIndex = 1
        Me.groupDevice.TabStop = False
        Me.groupDevice.Text = "اختيار الجهاز والدقة"
        '
        'btnCrossbar
        '
        Me.btnCrossbar.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnCrossbar.FlatAppearance.BorderSize = 0
        Me.btnCrossbar.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnCrossbar.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.btnCrossbar.Location = New System.Drawing.Point(14, 135)
        Me.btnCrossbar.Name = "btnCrossbar"
        Me.btnCrossbar.Size = New System.Drawing.Size(105, 30)
        Me.btnCrossbar.TabIndex = 8
        Me.btnCrossbar.Text = "Crossbar"
        Me.btnCrossbar.UseVisualStyleBackColor = False
        '
        'btnNativeSettings
        '
        Me.btnNativeSettings.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnNativeSettings.Enabled = False
        Me.btnNativeSettings.FlatAppearance.BorderSize = 0
        Me.btnNativeSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnNativeSettings.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.btnNativeSettings.Location = New System.Drawing.Point(125, 135)
        Me.btnNativeSettings.Name = "btnNativeSettings"
        Me.btnNativeSettings.Size = New System.Drawing.Size(105, 30)
        Me.btnNativeSettings.TabIndex = 7
        Me.btnNativeSettings.Text = "إعدادات أصلية"
        Me.btnNativeSettings.UseVisualStyleBackColor = False
        '
        'btnStop
        '
        Me.btnStop.BackColor = System.Drawing.Color.FromArgb(CType(CType(235, Byte), Integer), CType(CType(238, Byte), Integer), CType(CType(237, Byte), Integer))
        Me.btnStop.Enabled = False
        Me.btnStop.FlatAppearance.BorderSize = 0
        Me.btnStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnStop.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.btnStop.Location = New System.Drawing.Point(236, 135)
        Me.btnStop.Name = "btnStop"
        Me.btnStop.Size = New System.Drawing.Size(54, 30)
        Me.btnStop.TabIndex = 6
        Me.btnStop.Text = "إيقاف"
        Me.btnStop.UseVisualStyleBackColor = False
        '
        'btnStart
        '
        Me.btnStart.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.btnStart.FlatAppearance.BorderSize = 0
        Me.btnStart.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnStart.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.btnStart.Location = New System.Drawing.Point(296, 135)
        Me.btnStart.Name = "btnStart"
        Me.btnStart.Size = New System.Drawing.Size(56, 30)
        Me.btnStart.TabIndex = 5
        Me.btnStart.Text = "تشغيل"
        Me.btnStart.UseVisualStyleBackColor = False
        '
        'btnRefresh
        '
        Me.btnRefresh.BackColor = System.Drawing.Color.WhiteSmoke
        Me.btnRefresh.FlatAppearance.BorderSize = 0
        Me.btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnRefresh.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.btnRefresh.Location = New System.Drawing.Point(14, 34)
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Size = New System.Drawing.Size(70, 28)
        Me.btnRefresh.TabIndex = 4
        Me.btnRefresh.Text = "تحديث"
        Me.btnRefresh.UseVisualStyleBackColor = False
        '
        'cboResolution
        '
        Me.cboResolution.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboResolution.Font = New System.Drawing.Font("Tahoma", 8.5!)
        Me.cboResolution.FormattingEnabled = True
        Me.cboResolution.Location = New System.Drawing.Point(14, 98)
        Me.cboResolution.Name = "cboResolution"
        Me.cboResolution.Size = New System.Drawing.Size(236, 25)
        Me.cboResolution.TabIndex = 3
        '
        'lblResolution
        '
        Me.lblResolution.Font = New System.Drawing.Font("Tahoma", 8.5!, System.Drawing.FontStyle.Bold)
        Me.lblResolution.Location = New System.Drawing.Point(256, 97)
        Me.lblResolution.Name = "lblResolution"
        Me.lblResolution.Size = New System.Drawing.Size(96, 25)
        Me.lblResolution.TabIndex = 2
        Me.lblResolution.Text = "الدقة"
        Me.lblResolution.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'cboCameras
        '
        Me.cboCameras.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboCameras.Font = New System.Drawing.Font("Tahoma", 8.5!)
        Me.cboCameras.FormattingEnabled = True
        Me.cboCameras.Location = New System.Drawing.Point(90, 36)
        Me.cboCameras.Name = "cboCameras"
        Me.cboCameras.Size = New System.Drawing.Size(160, 25)
        Me.cboCameras.TabIndex = 1
        '
        'lblCamera
        '
        Me.lblCamera.Font = New System.Drawing.Font("Tahoma", 8.5!, System.Drawing.FontStyle.Bold)
        Me.lblCamera.Location = New System.Drawing.Point(256, 36)
        Me.lblCamera.Name = "lblCamera"
        Me.lblCamera.Size = New System.Drawing.Size(96, 25)
        Me.lblCamera.TabIndex = 0
        Me.lblCamera.Text = "الكاميرا"
        Me.lblCamera.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'groupControls
        '
        Me.groupControls.Controls.Add(Me.btnApplyProps)
        Me.groupControls.Controls.Add(Me.chkAutoWhiteBalance)
        Me.groupControls.Controls.Add(Me.chkAutoExposure)
        Me.groupControls.Controls.Add(Me.chkAutoFocus)
        Me.groupControls.Controls.Add(Me.lblWhiteBalance)
        Me.groupControls.Controls.Add(Me.trackWhiteBalance)
        Me.groupControls.Controls.Add(Me.lblZoom)
        Me.groupControls.Controls.Add(Me.trackZoom)
        Me.groupControls.Controls.Add(Me.lblFocus)
        Me.groupControls.Controls.Add(Me.trackFocus)
        Me.groupControls.Controls.Add(Me.lblExposure)
        Me.groupControls.Controls.Add(Me.trackExposure)
        Me.groupControls.Controls.Add(Me.lblSharpness)
        Me.groupControls.Controls.Add(Me.trackSharpness)
        Me.groupControls.Controls.Add(Me.lblContrast)
        Me.groupControls.Controls.Add(Me.trackContrast)
        Me.groupControls.Controls.Add(Me.lblBrightness)
        Me.groupControls.Controls.Add(Me.trackBrightness)
        Me.groupControls.Dock = System.Windows.Forms.DockStyle.Top
        Me.groupControls.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.groupControls.Location = New System.Drawing.Point(16, 228)
        Me.groupControls.Name = "groupControls"
        Me.groupControls.Padding = New System.Windows.Forms.Padding(12)
        Me.groupControls.Size = New System.Drawing.Size(368, 324)
        Me.groupControls.TabIndex = 2
        Me.groupControls.TabStop = False
        Me.groupControls.Text = "ضبط الصورة والتحكم"
        '
        'btnApplyProps
        '
        Me.btnApplyProps.BackColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.btnApplyProps.Enabled = False
        Me.btnApplyProps.FlatAppearance.BorderSize = 0
        Me.btnApplyProps.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnApplyProps.Font = New System.Drawing.Font("Tahoma", 8.75!, System.Drawing.FontStyle.Bold)
        Me.btnApplyProps.ForeColor = System.Drawing.Color.White
        Me.btnApplyProps.Location = New System.Drawing.Point(14, 276)
        Me.btnApplyProps.Name = "btnApplyProps"
        Me.btnApplyProps.Size = New System.Drawing.Size(338, 34)
        Me.btnApplyProps.TabIndex = 17
        Me.btnApplyProps.Text = "تطبيق الإعدادات المتاحة"
        Me.btnApplyProps.UseVisualStyleBackColor = False
        '
        'chkAutoWhiteBalance
        '
        Me.chkAutoWhiteBalance.AutoSize = True
        Me.chkAutoWhiteBalance.Font = New System.Drawing.Font("Tahoma", 8.0!)
        Me.chkAutoWhiteBalance.Location = New System.Drawing.Point(14, 247)
        Me.chkAutoWhiteBalance.Name = "chkAutoWhiteBalance"
        Me.chkAutoWhiteBalance.Size = New System.Drawing.Size(83, 21)
        Me.chkAutoWhiteBalance.TabIndex = 16
        Me.chkAutoWhiteBalance.Text = "أبيض آلي"
        Me.chkAutoWhiteBalance.UseVisualStyleBackColor = True
        '
        'chkAutoExposure
        '
        Me.chkAutoExposure.AutoSize = True
        Me.chkAutoExposure.Font = New System.Drawing.Font("Tahoma", 8.0!)
        Me.chkAutoExposure.Location = New System.Drawing.Point(116, 247)
        Me.chkAutoExposure.Name = "chkAutoExposure"
        Me.chkAutoExposure.Size = New System.Drawing.Size(83, 21)
        Me.chkAutoExposure.TabIndex = 15
        Me.chkAutoExposure.Text = "تعريض آلي"
        Me.chkAutoExposure.UseVisualStyleBackColor = True
        '
        'chkAutoFocus
        '
        Me.chkAutoFocus.AutoSize = True
        Me.chkAutoFocus.Font = New System.Drawing.Font("Tahoma", 8.0!)
        Me.chkAutoFocus.Location = New System.Drawing.Point(220, 247)
        Me.chkAutoFocus.Name = "chkAutoFocus"
        Me.chkAutoFocus.Size = New System.Drawing.Size(81, 21)
        Me.chkAutoFocus.TabIndex = 14
        Me.chkAutoFocus.Text = "تركيز آلي"
        Me.chkAutoFocus.UseVisualStyleBackColor = True
        '
        'lblWhiteBalance
        '
        Me.lblWhiteBalance.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblWhiteBalance.Location = New System.Drawing.Point(252, 214)
        Me.lblWhiteBalance.Name = "lblWhiteBalance"
        Me.lblWhiteBalance.Size = New System.Drawing.Size(100, 24)
        Me.lblWhiteBalance.TabIndex = 13
        Me.lblWhiteBalance.Text = "توازن الأبيض"
        Me.lblWhiteBalance.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'trackWhiteBalance
        '
        Me.trackWhiteBalance.Enabled = False
        Me.trackWhiteBalance.Location = New System.Drawing.Point(14, 210)
        Me.trackWhiteBalance.Maximum = 100
        Me.trackWhiteBalance.Name = "trackWhiteBalance"
        Me.trackWhiteBalance.Size = New System.Drawing.Size(232, 56)
        Me.trackWhiteBalance.TabIndex = 12
        '
        'lblZoom
        '
        Me.lblZoom.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblZoom.Location = New System.Drawing.Point(252, 184)
        Me.lblZoom.Name = "lblZoom"
        Me.lblZoom.Size = New System.Drawing.Size(100, 24)
        Me.lblZoom.TabIndex = 11
        Me.lblZoom.Text = "التقريب"
        Me.lblZoom.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'trackZoom
        '
        Me.trackZoom.Enabled = False
        Me.trackZoom.Location = New System.Drawing.Point(14, 180)
        Me.trackZoom.Maximum = 100
        Me.trackZoom.Name = "trackZoom"
        Me.trackZoom.Size = New System.Drawing.Size(232, 56)
        Me.trackZoom.TabIndex = 10
        '
        'lblFocus
        '
        Me.lblFocus.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblFocus.Location = New System.Drawing.Point(252, 154)
        Me.lblFocus.Name = "lblFocus"
        Me.lblFocus.Size = New System.Drawing.Size(100, 24)
        Me.lblFocus.TabIndex = 9
        Me.lblFocus.Text = "التركيز"
        Me.lblFocus.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'trackFocus
        '
        Me.trackFocus.Enabled = False
        Me.trackFocus.Location = New System.Drawing.Point(14, 150)
        Me.trackFocus.Maximum = 100
        Me.trackFocus.Name = "trackFocus"
        Me.trackFocus.Size = New System.Drawing.Size(232, 56)
        Me.trackFocus.TabIndex = 8
        '
        'lblExposure
        '
        Me.lblExposure.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblExposure.Location = New System.Drawing.Point(252, 124)
        Me.lblExposure.Name = "lblExposure"
        Me.lblExposure.Size = New System.Drawing.Size(100, 24)
        Me.lblExposure.TabIndex = 7
        Me.lblExposure.Text = "التعريض"
        Me.lblExposure.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'trackExposure
        '
        Me.trackExposure.Enabled = False
        Me.trackExposure.Location = New System.Drawing.Point(14, 120)
        Me.trackExposure.Maximum = 100
        Me.trackExposure.Name = "trackExposure"
        Me.trackExposure.Size = New System.Drawing.Size(232, 56)
        Me.trackExposure.TabIndex = 6
        '
        'lblSharpness
        '
        Me.lblSharpness.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblSharpness.Location = New System.Drawing.Point(252, 94)
        Me.lblSharpness.Name = "lblSharpness"
        Me.lblSharpness.Size = New System.Drawing.Size(100, 24)
        Me.lblSharpness.TabIndex = 5
        Me.lblSharpness.Text = "الحدة"
        Me.lblSharpness.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'trackSharpness
        '
        Me.trackSharpness.Enabled = False
        Me.trackSharpness.Location = New System.Drawing.Point(14, 90)
        Me.trackSharpness.Maximum = 100
        Me.trackSharpness.Name = "trackSharpness"
        Me.trackSharpness.Size = New System.Drawing.Size(232, 56)
        Me.trackSharpness.TabIndex = 4
        '
        'lblContrast
        '
        Me.lblContrast.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblContrast.Location = New System.Drawing.Point(252, 64)
        Me.lblContrast.Name = "lblContrast"
        Me.lblContrast.Size = New System.Drawing.Size(100, 24)
        Me.lblContrast.TabIndex = 3
        Me.lblContrast.Text = "التباين"
        Me.lblContrast.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'trackContrast
        '
        Me.trackContrast.Enabled = False
        Me.trackContrast.Location = New System.Drawing.Point(14, 60)
        Me.trackContrast.Maximum = 100
        Me.trackContrast.Name = "trackContrast"
        Me.trackContrast.Size = New System.Drawing.Size(232, 56)
        Me.trackContrast.TabIndex = 2
        '
        'lblBrightness
        '
        Me.lblBrightness.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblBrightness.Location = New System.Drawing.Point(252, 34)
        Me.lblBrightness.Name = "lblBrightness"
        Me.lblBrightness.Size = New System.Drawing.Size(100, 24)
        Me.lblBrightness.TabIndex = 1
        Me.lblBrightness.Text = "السطوع"
        Me.lblBrightness.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'trackBrightness
        '
        Me.trackBrightness.Enabled = False
        Me.trackBrightness.Location = New System.Drawing.Point(14, 30)
        Me.trackBrightness.Maximum = 100
        Me.trackBrightness.Name = "trackBrightness"
        Me.trackBrightness.Size = New System.Drawing.Size(232, 56)
        Me.trackBrightness.TabIndex = 0
        '
        'groupOutput
        '
        Me.groupOutput.Controls.Add(Me.btnOpenFolder)
        Me.groupOutput.Controls.Add(Me.numJpegQuality)
        Me.groupOutput.Controls.Add(Me.lblQuality)
        Me.groupOutput.Controls.Add(Me.txtSaveFolder)
        Me.groupOutput.Controls.Add(Me.lblFolder)
        Me.groupOutput.Dock = System.Windows.Forms.DockStyle.Fill
        Me.groupOutput.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.groupOutput.Location = New System.Drawing.Point(16, 552)
        Me.groupOutput.Name = "groupOutput"
        Me.groupOutput.Padding = New System.Windows.Forms.Padding(12)
        Me.groupOutput.Size = New System.Drawing.Size(368, 96)
        Me.groupOutput.TabIndex = 3
        Me.groupOutput.TabStop = False
        Me.groupOutput.Text = "الحفظ والجودة"
        '
        'btnOpenFolder
        '
        Me.btnOpenFolder.BackColor = System.Drawing.Color.WhiteSmoke
        Me.btnOpenFolder.FlatAppearance.BorderSize = 0
        Me.btnOpenFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOpenFolder.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.btnOpenFolder.Location = New System.Drawing.Point(14, 57)
        Me.btnOpenFolder.Name = "btnOpenFolder"
        Me.btnOpenFolder.Size = New System.Drawing.Size(92, 27)
        Me.btnOpenFolder.TabIndex = 4
        Me.btnOpenFolder.Text = "فتح المجلد"
        Me.btnOpenFolder.UseVisualStyleBackColor = False
        '
        'numJpegQuality
        '
        Me.numJpegQuality.Font = New System.Drawing.Font("Tahoma", 8.0!)
        Me.numJpegQuality.Location = New System.Drawing.Point(112, 59)
        Me.numJpegQuality.Maximum = New Decimal(New Integer() {100, 0, 0, 0})
        Me.numJpegQuality.Minimum = New Decimal(New Integer() {40, 0, 0, 0})
        Me.numJpegQuality.Name = "numJpegQuality"
        Me.numJpegQuality.Size = New System.Drawing.Size(58, 24)
        Me.numJpegQuality.TabIndex = 3
        Me.numJpegQuality.Value = New Decimal(New Integer() {92, 0, 0, 0})
        '
        'lblQuality
        '
        Me.lblQuality.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblQuality.Location = New System.Drawing.Point(176, 58)
        Me.lblQuality.Name = "lblQuality"
        Me.lblQuality.Size = New System.Drawing.Size(176, 24)
        Me.lblQuality.TabIndex = 2
        Me.lblQuality.Text = "جودة JPEG"
        Me.lblQuality.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtSaveFolder
        '
        Me.txtSaveFolder.BackColor = System.Drawing.Color.White
        Me.txtSaveFolder.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.txtSaveFolder.Font = New System.Drawing.Font("Tahoma", 8.0!)
        Me.txtSaveFolder.Location = New System.Drawing.Point(14, 27)
        Me.txtSaveFolder.Name = "txtSaveFolder"
        Me.txtSaveFolder.ReadOnly = True
        Me.txtSaveFolder.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.txtSaveFolder.Size = New System.Drawing.Size(238, 24)
        Me.txtSaveFolder.TabIndex = 1
        '
        'lblFolder
        '
        Me.lblFolder.Font = New System.Drawing.Font("Tahoma", 8.0!, System.Drawing.FontStyle.Bold)
        Me.lblFolder.Location = New System.Drawing.Point(258, 27)
        Me.lblFolder.Name = "lblFolder"
        Me.lblFolder.Size = New System.Drawing.Size(94, 24)
        Me.lblFolder.TabIndex = 0
        Me.lblFolder.Text = "مجلد اللقطات"
        Me.lblFolder.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'mainPanel
        '
        Me.mainPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(244, Byte), Integer), CType(CType(247, Byte), Integer), CType(CType(246, Byte), Integer))
        Me.mainPanel.Controls.Add(Me.picturePreview)
        Me.mainPanel.Controls.Add(Me.bottomPanel)
        Me.mainPanel.Controls.Add(Me.lblStatus)
        Me.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.mainPanel.Location = New System.Drawing.Point(0, 96)
        Me.mainPanel.Name = "mainPanel"
        Me.mainPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.mainPanel.Size = New System.Drawing.Size(840, 664)
        Me.mainPanel.TabIndex = 2
        '
        'picturePreview
        '
        Me.picturePreview.BackColor = System.Drawing.Color.FromArgb(CType(CType(25, Byte), Integer), CType(CType(32, Byte), Integer), CType(CType(30, Byte), Integer))
        Me.picturePreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.picturePreview.Dock = System.Windows.Forms.DockStyle.Fill
        Me.picturePreview.Location = New System.Drawing.Point(18, 18)
        Me.picturePreview.Name = "picturePreview"
        Me.picturePreview.Size = New System.Drawing.Size(804, 419)
        Me.picturePreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
        Me.picturePreview.TabIndex = 0
        Me.picturePreview.TabStop = False
        '
        'bottomPanel
        '
        Me.bottomPanel.Controls.Add(Me.lstCaptures)
        Me.bottomPanel.Controls.Add(Me.captureButtonsPanel)
        Me.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.bottomPanel.Location = New System.Drawing.Point(18, 437)
        Me.bottomPanel.Name = "bottomPanel"
        Me.bottomPanel.Padding = New System.Windows.Forms.Padding(0, 12, 0, 0)
        Me.bottomPanel.Size = New System.Drawing.Size(804, 176)
        Me.bottomPanel.TabIndex = 1
        '
        'lstCaptures
        '
        Me.lstCaptures.BackColor = System.Drawing.Color.White
        Me.lstCaptures.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.lstCaptures.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lstCaptures.Font = New System.Drawing.Font("Tahoma", 8.5!)
        Me.lstCaptures.FormattingEnabled = True
        Me.lstCaptures.ItemHeight = 17
        Me.lstCaptures.Location = New System.Drawing.Point(0, 12)
        Me.lstCaptures.Name = "lstCaptures"
        Me.lstCaptures.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.lstCaptures.Size = New System.Drawing.Size(804, 110)
        Me.lstCaptures.TabIndex = 0
        '
        'captureButtonsPanel
        '
        Me.captureButtonsPanel.Controls.Add(Me.btnCancel)
        Me.captureButtonsPanel.Controls.Add(Me.btnAccept)
        Me.captureButtonsPanel.Controls.Add(Me.btnRemoveCapture)
        Me.captureButtonsPanel.Controls.Add(Me.btnCapture)
        Me.captureButtonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.captureButtonsPanel.Location = New System.Drawing.Point(0, 122)
        Me.captureButtonsPanel.Name = "captureButtonsPanel"
        Me.captureButtonsPanel.Size = New System.Drawing.Size(804, 54)
        Me.captureButtonsPanel.TabIndex = 1
        '
        'btnCancel
        '
        Me.btnCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnCancel.BackColor = System.Drawing.Color.WhiteSmoke
        Me.btnCancel.FlatAppearance.BorderSize = 0
        Me.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnCancel.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnCancel.Location = New System.Drawing.Point(0, 8)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(120, 38)
        Me.btnCancel.TabIndex = 3
        Me.btnCancel.Text = "إلغاء"
        Me.btnCancel.UseVisualStyleBackColor = False
        '
        'btnAccept
        '
        Me.btnAccept.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnAccept.BackColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.btnAccept.FlatAppearance.BorderSize = 0
        Me.btnAccept.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAccept.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnAccept.ForeColor = System.Drawing.Color.White
        Me.btnAccept.Location = New System.Drawing.Point(546, 8)
        Me.btnAccept.Name = "btnAccept"
        Me.btnAccept.Size = New System.Drawing.Size(258, 38)
        Me.btnAccept.TabIndex = 2
        Me.btnAccept.Text = "اعتماد وإرسال لمعالج الأرشفة"
        Me.btnAccept.UseVisualStyleBackColor = False
        '
        'btnRemoveCapture
        '
        Me.btnRemoveCapture.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnRemoveCapture.BackColor = System.Drawing.Color.WhiteSmoke
        Me.btnRemoveCapture.FlatAppearance.BorderSize = 0
        Me.btnRemoveCapture.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnRemoveCapture.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnRemoveCapture.Location = New System.Drawing.Point(420, 8)
        Me.btnRemoveCapture.Name = "btnRemoveCapture"
        Me.btnRemoveCapture.Size = New System.Drawing.Size(120, 38)
        Me.btnRemoveCapture.TabIndex = 1
        Me.btnRemoveCapture.Text = "حذف لقطة"
        Me.btnRemoveCapture.UseVisualStyleBackColor = False
        '
        'btnCapture
        '
        Me.btnCapture.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnCapture.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.btnCapture.Enabled = False
        Me.btnCapture.FlatAppearance.BorderSize = 0
        Me.btnCapture.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnCapture.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnCapture.ForeColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.btnCapture.Location = New System.Drawing.Point(294, 8)
        Me.btnCapture.Name = "btnCapture"
        Me.btnCapture.Size = New System.Drawing.Size(120, 38)
        Me.btnCapture.TabIndex = 0
        Me.btnCapture.Text = "التقاط صفحة"
        Me.btnCapture.UseVisualStyleBackColor = False
        '
        'lblStatus
        '
        Me.lblStatus.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblStatus.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.lblStatus.ForeColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.lblStatus.Location = New System.Drawing.Point(18, 613)
        Me.lblStatus.Name = "lblStatus"
        Me.lblStatus.Size = New System.Drawing.Size(804, 33)
        Me.lblStatus.TabIndex = 2
        Me.lblStatus.Text = "جاهز"
        Me.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'CameraImportForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1240, 760)
        Me.Controls.Add(Me.mainPanel)
        Me.Controls.Add(Me.rightPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(1120, 700)
        Me.Name = "CameraImportForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "إدراج وثائق بالكاميرا"
        Me.WindowState = System.Windows.Forms.FormWindowState.Maximized
        Me.headerPanel.ResumeLayout(False)
        Me.rightPanel.ResumeLayout(False)
        Me.groupDevice.ResumeLayout(False)
        Me.groupControls.ResumeLayout(False)
        Me.groupControls.PerformLayout()
        CType(Me.trackWhiteBalance, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trackZoom, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trackFocus, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trackExposure, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trackSharpness, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trackContrast, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.trackBrightness, System.ComponentModel.ISupportInitialize).EndInit()
        Me.groupOutput.ResumeLayout(False)
        Me.groupOutput.PerformLayout()
        CType(Me.numJpegQuality, System.ComponentModel.ISupportInitialize).EndInit()
        Me.mainPanel.ResumeLayout(False)
        CType(Me.picturePreview, System.ComponentModel.ISupportInitialize).EndInit()
        Me.bottomPanel.ResumeLayout(False)
        Me.captureButtonsPanel.ResumeLayout(False)
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblSubtitle As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents rightPanel As System.Windows.Forms.Panel
    Friend WithEvents lblSettingsTitle As System.Windows.Forms.Label
    Friend WithEvents groupDevice As System.Windows.Forms.GroupBox
    Friend WithEvents btnCrossbar As System.Windows.Forms.Button
    Friend WithEvents btnNativeSettings As System.Windows.Forms.Button
    Friend WithEvents btnStop As System.Windows.Forms.Button
    Friend WithEvents btnStart As System.Windows.Forms.Button
    Friend WithEvents btnRefresh As System.Windows.Forms.Button
    Friend WithEvents cboResolution As System.Windows.Forms.ComboBox
    Friend WithEvents lblResolution As System.Windows.Forms.Label
    Friend WithEvents cboCameras As System.Windows.Forms.ComboBox
    Friend WithEvents lblCamera As System.Windows.Forms.Label
    Friend WithEvents groupControls As System.Windows.Forms.GroupBox
    Friend WithEvents btnApplyProps As System.Windows.Forms.Button
    Friend WithEvents chkAutoWhiteBalance As System.Windows.Forms.CheckBox
    Friend WithEvents chkAutoExposure As System.Windows.Forms.CheckBox
    Friend WithEvents chkAutoFocus As System.Windows.Forms.CheckBox
    Friend WithEvents lblWhiteBalance As System.Windows.Forms.Label
    Friend WithEvents trackWhiteBalance As System.Windows.Forms.TrackBar
    Friend WithEvents lblZoom As System.Windows.Forms.Label
    Friend WithEvents trackZoom As System.Windows.Forms.TrackBar
    Friend WithEvents lblFocus As System.Windows.Forms.Label
    Friend WithEvents trackFocus As System.Windows.Forms.TrackBar
    Friend WithEvents lblExposure As System.Windows.Forms.Label
    Friend WithEvents trackExposure As System.Windows.Forms.TrackBar
    Friend WithEvents lblSharpness As System.Windows.Forms.Label
    Friend WithEvents trackSharpness As System.Windows.Forms.TrackBar
    Friend WithEvents lblContrast As System.Windows.Forms.Label
    Friend WithEvents trackContrast As System.Windows.Forms.TrackBar
    Friend WithEvents lblBrightness As System.Windows.Forms.Label
    Friend WithEvents trackBrightness As System.Windows.Forms.TrackBar
    Friend WithEvents groupOutput As System.Windows.Forms.GroupBox
    Friend WithEvents btnOpenFolder As System.Windows.Forms.Button
    Friend WithEvents numJpegQuality As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblQuality As System.Windows.Forms.Label
    Friend WithEvents txtSaveFolder As System.Windows.Forms.TextBox
    Friend WithEvents lblFolder As System.Windows.Forms.Label
    Friend WithEvents mainPanel As System.Windows.Forms.Panel
    Friend WithEvents picturePreview As System.Windows.Forms.PictureBox
    Friend WithEvents bottomPanel As System.Windows.Forms.Panel
    Friend WithEvents lstCaptures As System.Windows.Forms.ListBox
    Friend WithEvents captureButtonsPanel As System.Windows.Forms.Panel
    Friend WithEvents btnCancel As System.Windows.Forms.Button
    Friend WithEvents btnAccept As System.Windows.Forms.Button
    Friend WithEvents btnRemoveCapture As System.Windows.Forms.Button
    Friend WithEvents btnCapture As System.Windows.Forms.Button
    Friend WithEvents lblStatus As System.Windows.Forms.Label
End Class
