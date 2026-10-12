Imports System.Net.Http
Imports System.Text.Json

Namespace Remote

    ''' <summary>
    ''' بث الأحداث لكاميرا واحدة عبر SSE: يعيد الاتصال تلقائياً بتراجع أسّي مع Last-Event-ID، ويكتشف الصمت (نبضات) ويعامل
    ''' bye بحسب سببها (إبطال/انتهاء ⇒ إنهاء الجلسة؛ غير ذلك ⇒ إعادة اتصال). كل الأحداث تُرفع من خيط خلفي ومعالجاتها محمية.
    ''' </summary>
    Public NotInheritable Class GatewayEventStream
        Implements IDisposable

        Private NotInheritable Class AttemptResult
            Public GotHello As Boolean
            Public Failure As Exception
            Public EndSession As String
            Public Immediate As Boolean
        End Class

        Private ReadOnly _client As GatewayClient
        Private ReadOnly _cameraId As String
        Private ReadOnly _backoff As Func(Of Integer, TimeSpan)
        Private _cts As Threading.CancellationTokenSource
        Private _run As Task
        Private _lastAlarmSeq As String
        Private _heartbeat As Integer = 15

        Public Event Connected As Action(Of HelloWire)
        Public Event Overlay As Action(Of OverlayWire)
        Public Event AlarmReceived As Action(Of AlarmDto)
        Public Event StateChanged As Action(Of StateWire)
        Public Event GeometryChanged As Action(Of GeoWire)
        Public Event HealthChanged As Action(Of HealthWire)
        Public Event AlarmAcknowledged As Action(Of AckWire)
        ''' <summary>فاتتنا أحداث أكثر مما تحتفظ به البوابة: على المستهلك إعادة تحميل الحالة والإنذارات.</summary>
        Public Event ResyncRequired As Action(Of ResyncWire)
        ''' <summary>(متصل؟ ، عدد المحاولات الفاشلة المتتالية).</summary>
        Public Event LinkChanged As Action(Of Boolean, Integer)
        Public Event Faulted As Action(Of Exception, Integer)

        Public Sub New(client As GatewayClient, cameraId As String, Optional backoff As Func(Of Integer, TimeSpan) = Nothing)
            If cameraId Is Nothing OrElse Not System.Text.RegularExpressions.Regex.IsMatch(cameraId, "^[a-z0-9-]{1,32}\z") Then Throw New ArgumentException("معرّف كاميرا غير صالح.")
            _client = client
            _cameraId = cameraId
            _backoff = If(backoff, AddressOf DefaultBackoff)
        End Sub

        Public Shared Function DefaultBackoff(attempt As Integer) As TimeSpan
            Dim seconds As Double = Math.Min(30.0, Math.Pow(2, Math.Min(attempt, 5)))
            Dim jitter As Double = 0.8 + 0.4 * Random.Shared.NextDouble()
            Return TimeSpan.FromSeconds(seconds * jitter)
        End Function

        Public ReadOnly Property LastAlarmSeq As String
            Get
                Return _lastAlarmSeq
            End Get
        End Property

        Public Sub Start()
            If _run IsNot Nothing Then Return
            _cts = New Threading.CancellationTokenSource()
            Dim ct As Threading.CancellationToken = _cts.Token
            _run = Task.Run(Function() RunAsync(ct))
        End Sub

        Public Async Function StopAsync() As Task
            Dim cts As Threading.CancellationTokenSource = _cts
            Dim run As Task = _run
            _cts = Nothing
            _run = Nothing
            If cts Is Nothing Then Return
            cts.Cancel()
            Try
                If run IsNot Nothing Then Await run.ConfigureAwait(False)
            Catch ex As OperationCanceledException
            End Try
            cts.Dispose()
        End Function

        Private Sub Safe(op As Action)
            Try
                op()
            Catch ex As Exception
                Diagnostics.Debug.WriteLine("SSE handler failed: " & ex.Message)
            End Try
        End Sub

        Private Async Function RunAsync(ct As Threading.CancellationToken) As Task
            Dim failures As Integer = 0
            While Not ct.IsCancellationRequested
                Dim r As AttemptResult = Await ConnectOnceAsync(ct).ConfigureAwait(False)
                If ct.IsCancellationRequested Then Exit While
                If r.EndSession IsNot Nothing Then
                    _client.NotifySessionEnded(r.EndSession)
                    Exit While
                End If
                If r.GotHello Then failures = 0
                If r.Failure IsNot Nothing Then
                    failures += 1
                    Dim n As Integer = failures
                    Dim ex As Exception = r.Failure
                    Safe(Sub() RaiseEvent LinkChanged(False, n))
                    Safe(Sub() RaiseEvent Faulted(ex, n))
                Else
                    Safe(Sub() RaiseEvent LinkChanged(False, 0))
                End If
                Dim wait As TimeSpan = If(r.Immediate, TimeSpan.FromMilliseconds(250), _backoff(Math.Max(failures, 1)))
                Try
                    Await Task.Delay(wait, ct).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    Exit While
                End Try
            End While
        End Function

        Private Async Function ConnectOnceAsync(ct As Threading.CancellationToken) As Task(Of AttemptResult)
            Dim result As New AttemptResult()
            Try
                Using resp As HttpResponseMessage = Await _client.OpenStreamAsync($"/api/v1/cameras/{_cameraId}/events", _lastAlarmSeq, ct).ConfigureAwait(False)
                    Using stream As IO.Stream = Await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(False)
                        Dim reader As New SseFrameReader(stream)
                        Dim silenceSeconds As Integer = _client.Endpoint.TimeoutSeconds
                        Using silence As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                            silence.CancelAfter(TimeSpan.FromSeconds(silenceSeconds))
                            While True
                                Dim frame As SseFrame = Await reader.ReadFrameAsync(silence.Token).ConfigureAwait(False)
                                If frame Is Nothing Then Exit While                        ' أغلق الخادم البث
                                silence.CancelAfter(TimeSpan.FromSeconds(silenceSeconds))
                                If frame.IsComment Then Continue While
                                If Dispatch(frame, result) Then
                                    silenceSeconds = Math.Max(10, _heartbeat * 3)
                                    silence.CancelAfter(TimeSpan.FromSeconds(silenceSeconds))
                                End If
                                If result.EndSession IsNot Nothing OrElse result.Immediate Then Exit While
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As OperationCanceledException When Not ct.IsCancellationRequested
                result.Failure = New GatewayUnreachableException("انقطع بث الأحداث (لا نبضات).")
            Catch ex As OperationCanceledException
                ' إيقاف مقصود
            Catch ex As GatewayApiException
                If ex.IsSessionEnded Then result.EndSession = ex.Code Else result.Failure = ex.ToDomainException()
            Catch ex As Exception When TypeOf ex Is GatewayUnreachableException OrElse TypeOf ex Is IO.IOException OrElse TypeOf ex Is HttpRequestException OrElse
                                      TypeOf ex Is IO.InvalidDataException OrElse TypeOf ex Is ObjectDisposedException
                result.Failure = If(TypeOf ex Is GatewayUnreachableException, ex, New GatewayUnreachableException("انقطع الاتصال ببث الأحداث."))
            End Try
            Return result
        End Function

        ''' <summary>يعيد True عند hello (لتحديث مهلة الصمت).</summary>
        Private Function Dispatch(frame As SseFrame, result As AttemptResult) As Boolean
            Try
                Select Case frame.EventName
                    Case "hello"
                        Dim h As HelloWire = JsonSerializer.Deserialize(Of HelloWire)(frame.Data, WireJson.ClientRead)
                        If h Is Nothing Then Return False
                        _heartbeat = Math.Max(1, Math.Min(h.HeartbeatSeconds, 120))
                        result.GotHello = True
                        Safe(Sub() RaiseEvent LinkChanged(True, 0))
                        Safe(Sub() RaiseEvent Connected(h))
                        Return True
                    Case "ov"
                        Dim o As OverlayWire = JsonSerializer.Deserialize(Of OverlayWire)(frame.Data, WireJson.ClientRead)
                        If o IsNot Nothing Then Safe(Sub() RaiseEvent Overlay(o))
                    Case "alarm"
                        Dim a As AlarmDto = JsonSerializer.Deserialize(Of AlarmDto)(frame.Data, WireJson.ClientRead)
                        If a IsNot Nothing Then
                            If frame.Id IsNot Nothing Then _lastAlarmSeq = frame.Id
                            Safe(Sub() RaiseEvent AlarmReceived(a))
                        End If
                    Case "state"
                        Dim s As StateWire = JsonSerializer.Deserialize(Of StateWire)(frame.Data, WireJson.ClientRead)
                        If s IsNot Nothing Then Safe(Sub() RaiseEvent StateChanged(s))
                    Case "geo"
                        Dim g As GeoWire = JsonSerializer.Deserialize(Of GeoWire)(frame.Data, WireJson.ClientRead)
                        If g IsNot Nothing Then Safe(Sub() RaiseEvent GeometryChanged(g))
                    Case "health"
                        Dim hw As HealthWire = JsonSerializer.Deserialize(Of HealthWire)(frame.Data, WireJson.ClientRead)
                        If hw IsNot Nothing Then Safe(Sub() RaiseEvent HealthChanged(hw))
                    Case "ack"
                        Dim ak As AckWire = JsonSerializer.Deserialize(Of AckWire)(frame.Data, WireJson.ClientRead)
                        If ak IsNot Nothing Then Safe(Sub() RaiseEvent AlarmAcknowledged(ak))
                    Case "resync"
                        Dim rs As ResyncWire = JsonSerializer.Deserialize(Of ResyncWire)(frame.Data, WireJson.ClientRead)
                        _lastAlarmSeq = Nothing
                        Safe(Sub() RaiseEvent ResyncRequired(If(rs, New ResyncWire())))
                    Case "bye"
                        Dim b As ByeWire = JsonSerializer.Deserialize(Of ByeWire)(frame.Data, WireJson.ClientRead)
                        Dim reason As String = If(b?.Reason, "")
                        If reason = "revoked" OrElse reason = "sessionExpired" Then result.EndSession = reason Else result.Immediate = True
                End Select
            Catch ex As JsonException
                Diagnostics.Debug.WriteLine("SSE: حدث غير مفهوم تم تجاهله (" & frame.EventName & ")")
            End Try
            Return False
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            StopAsync().GetAwaiter().GetResult()
        End Sub
    End Class

End Namespace
