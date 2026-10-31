Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Engine
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Core.Vision
Imports Ptz.Presentation
Imports Xunit

''' <summary>العميل الإنتاجي (GatewayClient وما فوقه) ضد البوابة الحقيقية عبر TestServer: بروتوكول، أخطاء، SSE، لقطات، ومقدِّم كامل.</summary>
Public Class ClientIntegrationTests

    Private Const Se As String = "se7c432mw"
    Private ReadOnly _ct As Threading.CancellationToken = Threading.CancellationToken.None

    Private NotInheritable Class Prompt
        Implements IGatewayCredentialProvider
        Public Calls As Integer
        Public Code As Func(Of String)
        Public Function GetStepUpCodeAsync(reason As String, ct As Threading.CancellationToken) As Task(Of String) Implements IGatewayCredentialProvider.GetStepUpCodeAsync
            Threading.Interlocked.Increment(Calls)
            Return Task.FromResult(If(Code Is Nothing, Nothing, Code()))
        End Function
    End Class

    Private Shared Function Capture(Of T As Exception)(op As Func(Of Task)) As T
        Try
            op().GetAwaiter().GetResult()
        Catch ex As T
            Return ex
        End Try
        Throw New Xunit.Sdk.XunitException("لم يُرمَ الاستثناء المتوقع " & GetType(T).Name)
    End Function

    Private Shared Function Count(log As List(Of TappedRequest), method As String, pathEnd As String) As Integer
        SyncLock log
            Return log.Where(Function(r) r.Method = method AndAlso r.Path.EndsWith(pathEnd, StringComparison.Ordinal)).Count()
        End SyncLock
    End Function

    ''' <summary>نسخة آمنة من قائمة إنذارات واجهة الاختبار (تُكتب من خيط الخلفية).</summary>
    Private Shared Function Snapshot(view As FakeAlarmView) As List(Of AlarmEvent)
        SyncLock view.Alarms
            Return view.Alarms.ToList()
        End SyncLock
    End Function

    Private Shared Function CamCount(cam As FakeCameraHandler, method As String, path As String) As Integer
        SyncLock cam.Requests
            Return cam.Requests.Where(Function(r) r.Method = method AndAlso r.Path = path).Count()
        End SyncLock
    End Function

    ' ======================= الدخول =======================
    <Fact>
    Public Sub Login_AppliesTheServerRoleCap_AndRejectsBadCredentialsWithAnArabicMessage()
        Using h As New Harness()
            Using c As GatewayClient = h.GatewayLogin("root1")
                Assert.True(c.IsSignedIn)
                Assert.Equal(Role.Supervisor, c.ToUserSession().Role)         ' المسؤول مقيَّد بسقف الدور البعيد
                Assert.Equal("root1", c.Me_.UserName)
            End Using
            Using c2 As GatewayClient = h.CreateGatewayClient()
                Dim ex As Ptz.Core.Security.AuthenticationException = Capture(Of Ptz.Core.Security.AuthenticationException)(
                    Function() c2.LoginAsync(New GatewayCredentials With {.UserName = "alice", .Password = "wrong-password-1!", .Totp = "123456"}, _ct))
                Assert.Contains("بيانات الدخول", ex.Message)
                Assert.False(c2.IsSignedIn)
                Capture(Of AccessDeniedException)(Function() c2.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, _ct))
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Logout_ClearsTheTokenAndRevokesServerSide()
        Using h As New Harness()
            Dim c As GatewayClient = h.GatewayLogin("alice")
            c.LogoutAsync().GetAwaiter().GetResult()
            Assert.False(c.IsSignedIn)
            c.Dispose()
            Assert.Contains("Auth.Logout", String.Join(vbLf, h.AuditLines()))
        End Using
    End Sub

    <Fact>
    Public Sub EndpointValidation_RejectsRiskyAddresses()
        Dim ok As New GatewayEndpoint With {.BaseUrl = "https://gw.example.test:8443"}
        Assert.Equal("https://gw.example.test:8443/", ok.Validate().ToString())
        For Each bad As String In {"", "ftp://x/", "gw.example.test", "http://gw.example.test/", "https://user:pw@gw/", "https://gw/api", "https://gw/?token=1", "https://gw/#x"}
            Dim ep As New GatewayEndpoint With {.BaseUrl = bad}
            Assert.Throws(Of ArgumentException)(Function() ep.Validate())
        Next
        Assert.Throws(Of ArgumentException)(Function() New GatewayEndpoint With {.BaseUrl = "http://localhost/"}.Validate())          ' HTTP يتطلب موافقة صريحة
        Assert.Throws(Of ArgumentException)(Function() New GatewayEndpoint With {.BaseUrl = "http://10.0.0.5/", .AllowInsecureLoopbackHttp = True}.Validate())
        Assert.NotNull(New GatewayEndpoint With {.BaseUrl = "http://127.0.0.1:8080", .AllowInsecureLoopbackHttp = True}.Validate())
        Assert.Throws(Of ArgumentException)(Function() New GatewayEndpoint With {.BaseUrl = "https://gw/", .PinnedCert = "sha1:abcd"}.Validate())
        Assert.Throws(Of ArgumentException)(Function() New GatewayEndpoint With {.BaseUrl = "https://gw/", .TimeoutSeconds = 1}.Validate())
    End Sub

    ' ======================= المتحكم =======================
    <Fact>
    Public Sub Controller_ConnectsAndMoves_RenewsWhileHeld_AndStopsRenewingAfterStop()
        Using h As New Harness()
            WaitOnline(h)
            Dim log As New List(Of TappedRequest)()
            Using c As GatewayClient = h.GatewayLogin("alice", tap:=log)
                Using ctl As New RemoteCameraController(c, Se, CameraProfiles.Ds2Se7c432mwAeb, renewMs:=80)
                    Dim dev As DeviceInfo = ctl.ConnectAsync(_ct).GetAwaiter().GetResult()
                    Assert.Equal("X", dev.Model)
                    Assert.NotEqual(0L, ctl.Features And CameraFeatures.Ptz)
                    ctl.MoveAsync(New PtzVector(40, -20, 0), _ct).GetAwaiter().GetResult()
                    WaitFor(Function() Count(log, "POST", "/ptz/move") >= 4, 5)
                    Assert.True(Count(log, "POST", "/ptz/move") >= 4, "لا تجديد دوري أثناء الإمساك")
                    SyncLock log
                        Dim moves = log.Where(Function(r) r.Path.EndsWith("/ptz/move", StringComparison.Ordinal)).ToList()
                        Assert.DoesNotContain("renew", moves(0).Body)
                        Assert.All(moves.Skip(1), Sub(r) Assert.Contains("""renew"":true", r.Body))
                        Dim seqs As List(Of Long) = moves.Select(Function(r) System.Text.Json.JsonDocument.Parse(r.Body).RootElement.GetProperty("seq").GetInt64()).ToList()
                        For i As Integer = 1 To seqs.Count - 1
                            Assert.True(seqs(i) > seqs(i - 1), "الأرقام التسلسلية يجب أن تتزايد")
                        Next
                    End SyncLock
                    Assert.Contains("<pan>40</pan>", SyncGet(h.SeCamera, "/ISAPI/PTZCtrl/channels/1/momentary"))
                    ctl.StopAsync(_ct).GetAwaiter().GetResult()
                    Dim moveCount As Integer = Count(log, "POST", "/ptz/move")
                    Threading.Thread.Sleep(400)
                    Assert.Equal(moveCount, Count(log, "POST", "/ptz/move"))
                    Assert.Equal(1, Count(log, "POST", "/ptz/stop"))
                End Using
            End Using
        End Using
    End Sub

    Private Shared Function SyncGet(cam As FakeCameraHandler, path As String) As String
        SyncLock cam.Requests
            Dim r = cam.Requests.LastOrDefault(Function(x) x.Path = path)
            Return If(r Is Nothing, "", r.Body)
        End SyncLock
    End Function

    <Fact>
    Public Sub Controller_DisposeWhileMoving_SendsAProtectiveStop()
        Using h As New Harness()
            WaitOnline(h)
            Dim log As New List(Of TappedRequest)()
            Using c As GatewayClient = h.GatewayLogin("alice", tap:=log)
                Dim ctl As New RemoteCameraController(c, Se, CameraProfiles.Ds2Se7c432mwAeb, renewMs:=80)
                ctl.MoveAsync(New PtzVector(30, 0, 0), _ct).GetAwaiter().GetResult()
                ctl.Dispose()
                WaitFor(Function() Count(log, "POST", "/ptz/stop") >= 1, 3)
                Assert.True(Count(log, "POST", "/ptz/stop") >= 1)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Controller_ServerErrors_AreTranslatedToTheSameExceptionsAsTheLocalMode()
        Using h As New Harness()
            WaitOnline(h)
            Using c As GatewayClient = h.GatewayLogin("view")
                Using ctl As New RemoteCameraController(c, Se, CameraProfiles.Ds2Se7c432mwAeb)
                    Dim denied As AccessDeniedException = Capture(Of AccessDeniedException)(Function() ctl.MoveAsync(New PtzVector(10, 0, 0), _ct))
                    Assert.Equal(Permission.PtzControl, denied.Required)
                    Assert.False(denied.IsSessionExpired)
                End Using
                Using ghost As New RemoteCameraController(c, "nope", CameraProfiles.Ds2Se7c432mwAeb)
                    Capture(Of InvalidOperationException)(Function() ghost.GetPositionAsync(_ct))
                End Using
                Assert.Throws(Of ArgumentException)(Function() New RemoteCameraController(c, "../x", CameraProfiles.Ds2Se7c432mwAeb))
            End Using
            Using c As GatewayClient = h.GatewayLogin("sup")
                Using ctl As New RemoteCameraController(c, Se, CameraProfiles.Ds2Se7c432mwAeb)
                    Capture(Of ArgumentException)(Function() ctl.SavePresetAsync(9999, "ok", _ct))             ' خارج النطاق
                    Capture(Of NotSupportedException)(Function() ctl.RebootAsync(_ct))                         ' المسار غير مسجَّل ⇒ 404 → لا دعم
                End Using
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Controller_SessionEnd_RaisesEventOnce_AndLaterCallsAreSessionExpired()
        Using h As New Harness()
            WaitOnline(h)
            Using c As GatewayClient = h.GatewayLogin("alice")
                Dim ended As New List(Of String)()
                AddHandler c.SessionEnded, Sub(reason) ended.Add(reason)
                h.Svc.Users.SetDisabled("setup", "alice", True)
                Dim ex As AccessDeniedException = Capture(Of AccessDeniedException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, _ct))
                Assert.True(ex.IsSessionExpired)
                Capture(Of AccessDeniedException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, _ct))
                Assert.Single(ended)
                Assert.False(c.IsSignedIn)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Controller_PresetsImageAndAux_RoundTrip()
        Using h As New Harness()
            WaitOnline(h)
            Using c As GatewayClient = h.GatewayLogin("sup")
                Using ctl As New RemoteCameraController(c, Se, CameraProfiles.Ds2Se7c432mwAeb)
                    Dim presets = ctl.GetPresetsAsync(_ct).GetAwaiter().GetResult()
                    Assert.Equal("Gate", presets.Single().Name)
                    Dim img As ImageSettings = ctl.GetImageSettingsAsync(_ct).GetAwaiter().GetResult()
                    Assert.Equal("40", img(ImageParam.Brightness))
                    Dim s As New ImageSettings()
                    s(ImageParam.Brightness) = "75"
                    ctl.ApplyImageSettingsAsync(s, _ct).GetAwaiter().GetResult()
                    Assert.Contains("<brightnessLevel>75</brightnessLevel>", SyncGet(h.SeCamera, "/ISAPI/Image/channels/1/color"))
                    ctl.SetAuxAsync(AuxKind.Wiper, True, _ct).GetAwaiter().GetResult()
                    Dim p As PtzPosition = ctl.GetPositionAsync(_ct).GetAwaiter().GetResult()
                    Assert.Equal(180.0, p.Azimuth)
                    Dim jpeg As Byte() = ctl.GetSnapshotAsync(_ct).GetAwaiter().GetResult()
                    Assert.Equal(CByte(&HFF), jpeg(0))
                End Using
            End Using
        End Using
    End Sub

    ' ======================= المحرك الذكي البعيد =======================
    Private Shared Function MakeEngine(c As GatewayClient, ByRef stream As GatewayEventStream) As RemoteSmartEngine
        stream = New GatewayEventStream(c, Se)
        Return New RemoteSmartEngine(c, Se, stream)
    End Function

    <Fact>
    Public Sub Engine_CommandsAreOptimistic_AppliedOnTheServer_AndRevertWhenDenied()
        Using h As New Harness()
            WaitOnline(h)
            Dim failures As New List(Of Exception)()
            Using sup As GatewayClient = h.GatewayLogin("sup"), view As GatewayClient = h.GatewayLogin("view")
                Dim s1 As GatewayEventStream = Nothing
                Dim eng As RemoteSmartEngine = MakeEngine(sup, s1)
                AddHandler eng.CommandFailed, Sub(ex) failures.Add(ex)
                eng.AutoTrackEnabled = True
                Assert.True(eng.AutoTrackEnabled)                                            ' تفاؤلي فوراً
                WaitFor(Function() h.Svc.Cameras.Find(Se).Engine.AutoTrackEnabled)
                Assert.True(h.Svc.Cameras.Find(Se).Engine.AutoTrackEnabled)
                eng.ApplyBehaviorOptions(New BehaviorOptions With {.LoiterSeconds = 33, .LoiterRadius = 0.05, .RunSpeed = 1, .RunMinSeconds = 1, .StationarySeconds = 30, .StationarySpeed = 0.01,
                                         .ErraticReversals = 5, .ErraticWindowSeconds = 6, .ErraticMinSegment = 0.02, .GatherCount = 3, .GatherRadius = 0.15, .EventCooldownSeconds = 10, .MinTrackAgeSeconds = 1})
                WaitFor(Function() h.Svc.Cameras.Find(Se).Engine.SnapshotBehaviorOptions().LoiterSeconds = 33)
                Assert.Equal(33.0, h.Svc.Cameras.Find(Se).Engine.SnapshotBehaviorOptions().LoiterSeconds)
                eng.StopAsync().GetAwaiter().GetResult()
                Assert.Empty(failures)

                Dim s2 As GatewayEventStream = Nothing
                Dim viewer As RemoteSmartEngine = MakeEngine(view, s2)
                AddHandler viewer.CommandFailed, Sub(ex) failures.Add(ex)
                viewer.MotionEnabled = False
                WaitFor(Function() failures.Count > 0)
                Assert.IsType(Of AccessDeniedException)(failures.Single())
                Assert.True(viewer.MotionEnabled, "يجب أن ينقلب التغيير المرفوض")
                Assert.True(h.Svc.Cameras.Find(Se).Engine.MotionEnabled)
                viewer.StopAsync().GetAwaiter().GetResult()
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Engine_WeakeningNeedsStepUp_ThePromptIsUsed_AndCancellingRevertsTheChange()
        Using h As New Harness()
            WaitOnline(h)
            Dim prompt As New Prompt()
            Using c As GatewayClient = h.GatewayLogin("sup", prompt)
                h.Clock.Advance(6 * 60)                           ' انتهت نافذة التحقق المعزَّز
                Dim failures As New List(Of Exception)()
                Dim st As GatewayEventStream = Nothing
                Dim eng As RemoteSmartEngine = MakeEngine(c, st)
                AddHandler eng.CommandFailed, Sub(ex) failures.Add(ex)
                prompt.Code = Function() Nothing                  ' المستخدم يلغي
                eng.MotionEnabled = False
                WaitFor(Function() failures.Count > 0)
                Assert.Equal(1, prompt.Calls)
                Assert.IsType(Of AccessDeniedException)(failures.Single())
                Assert.True(eng.MotionEnabled)
                Assert.True(h.Svc.Cameras.Find(Se).Engine.MotionEnabled)
                ' الآن يوافق
                failures.Clear()
                prompt.Code = Function() h.NextCode("sup")
                eng.MotionEnabled = False
                WaitFor(Function() Not h.Svc.Cameras.Find(Se).Engine.MotionEnabled)
                Assert.False(h.Svc.Cameras.Find(Se).Engine.MotionEnabled)
                Assert.Equal(2, prompt.Calls)
                Assert.Empty(failures)
                eng.StopAsync().GetAwaiter().GetResult()
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Stream_DeliversOverlayAlarmsAndStateToTheEngine_AndEndsTheSessionOnRevocation()
        Using h As New Harness(Sub(o)
                                   o.Streams.HeartbeatSeconds = 1
                                   o.Streams.GuardSeconds = 1
                               End Sub)
            WaitOnline(h)
            Using c As GatewayClient = h.GatewayLogin("alice")
                Dim results As New List(Of SmartFrameResult)()
                Dim alarms As New List(Of AlarmDto)()
                Dim states As New List(Of StateWire)()
                Dim ended As New List(Of String)()
                AddHandler c.SessionEnded, Sub(r) ended.Add(r)
                Dim stream As GatewayEventStream = Nothing
                Dim eng As RemoteSmartEngine = MakeEngine(c, stream)
                AddHandler eng.FrameProcessed, Sub(r) results.Add(r)
                AddHandler stream.AlarmReceived, Sub(a) alarms.Add(a)
                AddHandler stream.StateChanged, Sub(s) states.Add(s)
                Dim hello As Boolean = False
                AddHandler stream.Connected, Sub(hw) hello = True
                eng.Start()
                WaitFor(Function() hello)
                Assert.True(hello)

                Dim cam As Ptz.Gateway.Cameras.CameraHost = h.Svc.Cameras.Find(Se)
                Dim ov As New OverlayWire With {.Seq = 1, .T = 1700000000000L, .Fl = WireMap.FlagAutoTrack}
                ov.Tr.Add(New TrackWire With {.I = 7, .B = {100, 200, 300, 400}, .S = 1500, .K = 1, .P = {100, 200, 110, 210}})
                cam.Events.PublishOverlay(New Ptz.Gateway.Cameras.SseMessage("ov", Nothing, WireJson.Serialize(ov)))
                WaitFor(Function() results.Count > 0)
                Assert.Equal(7, results(0).Tracks.Single().Id)
                Assert.Equal(0.1, results(0).Tracks.Single().Box.X, 3)
                Assert.True(results(0).AutoTrackActive)

                cam.Bus.Publish(New AlarmEvent With {.Camera = Se, .Kind = "Intrusion", .Severity = AlarmSeverity.Critical, .Message = "gate", .TrackId = 3})
                WaitFor(Function() alarms.Count > 0)
                Assert.Equal("gate", alarms.Single().Message)
                Assert.NotNull(stream.LastAlarmSeq)

                ' تغيير حالة من مشغّل آخر يصل كحدث state
                Assert.Equal(System.Net.HttpStatusCode.OK, h.Send("PUT", "/api/v1/cameras/" & Se & "/smart/state", New EngineStateDto With {.AutoTrackEnabled = True}, h.Token("sup")).StatusCode)
                WaitFor(Function() states.Count > 0)
                Assert.Equal("sup", states(0).By)
                WaitFor(Function() eng.AutoTrackEnabled)
                Assert.True(eng.AutoTrackEnabled, "حالة المحرك الوكيل يجب أن تتبع بث الخادم")

                ' إبطال الجلسة من الخادم ينهيها عند العميل خلال ثوانٍ
                h.Svc.Users.SetDisabled("setup", "alice", True)
                WaitFor(Function() ended.Count > 0, 8)
                Assert.Equal("revoked", ended.Single())
                Assert.False(c.IsSignedIn)
                eng.StopAsync().GetAwaiter().GetResult()
            End Using
        End Using
    End Sub

    ' ======================= اللقطات والإعداد =======================
    <Fact>
    Public Sub JpegFeed_DeliversValidFrames_AndAdaptsUpOnAFastLink()
        Using h As New Harness(engines:=True)
            WaitOnline(h)
            Using c As GatewayClient = h.GatewayLogin("view")
                Dim selector As New TierSelector(FrameLadder.Entries.Select(Function(e) e.Id), "tiny", upgradeAfterGoodFrames:=2)
                Dim feed As New RemoteJpegFeed(c, Se, selector, New LinkEstimator(minSampleBytes:=200))
                Dim frames As New List(Of Byte())()
                AddHandler feed.FrameCaptured, Sub(b) frames.Add(b)
                feed.Start(_ct)
                WaitFor(Function() frames.Count >= 8 AndAlso selector.Current <> "tiny", 20)
                feed.StopAsync().GetAwaiter().GetResult()
                Assert.True(frames.Count >= 8, "لم تصل إطارات كافية")
                Assert.All(frames, Sub(b) Assert.True(b.Length > 4 AndAlso b(0) = &HFF AndAlso b(1) = &HD8))
                Assert.NotEqual("tiny", selector.Current)
                Assert.True(feed.Link.Samples > 0)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub JpegFeed_RespectsTheServerTierCap()
        Using h As New Harness(Sub(o) o.Frames.MaxTier = "low", engines:=True)
            WaitOnline(h)
            Using c As GatewayClient = h.GatewayLogin("view")
                Dim selector As New TierSelector(FrameLadder.Entries.Select(Function(e) e.Id), "low", upgradeAfterGoodFrames:=1)
                Dim feed As New RemoteJpegFeed(c, Se, selector, New LinkEstimator(minSampleBytes:=200))
                Dim n As Integer = 0
                AddHandler feed.FrameCaptured, Sub(b) Threading.Interlocked.Increment(n)
                feed.Start(_ct)
                WaitFor(Function() n >= 10, 20)
                feed.StopAsync().GetAwaiter().GetResult()
                Assert.True(n >= 10)
                Assert.Equal("low", selector.Current)           ' لم يتجاوز سقف الخادم رغم سرعة الوصلة
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub ConfigRepository_UsesETag_ReportsConflicts_AndReloadsTheNewestCopy()
        Using h As New Harness()
            WaitOnline(h)
            Using a As GatewayClient = h.GatewayLogin("sup"), b As GatewayClient = h.GatewayLogin("sup")
                Dim ra As New RemoteSmartConfigRepository(a, Se)
                Dim rb As New RemoteSmartConfigRepository(b, Se)
                Assert.Empty(ra.LoadAsync(_ct).GetAwaiter().GetResult().Zones)
                Assert.Empty(rb.LoadAsync(_ct).GetAwaiter().GetResult().Zones)
                Dim zone As New RestrictedZone("Gate", New NormPoint() {New NormPoint(0.1, 0.1), New NormPoint(0.5, 0.1), New NormPoint(0.5, 0.5)}, AlarmSeverity.Critical)
                rb.SaveGeometryAsync(New SmartGeometry({zone}, New TripLine() {}, New IReadOnlyList(Of NormPoint)() {}), _ct).GetAwaiter().GetResult()
                h.Clock.Advance(5)
                ' أ لم يعلم بتعديل ب ⇒ تعارض صريح ثم يحمّل الأحدث
                Dim conflict As InvalidOperationException = Capture(Of InvalidOperationException)(Function() ra.SaveGeometryAsync(SmartGeometry.Empty, _ct))
                Assert.Contains("مشغّل آخر", conflict.Message)
                Assert.Single(h.Svc.Cameras.Find(Se).Geometry.Zones)                 ' لم يُطمس عمل ب
                h.Clock.Advance(5)
                ra.SaveGeometryAsync(SmartGeometry.Empty, _ct).GetAwaiter().GetResult()     ' بعد إعادة التحميل ينجح
                Assert.Empty(h.Svc.Cameras.Find(Se).Geometry.Zones)
                ra.SaveAnomalyModelAsync(_ct).GetAwaiter().GetResult()
            End Using
        End Using
    End Sub

    ' ======================= المقدِّم الكامل فوق البوابة =======================
    Private NotInheritable Class Rig
        Implements IDisposable
        Public ReadOnly View As New FakeFormView()
        Public ReadOnly Audit As New ListAudit()
        Public ReadOnly Bus As New AlarmBus(cooldownSeconds:=0)
        Public ReadOnly Dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-gwui-" & Guid.NewGuid().ToString("N"))
        Public ReadOnly Presenter As CameraSessionPresenter

        Public Sub New(client As GatewayClient, clock As IClock)
            Dim deps As New CameraSessionDeps With {
                .Clock = clock,
                .Session = client.ToUserSession(), .Access = New AccessControl(clock), .Audit = Audit, .Bus = Bus, .Decoder = New FakeDecoder(),
                .Dispatcher = New ImmediateDispatcher(), .DataDirectory = Dir, .CameraName = "Gate", .BackendFactory = New GatewayBackendFactory(client)}
            Presenter = New Ds2Se7c432mwPresenter(View, deps)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Presenter.Dispose()
        End Sub
    End Class

    <Fact>
    Public Sub FullPresenter_OverTheGateway_ConnectsStreamsMovesAcksAndShowsAlarms()
        Using h As New Harness(engines:=True)
            WaitOnline(h)
            Using c As GatewayClient = h.GatewayLogin("alice")
                Using rig As New Rig(c, h.Clock)
                    rig.Presenter.StartAsync().GetAwaiter().GetResult()
                    Assert.True(rig.View.Connected, String.Join(" | ", rig.View.Errors))
                    WaitFor(Function() rig.View.PtzView.Presets IsNot Nothing AndAlso rig.View.PtzView.Presets.Count > 0, 5)
                    Assert.Equal("Gate", rig.View.PtzView.Presets.Single().Name)
                    WaitFor(Function() rig.View.VideoView.Frames >= 3, 15)
                    Assert.True(rig.View.VideoView.Frames >= 3, "الفيديو لا يصل للواجهة")

                    ' PTZ
                    rig.View.PtzView.RaiseMove(New PtzVector(50, 0, 0))
                    WaitFor(Function() CamCount(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary") >= 1, 5)
                    Assert.True(CamCount(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary") >= 1)
                    rig.View.PtzView.RaiseStop()
                    WaitFor(Function() Not h.Svc.Cameras.Find(Se).Lease.IsActive, 5)
                    Assert.False(h.Svc.Cameras.Find(Se).Lease.IsActive)

                    ' التتبع الآلي من لوحة الذكاء
                    rig.View.SmartView.RaiseAutoTrack(True)
                    WaitFor(Function() h.Svc.Cameras.Find(Se).Engine.AutoTrackEnabled, 5)
                    Assert.True(h.Svc.Cameras.Find(Se).Engine.AutoTrackEnabled)

                    ' إنذار من البوابة يظهر في اللوحة، والإقرار منها يصل للبوابة
                    h.Svc.Cameras.Find(Se).Bus.Publish(New AlarmEvent With {.Camera = Se, .Kind = "Intrusion", .Severity = AlarmSeverity.Critical, .Message = "حركة عند البوابة", .TrackId = 9})
                    ' (تغيير حالة الذكاء أعلاه ولّد أيضاً تحذيراً تشغيلياً من النوع System، وهو سلوك مقصود)
                    WaitFor(Function() Snapshot(rig.View.AlarmView).Any(Function(a) a.Kind = "Intrusion"), 5)
                    Dim alarm As AlarmEvent = Snapshot(rig.View.AlarmView).First(Function(a) a.Kind = "Intrusion")
                    Assert.Equal("حركة عند البوابة", alarm.Message)
                    rig.View.AlarmView.RaiseAck(alarm.Id)
                    Dim watcher As String = h.Token("view")
                    WaitFor(Function() Not Harness.Read(Of AlarmListDto)(h.Send("GET", "/api/v1/alarms?unacked=true&camera=" & Se, token:=watcher)).Items.Any(Function(a) a.Id = alarm.Id), 8)
                    Assert.False(Harness.Read(Of AlarmListDto)(h.Send("GET", "/api/v1/alarms?unacked=true&camera=" & Se, token:=watcher)).Items.Any(Function(a) a.Id = alarm.Id), "الإقرار لم يصل للبوابة")
                    rig.Presenter.StopAsync().GetAwaiter().GetResult()
                End Using
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub FullPresenter_Viewer_IsBlockedLocally_BeforeAnyNetworkCall()
        Using h As New Harness()
            WaitOnline(h)
            Dim log As New List(Of TappedRequest)()
            Using c As GatewayClient = h.GatewayLogin("view", tap:=log)
                Using rig As New Rig(c, h.Clock)
                    rig.Presenter.StartAsync().GetAwaiter().GetResult()
                    Assert.True(rig.View.Connected)
                    rig.View.PtzView.RaiseMove(New PtzVector(50, 0, 0))
                    WaitFor(Function() rig.View.Errors.Count > 0)
                    Assert.Contains(UiText.NoPermission, rig.View.Errors)
                    Assert.Equal(0, Count(log, "POST", "/ptz/move"))
                    Assert.Equal(0, CamCount(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
                    rig.Presenter.StopAsync().GetAwaiter().GetResult()
                End Using
            End Using
        End Using
    End Sub
End Class
