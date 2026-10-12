Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Remote
Imports Ptz.Gateway.Cameras
Imports Xunit

Public Class CameraApiTests

    Private Const Se As String = "/api/v1/cameras/se7c432mw"
    Private Const De As String = "/api/v1/cameras/de7232iw"

    Private Shared Function Count(h As FakeCameraHandler, method As String, path As String) As Integer
        SyncLock h.Requests
            Return h.Requests.Where(Function(r) r.Method = method AndAlso r.Path = path).Count()
        End SyncLock
    End Function

    Private Shared Function Last(h As FakeCameraHandler, path As String) As FakeCameraHandler.Recorded
        SyncLock h.Requests
            Return h.Requests.LastOrDefault(Function(r) r.Path = path)
        End SyncLock
    End Function

    Private Shared Sub WaitFor(cond As Func(Of Boolean), Optional seconds As Double = 5)
        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While Not cond() AndAlso sw.Elapsed.TotalSeconds < seconds
            Threading.Thread.Sleep(10)
        End While
    End Sub

    Private Shared Sub WaitOnline(h As Harness)
        WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Online AndAlso h.Svc.Cameras.Find("de7232iw").Online)
    End Sub

    <Fact>
    Public Sub Cameras_List_ShowsBothModels_AndHidesNetworkDetails()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("view")
            Dim resp As HttpResponseMessage = h.Send("GET", "/api/v1/cameras", token:=t)
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode)
            Dim body As String = Harness.Body(resp)
            Dim list As CameraListDto = Harness.Read(Of CameraListDto)(resp)
            Assert.Equal({"de7232iw", "se7c432mw"}, list.Items.Select(Function(c) c.Id).OrderBy(Function(x) x).ToArray())
            Assert.All(list.Items, Sub(c) Assert.True(c.Online))
            Assert.Contains("DS-2SE7C432MW-AEB", list.Items.Select(Function(c) c.Model))
            Assert.DoesNotContain("10.0.0.", body)
            Assert.DoesNotContain("Cam-P@ss", body)
            Assert.DoesNotContain("SN-SECRET", body)
        End Using
    End Sub

    <Fact>
    Public Sub Connect_HidesSerialAndFirmware_FromNonMaintenanceRoles()
        Using h As New Harness()
            WaitOnline(h)
            Dim r As ConnectResultDto = Harness.Read(Of ConnectResultDto)(h.Send("POST", Se & "/connect", token:=h.Token("alice")))
            Assert.Equal("", r.Device.SerialNumber)
            Assert.Equal("", r.Device.FirmwareVersion)
            Assert.Contains("ptz", r.Features)
        End Using
    End Sub

    <Fact>
    Public Sub Move_Operator_ReachesOnlyTheRightCamera_WithCombinedAxes_AndIsAuditedUnderRealUser()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 40, .Tilt = -20, .Zoom = 10, .Seq = 1}, t, ip:="203.0.113.5")
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode)
            Dim rec = Last(h.SeCamera, "/ISAPI/PTZCtrl/channels/1/momentary")
            Assert.NotNull(rec)
            Assert.Contains("<pan>40</pan>", rec.Body)
            Assert.Contains("<tilt>-20</tilt>", rec.Body)
            Assert.Equal(0, Count(h.DeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
            Dim audit As String = String.Join(vbLf, h.AuditLines())
            Assert.Contains("alice", audit)
            Assert.Contains("Ptz.Move", audit)
            Assert.Contains("ip=203.0.113.5", audit)
            Assert.Contains("via=gw", audit)
        End Using
    End Sub

    <Fact>
    Public Sub Move_ByViewer_IsForbidden_AndNothingReachesCamera()
        Using h As New Harness()
            WaitOnline(h)
            Dim before As Integer = Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary")
            Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10}, h.Token("view"))
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode)
            Dim env As ErrorEnvelope = Harness.Read(Of ErrorEnvelope)(r)
            Assert.Equal("forbidden", env.Error.Code)
            Assert.Equal("ptzControl", env.Error.Required)
            Assert.Equal(before, Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
        End Using
    End Sub

    <Theory>
    <InlineData("{""pan"":101,""tilt"":0,""zoom"":0}")>
    <InlineData("{""pan"":0,""tilt"":-101,""zoom"":0}")>
    <InlineData("{""pan"":1,""tilt"":0,""zoom"":0,""ttlMs"":50}")>
    <InlineData("{""pan"":1,""tilt"":0,""zoom"":0,""ttlMs"":99999}")>
    <InlineData("{""pan"":""40"",""tilt"":0,""zoom"":0}")>
    <InlineData("{""pan"":1.5,""tilt"":0,""zoom"":0}")>
    <InlineData("{""pan"":1,""tilt"":0,""zoom"":0,""x"":1}")>
    Public Sub Move_InvalidInput_Is400_AndNotClamped(json As String)
        Using h As New Harness()
            WaitOnline(h)
            Dim before As Integer = Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary")
            Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", json, h.Token("alice"))
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode)
            Assert.Equal(before, Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
        End Using
    End Sub

    <Fact>
    Public Sub Move_StaleSequence_Is409_NothingExecuted()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 10, .Seq = 5}, t).StatusCode)
            Dim n As Integer = Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary")
            Dim stale As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 50, .Seq = 5}, t)
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode)
            Assert.Equal("stale_seq", Harness.ErrorCode(stale))
            Assert.Equal(n, Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/stop", New StopRequest With {.Seq = 9}, t).StatusCode)
            Assert.Equal(HttpStatusCode.Conflict, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 5, .Seq = 8}, t).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub ZeroVector_IsTreatedAsStop()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30}, t)
            Dim before As Integer = Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 0, .Tilt = 0, .Zoom = 0}, t).StatusCode)
            Assert.Equal(before + 1, Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous"))
            Assert.Contains("<pan>0</pan>", Last(h.SeCamera, "/ISAPI/PTZCtrl/channels/1/continuous").Body)
        End Using
    End Sub

    <Fact>
    Public Sub Lease_StopsCamera_WhenClientStopsRenewing()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .TtlMs = 300}, t)
            Dim stops As Integer = Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous")
            h.Clock.Advance(5)                         ' انتهى العقد (لا تجديد)
            WaitFor(Function() Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous") > stops)
            Assert.True(Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous") > stops, "يجب أن يصدر الإيقاف تلقائياً")
            Assert.Contains("svc:lease", String.Join(vbLf, h.AuditLines()))
        End Using
    End Sub

    <Fact>
    Public Sub Lease_StopsCamera_WhenOwnerSessionIsLoggedOut()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .Seq = 1}, t)
            Dim stops As Integer = Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous")
            h.Send("POST", "/api/v1/auth/logout", token:=t)
            WaitFor(Function() Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous") > stops)
            Assert.True(Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/continuous") > stops)
        End Using
    End Sub

    <Fact>
    Public Sub Renewal_FromPreviousOwner_DoesNotOverrideAnotherOperatorsStop()
        Using h As New Harness()
            WaitOnline(h)
            Dim a As String = h.Token("alice")
            Dim sup As String = h.Token("sup")
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .Seq = 1}, a)
            h.Send("POST", Se & "/ptz/stop", token:=sup)                              ' مشغّل آخر يوقف
            Dim moves As Integer = Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .Seq = 2, .Renew = True}, a).StatusCode)
            Assert.Equal(moves, Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))      ' التجديد تُجوهل
            ' حركة جديدة صريحة (ليست تجديداً) تُنفَّذ
            h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 31, .Seq = 3}, a)
            Assert.Equal(moves + 1, Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary"))
        End Using
    End Sub

    <Fact>
    Public Sub Stop_IsExemptFromAuditGate_AndWorks_WhenAuditIsDown()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            h.Svc.AuditHealth.MarkFailed()
            Dim blocked As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 5}, t)
            Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode)
            Assert.Equal("audit_unavailable", Harness.ErrorCode(blocked))
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/stop", token:=t).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", "/api/v1/auth/logout", token:=t).StatusCode)
            h.Svc.AuditHealth.MarkHealthy()
        End Using
    End Sub

    <Fact>
    Public Sub Presets_CrudAndValidation()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Dim list As PresetListDto = Harness.Read(Of PresetListDto)(h.Send("GET", Se & "/presets", token:=sup))
            Assert.Equal("Gate", list.Items.Single().Name)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("PUT", Se & "/presets/2", New PresetPutRequest With {.Name = "Main Door (1)"}, sup).StatusCode)
            Assert.Contains("Main Door (1)", Last(h.SeCamera, "/ISAPI/PTZCtrl/channels/1/presets/2").Body)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/presets/2/goto", token:=sup).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("DELETE", Se & "/presets/2", token:=sup).StatusCode)
            ' تحقق
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/presets/301", New PresetPutRequest With {.Name = "x"}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/presets/0", New PresetPutRequest With {.Name = "x"}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/presets/abc", New PresetPutRequest With {.Name = "x"}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/presets/2", New PresetPutRequest With {.Name = "a<b"}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/presets/2", New PresetPutRequest With {.Name = "evil" & ChrW(&H202E) & "txt"}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/presets/2", New PresetPutRequest With {.Name = New String("n"c, 33)}, sup).StatusCode)
            ' المشغّل يستعمل ولا يدير
            Dim op As String = h.Token("alice")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/presets/1/goto", token:=op).StatusCode)
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("PUT", Se & "/presets/3", New PresetPutRequest With {.Name = "x"}, op).StatusCode)
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("DELETE", Se & "/presets/1", token:=op).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Image_GetPut_ValidatesAndAppliesReadModifyWrite()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Dim img As ImageDto = Harness.Read(Of ImageDto)(h.Send("GET", Se & "/image", token:=sup))
            Assert.Equal("40", img.Values("brightness"))
            Assert.Contains("brightness", img.Supported)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("PUT", Se & "/image", New ImagePutRequest With {.Values = New Dictionary(Of String, String) From {{"brightness", "75"}}}, sup).StatusCode)
            Assert.Contains("<brightnessLevel>75</brightnessLevel>", Last(h.SeCamera, "/ISAPI/Image/channels/1/color").Body)
            Dim bad1 As HttpResponseMessage = h.Send("PUT", Se & "/image", New ImagePutRequest With {.Values = New Dictionary(Of String, String) From {{"brightness", "101"}}}, sup)
            Assert.Equal(HttpStatusCode.BadRequest, bad1.StatusCode)
            Assert.Equal("brightness", Harness.Read(Of ErrorEnvelope)(bad1).Error.Field)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/image", New ImagePutRequest With {.Values = New Dictionary(Of String, String) From {{"hax", "1"}}}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/image", New ImagePutRequest With {.Values = New Dictionary(Of String, String) From {{"irCutMode", "<evil/>"}}}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("PUT", Se & "/image", New ImagePutRequest With {.Values = New Dictionary(Of String, String) From {{"brightness", "50"}}}, h.Token("alice")).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Aux_Lens_ZoomArea_Position_Validation()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("PUT", Se & "/aux/wiper", New AuxRequest With {.Enabled = True}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("PUT", Se & "/aux/laser", New AuxRequest With {.Enabled = True}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "far", .Speed = 50}, sup).StatusCode)
            Assert.Contains("<focus>50</focus>", Last(h.SeCamera, "/ISAPI/System/Video/inputs/channels/1/focus").Body)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "sideways", .Speed = 50}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "far", .Speed = 0}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/zoom-area", New ZoomAreaRequest With {.X = 0.4, .Y = 0.4, .Width = 0.2, .Height = 0.2}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/ptz/zoom-area", New ZoomAreaRequest With {.X = 0.9, .Y = 0.4, .Width = 0.2, .Height = 0.2}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/ptz/zoom-area", New ZoomAreaRequest With {.X = 0.1, .Y = 0.1, .Width = 0.0, .Height = 0.2}, sup).StatusCode)
            h.Clock.Advance(5)                         ' إعادة تعبئة دلو الأوامر (5/ث، دفعة 10)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("PUT", Se & "/ptz/position", New PositionDto With {.Azimuth = 90, .Elevation = 10, .Zoom = 4}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/ptz/position", New PositionDto With {.Azimuth = 361, .Elevation = 10, .Zoom = 4}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/ptz/position", New PositionDto With {.Azimuth = 1, .Elevation = 10, .Zoom = 99}, sup).StatusCode)
            Dim pos As PositionDto = Harness.Read(Of PositionDto)(h.Send("GET", Se & "/ptz/position", token:=sup))
            Assert.Equal(180.0, pos.Azimuth)
        End Using
    End Sub

    <Fact>
    Public Sub UnknownCamera_Is404_AfterPermissionCheck_AndRebootRouteIsNotMapped()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.NotFound, h.Send("GET", "/api/v1/cameras/nope", token:=sup).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("GET", "/api/v1/cameras/..%2Fadmin", token:=sup).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("GET", "/api/v1/cameras/SE7C432MW", token:=sup).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("POST", Se & "/device/reboot", New RebootRequest With {.Confirm = "REBOOT"}, sup).StatusCode)
            Assert.Equal(0, Count(h.SeCamera, "PUT", "/ISAPI/System/reboot"))
        End Using
    End Sub

    <Fact>
    Public Sub Snapshot_FreshAndCached_AndTierValidation()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("view")
            Dim cached As HttpResponseMessage = h.Send("GET", Se & "/snapshot", token:=t)
            Assert.Equal(HttpStatusCode.ServiceUnavailable, cached.StatusCode)       ' المحرك متوقف ⇒ لا إطار محتفظ به
            Dim fresh As HttpResponseMessage = h.Send("GET", Se & "/snapshot?fresh=1&tier=low", token:=t)
            Assert.Equal(HttpStatusCode.OK, fresh.StatusCode)
            Assert.Equal("image/jpeg", fresh.Content.Headers.ContentType.MediaType)
            Dim bytes As Byte() = fresh.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            Assert.Equal(CByte(&HFF), bytes(0))
            Assert.True(bytes.Length < Harness.MakeJpeg(1280, 720).Length)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", Se & "/snapshot?tier=ultra", token:=t).StatusCode)
            ' تحديد معدل الطلب الطازج
            Dim codes As New List(Of HttpStatusCode)()
            For i As Integer = 1 To 6
                codes.Add(h.Send("GET", Se & "/snapshot?fresh=1", token:=t).StatusCode)
            Next
            Assert.Contains(HttpStatusCode.TooManyRequests, codes)
        End Using
    End Sub

    <Fact>
    Public Sub CameraOffline_GivesServiceUnavailable_NotAnInternalError()
        Using h As New Harness()
            WaitOnline(h)
            h.SeCamera.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/momentary", "<x/>", HttpStatusCode.NotFound)
            h.SeCamera.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", "<ResponseStatus><statusCode>4</statusCode><statusString>Invalid Operation</statusString><subStatusCode>notSupport</subStatusCode></ResponseStatus>")
            Dim r As HttpResponseMessage = h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 5}, h.Token("alice"))
            Assert.Equal(HttpStatusCode.BadGateway, r.StatusCode)
            Dim env As ErrorEnvelope = Harness.Read(Of ErrorEnvelope)(r)
            Assert.Equal("camera_error", env.Error.Code)
            Assert.Equal("notSupport", env.Error.IsapiSubStatus)
            Assert.DoesNotContain("10.0.0.1", Harness.Body(r))
            Assert.DoesNotContain("Invalid Operation", Harness.Body(r))
        End Using
    End Sub

    <Fact>
    Public Sub TwoCameras_RunIndependently_Under_ConcurrentLoad()
        Using h As New Harness()
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            Dim tasks As New List(Of Task)()
            For i As Integer = 1 To 20
                Dim n As Integer = i
                tasks.Add(Task.Run(Sub() h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = (n Mod 50) + 1, .Seq = n}, t)))
                tasks.Add(Task.Run(Sub() h.Send("POST", De & "/ptz/move", New MoveRequest With {.Tilt = (n Mod 50) + 1, .Seq = n}, t)))
            Next
            Task.WaitAll(tasks.ToArray())
            Assert.True(Count(h.SeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary") > 0)
            Assert.True(Count(h.DeCamera, "PUT", "/ISAPI/PTZCtrl/channels/1/momentary") > 0)
            SyncLock h.SeCamera.Requests
                Assert.All(h.SeCamera.Requests.Where(Function(r) r.Path.EndsWith("/momentary")), Sub(r) Assert.Contains("<tilt>0</tilt>", r.Body))
            End SyncLock
            SyncLock h.DeCamera.Requests
                Assert.All(h.DeCamera.Requests.Where(Function(r) r.Path.EndsWith("/momentary")), Sub(r) Assert.Contains("<pan>0</pan>", r.Body))
            End SyncLock
        End Using
    End Sub
End Class
