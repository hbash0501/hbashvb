Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.TestHost
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway
Imports Ptz.Gateway.Api
Imports Ptz.Gateway.Hosting
Imports Ptz.Gateway.Imaging
Imports Ptz.Presentation
Imports SkiaSharp

''' <summary>بيئة اختبار كاملة: خادم TestServer حقيقي + كاميرتان وهميتان + مستخدمون وأسرار TOTP + ساعة قابلة للتحكم.</summary>
Public NotInheritable Class Harness
    Implements IDisposable

    Public Const Pw As String = "Str0ng!Passw0rd"

    Public ReadOnly Property Dir As String
    Public ReadOnly Property Clock As New TestClock()
    Public ReadOnly Property Options As GatewayOptions
    Public ReadOnly Property Svc As GatewayServices
    Public ReadOnly Property App As WebApplication
    Public ReadOnly Property Client As HttpClient
    Public ReadOnly Property SeCamera As New FakeCameraHandler()
    Public ReadOnly Property DeCamera As New FakeCameraHandler()
    Public ReadOnly Property Registry As ApiRegistry
    Public ReadOnly Property Secrets As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)
    Private _reg As ApiRegistry

    Public Shared Function MakeJpeg(width As Integer, height As Integer, Optional shade As Byte = 120) As Byte()
        Using bmp As New SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)
            Using canvas As New SKCanvas(bmp)
                canvas.Clear(New SKColor(shade, shade, shade))
                Using p As New SKPaint With {.Color = SKColors.White}
                    canvas.DrawRect(width \ 4, height \ 4, width \ 4, height \ 4, p)
                End Using
            End Using
            Using img As SKImage = SKImage.FromBitmap(bmp)
                Using data As SKData = img.Encode(SKEncodedImageFormat.Jpeg, 85)
                    Return data.ToArray()
                End Using
            End Using
        End Using
    End Function

    Public Shared Sub ConfigureCamera(h As FakeCameraHandler, jpeg As Byte())
        h.OnXml("GET", "/ISAPI/System/deviceInfo", "<DeviceInfo><deviceName>Cam</deviceName><model>X</model><serialNumber>SN-SECRET</serialNumber><firmwareVersion>V5</firmwareVersion></DeviceInfo>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/capabilities", "<PTZChanelCap/>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/presets", "<PTZPresetList><PTZPreset><id>1</id><enabled>true</enabled><presetName>Gate</presetName></PTZPreset></PTZPresetList>")
        h.OnXml("GET", "/ISAPI/PTZCtrl/channels/1/status", "<PTZStatus><AbsoluteHigh><elevation>150</elevation><azimuth>1800</azimuth><absoluteZoom>85</absoluteZoom></AbsoluteHigh></PTZStatus>")
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/momentary", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/absolute", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/position3D", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/homeposition", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/homeposition/goto", FakeCameraHandler.OkStatus)
        For i As Integer = 1 To 4
            h.OnXml("PUT", $"/ISAPI/PTZCtrl/channels/1/presets/{i}", FakeCameraHandler.OkStatus)
            h.OnXml("PUT", $"/ISAPI/PTZCtrl/channels/1/presets/{i}/goto", FakeCameraHandler.OkStatus)
            h.OnXml("DELETE", $"/ISAPI/PTZCtrl/channels/1/presets/{i}", FakeCameraHandler.OkStatus)
        Next
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/patrols/1/start", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/patrols/1/stop", FakeCameraHandler.OkStatus)
        h.OnXml("GET", "/ISAPI/Image/channels/1/color", "<Color><brightnessLevel>40</brightnessLevel><contrastLevel>50</contrastLevel><saturationLevel>60</saturationLevel></Color>")
        h.OnXml("PUT", "/ISAPI/Image/channels/1/color", FakeCameraHandler.OkStatus)
        h.OnXml("GET", "/ISAPI/Image/channels/1/irCutFilter", "<IrcutFilter><IrcutFilterType>auto</IrcutFilterType></IrcutFilter>")
        h.OnXml("PUT", "/ISAPI/System/Video/inputs/channels/1/focus", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/auxcontrols/1", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/System/reboot", FakeCameraHandler.OkStatus)
        h.On("GET", "/ISAPI/Streaming/channels/101/picture", Function(r) New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(jpeg)})
    End Sub

    Public Sub New(Optional configure As Action(Of GatewayOptions) = Nothing, Optional engines As Boolean = False)
        Dir = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-gw-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(Dir)
        Options = New GatewayOptions With {.DataDir = Dir, .StartEngines = engines}
        Options.Frames.GatingEnabled = False
        If configure IsNot Nothing Then configure(Options)

        Dim keys As New FileKeyProvider(IO.Path.Combine(Dir, "master.key"))
        Dim master As Byte() = keys.GetMasterKey()
        Dim vault As New CredentialVault(keys)
        Dim audit As New FileAuditLog(IO.Path.Combine(Dir, "audit.log"), KeyDerivation.Derive(master, "audit"), Clock)
        Dim users As New UserStore(IO.Path.Combine(Dir, "users.json"), audit, Clock, 1000)
        users.CreateUser("setup", "alice", Pw, Role.Operator)
        users.CreateUser("setup", "root1", Pw, Role.Administrator)
        users.CreateUser("setup", "sup", Pw, Role.Supervisor)
        users.CreateUser("setup", "view", Pw, Role.Viewer)
        Dim totp As New TotpStore(IO.Path.Combine(Dir, "totp.json"), IO.Path.Combine(Dir, "totp-state.json"), vault, Clock)
        For Each u As String In {"alice", "root1", "sup", "view"}
            Secrets(u) = totp.Enroll(u)
        Next
        Dim cams As New CameraConfigStore(IO.Path.Combine(Dir, "cameras.json"), vault)
        cams.EnsureDefaults({("se7c432mw", CameraProfiles.Ds2Se7c432mwAeb), ("de7232iw", CameraProfiles.Ds2De7232iwAe)})
        Dim c1 As CameraConfig = cams.Get("se7c432mw")
        c1.Host = "10.0.0.1" : c1.Username = "svc" : c1.DisplayName = "Gate"
        cams.Update(c1, "Cam-P@ss-1")
        Dim c2 As CameraConfig = cams.Get("de7232iw")
        c2.Host = "10.0.0.2" : c2.Username = "svc" : c2.DisplayName = "Yard"
        cams.Update(c2, "Cam-P@ss-2")

        Dim jpeg As Byte() = MakeJpeg(1280, 720)
        ConfigureCamera(SeCamera, jpeg)
        ConfigureCamera(DeCamera, jpeg)

        _Svc = New GatewayServices(Options, Clock, users, totp, vault, cams, audit, New SkiaFrameDecoder(Clock), New SkiaJpegTranscoder())
        _Svc.CameraHandlerFactory = Function(conn) New Ptz.Gateway.Cameras.CameraGuardHandler(If(conn.Host = "10.0.0.1", CType(SeCamera, HttpMessageHandler), DeCamera), Clock)

        _App = GatewayHost.Build(Options, _Svc,
                                 Sub(b) b.WebHost.UseTestServer(),
                                 Sub(a)
                                     a.Use(Function(ctx As HttpContext, nxt As RequestDelegate) As Task
                                               Dim ip As String = ctx.Request.Headers("X-Test-Ip").ToString()
                                               If ip <> "" Then ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip)
                                               If ctx.Request.Headers("X-Test-Https").ToString() = "1" Then ctx.Request.Scheme = "https"
                                               Return nxt(ctx)
                                           End Function)
                                 End Sub,
                                 Sub(r) _reg = r)
        _Registry = _reg
        _App.StartAsync().GetAwaiter().GetResult()
        _Client = _App.GetTestClient()
        _Client.DefaultRequestHeaders.Add("X-Test-Ip", "127.0.0.1")
    End Sub

    ''' <summary>رمز TOTP صالح وغير مستهلك للمستخدم (يقدّم الساعة 31 ث لتغيير الخطوة).</summary>
    Public Function NextCode(user As String) As String
        If Not Secrets.ContainsKey(user) Then Return "123456"
        Clock.Advance(31)
        Return Totp.Compute(Secrets(user), Totp.StepOf(New DateTimeOffset(Clock.UtcNow).ToUnixTimeSeconds()))
    End Function

    Public Function Send(method As String, path As String, Optional body As Object = Nothing, Optional token As String = Nothing, Optional ip As String = Nothing,
                         Optional headers As Dictionary(Of String, String) = Nothing, Optional https As Boolean = True) As HttpResponseMessage
        Dim req As New HttpRequestMessage(New HttpMethod(method), path)
        If body IsNot Nothing Then
            Dim json As String = If(TypeOf body Is String, CStr(body), JsonSerializer.Serialize(body, WireJson.Write))
            req.Content = New StringContent(json, Encoding.UTF8, "application/json")
        End If
        If token IsNot Nothing Then req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", token)
        If ip IsNot Nothing Then
            req.Headers.Remove("X-Test-Ip")
            req.Headers.Add("X-Test-Ip", ip)
            If https Then req.Headers.TryAddWithoutValidation("X-Test-Https", "1")
        End If
        If headers IsNot Nothing Then
            For Each kv In headers
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value)
            Next
        End If
        Return Client.SendAsync(req).GetAwaiter().GetResult()
    End Function

    Public Function Login(user As String, Optional password As String = Pw, Optional totp As String = Nothing, Optional ip As String = Nothing) As HttpResponseMessage
        Dim code As String = If(totp, NextCode(user))
        Return Send("POST", "/api/v1/auth/login", New LoginRequest With {.UserName = user, .Password = password, .Totp = code, .Client = "tests"}, ip:=ip)
    End Function

    ''' <summary>دخول ناجح يعيد الرمز.</summary>
    Public Function Token(user As String) As String
        Dim resp As HttpResponseMessage = Login(user)
        Dim text As String = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        If resp.StatusCode <> HttpStatusCode.OK Then Throw New InvalidOperationException("login failed: " & CInt(resp.StatusCode) & " " & text)
        Return JsonSerializer.Deserialize(Of LoginOkDto)(text, WireJson.ClientRead).AccessToken
    End Function

    Public Shared Function Body(resp As HttpResponseMessage) As String
        Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    End Function

    Public Shared Function Read(Of T)(resp As HttpResponseMessage) As T
        Return JsonSerializer.Deserialize(Of T)(Body(resp), WireJson.ClientRead)
    End Function

    Public Shared Function ErrorCode(resp As HttpResponseMessage) As String
        Return Read(Of ErrorEnvelope)(resp).Error.Code
    End Function

    Public Function AuditLines() As String()
        Return IO.File.ReadAllLines(IO.Path.Combine(Dir, "audit.log"))
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        Client.Dispose()
        App.StopAsync().GetAwaiter().GetResult()
        App.DisposeAsync().AsTask().GetAwaiter().GetResult()
    End Sub
End Class
