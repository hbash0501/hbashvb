Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Cameras
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security

''' <summary>
''' الوضع البعيد: كل شيء يمر عبر بوابة LAN التي تملك بيانات اعتماد الكاميرات وتشغّل المحرك الذكي على مدار الساعة.
''' التطبيق هنا عميل: متحكم REST، محرك وكيل، لقطات تكيّفية، وجسر إنذارات (SSE ⇄ <see cref="AlarmBus"/>).
''' </summary>
Public NotInheritable Class GatewayBackendFactory
    Implements ICameraBackendFactory

    Private ReadOnly _client As GatewayClient

    Public Sub New(client As GatewayClient)
        If client Is Nothing Then Throw New ArgumentNullException(NameOf(client))
        _client = client
    End Sub

    Public Function Create(profile As CameraProfile, deps As CameraSessionDeps,
                           secure As Func(Of ICameraController, SecuredCameraController),
                           configureEngine As Action(Of SmartEngineOptions)) As ICameraBackend Implements ICameraBackendFactory.Create
        Dim id As String = GatewayCameraIds.ForProfile(profile)
        Dim controller As SecuredCameraController = secure(New RemoteCameraController(_client, id, profile))
        Dim stream As New GatewayEventStream(_client, id)
        Dim engine As New RemoteSmartEngine(_client, id, stream)
        Dim selector As New TierSelector(FrameLadder.Entries.Select(Function(e) e.Id), "low")
        Dim feed As New RemoteJpegFeed(_client, id, selector)
        Return New GatewayCameraBackend(_client, id, deps, controller, engine, stream, feed, New RemoteSmartConfigRepository(_client, id), selector)
    End Function
End Class

