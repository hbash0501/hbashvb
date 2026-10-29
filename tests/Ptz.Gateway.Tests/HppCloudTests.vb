Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports Ptz.Core.Models
Imports Ptz.Gateway.Cloud
Imports Xunit

''' <summary>عميل Hik-Partner Pro: الرمز، سياسة المسارات، تحويل الردود، المحدِّد، ومعالج الترحيل (على خادم وهمي مطابق للدليل V2.15.500).</summary>
Public Class HppCloudTests

    Private Const Serial As String = "SE7SERIAL"
    Private Const Continuous As String = "ISAPI/PTZCtrl/channels/1/continuous"

    Private NotInheritable Class Rig
        Implements IDisposable
        Public ReadOnly Property Clock As New TestClock()
        Public ReadOnly Property Fake As FakeHppCloud
        Public ReadOnly Property Cloud As HppCloud
        Public ReadOnly Property Http As HttpClient
        Public ReadOnly Property Events As New List(Of HppCallEventArgs)()

        Public Sub New(Optional configure As Action(Of HppOptions) = Nothing, Optional online As Boolean = True)
            Fake = New FakeHppCloud(Clock)
            Dim d As FakeHppCloud.Device = Fake.AddDevice(Serial)
            d.Online = online
            Dim o As New HppOptions With {.Enabled = True}
            If configure IsNot Nothing Then configure(o)
            Cloud = New HppCloud(o, FakeHppCloud.AppKey, FakeHppCloud.SecretKey, Clock, Fake)
            AddHandler Cloud.CallCompleted, Sub(s, e)
                                                SyncLock Events
                                                    Events.Add(e)
                                                End SyncLock
                                            End Sub
            Http = New HttpClient(New HppRelayHandler(Cloud, Serial)) With {.BaseAddress = New Uri("https://hpp-relay.invalid/")}
        End Sub

        Public Function Put(path As String, xml As String) As HttpResponseMessage
            Return Http.PutAsync(path, New StringContent(xml, Encoding.UTF8, "application/xml")).GetAwaiter().GetResult()
        End Function

        Public Function [Get](path As String) As HttpResponseMessage
            Return Http.GetAsync(path).GetAwaiter().GetResult()
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            Http.Dispose()
            Cloud.Dispose()
        End Sub
    
    ' ===================== إصلاحات المراجعة =====================
    <Fact>
    Public Sub TokenResponse_FlatShapeOfSection311_IsAccepted()
        Using r As New Rig()
            r.Fake.TokenShape = "flat"     ' {"accessToken ": ..., "expireTime ": ...} بلا data ولا errorCode
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(5)).StatusCode)
            Assert.Equal("Bearer hpc.tok1", r.Fake.Transparent().Single().Authorization)
        End Using
    End Sub

    Public Shared Iterator Function TokenOutages() As IEnumerable(Of Object())
        Yield {500, "{}"}
        Yield {503, "Service Unavailable"}
        Yield {429, "{""errorCode"":""LAP500007""}"}
        Yield {200, "{""errorCode"":""LAP500001""}"}
        Yield {200, "{""errorCode"":""LAP500007""}"}
    End Function

    <Theory>
    <MemberData(NameOf(TokenOutages))>
    Public Sub TokenServiceOutage_IsTransient_NeverAKeyRejection_SoTheBreakerStaysClosed(status As Integer, body As String)
        Using r As New Rig()
            r.Fake.TokenOverride = Function() New HttpResponseMessage(CType(status, HttpStatusCode)) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
            Assert.Throws(Of HttpRequestException)(Sub() r.Put(Continuous, Move(1)))      ' خطأ نقل (503 للعميل) لا 401
            Assert.DoesNotContain(r.Events, Function(e) e.Outcome = HppOutcome.AuthRejected)
            r.Fake.TokenOverride = Nothing
            r.Clock.Advance(3)
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub StillValidToken_KeepsWorking_WhenItsRefreshFails_UntilItReallyExpires()
        Using r As New Rig()
            r.Put(Continuous, Move(1))
            r.Clock.Advance(6.8 * 86400)        ' التجديد مستحق، والرمز صالح حتى اليوم السابع
            r.Fake.TokenRequestsFail = True
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(0)).StatusCode)     ' إيقاف لا ينتظر تجديداً فاشلاً
            Assert.Equal("Bearer hpc.tok1", r.Fake.Transparent().Last().Authorization)
            GwWait.WaitFor(Function() r.Fake.TokenCalls >= 2)
            Assert.True(r.Fake.TokenCalls >= 2, "التجديد جُرِّب في الخلفية")
            r.Clock.Advance(0.5 * 86400)        ' انتهى فعلاً
            Assert.Throws(Of HttpRequestException)(Sub() r.Put(Continuous, Move(1)))
        End Using
    End Sub

    <Fact>
    Public Sub ConcurrentCallers_ShareOneTokenFetch_AndOneCallersTimeoutDoesNotAbortIt()
        Using r As New Rig()
            r.Fake.TokenDelayMs = 400
            Using cts As New Threading.CancellationTokenSource(50)
                Dim req As New HttpRequestMessage(HttpMethod.Get, "ISAPI/System/deviceInfo")
                Assert.ThrowsAny(Of OperationCanceledException)(Sub() r.Http.SendAsync(req, cts.Token).GetAwaiter().GetResult())
            End Using
            Dim tasks As Task(Of HttpResponseMessage)() = Enumerable.Range(0, 8).Select(Function(i) r.Http.GetAsync("ISAPI/System/deviceInfo")).ToArray()
            Task.WaitAll(tasks)
            Assert.All(tasks, Sub(t) Assert.Equal(HttpStatusCode.OK, t.Result.StatusCode))
            Assert.Equal(1, r.Fake.TokenCalls)      ' الجلب الذي بدأه المستدعي الملغى أكمل لغيره
        End Using
    End Sub

    <Fact>
    Public Sub BareHttp401_FromTheCloud_RefreshesTheTokenOnce_ThenSucceeds()
        Using r As New Rig()
            Dim first As Boolean = True
            r.Fake.TransparentOverride = Function(rec)
                                             If first Then
                                                 first = False
                                                 Return New HttpResponseMessage(HttpStatusCode.Unauthorized)
                                             End If
                                             Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("<ResponseStatus><statusCode>1</statusCode><statusString>OK</statusString></ResponseStatus>", Encoding.UTF8, "application/xml")}
                                         End Function
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
            Assert.Equal(2, r.Fake.TokenCalls)
        End Using
    End Sub

    <Fact>
    Public Sub StopPriority_IsOnePerSecondPerCamera_FurtherStopsGoThroughTheLimiter()
        Using r As New Rig(Sub(o)
                               o.MaxRequestsPerSecond = 0.2
                               o.MaxBurst = 1
                           End Sub)
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)      ' يستهلك الرصيد
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(0)).StatusCode)      ' أول إيقاف: أولوية
            Dim flood As HttpResponseMessage = r.Put(Continuous, Move(0))                ' ثانٍ فوراً: عبر المحدِّد ⇒ يُرفض
            Assert.Equal(HttpStatusCode.ServiceUnavailable, flood.StatusCode)
            Assert.Contains("localRate", Body(flood))
        End Using
    End Sub

    <Fact>
    Public Sub UnconfirmedWrite_FailsClosed_UnlessTheOperatorOptedIn()
        For Each optIn As Boolean In {False, True}
            Using r As New Rig(Sub(o) o.AcceptUnconfirmedWrites = optIn)
                r.Fake.TransparentOverride = Function(rec) New HttpResponseMessage(HttpStatusCode.OK)
                Dim resp As HttpResponseMessage = r.Put(Continuous, Move(0))
                Assert.Equal(If(optIn, HttpStatusCode.OK, HttpStatusCode.BadGateway), resp.StatusCode)
            End Using
        Next
    End Sub

    <Fact>
    Public Sub XmlWithBomAndLeadingWhitespace_IsNormalised_SoTheControllerCanParseIt()
        Using r As New Rig()
            r.Fake.TransparentOverride = Function(rec) New HttpResponseMessage(HttpStatusCode.OK) With {
                .Content = New ByteArrayContent(Encoding.UTF8.GetBytes(ChrW(&HFEFF) & vbLf & "  <DeviceInfo><model>DS-2SE7C432MW-AEB</model><firmwareVersion>V5</firmwareVersion></DeviceInfo>"))}
            Dim conn As New CameraConnection With {.Transport = CameraTransport.HikPartnerPro, .CloudSerial = Serial}
            Using ctl As Ptz.Core.Abstractions.ICameraController = Ptz.Core.Cameras.CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, conn, New HppRelayHandler(r.Cloud, Serial))
                Assert.Equal("DS-2SE7C432MW-AEB", ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult().Model)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub DeviceAndEzoCodes_AreReportedForDiagnostics_ButDoNotDecideSuccess()
        Using r As New Rig()
            r.Fake.TransparentOverride = Function(rec)
                                             Dim m As New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("<ResponseStatus><statusCode>1</statusCode><statusString>OK</statusString></ResponseStatus>", Encoding.UTF8, "application/xml")}
                                             m.Headers.TryAddWithoutValidation("X-DeviceCode", "0x0")
                                             m.Headers.TryAddWithoutValidation("X-EZO-Code", "200")
                                             Return m
                                         End Function
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(0)).StatusCode)
            Assert.Equal("0x0", r.Events.Last().DeviceCode)
            Assert.Equal("200", r.Events.Last().EzoCode)
        End Using
    End Sub

    <Fact>
    Public Sub CallTimeout_EndsBeforeTheCameraTimeout_AndIsReportedAsUnreachable()
        Using r As New Rig()
            r.Get("ISAPI/System/deviceInfo")                 ' الرمز مخبّأ
            r.Fake.DelayMs = 4000
            Using http As New HttpClient(New HppRelayHandler(r.Cloud, Serial, cameraTimeoutSeconds:=3)) With {.BaseAddress = New Uri("https://hpp-relay.invalid/")}
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                Assert.Throws(Of HttpRequestException)(Sub() http.GetAsync("ISAPI/System/deviceInfo").GetAwaiter().GetResult())
                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3.5), sw.Elapsed.ToString())
            End Using
            Assert.Equal(HppOutcome.DeviceUnreachable, r.Events.Last().Outcome)
            Assert.Equal("cloudTimeout", r.Events.Last().Code)
        End Using
    End Sub

    <Fact>
    Public Sub RateLimiter_AfterAPenalty_ResumesGradually_NotWithAFullBurst()
        Dim ticks As Long = 0
        Dim lim As New HppRateLimiter(2.0, 5, Function() ticks, 1000.0)
        lim.Penalize(3)
        ticks += 3000
        Assert.True(lim.Reserve() > TimeSpan.Zero, "لا رصيد متراكم من زمن الحجب")
        ticks += 500
        Assert.Equal(TimeSpan.Zero, lim.Reserve())
        Assert.True(lim.Reserve() > TimeSpan.Zero)
    End Sub

    <Fact>
    Public Sub RussiaRegionDomain_IsAllowedByDefault()
        Using r As New Rig()
            r.Fake.AreaDomain = "https://api.hik-partnerru.com"
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
            Assert.Equal("api.hik-partnerru.com", r.Fake.Transparent().Single().Host)
        End Using
    End Sub
End Class

    Private Shared Function Move(pan As Integer) As String
        Return $"<PTZData version=""2.0"" xmlns=""http://www.hikvision.com/ver20/XMLSchema""><pan>{pan}</pan><tilt>0</tilt><zoom>0</zoom></PTZData>"
    End Function

    Private Shared Function Body(resp As HttpResponseMessage) As String
        Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    End Function

    ' ===================== الرمز =====================
    <Fact>
    Public Sub Token_IsFetchedOnce_FromGlobalDomain_WithKeysInBody_AndReusedForCalls()
        Using r As New Rig()
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(10)).StatusCode)
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(20)).StatusCode)
            Assert.Equal(1, r.Fake.TokenCalls)
            Dim tokenReq = r.Fake.Requests.First()
            Assert.Equal("api.hik-partner.com", tokenReq.Host)
            Assert.Equal("POST", tokenReq.Method)
            Assert.Equal("", tokenReq.Authorization)
            Assert.Contains(FakeHppCloud.AppKey, tokenReq.Body)
            ' كل نداءات الخدمة على areaDomain المُعاد لا على النطاق العام.
            Assert.All(r.Fake.Transparent(), Sub(x) Assert.Equal("isgpapi.hik-partner.com", x.Host))
        End Using
    End Sub

    <Fact>
    Public Sub Token_IsRefreshed_BeforeItExpires()
        Using r As New Rig()
            r.Put(Continuous, Move(1))
            r.Clock.Advance(6.8 * 86400)   ' بقي أقل من 12 ساعة من 7 أيام
            r.Put(Continuous, Move(2))
            Assert.Equal(2, r.Fake.TokenCalls)
            Assert.Equal("Bearer hpc.tok2", r.Fake.Transparent().Last().Authorization)
        End Using
    End Sub

    <Fact>
    Public Sub RejectedToken_IsReplacedOnce_AndTheCommandIsRetried()
        Using r As New Rig()
            r.Put(Continuous, Move(1))
            r.Fake.ExpireCurrentToken()
            Dim resp As HttpResponseMessage = r.Put(Continuous, Move(2))
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode)
            Assert.Equal(2, r.Fake.TokenCalls)
            Assert.Equal({"move:1,0,0", "move:2,0,0"}, r.Fake.MoveLog(Serial))
        End Using
    End Sub

    <Theory>
    <InlineData("json")>
    <InlineData("header")>
    <InlineData("http401")>
    Public Sub RejectedToken_InEveryErrorStyle_IsHandledTheSame(style As String)
        Using r As New Rig()
            r.Fake.ErrorStyle = style
            r.Put(Continuous, Move(1))
            r.Fake.ExpireCurrentToken()
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(2)).StatusCode)
            Assert.Equal(2, r.Fake.TokenCalls)
        End Using
    End Sub

    <Fact>
    Public Sub PersistentTokenRejection_Returns401_WithoutLooping()
        Using r As New Rig()
            r.Fake.ForceCode = "LAP500004"
            Dim resp As HttpResponseMessage = r.Put(Continuous, Move(1))
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode)
            Assert.True(r.Fake.TransparentCalls <= 2, "retry must be bounded to one")
            Assert.True(r.Fake.TokenCalls <= 2)
            Assert.Equal(HppOutcome.AuthRejected, r.Events.Last().Outcome)
        End Using
    End Sub

    <Fact>
    Public Sub WrongKeys_ThrowAuthFailure_WithoutLeakingSecrets_AndAreBackedOff()
        Dim clock As New TestClock()
        Dim fake As New FakeHppCloud(clock)
        fake.AddDevice(Serial)
        Using cloud As New HppCloud(New HppOptions With {.Enabled = True}, "wrong-ak-0001", "wrong-sk-0002", clock, fake)
            Using http As New HttpClient(New HppRelayHandler(cloud, Serial)) With {.BaseAddress = New Uri("https://hpp-relay.invalid/")}
                Dim r1 As HttpResponseMessage = http.GetAsync("ISAPI/System/deviceInfo").GetAwaiter().GetResult()
                Assert.Equal(HttpStatusCode.Unauthorized, r1.StatusCode)
                Dim text As String = Body(r1)
                Assert.DoesNotContain("wrong-ak-0001", text)
                Assert.DoesNotContain("wrong-sk-0002", text)
                ' مكالمة ثانية خلال دقيقة: لا نداء جديد لخدمة الرمز (حجب 60 ث).
                http.GetAsync("ISAPI/System/deviceInfo").GetAwaiter().GetResult()
                Assert.Equal(1, fake.TokenCalls)
                clock.Advance(61)
                http.GetAsync("ISAPI/System/deviceInfo").GetAwaiter().GetResult()
                Assert.Equal(2, fake.TokenCalls)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub CloudOutage_OnTokenFetch_IsTransient_AndRecovers()
        Using r As New Rig()
            r.Fake.TokenRequestsFail = True
            Assert.Throws(Of HttpRequestException)(Sub() r.Put(Continuous, Move(1)))
            r.Fake.TokenRequestsFail = False
            r.Clock.Advance(3)
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
            Assert.Equal(HppOutcome.Ok, r.Events.Last().Outcome)
        End Using
    End Sub

    <Theory>
    <InlineData("https://evil.example.com")>
    <InlineData("http://apiisgp.hik-partner.com")>
    <InlineData("https://hik-partner.com.evil.com")>
    <InlineData("https://evilhik-partner.com")>
    <InlineData("https://user@apiisgp.hik-partner.com")>
    <InlineData("https://apiisgp.hik-partner.com:8443")>
    <InlineData("https://apiisgp.hik-partner.com/x")>
    <InlineData("not a url")>
    Public Sub HostileAreaDomain_IsRejected_AndNeverReceivesTheToken(area As String)
        Using r As New Rig()
            r.Fake.AreaDomain = area
            Dim resp As HttpResponseMessage = r.Put(Continuous, Move(1))
            Assert.Equal(HttpStatusCode.BadGateway, resp.StatusCode)
            Assert.Equal(0, r.Fake.TransparentCalls)
            Assert.All(r.Fake.Requests, Sub(x) Assert.Equal("api.hik-partner.com", x.Host))
        End Using
    End Sub

    <Fact>
    Public Sub NonsenseExpiry_IsRejected_NotCachedForever()
        Using r As New Rig()
            r.Fake.TokenLifetime = TimeSpan.FromSeconds(30)
            Dim resp As HttpResponseMessage = r.Put(Continuous, Move(1))
            Assert.Equal(HttpStatusCode.BadGateway, resp.StatusCode)
            Assert.Equal(0, r.Fake.TransparentCalls)
        End Using
    End Sub

    <Theory>
    <InlineData("https://api.hik-partner.com", True)>
    <InlineData("https://API.HIK-PARTNER.COM/", True)>
    <InlineData("http://api.hik-partner.com", False)>
    <InlineData("https://api.hik-partner.com:444", False)>
    <InlineData("https://api.hik-partner.com/api", False)>
    <InlineData("https://api.hik-partner.com?x=1", False)>
    <InlineData("https://api.other.com", False)>
    Public Sub ServiceUrl_Normalization(url As String, expectOk As Boolean)
        Dim norm As String = HppCloud.NormalizeServiceUrl(url, {"hik-partner.com"})
        If expectOk Then Assert.Equal("https://api.hik-partner.com", norm) Else Assert.Null(norm)
    End Sub

    <Theory>
    <InlineData("com")>
    <InlineData("*.hik-partner.com")>
    <InlineData("")>
    Public Sub UnsafeSuffixes_NeverMatchAnything(suffix As String)
        Assert.False(HppCloud.HostAllowed("api.hik-partner.com", {suffix}))
        Assert.False(HppCloud.HostAllowed("evil.com", {suffix}))
    End Sub

    <Fact>
    Public Sub Constructor_RejectsBadTokenUrl_AndEmptyKeys()
        Dim clock As New TestClock()
        Assert.Throws(Of ArgumentException)(Function() New HppCloud(New HppOptions With {.TokenUrl = "https://token.evil.example"}, "a", "b", clock))
        Assert.Throws(Of ArgumentException)(Function() New HppCloud(New HppOptions(), "", "b", clock))
        Assert.Throws(Of ArgumentException)(Function() New HppCloud(New HppOptions(), "a", " ", clock))
    End Sub

    ' ===================== ترجمة الطلب =====================
    <Fact>
    Public Sub Request_IsTranslated_ToTheTransparentEndpoint_WithGuideHeaders_AndIsapiNamespace()
        Using r As New Rig()
            r.Put(Continuous, Move(40))
            Dim rec = r.Fake.Transparent().Single()
            Assert.Equal("PUT", rec.Method)
            Assert.Equal("/api/hpcgw/v1/device/transparent/ISAPI/PTZCtrl/channels/1/continuous", rec.Path)
            Assert.Equal("Bearer hpc.tok1", rec.Authorization)
            Assert.Equal(Serial, rec.DevSerial)
            Assert.Equal("application/xml", rec.ContentType)
            Assert.Contains("http://www.isapi.org/ver20/XMLSchema", rec.Body)
            Assert.StartsWith("<?xml version=""1.0"" encoding=""UTF-8""?>", rec.Body)
            Assert.Empty(r.Fake.Violations)
            Assert.DoesNotContain("hikvision.com", rec.Body)
            Assert.Contains("<pan>40</pan>", rec.Body)
        End Using
    End Sub

    <Fact>
    Public Sub GetRequest_HasAnEmptyBody_ButStillCarriesTheMandatoryContentType()
        Using r As New Rig()
            Dim resp As HttpResponseMessage = r.Get("ISAPI/System/deviceInfo")
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode)
            Assert.Contains("<model>DS-2SE7C432MW-AEB</model>", Body(resp))
            Dim rec = r.Fake.Transparent().Single()
            Assert.Equal("application/xml", rec.ContentType)    ' الدليل: Content-Type إلزامي (جدول 3-101)
            Assert.Equal("", rec.Body)
            Assert.Empty(r.Fake.Violations)
        End Using
    End Sub

    <Theory>
    <InlineData("GET", "ISAPI/System/reboot")>
    <InlineData("PUT", "ISAPI/System/reboot")>
    <InlineData("PUT", "ISAPI/PTZCtrl/channels/1/momentary")>
    <InlineData("PUT", "ISAPI/PTZCtrl/channels/1/absolute")>
    <InlineData("GET", "ISAPI/PTZCtrl/channels/1/status")>
    <InlineData("GET", "ISAPI/Streaming/channels/101/picture")>
    <InlineData("DELETE", "ISAPI/PTZCtrl/channels/1/presets")>
    <InlineData("POST", "ISAPI/PTZCtrl/channels/1/presets")>
    <InlineData("PUT", "ISAPI/System/Video/inputs/channels/1/focus")>
    <InlineData("GET", "ISAPI/PTZCtrl/channels/1/continuous?format=json")>
    <InlineData("PUT", "ISAPI/PTZCtrl/channels/12345/continuous")>
    <InlineData("PUT", "ISAPI/PTZCtrl/channels/1/presets/1/goto/x")>
    Public Sub UnlistedPaths_AreRefusedLocally_WithoutAnyCloudCall(method As String, path As String)
        Using r As New Rig()
            Dim req As New HttpRequestMessage(New HttpMethod(method), path)
            If method <> "GET" Then req.Content = New StringContent("<x/>", Encoding.UTF8, "application/xml")
            Dim resp As HttpResponseMessage = r.Http.SendAsync(req).GetAwaiter().GetResult()
            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode)
            Assert.Contains("notAllowed", Body(resp))
            Assert.Empty(r.Fake.Requests)   ' حتى الرمز لم يُجلب
        End Using
    End Sub

    <Fact>
    Public Sub PathTricks_AreRefused()
        Using r As New Rig()
            For Each p As String In {"ISAPI/PTZCtrl/channels/1/../../System/reboot", "ISAPI//System/deviceInfo", "ISAPI/PTZCtrl/channels/%31/continuous", "ISAPI/System/deviceInfo%00", "isapi/System/deviceInfo"}
                Dim req As New HttpRequestMessage(HttpMethod.Get, p)
                Dim resp As HttpResponseMessage
                Try
                    resp = r.Http.SendAsync(req).GetAwaiter().GetResult()
                Catch ex As Exception When TypeOf ex Is UriFormatException OrElse TypeOf ex Is InvalidOperationException
                    Continue For
                End Try
                Assert.True(resp.StatusCode = HttpStatusCode.NotFound, p)
            Next
            Assert.Empty(r.Fake.Requests)
        End Using
    End Sub

    <Fact>
    Public Sub OversizedBody_IsRefused_Locally()
        Using r As New Rig()
            Dim resp As HttpResponseMessage = r.Put(Continuous, New String("x"c, 70000))
            Assert.Equal(CType(413, HttpStatusCode), resp.StatusCode)
            Assert.Empty(r.Fake.Requests)
        End Using
    End Sub

    <Fact>
    Public Sub PathPolicy_ExtraRules_AreValidated_AndEnableOnlyWhatTheyName()
        Dim p As New HppPathPolicy({"GET /ISAPI/PTZCtrl/channels/<ID>/status", "put /ISAPI/Foo/Bar?format=json"})
        Assert.True(p.IsAllowed("GET", "/ISAPI/PTZCtrl/channels/2/status", ""))
        Assert.False(p.IsAllowed("PUT", "/ISAPI/PTZCtrl/channels/2/status", ""))
        Assert.True(p.IsAllowed("PUT", "/ISAPI/Foo/Bar", "?format=json"))
        Assert.False(p.IsAllowed("PUT", "/ISAPI/Foo/Bar", "?format=xml"))
        Assert.False(p.IsAllowed("PUT", "/ISAPI/Foo/Bar", ""))
        Assert.Throws(Of ArgumentException)(Function() New HppPathPolicy({"PUT /ISAPI/../x"}))
        Assert.Throws(Of ArgumentException)(Function() New HppPathPolicy({"FETCH /ISAPI/x"}))
        Assert.Throws(Of ArgumentException)(Function() New HppPathPolicy({"GET /other/x"}))
        Assert.False(HppPathPolicy.IsValidRule("GET /ISAPI/x y"))
        Assert.False(HppPathPolicy.IsValidRule("GET /ISAPI/%2e"))
        Assert.True(HppPathPolicy.IsValidRule("GET /ISAPI/System/time"))
    End Sub

    <Fact>
    Public Sub PathPolicy_Documented_AllowsExactlyWhatThePlatformUses()
        Dim p As New HppPathPolicy(Nothing)
        Assert.True(p.IsAllowed("GET", "/ISAPI/System/deviceInfo", ""))
        Assert.True(p.IsAllowed("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", ""))
        Assert.True(p.IsAllowed("GET", "/ISAPI/PTZCtrl/channels/1/presets", ""))
        Assert.True(p.IsAllowed("PUT", "/ISAPI/PTZCtrl/channels/1/presets/7", ""))
        Assert.True(p.IsAllowed("DELETE", "/ISAPI/PTZCtrl/channels/1/presets/7", ""))
        Assert.True(p.IsAllowed("PUT", "/ISAPI/PTZCtrl/channels/1/presets/7/goto", ""))
        Assert.False(p.IsAllowed("DELETE", "/ISAPI/PTZCtrl/channels/1/presets", ""))
        Assert.False(p.IsAllowed("PUT", "/ISAPI/PTZCtrl/channels/1/presets/x/goto", ""))
    End Sub

    ' ===================== تحويل الردود =====================
    <Theory>
    <InlineData("EVZ20007", 503, HppOutcome.DeviceUnreachable)>
    <InlineData("EVZ20008", 503, HppOutcome.DeviceUnreachable)>
    <InlineData("EVZ10029", 503, HppOutcome.RateLimited)>
    <InlineData("LAP500007", 503, HppOutcome.RateLimited)>
    <InlineData("EVZ10013", 401, HppOutcome.AuthRejected)>
    <InlineData("EVZ60042", 401, HppOutcome.AuthRejected)>
    <InlineData("EVZ60020", 404, HppOutcome.Rejected)>
    <InlineData("EVZ20015", 404, HppOutcome.Rejected)>
    <InlineData("EVZ20002", 403, HppOutcome.Rejected)>
    <InlineData("EVZ20018", 403, HppOutcome.Rejected)>
    <InlineData("EVZ10001", 400, HppOutcome.Rejected)>
    <InlineData("SOMETHING9", 502, HppOutcome.Rejected)>
    Public Sub Mapper_KnownCodes_FromHeaderOrJsonBody(code As String, status As Integer, outcome As HppOutcome)
        Dim fromHeader As HppMapped = HppResponseMapper.Map(True, 200, code, "application/json", Array.Empty(Of Byte)())
        Dim fromJson As HppMapped = HppResponseMapper.Map(True, 200, Nothing, "application/json", Encoding.UTF8.GetBytes("{""errorCode"":""" & code & """}"))
        For Each m As HppMapped In {fromHeader, fromJson}
            Assert.Equal(status, m.Status)
            Assert.Equal(outcome, m.Outcome)
            Assert.Equal(code, m.Code)
            Assert.Contains("<subStatusCode>" & code & "</subStatusCode>", Encoding.UTF8.GetString(m.Body))
        Next
    End Sub

    <Fact>
    Public Sub Mapper_SuccessShapes()
        Dim xml As Byte() = Encoding.UTF8.GetBytes("<DeviceInfo/>")
        Assert.Equal(HppOutcome.Ok, HppResponseMapper.Map(False, 200, Nothing, "application/xml", xml).Outcome)
        Assert.Equal(HppOutcome.Ok, HppResponseMapper.Map(False, 200, "0", "application/xml", xml).Outcome)
        ' الكتابة: لا تُعدّ ناجحة إلا بـ ResponseStatus (الدليل)؛ الفارغ/JSON/XML آخر = غير مؤكَّد ⇒ فشل آمن.
        Dim okStatus As Byte() = Encoding.UTF8.GetBytes("<ResponseStatus><statusCode>1</statusCode><statusString>OK</statusString></ResponseStatus>")
        Assert.Equal(HppOutcome.Ok, HppResponseMapper.Map(True, 200, Nothing, "application/xml", okStatus).Outcome)
        For Each unconfirmed As Byte() In {Array.Empty(Of Byte)(), Encoding.UTF8.GetBytes("{""errorCode"":""0""}"), Encoding.UTF8.GetBytes("{""errorCode"":null}"),
                                           Encoding.UTF8.GetBytes("{""statusCode"":4,""statusString"":""Invalid Operation""}"), Encoding.UTF8.GetBytes("<DeviceInfo/>")}
            Dim m As HppMapped = HppResponseMapper.Map(True, 200, Nothing, Nothing, unconfirmed)
            Assert.Equal(502, m.Status)
            Assert.Equal("unconfirmed", m.Code)
            ' مخرج يدوي موثَّق: يقبلها فقط إن فعّله المشغّل بعد تحقق ميداني.
            Assert.Equal(HppOutcome.Ok, HppResponseMapper.Map(True, 200, Nothing, Nothing, unconfirmed, acceptUnconfirmed:=True).Outcome)
        Next
        ' القراءة بلا XML فشل (لا نسلّم JSON/HTML لمحلل XML).
        Assert.Equal(502, HppResponseMapper.Map(False, 200, Nothing, "application/json", Encoding.UTF8.GetBytes("{""errorCode"":""0""}")).Status)
        Assert.Equal(502, HppResponseMapper.Map(True, 200, Nothing, "text/html", Encoding.UTF8.GetBytes("Bad Gateway")).Status)
        ' XML مكسور أو صفحة HTML من وكيل/بوابة لا تُسلَّم لمحلل ISAPI.
        Assert.Equal(502, HppResponseMapper.Map(False, 200, Nothing, "text/html", Encoding.UTF8.GetBytes("<html><body>Bad Gateway</body></html>")).Status)
        Assert.Equal(502, HppResponseMapper.Map(False, 200, Nothing, "application/xml", Encoding.UTF8.GetBytes("<DeviceInfo><x>")).Status)
        Assert.Equal(502, HppResponseMapper.Map(False, 200, Nothing, "text/html", Encoding.UTF8.GetBytes("<!DOCTYPE html><html></html>")).Status)
        Assert.Equal(503, HppResponseMapper.Map(True, 502, Nothing, "text/html", Encoding.UTF8.GetBytes("<html><body>Bad Gateway</body></html>")).Status)
    End Sub

    <Theory>
    <InlineData(401, 401, HppOutcome.AuthRejected)>
    <InlineData(403, 403, HppOutcome.Rejected)>
    <InlineData(404, 404, HppOutcome.Rejected)>
    <InlineData(408, 503, HppOutcome.DeviceUnreachable)>
    <InlineData(429, 503, HppOutcome.RateLimited)>
    <InlineData(500, 503, HppOutcome.DeviceUnreachable)>
    <InlineData(502, 503, HppOutcome.DeviceUnreachable)>
    <InlineData(504, 503, HppOutcome.DeviceUnreachable)>
    <InlineData(400, 502, HppOutcome.Rejected)>
    Public Sub Mapper_HttpStatusWithoutKnownCode(http As Integer, expected As Integer, outcome As HppOutcome)
        Dim m As HppMapped = HppResponseMapper.Map(True, http, Nothing, Nothing, Array.Empty(Of Byte)())
        Assert.Equal(expected, m.Status)
        Assert.Equal(outcome, m.Outcome)
    End Sub

    <Fact>
    Public Sub Mapper_DeviceXmlFailure_PassesThroughWithItsOwnStatus()
        Dim xml As Byte() = Encoding.UTF8.GetBytes("<ResponseStatus><statusCode>4</statusCode><statusString>Invalid Operation</statusString><subStatusCode>notSupport</subStatusCode></ResponseStatus>")
        Dim m As HppMapped = HppResponseMapper.Map(True, 400, Nothing, "application/xml", xml)
        Assert.Equal(400, m.Status)
        Assert.Equal(xml, m.Body)
    End Sub

    <Fact>
    Public Sub Mapper_ErrorCode_IsSanitized_NoFreeTextFromTheServer()
        Dim evil As String = "EVZ1<script>alert(1)</script>" & New String("A"c, 100)
        Dim m As HppMapped = HppResponseMapper.Map(True, 200, evil, Nothing, Array.Empty(Of Byte)())
        Dim text As String = Encoding.UTF8.GetString(m.Body)
        Assert.DoesNotContain("<script>", text)
        Assert.True(m.Code.Length <= 24)
    End Sub

    ' ===================== ردود الجهاز عبر المعالج =====================
    <Fact>
    Public Sub OfflineDevice_Maps503_AndReportsDeviceUnreachable()
        Using r As New Rig(online:=False)
            Dim resp As HttpResponseMessage = r.Put(Continuous, Move(5))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode)
            Assert.Contains("EVZ20007", Body(resp))
            Assert.Equal(HppOutcome.DeviceUnreachable, r.Events.Last().Outcome)
            Assert.Equal(Serial, r.Events.Last().Serial)
        End Using
    End Sub

    <Fact>
    Public Sub UnknownSerial_IsForbidden_NotRetried()
        Dim clock As New TestClock()
        Dim fake As New FakeHppCloud(clock)
        Using cloud As New HppCloud(New HppOptions(), FakeHppCloud.AppKey, FakeHppCloud.SecretKey, clock, fake)
            Using http As New HttpClient(New HppRelayHandler(cloud, "NOSUCH")) With {.BaseAddress = New Uri("https://hpp-relay.invalid/")}
                Dim resp As HttpResponseMessage = http.GetAsync("ISAPI/System/deviceInfo").GetAwaiter().GetResult()
                Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode)
                Assert.Equal(1, fake.TransparentCalls)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub RateLimitedReply_PausesNonStopRequests_ButNeverStops()
        Using r As New Rig(Sub(o) o.MaxRequestsPerSecond = 50)
            r.Fake.ForceCode = "EVZ10029"
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.Put(Continuous, Move(1)).StatusCode)
            Assert.Equal(HppOutcome.RateLimited, r.Events.Last().Outcome)
            r.Fake.ForceCode = Nothing
            ' الإيقاف يتجاوز التوقف المؤقت ويصل فوراً.
            Dim stop_ As HttpResponseMessage = r.Put(Continuous, Move(0))
            Assert.Equal(HttpStatusCode.OK, stop_.StatusCode)
            Assert.Equal("stop", r.Fake.MoveLog(Serial).Last())
        End Using
    End Sub

    <Fact>
    Public Sub LocalLimiter_FailsFastInsteadOfQueueing_ButStopAlwaysPasses()
        Using r As New Rig(Sub(o)
                               o.MaxRequestsPerSecond = 0.2    ' الانتظار 5 ث > سقف الطابور (2 ث) ⇒ فشل فوري
                               o.MaxBurst = 1
                           End Sub)
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
            Dim second As HttpResponseMessage = r.Put(Continuous, Move(2))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode)
            Assert.Contains("localRate", Body(second))
            Assert.Equal(HppOutcome.RateLimited, r.Events.Last().Outcome)
            Dim stop_ As HttpResponseMessage = r.Put(Continuous, Move(0))
            Assert.Equal(HttpStatusCode.OK, stop_.StatusCode)
        End Using
    End Sub

    ' ===================== أدوات صغيرة =====================
    <Theory>
    <InlineData("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", "<PTZData><pan>0</pan><tilt>0</tilt><zoom>0</zoom></PTZData>", True)>
    <InlineData("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", "<PTZData><pan>1</pan><tilt>0</tilt><zoom>0</zoom></PTZData>", False)>
    <InlineData("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", "<PTZData><pan>0</pan><tilt>0</tilt></PTZData>", False)>
    <InlineData("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", "garbage", False)>
    <InlineData("GET", "/ISAPI/PTZCtrl/channels/1/continuous", "<PTZData><pan>0</pan><tilt>0</tilt><zoom>0</zoom></PTZData>", False)>
    <InlineData("PUT", "/ISAPI/PTZCtrl/channels/1/presets/1", "<PTZData><pan>0</pan><tilt>0</tilt><zoom>0</zoom></PTZData>", False)>
    Public Sub StopDetection(method As String, path As String, xml As String, expected As Boolean)
        Assert.Equal(expected, HppRelayHandler.IsStopCommand(method, path, Encoding.UTF8.GetBytes(xml)))
    End Sub

    <Fact>
    Public Sub RateLimiter_Burst_Wait_Penalty_AndMaxWait()
        Dim ticks As Long = 0
        Dim lim As New HppRateLimiter(2.0, 3, Function() ticks, 1000.0)   ' 1000 tick = ثانية
        Assert.Equal(TimeSpan.Zero, lim.Reserve())
        Assert.Equal(TimeSpan.Zero, lim.Reserve())
        Assert.Equal(TimeSpan.Zero, lim.Reserve())
        Dim wait As TimeSpan = lim.Reserve()
        Assert.True(wait > TimeSpan.Zero AndAlso wait <= TimeSpan.FromSeconds(0.5))
        ticks += 500          ' نصف ثانية ⇒ رمز واحد
        Assert.Equal(TimeSpan.Zero, lim.Reserve())
        lim.Penalize(3)
        Assert.True(lim.Reserve() >= TimeSpan.FromSeconds(2.9))
        Assert.False(lim.WaitAsync(TimeSpan.FromSeconds(1), Threading.CancellationToken.None).GetAwaiter().GetResult())
        ticks += 3500         ' انتهى الحجب + نصف ثانية ⇒ رمز واحد (لا دفعة كاملة؛ انظر الاختبار التالي)
        Assert.True(lim.WaitAsync(TimeSpan.FromSeconds(1), Threading.CancellationToken.None).GetAwaiter().GetResult())
    End Sub

    <Fact>
    Public Sub ListDevices_ReturnsRows_AndRefreshesRejectedToken()
        Using r As New Rig()
            r.Fake.AddDevice("OTHER123", "DS-2DE7232IW-AE").Online = False
            Dim list As IReadOnlyList(Of HppDevice) = r.Cloud.ListDevicesAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            Assert.Equal({"OTHER123", Serial}, list.Select(Function(d) d.Serial).OrderBy(Function(x) x).ToArray())
            Assert.Equal(0, list.Single(Function(d) d.Serial = "OTHER123").OnlineStatus)
            r.Fake.ExpireCurrentToken()
            Assert.Equal(2, r.Cloud.ListDevicesAsync(Threading.CancellationToken.None).GetAwaiter().GetResult().Count)
            Assert.Equal(2, r.Fake.TokenCalls)
        End Using
    End Sub

    <Fact>
    Public Sub CameraConnection_Relay_ValidatesSerialOnly()
        Dim c As New CameraConnection With {.Transport = CameraTransport.HikPartnerPro, .CloudSerial = "ABC-123_x"}
        c.Validate()
        Assert.Equal("hpp://ABC-123_x", c.ToString())
        For Each bad As String In {"", "a b", "a/b", "a" & vbLf & "b", New String("a"c, 65), "ü"}
            Dim x As New CameraConnection With {.Transport = CameraTransport.HikPartnerPro, .CloudSerial = bad}
            Assert.Throws(Of ArgumentException)(Sub() x.Validate())
        Next
        Assert.Throws(Of InvalidOperationException)(Sub() Ptz.Core.Isapi.IsapiClient.CreateSecureHandler(c))
        Assert.Throws(Of InvalidOperationException)(Function() New Ptz.Core.Isapi.IsapiClient(c))
    End Sub

    ' ===================== إصلاحات المراجعة =====================
    <Fact>
    Public Sub TokenResponse_FlatShapeOfSection311_IsAccepted()
        Using r As New Rig()
            r.Fake.TokenShape = "flat"     ' {"accessToken ": ..., "expireTime ": ...} بلا data ولا errorCode
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(5)).StatusCode)
            Assert.Equal("Bearer hpc.tok1", r.Fake.Transparent().Single().Authorization)
        End Using
    End Sub

    Public Shared Iterator Function TokenOutages() As IEnumerable(Of Object())
        Yield {500, "{}"}
        Yield {503, "Service Unavailable"}
        Yield {429, "{""errorCode"":""LAP500007""}"}
        Yield {200, "{""errorCode"":""LAP500001""}"}
        Yield {200, "{""errorCode"":""LAP500007""}"}
    End Function

    <Theory>
    <MemberData(NameOf(TokenOutages))>
    Public Sub TokenServiceOutage_IsTransient_NeverAKeyRejection_SoTheBreakerStaysClosed(status As Integer, body As String)
        Using r As New Rig()
            r.Fake.TokenOverride = Function() New HttpResponseMessage(CType(status, HttpStatusCode)) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
            Assert.Throws(Of HttpRequestException)(Sub() r.Put(Continuous, Move(1)))      ' خطأ نقل (503 للعميل) لا 401
            Assert.DoesNotContain(r.Events, Function(e) e.Outcome = HppOutcome.AuthRejected)
            r.Fake.TokenOverride = Nothing
            r.Clock.Advance(3)
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub StillValidToken_KeepsWorking_WhenItsRefreshFails_UntilItReallyExpires()
        Using r As New Rig()
            r.Put(Continuous, Move(1))
            r.Clock.Advance(6.8 * 86400)        ' التجديد مستحق، والرمز صالح حتى اليوم السابع
            r.Fake.TokenRequestsFail = True
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(0)).StatusCode)     ' إيقاف لا ينتظر تجديداً فاشلاً
            Assert.Equal("Bearer hpc.tok1", r.Fake.Transparent().Last().Authorization)
            GwWait.WaitFor(Function() r.Fake.TokenCalls >= 2)
            Assert.True(r.Fake.TokenCalls >= 2, "التجديد جُرِّب في الخلفية")
            r.Clock.Advance(0.5 * 86400)        ' انتهى فعلاً
            Assert.Throws(Of HttpRequestException)(Sub() r.Put(Continuous, Move(1)))
        End Using
    End Sub

    <Fact>
    Public Sub ConcurrentCallers_ShareOneTokenFetch_AndOneCallersTimeoutDoesNotAbortIt()
        Using r As New Rig()
            r.Fake.TokenDelayMs = 400
            Using cts As New Threading.CancellationTokenSource(50)
                Dim req As New HttpRequestMessage(HttpMethod.Get, "ISAPI/System/deviceInfo")
                Assert.ThrowsAny(Of OperationCanceledException)(Sub() r.Http.SendAsync(req, cts.Token).GetAwaiter().GetResult())
            End Using
            Dim tasks As Task(Of HttpResponseMessage)() = Enumerable.Range(0, 8).Select(Function(i) r.Http.GetAsync("ISAPI/System/deviceInfo")).ToArray()
            Task.WaitAll(tasks)
            Assert.All(tasks, Sub(t) Assert.Equal(HttpStatusCode.OK, t.Result.StatusCode))
            Assert.Equal(1, r.Fake.TokenCalls)      ' الجلب الذي بدأه المستدعي الملغى أكمل لغيره
        End Using
    End Sub

    <Fact>
    Public Sub BareHttp401_FromTheCloud_RefreshesTheTokenOnce_ThenSucceeds()
        Using r As New Rig()
            Dim first As Boolean = True
            r.Fake.TransparentOverride = Function(rec)
                                             If first Then
                                                 first = False
                                                 Return New HttpResponseMessage(HttpStatusCode.Unauthorized)
                                             End If
                                             Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("<ResponseStatus><statusCode>1</statusCode><statusString>OK</statusString></ResponseStatus>", Encoding.UTF8, "application/xml")}
                                         End Function
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
            Assert.Equal(2, r.Fake.TokenCalls)
        End Using
    End Sub

    <Fact>
    Public Sub StopPriority_IsOnePerSecondPerCamera_FurtherStopsGoThroughTheLimiter()
        Using r As New Rig(Sub(o)
                               o.MaxRequestsPerSecond = 0.2
                               o.MaxBurst = 1
                           End Sub)
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)      ' يستهلك الرصيد
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(0)).StatusCode)      ' أول إيقاف: أولوية
            Dim flood As HttpResponseMessage = r.Put(Continuous, Move(0))                ' ثانٍ فوراً: عبر المحدِّد ⇒ يُرفض
            Assert.Equal(HttpStatusCode.ServiceUnavailable, flood.StatusCode)
            Assert.Contains("localRate", Body(flood))
        End Using
    End Sub

    <Fact>
    Public Sub UnconfirmedWrite_FailsClosed_UnlessTheOperatorOptedIn()
        For Each optIn As Boolean In {False, True}
            Using r As New Rig(Sub(o) o.AcceptUnconfirmedWrites = optIn)
                r.Fake.TransparentOverride = Function(rec) New HttpResponseMessage(HttpStatusCode.OK)
                Dim resp As HttpResponseMessage = r.Put(Continuous, Move(0))
                Assert.Equal(If(optIn, HttpStatusCode.OK, HttpStatusCode.BadGateway), resp.StatusCode)
            End Using
        Next
    End Sub

    <Fact>
    Public Sub XmlWithBomAndLeadingWhitespace_IsNormalised_SoTheControllerCanParseIt()
        Using r As New Rig()
            r.Fake.TransparentOverride = Function(rec) New HttpResponseMessage(HttpStatusCode.OK) With {
                .Content = New ByteArrayContent(Encoding.UTF8.GetBytes(ChrW(&HFEFF) & vbLf & "  <DeviceInfo><model>DS-2SE7C432MW-AEB</model><firmwareVersion>V5</firmwareVersion></DeviceInfo>"))}
            Dim conn As New CameraConnection With {.Transport = CameraTransport.HikPartnerPro, .CloudSerial = Serial}
            Using ctl As Ptz.Core.Abstractions.ICameraController = Ptz.Core.Cameras.CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb, conn, New HppRelayHandler(r.Cloud, Serial))
                Assert.Equal("DS-2SE7C432MW-AEB", ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult().Model)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub DeviceAndEzoCodes_AreReportedForDiagnostics_ButDoNotDecideSuccess()
        Using r As New Rig()
            r.Fake.TransparentOverride = Function(rec)
                                             Dim m As New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("<ResponseStatus><statusCode>1</statusCode><statusString>OK</statusString></ResponseStatus>", Encoding.UTF8, "application/xml")}
                                             m.Headers.TryAddWithoutValidation("X-DeviceCode", "0x0")
                                             m.Headers.TryAddWithoutValidation("X-EZO-Code", "200")
                                             Return m
                                         End Function
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(0)).StatusCode)
            Assert.Equal("0x0", r.Events.Last().DeviceCode)
            Assert.Equal("200", r.Events.Last().EzoCode)
        End Using
    End Sub

    <Fact>
    Public Sub CallTimeout_EndsBeforeTheCameraTimeout_AndIsReportedAsUnreachable()
        Using r As New Rig()
            r.Get("ISAPI/System/deviceInfo")                 ' الرمز مخبّأ
            r.Fake.DelayMs = 4000
            Using http As New HttpClient(New HppRelayHandler(r.Cloud, Serial, cameraTimeoutSeconds:=3)) With {.BaseAddress = New Uri("https://hpp-relay.invalid/")}
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                Assert.Throws(Of HttpRequestException)(Sub() http.GetAsync("ISAPI/System/deviceInfo").GetAwaiter().GetResult())
                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3.5), sw.Elapsed.ToString())
            End Using
            Assert.Equal(HppOutcome.DeviceUnreachable, r.Events.Last().Outcome)
            Assert.Equal("cloudTimeout", r.Events.Last().Code)
        End Using
    End Sub

    <Fact>
    Public Sub RateLimiter_AfterAPenalty_ResumesGradually_NotWithAFullBurst()
        Dim ticks As Long = 0
        Dim lim As New HppRateLimiter(2.0, 5, Function() ticks, 1000.0)
        lim.Penalize(3)
        ticks += 3000
        Assert.True(lim.Reserve() > TimeSpan.Zero, "لا رصيد متراكم من زمن الحجب")
        ticks += 500
        Assert.Equal(TimeSpan.Zero, lim.Reserve())
        Assert.True(lim.Reserve() > TimeSpan.Zero)
    End Sub

    <Fact>
    Public Sub RussiaRegionDomain_IsAllowedByDefault()
        Using r As New Rig()
            r.Fake.AreaDomain = "https://api.hik-partnerru.com"
            Assert.Equal(HttpStatusCode.OK, r.Put(Continuous, Move(1)).StatusCode)
            Assert.Equal("api.hik-partnerru.com", r.Fake.Transparent().Single().Host)
        End Using
    End Sub
End Class
