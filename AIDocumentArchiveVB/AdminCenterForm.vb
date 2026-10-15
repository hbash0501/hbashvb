Imports System
Imports System.Windows.Forms

Public Class AdminCenterForm
    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub btnSettings_Click(sender As Object, e As EventArgs) Handles btnSettings.Click
        Using f As New SettingsForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnUsers_Click(sender As Object, e As EventArgs) Handles btnUsers.Click
        Using f As New UserManagerForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnCategories_Click(sender As Object, e As EventArgs) Handles btnCategories.Click
        Using f As New CategoryManagerForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnAudit_Click(sender As Object, e As EventArgs) Handles btnAudit.Click
        Using f As New AuditLogForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnMaintenance_Click(sender As Object, e As EventArgs) Handles btnMaintenance.Click
        Using f As New ArchiveMaintenanceForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnPdfOcr_Click(sender As Object, e As EventArgs) Handles btnPdfOcr.Click
        Using f As New PdfOcrCenterForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnCamera_Click(sender As Object, e As EventArgs) Handles btnCamera.Click
        Using f As New CameraImportForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnSecurity_Click(sender As Object, e As EventArgs) Handles btnSecurity.Click
        Using f As New EnterpriseSecurityCenterForm()
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub
End Class