Friend NotInheritable Class GatewayCameraBackend
    Implements ICameraBackend

    Private ReadOnly _client As GatewayClient
    Private ReadOnly _cameraId As String
    Private ReadOnly _deps As CameraSessionDeps
    Private ReadOnly _stream As GatewayEventStream
    Private ReadOnly _feed As RemoteJpegFeed
    Private ReadOnly _selector As TierSelector
    Private _disposed As Integer

    Public Sub New(client As GatewayClient, cameraId As String, deps As CameraSessionDeps, controller As SecuredCameraController,
                   engine As RemoteSmartEngine, stream As GatewayEventStream, feed As RemoteJpegFeed, config As ISmartConfigRepository, selector As TierSelector)
        _client = client
        _cameraId = cameraId
        _deps = deps
        _stream = stream
        _feed = feed
        _selector = selector
        Me.Controller = controller
        Me.Engine = engine
        SmartConfig = config
        Video = feed
        AddHandler _stream.Connected, AddressOf OnHello
        AddHandler _stream.AlarmReceived, AddressOf OnAlarm
        AddHandler _stream.AlarmAcknowledged, AddressOf OnRemoteAck
        AddHandler _stream.ResyncRequired, AddressOf OnResync
        AddHandler _deps.Bus.Acknowledged, AddressOf OnLocalAck
    End Sub

    Public ReadOnly Property Controller As SecuredCameraController Implements ICameraBackend.Controller
    Public ReadOnly Property Engine As ISmartEngine Implements ICameraBackend.Engine
    Public ReadOnly Property SmartConfig As ISmartConfigRepository Implements ICameraBackend.SmartConfig
    Public ReadOnly Property Video As IJpegFeed Implements ICameraBackend.Video

    Private Sub OnHello(h As HelloWire)
        If h.Tiers IsNot Nothing AndAlso h.Tiers.Count > 0 Then _selector.SetAvailable(h.Tiers)
        ' عند كل (إعادة) اتصال: ما حدث أثناء الانقطاع قد لا يُعاد بث الأحداث إن لم يكن لدينا معرّف استئناف بعد؛ نجلب آخر الإنذارات
        ' (رخيص ومتساوي القوة: يُتجاهل المكرر بالمعرّف).
        Task.Run(Function() LoadBacklogAsync(Threading.CancellationToken.None))
    End Sub

    Private Sub OnAlarm(dto As AlarmDto)
        _deps.Bus.PublishRemote(WireMap.FromAlarmDto(dto, _deps.CameraName))
    End Sub

    Private Sub OnRemoteAck(ack As AckWire)
        _deps.Bus.AcknowledgeRemote(ack.Id)
    End Sub

    ''' <summary>إقرار محلي بإنذار هذه الكاميرا ⇒ أبلغ البوابة (تعميمه على بقية المشغّلين).</summary>
    Private Sub OnLocalAck(alarm As AlarmEvent)
        If alarm.Camera <> _deps.CameraName Then Return
        Dim id As Guid = alarm.Id
        Task.Run(Async Function()
                     Try
                         Using cts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(10))
                             Await _client.RequestAsync(HttpMethod.Post, "/api/v1/alarms/" & id.ToString() & "/ack", Nothing, cts.Token).ConfigureAwait(False)
                         End Using
                     Catch ex As Exception When TypeOf ex Is Ptz.Core.Isapi.IsapiException OrElse TypeOf ex Is AccessDeniedException OrElse
                                               TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is OperationCanceledException
                         Diagnostics.Debug.WriteLine("Remote ack failed: " & ex.Message)
                     End Try
                 End Function)
    End Sub

    Private Sub OnResync(w As ResyncWire)
        Task.Run(Function() LoadBacklogAsync(Threading.CancellationToken.None))
    End Sub

    ''' <summary>يستورد آخر إنذارات الكاميرا (يتجاهل المكرر بالمعرّف) كي تظهر في اللوحة عند الفتح أو بعد فجوة.</summary>
    Private Async Function LoadBacklogAsync(ct As Threading.CancellationToken) As Task
        Try
            Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                cts.CancelAfter(TimeSpan.FromSeconds(15))
                Dim list As AlarmListDto = (Await _client.RequestAsync(HttpMethod.Get, $"/api/v1/alarms?camera={_cameraId}&limit=50", Nothing, cts.Token).ConfigureAwait(False)).Json(Of AlarmListDto)()
                For Each a As AlarmDto In list.Items
                    Dim ev As AlarmEvent = WireMap.FromAlarmDto(a, _deps.CameraName)
                    If _deps.Bus.PublishRemote(ev) AndAlso a.Acknowledged Then _deps.Bus.AcknowledgeRemote(ev.Id)
                Next
            End Using
        Catch ex As Exception When TypeOf ex Is Ptz.Core.Isapi.IsapiException OrElse TypeOf ex Is AccessDeniedException OrElse
                                  TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is ArgumentException
            Diagnostics.Debug.WriteLine("Backlog load failed: " & ex.Message)
        End Try
    End Function

    Public Function StartBackground(ct As Threading.CancellationToken) As Task Implements ICameraBackend.StartBackground
        Return Task.Run(Async Function()
                            _feed.Start(ct)
                            Await LoadBacklogAsync(ct).ConfigureAwait(False)
                            Try
                                Await Task.Delay(Threading.Timeout.Infinite, ct).ConfigureAwait(False)
                            Catch ex As OperationCanceledException
                            End Try
                            Await _feed.StopAsync().ConfigureAwait(False)
                        End Function)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If Threading.Interlocked.Exchange(_disposed, 1) <> 0 Then Return
        RemoveHandler _stream.Connected, AddressOf OnHello
        RemoveHandler _stream.AlarmReceived, AddressOf OnAlarm
        RemoveHandler _stream.AlarmAcknowledged, AddressOf OnRemoteAck
        RemoveHandler _stream.ResyncRequired, AddressOf OnResync
        RemoveHandler _deps.Bus.Acknowledged, AddressOf OnLocalAck
    End Sub
End Class
