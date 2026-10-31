Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Cameras
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Xunit

Public Class IsapiTests
    Private Shared ReadOnly NoCt As Threading.CancellationToken = Threading.CancellationToken.None

    Private Shared Function Conn() As CameraConnection
        Return New CameraConnection With {.Host = "192.168.1.64", .Port = 443, .Username = "admin", .Password = "x", .TimeoutSeconds = 5}
    End Function

    Private Shared Function MakeSe(handler As FakeCameraHandler) As ICameraController
        Return CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, Conn(), handler)
    End Function

    Private Shared Function MakeDe(handler As FakeCameraHandler) As ICameraController
        Return CameraControllerFactory.Create(CameraProfiles.Ds2De7232iwAe, Conn(), handler)
    End Function

    Private Shared Function OkHandler() As FakeCameraHandler
        Dim h As New FakeCameraHandler()
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/momentary", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/absolute", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/position3D", FakeCameraHandler.OkStatus)
        Return h
    End Function

    <Fact>
    Public Async Function Factory_ReturnsDistinctControllersWithOwnProfiles() As Task
        Dim a As ICameraController = MakeSe(New FakeCameraHandler())
        Dim b As ICameraController = MakeDe(New FakeCameraHandler())
        Assert.IsType(Of Ds2Se7c432mwAebController)(a)
        Assert.IsType(Of Ds2De7232iwAeController)(b)
        Assert.Equal(2560, a.Profile.ResolutionWidth)
        Assert.Equal(1920, b.Profile.ResolutionWidth)
        Assert.True((a.Features And CameraFeatures.LowLight) = CameraFeatures.LowLight)
        Assert.True((b.Features And CameraFeatures.LowLight) = 0)
        a.Dispose()
        b.Dispose()
        Await Task.CompletedTask
    End Function

    <Fact>
    Public Async Function Move_SendsMomentaryWithWatchdog_AndCombinedAxes() As Task
        Dim h As FakeCameraHandler = OkHandler()
        Using cam As ICameraController = MakeSe(h)
            Await cam.MoveAsync(New PtzVector(40, -20, 10), NoCt)
        End Using
        Dim r As FakeCameraHandler.Recorded = h.Requests.Single()
        Assert.Equal("PUT", r.Method)
        Assert.Equal("/ISAPI/PTZCtrl/channels/1/momentary", r.Path)
        Assert.Contains("<pan>40</pan>", r.Body)
        Assert.Contains("<tilt>-20</tilt>", r.Body)
        Assert.Contains("<zoom>10</zoom>", r.Body)
        Assert.Contains("<duration>1000</duration>", r.Body)
    End Function

    <Fact>
    Public Async Function Move_FallsBackToContinuous_WhenMomentaryUnsupported() As Task
        Dim h As New FakeCameraHandler()
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", FakeCameraHandler.OkStatus)
        Using cam As ICameraController = MakeDe(h)
            Await cam.MoveAsync(New PtzVector(10, 0, 0), NoCt)
            Await cam.MoveAsync(New PtzVector(20, 0, 0), NoCt)
        End Using
        Assert.Equal(3, h.Requests.Count)  ' محاولة momentary واحدة فقط ثم continuous مرتين
        Assert.Equal("/ISAPI/PTZCtrl/channels/1/momentary", h.Requests(0).Path)
        Assert.All(h.Requests.Skip(1), Sub(r) Assert.Equal("/ISAPI/PTZCtrl/channels/1/continuous", r.Path))
        Assert.DoesNotContain("Momentary", h.Requests(1).Body)
    End Function

    <Fact>
    Public Async Function Stop_SendsZeroVectorToContinuous() As Task
        Dim h As FakeCameraHandler = OkHandler()
        Using cam As ICameraController = MakeSe(h)
            Await cam.StopAsync(NoCt)
        End Using
        Assert.Equal("/ISAPI/PTZCtrl/channels/1/continuous", h.Requests.Single().Path)
        Assert.Contains("<pan>0</pan>", h.Requests.Single().Body)
    End Function

    <Fact>
    Public Sub PtzVector_ClampsOutOfRangeSpeeds()
        Dim v As New PtzVector(500, -500, 101)
        Assert.Equal(100, v.Pan)
        Assert.Equal(-100, v.Tilt)
        Assert.Equal(100, v.Zoom)
    End Sub

    <Fact>
    Public Async Function GoToPosition_ConvertsToTenthsAndNormalizesAzimuth() As Task
        Dim h As FakeCameraHandler = OkHandler()
        Using cam As ICameraController = MakeSe(h)
            Await cam.GoToPositionAsync(New PtzPosition(-90.0, 120.0, 99.0), NoCt)
        End Using
        Dim body As String = h.Requests.Single().Body
        Assert.Contains("<azimuth>2700</azimuth>", body)
        Assert.Contains("<elevation>900</elevation>", body)
        Assert.Contains("<absoluteZoom>320</absoluteZoom>", body)
    End Function

    <Fact>
    Public Async Function ZoomToArea_ConvertsToCameraCoordinates_AndCentersOnClick() As Task
        Dim h As FakeCameraHandler = OkHandler()
        Using cam As ICameraController = MakeSe(h)
            Await cam.ZoomToAreaAsync(New NormRect(0.25, 0.25, 0.5, 0.5), NoCt)
            Await cam.ZoomToAreaAsync(New NormRect(0.5, 0.5, 0, 0), NoCt)
        End Using
        Assert.Contains("<positionX>64</positionX>", h.Requests(0).Body)
        Assert.Contains("<positionX>191</positionX>", h.Requests(0).Body)
        Assert.Contains("<positionX>115</positionX>", h.Requests(1).Body)
    End Function

    <Fact>
    Public Async Function GetPosition_ParsesStatus() As Task
        Dim h As New FakeCameraHandler()
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/status",
                "<PTZStatus xmlns=""http://www.hikvision.com/ver20/XMLSchema""><AbsoluteHigh><elevation>150</elevation><azimuth>1800</azimuth><absoluteZoom>85</absoluteZoom></AbsoluteHigh></PTZStatus>")
        Using cam As ICameraController = MakeSe(h)
            Dim p As PtzPosition = Await cam.GetPositionAsync(NoCt)
            Assert.Equal(180.0, p.Azimuth)
            Assert.Equal(15.0, p.Elevation)
            Assert.Equal(8.5, p.Zoom)
        End Using
    End Function

    <Theory>
    <InlineData(0)>
    <InlineData(301)>
    <InlineData(-5)>
    Public Async Function Preset_OutOfRangeId_IsRejectedBeforeAnyRequest(id As Integer) As Task
        Dim h As New FakeCameraHandler()
        Using cam As ICameraController = MakeSe(h)
            Await Assert.ThrowsAsync(Of ArgumentOutOfRangeException)(Function() cam.GoToPresetAsync(id, NoCt))
        End Using
        Assert.Empty(h.Requests)
    End Function

    <Fact>
    Public Async Function Presets_ListSavedWithSanitizedNames() As Task
        Dim h As New FakeCameraHandler()
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/presets",
                "<PTZPresetList><PTZPreset><id>1</id><enabled>true</enabled><presetName>Gate</presetName></PTZPreset><PTZPreset><id>2</id><enabled>false</enabled></PTZPreset></PTZPresetList>")
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/presets/5", FakeCameraHandler.OkStatus)
        Using cam As ICameraController = MakeDe(h)
            Dim list As IReadOnlyList(Of PtzPreset) = Await cam.GetPresetsAsync(NoCt)
            Assert.Single(list)
            Assert.Equal("Gate", list(0).Name)
            Await cam.SavePresetAsync(5, "<script>&evil" & vbCr & "name</script>", NoCt)
        End Using
        Dim body As String = h.Requests.Last().Body
        Assert.DoesNotContain("<script>", body)
        Assert.DoesNotContain("&evil", body)
        Assert.Contains("<presetName>scriptevilname/script</presetName>", body)
    End Function

    <Fact>
    Public Async Function ImageSettings_ReadModifyWrite_PreservesOtherFields() As Task
        Dim h As New FakeCameraHandler()
        Dim colorXml As String = "<Color xmlns=""http://www.hikvision.com/ver20/XMLSchema""><brightnessLevel>50</brightnessLevel><contrastLevel>50</contrastLevel><saturationLevel>50</saturationLevel><graphicsMode>keep</graphicsMode></Color>"
        h.OnXml("GET", "/ISAPI/Image/channels/1/color", colorXml)
        h.OnXml("PUT", "/ISAPI/Image/channels/1/color", FakeCameraHandler.OkStatus)
        Using cam As ICameraController = MakeSe(h)
            Dim s As New ImageSettings()
            s(ImageParam.Brightness) = "70"
            Await cam.ApplyImageSettingsAsync(s, NoCt)
        End Using
        Dim put As FakeCameraHandler.Recorded = h.Requests.Last()
        Assert.Equal("PUT", put.Method)
        Assert.Contains("<brightnessLevel>70</brightnessLevel>", put.Body)
        Assert.Contains("<contrastLevel>50</contrastLevel>", put.Body)
        Assert.Contains("<graphicsMode>keep</graphicsMode>", put.Body)
    End Function

    <Fact>
    Public Sub ImageSettings_ValidatesRangesAndEnums()
        Dim s As New ImageSettings()
        Assert.Throws(Of ArgumentException)(Sub() s(ImageParam.Brightness) = "101")
        Assert.Throws(Of ArgumentException)(Sub() s(ImageParam.Brightness) = "abc")
        Assert.Throws(Of ArgumentException)(Sub() s(ImageParam.IrCutMode) = "<evil/>")
        s(ImageParam.IrCutMode) = "NIGHT"
        Assert.Equal("night", s(ImageParam.IrCutMode))
    End Sub

    <Fact>
    Public Async Function GetImageSettings_ReadsAvailableAndSkipsUnsupported() As Task
        Dim h As New FakeCameraHandler()
        h.OnXml("GET", "/ISAPI/Image/channels/1/color", "<Color><brightnessLevel>33</brightnessLevel><contrastLevel>44</contrastLevel><saturationLevel>55</saturationLevel></Color>")
        h.OnXml("GET", "/ISAPI/Image/channels/1/irCutFilter", "<IrcutFilter><IrcutFilterType>night</IrcutFilterType></IrcutFilter>")
        Using cam As ICameraController = MakeDe(h)
            Dim s As ImageSettings = Await cam.GetImageSettingsAsync(NoCt)
            Assert.Equal("33", s(ImageParam.Brightness))
            Assert.Equal("night", s(ImageParam.IrCutMode))
            Assert.Null(s(ImageParam.WdrMode))
        End Using
    End Function

    <Fact>
    Public Async Function Snapshot_RejectsNonJpeg_AcceptsJpeg() As Task
        Dim h As New FakeCameraHandler()
        Dim body As Byte() = {1, 2, 3, 4}
        h.On("GET", "/ISAPI/Streaming/channels/101/picture", Function(r) New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(body)})
        Using cam As ICameraController = MakeSe(h)
            Await Assert.ThrowsAsync(Of IsapiException)(Function() cam.GetSnapshotAsync(NoCt))
            body = {&HFF, &HD8, &HFF, &HE0, 0}
            Dim jpg As Byte() = Await cam.GetSnapshotAsync(NoCt)
            Assert.Equal(CByte(&HD8), jpg(1))
        End Using
    End Function

    <Fact>
    Public Async Function Error401_IsMappedWithoutLeakingCredentials() As Task
        Dim h As New FakeCameraHandler()
        h.OnXml("GET", "/ISAPI/System/deviceInfo", "<x/>", HttpStatusCode.Unauthorized)
        Using cam As ICameraController = MakeSe(h)
            Dim ex As IsapiException = Await Assert.ThrowsAsync(Of IsapiException)(Function() cam.ConnectAsync(NoCt))
            Assert.Equal(401, ex.HttpStatus)
            Assert.DoesNotContain("admin", ex.Message)
        End Using
    End Function

    <Fact>
    Public Async Function Gets_AreRetriedOnTransientFailure_ButPutsAreNot() As Task
        Dim h As New FakeCameraHandler() With {.FailFirstNGets = 2}
        h.OnXml("GET", "/ISAPI/System/deviceInfo", "<DeviceInfo><deviceName>Gate</deviceName><model>DS-2SE7C432MW-AEB</model></DeviceInfo>")
        Using client As New IsapiClient(Conn(), h)
            Dim doc = Await client.GetXmlAsync("/ISAPI/System/deviceInfo", NoCt)
            Assert.Equal("Gate", IsapiXml.ValueOf(doc, "deviceName"))
        End Using
        Assert.Equal(3, h.Requests.Count)

        Dim h2 As New FakeCameraHandler()
        h2.On("PUT", "/ISAPI/PTZCtrl/channels/1/continuous",
              Function(r As FakeCameraHandler.Recorded) As HttpResponseMessage
                  Throw New HttpRequestException("boom")
              End Function)
        Using client As New IsapiClient(Conn(), h2)
            Dim ex As IsapiException = Await Assert.ThrowsAsync(Of IsapiException)(Function() client.PutAsync("/ISAPI/PTZCtrl/channels/1/continuous", IsapiXml.Build("PTZData"), NoCt))
            Assert.Equal(0, ex.HttpStatus)
        End Using
        Assert.Single(h2.Requests)
    End Function

    <Theory>
    <InlineData("/etc/passwd")>
    <InlineData("/ISAPI/../admin")>
    <InlineData("/ISAPI/x//y")>
    <InlineData("http://evil/ISAPI/x")>
    <InlineData("")>
    Public Async Function Client_RejectsSuspiciousPaths(path As String) As Task
        Dim h As New FakeCameraHandler()
        Using client As New IsapiClient(Conn(), h)
            Await Assert.ThrowsAsync(Of ArgumentException)(Function() client.GetXmlAsync(path, NoCt))
        End Using
        Assert.Empty(h.Requests)
    End Function

    <Fact>
    Public Sub Xml_Parser_BlocksDtdEntityExpansion()
        Dim xxe As String = "<?xml version=""1.0""?><!DOCTYPE x [<!ENTITY e SYSTEM ""file:///etc/passwd"">]><x>&e;</x>"
        Assert.ThrowsAny(Of Xml.XmlException)(Function() IsapiXml.Parse(xxe))
    End Sub

    <Fact>
    Public Async Function Command_RejectedByDevice_RaisesIsapiException() As Task
        Dim h As New FakeCameraHandler()
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/presets/3/goto", "<ResponseStatus><statusCode>4</statusCode><statusString>Invalid Operation</statusString><subStatusCode>notSupport</subStatusCode></ResponseStatus>")
        Using cam As ICameraController = MakeSe(h)
            Dim ex As IsapiException = Await Assert.ThrowsAsync(Of IsapiException)(Function() cam.GoToPresetAsync(3, NoCt))
            Assert.Equal("notSupport", ex.SubStatus)
        End Using
    End Function

    <Fact>
    Public Async Function Connect_ProbesCapabilities_AndDisablesUnsupportedFeatures() As Task
        Dim h As New FakeCameraHandler()
        h.OnXml("GET", "/ISAPI/System/deviceInfo", "<DeviceInfo><deviceName>D</deviceName><model>X</model><serialNumber>S1</serialNumber><firmwareVersion>V5</firmwareVersion></DeviceInfo>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/capabilities", "<PTZChanelCap/>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/presets", "<PTZPresetList/>")
        h.OnXml("GET", "/ISAPI/Image/channels/1/color", "<Color><brightnessLevel>1</brightnessLevel></Color>")
        h.OnXml("GET", "/ISAPI/Image/channels/1/irCutFilter", "<IrcutFilter/>")
        Using cam As ICameraController = MakeSe(h)
            Dim info As DeviceInfo = Await cam.ConnectAsync(NoCt)
            Assert.Equal("S1", info.SerialNumber)
            Assert.True((cam.Features And CameraFeatures.Ptz) <> 0)
            Assert.True((cam.Features And CameraFeatures.IrCut) <> 0)
            Assert.True((cam.Features And CameraFeatures.Wdr) = 0, "WDR غير مدعوم (404) فيجب حذفه")
            Assert.True((cam.Features And CameraFeatures.Defog) = 0)
            Dim wdr As New ImageSettings()
            wdr(ImageParam.WdrMode) = "open"
            Await Assert.ThrowsAsync(Of NotSupportedException)(Function() cam.ApplyImageSettingsAsync(wdr, NoCt))
        End Using
    End Function

    ' ---------- تدفق الأحداث ----------
    <Fact>
    Public Sub AlertStreamParser_HandlesSplitChunks_AndFiltersHeartbeat()
        Dim p As New AlertStreamParser()
        Dim ev1 As String = "<EventNotificationAlert><channelID>1</channelID><eventType>linedetection</eventType><eventState>active</eventState><dateTime>2026-01-01T10:00:00Z</dateTime><eventDescription>line</eventDescription></EventNotificationAlert>"
        Dim hb As String = "<EventNotificationAlert><eventType>videoloss</eventType><eventState>inactive</eventState></EventNotificationAlert>"
        Dim stream As String = "--boundary" & vbCrLf & "Content-Type: application/xml" & vbCrLf & vbCrLf & ev1 & vbCrLf & "--boundary" & vbCrLf & hb
        Dim got As New List(Of DeviceEvent)()
        For i As Integer = 0 To stream.Length - 1 Step 7
            got.AddRange(p.Feed(stream.Substring(i, Math.Min(7, stream.Length - i))))
        Next
        Assert.Single(got)
        Assert.Equal("linedetection", got(0).EventType)
        Assert.Equal(1, got(0).ChannelId)
    End Sub

    <Fact>
    Public Sub AlertStreamParser_GarbageAndOversizedInput_DoNotCrash()
        Dim p As New AlertStreamParser()
        Assert.Empty(p.Feed("<EventNotificationAlert><unclosed"))
        Assert.Empty(p.Feed(New String("x"c, 2 * 1024 * 1024)))
        Assert.Empty(p.Feed("<EventNotificationAlert>not xml &&</EventNotificationAlert>"))
    End Sub

    ' ---------- المتحكم المؤمَّن ----------
    Private NotInheritable Class Rig
        Public ReadOnly Handler As FakeCameraHandler = OkHandler()
        Public ReadOnly Audit As New ListAudit()
        Public ReadOnly Clock As New TestClock()
        Public Function Secure(role As Role) As SecuredCameraController
            Dim inner As ICameraController = MakeSe(Handler)
            Dim sess As New UserSession("tester", role, Clock.UtcNow)
            Return New SecuredCameraController(inner, sess, New AccessControl(Clock), Audit, Clock)
        End Function
    End Class

    <Fact>
    Public Async Function Secured_ViewerCannotMove_DeniedIsAudited_NoRequestSent() As Task
        Dim rig As New Rig()
        Using cam As SecuredCameraController = rig.Secure(Role.Viewer)
            Await Assert.ThrowsAsync(Of AccessDeniedException)(Function() cam.MoveAsync(New PtzVector(10, 0, 0), NoCt))
        End Using
        Assert.Empty(rig.Handler.Requests)
        Assert.Contains("tester|Ptz.Move|DENIED", rig.Audit.Entries)
    End Function

    <Fact>
    Public Async Function Secured_OperatorMoves_AndMoveAuditIsThrottled() As Task
        Dim rig As New Rig()
        Using cam As SecuredCameraController = rig.Secure(Role.Operator)
            For i As Integer = 1 To 10
                Await cam.MoveAsync(New PtzVector(30, 0, 0), NoCt)
            Next
            Await cam.StopAsync(NoCt)
            Await Assert.ThrowsAsync(Of AccessDeniedException)(Function() cam.RebootAsync(NoCt))
        End Using
        Assert.Equal(11, rig.Handler.Requests.Count)
        Assert.Equal(1, rig.Audit.Entries.Where(Function(e) e = "tester|Ptz.Move|OK").Count())
        Assert.Contains("tester|Ptz.Stop|OK", rig.Audit.Entries)
        Assert.Contains("tester|Device.Reboot|DENIED", rig.Audit.Entries)
    End Function

    <Fact>
    Public Async Function Secured_DeviceFailure_IsAuditedAsFail() As Task
        Dim rig As New Rig()
        Using cam As SecuredCameraController = rig.Secure(Role.Supervisor)
            Await Assert.ThrowsAsync(Of IsapiException)(Function() cam.GoToPresetAsync(3, NoCt)) ' لا مسار → 404
        End Using
        Assert.Contains("tester|Preset.Goto|FAIL", rig.Audit.Entries)
    End Function

    <Fact>
    Public Async Function Secured_AutoTrackMover_NeedsAutoTrackPermission_NotPtz() As Task
        Dim rig As New Rig()
        Using viewer As SecuredCameraController = rig.Secure(Role.Viewer)
            Await Assert.ThrowsAsync(Of AccessDeniedException)(Function() viewer.AsAutoTrackMover().MoveAsync(New PtzVector(5, 0, 0), NoCt))
        End Using
        Using op As SecuredCameraController = rig.Secure(Role.Operator)
            Await op.AsAutoTrackMover().MoveAsync(New PtzVector(5, 0, 0), NoCt)
        End Using
        Assert.Contains("tester|AutoTrack.Move|OK", rig.Audit.Entries)
    End Function

    <Fact>
    Public Async Function Secured_ExpiredSession_BlocksEverything() As Task
        Dim rig As New Rig()
        Using cam As SecuredCameraController = rig.Secure(Role.Administrator)
            rig.Clock.Advance(60 * 60)
            Await Assert.ThrowsAsync(Of AccessDeniedException)(Function() cam.GetSnapshotAsync(NoCt))
        End Using
    End Function

    ' ---------- تكامل الكاميرتين معاً ----------
    <Fact>
    Public Async Function TwoCameras_RunConcurrently_WithIsolatedState() As Task
        Dim h1 As FakeCameraHandler = OkHandler()
        Dim h2 As FakeCameraHandler = OkHandler()
        Using c1 As ICameraController = MakeSe(h1), c2 As ICameraController = MakeDe(h2)
            Dim tasks As New List(Of Task)()
            For i As Integer = 1 To 50
                tasks.Add(c1.MoveAsync(New PtzVector(i, 0, 0), NoCt))
                tasks.Add(c2.MoveAsync(New PtzVector(0, i, 0), NoCt))
            Next
            Await Task.WhenAll(tasks)
        End Using
        Assert.Equal(50, h1.Requests.Count)
        Assert.Equal(50, h2.Requests.Count)
        Assert.All(h1.Requests, Sub(r) Assert.Contains("<tilt>0</tilt>", r.Body))
        Assert.All(h2.Requests, Sub(r) Assert.Contains("<pan>0</pan>", r.Body))
    End Function
End Class
