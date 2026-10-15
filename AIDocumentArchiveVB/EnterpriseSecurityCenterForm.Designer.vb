<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class EnterpriseSecurityCenterForm
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
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblVault = New System.Windows.Forms.Label()
        Me.lstProviders = New System.Windows.Forms.ListBox()
        Me.lblDocument = New System.Windows.Forms.Label()
        Me.cboDocuments = New System.Windows.Forms.ComboBox()
        Me.txtFilePath = New System.Windows.Forms.TextBox()
        Me.btnValidate = New System.Windows.Forms.Button()
        Me.btnProtect = New System.Windows.Forms.Button()
        Me.btnRestore = New System.Windows.Forms.Button()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.cboProfile = New System.Windows.Forms.ComboBox()
        Me.btnRunPipeline = New System.Windows.Forms.Button()
        Me.btnCancelPipeline = New System.Windows.Forms.Button()
        Me.lblPipelineStatus = New System.Windows.Forms.Label()
        Me.progressPipeline = New System.Windows.Forms.ProgressBar()
        Me.txtSecurityReport = New System.Windows.Forms.TextBox()
        Me.chkValidateSignatures = New System.Windows.Forms.CheckBox()
        Me.chkProtectSensitive = New System.Windows.Forms.CheckBox()
        Me.numRiskThreshold = New System.Windows.Forms.NumericUpDown()
        Me.lblThreshold = New System.Windows.Forms.Label()
        Me.btnSavePolicy = New System.Windows.Forms.Button()
        Me.btnClose = New System.Windows.Forms.Button()
        CType(Me.numRiskThreshold, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Tahoma", 16.0!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.ForeColor = System.Drawing.Color.FromArgb(18, 77, 64)
        Me.lblTitle.Height = 58
        Me.lblTitle.Text = "مركز الأمان والمعالجة المؤسسية"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        Me.lblVault.Location = New System.Drawing.Point(28, 66)
        Me.lblVault.Size = New System.Drawing.Size(840, 24)
        Me.lblVault.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.lstProviders.Location = New System.Drawing.Point(28, 94)
        Me.lstProviders.Size = New System.Drawing.Size(840, 78)
        Me.lblDocument.Location = New System.Drawing.Point(28, 182)
        Me.lblDocument.Size = New System.Drawing.Size(840, 24)
        Me.lblDocument.Text = "المستند المراد فحصه أو معالجته أو حمايته"
        Me.lblDocument.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.cboDocuments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboDocuments.Location = New System.Drawing.Point(448, 212)
        Me.cboDocuments.Size = New System.Drawing.Size(420, 28)
        Me.txtFilePath.Location = New System.Drawing.Point(28, 212)
        Me.txtFilePath.ReadOnly = True
        Me.txtFilePath.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.txtFilePath.Size = New System.Drawing.Size(408, 28)
        Me.btnValidate.Location = New System.Drawing.Point(688, 252)
        Me.btnValidate.Size = New System.Drawing.Size(180, 38)
        Me.btnValidate.Text = "فحص البصمة الرقمية"
        Me.btnProtect.Location = New System.Drawing.Point(494, 252)
        Me.btnProtect.Size = New System.Drawing.Size(180, 38)
        Me.btnProtect.Text = "إنشاء نسخة محمية"
        Me.btnRestore.Location = New System.Drawing.Point(300, 252)
        Me.btnRestore.Size = New System.Drawing.Size(180, 38)
        Me.btnRestore.Text = "استعادة النسخة المحمية"
        Me.btnRefresh.Location = New System.Drawing.Point(28, 252)
        Me.btnRefresh.Size = New System.Drawing.Size(160, 38)
        Me.btnRefresh.Text = "تحديث البيانات"
        Me.cboProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboProfile.Location = New System.Drawing.Point(448, 304)
        Me.cboProfile.Size = New System.Drawing.Size(420, 28)
        Me.btnRunPipeline.Location = New System.Drawing.Point(254, 300)
        Me.btnRunPipeline.Size = New System.Drawing.Size(180, 38)
        Me.btnRunPipeline.Text = "تشغيل المعالجة المؤسسية"
        Me.btnCancelPipeline.Enabled = False
        Me.btnCancelPipeline.Location = New System.Drawing.Point(28, 300)
        Me.btnCancelPipeline.Size = New System.Drawing.Size(212, 38)
        Me.btnCancelPipeline.Text = "إلغاء المعالجة"
        Me.lblPipelineStatus.Location = New System.Drawing.Point(28, 344)
        Me.lblPipelineStatus.Size = New System.Drawing.Size(840, 26)
        Me.lblPipelineStatus.Text = "جاهز لبدء خط المعالجة غير المتزامن."
        Me.lblPipelineStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.progressPipeline.Location = New System.Drawing.Point(28, 372)
        Me.progressPipeline.Size = New System.Drawing.Size(840, 22)
        Me.txtSecurityReport.Location = New System.Drawing.Point(28, 406)
        Me.txtSecurityReport.Multiline = True
        Me.txtSecurityReport.ReadOnly = True
        Me.txtSecurityReport.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.txtSecurityReport.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtSecurityReport.Size = New System.Drawing.Size(840, 142)
        Me.chkValidateSignatures.Location = New System.Drawing.Point(568, 562)
        Me.chkValidateSignatures.Size = New System.Drawing.Size(300, 28)
        Me.chkValidateSignatures.Text = "منع الملفات ذات البصمة غير المطابقة"
        Me.chkProtectSensitive.Location = New System.Drawing.Point(248, 562)
        Me.chkProtectSensitive.Size = New System.Drawing.Size(300, 28)
        Me.chkProtectSensitive.Text = "إنشاء نسخة محمية للمستندات الحساسة"
        Me.lblThreshold.Location = New System.Drawing.Point(568, 602)
        Me.lblThreshold.Size = New System.Drawing.Size(300, 24)
        Me.lblThreshold.Text = "حد المخاطر لتفعيل الحماية (%)"
        Me.numRiskThreshold.Location = New System.Drawing.Point(448, 600)
        Me.numRiskThreshold.Minimum = 50
        Me.numRiskThreshold.Maximum = 100
        Me.numRiskThreshold.Value = 70
        Me.numRiskThreshold.Size = New System.Drawing.Size(100, 28)
        Me.btnSavePolicy.Location = New System.Drawing.Point(28, 593)
        Me.btnSavePolicy.Size = New System.Drawing.Size(190, 40)
        Me.btnSavePolicy.Text = "حفظ سياسة الأمان"
        Me.btnClose.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.btnClose.Height = 48
        Me.btnClose.Text = "إغلاق"
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(900, 700)
        Me.Controls.Add(Me.btnClose)
        Me.Controls.Add(Me.btnSavePolicy)
        Me.Controls.Add(Me.numRiskThreshold)
        Me.Controls.Add(Me.lblThreshold)
        Me.Controls.Add(Me.chkProtectSensitive)
        Me.Controls.Add(Me.chkValidateSignatures)
        Me.Controls.Add(Me.txtSecurityReport)
        Me.Controls.Add(Me.progressPipeline)
        Me.Controls.Add(Me.lblPipelineStatus)
        Me.Controls.Add(Me.btnCancelPipeline)
        Me.Controls.Add(Me.btnRunPipeline)
        Me.Controls.Add(Me.cboProfile)
        Me.Controls.Add(Me.btnRefresh)
        Me.Controls.Add(Me.btnRestore)
        Me.Controls.Add(Me.btnProtect)
        Me.Controls.Add(Me.btnValidate)
        Me.Controls.Add(Me.txtFilePath)
        Me.Controls.Add(Me.cboDocuments)
        Me.Controls.Add(Me.lblDocument)
        Me.Controls.Add(Me.lstProviders)
        Me.Controls.Add(Me.lblVault)
        Me.Controls.Add(Me.lblTitle)
        Me.Font = New System.Drawing.Font("Tahoma", 9.5!)
        Me.Name = "EnterpriseSecurityCenterForm"
        Me.RightToLeft = System.Windows.Forms.RightToLeft.Yes
        Me.RightToLeftLayout = True
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "مركز الأمان والمعالجة المؤسسية"
        CType(Me.numRiskThreshold, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents lblVault As System.Windows.Forms.Label
    Friend WithEvents lstProviders As System.Windows.Forms.ListBox
    Friend WithEvents lblDocument As System.Windows.Forms.Label
    Friend WithEvents cboDocuments As System.Windows.Forms.ComboBox
    Friend WithEvents txtFilePath As System.Windows.Forms.TextBox
    Friend WithEvents btnValidate As System.Windows.Forms.Button
    Friend WithEvents btnProtect As System.Windows.Forms.Button
    Friend WithEvents btnRestore As System.Windows.Forms.Button
    Friend WithEvents btnRefresh As System.Windows.Forms.Button
    Friend WithEvents cboProfile As System.Windows.Forms.ComboBox
    Friend WithEvents btnRunPipeline As System.Windows.Forms.Button
    Friend WithEvents btnCancelPipeline As System.Windows.Forms.Button
    Friend WithEvents lblPipelineStatus As System.Windows.Forms.Label
    Friend WithEvents progressPipeline As System.Windows.Forms.ProgressBar
    Friend WithEvents txtSecurityReport As System.Windows.Forms.TextBox
    Friend WithEvents chkValidateSignatures As System.Windows.Forms.CheckBox
    Friend WithEvents chkProtectSensitive As System.Windows.Forms.CheckBox
    Friend WithEvents numRiskThreshold As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblThreshold As System.Windows.Forms.Label
    Friend WithEvents btnSavePolicy As System.Windows.Forms.Button
    Friend WithEvents btnClose As System.Windows.Forms.Button
End Class
