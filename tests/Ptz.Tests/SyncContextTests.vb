Imports System.Collections.Concurrent
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports Ptz.Core.Alarms
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Presentation
Imports Xunit

''' <summary>انحدار: FormClosing يستدعي Dispose() المتزامن؛ يجب ألا يتجمّد على سياق تزامن أحادي الخيط (WinForms).</summary>
Public Class SyncContextTests
    Private NotInheritable Class PumpContext
        Inherits SynchronizationContext
        Private ReadOnly _q As New BlockingCollection(Of Action)()
        Public Overrides Sub Post(d As SendOrPostCallback, state As Object)
            _q.Add(Sub() d(state))
        End Sub
        Public Sub RunUntil(done As Task)
            While Not done.IsCompleted
                Dim a As Action = Nothing
                If _q.TryTake(a, 20) Then a()
            End While
        End Sub
    End Class

    <Fact>
    Public Sub Dispose_OnSingleThreadedSyncContext_DoesNotDeadlock() 
        Dim finished As Boolean = False
        Dim t As New Thread(Sub()
                                Dim ctx As New PumpContext()
                                SynchronizationContext.SetSynchronizationContext(ctx)
                                Dim h As New FakeCameraHandler()
                                h.On("GET", "/ISAPI/Streaming/channels/101/picture", Function(r) New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(New Byte() {&HFF, &HD8, 1, 2})})
                                h.OnXml("GET", "/ISAPI/System/deviceInfo", "<DeviceInfo><serialNumber>S</serialNumber></DeviceInfo>")
                                h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/capabilities", "<PTZChanelCap/>")
                                h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/presets", "<PTZPresetList/>")
                                h.OnXml("GET", "/ISAPI/Image/channels/1/color", "<Color/>")
                                h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/status", "<PTZStatus/>")
                                Dim view As New FakeFormView()
                                Dim deps As New CameraSessionDeps With {
                                    .Connection = New CameraConnection With {.Host = "10.1.1.10", .Username = "admin", .Password = "pw"},
                                    .Session = New UserSession("t", Role.Operator, DateTime.UtcNow, TimeSpan.FromHours(1)),
                                    .Access = New AccessControl(), .Audit = New ListAudit(), .Bus = New AlarmBus(), .Decoder = New FakeDecoder(),
                                    .Dispatcher = New ImmediateDispatcher(), .DataDirectory = IO.Path.GetTempPath(), .HttpHandler = h, .CameraName = "probe"}
                                Dim p As New Ds2Se7c432mwPresenter(view, deps)
                                Dim start As Task = p.StartAsync()
                                ctx.RunUntil(start)
                                Thread.Sleep(300)
                                p.Dispose()          ' كما يفعل FormClosing
                                finished = True
                            End Sub)
        t.IsBackground = True
        t.Start()
        Dim ok As Boolean = t.Join(8000)
        Assert.True(ok AndAlso finished, "Dispose() على خيط بسياق تزامن أحادي تجمّد (deadlock)")
    End Sub
End Class
