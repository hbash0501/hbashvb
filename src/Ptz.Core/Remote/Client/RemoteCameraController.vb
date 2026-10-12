Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models

Namespace Remote

    ''' <summary>
    ''' تنفيذ <see cref="ICameraController"/> فوق REST البوابة. يلفّه <c>SecuredCameraController</c> (فحص صلاحيات مبكر + تدقيق محلي)
    ''' بينما الفرض الحقيقي في البوابة. أوامر الحركة: رقم تسلسلي متزايد، أحدث أمر يغلب، وتجديد دوري يحافظ على عقد الحركة
    ''' (البوابة توقف الكاميرا وحدها إن انقطعت التجديدات، فلا تبقى الكاميرا تدور عند انهيار العميل أو انقطاع الشبكة).
    ''' </summary>
    Public NotInheritable Class RemoteCameraController
        Implements ICameraController

        Private ReadOnly _client As GatewayClient
        Private ReadOnly _cameraId As String
        Private ReadOnly _profile As CameraProfile
        Private ReadOnly _renewMs As Integer
        Private ReadOnly _gate As New Threading.SemaphoreSlim(1, 1)
        Private ReadOnly _lock As New Object()
        Private _features As CameraFeatures
        Private _seq As Long
        Private _latestSeq As Long
        Private _renewTimer As Threading.Timer
        Private _renewVector As PtzVector
        Private _renewBusy As Integer
        Private _disposed As Integer

        Public Sub New(client As GatewayClient, cameraId As String, profile As CameraProfile, Optional renewMs As Integer = 400)
            If client Is Nothing Then Throw New ArgumentNullException(NameOf(client))
            If cameraId Is Nothing OrElse Not System.Text.RegularExpressions.Regex.IsMatch(cameraId, "^[a-z0-9-]{1,32}\z") Then Throw New ArgumentException("معرّف كاميرا غير صالح.")
            _client = client
            _cameraId = cameraId
            _profile = profile
            _features = profile.DefaultFeatures
            _renewMs = Math.Max(100, renewMs)
        End Sub

        Public ReadOnly Property CameraId As String
            Get
                Return _cameraId
            End Get
        End Property

        Public ReadOnly Property Profile As CameraProfile Implements ICameraController.Profile
            Get
                Return _profile
            End Get
        End Property

        Public ReadOnly Property Features As CameraFeatures Implements ICameraController.Features
            Get
                Return _features
            End Get
        End Property

        Private Function Path(suffix As String) As String
            Return "/api/v1/cameras/" & _cameraId & suffix
        End Function

        Private Function Send(method As HttpMethod, suffix As String, body As Object, ct As Threading.CancellationToken) As Task(Of GatewayResponse)
            Return _client.RequestAsync(method, Path(suffix), body, ct)
        End Function

        ' ============ الاتصال ============
        Public Async Function ConnectAsync(ct As Threading.CancellationToken) As Task(Of DeviceInfo) Implements ICameraController.ConnectAsync
            Dim r As GatewayResponse = Await Send(HttpMethod.Post, "/connect", Nothing, ct).ConfigureAwait(False)
            Dim dto As ConnectResultDto = r.Json(Of ConnectResultDto)()
            _features = WireMap.ParseFeatures(dto.Features)
            Return New DeviceInfo With {.DeviceName = dto.Device.DeviceName, .Model = dto.Device.Model,
                                        .SerialNumber = dto.Device.SerialNumber, .FirmwareVersion = dto.Device.FirmwareVersion}
        End Function

        ' ============ PTZ المستمر ============
        Private Function NextSeq() As Long
            SyncLock _lock
                _seq += 1
                _latestSeq = _seq
                Return _seq
            End SyncLock
        End Function

        Private Function IsSuperseded(seq As Long) As Boolean
            SyncLock _lock
                Return seq < _latestSeq
            End SyncLock
        End Function

        Public Async Function MoveAsync(vector As PtzVector, ct As Threading.CancellationToken) As Task Implements IPtzMover.MoveAsync
            If vector.IsZero Then
                Await StopAsync(ct).ConfigureAwait(False)
                Return
            End If
            Dim seq As Long = NextSeq()
            Await SendMoveAsync(vector, seq, renew:=False, ct:=ct).ConfigureAwait(False)
            ArmRenew(vector)
        End Function

        Private Async Function SendMoveAsync(vector As PtzVector, seq As Long, renew As Boolean, ct As Threading.CancellationToken) As Task
            Await _gate.WaitAsync(ct).ConfigureAwait(False)
            Try
                If IsSuperseded(seq) Then Return              ' وصل أمر أحدث أثناء الانتظار
                Dim req As New MoveRequest With {.Pan = vector.Pan, .Tilt = vector.Tilt, .Zoom = vector.Zoom, .Seq = seq, .Renew = If(renew, CType(True, Boolean?), Nothing)}
                Dim res = Await _client.TryRequestAsync(HttpMethod.Post, Path("/ptz/move"), req, ct).ConfigureAwait(False)
                If res.Error IsNot Nothing AndAlso res.Error.Code <> "stale_seq" Then Throw res.Error.ToDomainException()
            Finally
                _gate.Release()
            End Try
        End Function

        Public Async Function StopAsync(ct As Threading.CancellationToken) As Task Implements IPtzMover.StopAsync
            DisarmRenew()
            Dim seq As Long = NextSeq()
            Dim attempt As Integer = 0
            While True
                attempt += 1
                Dim transient As Boolean = False
                Await _gate.WaitAsync(ct).ConfigureAwait(False)
                Try
                    If IsSuperseded(seq) Then Return
                    Dim res = Await _client.TryRequestAsync(HttpMethod.Post, Path("/ptz/stop"), New StopRequest With {.Seq = seq}, ct).ConfigureAwait(False)
                    If res.Error Is Nothing OrElse res.Error.Code = "stale_seq" Then Return
                    transient = (res.Error.Status = 503 OrElse res.Error.Status = 504 OrElse res.Error.Status = 502)
                    If Not transient OrElse attempt >= 3 Then Throw res.Error.ToDomainException()
                Catch ex As GatewayUnreachableException When attempt < 3
                    transient = True
                Finally
                    _gate.Release()
                End Try
                Await Task.Delay(150, ct).ConfigureAwait(False)
            End While
        End Function

        Private Sub ArmRenew(vector As PtzVector)
            If Threading.Volatile.Read(_disposed) <> 0 Then Return
            SyncLock _lock
                _renewVector = vector
                If _renewTimer Is Nothing Then _renewTimer = New Threading.Timer(AddressOf RenewTick, Nothing, _renewMs, _renewMs)
            End SyncLock
        End Sub

        Private Sub DisarmRenew()
            SyncLock _lock
                _renewVector = PtzVector.Zero
                _renewTimer?.Dispose()
                _renewTimer = Nothing
            End SyncLock
        End Sub

        Private Sub RenewTick(state As Object)
            If Threading.Volatile.Read(_disposed) <> 0 Then Return
            If Threading.Interlocked.Exchange(_renewBusy, 1) = 1 Then Return
            Task.Run(AddressOf RenewOnceAsync)
        End Sub

        Private Async Function RenewOnceAsync() As Task
            Dim failed As Boolean = False
            Try
                Dim v As PtzVector
                SyncLock _lock
                    v = _renewVector
                End SyncLock
                If v.IsZero Then Return
                Dim seq As Long = NextSeq()
                Using cts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(3))
                    Await SendMoveAsync(v, seq, renew:=True, ct:=cts.Token).ConfigureAwait(False)
                End Using
            Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is Security.AccessDeniedException OrElse
                                      TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse
                                      TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is NotSupportedException
                failed = True
            Finally
                Threading.Volatile.Write(_renewBusy, 0)
            End Try
            ' فشل التجديد ⇒ نكفّ عن الإرسال؛ ينتهي عقد البوابة خلال ttl فتوقف الكاميرا بنفسها (آمن عند الفشل).
            If failed Then DisarmRenew()
        End Function

        ' ============ الموضع ============
        Public Async Function GetPositionAsync(ct As Threading.CancellationToken) As Task(Of PtzPosition) Implements ICameraController.GetPositionAsync
            Dim p As PositionDto = (Await Send(HttpMethod.Get, "/ptz/position", Nothing, ct).ConfigureAwait(False)).Json(Of PositionDto)()
            Return New PtzPosition(p.Azimuth, p.Elevation, p.Zoom)
        End Function

        Public Async Function GoToPositionAsync(position As PtzPosition, ct As Threading.CancellationToken) As Task Implements ICameraController.GoToPositionAsync
            Await Send(HttpMethod.Put, "/ptz/position", New PositionDto With {.Azimuth = position.Azimuth, .Elevation = position.Elevation, .Zoom = position.Zoom}, ct).ConfigureAwait(False)
        End Function

        Public Async Function ZoomToAreaAsync(area As NormRect, ct As Threading.CancellationToken) As Task Implements ICameraController.ZoomToAreaAsync
            Await Send(HttpMethod.Post, "/ptz/zoom-area", New ZoomAreaRequest With {.X = area.X, .Y = area.Y, .Width = area.Width, .Height = area.Height}, ct).ConfigureAwait(False)
        End Function

        Public Async Function GoHomeAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.GoHomeAsync
            Await Send(HttpMethod.Post, "/ptz/home", Nothing, ct).ConfigureAwait(False)
        End Function

        Public Async Function SetHomeAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.SetHomeAsync
            Await Send(HttpMethod.Put, "/ptz/home", Nothing, ct).ConfigureAwait(False)
        End Function

        ' ============ المواضع المسبقة والدوريات والأنماط ============
        Public Async Function GetPresetsAsync(ct As Threading.CancellationToken) As Task(Of IReadOnlyList(Of PtzPreset)) Implements ICameraController.GetPresetsAsync
            Dim list As PresetListDto = (Await Send(HttpMethod.Get, "/presets", Nothing, ct).ConfigureAwait(False)).Json(Of PresetListDto)()
            Return list.Items.Select(Function(p) New PtzPreset With {.Id = p.Id, .Name = p.Name}).ToList()
        End Function

        Public Async Function SavePresetAsync(id As Integer, name As String, ct As Threading.CancellationToken) As Task Implements ICameraController.SavePresetAsync
            Await Send(HttpMethod.Put, "/presets/" & id.ToString(Globalization.CultureInfo.InvariantCulture), New PresetPutRequest With {.Name = name}, ct).ConfigureAwait(False)
        End Function

        Public Async Function GoToPresetAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.GoToPresetAsync
            Await Send(HttpMethod.Post, "/presets/" & id.ToString(Globalization.CultureInfo.InvariantCulture) & "/goto", Nothing, ct).ConfigureAwait(False)
        End Function

        Public Async Function DeletePresetAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.DeletePresetAsync
            Await Send(HttpMethod.Delete, "/presets/" & id.ToString(Globalization.CultureInfo.InvariantCulture), Nothing, ct).ConfigureAwait(False)
        End Function

        Private Function Num(id As Integer) As String
            Return id.ToString(Globalization.CultureInfo.InvariantCulture)
        End Function

        Public Async Function StartPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StartPatrolAsync
            Await Send(HttpMethod.Post, "/patrols/" & Num(id) & "/start", Nothing, ct).ConfigureAwait(False)
        End Function

        Public Async Function StopPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatrolAsync
            Await Send(HttpMethod.Post, "/patrols/" & Num(id) & "/stop", Nothing, ct).ConfigureAwait(False)
        End Function

        Public Async Function StartPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StartPatternRecordingAsync
            Await Send(HttpMethod.Post, "/patterns/" & Num(id) & "/record/start", Nothing, ct).ConfigureAwait(False)
        End Function

        Public Async Function StopPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatternRecordingAsync
            Await Send(HttpMethod.Post, "/patterns/" & Num(id) & "/record/stop", Nothing, ct).ConfigureAwait(False)
        End Function

        Public Async Function RunPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.RunPatternAsync
            Await Send(HttpMethod.Post, "/patterns/" & Num(id) & "/run", Nothing, ct).ConfigureAwait(False)
        End Function

        Public Async Function StopPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatternAsync
            Await Send(HttpMethod.Post, "/patterns/" & Num(id) & "/stop", Nothing, ct).ConfigureAwait(False)
        End Function

        ' ============ العدسة ============
        Public Async Function MoveFocusAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.MoveFocusAsync
            Await Send(HttpMethod.Post, "/lens/focus", New LensRequest With {.Direction = WireMap.LensDirectionName(direction), .Speed = speed}, ct).ConfigureAwait(False)
        End Function

        Public Async Function MoveIrisAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.MoveIrisAsync
            Await Send(HttpMethod.Post, "/lens/iris", New LensRequest With {.Direction = WireMap.LensDirectionName(direction), .Speed = speed}, ct).ConfigureAwait(False)
        End Function

        ' ============ الصورة والإضاءة ============
        Public Async Function GetImageSettingsAsync(ct As Threading.CancellationToken) As Task(Of ImageSettings) Implements ICameraController.GetImageSettingsAsync
            Dim dto As ImageDto = (Await Send(HttpMethod.Get, "/image", Nothing, ct).ConfigureAwait(False)).Json(Of ImageDto)()
            Dim s As New ImageSettings()
            For Each kv In dto.Values
                Dim p As ImageParam
                If Not WireMap.TryParseImageKey(kv.Key, p) Then Continue For            ' معامل أحدث من هذا العميل
                Try
                    s(p) = kv.Value
                Catch ex As ArgumentException
                    ' قيمة لا يقبلها فهرس هذا العميل: نتجاهلها بدل إسقاط الصفحة كلها
                End Try
            Next
            Return s
        End Function

        Public Async Function ApplyImageSettingsAsync(settings As ImageSettings, ct As Threading.CancellationToken) As Task Implements ICameraController.ApplyImageSettingsAsync
            Await Send(HttpMethod.Put, "/image", New ImagePutRequest With {.Values = WireMap.FromImageSettings(settings)}, ct).ConfigureAwait(False)
        End Function

        Public Async Function SetAuxAsync(kind As AuxKind, enabled As Boolean, ct As Threading.CancellationToken) As Task Implements ICameraController.SetAuxAsync
            Await Send(HttpMethod.Put, "/aux/" & If(kind = AuxKind.Wiper, "wiper", "light"), New AuxRequest With {.Enabled = enabled}, ct).ConfigureAwait(False)
        End Function

        ' ============ الفيديو والصيانة ============
        Public Async Function GetSnapshotAsync(ct As Threading.CancellationToken) As Task(Of Byte()) Implements ICameraController.GetSnapshotAsync
            Dim r As GatewayResponse = Await Send(HttpMethod.Get, "/snapshot?fresh=1", Nothing, ct).ConfigureAwait(False)
            If r.Body.Length < 4 OrElse r.Body(0) <> &HFF OrElse r.Body(1) <> &HD8 Then Throw New IsapiException(200, "", "استجابة اللقطة ليست صورة JPEG صالحة.")
            Return r.Body
        End Function

        Public Async Function RebootAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.RebootAsync
            Dim res = Await _client.TryRequestAsync(HttpMethod.Post, Path("/device/reboot"), New RebootRequest With {.Confirm = "REBOOT"}, ct).ConfigureAwait(False)
            If res.Error Is Nothing Then Return
            ' المسار غير مسجَّل على البوابة ما لم يُفعَّل الدور الإداري البعيد صراحةً
            If res.Error.Status = 404 Then Throw New NotSupportedException("إعادة تشغيل الكاميرا عن بُعد غير مفعّلة على البوابة.")
            Throw res.Error.ToDomainException()
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If Threading.Interlocked.Exchange(_disposed, 1) <> 0 Then Return
            Dim wasMoving As Boolean
            SyncLock _lock
                wasMoving = Not _renewVector.IsZero
            End SyncLock
            DisarmRenew()
            If wasMoving Then
                ' إيقاف احترازي؛ إن فشل فعقد البوابة سينتهي من تلقاء نفسه.
                Task.Run(Async Function()
                             Try
                                 Using cts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(2))
                                     Dim seq As Long = NextSeq()
                                     Await _client.TryRequestAsync(HttpMethod.Post, Path("/ptz/stop"), New StopRequest With {.Seq = seq}, cts.Token).ConfigureAwait(False)
                                 End Using
                             Catch ex As Exception
                             End Try
                         End Function)
            End If
        End Sub
    End Class

End Namespace
