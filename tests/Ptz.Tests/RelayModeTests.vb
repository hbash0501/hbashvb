Imports Ptz.Core.Abstractions
Imports Ptz.Core.Cameras
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Presentation
Imports Xunit

''' <summary>وضع النقل السحابي في النواة: المتحكم يلتزم بالمجموعة الموثَّقة ولا يفحص قدرات غير مدرجة، ومخزن الإعداد يقبل كاميرا بلا عنوان.</summary>
Public Class RelayModeTests

    Private Shared Function RelayConn() As CameraConnection
        Return New CameraConnection With {.Transport = CameraTransport.HikPartnerPro, .CloudSerial = "SE7SERIAL"}
    End Function

    Private Shared Function Handler() As FakeCameraHandler
        Dim h As New FakeCameraHandler()
        h.OnXml("GET", "/ISAPI/System/deviceInfo", "<DeviceInfo><deviceName>C</deviceName><model>DS-2SE7C432MW-AEB</model><serialNumber>S</serialNumber><firmwareVersion>V5</firmwareVersion></DeviceInfo>")
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/presets/3", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/presets/3/goto", FakeCameraHandler.OkStatus)
        h.OnXml("DELETE", "/ISAPI/PTZCtrl/channels/1/presets/3", FakeCameraHandler.OkStatus)
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/presets", "<PTZPresetList><PTZPreset><id>3</id><enabled>true</enabled><presetName>Gate</presetName></PTZPreset></PTZPresetList>")
        Return h
    End Function

    Private Shared Function Paths(h As FakeCameraHandler) As String()
        SyncLock h.Requests
            Return h.Requests.Select(Function(r) r.Method & " " & r.Path).ToArray()
        End SyncLock
    End Function

    Private Shared Sub Refused(act As Func(Of Task))
        Assert.Throws(Of NotSupportedException)(Sub() act().GetAwaiter().GetResult())
    End Sub

    <Fact>
    Public Sub Connect_ReadsDeviceInfoOnly_NoCapabilityProbes_AndFixesTheFeatureSet()
        Dim h As FakeCameraHandler = Handler()
        Using ctl As ICameraController = CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, RelayConn(), h)
            Dim info As DeviceInfo = ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            Assert.Equal("DS-2SE7C432MW-AEB", info.Model)
            Assert.Equal(CameraFeatures.Ptz Or CameraFeatures.Presets, ctl.Features)
        End Using
        Assert.Equal({"GET /ISAPI/System/deviceInfo"}, Paths(h))
    End Sub

    <Fact>
    Public Sub Lan_StillProbesCapabilities_AndKeepsItsFullFeatureSet()
        Dim h As FakeCameraHandler = Handler()
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/capabilities", "<PTZChanelCap/>")
        Dim lan As New CameraConnection With {.Host = "10.0.0.1", .Username = "svc"}
        Using ctl As ICameraController = CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, lan, h)
            ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            Assert.True((ctl.Features And CameraFeatures.Snapshot) <> 0)
        End Using
        Assert.Contains("GET /ISAPI/PTZCtrl/channels/1/capabilities", Paths(h))
    End Sub

    <Fact>
    Public Sub Move_FallsBackFromMomentaryToContinuous_AndStopSendsZeroVector()
        Dim h As FakeCameraHandler = Handler()
        Using ctl As ICameraController = CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, RelayConn(), h)
            ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            ctl.MoveAsync(New PtzVector(30, -10, 5), Threading.CancellationToken.None).GetAwaiter().GetResult()
            ctl.StopAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
        End Using
        SyncLock h.Requests
            Dim puts = h.Requests.Where(Function(r) r.Method = "PUT" AndAlso r.Path.EndsWith("/continuous")).ToList()
            Assert.Equal(2, puts.Count)
            Assert.Contains("<pan>30</pan>", puts(0).Body)
            Assert.Contains("<pan>0</pan>", puts(1).Body)
        End SyncLock
    End Sub

    <Fact>
    Public Sub Presets_UseTheDocumentedPaths_AndTheSaveBodyCarriesEnabled()
        Dim h As FakeCameraHandler = Handler()
        Using ctl As ICameraController = CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, RelayConn(), h)
            ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            Assert.Equal("Gate", ctl.GetPresetsAsync(Threading.CancellationToken.None).GetAwaiter().GetResult().Single().Name)
            ctl.SavePresetAsync(3, "Door", Threading.CancellationToken.None).GetAwaiter().GetResult()
            ctl.GoToPresetAsync(3, Threading.CancellationToken.None).GetAwaiter().GetResult()
            ctl.DeletePresetAsync(3, Threading.CancellationToken.None).GetAwaiter().GetResult()
        End Using
        SyncLock h.Requests
            Dim save = h.Requests.Single(Function(r) r.Method = "PUT" AndAlso r.Path = "/ISAPI/PTZCtrl/channels/1/presets/3")
            Dim body As String = save.Body
            Assert.True(body.IndexOf("<enabled>true</enabled>", StringComparison.Ordinal) >= 0)
            Assert.True(body.IndexOf("<enabled>", StringComparison.Ordinal) < body.IndexOf("<id>", StringComparison.Ordinal), "ترتيب الحقول كما في XML_Set_PTZPreset")
        End SyncLock
    End Sub

    <Fact>
    Public Sub OperationsWithoutADocumentedCloudPath_AreRefusedLocally_WithoutAnyRequest()
        Dim h As FakeCameraHandler = Handler()
        Using ctl As ICameraController = CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, RelayConn(), h)
            ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            Dim before As Integer = Paths(h).Length
            Dim ct As Threading.CancellationToken = Threading.CancellationToken.None
            Refused(Function() ctl.GetPositionAsync(ct))
            Refused(Function() ctl.GoToPositionAsync(New PtzPosition(10, 10, 2), ct))
            Refused(Function() ctl.RebootAsync(ct))
            Refused(Function() ctl.GoHomeAsync(ct))
            Refused(Function() ctl.ZoomToAreaAsync(New NormRect(0.4, 0.4, 0.2, 0.2), ct))
            Refused(Function() ctl.MoveFocusAsync(LensDirection.Far, 50, ct))
            Refused(Function() ctl.MoveIrisAsync(LensDirection.Far, 50, ct))
            Refused(Function() ctl.StartPatrolAsync(1, ct))
            Refused(Function() ctl.GetSnapshotAsync(ct))
            Refused(Function() ctl.SetAuxAsync(AuxKind.Wiper, True, ct))
            Assert.Equal(before, Paths(h).Length)
        End Using
    End Sub

    <Fact>
    Public Sub Relay_WithoutAHandler_IsRefused_NoDirectConnectionIsEverMade()
        Assert.Throws(Of InvalidOperationException)(Function() CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, RelayConn()))
    End Sub

    <Fact>
    Public Sub Transport_ParseAndSerialNames()
        Assert.Equal(CameraTransport.Lan, CameraTransports.Parse(Nothing))
        Assert.Equal(CameraTransport.Lan, CameraTransports.Parse("LAN"))
        Assert.Equal(CameraTransport.HikPartnerPro, CameraTransports.Parse("Hpp"))
        Assert.Throws(Of ArgumentException)(Function() CameraTransports.Parse("cloud"))
        Assert.Equal("hpp", CameraTransports.Name(CameraTransport.HikPartnerPro))
        Assert.True(CameraTransports.IsValidCloudSerial("DS-2DE7232IW_AE123"))
        Assert.False(CameraTransports.IsValidCloudSerial("a b"))
        Assert.False(CameraTransports.IsValidCloudSerial("ａｂ"))     ' عريضة العرض: ليست ASCII
    End Sub

    <Fact>
    Public Sub ConfigStore_AcceptsRelayCameraWithoutHostOrUser_AndBuildsARelayConnection()
        Dim dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-relay-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(dir)
        Try
            Dim keys As New Ptz.Core.Security.FileKeyProvider(IO.Path.Combine(dir, "master.key"))
            Dim store As New CameraConfigStore(IO.Path.Combine(dir, "cameras.json"), New Ptz.Core.Security.CredentialVault(keys))
            store.EnsureDefaults({("se7c432mw", CameraProfiles.Ds2Se7c432mwAeb)})
            Dim cfg As CameraConfig = store.Get("se7c432mw")
            cfg.Transport = "hpp"
            cfg.CloudSerial = "SE7SERIAL"
            store.Update(cfg, Nothing)
            Dim conn As CameraConnection = store.BuildConnection("se7c432mw")
            Assert.True(conn.IsRelay)
            Assert.Equal("SE7SERIAL", conn.CloudSerial)
            ' بلا رقم تسلسلي لا يُقبل الحفظ.
            cfg.CloudSerial = ""
            Assert.Throws(Of ArgumentException)(Sub() store.Update(cfg, Nothing))
            ' ونقل مجهول مرفوض.
            cfg.CloudSerial = "SE7SERIAL"
            cfg.Transport = "carrier-pigeon"
            Assert.Throws(Of ArgumentException)(Sub() store.Update(cfg, Nothing))
        Finally
            Try
                IO.Directory.Delete(dir, True)
            Catch ex As IO.IOException
            End Try
        End Try
    End Sub
End Class
