Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Security
Imports Ptz.Presentation

Namespace Views

    Public Class FrmAudit
        Implements IAuditView

        Public Event RefreshRequested As Action Implements IAuditView.RefreshRequested
        Public Event VerifyRequested As Action Implements IAuditView.VerifyRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
            pnlTop.BackColor = Theme.Panel
        End Sub

        Public Sub ShowEntries(entries As IReadOnlyList(Of AuditEntry)) Implements IAuditView.ShowEntries
            lvAudit.BeginUpdate()
            lvAudit.Items.Clear()
            For Each e As AuditEntry In entries
                Dim item As New ListViewItem(e.Seq.ToString())
                item.SubItems.Add(e.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))
                item.SubItems.Add(e.User)
                item.SubItems.Add(e.Action)
                item.SubItems.Add(e.Target)
                item.SubItems.Add(e.Outcome)
                item.SubItems.Add(e.Details)
                If e.Outcome = "DENIED" OrElse e.Outcome = "FAIL" Then item.ForeColor = If(e.Outcome = "DENIED", Theme.Warn, Theme.Danger)
                lvAudit.Items.Add(item)
            Next
            lvAudit.EndUpdate()
        End Sub

        Public Sub ShowVerification(result As AuditVerifyResult) Implements IAuditView.ShowVerification
            If result.IsValid Then
                lblVerify.ForeColor = Theme.Ok
                lblVerify.Text = $"السلسلة سليمة ({result.EntriesChecked} سجل)"
            Else
                lblVerify.ForeColor = Theme.Danger
                lblVerify.Text = $"⚠ تم رصد عبث أو حذف عند السجل رقم {result.FirstBadSeq}"
            End If
        End Sub

        Public Sub ShowError(message As String) Implements IMessageSink.ShowError
            lblVerify.ForeColor = Theme.Danger
            lblVerify.Text = message
        End Sub

        Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
            lblVerify.Text = message
        End Sub

        Private Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
            RaiseEvent RefreshRequested()
        End Sub

        Private Sub btnVerify_Click(sender As Object, e As EventArgs) Handles btnVerify.Click
            RaiseEvent VerifyRequested()
        End Sub
    End Class

End Namespace
