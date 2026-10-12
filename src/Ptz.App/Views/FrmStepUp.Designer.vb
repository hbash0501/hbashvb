Namespace Views
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmStepUp
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
        Me.lblReason = New System.Windows.Forms.Label()
        Me.lblCodeCap = New System.Windows.Forms.Label()
        Me.txtCode = New System.Windows.Forms.TextBox()
        Me.btnOk = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblReason
        '
        Me.lblReason.Name = "lblReason"
        Me.lblReason.Location = New System.Drawing.Point(20, 16)
        Me.lblReason.Size = New System.Drawing.Size(360, 60)
        Me.lblReason.Text = ""
        '
        'lblCodeCap
        '
        Me.lblCodeCap.Name = "lblCodeCap"
        Me.lblCodeCap.AutoSize = True
        Me.lblCodeCap.Location = New System.Drawing.Point(20, 84)
        Me.lblCodeCap.Text = "رمز التحقق الحالي (6 أرقام)"
        '
        'txtCode
        '
        Me.txtCode.Name = "txtCode"
        Me.txtCode.Location = New System.Drawing.Point(20, 106)
        Me.txtCode.Size = New System.Drawing.Size(140, 23)
        Me.txtCode.MaxLength = 6
        '
        'btnOk
        '
        Me.btnOk.Name = "btnOk"
        Me.btnOk.Location = New System.Drawing.Point(20, 150)
        Me.btnOk.Size = New System.Drawing.Size(170, 36)
        Me.btnOk.Text = "تأكيد"
        Me.btnOk.UseVisualStyleBackColor = True
        '
        'btnCancel
        '
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Location = New System.Drawing.Point(210, 150)
        Me.btnCancel.Size = New System.Drawing.Size(170, 36)
        Me.btnCancel.Text = "إلغاء"
        Me.btnCancel.UseVisualStyleBackColor = True
        Me.Controls.Add(Me.lblReason)
        Me.Controls.Add(Me.lblCodeCap)
        Me.Controls.Add(Me.txtCode)
        Me.Controls.Add(Me.btnOk)
        Me.Controls.Add(Me.btnCancel)
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(400, 210)
        Me.Name = "FrmStepUp"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "تحقق إضافي"
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.AcceptButton = Me.btnOk
        Me.CancelButton = Me.btnCancel
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lblReason As System.Windows.Forms.Label
    Friend WithEvents lblCodeCap As System.Windows.Forms.Label
    Friend WithEvents txtCode As System.Windows.Forms.TextBox
    Friend WithEvents btnOk As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button

End Class
End Namespace
