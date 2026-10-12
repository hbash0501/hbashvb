Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Engine
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision
Imports Ptz.Presentation
Imports Xunit

''' <summary>يثبت أن المقدِّمات تعمل مع واجهة خلفية غير محلية عبر الحدود الجديدة فقط (بلا HTTP حقيقي).</summary>
Public Class RemoteSeamTests

    Private NotInheritable Class FakeEngine
        Implements ISmartEngine
        Public Event FrameProcessed As Action(Of SmartFrameResult) Implements ISmartEngine.FrameProcessed
        Public Event Faulted As Action(Of Exception, Integer) Implements ISmartEngine.Faulted
        Public Event CommandFailed As Action(Of Exception) Implements ISmartEngine.CommandFailed
        Public Property MotionEnabled As Boolean Implements ISmartEngine.MotionEnabled
        Public Property BehaviorEnabled As Boolean Implements ISmartEngine.BehaviorEnabled
        Public Property AutoTrackEnabled As Boolean Implements ISmartEngine.AutoTrackEnabled
        Public Property ForceAnomalyLearning As Boolean? Implements ISmartEngine.ForceAnomalyLearning
        Public ReadOnly Calls As New List(Of String)()
        Public Started As Boolean
        Public Sub ApplyBehaviorOptions(src As BehaviorOptions) Implements ISmartEngine.ApplyBehaviorOptions
            Calls.Add("opts")
        End Sub
        Public Sub LockTargetAt(point As NormPoint) Implements ISmartEngine.LockTargetAt
            Calls.Add("lock")
        End Sub
        Public Sub ReleaseTarget() Implements ISmartEngine.ReleaseTarget
            Calls.Add("release")
        End Sub
        Public Sub NotifyCameraMotion(Optional extraSeconds As Double = 0) Implements ISmartEngine.NotifyCameraMotion
            Calls.Add("motion")
        End Sub
        Public Sub Start() Implements ISmartEngine.Start
            Started = True
        End Sub
        Public Function StopAsync() As Task Implements ISmartEngine.StopAsync
            Started = False
            Return Task.CompletedTask
        End Function
        Public Sub RaiseFrame(r As SmartFrameResult)
            RaiseEvent FrameProcessed(r)
        End Sub
        Public Sub RaiseFailed(ex As Exception)
            RaiseEvent CommandFailed(ex)
        End Sub
    End Class

    Private NotInheritable Class FakeConfig
        Implements ISmartConfigRepository
        Public Saved As SmartGeometry
        Public Fail As Exception
        Public ModelSaves As Integer
        Public Function LoadAsync(ct As Threading.CancellationToken) As Task(Of SmartGeometry) Implements ISmartConfigRepository.LoadAsync
            Dim z As New RestrictedZone("Gateway Zone", {New NormPoint(0, 0), New NormPoint(1, 0), New NormPoint(1, 1)})
            Return Task.FromResult(New SmartGeometry({z}, New TripLine() {}, New IReadOnlyList(Of NormPoint)() {}))
        End Function
        Public Function SaveGeometryAsync(geometry As SmartGeometry, ct As Threading.CancellationToken) As Task Implements ISmartConfigRepository.SaveGeometryAsync
            If Fail IsNot Nothing Then Return Task.FromException(Fail)
            Saved = geometry
            Return Task.CompletedTask
        End Function
        Public Function SaveAnomalyModelAsync(ct As Threading.CancellationToken) As Task Implements ISmartConfigRepository.SaveAnomalyModelAsync
            ModelSaves += 1
            Return Task.CompletedTask
        End Function
    End Class

    Private NotInheritable Class FakeFeed
        Implements IJpegFeed
        Public Event FrameCaptured As Action(Of Byte()) Implements IJpegFeed.FrameCaptured
        Public Sub Push(b As Byte())
            RaiseEvent FrameCaptured(b)
        End Sub
    End Class

    Private NotInheritable Class FakeBackend
        Implements ICameraBackend, ICameraBackendFactory
        Public ReadOnly Engine As New FakeEngine()
        Public ReadOnly Config As New FakeConfig()
        Public ReadOnly Feed As New FakeFeed()
        Private _controller As SecuredCameraController
        Public Disposed As Boolean

        Public Function Create(profile As CameraProfile, deps As CameraSessionDeps, secure As Func(Of ICameraController, SecuredCameraController),
                               configureEngine As Action(Of SmartEngineOptions)) As ICameraBackend Implements ICameraBackendFactory.Create
            _controller = secure(Ptz.Core.Cameras.CameraControllerFactory.Create(profile, deps.Connection, deps.HttpHandler))
            Return Me
        End Function
        Public ReadOnly Property Controller As SecuredCameraController Implements ICameraBackend.Controller
            Get
                Return _controller
            End Get
        End Property
        Public ReadOnly Property SmartEngine As ISmartEngine Implements ICameraBackend.Engine
            Get
                Return Engine
            End Get
        End Property
        Public ReadOnly Property SmartConfig As ISmartConfigRepository Implements ICameraBackend.SmartConfig
            Get
                Return Config
            End Get
        End Property
        Public ReadOnly Property Video As IJpegFeed Implements ICameraBackend.Video
            Get
                Return Feed
            End Get
        End Property
        Public Function StartBackground(ct As Threading.CancellationToken) As Task Implements ICameraBackend.StartBackground
            Return Task.CompletedTask
        End Function
        Public Sub Dispose() Implements IDisposable.Dispose
            Disposed = True
        End Sub
    End Class

    Private Shared Function Handler() As FakeCameraHandler
        Dim h As New FakeCameraHandler()
        h.OnXml("GET", "/ISAPI/System/deviceInfo", "<DeviceInfo><deviceName>Gate</deviceName><model>DS-2SE7C432MW-AEB</model><serialNumber>SN9</serialNumber></DeviceInfo>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/capabilities", "<PTZChanelCap/>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/presets", "<PTZPresetList/>")
        h.OnXml("GET", "/ISAPI/Image/channels/1/color", "<Color><brightnessLevel>40</brightnessLevel></Color>")
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/momentary", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", FakeCameraHandler.OkStatus)
        Return h
    End Function

    Private Shared Function NewPresenter(role As Role, backend As FakeBackend, view As FakeFormView) As Ds2Se7c432mwPresenter
        Dim deps As New CameraSessionDeps With {
            .Connection = New CameraConnection With {.Host = "gw.example", .Username = "u", .Password = "p"},
            .Session = New UserSession("tester", role, DateTime.UtcNow, TimeSpan.FromHours(1)),
            .Access = New AccessControl(), .Audit = New ListAudit(), .Bus = New AlarmBus(cooldownSeconds:=0), .Decoder = New FakeDecoder(),
            .Dispatcher = New ImmediateDispatcher(), .DataDirectory = IO.Path.GetTempPath(), .HttpHandler = Handler(), .CameraName = "remoteCam",
            .BackendFactory = backend}
        Return New Ds2Se7c432mwPresenter(view, deps)
    End Function

    <Fact>
    Public Async Function RemoteBackend_FlowsFramesOverlaysAndGeometryToViews() As Task
        Dim backend As New FakeBackend()
        Dim view As New FakeFormView()
        Using p As Ds2Se7c432mwPresenter = NewPresenter(Role.Supervisor, backend, view)
            Await p.StartAsync()
            Assert.True(view.Connected)
            Assert.True(backend.Engine.Started)
            Assert.Same(backend.Engine, p.Engine)
            Assert.Equal(1, view.VideoView.Zones)               ' الهندسة وصلت من المخزن (البوابة) لا من ملف محلي
            backend.Feed.Push(New Byte() {1, 2, 3})
            Assert.Equal(1, view.VideoView.Frames)
            backend.Engine.RaiseFrame(New SmartFrameResult With {.TimestampUtc = DateTime.UtcNow})
            view.PtzView.RaiseMove(New PtzVector(20, 0, 0))
            Assert.Contains("motion", backend.Engine.Calls)
            Await p.StopAsync()
            Assert.False(backend.Engine.Started)
            Assert.True(backend.Disposed)
        End Using
    End Function

    <Fact>
    Public Async Function RemoteBackend_CommandFailure_ShowsError_AndGeometryRejectionRollsBack() As Task
        Dim backend As New FakeBackend()
        Dim view As New FakeFormView()
        Using p As Ds2Se7c432mwPresenter = NewPresenter(Role.Supervisor, backend, view)
            Await p.StartAsync()
            backend.Engine.RaiseFailed(New AccessDeniedException(Permission.AutoTrack, "denied"))
            Assert.Contains(UiText.NoPermission, view.Errors)

            backend.Config.Fail = New IsapiException(503, "", "gateway down")
            view.VideoView.RaiseLine(New NormPoint(0.1, 0.5), New NormPoint(0.9, 0.5))
            Assert.Contains("gateway down", view.Errors)
            Assert.Equal(0, view.VideoView.Lines)               ' لم يُعرض خط رفضته البوابة
            backend.Config.Fail = Nothing
            view.VideoView.RaiseLine(New NormPoint(0.1, 0.5), New NormPoint(0.9, 0.5))
            Assert.Equal(1, view.VideoView.Lines)
            Assert.Equal(1, backend.Config.Saved.Lines.Count)
            Assert.Equal(1, backend.Config.Saved.Zones.Count)
            view.SmartView.RaiseSaveModel()
            Assert.Equal(1, backend.Config.ModelSaves)
            Await p.StopAsync()
        End Using
    End Function

    <Fact>
    Public Async Function Viewer_CannotConfigureGeometry_EvenWithRemoteBackend() As Task
        Dim backend As New FakeBackend()
        Dim view As New FakeFormView()
        Using p As Ds2Se7c432mwPresenter = NewPresenter(Role.Viewer, backend, view)
            Await p.StartAsync()
            view.VideoView.RaiseLine(New NormPoint(0.1, 0.5), New NormPoint(0.9, 0.5))
            Assert.Contains(UiText.NoPermission, view.Errors)
            Assert.Null(backend.Config.Saved)
            Await p.StopAsync()
        End Using
    End Function
End Class

Public Class AlarmBusRemoteTests
    <Fact>
    Public Sub PublishRemote_KeepsIdAndTime_IsIdempotent_AndIgnoresCooldown()
        Dim bus As New AlarmBus(cooldownSeconds:=60)
        Dim got As New List(Of AlarmEvent)()
        bus.Subscribe(Sub(a) got.Add(a))
        Dim t As New DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
        Dim a1 As New AlarmEvent With {.Camera = "c", .Kind = "Intrusion", .TrackId = 7, .TimeUtc = t}
        Dim a2 As New AlarmEvent With {.Camera = "c", .Kind = "Intrusion", .TrackId = 7, .TimeUtc = t.AddSeconds(1)}
        Assert.True(bus.PublishRemote(a1))
        Assert.True(bus.PublishRemote(a2))            ' نفس المفتاح لكن معرّف مختلف: لا تبريد
        Assert.False(bus.PublishRemote(a1))           ' إعادة بث بعد انقطاع: مكرَّر بالمعرّف
        Assert.Equal(2, got.Count)
        Assert.Equal(t, got(0).TimeUtc)
        Assert.Equal(a1.Id, bus.Recent(5)(1).Id)
    End Sub

    <Fact>
    Public Sub SecuredController_DoesNotDisposeSharedInner_WhenNotOwner()
        Dim h As New FakeCameraHandler()
        Dim raw As ICameraController = Ptz.Core.Cameras.CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb,
            New CameraConnection With {.Host = "10.0.0.1", .Username = "u", .Password = "p"}, h)
        Dim sess As New UserSession("u", Role.Viewer, DateTime.UtcNow)
        Dim sec As New SecuredCameraController(raw, sess, New AccessControl(), New ListAudit(), Nothing, ownsInner:=False)
        sec.Dispose()
        ' المتحكم الخام ما زال صالحاً للاستخدام من جلسة أخرى (لن يرمي ObjectDisposedException).
        Dim ex As Exception = Record.Exception(Sub() Assert.NotNull(raw.Profile))
        Assert.Null(ex)
    End Sub
End Class
