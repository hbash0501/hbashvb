Imports System
Imports System.Windows.Forms

Partial Public Class AuditLogForm
    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub AuditLogForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        UiThemeService.StyleDataGrid(dgvAudit, False)
        dgvAudit.DataSource = AppServices.Repository.AuditToDataTable(AppServices.Repository.GetAuditLog())
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub
End Class
