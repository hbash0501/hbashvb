Imports Ptz.Core.Abstractions
Imports Ptz.Core.Models

Namespace Security

    ''' <summary>
    ''' مزخرف أمني (Decorator) يلفّ أي ICameraController: يفرض الصلاحية قبل كل عملية، ويسجّل
    ''' نتيجتها (OK / DENIED / FAIL) في سجل التدقيق. لا يمكن للواجهة الوصول للمتحكم الخام.
    ''' </summary>
    Public NotInheritable Class SecuredCameraController
        Implements ICameraController

        Private ReadOnly _inner As ICameraController
        Private ReadOnly _session As UserSession
        Private ReadOnly _access As AccessControl
        Private ReadOnly _audit As IAuditLog
        Private ReadOnly _clock As IClock
        Private ReadOnly _target As String
        Private ReadOnly _ownsInner As Boolean
        Private _lastMoveAudit As DateTime = DateTime.MinValue
        Private _lastStopAudit As DateTime = DateTime.MinValue
        Private _lastMoveVector As PtzVector

        Public Sub New(inner As ICameraController, session As UserSession, access As AccessControl, audit As IAuditLog,
                       Optional clock As IClock = Nothing, Optional ownsInner As Boolean = True)
            _inner = inner
            _ownsInner = ownsInner
            _session = session
            _access = access
            _audit = audit
            _clock = If(clock, SystemClock.Instance)
            _target = inner.Profile.ModelName
        End Sub

        Public ReadOnly Property Profile As CameraProfile Implements ICameraController.Profile
            Get
                Return _inner.Profile
            End Get
        End Property

        Public ReadOnly Property Features As CameraFeatures Implements ICameraController.Features
            Get
                Return _inner.Features
            End Get
        End Property

        Public ReadOnly Property Session As UserSession
            Get
                Return _session
            End Get
        End Property

        ' ---- المحرك المركزي ----
        Private Sub Authorize(permission As Permission, action As String, touch As Boolean)
            Try
                _access.Demand(_session, permission, touch)
            Catch ex As AccessDeniedException
                _audit.Write(UserName(), action, _target, "DENIED", ex.Message)
                Throw
            End Try
        End Sub

        Private Function UserName() As String
            Return If(_session Is Nothing, "(none)", _session.UserName)
        End Function

        ' ملاحظة: العمليات غير المدقَّقة (قراءات/استطلاع/تكرار حركة) لا تجدّد نشاط الجلسة،
        ' فلا يُبقيها الاستطلاع الخلفي للفيديو حيّة بعد خمول المستخدم.
        Private Async Function RunAsync(permission As Permission, action As String, details As String, op As Func(Of Task), Optional audit As Boolean = True) As Task
            Authorize(permission, action, audit)
            Try
                Await op().ConfigureAwait(False)
                If audit Then _audit.Write(UserName(), action, _target, "OK", details)
            Catch ex As Exception
                _audit.Write(UserName(), action, _target, "FAIL", ex.GetType().Name & ": " & ex.Message)
                Throw
            End Try
        End Function

        Private Async Function RunAsync(Of T)(permission As Permission, action As String, details As String, op As Func(Of Task(Of T)), Optional audit As Boolean = True) As Task(Of T)
            Authorize(permission, action, audit)
            Try
                Dim r As T = Await op().ConfigureAwait(False)
                If audit Then _audit.Write(UserName(), action, _target, "OK", details)
                Return r
            Catch ex As Exception
                _audit.Write(UserName(), action, _target, "FAIL", ex.GetType().Name & ": " & ex.Message)
                Throw
            End Try
        End Function

        Public Function ConnectAsync(ct As Threading.CancellationToken) As Task(Of DeviceInfo) Implements ICameraController.ConnectAsync
            Return RunAsync(Permission.ViewLive, "Camera.Connect", "", Function() _inner.ConnectAsync(ct))
        End Function

        ' ---- PTZ ----
        ''' <summary>تدقيق الحركة المستمرة مخفّف: مرة في الثانية كحد أقصى (يشمل التتبع الآلي الذي يغيّر المتجه كل إطار).</summary>
        Private Function ShouldAuditMove(v As PtzVector) As Boolean
            Dim now As DateTime = _clock.UtcNow
            If (now - _lastMoveAudit).TotalSeconds < 1.0 Then Return False
            _lastMoveVector = v
            _lastMoveAudit = now
            Return True
        End Function

        Public Function MoveAsync(vector As PtzVector, ct As Threading.CancellationToken) As Task Implements IPtzMover.MoveAsync
            Return RunAsync(Permission.PtzControl, "Ptz.Move", vector.ToString(), Function() _inner.MoveAsync(vector, ct), ShouldAuditMove(vector))
        End Function

        Public Function StopAsync(ct As Threading.CancellationToken) As Task Implements IPtzMover.StopAsync
            _lastMoveVector = PtzVector.Zero
            _lastMoveAudit = DateTime.MinValue
            ' الإيقاف يُنفَّذ دائماً؛ تدقيقه يُخنق إلى سطر في الثانية كي لا يفيض السجل بسيل إيقافات.
            Dim now As DateTime = _clock.UtcNow
            Dim audit As Boolean = (now - _lastStopAudit) >= TimeSpan.FromSeconds(1)
            If audit Then _lastStopAudit = now
            Return RunAsync(Permission.PtzControl, "Ptz.Stop", "", Function() _inner.StopAsync(ct), audit)
        End Function

        Public Function GetPositionAsync(ct As Threading.CancellationToken) As Task(Of PtzPosition) Implements ICameraController.GetPositionAsync
            Return RunAsync(Permission.ViewLive, "Ptz.GetPosition", "", Function() _inner.GetPositionAsync(ct), False)
        End Function

        Public Function GoToPositionAsync(position As PtzPosition, ct As Threading.CancellationToken) As Task Implements ICameraController.GoToPositionAsync
            Return RunAsync(Permission.PtzControl, "Ptz.GoToPosition", position.ToString(), Function() _inner.GoToPositionAsync(position, ct))
        End Function

        Public Function ZoomToAreaAsync(area As NormRect, ct As Threading.CancellationToken) As Task Implements ICameraController.ZoomToAreaAsync
            Return RunAsync(Permission.PtzControl, "Ptz.ZoomToArea", $"{area.X:F2},{area.Y:F2},{area.Width:F2},{area.Height:F2}", Function() _inner.ZoomToAreaAsync(area, ct))
        End Function

        Public Function GoHomeAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.GoHomeAsync
            Return RunAsync(Permission.PtzControl, "Ptz.GoHome", "", Function() _inner.GoHomeAsync(ct))
        End Function

        Public Function SetHomeAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.SetHomeAsync
            Return RunAsync(Permission.PresetManage, "Ptz.SetHome", "", Function() _inner.SetHomeAsync(ct))
        End Function

        ' ---- المواضع والدوريات والأنماط ----
        Public Function GetPresetsAsync(ct As Threading.CancellationToken) As Task(Of IReadOnlyList(Of PtzPreset)) Implements ICameraController.GetPresetsAsync
            Return RunAsync(Permission.PresetUse, "Preset.List", "", Function() _inner.GetPresetsAsync(ct), False)
        End Function

        Public Function SavePresetAsync(id As Integer, name As String, ct As Threading.CancellationToken) As Task Implements ICameraController.SavePresetAsync
            Return RunAsync(Permission.PresetManage, "Preset.Save", $"id={id} name={name}", Function() _inner.SavePresetAsync(id, name, ct))
        End Function

        Public Function GoToPresetAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.GoToPresetAsync
            Return RunAsync(Permission.PresetUse, "Preset.Goto", $"id={id}", Function() _inner.GoToPresetAsync(id, ct))
        End Function

        Public Function DeletePresetAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.DeletePresetAsync
            Return RunAsync(Permission.PresetManage, "Preset.Delete", $"id={id}", Function() _inner.DeletePresetAsync(id, ct))
        End Function

        Public Function StartPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StartPatrolAsync
            Return RunAsync(Permission.PresetUse, "Patrol.Start", $"id={id}", Function() _inner.StartPatrolAsync(id, ct))
        End Function

        Public Function StopPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatrolAsync
            Return RunAsync(Permission.PresetUse, "Patrol.Stop", $"id={id}", Function() _inner.StopPatrolAsync(id, ct))
        End Function

        Public Function StartPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StartPatternRecordingAsync
            Return RunAsync(Permission.PresetManage, "Pattern.RecordStart", $"id={id}", Function() _inner.StartPatternRecordingAsync(id, ct))
        End Function

        Public Function StopPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatternRecordingAsync
            Return RunAsync(Permission.PresetManage, "Pattern.RecordStop", $"id={id}", Function() _inner.StopPatternRecordingAsync(id, ct))
        End Function

        Public Function RunPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.RunPatternAsync
            Return RunAsync(Permission.PresetUse, "Pattern.Run", $"id={id}", Function() _inner.RunPatternAsync(id, ct))
        End Function

        Public Function StopPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatternAsync
            Return RunAsync(Permission.PresetUse, "Pattern.Stop", $"id={id}", Function() _inner.StopPatternAsync(id, ct))
        End Function

        ' ---- العدسة والصورة ----
        Public Function MoveFocusAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.MoveFocusAsync
            Return RunAsync(Permission.ImageControl, "Lens.Focus", $"{direction} {speed}", Function() _inner.MoveFocusAsync(direction, speed, ct))
        End Function

        Public Function MoveIrisAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.MoveIrisAsync
            Return RunAsync(Permission.ImageControl, "Lens.Iris", $"{direction} {speed}", Function() _inner.MoveIrisAsync(direction, speed, ct))
        End Function

        Public Function GetImageSettingsAsync(ct As Threading.CancellationToken) As Task(Of ImageSettings) Implements ICameraController.GetImageSettingsAsync
            Return RunAsync(Permission.ViewLive, "Image.Get", "", Function() _inner.GetImageSettingsAsync(ct), False)
        End Function

        Public Function ApplyImageSettingsAsync(settings As ImageSettings, ct As Threading.CancellationToken) As Task Implements ICameraController.ApplyImageSettingsAsync
            Dim summary As String = If(settings Is Nothing, "", String.Join(",", settings.Values.Select(Function(kv) $"{kv.Key}={kv.Value}")))
            Return RunAsync(Permission.ImageControl, "Image.Apply", summary, Function() _inner.ApplyImageSettingsAsync(settings, ct))
        End Function

        Public Function SetAuxAsync(kind As AuxKind, enabled As Boolean, ct As Threading.CancellationToken) As Task Implements ICameraController.SetAuxAsync
            Return RunAsync(Permission.ImageControl, "Aux." & kind.ToString(), If(enabled, "on", "off"), Function() _inner.SetAuxAsync(kind, enabled, ct))
        End Function

        ' ---- الفيديو والصيانة ----
        Public Function GetSnapshotAsync(ct As Threading.CancellationToken) As Task(Of Byte()) Implements ICameraController.GetSnapshotAsync
            Return RunAsync(Permission.ViewLive, "Video.Snapshot", "", Function() _inner.GetSnapshotAsync(ct), False)
        End Function

        Public Function RebootAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.RebootAsync
            Return RunAsync(Permission.DeviceMaintenance, "Device.Reboot", "", Function() _inner.RebootAsync(ct))
        End Function

        ''' <summary>محرّك للتتبع الآلي: يتطلب صلاحية AutoTrack (لا PtzControl) ويُدقَّق بتخفيف.</summary>
        Public Function AsAutoTrackMover() As IPtzMover
            Return New AutoTrackMover(Me)
        End Function

        Private NotInheritable Class AutoTrackMover
            Implements IPtzMover
            Private ReadOnly _owner As SecuredCameraController
            Public Sub New(owner As SecuredCameraController)
                _owner = owner
            End Sub

            Public Function MoveAsync(vector As PtzVector, ct As Threading.CancellationToken) As Task Implements IPtzMover.MoveAsync
                Return _owner.RunAsync(Permission.AutoTrack, "AutoTrack.Move", vector.ToString(), Function() _owner._inner.MoveAsync(vector, ct), _owner.ShouldAuditMove(vector))
            End Function

            Public Function StopAsync(ct As Threading.CancellationToken) As Task Implements IPtzMover.StopAsync
                _owner._lastMoveVector = PtzVector.Zero
                _owner._lastMoveAudit = DateTime.MinValue
                Return _owner.RunAsync(Permission.AutoTrack, "AutoTrack.Stop", "", Function() _owner._inner.StopAsync(ct))
            End Function
        End Class

        Public Sub Dispose() Implements IDisposable.Dispose
            If _ownsInner Then _inner.Dispose()
        End Sub
    End Class

End Namespace
