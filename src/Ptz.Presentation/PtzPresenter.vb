Imports Ptz.Core.Models
Imports Ptz.Core.Security

''' <summary>يربط لوحة PTZ بالمتحكم المؤمَّن. دمج أوامر الحركة المتلاحقة (coalescing) كي لا تتراكم الطلبات.</summary>
Public NotInheritable Class PtzPresenter
    Inherits PanelPresenterBase

    Private ReadOnly _view As IPtzPanelView
    Private _latest As PtzVector?
    Private _pumping As Integer
    Private _generation As Integer

    Public Sub New(context As CameraContext, view As IPtzPanelView)
        MyBase.New(context)
        _view = view
        AddHandler view.MoveRequested, AddressOf OnMove
        AddHandler view.StopRequested, AddressOf OnStop
        AddHandler view.GoHomeRequested, Sub() Fire(Function() Context.Controller.GoHomeAsync(Ct), True)
        AddHandler view.SetHomeRequested, Sub() Fire(Function() Context.Controller.SetHomeAsync(Ct), True, UiText.Saved)
        AddHandler view.GoToPositionRequested, Sub(p) Fire(Function() Context.Controller.GoToPositionAsync(p, Ct), True)
        AddHandler view.RefreshPresetsRequested, Sub() Fire(AddressOf RefreshPresetsAsync, False)
        AddHandler view.PresetGoRequested, Sub(id) Fire(Function() Context.Controller.GoToPresetAsync(id, Ct), True)
        AddHandler view.PresetSaveRequested, Sub(id, name) Fire(Function() SavePresetAsync(id, name), False)
        AddHandler view.PresetDeleteRequested, Sub(id) Fire(Function() DeletePresetAsync(id), False)
        AddHandler view.PatrolRequested, Sub(id, start) Fire(Function() If(start, Context.Controller.StartPatrolAsync(id, Ct), Context.Controller.StopPatrolAsync(id, Ct)), start)
        AddHandler view.PatternRecordRequested, Sub(id, start) Fire(Function() If(start, Context.Controller.StartPatternRecordingAsync(id, Ct), Context.Controller.StopPatternRecordingAsync(id, Ct)), False)
        AddHandler view.PatternRunRequested, Sub(id, start) Fire(Function() If(start, Context.Controller.RunPatternAsync(id, Ct), Context.Controller.StopPatternAsync(id, Ct)), start)
    End Sub

    Public Sub Initialize()
        _view.SetCapabilities(Context.Controller.Features, Context.Controller.Profile)
        Fire(AddressOf RefreshPresetsAsync, False)
    End Sub

    ''' <param name="movesCamera">true = تتحرك الكاميرا فيُعلَّق كشف الحركة مؤقتاً.</param>
    Private Async Sub Fire(op As Func(Of Task), movesCamera As Boolean, Optional okMessage As String = Nothing)
        If movesCamera Then Context.Engine.NotifyCameraMotion(1.0)
        Await GuardAsync(op, okMessage)
    End Sub

    Private Async Function RefreshPresetsAsync() As Task
        Dim list As IReadOnlyList(Of PtzPreset) = Await Context.Controller.GetPresetsAsync(Ct)
        _view.ShowPresets(list)
    End Function

    Private Async Function SavePresetAsync(id As Integer, name As String) As Task
        Await Context.Controller.SavePresetAsync(id, name, Ct)
        Await RefreshPresetsAsync()
        Messages.ShowInfo(UiText.Saved)
    End Function

    Private Async Function DeletePresetAsync(id As Integer) As Task
        Await Context.Controller.DeletePresetAsync(id, Ct)
        Await RefreshPresetsAsync()
    End Function

    Private Sub OnMove(v As PtzVector)
        If v.IsZero Then
            OnStop()
            Return
        End If
        Context.Engine.NotifyCameraMotion(1.0)
        _latest = v
        If Threading.Interlocked.CompareExchange(_pumping, 1, 0) = 0 Then PumpMoves()
    End Sub

    Private Async Sub PumpMoves()
        Dim gen As Integer = _generation
        Try
            While _latest.HasValue AndAlso gen = _generation
                Dim v As PtzVector = _latest.Value
                _latest = Nothing
                If Not Await GuardAsync(Function() Context.Controller.MoveAsync(v, Ct)) Then
                    _latest = Nothing
                    Exit While
                End If
            End While
        Finally
            Threading.Volatile.Write(_pumping, 0)
        End Try
    End Sub

    Private Async Sub OnStop()
        _latest = Nothing
        Threading.Interlocked.Increment(_generation)
        Context.Engine.NotifyCameraMotion(1.0)
        Await GuardAsync(Function() Context.Controller.StopAsync(Ct))
    End Sub

    ''' <summary>يحدّث عرض الموضع الحالي (يُستدعى بمؤقّت منخفض المعدل من المقدِّم الرئيسي).</summary>
    Public Async Function RefreshPositionAsync() As Task
        Try
            Dim p As PtzPosition = Await Context.Controller.GetPositionAsync(Ct)
            Context.Ui.Post(Sub() _view.ShowPosition(p))
        Catch ex As Exception When TypeOf ex Is Ptz.Core.Isapi.IsapiException OrElse TypeOf ex Is AccessDeniedException OrElse TypeOf ex Is NotSupportedException OrElse TypeOf ex Is OperationCanceledException
            ' استطلاع خلفي: الأخطاء المؤقتة لا تُزعج المستخدم.
        End Try
    End Function
End Class
