<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class PdfOcrCenterForm
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
        Me.leftPanel = New System.Windows.Forms.Panel()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.btnSaveSettings = New System.Windows.Forms.Button()
        Me.btnDiagnose = New System.Windows.Forms.Button()
        Me.btnRun = New System.Windows.Forms.Button()
        Me.chkPageHeaders = New System.Windows.Forms.CheckBox()
        Me.chkForceOcr = New System.Windows.Forms.CheckBox()
        Me.chkDirectText = New System.Windows.Forms.CheckBox()
        Me.nudLastPage = New System.Windows.Forms.NumericUpDown()
        Me.lblLastPage = New System.Windows.Forms.Label()
        Me.nudFirstPage = New System.Windows.Forms.NumericUpDown()
        Me.lblFirstPage = New System.Windows.Forms.Label()
        Me.nudMaxPages = New System.Windows.Forms.NumericUpDown()
        Me.lblMaxPages = New System.Windows.Forms.Label()
        Me.nudDpi = New System.Windows.Forms.NumericUpDown()
        Me.lblDpi = New System.Windows.Forms.Label()
        Me.btnBrowseGhostscript = New System.Windows.Forms.Button()
        Me.txtGhostscript = New System.Windows.Forms.TextBox()
        Me.lblGhostscript = New System.Windows.Forms.Label()
        Me.btnBrowsePdfToText = New System.Windows.Forms.Button()
        Me.txtPdfToText = New System.Windows.Forms.TextBox()
        Me.lblPdfToText = New System.Windows.Forms.Label()
        Me.btnBrowsePdfToPpm = New System.Windows.Forms.Button()
        Me.txtPdfToPpm = New System.Windows.Forms.TextBox()
        Me.lblPdfToPpm = New System.Windows.Forms.Label()
        Me.btnBrowseTess = New System.Windows.Forms.Button()
        Me.txtTesseract = New System.Windows.Forms.TextBox()
        Me.lblTesseract = New System.Windows.Forms.Label()
        Me.btnBrowsePdf = New System.Windows.Forms.Button()
        Me.txtPdfPath = New System.Windows.Forms.TextBox()
        Me.lblPdfPath = New System.Windows.Forms.Label()
        Me.resultPanel = New System.Windows.Forms.Panel()
        Me.splitContainer = New System.Windows.Forms.SplitContainer()
        Me.txtExtractedText = New System.Windows.Forms.TextBox()
        Me.resultHeaderPanel = New System.Windows.Forms.Panel()
        Me.btnSaveText = New System.Windows.Forms.Button()
        Me.btnCopyText = New System.Windows.Forms.Button()
        Me.lblExtractedText = New System.Windows.Forms.Label()
        Me.txtReport = New System.Windows.Forms.TextBox()
        Me.lblReport = New System.Windows.Forms.Label()
        Me.headerPanel.SuspendLayout()
        Me.leftPanel.SuspendLayout()
        CType(Me.nudLastPage, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudFirstPage, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudMaxPages, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudDpi, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.resultPanel.SuspendLayout()
        CType(Me.splitContainer, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.splitContainer.Panel1.SuspendLayout()
        Me.splitContainer.Panel2.SuspendLayout()
        Me.splitContainer.SuspendLayout()
        Me.resultHeaderPanel.SuspendLayout()
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
        Me.headerPanel.Padding = New System.Windows.Forms.Padding(18)
        Me.headerPanel.Size = New System.Drawing.Size(1280, 82)
        Me.headerPanel.TabIndex = 0
        '
        'lblSubtitle
        '
        Me.lblSubtitle.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.lblSubtitle.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(229, Byte), Integer), CType(CType(219, Byte), Integer), CType(CType(190, Byte), Integer))
        Me.lblSubtitle.Location = New System.Drawing.Point(18, 47)
        Me.lblSubtitle.Name = "lblSubtitle"
        Me.lblSubtitle.Size = New System.Drawing.Size(1244, 17)
        Me.lblSubtitle.TabIndex = 1
        Me.lblSubtitle.Text = "يدعم PDF النصي والمصور: pdftotext للنص المباشر، وpdftoppm/Ghostscript لتحويل الصفحات، ثم Tesseract OCR عربي/إنجليزي."
        '
        'lblTitle
        '
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 16.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(18, 18)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(1244, 31)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "مركز OCR الاحترافي لملفات PDF"
        '
        'leftPanel
        '
        Me.leftPanel.BackColor = System.Drawing.Color.FromArgb(CType(CType(246, Byte), Integer), CType(CType(248, Byte), Integer), CType(CType(247, Byte), Integer))
        Me.leftPanel.Controls.Add(Me.btnClose)
        Me.leftPanel.Controls.Add(Me.btnSaveSettings)
        Me.leftPanel.Controls.Add(Me.btnDiagnose)
        Me.leftPanel.Controls.Add(Me.btnRun)
        Me.leftPanel.Controls.Add(Me.chkPageHeaders)
        Me.leftPanel.Controls.Add(Me.chkForceOcr)
        Me.leftPanel.Controls.Add(Me.chkDirectText)
        Me.leftPanel.Controls.Add(Me.nudLastPage)
        Me.leftPanel.Controls.Add(Me.lblLastPage)
        Me.leftPanel.Controls.Add(Me.nudFirstPage)
        Me.leftPanel.Controls.Add(Me.lblFirstPage)
        Me.leftPanel.Controls.Add(Me.nudMaxPages)
        Me.leftPanel.Controls.Add(Me.lblMaxPages)
        Me.leftPanel.Controls.Add(Me.nudDpi)
        Me.leftPanel.Controls.Add(Me.lblDpi)
        Me.leftPanel.Controls.Add(Me.btnBrowseGhostscript)
        Me.leftPanel.Controls.Add(Me.txtGhostscript)
        Me.leftPanel.Controls.Add(Me.lblGhostscript)
        Me.leftPanel.Controls.Add(Me.btnBrowsePdfToText)
        Me.leftPanel.Controls.Add(Me.txtPdfToText)
        Me.leftPanel.Controls.Add(Me.lblPdfToText)
        Me.leftPanel.Controls.Add(Me.btnBrowsePdfToPpm)
        Me.leftPanel.Controls.Add(Me.txtPdfToPpm)
        Me.leftPanel.Controls.Add(Me.lblPdfToPpm)
        Me.leftPanel.Controls.Add(Me.btnBrowseTess)
        Me.leftPanel.Controls.Add(Me.txtTesseract)
        Me.leftPanel.Controls.Add(Me.lblTesseract)
        Me.leftPanel.Controls.Add(Me.btnBrowsePdf)
        Me.leftPanel.Controls.Add(Me.txtPdfPath)
        Me.leftPanel.Controls.Add(Me.lblPdfPath)
        Me.leftPanel.Dock = System.Windows.Forms.DockStyle.Right
        Me.leftPanel.Location = New System.Drawing.Point(852, 82)
        Me.leftPanel.Name = "leftPanel"
        Me.leftPanel.Padding = New System.Windows.Forms.Padding(16)
        Me.leftPanel.Size = New System.Drawing.Size(428, 658)
        Me.leftPanel.TabIndex = 1
        '
        'btnClose
        '
        Me.btnClose.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnClose.BackColor = System.Drawing.Color.FromArgb(CType(CType(229, Byte), Integer), CType(CType(232, Byte), Integer), CType(CType(229, Byte), Integer))
        Me.btnClose.FlatAppearance.BorderSize = 0
        Me.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnClose.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnClose.Location = New System.Drawing.Point(16, 600)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(126, 42)
        Me.btnClose.TabIndex = 29
        Me.btnClose.Text = "إغلاق"
        Me.btnClose.UseVisualStyleBackColor = False
        '
        'btnSaveSettings
        '
        Me.btnSaveSettings.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnSaveSettings.BackColor = System.Drawing.Color.FromArgb(CType(CType(236, Byte), Integer), CType(CType(242, Byte), Integer), CType(CType(240, Byte), Integer))
        Me.btnSaveSettings.FlatAppearance.BorderSize = 0
        Me.btnSaveSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSaveSettings.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnSaveSettings.Location = New System.Drawing.Point(148, 600)
        Me.btnSaveSettings.Name = "btnSaveSettings"
        Me.btnSaveSettings.Size = New System.Drawing.Size(122, 42)
        Me.btnSaveSettings.TabIndex = 28
        Me.btnSaveSettings.Text = "حفظ الإعدادات"
        Me.btnSaveSettings.UseVisualStyleBackColor = False
        '
        'btnDiagnose
        '
        Me.btnDiagnose.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnDiagnose.BackColor = System.Drawing.Color.FromArgb(CType(CType(35, Byte), Integer), CType(CType(84, Byte), Integer), CType(CType(78, Byte), Integer))
        Me.btnDiagnose.FlatAppearance.BorderSize = 0
        Me.btnDiagnose.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnDiagnose.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnDiagnose.ForeColor = System.Drawing.Color.White
        Me.btnDiagnose.Location = New System.Drawing.Point(276, 600)
        Me.btnDiagnose.Name = "btnDiagnose"
        Me.btnDiagnose.Size = New System.Drawing.Size(136, 42)
        Me.btnDiagnose.TabIndex = 27
        Me.btnDiagnose.Text = "تشخيص الأدوات"
        Me.btnDiagnose.UseVisualStyleBackColor = False
        '
        'btnRun
        '
        Me.btnRun.BackColor = System.Drawing.Color.FromArgb(CType(CType(214, Byte), Integer), CType(CType(176, Byte), Integer), CType(CType(92, Byte), Integer))
        Me.btnRun.FlatAppearance.BorderSize = 0
        Me.btnRun.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnRun.Font = New System.Drawing.Font("Tahoma", 10.0!, System.Drawing.FontStyle.Bold)
        Me.btnRun.Location = New System.Drawing.Point(16, 541)
        Me.btnRun.Name = "btnRun"
        Me.btnRun.Size = New System.Drawing.Size(396, 46)
        Me.btnRun.TabIndex = 26
        Me.btnRun.Text = "تشغيل PDF OCR الآن"
        Me.btnRun.UseVisualStyleBackColor = False
        '
        'chkPageHeaders
        '
        Me.chkPageHeaders.AutoSize = True
        Me.chkPageHeaders.Checked = True
        Me.chkPageHeaders.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkPageHeaders.Location = New System.Drawing.Point(197, 511)
        Me.chkPageHeaders.Name = "chkPageHeaders"
        Me.chkPageHeaders.Size = New System.Drawing.Size(215, 22)
        Me.chkPageHeaders.TabIndex = 25
        Me.chkPageHeaders.Text = "إضافة عنوان لكل صفحة في النص"
        Me.chkPageHeaders.UseVisualStyleBackColor = True
        '
        'chkForceOcr
        '
        Me.chkForceOcr.AutoSize = True
        Me.chkForceOcr.Location = New System.Drawing.Point(201, 483)
        Me.chkForceOcr.Name = "chkForceOcr"
        Me.chkForceOcr.Size = New System.Drawing.Size(211, 22)
        Me.chkForceOcr.TabIndex = 24
        Me.chkForceOcr.Text = "إجبار OCR حتى لو كان PDF نصي"
        Me.chkForceOcr.UseVisualStyleBackColor = True
        '
        'chkDirectText
        '
        Me.chkDirectText.AutoSize = True
        Me.chkDirectText.Checked = True
        Me.chkDirectText.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkDirectText.Location = New System.Drawing.Point(179, 455)
        Me.chkDirectText.Name = "chkDirectText"
        Me.chkDirectText.Size = New System.Drawing.Size(233, 22)
        Me.chkDirectText.TabIndex = 23
        Me.chkDirectText.Text = "جرّب قراءة النص المباشر أولاً"
        Me.chkDirectText.UseVisualStyleBackColor = True
        '
        'nudLastPage
        '
        Me.nudLastPage.Location = New System.Drawing.Point(16, 421)
        Me.nudLastPage.Maximum = New Decimal(New Integer() {500, 0, 0, 0})
        Me.nudLastPage.Name = "nudLastPage"
        Me.nudLastPage.Size = New System.Drawing.Size(116, 26)
        Me.nudLastPage.TabIndex = 22
        '
        'lblLastPage
        '
        Me.lblLastPage.Location = New System.Drawing.Point(138, 421)
        Me.lblLastPage.Name = "lblLastPage"
        Me.lblLastPage.Size = New System.Drawing.Size(274, 24)
        Me.lblLastPage.TabIndex = 21
        Me.lblLastPage.Text = "آخر صفحة (0 = حتى النهاية)"
        '
        'nudFirstPage
        '
        Me.nudFirstPage.Location = New System.Drawing.Point(16, 389)
        Me.nudFirstPage.Maximum = New Decimal(New Integer() {500, 0, 0, 0})
        Me.nudFirstPage.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudFirstPage.Name = "nudFirstPage"
        Me.nudFirstPage.Size = New System.Drawing.Size(116, 26)
        Me.nudFirstPage.TabIndex = 20
        Me.nudFirstPage.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'lblFirstPage
        '
        Me.lblFirstPage.Location = New System.Drawing.Point(138, 389)
        Me.lblFirstPage.Name = "lblFirstPage"
        Me.lblFirstPage.Size = New System.Drawing.Size(274, 24)
        Me.lblFirstPage.TabIndex = 19
        Me.lblFirstPage.Text = "أول صفحة"
        '
        'nudMaxPages
        '
        Me.nudMaxPages.Location = New System.Drawing.Point(16, 357)
        Me.nudMaxPages.Maximum = New Decimal(New Integer() {500, 0, 0, 0})
        Me.nudMaxPages.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudMaxPages.Name = "nudMaxPages"
        Me.nudMaxPages.Size = New System.Drawing.Size(116, 26)
        Me.nudMaxPages.TabIndex = 18
        Me.nudMaxPages.Value = New Decimal(New Integer() {100, 0, 0, 0})
        '
        'lblMaxPages
        '
        Me.lblMaxPages.Location = New System.Drawing.Point(138, 357)
        Me.lblMaxPages.Name = "lblMaxPages"
        Me.lblMaxPages.Size = New System.Drawing.Size(274, 24)
        Me.lblMaxPages.TabIndex = 17
        Me.lblMaxPages.Text = "حد الصفحات للدفعة الواحدة"
        '
        'nudDpi
        '
        Me.nudDpi.Increment = New Decimal(New Integer() {50, 0, 0, 0})
        Me.nudDpi.Location = New System.Drawing.Point(16, 325)
        Me.nudDpi.Maximum = New Decimal(New Integer() {600, 0, 0, 0})
        Me.nudDpi.Minimum = New Decimal(New Integer() {150, 0, 0, 0})
        Me.nudDpi.Name = "nudDpi"
        Me.nudDpi.Size = New System.Drawing.Size(116, 26)
        Me.nudDpi.TabIndex = 16
        Me.nudDpi.Value = New Decimal(New Integer() {300, 0, 0, 0})
        '
        'lblDpi
        '
        Me.lblDpi.Location = New System.Drawing.Point(138, 325)
        Me.lblDpi.Name = "lblDpi"
        Me.lblDpi.Size = New System.Drawing.Size(274, 24)
        Me.lblDpi.TabIndex = 15
        Me.lblDpi.Text = "دقة تحويل PDF إلى صور DPI"
        '
        'btnBrowseGhostscript
        '
        Me.btnBrowseGhostscript.Location = New System.Drawing.Point(16, 282)
        Me.btnBrowseGhostscript.Name = "btnBrowseGhostscript"
        Me.btnBrowseGhostscript.Size = New System.Drawing.Size(72, 30)
        Me.btnBrowseGhostscript.TabIndex = 14
        Me.btnBrowseGhostscript.Text = "استعراض"
        Me.btnBrowseGhostscript.UseVisualStyleBackColor = True
        '
        'txtGhostscript
        '
        Me.txtGhostscript.Location = New System.Drawing.Point(94, 284)
        Me.txtGhostscript.Name = "txtGhostscript"
        Me.txtGhostscript.Size = New System.Drawing.Size(318, 26)
        Me.txtGhostscript.TabIndex = 13
        '
        'lblGhostscript
        '
        Me.lblGhostscript.Location = New System.Drawing.Point(16, 257)
        Me.lblGhostscript.Name = "lblGhostscript"
        Me.lblGhostscript.Size = New System.Drawing.Size(396, 24)
        Me.lblGhostscript.TabIndex = 12
        Me.lblGhostscript.Text = "Ghostscript gswin64c.exe - بديل عند عدم توفر Poppler"
        '
        'btnBrowsePdfToText
        '
        Me.btnBrowsePdfToText.Location = New System.Drawing.Point(16, 217)
        Me.btnBrowsePdfToText.Name = "btnBrowsePdfToText"
        Me.btnBrowsePdfToText.Size = New System.Drawing.Size(72, 30)
        Me.btnBrowsePdfToText.TabIndex = 11
        Me.btnBrowsePdfToText.Text = "استعراض"
        Me.btnBrowsePdfToText.UseVisualStyleBackColor = True
        '
        'txtPdfToText
        '
        Me.txtPdfToText.Location = New System.Drawing.Point(94, 219)
        Me.txtPdfToText.Name = "txtPdfToText"
        Me.txtPdfToText.Size = New System.Drawing.Size(318, 26)
        Me.txtPdfToText.TabIndex = 10
        '
        'lblPdfToText
        '
        Me.lblPdfToText.Location = New System.Drawing.Point(16, 192)
        Me.lblPdfToText.Name = "lblPdfToText"
        Me.lblPdfToText.Size = New System.Drawing.Size(396, 24)
        Me.lblPdfToText.TabIndex = 9
        Me.lblPdfToText.Text = "Poppler pdftotext.exe - قراءة PDF النصي بسرعة"
        '
        'btnBrowsePdfToPpm
        '
        Me.btnBrowsePdfToPpm.Location = New System.Drawing.Point(16, 152)
        Me.btnBrowsePdfToPpm.Name = "btnBrowsePdfToPpm"
        Me.btnBrowsePdfToPpm.Size = New System.Drawing.Size(72, 30)
        Me.btnBrowsePdfToPpm.TabIndex = 8
        Me.btnBrowsePdfToPpm.Text = "استعراض"
        Me.btnBrowsePdfToPpm.UseVisualStyleBackColor = True
        '
        'txtPdfToPpm
        '
        Me.txtPdfToPpm.Location = New System.Drawing.Point(94, 154)
        Me.txtPdfToPpm.Name = "txtPdfToPpm"
        Me.txtPdfToPpm.Size = New System.Drawing.Size(318, 26)
        Me.txtPdfToPpm.TabIndex = 7
        '
        'lblPdfToPpm
        '
        Me.lblPdfToPpm.Location = New System.Drawing.Point(16, 127)
        Me.lblPdfToPpm.Name = "lblPdfToPpm"
        Me.lblPdfToPpm.Size = New System.Drawing.Size(396, 24)
        Me.lblPdfToPpm.TabIndex = 6
        Me.lblPdfToPpm.Text = "Poppler pdftoppm.exe - تحويل PDF المصور إلى صور"
        '
        'btnBrowseTess
        '
        Me.btnBrowseTess.Location = New System.Drawing.Point(16, 88)
        Me.btnBrowseTess.Name = "btnBrowseTess"
        Me.btnBrowseTess.Size = New System.Drawing.Size(72, 30)
        Me.btnBrowseTess.TabIndex = 5
        Me.btnBrowseTess.Text = "استعراض"
        Me.btnBrowseTess.UseVisualStyleBackColor = True
        '
        'txtTesseract
        '
        Me.txtTesseract.Location = New System.Drawing.Point(94, 90)
        Me.txtTesseract.Name = "txtTesseract"
        Me.txtTesseract.Size = New System.Drawing.Size(318, 26)
        Me.txtTesseract.TabIndex = 4
        '
        'lblTesseract
        '
        Me.lblTesseract.Location = New System.Drawing.Point(16, 63)
        Me.lblTesseract.Name = "lblTesseract"
        Me.lblTesseract.Size = New System.Drawing.Size(396, 24)
        Me.lblTesseract.TabIndex = 3
        Me.lblTesseract.Text = "Tesseract OCR"
        '
        'btnBrowsePdf
        '
        Me.btnBrowsePdf.Location = New System.Drawing.Point(16, 25)
        Me.btnBrowsePdf.Name = "btnBrowsePdf"
        Me.btnBrowsePdf.Size = New System.Drawing.Size(72, 30)
        Me.btnBrowsePdf.TabIndex = 2
        Me.btnBrowsePdf.Text = "استعراض"
        Me.btnBrowsePdf.UseVisualStyleBackColor = True
        '
        'txtPdfPath
        '
        Me.txtPdfPath.Location = New System.Drawing.Point(94, 27)
        Me.txtPdfPath.Name = "txtPdfPath"
        Me.txtPdfPath.Size = New System.Drawing.Size(318, 26)
        Me.txtPdfPath.TabIndex = 1
        '
        'lblPdfPath
        '
        Me.lblPdfPath.Location = New System.Drawing.Point(16, 3)
        Me.lblPdfPath.Name = "lblPdfPath"
        Me.lblPdfPath.Size = New System.Drawing.Size(396, 24)
        Me.lblPdfPath.TabIndex = 0
        Me.lblPdfPath.Text = "ملف PDF للتجربة"
        '
        'resultPanel
        '
        Me.resultPanel.Controls.Add(Me.splitContainer)
        Me.resultPanel.Controls.Add(Me.resultHeaderPanel)
        Me.resultPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.resultPanel.Location = New System.Drawing.Point(0, 82)
        Me.resultPanel.Name = "resultPanel"
        Me.resultPanel.Padding = New System.Windows.Forms.Padding(14)
        Me.resultPanel.Size = New System.Drawing.Size(852, 658)
        Me.resultPanel.TabIndex = 2
        '
        'splitContainer
        '
        Me.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill
        Me.splitContainer.Location = New System.Drawing.Point(14, 62)
        Me.splitContainer.Name = "splitContainer"
        Me.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal
        '
        'splitContainer.Panel1
        '
        Me.splitContainer.Panel1.Controls.Add(Me.txtExtractedText)
        '
        'splitContainer.Panel2
        '
        Me.splitContainer.Panel2.Controls.Add(Me.txtReport)
        Me.splitContainer.Panel2.Controls.Add(Me.lblReport)
        Me.splitContainer.Size = New System.Drawing.Size(824, 582)
        Me.splitContainer.SplitterDistance = 346
        Me.splitContainer.TabIndex = 1
        '
        'txtExtractedText
        '
        Me.txtExtractedText.BackColor = System.Drawing.Color.White
        Me.txtExtractedText.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtExtractedText.Font = New System.Drawing.Font("Tahoma", 10.0!)
        Me.txtExtractedText.Location = New System.Drawing.Point(0, 0)
        Me.txtExtractedText.Multiline = True
        Me.txtExtractedText.Name = "txtExtractedText"
        Me.txtExtractedText.ScrollBars = System.Windows.Forms.ScrollBars.Both
        Me.txtExtractedText.Size = New System.Drawing.Size(824, 346)
        Me.txtExtractedText.TabIndex = 0
        '
        'resultHeaderPanel
        '
        Me.resultHeaderPanel.Controls.Add(Me.btnSaveText)
        Me.resultHeaderPanel.Controls.Add(Me.btnCopyText)
        Me.resultHeaderPanel.Controls.Add(Me.lblExtractedText)
        Me.resultHeaderPanel.Dock = System.Windows.Forms.DockStyle.Top
        Me.resultHeaderPanel.Location = New System.Drawing.Point(14, 14)
        Me.resultHeaderPanel.Name = "resultHeaderPanel"
        Me.resultHeaderPanel.Size = New System.Drawing.Size(824, 48)
        Me.resultHeaderPanel.TabIndex = 0
        '
        'btnSaveText
        '
        Me.btnSaveText.Dock = System.Windows.Forms.DockStyle.Left
        Me.btnSaveText.Location = New System.Drawing.Point(0, 0)
        Me.btnSaveText.Name = "btnSaveText"
        Me.btnSaveText.Size = New System.Drawing.Size(120, 48)
        Me.btnSaveText.TabIndex = 2
        Me.btnSaveText.Text = "حفظ TXT"
        Me.btnSaveText.UseVisualStyleBackColor = True
        '
        'btnCopyText
        '
        Me.btnCopyText.Dock = System.Windows.Forms.DockStyle.Left
        Me.btnCopyText.Location = New System.Drawing.Point(120, 0)
        Me.btnCopyText.Name = "btnCopyText"
        Me.btnCopyText.Size = New System.Drawing.Size(120, 48)
        Me.btnCopyText.TabIndex = 1
        Me.btnCopyText.Text = "نسخ النص"
        Me.btnCopyText.UseVisualStyleBackColor = True
        '
        'lblExtractedText
        '
        Me.lblExtractedText.Dock = System.Windows.Forms.DockStyle.Right
        Me.lblExtractedText.Font = New System.Drawing.Font("Tahoma", 12.0!, System.Drawing.FontStyle.Bold)
        Me.lblExtractedText.ForeColor = System.Drawing.Color.FromArgb(CType(CType(22, Byte), Integer), CType(CType(53, Byte), Integer), CType(CType(50, Byte), Integer))
        Me.lblExtractedText.Location = New System.Drawing.Point(454, 0)
        Me.lblExtractedText.Name = "lblExtractedText"
        Me.lblExtractedText.Size = New System.Drawing.Size(370, 48)
        Me.lblExtractedText.TabIndex = 0
        Me.lblExtractedText.Text = "النص المستخرج من PDF"
        Me.lblExtractedText.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtReport
        '
        Me.txtReport.BackColor = System.Drawing.Color.FromArgb(CType(CType(250, Byte), Integer), CType(CType(251, Byte), Integer), CType(CType(250, Byte), Integer))
        Me.txtReport.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtReport.Font = New System.Drawing.Font("Consolas", 9.0!)
        Me.txtReport.Location = New System.Drawing.Point(0, 24)
        Me.txtReport.Multiline = True
        Me.txtReport.Name = "txtReport"
        Me.txtReport.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtReport.Size = New System.Drawing.Size(824, 208)
        Me.txtReport.TabIndex = 1
        '
        'lblReport
        '
        Me.lblReport.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblReport.Font = New System.Drawing.Font("Tahoma", 9.0!, System.Drawing.FontStyle.Bold)
        Me.lblReport.Location = New System.Drawing.Point(0, 0)
        Me.lblReport.Name = "lblReport"
        Me.lblReport.Size = New System.Drawing.Size(824, 24)
        Me.lblReport.TabIndex = 0
        Me.lblReport.Text = "التقرير والتشخيص"
        Me.lblReport.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'PdfOcrCenterForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.ClientSize = New System.Drawing.Size(1280, 740)
        Me.Controls.Add(Me.resultPanel)
        Me.Controls.Add(Me.leftPanel)
        Me.Controls.Add(Me.headerPanel)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(1180, 720)
        Me.Name = "PdfOcrCenterForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "مركز OCR الاحترافي لملفات PDF"
        Me.headerPanel.ResumeLayout(False)
        Me.leftPanel.ResumeLayout(False)
        Me.leftPanel.PerformLayout()
        CType(Me.nudLastPage, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudFirstPage, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudMaxPages, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudDpi, System.ComponentModel.ISupportInitialize).EndInit()
        Me.resultPanel.ResumeLayout(False)
        Me.splitContainer.Panel1.ResumeLayout(False)
        Me.splitContainer.Panel1.PerformLayout()
        Me.splitContainer.Panel2.ResumeLayout(False)
        Me.splitContainer.Panel2.PerformLayout()
        CType(Me.splitContainer, System.ComponentModel.ISupportInitialize).EndInit()
        Me.splitContainer.ResumeLayout(False)
        Me.resultHeaderPanel.ResumeLayout(False)
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents headerPanel As System.Windows.Forms.Panel
    Friend WithEvents lblSubtitle As System.Windows.Forms.Label
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents leftPanel As System.Windows.Forms.Panel
    Friend WithEvents btnClose As System.Windows.Forms.Button
    Friend WithEvents btnSaveSettings As System.Windows.Forms.Button
    Friend WithEvents btnDiagnose As System.Windows.Forms.Button
    Friend WithEvents btnRun As System.Windows.Forms.Button
    Friend WithEvents chkPageHeaders As System.Windows.Forms.CheckBox
    Friend WithEvents chkForceOcr As System.Windows.Forms.CheckBox
    Friend WithEvents chkDirectText As System.Windows.Forms.CheckBox
    Friend WithEvents nudLastPage As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblLastPage As System.Windows.Forms.Label
    Friend WithEvents nudFirstPage As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblFirstPage As System.Windows.Forms.Label
    Friend WithEvents nudMaxPages As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblMaxPages As System.Windows.Forms.Label
    Friend WithEvents nudDpi As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblDpi As System.Windows.Forms.Label
    Friend WithEvents btnBrowseGhostscript As System.Windows.Forms.Button
    Friend WithEvents txtGhostscript As System.Windows.Forms.TextBox
    Friend WithEvents lblGhostscript As System.Windows.Forms.Label
    Friend WithEvents btnBrowsePdfToText As System.Windows.Forms.Button
    Friend WithEvents txtPdfToText As System.Windows.Forms.TextBox
    Friend WithEvents lblPdfToText As System.Windows.Forms.Label
    Friend WithEvents btnBrowsePdfToPpm As System.Windows.Forms.Button
    Friend WithEvents txtPdfToPpm As System.Windows.Forms.TextBox
    Friend WithEvents lblPdfToPpm As System.Windows.Forms.Label
    Friend WithEvents btnBrowseTess As System.Windows.Forms.Button
    Friend WithEvents txtTesseract As System.Windows.Forms.TextBox
    Friend WithEvents lblTesseract As System.Windows.Forms.Label
    Friend WithEvents btnBrowsePdf As System.Windows.Forms.Button
    Friend WithEvents txtPdfPath As System.Windows.Forms.TextBox
    Friend WithEvents lblPdfPath As System.Windows.Forms.Label
    Friend WithEvents resultPanel As System.Windows.Forms.Panel
    Friend WithEvents splitContainer As System.Windows.Forms.SplitContainer
    Friend WithEvents txtExtractedText As System.Windows.Forms.TextBox
    Friend WithEvents resultHeaderPanel As System.Windows.Forms.Panel
    Friend WithEvents btnSaveText As System.Windows.Forms.Button
    Friend WithEvents btnCopyText As System.Windows.Forms.Button
    Friend WithEvents lblExtractedText As System.Windows.Forms.Label
    Friend WithEvents txtReport As System.Windows.Forms.TextBox
    Friend WithEvents lblReport As System.Windows.Forms.Label
End Class
