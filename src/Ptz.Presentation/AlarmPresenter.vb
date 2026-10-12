Imports Ptz.Core.Models
Imports Ptz.Core.Security

''' <summary>لوحة الإنذارات: تعرض إنذارات هذه الكاميرا فقط وتتيح الإقرار بها (بصلاحية AckAlarms).</summary>
Public NotInheritable Class AlarmPresenter
    Inherits PanelPresenterBase
    Implements IDisposable

    Private ReadOnly _view As IAlarmPanelView
    Private _subscription As IDisposable

    Public Sub New(context As CameraContext, view As IAlarmPanelView)
        MyBase.New(context)
        _view = view
        AddHandler view.AcknowledgeRequested, AddressOf OnAcknowledge
        _subscription = context.Deps.Bus.Subscribe(AddressOf OnAlarm)
        ' إنذارات سبقت فتح النافذة
        For Each a As AlarmEvent In context.Deps.Bus.Recent(50).Where(Function(x) x.Camera = context.CameraName).Reverse()
            view.AddAlarm(a)
        Next
    End Sub

    Private Sub OnAlarm(alarm As AlarmEvent)
        If alarm.Camera <> Context.CameraName Then Return
        Context.Ui.Post(Sub() _view.AddAlarm(alarm))
    End Sub

    Private Sub OnAcknowledge(id As Guid)
        GuardSync(Sub()
                      Context.Authorize(Permission.AckAlarms, "Alarm.Ack", id.ToString())
                      If Context.Deps.Bus.Acknowledge(id) Then _view.MarkAcknowledged(id)
                  End Sub)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _subscription?.Dispose()
        _subscription = Nothing
    End Sub
End Class
