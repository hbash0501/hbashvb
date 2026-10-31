Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Remote
Imports Ptz.Gateway.Cameras
Imports Xunit

''' <summary>
''' تكامل كامل لكاميرا بنقل Hik-Partner Pro: واجهة REST الحقيقية ← البوابة ← معالج الترحيل ← خادم سحابي وهمي مطابق للدليل،
''' بينما الكاميرا الأخرى تبقى على النقل المباشر. تثبت أن الصلاحيات والتدقيق وعقد الحركة كلها تعمل فوق النقل الجديد.
''' </summary>
Public Class HppRelayApiTests

    Private Const Se As String = "/api/v1/cameras/se7c432mw"
    Private Const De As String = "/api/v1/cameras/de7232iw"

    Private Shared Function NewHarness(Optional engines As Boolean = False) As Harness
        Dim fake As New FakeHppCloud(New TestClock())
        Return New Harness(Nothing, engines, 0, fake)
    End Function

    Private Shared Sub WaitSeOnline(h As Harness)
        GwWait.WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Online AndAlso h.Svc.Cameras.Find("de7232iw").Online)
        Assert.True(h.Svc.Cameras.Find("se7c432mw").Online, "كاميرا الترحيل يجب أن تتصل عبر السحابة")
    End Sub

    Private Shared Function LastMove(h As Harness) As String
        Dim log As String() = h.HppFake.MoveLog(Harness.HppSerial)
        Return If(log.Length = 0, "", log(log.Length - 1))
    End Function

    <Fact>
    Public Sub RelayCamera_ConnectsThroughCloud_AndAdvertisesOnlyDocumentedFeatures_AndHidesCloudDetails()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim t As String = h.Token("view")
            Dim resp As HttpResponseMessage = h.Send("GET", "/api/v1/cameras", token:=t)
            Dim body As String = Harness.Body(resp)
            Dim list As CameraListDto = Harness.Read(Of CameraListDto)(resp)
            Dim seCam As CameraDto = list.Items.Single(Function(c) c.Id = "se7c432mw")
            Dim deCam As CameraDto = list.Items.Single(Function(c) c.Id = "de7232iw")
            Assert.Equal({"presets", "ptz"}, seCam.Features.OrderBy(Function(x) x).ToArray())
            Assert.Contains("snapshot", deCam.Features)
            Assert.True(seCam.Online)
            ' لا تسرُّب: لا رقم تسلسلي سحابي ولا رمز ولا مفاتيح ولا نطاق السحابة.
            For Each secret As String In {Harness.HppSerial, "hpc.tok", FakeHppCloud.AppKey, FakeHppCloud.SecretKey, "hik-partner"}
                Assert.DoesNotContain(secret, body)
            Next
            Dim conn As ConnectResultDto = Harness.Read(Of ConnectResultDto)(h.Send("POST", Se & "/connect", token:=h.Token("alice")))
            Assert.Equal("DS-2SE7C432MW-AEB", conn.Device.Model)
            Assert.Equal("", conn.Device.SerialNumber)
        End Using
    End Sub

    <Fact>
    Public Sub Move_GoesThroughCloudAsContinuous_WithCombinedAxes_AndIsAuditedUnderTheRealUser()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 40, .Tilt = -20, .Zoom = 10, .Seq = 1}, h.Token("alice"), ip:="203.0.113.5")
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode)
            GwWait.WaitFor(Function() LastMove(h) = "move:40,-20,10")
            Assert.Equal("move:40,-20,10", LastMove(h))
            ' المسار غير الموثَّق (momentary) لا يغادر البوابة أبداً.
            Assert.DoesNotContain(h.HppFake.Transparent(), Function(x) x.Path.EndsWith("/momentary", StringComparison.Ordinal))
            Dim audit As String = String.Join(vbLf, h.AuditLines())
            Assert.Contains("alice", audit)
            Assert.Contains("Ptz.Move", audit)
            Assert.Contains("ip=203.0.113.5", audit)
            ' الكاميرا الأخرى لم تُمَسّ.
            Assert.Empty(h.DeCamera.Requests.Where(Function(q) q.Method = "PUT"))
        End Using
    End Sub

    <Fact>
    Public Sub Move_ByViewer_IsForbidden_AndNothingReachesTheCloud()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim before As Integer = h.HppFake.TransparentCalls
            Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10}, h.Token("view"))
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode)
            Assert.Equal(before, h.HppFake.TransparentCalls)
        End Using
    End Sub

    <Fact>
    Public Sub Stop_SendsZeroVector_ThroughCloud()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim t As String = h.Token("alice")
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .Seq = 1}, t)
            GwWait.WaitFor(Function() LastMove(h) = "move:30,0,0")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/stop", token:=t).StatusCode)
            GwWait.WaitFor(Function() LastMove(h) = "stop")
            Assert.Equal("stop", LastMove(h))
        End Using
    End Sub

    <Fact>
    Public Sub Lease_StopsTheCamera_ThroughCloud_WhenTheClientStopsRenewing()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .TtlMs = 300}, h.Token("alice"))
            GwWait.WaitFor(Function() LastMove(h) = "move:30,0,0")
            h.Clock.Advance(5)
            GwWait.WaitFor(Function() LastMove(h) = "stop")
            Assert.Equal("stop", LastMove(h))
            GwWait.WaitFor(Function() String.Join(vbLf, h.AuditLines()).Contains("svc:lease"))   ' التدقيق يُكتب بعد عودة النداء
            Assert.Contains("svc:lease", String.Join(vbLf, h.AuditLines()))
        End Using
    End Sub

    <Fact>
    Public Sub LostStop_IsRetriedWithBackoff_UntilTheCloudAcknowledges()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .TtlMs = 300}, h.Token("alice"))
            GwWait.WaitFor(Function() LastMove(h) = "move:30,0,0")
            h.HppFake.Devices(Harness.HppSerial).StopFailuresLeft = 3
            h.Clock.Advance(5)
            GwWait.WaitFor(Function() LastMove(h) = "stop", 15)
            Assert.Equal("stop", LastMove(h))
            Assert.Equal(0, h.HppFake.Devices(Harness.HppSerial).StopFailuresLeft)
        End Using
    End Sub

    <Fact>
    Public Sub Stop_ThatNeverSucceeds_RaisesACriticalAlarmAndAuditEntry_WithinTheDeadline()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .TtlMs = 300}, h.Token("alice"))
            GwWait.WaitFor(Function() LastMove(h) = "move:30,0,0")
            h.HppFake.Devices(Harness.HppSerial).StopFailuresLeft = 1000
            Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            h.Clock.Advance(5)
            GwWait.WaitFor(Function() String.Join(vbLf, h.AuditLines()).Contains("Ptz.StopFailed"), 40)
            ' المهلة الإجمالية (15 ث) أقل من مهلة إيقاف الخدمة (20 ث) كي يصدر الإنذار قبل القتل.
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(19), sw.Elapsed.ToString())
            Assert.Contains("Ptz.StopFailed", String.Join(vbLf, h.AuditLines()))
            Dim resync As Boolean = False
            GwWait.WaitFor(Function() h.Svc.Alarms.Query(0, False, 100, Nothing, False, resync).Any(Function(a) a.Message.Contains("تعذّر إيقاف")))
            Assert.Contains(h.Svc.Alarms.Query(0, False, 100, Nothing, False, resync), Function(a) a.Message.Contains("تعذّر إيقاف"))
        End Using
    End Sub

    <Fact>
    Public Sub Presets_List_Save_Goto_Delete_ThroughCloud()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("PUT", Se & "/presets/2", New PresetPutRequest With {.Name = "Main Door (1)"}, sup).StatusCode)
            Assert.Equal("save:2:enabled", LastMove(h))      ' الدليل يطلب enabled في XML_Set_PTZPreset
            Dim list As PresetListDto = Harness.Read(Of PresetListDto)(h.Send("GET", Se & "/presets", token:=sup))
            Assert.Equal("Main Door (1)", list.Items.Single().Name)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/presets/2/goto", token:=sup).StatusCode)
            Assert.Equal("goto:2", LastMove(h))
            Assert.Equal(HttpStatusCode.NoContent, h.Send("DELETE", Se & "/presets/2", token:=sup).StatusCode)
            Assert.Equal("delete:2", LastMove(h))
            Assert.Empty(Harness.Read(Of PresetListDto)(h.Send("GET", Se & "/presets", token:=sup)).Items)
            ' المشغّل يستعمل ولا يدير (الصلاحيات كما هي).
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("PUT", Se & "/presets/3", New PresetPutRequest With {.Name = "x"}, h.Token("alice")).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub UnsupportedOperations_AreRefusedCleanly_WithoutAnyCloudCall()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim sup As String = h.Token("sup")
            Dim before As Integer = h.HppFake.TransparentCalls
            Dim attempts As New List(Of (Method As String, Path As String, Body As Object))
            attempts.Add(("POST", Se & "/lens/focus", New LensRequest With {.Direction = "far", .Speed = 50}))
            attempts.Add(("POST", Se & "/lens/iris", New LensRequest With {.Direction = "far", .Speed = 50}))
            attempts.Add(("PUT", Se & "/aux/wiper", New AuxRequest With {.Enabled = True}))
            attempts.Add(("GET", Se & "/ptz/position", Nothing))
            attempts.Add(("PUT", Se & "/ptz/position", New PositionDto With {.Azimuth = 90, .Elevation = 10, .Zoom = 4}))
            attempts.Add(("POST", Se & "/ptz/zoom-area", New ZoomAreaRequest With {.X = 0.4, .Y = 0.4, .Width = 0.2, .Height = 0.2}))
            attempts.Add(("POST", Se & "/ptz/home", Nothing))
            attempts.Add(("POST", Se & "/patrols/1/start", Nothing))
            attempts.Add(("GET", Se & "/snapshot?fresh=1", Nothing))
            For Each a In attempts
                GwWait.Refill(h)
                Dim r As HttpResponseMessage = h.Send(a.Method, a.Path, a.Body, sup)
                Assert.True(r.StatusCode = HttpStatusCode.NotImplemented OrElse r.StatusCode = HttpStatusCode.ServiceUnavailable, $"{a.Method} {a.Path} → {CInt(r.StatusCode)}")
            Next
            GwWait.Refill(h)
            ' قراءة الصورة تعيد قائمة فارغة لا خطأ (لا ميزات صورة).
            Dim img As ImageDto = Harness.Read(Of ImageDto)(h.Send("GET", Se & "/image", token:=sup))
            Assert.Empty(img.Supported)
            Assert.Equal(before, h.HppFake.TransparentCalls)
        End Using
    End Sub

    <Fact>
    Public Sub EngineIsNotStarted_ForRelayCamera_SoItNeverGoesOfflineFromMissingSnapshots()
        Using h As Harness = NewHarness(engines:=True)
            WaitSeOnline(h)
            Threading.Thread.Sleep(2500)    ' لو بدأ المحرك لفشلت اللقطات مرتين وانقلبت الحالة إلى غير متصلة
            Dim cam As CameraHost = h.Svc.Cameras.Find("se7c432mw")
            Assert.True(cam.Online)
            Assert.Equal(0, cam.ConsecutiveFailures)
            Assert.True(cam.IsRelay)
            Assert.False(h.Svc.Cameras.Find("de7232iw").IsRelay)
            ' المحرك يعمل على الكاميرا المباشرة فعلاً (يجلب لقطات).
            Assert.NotEmpty(h.DeCamera.Requests.Where(Function(q) q.Path = "/ISAPI/Streaming/channels/101/picture"))
        End Using
    End Sub

    <Fact>
    Public Sub OfflineDevice_Gives503_ThenCameraGoesOffline_AndRecoversWhenItReturns()
        Dim old As Double = CameraHost.RelayRecoveryIntervalSeconds
        CameraHost.RelayRecoveryIntervalSeconds = 0.1
        Try
            Using h As Harness = NewHarness()
                WaitSeOnline(h)
                Dim t As String = h.Token("alice")
                h.HppFake.Devices(Harness.HppSerial).Online = False
                For i As Integer = 1 To 2
                    GwWait.Refill(h)
                    Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = i}, t)
                    Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode)
                    Assert.Equal("camera_unavailable", Harness.ErrorCode(r))
                Next
                GwWait.WaitFor(Function() Not h.Svc.Cameras.Find("se7c432mw").Online)
                Assert.False(h.Svc.Cameras.Find("se7c432mw").Online)
                Assert.True(h.Svc.Cameras.Find("de7232iw").Online)
                Assert.Equal(HttpStatusCode.ServiceUnavailable, h.Send("POST", Se & "/connect", token:=t).StatusCode)
                h.HppFake.Devices(Harness.HppSerial).Online = True
                GwWait.WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Online, 10)
                Assert.True(h.Svc.Cameras.Find("se7c432mw").Online)
            End Using
        Finally
            CameraHost.RelayRecoveryIntervalSeconds = old
        End Try
    End Sub

    <Fact>
    Public Sub RateLimitedOrRejectedCommands_DoNotMarkTheCameraOffline()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim t As String = h.Token("alice")
            h.HppFake.ForceCode = "EVZ10029"
            For i As Integer = 1 To 3
                GwWait.Refill(h)
                Assert.Equal(HttpStatusCode.ServiceUnavailable, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = i}, t).StatusCode)
            Next
            Assert.True(h.Svc.Cameras.Find("se7c432mw").Online)
            h.HppFake.ForceCode = Nothing
        End Using
    End Sub

    <Fact>
    Public Sub RejectedCloudCredentials_OpenTheBreaker_AndStopHammeringTheCloud()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim t As String = h.Token("alice")
            h.HppFake.ForceCode = "EVZ10013"
            For i As Integer = 1 To 2
                GwWait.Refill(h)
                Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = i}, t)
                Assert.Equal(HttpStatusCode.BadGateway, r.StatusCode)
            Next
            GwWait.WaitFor(Function() Not h.Svc.Cameras.Find("se7c432mw").Online)
            Assert.False(h.Svc.Cameras.Find("se7c432mw").Online)
            Dim calls As Integer = h.HppFake.TransparentCalls
            For i As Integer = 3 To 6
                GwWait.Refill(h)
                Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = i}, t)
                Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode)
            Next
            Assert.Equal(calls, h.HppFake.TransparentCalls)
            h.HppFake.ForceCode = Nothing
        End Using
    End Sub

    <Fact>
    Public Sub NoSecret_EverReachesTheAuditLog_Responses_OrAlarms()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim t As String = h.Token("alice")
            Dim bodies As New System.Text.StringBuilder()
            bodies.Append(Harness.Body(h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = 1}, t)))
            h.HppFake.ForceCode = "EVZ20007"
            GwWait.Refill(h)
            bodies.Append(Harness.Body(h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = 2}, t)))
            h.HppFake.ForceCode = "LAP500004"
            GwWait.Refill(h)
            bodies.Append(Harness.Body(h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = 3}, t)))
            h.HppFake.ForceCode = Nothing
            bodies.Append(Harness.Body(h.Send("GET", "/api/v1/alarms", token:=h.Token("view"))))
            bodies.Append(String.Join(vbLf, h.AuditLines()))
            Dim all As String = bodies.ToString()
            For Each secret As String In {FakeHppCloud.AppKey, FakeHppCloud.SecretKey, "hpc.tok"}
                Assert.DoesNotContain(secret, all)
            Next
            Assert.Contains("Hpp.Error", all)     ' الأخطاء السحابية مُدوَّنة (رموزها فقط)
        End Using
    End Sub

    <Fact>
    Public Sub ExplicitStopThatFails_IsRetriedInTheBackground_SoTheCameraStillStops()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim t As String = h.Token("alice")
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .Seq = 1}, t)
            GwWait.WaitFor(Function() LastMove(h) = "move:30,0,0")
            h.HppFake.Devices(Harness.HppSerial).StopFailuresLeft = 2
            Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/stop", token:=t)
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode)        ' الطلب نفسه فشل...
            GwWait.WaitFor(Function() LastMove(h) = "stop", 10)
            Assert.Equal("stop", LastMove(h))                                    ' ...لكن البوابة أوقفت الكاميرا لاحقاً
            Assert.Empty(h.HppFake.Violations)
        End Using
    End Sub

    <Fact>
    Public Sub StopRetries_GiveWay_WhenSomeoneStartsANewMove()
        Dim old As Double = CameraHost.RelayStopDeadlineSeconds
        CameraHost.RelayStopDeadlineSeconds = 4
        Try
            Using h As Harness = NewHarness()
                WaitSeOnline(h)
                Dim t As String = h.Token("alice")
                h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .TtlMs = 300, .Seq = 1}, t)
                GwWait.WaitFor(Function() LastMove(h) = "move:30,0,0")
                h.HppFake.Devices(Harness.HppSerial).StopFailuresLeft = 1000
                h.Clock.Advance(5)                                   ' العقد انتهى ⇒ محاولات إيقاف (تفشل)
                Threading.Thread.Sleep(600)
                GwWait.Refill(h)
                Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = -20, .TtlMs = 2000, .Seq = 2}, t).StatusCode)
                Threading.Thread.Sleep(5000)                         ' أطول من المهلة الإجمالية
                ' توقفت المحاولات حين صار هناك عقد جديد: لا إنذار «تعذّر الإيقاف» ولا قطع لحركة المشغّل الجديدة.
                Assert.DoesNotContain("Ptz.StopFailed", String.Join(vbLf, h.AuditLines()))
                Assert.Contains("move:-20,0,0", h.HppFake.MoveLog(Harness.HppSerial))
            End Using
        Finally
            CameraHost.RelayStopDeadlineSeconds = old
        End Try
    End Sub

    <Fact>
    Public Sub RequestsSentToTheCloud_FollowTheGuidesShape()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            Dim sup As String = h.Token("sup")
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 5, .Seq = 1}, h.Token("alice"))
            h.Send("POST", Se & "/ptz/stop", token:=h.Token("alice"))
            h.Send("PUT", Se & "/presets/4", New PresetPutRequest With {.Name = "Yard"}, sup)
            h.Send("POST", Se & "/presets/4/goto", token:=sup)
            h.Send("DELETE", Se & "/presets/4", token:=sup)
            h.Send("GET", Se & "/presets", token:=sup)
            Assert.Empty(h.HppFake.Violations)    ' Content-Type، تصريح XML، مساحة isapi.org، enabled/id في المواضع
            Assert.All(h.HppFake.Transparent(), Sub(x) Assert.Equal("application/xml", x.ContentType))
        End Using
    End Sub

    Private Shared Function Alarms(h As Harness, t As String) As List(Of AlarmDto)
        Return Harness.Read(Of AlarmListDto)(h.Send("GET", "/api/v1/alarms?limit=100", token:=t)).Items
    End Function

    <Fact>
    Public Sub CameraOwnAlarms_ArriveThroughTheCloud_AsDeviceAlarmsOnTheRightCamera()
        Using h As Harness = NewHarness()
            WaitSeOnline(h)
            GwWait.WaitFor(Function() h.Svc.Cameras.EventPump IsNot Nothing AndAlso h.Svc.Cameras.EventPump.IsSubscribed)
            Assert.True(h.Svc.Cameras.EventPump.IsSubscribed)
            Dim inner As String = "<DetectionRegionList><DetectionRegionEntry><detectionTarget>human</detectionTarget><TargetRect><X>0.1</X><Y>0.1</Y><width>0.2</width><height>0.2</height></TargetRect></DetectionRegionEntry></DetectionRegionList>"
            Dim id As String = h.HppFake.EnqueueBatch(FakeHppCloud.XmlItem(Harness.HppSerial, "fielddetection", inner), FakeHppCloud.XmlItem("FOREIGN01", "VMD"))
            GwWait.WaitFor(Function() h.HppFake.ConfirmedBatches.Contains(id), 10)
            Dim t As String = h.Token("sup")
            Dim list As List(Of AlarmDto) = Alarms(h, t)
            Dim a As AlarmDto = list.Single(Function(x) x.Kind = "Device:fielddetection")
            Assert.Equal("se7c432mw", a.CameraId)
            Assert.Equal("critical", a.Severity.ToLowerInvariant())
            Assert.Contains("تسلل", a.Message)
            Assert.Contains("شخص", a.Message)
            Assert.Equal(0.2, a.X, 3)
            Assert.DoesNotContain(list, Function(x) x.Kind = "Device:VMD")    ' جهاز آخر في الحساب
            Assert.Empty(h.HppFake.Violations)
        End Using
    End Sub

    <Fact>
    Public Sub DeviceOfflineAndOnlineEvents_DriveCameraHealth_AndOnlineWakesRecoveryAtOnce()
        Dim old As Double = CameraHost.RelayRecoveryIntervalSeconds
        CameraHost.RelayRecoveryIntervalSeconds = 2           ' بعد انقطاع معلَن تصبح الدورة 12 ث
        Try
            Using h As Harness = NewHarness()
                WaitSeOnline(h)
                GwWait.WaitFor(Function() h.Svc.Cameras.EventPump IsNot Nothing AndAlso h.Svc.Cameras.EventPump.IsSubscribed)
                h.HppFake.Devices(Harness.HppSerial).Online = False
                h.HppFake.EnqueueBatch(FakeHppCloud.JsonItem(Harness.HppSerial, "deviceoffline"))
                GwWait.WaitFor(Function() Not h.Svc.Cameras.Find("se7c432mw").Online, 10)
                Assert.False(h.Svc.Cameras.Find("se7c432mw").Online)
                Assert.Contains(Alarms(h, h.Token("sup")), Function(x) x.Kind = "System" AndAlso x.Message.Contains("انقطعت"))
                Threading.Thread.Sleep(3000)                    ' الاستعادة الآن في دورتها البطيئة (12 ث)
                Assert.False(h.Svc.Cameras.Find("se7c432mw").Online)
                h.HppFake.Devices(Harness.HppSerial).Online = True
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                h.HppFake.EnqueueBatch(FakeHppCloud.JsonItem(Harness.HppSerial, "deviceonline"))
                GwWait.WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Online, 15)
                Assert.True(h.Svc.Cameras.Find("se7c432mw").Online)
                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), "حدث العودة يوقظ الاستعادة فوراً: " & sw.Elapsed.ToString())
            End Using
        Finally
            CameraHost.RelayRecoveryIntervalSeconds = old
        End Try
    End Sub

    <Fact>
    Public Sub EventsDisabled_NoSubscriptionIsMade()
        Dim fake As New FakeHppCloud(New TestClock())
        Using h As New Harness(Sub(o) o.Hpp.Events = False, False, 0, fake)
            WaitSeOnline(h)
            Threading.Thread.Sleep(500)
            Assert.Null(h.Svc.Cameras.EventPump)
            Assert.Empty(fake.Subscriptions)
            Assert.Equal(0, fake.MessagesCalls)
        End Using
    End Sub

    <Fact>
    Public Sub RelayCamera_WithoutAttachedCloud_FailsFast_InsteadOfFallingBackToLan()
        Using h As Harness = NewHarness()
            Dim conn As New Ptz.Core.Models.CameraConnection With {.Transport = Ptz.Core.Models.CameraTransport.HikPartnerPro, .CloudSerial = "X1"}
            h.Svc.Hpp = Nothing
            Assert.Throws(Of InvalidOperationException)(Function() h.Svc.CreateCameraHandler(conn))
        End Using
    End Sub
End Class
