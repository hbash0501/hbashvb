Imports System.Net.Http
Imports System.Threading.Channels
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision

Namespace Remote

    ''' <summary>
    ''' وكيل <see cref="ISmartEngine"/> لمحرك يعمل داخل البوابة على مدار الساعة. الأوامر تفاؤلية ومتسلسلة في الخلفية
    ''' (تنقلب القيمة عند فشل أمر وتُرفع <see cref="CommandFailed"/>)، والحالة تُصحَّح من بث الخادم (هو المرجع).
    ''' التراكب (مسارات/أحداث) يصل من SSE ويُحوَّل إلى <see cref="SmartFrameResult"/> كما لو كان المحرك محلياً.
    ''' </summary>
    Public NotInheritable Class RemoteSmartEngine
        Implements ISmartEngine

        Private ReadOnly _client As GatewayClient
        Private ReadOnly _cameraId As String
        Private ReadOnly _stream As GatewayEventStream
        Private ReadOnly _queue As Channel(Of Func(Of Task)) = Channel.CreateBounded(Of Func(Of Task))(New BoundedChannelOptions(64) With {.FullMode = BoundedChannelFullMode.DropOldest, .SingleReader = True})
        Private ReadOnly _pump As Task
        Private ReadOnly _cts As New Threading.CancellationTokenSource()
        Private _motion As Integer = 1
        Private _behavior As Integer = 1
        Private _autoTrack As Integer
        Private _force As Integer = -1            ' -1 = Nothing، 0/1 = False/True
        Private _started As Integer

        Public Event FrameProcessed As Action(Of SmartFrameResult) Implements ISmartEngine.FrameProcessed
        Public Event Faulted As Action(Of Exception, Integer) Implements ISmartEngine.Faulted
        Public Event CommandFailed As Action(Of Exception) Implements ISmartEngine.CommandFailed

        Public Sub New(client As GatewayClient, cameraId As String, stream As GatewayEventStream, Optional initial As EngineStateDto = Nothing)
            _client = client
            _cameraId = cameraId
            _stream = stream
            If initial IsNot Nothing Then ApplyState(initial.MotionEnabled, initial.BehaviorEnabled, initial.AutoTrackEnabled, initial.ForceAnomalyLearning)
            AddHandler _stream.Overlay, AddressOf OnOverlay
            AddHandler _stream.StateChanged, AddressOf OnState
            AddHandler _stream.Connected, AddressOf OnHello
            AddHandler _stream.Faulted, AddressOf OnStreamFault
            AddHandler _client.SessionEnded, AddressOf OnSessionEnded
            _pump = Task.Run(AddressOf PumpAsync)
        End Sub

        ' ---------------- الحالة ----------------
        Private Sub ApplyState(motion As Boolean?, behavior As Boolean?, autoTrack As Boolean?, force As Boolean?)
            If motion.HasValue Then Threading.Volatile.Write(_motion, If(motion.Value, 1, 0))
            If behavior.HasValue Then Threading.Volatile.Write(_behavior, If(behavior.Value, 1, 0))
            If autoTrack.HasValue Then Threading.Volatile.Write(_autoTrack, If(autoTrack.Value, 1, 0))
            Threading.Volatile.Write(_force, If(force.HasValue, If(force.Value, 1, 0), -1))
        End Sub

        Private Sub OnHello(h As HelloWire)
            If h.Engine IsNot Nothing Then ApplyState(h.Engine.MotionEnabled, h.Engine.BehaviorEnabled, h.Engine.AutoTrackEnabled, h.Engine.ForceAnomalyLearning)
        End Sub

        Private Sub OnState(s As StateWire)
            ApplyState(s.MotionEnabled, s.BehaviorEnabled, s.AutoTrackEnabled, s.ForceAnomalyLearning)
        End Sub

        Public Property MotionEnabled As Boolean Implements ISmartEngine.MotionEnabled
            Get
                Return Threading.Volatile.Read(_motion) = 1
            End Get
            Set(value As Boolean)
                Dim prev As Integer = Threading.Interlocked.Exchange(_motion, If(value, 1, 0))
                If prev = If(value, 1, 0) Then Return
                Enqueue(New EngineStateDto With {.MotionEnabled = value}, Sub() Threading.Interlocked.CompareExchange(_motion, prev, If(value, 1, 0)))
            End Set
        End Property

        Public Property BehaviorEnabled As Boolean Implements ISmartEngine.BehaviorEnabled
            Get
                Return Threading.Volatile.Read(_behavior) = 1
            End Get
            Set(value As Boolean)
                Dim prev As Integer = Threading.Interlocked.Exchange(_behavior, If(value, 1, 0))
                If prev = If(value, 1, 0) Then Return
                Enqueue(New EngineStateDto With {.BehaviorEnabled = value}, Sub() Threading.Interlocked.CompareExchange(_behavior, prev, If(value, 1, 0)))
            End Set
        End Property

        Public Property AutoTrackEnabled As Boolean Implements ISmartEngine.AutoTrackEnabled
            Get
                Return Threading.Volatile.Read(_autoTrack) = 1
            End Get
            Set(value As Boolean)
                Dim prev As Integer = Threading.Interlocked.Exchange(_autoTrack, If(value, 1, 0))
                If prev = If(value, 1, 0) Then Return
                Enqueue(New EngineStateDto With {.AutoTrackEnabled = value}, Sub() Threading.Interlocked.CompareExchange(_autoTrack, prev, If(value, 1, 0)))
            End Set
        End Property

        Public Property ForceAnomalyLearning As Boolean? Implements ISmartEngine.ForceAnomalyLearning
            Get
                Dim v As Integer = Threading.Volatile.Read(_force)
                Return If(v < 0, CType(Nothing, Boolean?), v = 1)
            End Get
            Set(value As Boolean?)
                Dim code As Integer = If(value.HasValue, If(value.Value, 1, 0), -1)
                Dim prev As Integer = Threading.Interlocked.Exchange(_force, code)
                If prev = code Then Return
                Dim dto As New EngineStateDto()
                If value.HasValue Then dto.ForceAnomalyLearning = value.Value Else dto.AutoAnomalyLearning = True
                Enqueue(dto, Sub() Threading.Interlocked.CompareExchange(_force, prev, code))
            End Set
        End Property

        ' ---------------- الأوامر ----------------
        Private Sub Enqueue(dto As EngineStateDto, revert As Action)
            Post(Async Function()
                     Dim r As GatewayResponse = Await _client.RequestAsync(HttpMethod.Put, "/api/v1/cameras/" & _cameraId & "/smart/state", dto, _cts.Token).ConfigureAwait(False)
                     Dim st As EngineStateDto = r.Json(Of EngineStateDto)()
                     If st IsNot Nothing Then ApplyState(st.MotionEnabled, st.BehaviorEnabled, st.AutoTrackEnabled, st.ForceAnomalyLearning)
                 End Function, revert)
        End Sub

        Private Sub Post(work As Func(Of Task), Optional revert As Action = Nothing)
            Dim item As Func(Of Task) =
                Async Function()
                    Dim failure As Exception = Nothing
                    Try
                        Await work().ConfigureAwait(False)
                    Catch ex As Exception When TypeOf ex Is AccessDeniedException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse
                                              TypeOf ex Is NotSupportedException OrElse TypeOf ex Is Isapi.IsapiException
                        failure = ex
                    Catch ex As OperationCanceledException
                        Return
                    End Try
                    If failure IsNot Nothing Then
                        revert?.Invoke()
                        RaiseEvent CommandFailed(failure)
                    End If
                End Function
            _queue.Writer.TryWrite(item)
        End Sub

        Private Async Function PumpAsync() As Task
            Try
                While Await _queue.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(False)
                    Dim item As Func(Of Task) = Nothing
                    While _queue.Reader.TryRead(item)
                        Try
                            Await item().ConfigureAwait(False)
                        Catch ex As Exception When Not TypeOf ex Is OperationCanceledException
                            Diagnostics.Debug.WriteLine("RemoteSmartEngine command crashed: " & ex.Message)
                        End Try
                    End While
                End While
            Catch ex As OperationCanceledException
            End Try
        End Function

        Public Sub ApplyBehaviorOptions(src As BehaviorOptions) Implements ISmartEngine.ApplyBehaviorOptions
            Dim dto As BehaviorDto = WireMap.FromBehavior(src)
            Post(Async Function()
                     Await _client.RequestAsync(HttpMethod.Put, "/api/v1/cameras/" & _cameraId & "/smart/behavior", dto, _cts.Token).ConfigureAwait(False)
                 End Function)
        End Sub

        Public Sub LockTargetAt(point As NormPoint) Implements ISmartEngine.LockTargetAt
            Dim req As New TargetRequest With {.X = point.X, .Y = point.Y}
            Post(Async Function()
                     Await _client.RequestAsync(HttpMethod.Post, "/api/v1/cameras/" & _cameraId & "/smart/target", req, _cts.Token).ConfigureAwait(False)
                 End Function)
        End Sub

        Public Sub ReleaseTarget() Implements ISmartEngine.ReleaseTarget
            Post(Async Function()
                     Await _client.RequestAsync(HttpMethod.Delete, "/api/v1/cameras/" & _cameraId & "/smart/target", Nothing, _cts.Token).ConfigureAwait(False)
                 End Function)
        End Sub

        ''' <summary>لا عمل: البوابة تعرف حركة الكاميرا بنفسها (تُبلَّغ عند كل أمر PTZ تستقبله).</summary>
        Public Sub NotifyCameraMotion(Optional extraSeconds As Double = 0) Implements ISmartEngine.NotifyCameraMotion
        End Sub

        ' ---------------- الدورة والأحداث ----------------
        Public Sub Start() Implements ISmartEngine.Start
            If Threading.Interlocked.Exchange(_started, 1) = 1 Then Return
            _stream.Start()
        End Sub

        Public Async Function StopAsync() As Task Implements ISmartEngine.StopAsync
            RemoveHandler _stream.Overlay, AddressOf OnOverlay
            RemoveHandler _stream.StateChanged, AddressOf OnState
            RemoveHandler _stream.Connected, AddressOf OnHello
            RemoveHandler _stream.Faulted, AddressOf OnStreamFault
            RemoveHandler _client.SessionEnded, AddressOf OnSessionEnded
            _queue.Writer.TryComplete()
            _cts.Cancel()
            Try
                Await _pump.ConfigureAwait(False)
            Catch ex As OperationCanceledException
            End Try
            Await _stream.StopAsync().ConfigureAwait(False)
        End Function

        Private Sub OnOverlay(w As OverlayWire)
            Dim result As SmartFrameResult = Nothing
            Try
                result = WireMap.FromOverlay(w)
            Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is OverflowException
                Return
            End Try
            RaiseEvent FrameProcessed(result)
        End Sub

        Private Sub OnStreamFault(ex As Exception, count As Integer)
            RaiseEvent Faulted(ex, count)
        End Sub

        Private Sub OnSessionEnded(reason As String)
            RaiseEvent Faulted(New AccessDeniedException(Permission.None, GatewayApiException.UiMessage("session_expired", ""), isSessionExpired:=True), 0)
        End Sub
    End Class

End Namespace
