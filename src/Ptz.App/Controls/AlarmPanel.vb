Imports System.Drawing
Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Models
Imports Ptz.Presentation

Namespace Controls

    ''' <summary>لوحة الإنذارات: قائمة ملوّنة بالخطورة (الأحدث أولاً، حدّ أقصى 500) مع إقرار.</summary>
    Public Class AlarmPanel
        Implements IAlarmPanelView

        Private Const MaxRows As Integer = 500
        Public Event AcknowledgeRequested As Action(Of Guid) Implements IAlarmPanelView.AcknowledgeRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
            pnlBottom.BackColor = Theme.Panel
        End Sub

        Public Sub AddAlarm(alarm As AlarmEvent) Implements IAlarmPanelView.AddAlarm
            Dim item As New ListViewItem(alarm.TimeUtc.ToLocalTime().ToString("HH:mm:ss"))
            item.SubItems.Add(SeverityText(alarm.Severity))
            item.SubItems.Add(alarm.Kind)
            item.SubItems.Add(alarm.Message)
            item.Tag = alarm.Id
            item.ForeColor = ColorOf(alarm.Severity)
            lvAlarms.BeginUpdate()
            lvAlarms.Items.Insert(0, item)
            While lvAlarms.Items.Count > MaxRows
                lvAlarms.Items.RemoveAt(lvAlarms.Items.Count - 1)
            End While
            lvAlarms.EndUpdate()
            lblCount.Text = $"الإنذارات: {lvAlarms.Items.Count}"
            If alarm.Severity = AlarmSeverity.Critical Then System.Media.SystemSounds.Exclamation.Play()
        End Sub

        Public Sub MarkAcknowledged(id As Guid) Implements IAlarmPanelView.MarkAcknowledged
            For Each item As ListViewItem In lvAlarms.Items
                If item.Tag IsNot Nothing AndAlso CType(item.Tag, Guid) = id Then
                    item.ForeColor = Theme.Muted
                    item.SubItems(1).Text &= " ✓"
                End If
            Next
        End Sub

        Private Shared Function SeverityText(s As AlarmSeverity) As String
            Select Case s
                Case AlarmSeverity.Critical : Return "حرج"
                Case AlarmSeverity.Warning : Return "تحذير"
                Case Else : Return "معلومة"
            End Select
        End Function

        Private Shared Function ColorOf(s As AlarmSeverity) As Color
            Select Case s
                Case AlarmSeverity.Critical : Return Theme.Danger
                Case AlarmSeverity.Warning : Return Theme.Warn
                Case Else : Return Theme.Text
            End Select
        End Function

        Private Sub btnAck_Click(sender As Object, e As EventArgs) Handles btnAck.Click
            For Each item As ListViewItem In lvAlarms.SelectedItems
                RaiseEvent AcknowledgeRequested(CType(item.Tag, Guid))
            Next
        End Sub

        Private Sub btnClearList_Click(sender As Object, e As EventArgs) Handles btnClearList.Click
            lvAlarms.Items.Clear()
            lblCount.Text = "الإنذارات: 0"
        End Sub
    End Class

End Namespace
