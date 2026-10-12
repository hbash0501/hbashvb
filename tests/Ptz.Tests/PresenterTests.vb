Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision
Imports Ptz.Presentation
Imports Xunit

Public Class PresenterTests

    Private Shared Function JpegHandler() As FakeCameraHandler
        Dim h As New FakeCameraHandler()
        Dim jpg As Byte() = {&HFF, &HD8, &HFF, &HE0, 1, 2, 3, 4}
        h.On("GET", "/ISAPI/Streaming/channels/101/picture", Function(r) New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(jpg)})
        h.OnXml("GET", "/ISAPI/System/deviceInfo", "<DeviceInfo><deviceName>Gate</deviceName><model>DS-2SE7C432MW-AEB</model><serialNumber>SN1</serialNumber><firmwareVersion>V5.7</firmwareVersion></DeviceInfo>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/capabilities", "<PTZChanelCap/>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/presets", "<PTZPresetList><PTZPreset><id>1</id><enabled>true</enabled><presetName>Door</presetName></PTZPreset></PTZPresetList>")
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/momentary", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/presets/1/goto", FakeCameraHandler.OkStatus)
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/status", "<PTZStatus><AbsoluteHigh><elevation>0</elevation><azimuth>900</azimuth><absoluteZoom>10</absoluteZoom></AbsoluteHigh></PTZStatus>")
        h.OnXml("GET", "/ISAPI/Image/channels/1/color", "<Color><brightnessLevel>40</brightnessLevel><contrastLevel>50</contrastLevel><saturationLevel>60</saturationLevel></Color>")
        h.OnXml("PUT", "/ISAPI/Image/channels/1/color", FakeCameraHandler.OkStatus)
        Return h
    End Function

    Private NotInheritable Class Rig
        Implements IDisposable
        Public ReadOnly View As New FakeFormView()
        Public ReadOnly Handler As FakeCameraHandler
        Public ReadOnly Audit As New ListAudit()
        Public ReadOnly Bus As New AlarmBus(cooldownSeconds:=0)
        Public ReadOnly Dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-pres-" & Guid.NewGuid().ToString("N"))
        Public ReadOnly Presenter As CameraSessionPresenter

        Public Sub New(role As Role, Optional model As String = "se", Optional handler As FakeCameraHandler = Nothing, Optional name As String = "camA")
            Me.Handler = If(handler, JpegHandler())
            Dim clock As New TestClock()
            Dim deps As New CameraSessionDeps With {
                .Connection = New CameraConnection With {.Host = "10.1.1.10", .Username = "admin", .Password = "pw"},
                .Session = New UserSession("tester", role, DateTime.UtcNow, TimeSpan.FromHours(1)),
                .Access = New AccessControl(), .Audit = Audit, .Bus = Bus, .Decoder = New FakeDecoder(),
                .Dispatcher = New ImmediateDispatcher(), .DataDirectory = Dir, .HttpHandler = Me.Handler, .CameraName = name}
            Presenter = If(model = "se", CType(New Ds2Se7c432mwPresenter(View, deps), CameraSessionPresenter), New Ds2De7232iwPresenter(View, deps))
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Presenter.Dispose()
        End Sub
    End Class

    Private Shared Async Function WaitFor(cond As Func(Of Boolean), Optional seconds As Double = 5) As Task
        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While Not cond() AndAlso sw.Elapsed.TotalSeconds < seconds
            Await Task.Delay(15)
        End While
    End Function

    Private Shared Function Count(h As FakeCameraHandler, method As String, path As String) As Integer
        SyncLock h.Requests
            Return h.Requests.Where(Function(r) r.Method = method AndAlso r.Path = path).Count()
        End SyncLock
    End Function

    <Fact>
    Public Async Function Start_ConnectsLoadsPanelsAndStreamsFrames() As Task
        Using rig As New Rig(Role.Operator)
            Await rig.Presenter.StartAsync()
            Assert.True(rig.View.Connected)
            Assert.Equal("SN1", rig.View.Device.SerialNumber)
            Assert.Contains("DS-2SE7C432MW-AEB", rig.View.Title)
            Assert.Equal("Door", rig.View.PtzView.Presets.Single().Name)
            Await WaitFor(Function() rig.View.VideoView.Frames >= 3)
            Assert.True(rig.View.VideoView.Frames >= 3, "الفيديو لا يصل للواجهة")
            Assert.Equal(RolePermissions.For(Role.Operator), rig.View.LastPermissions)
            Await rig.Presenter.StopAsync()
            Assert.False(rig.View.Connected)
        End Using
    End Function

    <Fact>
    Public Async Function Joystick_Move_ReachesCamera_AndStopIsSent_ForOperator() As Task
        Using rig As New Rig(Role.Operator)
            Await rig.Presenter.StartAsync()
            rig.View.PtzView.RaiseMove(New PtzVector(50, 0, 0))
            Await WaitFor(Function() Count(rig.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary") >= 1)
            rig.View.PtzView.RaiseStop()
            Await WaitFor(Function() Count(rig.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous") >= 1)
            Assert.True(Count(rig.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary") >= 1)
            Assert.True(Count(rig.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous") >= 1)
            Await rig.Presenter.StopAsync()
        End Using
    End Function

    <Fact>
    Public Async Function Viewer_CannotMove_SeesPermissionMessage_AndNothingIsSent() As Task
        Using rig As New Rig(Role.Viewer)
            Await rig.Presenter.StartAsync()
            rig.View.PtzView.RaiseMove(New PtzVector(50, 0, 0))
            Await WaitFor(Function() rig.View.Errors.Count > 0)
            Assert.Contains(UiText.NoPermission, rig.View.Errors)
            Assert.Equal(0, Count(rig.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
            Assert.Contains("tester|Ptz.Move|DENIED", rig.Audit.Entries)
            Await rig.Presenter.StopAsync()
        End Using
    End Function

    <Fact>
    Public Async Function PresetGo_AndImageApply_WorkForSupervisor() As Task
        Using rig As New Rig(Role.Supervisor)
            Await rig.Presenter.StartAsync()
            rig.View.PtzView.RaisePresetGo(1)
            Dim s As New ImageSettings()
            s(ImageParam.Brightness) = "75"
            rig.View.ImageView.RaiseApply(s)
            Await WaitFor(Function() Count(rig.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/presets/1/goto") = 1 AndAlso Count(rig.Handler, "PUT", "/ISAPI/Image/channels/1/color") = 1)
            Assert.Equal(1, Count(rig.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/presets/1/goto"))
            Assert.Equal(1, Count(rig.Handler, "PUT", "/ISAPI/Image/channels/1/color"))
            Assert.Empty(rig.View.Errors)
            Await rig.Presenter.StopAsync()
        End Using
    End Function

    <Fact>
    Public Async Function ZoneDrawing_RequiresSmartConfigure_PersistsAndReloads() As Task
        Dim poly As New List(Of NormPoint) From {New NormPoint(0.1, 0.1), New NormPoint(0.5, 0.1), New NormPoint(0.5, 0.5)}
        Dim dir As String
        Using rig As New Rig(Role.Operator)
            Await rig.Presenter.StartAsync()
            rig.View.VideoView.RaiseZone(poly, GeometryDrawMode.Zone)
            Assert.Contains(UiText.NoPermission, rig.View.Errors)
            Assert.Equal(0, rig.View.VideoView.Zones)
            Await rig.Presenter.StopAsync()
        End Using
        Using rig As New Rig(Role.Supervisor)
            dir = rig.Dir
            Await rig.Presenter.StartAsync()
            rig.View.VideoView.RaiseZone(poly, GeometryDrawMode.Zone)
            rig.View.VideoView.RaiseLine(New NormPoint(0.1, 0.5), New NormPoint(0.9, 0.5))
            Assert.Equal(1, rig.View.VideoView.Zones)
            Assert.Equal(1, rig.View.VideoView.Lines)
            Await rig.Presenter.StopAsync()
        End Using
        ' فتح جديد بنفس الاسم والمجلد يعيد المناطق
        Dim again As New Rig(Role.Supervisor)
        Try
            IO.Directory.CreateDirectory(dir)
            Dim zones As New List(Of RestrictedZone)()
            Dim lines As New List(Of TripLine)()
            Dim masks As New List(Of IReadOnlyList(Of NormPoint))()
            GeometryStore.Load(IO.Path.Combine(dir, "camA-geometry.json"), zones, lines, masks)
            Assert.Single(zones)
            Assert.Single(lines)
        Finally
            again.Dispose()
        End Try
    End Function

    <Fact>
    Public Async Function AutoTrackToggle_NeedsPermission_AndIsAudited() As Task
        Using rig As New Rig(Role.Viewer)
            Await rig.Presenter.StartAsync()
            rig.View.SmartView.RaiseAutoTrack(True)
            Assert.False(rig.Presenter.Engine.AutoTrackEnabled)
            Assert.Contains("tester|AutoTrack.Toggle|DENIED", rig.Audit.Entries)
            Await rig.Presenter.StopAsync()
        End Using
        Using rig As New Rig(Role.Operator)
            Await rig.Presenter.StartAsync()
            rig.View.SmartView.RaiseAutoTrack(True)
            Assert.True(rig.Presenter.Engine.AutoTrackEnabled)
            Assert.Contains("tester|AutoTrack.Toggle|OK", rig.Audit.Entries)
            Await rig.Presenter.StopAsync()
        End Using
    End Function

    <Fact>
    Public Async Function Alarms_FlowToPanel_OnlyForThisCamera_AndAckNeedsPermission() As Task
        Using rig As New Rig(Role.Operator)
            Await rig.Presenter.StartAsync()
            rig.Bus.Publish(New AlarmEvent With {.Camera = "camA", .Kind = "Intrusion", .Severity = AlarmSeverity.Critical})
            rig.Bus.Publish(New AlarmEvent With {.Camera = "other", .Kind = "Intrusion"})
            Assert.Single(rig.View.AlarmView.Alarms)
            rig.View.AlarmView.RaiseAck(rig.View.AlarmView.Alarms(0).Id)
            Assert.Single(rig.View.AlarmView.Acked)
            Await rig.Presenter.StopAsync()
        End Using
        Using rig As New Rig(Role.Viewer)
            Await rig.Presenter.StartAsync()
            rig.Bus.Publish(New AlarmEvent With {.Camera = "camA", .Kind = "Motion"})
            rig.View.AlarmView.RaiseAck(rig.View.AlarmView.Alarms(0).Id)
            Assert.Empty(rig.View.AlarmView.Acked)
            Assert.Contains(UiText.NoPermission, rig.View.Errors)
            Await rig.Presenter.StopAsync()
        End Using
    End Function

    <Fact>
    Public Async Function ConnectFailure_ShowsError_StaysDisconnected_ThenReconnectWorks() As Task
        Dim h As FakeCameraHandler = JpegHandler()
        Dim fail As Boolean = True
        h.On("GET", "/ISAPI/System/deviceInfo",
             Function(r)
                 If fail Then Return New HttpResponseMessage(HttpStatusCode.Unauthorized)
                 Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("<DeviceInfo><serialNumber>OK1</serialNumber></DeviceInfo>")}
             End Function)
        Using rig As New Rig(Role.Operator, handler:=h)
            Await rig.Presenter.StartAsync()
            Assert.False(rig.View.Connected)
            Assert.Contains(rig.View.Errors, Function(e) e.Contains("401"))
            fail = False
            rig.View.RaiseReconnect()
            Await WaitFor(Function() rig.View.Connected)
            Assert.True(rig.View.Connected)
            Assert.Equal("OK1", rig.View.Device.SerialNumber)
            Await rig.Presenter.StopAsync()
        End Using
    End Function

    <Fact>
    Public Async Function BothCameraPresenters_RunSideBySide_WithSeparateState() As Task
        Using a As New Rig(Role.Operator, "se", name:="cam-se"), b As New Rig(Role.Operator, "de", name:="cam-de")
            Await Task.WhenAll(a.Presenter.StartAsync(), b.Presenter.StartAsync())
            Assert.Contains("DS-2SE7C432MW-AEB", a.View.Title)
            Assert.Contains("DS-2DE7232IW-AE", b.View.Title)
            a.View.PtzView.RaiseMove(New PtzVector(30, 0, 0))
            Await WaitFor(Function() Count(a.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary") >= 1)
            Assert.Equal(0, Count(b.Handler, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
            Await Task.WhenAll(a.Presenter.StopAsync(), b.Presenter.StopAsync())
        End Using
    End Function

    ' ---------- المدخل والمحور ----------
    Private Shared Function TempDir() As String
        Dim d As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-shell-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(d)
        Return d
    End Function

    Private NotInheritable Class FakeLogin
        Implements ILoginView
        Public Event SubmitRequested As Action(Of String, String, String) Implements ILoginView.SubmitRequested
        Public Mode As LoginMode
        Public Errors As New List(Of String)()
        Public Closed As Boolean
        Public Sub SetMode(m As LoginMode) Implements ILoginView.SetMode
            Mode = m
        End Sub
        Public Sub SetBusy(busy As Boolean) Implements ILoginView.SetBusy
        End Sub
        Public Sub CloseWithSuccess() Implements ILoginView.CloseWithSuccess
            Closed = True
        End Sub
        Public Sub ShowError(message As String) Implements IMessageSink.ShowError
            Errors.Add(message)
        End Sub
        Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
        End Sub
        Public Sub Submit(u As String, p As String, c As String)
            RaiseEvent SubmitRequested(u, p, c)
        End Sub
    End Class

    <Fact>
    Public Async Function Login_FirstRunCreatesAdmin_ThenSignInWorks_AndWrongPasswordFails() As Task
        Dim dir As String = TempDir()
        Dim users As New UserStore(IO.Path.Combine(dir, "u.json"), Nothing, Nothing, 1000)
        Dim view As New FakeLogin()
        Dim presenter As New LoginPresenter(view, users)
        Dim got As UserSession = Nothing
        AddHandler presenter.SessionEstablished, Sub(s) got = s
        Assert.Equal(LoginMode.FirstRunCreateAdmin, view.Mode)

        view.Submit("admin", "Str0ng!Passw0rd", "different")
        Await WaitFor(Function() view.Errors.Count = 1)
        Assert.False(users.HasAnyUser)

        view.Submit("admin", "weak", "weak")
        Await WaitFor(Function() view.Errors.Count = 2)
        Assert.False(users.HasAnyUser)

        view.Submit("admin", "Str0ng!Passw0rd", "Str0ng!Passw0rd")
        Await WaitFor(Function() view.Closed)
        Assert.True(view.Closed)
        Assert.Equal(Role.Administrator, got.Role)

        Dim view2 As New FakeLogin()
        Dim p2 As New LoginPresenter(view2, users)
        Assert.Equal(LoginMode.SignIn, view2.Mode)
        view2.Submit("admin", "wrong-password", "")
        Await WaitFor(Function() view2.Errors.Count = 1)
        Assert.False(view2.Closed)
    End Function

    <Fact>
    Public Sub ConfigStore_EncryptsPasswordAtRest_AndBuildsConnection()
        Dim dir As String = TempDir()
        Dim vault As New CredentialVault(New FileKeyProvider(IO.Path.Combine(dir, "k")))
        Dim store As New CameraConfigStore(IO.Path.Combine(dir, "cams.json"), vault)
        store.EnsureDefaults({("se", Ptz.Core.Models.CameraProfiles.Ds2Se7c432mwAeb)})
        Dim cfg As CameraConfig = store.Get("se")
        cfg.Host = "10.2.2.2"
        cfg.Username = "admin"
        store.Update(cfg, "Cam-P@ss-123")
        Assert.DoesNotContain("Cam-P@ss-123", IO.File.ReadAllText(IO.Path.Combine(dir, "cams.json")))
        Assert.Equal("Cam-P@ss-123", store.BuildConnection("se").Password)
        ' تحديث بدون كلمة مرور يبقيها
        cfg.Port = 8443
        store.Update(cfg, Nothing)
        Assert.Equal("Cam-P@ss-123", store.BuildConnection("se").Password)
        Assert.Equal(8443, store.BuildConnection("se").Port)
        ' إعدادات غير صالحة تُرفض
        cfg.Host = "bad host/x"
        Assert.Throws(Of ArgumentException)(Sub() store.Update(cfg, Nothing))
        ' إعادة تحميل
        Assert.Equal("10.2.2.2", New CameraConfigStore(IO.Path.Combine(dir, "cams.json"), vault).Get("se").Host)
    End Sub

    <Fact>
    Public Sub GeometryStore_RoundTrips_ClampsValues_AndSurvivesCorruption()
        Dim path As String = IO.Path.Combine(TempDir(), "g.json")
        Dim zones As New List(Of RestrictedZone) From {New RestrictedZone("Z", {New NormPoint(0, 0), New NormPoint(1, 0), New NormPoint(1, 1)})}
        Dim lines As New List(Of TripLine) From {New TripLine("L", New NormPoint(0.1, 0.1), New NormPoint(0.9, 0.9), LineDirection.LeftToRight)}
        Dim masks As New List(Of IReadOnlyList(Of NormPoint)) From {New List(Of NormPoint) From {New NormPoint(0.2, 0.2), New NormPoint(0.4, 0.2), New NormPoint(0.4, 0.4)}}
        GeometryStore.Save(path, zones, lines, masks)
        Dim z2 As New List(Of RestrictedZone)()
        Dim l2 As New List(Of TripLine)()
        Dim m2 As New List(Of IReadOnlyList(Of NormPoint))()
        GeometryStore.Load(path, z2, l2, m2)
        Assert.Equal(LineDirection.LeftToRight, l2.Single().Direction)
        Assert.Single(z2)
        Assert.Single(m2)

        IO.File.WriteAllText(path, "{""Zones"":[{""Name"":""x"",""Severity"":99,""Points"":[{""X"":-5,""Y"":9},{""X"":0,""Y"":0},{""X"":1,""Y"":1}]}]}")
        GeometryStore.Load(path, z2, l2, m2)
        Assert.Equal(AlarmSeverity.Critical, z2.Single().Severity)
        Assert.Equal(0.0, z2.Single().Points(0).X)
        Assert.Equal(1.0, z2.Single().Points(0).Y)

        IO.File.WriteAllText(path, "not json at all")
        GeometryStore.Load(path, z2, l2, m2)
        Assert.Empty(z2)
    End Sub
End Class
