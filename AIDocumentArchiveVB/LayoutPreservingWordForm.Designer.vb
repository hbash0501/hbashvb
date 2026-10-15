<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class LayoutPreservingWordForm
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents lblHint As System.Windows.Forms.Label
    Friend WithEvents txtInput As System.Windows.Forms.TextBox
    Friend WithEvents btnBrowseInput As System.Windows.Forms.Button
    Friend WithEvents txtOutput As System.Windows.Forms.TextBox
    Friend WithEvents btnBrowseOutput As System.Windows.Forms.Button
    Friend WithEvents cboMode As System.Windows.Forms.ComboBox
    Friend WithEvents nudDpi As System.Windows.Forms.NumericUpDown
    Friend WithEvents nudFirstPage As System.Windows.Forms.NumericUpDown
    Friend WithEvents nudLastPage As System.Windows.Forms.NumericUpDown
    Friend WithEvents chkEditableLayer As System.Windows.Forms.CheckBox
    Friend WithEvents chkHighlight As System.Windows.Forms.CheckBox
    Friend WithEvents txtLanguage As System.Windows.Forms.TextBox
    Friend WithEvents btnConvert As System.Windows.Forms.Button
    Friend WithEvents btnOpenOutput As System.Windows.Forms.Button
    Friend WithEvents txtLog As System.Windows.Forms.TextBox
    Friend WithEvents lblInput As System.Windows.Forms.Label
    Friend WithEvents lblOutput As System.Windows.Forms.Label
    Friend WithEvents lblMode As System.Windows.Forms.Label
    Friend WithEvents lblDpi As System.Windows.Forms.Label
    Friend WithEvents lblPages As System.Windows.Forms.Label
    Friend WithEvents lblLanguage As System.Windows.Forms.Label

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
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblHint = New System.Windows.Forms.Label()
        Me.txtInput = New System.Windows.Forms.TextBox()
        Me.btnBrowseInput = New System.Windows.Forms.Button()
        Me.txtOutput = New System.Windows.Forms.TextBox()
        Me.btnBrowseOutput = New System.Windows.Forms.Button()
        Me.cboMode = New System.Windows.Forms.ComboBox()
        Me.nudDpi = New System.Windows.Forms.NumericUpDown()
        Me.nudFirstPage = New System.Windows.Forms.NumericUpDown()
        Me.nudLastPage = New System.Windows.Forms.NumericUpDown()
        Me.chkEditableLayer = New System.Windows.Forms.CheckBox()
        Me.chkHighlight = New System.Windows.Forms.CheckBox()
        Me.txtLanguage = New System.Windows.Forms.TextBox()
        Me.btnConvert = New System.Windows.Forms.Button()
        Me.btnOpenOutput = New System.Windows.Forms.Button()
        Me.txtLog = New System.Windows.Forms.TextBox()
        Me.lblInput = New System.Windows.Forms.Label()
        Me.lblOutput = New System.Windows.Forms.Label()
        Me.lblMode = New System.Windows.Forms.Label()
        Me.lblDpi = New System.Windows.Forms.Label()
        Me.lblPages = New System.Windows.Forms.Label()
        Me.lblLanguage = New System.Windows.Forms.Label()
        CType(Me.nudDpi, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudFirstPage, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudLastPage, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'LayoutPreservingWordForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(246, 248, 247)
        Me.ClientSize = New System.Drawing.Size(980, 650)
        Me.Controls.Add(Me.txtLog)
        Me.Controls.Add(Me.btnOpenOutput)
        Me.Controls.Add(Me.btnConvert)
        Me.Controls.Add(Me.txtLanguage)
        Me.Controls.Add(Me.lblLanguage)
        Me.Controls.Add(Me.chkHighlight)
        Me.Controls.Add(Me.chkEditableLayer)
        Me.Controls.Add(Me.nudLastPage)
        Me.Controls.Add(Me.nudFirstPage)
        Me.Controls.Add(Me.lblPages)
        Me.Controls.Add(Me.nudDpi)
        Me.Controls.Add(Me.lblDpi)
        Me.Controls.Add(Me.cboMode)
        Me.Controls.Add(Me.lblMode)
        Me.Controls.Add(Me.btnBrowseOutput)
        Me.Controls.Add(Me.txtOutput)
        Me.Controls.Add(Me.lblOutput)
        Me.Controls.Add(Me.btnBrowseInput)
        Me.Controls.Add(Me.txtInput)
        Me.Controls.Add(Me.lblInput)
        Me.Controls.Add(Me.lblHint)
        Me.Controls.Add(Me.lblTitle)
        Me.Font = New System.Drawing.Font("Tahoma", 9.0!)
        Me.Name = "LayoutPreservingWordForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "تحويل PDF/صورة إلى Word مطابق"
        '
        'lblTitle
        '
        Me.lblTitle.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblTitle.BackColor = System.Drawing.Color.FromArgb(18, 59, 54)
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 16.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(0, 0)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Padding = New System.Windows.Forms.Padding(16, 0, 16, 0)
        Me.lblTitle.Size = New System.Drawing.Size(980, 60)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "تحويل PDF أو صورة إلى Word مع الحفاظ على الشكل"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblHint
        '
        Me.lblHint.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblHint.ForeColor = System.Drawing.Color.FromArgb(80, 95, 90)
        Me.lblHint.Location = New System.Drawing.Point(24, 72)
        Me.lblHint.Name = "lblHint"
        Me.lblHint.Size = New System.Drawing.Size(932, 44)
        Me.lblHint.TabIndex = 1
        Me.lblHint.Text = "النمط المطابق بصرياً يحافظ على الصور والخلفيات ومواقع الصفحة بإدراج كل صفحة كصورة عالية الدقة، ويضيف طبقة نص قابلة للتحرير اختيارياً."
        '
        'lblInput
        '
        Me.lblInput.Location = New System.Drawing.Point(818, 132)
        Me.lblInput.Name = "lblInput"
        Me.lblInput.Size = New System.Drawing.Size(130, 24)
        Me.lblInput.TabIndex = 2
        Me.lblInput.Text = "ملف PDF أو صورة:"
        '
        'txtInput
        '
        Me.txtInput.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtInput.Location = New System.Drawing.Point(142, 130)
        Me.txtInput.Name = "txtInput"
        Me.txtInput.Size = New System.Drawing.Size(670, 26)
        Me.txtInput.TabIndex = 3
        '
        'btnBrowseInput
        '
        Me.btnBrowseInput.Location = New System.Drawing.Point(24, 128)
        Me.btnBrowseInput.Name = "btnBrowseInput"
        Me.btnBrowseInput.Size = New System.Drawing.Size(110, 30)
        Me.btnBrowseInput.TabIndex = 4
        Me.btnBrowseInput.Text = "اختيار..."
        Me.btnBrowseInput.UseVisualStyleBackColor = True
        '
        'lblOutput
        '
        Me.lblOutput.Location = New System.Drawing.Point(818, 176)
        Me.lblOutput.Name = "lblOutput"
        Me.lblOutput.Size = New System.Drawing.Size(130, 24)
        Me.lblOutput.TabIndex = 5
        Me.lblOutput.Text = "ملف Word الناتج:"
        '
        'txtOutput
        '
        Me.txtOutput.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtOutput.Location = New System.Drawing.Point(142, 174)
        Me.txtOutput.Name = "txtOutput"
        Me.txtOutput.Size = New System.Drawing.Size(670, 26)
        Me.txtOutput.TabIndex = 6
        '
        'btnBrowseOutput
        '
        Me.btnBrowseOutput.Location = New System.Drawing.Point(24, 172)
        Me.btnBrowseOutput.Name = "btnBrowseOutput"
        Me.btnBrowseOutput.Size = New System.Drawing.Size(110, 30)
        Me.btnBrowseOutput.TabIndex = 7
        Me.btnBrowseOutput.Text = "حفظ باسم..."
        Me.btnBrowseOutput.UseVisualStyleBackColor = True
        '
        'lblMode
        '
        Me.lblMode.Location = New System.Drawing.Point(818, 222)
        Me.lblMode.Name = "lblMode"
        Me.lblMode.Size = New System.Drawing.Size(130, 24)
        Me.lblMode.TabIndex = 8
        Me.lblMode.Text = "نمط التحويل:"
        '
        'cboMode
        '
        Me.cboMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboMode.FormattingEnabled = True
        Me.cboMode.Items.AddRange(New Object() {"مطابق بصرياً + نص قابل للتحرير", "مطابق بصرياً فقط", "نص قابل للتحرير فقط"})
        Me.cboMode.Location = New System.Drawing.Point(554, 220)
        Me.cboMode.Name = "cboMode"
        Me.cboMode.Size = New System.Drawing.Size(258, 26)
        Me.cboMode.TabIndex = 9
        '
        'lblDpi
        '
        Me.lblDpi.Location = New System.Drawing.Point(448, 222)
        Me.lblDpi.Name = "lblDpi"
        Me.lblDpi.Size = New System.Drawing.Size(80, 24)
        Me.lblDpi.TabIndex = 10
        Me.lblDpi.Text = "الدقة DPI:"
        '
        'nudDpi
        '
        Me.nudDpi.Increment = New Decimal(New Integer() {50, 0, 0, 0})
        Me.nudDpi.Location = New System.Drawing.Point(338, 220)
        Me.nudDpi.Maximum = New Decimal(New Integer() {600, 0, 0, 0})
        Me.nudDpi.Minimum = New Decimal(New Integer() {150, 0, 0, 0})
        Me.nudDpi.Name = "nudDpi"
        Me.nudDpi.Size = New System.Drawing.Size(100, 26)
        Me.nudDpi.TabIndex = 11
        Me.nudDpi.Value = New Decimal(New Integer() {300, 0, 0, 0})
        '
        'lblPages
        '
        Me.lblPages.Location = New System.Drawing.Point(818, 264)
        Me.lblPages.Name = "lblPages"
        Me.lblPages.Size = New System.Drawing.Size(130, 24)
        Me.lblPages.TabIndex = 12
        Me.lblPages.Text = "من/إلى صفحة:"
        '
        'nudFirstPage
        '
        Me.nudFirstPage.Location = New System.Drawing.Point(704, 262)
        Me.nudFirstPage.Maximum = New Decimal(New Integer() {999, 0, 0, 0})
        Me.nudFirstPage.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudFirstPage.Name = "nudFirstPage"
        Me.nudFirstPage.Size = New System.Drawing.Size(108, 26)
        Me.nudFirstPage.TabIndex = 13
        Me.nudFirstPage.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'nudLastPage
        '
        Me.nudLastPage.Location = New System.Drawing.Point(590, 262)
        Me.nudLastPage.Maximum = New Decimal(New Integer() {999, 0, 0, 0})
        Me.nudLastPage.Name = "nudLastPage"
        Me.nudLastPage.Size = New System.Drawing.Size(108, 26)
        Me.nudLastPage.TabIndex = 14
        '
        'chkEditableLayer
        '
        Me.chkEditableLayer.Checked = True
        Me.chkEditableLayer.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkEditableLayer.Location = New System.Drawing.Point(338, 262)
        Me.chkEditableLayer.Name = "chkEditableLayer"
        Me.chkEditableLayer.Size = New System.Drawing.Size(230, 26)
        Me.chkEditableLayer.TabIndex = 15
        Me.chkEditableLayer.Text = "إضافة نص قابل للتحرير"
        Me.chkEditableLayer.UseVisualStyleBackColor = True
        '
        'chkHighlight
        '
        Me.chkHighlight.Checked = True
        Me.chkHighlight.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkHighlight.Location = New System.Drawing.Point(24, 262)
        Me.chkHighlight.Name = "chkHighlight"
        Me.chkHighlight.Size = New System.Drawing.Size(292, 26)
        Me.chkHighlight.TabIndex = 16
        Me.chkHighlight.Text = "تمييز التواريخ والأرقام والجهات داخل Word"
        Me.chkHighlight.UseVisualStyleBackColor = True
        '
        'lblLanguage
        '
        Me.lblLanguage.Location = New System.Drawing.Point(818, 306)
        Me.lblLanguage.Name = "lblLanguage"
        Me.lblLanguage.Size = New System.Drawing.Size(130, 24)
        Me.lblLanguage.TabIndex = 17
        Me.lblLanguage.Text = "لغة OCR:"
        '
        'txtLanguage
        '
        Me.txtLanguage.Location = New System.Drawing.Point(704, 304)
        Me.txtLanguage.Name = "txtLanguage"
        Me.txtLanguage.Size = New System.Drawing.Size(108, 26)
        Me.txtLanguage.TabIndex = 18
        Me.txtLanguage.Text = "ara+eng"
        '
        'btnConvert
        '
        Me.btnConvert.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnConvert.BackColor = System.Drawing.Color.FromArgb(18, 94, 85)
        Me.btnConvert.FlatAppearance.BorderSize = 0
        Me.btnConvert.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnConvert.Font = New System.Drawing.Font("Tahoma", 10.0!, System.Drawing.FontStyle.Bold)
        Me.btnConvert.ForeColor = System.Drawing.Color.White
        Me.btnConvert.Location = New System.Drawing.Point(142, 304)
        Me.btnConvert.Name = "btnConvert"
        Me.btnConvert.Size = New System.Drawing.Size(186, 38)
        Me.btnConvert.TabIndex = 19
        Me.btnConvert.Text = "ابدأ التحويل"
        Me.btnConvert.UseVisualStyleBackColor = False
        '
        'btnOpenOutput
        '
        Me.btnOpenOutput.Enabled = False
        Me.btnOpenOutput.Location = New System.Drawing.Point(24, 304)
        Me.btnOpenOutput.Name = "btnOpenOutput"
        Me.btnOpenOutput.Size = New System.Drawing.Size(110, 38)
        Me.btnOpenOutput.TabIndex = 20
        Me.btnOpenOutput.Text = "فتح الناتج"
        Me.btnOpenOutput.UseVisualStyleBackColor = True
        '
        'txtLog
        '
        Me.txtLog.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtLog.BackColor = System.Drawing.Color.White
        Me.txtLog.Location = New System.Drawing.Point(24, 362)
        Me.txtLog.Multiline = True
        Me.txtLog.Name = "txtLog"
        Me.txtLog.ReadOnly = True
        Me.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtLog.Size = New System.Drawing.Size(932, 264)
        Me.txtLog.TabIndex = 21
        '
        CType(Me.nudDpi, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudFirstPage, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudLastPage, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub
End Class
