Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Windows.Forms

Partial Public Class ArchiveMaintenanceForm
    Private ReadOnly _service As ArchiveMaintenanceService

    Public Sub New()
        InitializeComponent()
        _service = New ArchiveMaintenanceService(AppServices.Settings, AppServices.Repository)
        UiThemeService.StyleDataGrid(dgvDiagnostics)
    End Sub

    Private Sub ArchiveMaintenanceForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        RunDiagnostics()
    End Sub

    Private Sub RunDiagnostics()
        dgvDiagnostics.DataSource = _service.BuildDiagnostics()
        txtOutput.Text = "آخر فحص: " & DateTime.Now.ToString("yyyy/MM/dd HH:mm") & Environment.NewLine &
                         "مجلد البيانات: " & AppServices.Settings.BaseDataFolder & Environment.NewLine &
                         "مجلد الأرشيف: " & AppServices.Settings.ArchiveRootFolder
    End Sub

    Private Sub btnRun_Click(sender As Object, e As EventArgs) Handles btnRun.Click
        RunDiagnostics()
    End Sub

    Private Sub btnBackup_Click(sender As Object, e As EventArgs) Handles btnBackup.Click
        Using sfd As New SaveFileDialog()
            sfd.Title = "حفظ نسخة احتياطية"
            sfd.Filter = "ZIP Backup|*.zip"
            sfd.FileName = "AIDocArchive_Backup_" & DateTime.Now.ToString("yyyyMMdd_HHmm") & ".zip"
            If sfd.ShowDialog(Me) = DialogResult.OK Then
                Try
                    Dim resultPath = _service.CreateBackupPackage(sfd.FileName)
                    txtOutput.Text = "تم إنشاء النسخة الاحتياطية:" & Environment.NewLine & resultPath
                    MessageBox.Show("تم إنشاء النسخة الاحتياطية بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("تعذر إنشاء النسخة الاحتياطية:" & Environment.NewLine & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    Private Sub btnInventory_Click(sender As Object, e As EventArgs) Handles btnInventory.Click
        Using sfd As New SaveFileDialog()
            sfd.Title = "تصدير فهرس الأرشيف"
            sfd.Filter = "CSV|*.csv"
            sfd.FileName = "Archive_Inventory_" & DateTime.Now.ToString("yyyyMMdd_HHmm") & ".csv"
            If sfd.ShowDialog(Me) = DialogResult.OK Then
                Dim resultPath = _service.ExportInventoryCsv(sfd.FileName)
                txtOutput.Text = "تم تصدير الفهرس:" & Environment.NewLine & resultPath
                MessageBox.Show("تم تصدير الفهرس بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Using
    End Sub

    Private Sub btnIndex_Click(sender As Object, e As EventArgs) Handles btnIndex.Click
        Dim resultPath = _service.RebuildTextIndex()
        txtOutput.Text = "تم بناء الفهرس النصي:" & Environment.NewLine & resultPath & Environment.NewLine & "يمكن استخدامه لاحقاً للبحث السريع أو الفهرسة الخارجية."
        MessageBox.Show("تم بناء الفهرس النصي بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub btnOpenData_Click(sender As Object, e As EventArgs) Handles btnOpenData.Click
        Directory.CreateDirectory(AppServices.Settings.BaseDataFolder)
        Process.Start(New ProcessStartInfo(AppServices.Settings.BaseDataFolder) With {.UseShellExecute = True})
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub


End Class
