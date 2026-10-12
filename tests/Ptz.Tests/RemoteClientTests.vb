Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Xunit

''' <summary>اختبارات وحدة لطبقة العميل بلا شبكة: قارئ SSE، اختيار الجودة، ترجمة الأخطاء، التحقق المعزَّز، الترتيب، إعادة الاتصال.</summary>
Public Class RemoteClientTests

    ' ======================= SSE reader =======================
    Private Shared Function Reader(text As String, Optional chunk As Integer = 0) As SseFrameReader
        Dim bytes As Byte() = Encoding.UTF8.GetBytes(text)
        Return New SseFrameReader(If(chunk > 0, CType(New ChunkedStream(bytes, chunk), IO.Stream), New IO.MemoryStream(bytes)))
    End Function

    Private NotInheritable Class ChunkedStream
        Inherits IO.Stream
        Private ReadOnly _data As Byte()
        Private ReadOnly _chunk As Integer
        Private _pos As Integer
        Public Sub New(data As Byte(), chunk As Integer)
            _data = data
            _chunk = chunk
        End Sub
        Public Overrides ReadOnly Property CanRead As Boolean = True
        Public Overrides ReadOnly Property CanSeek As Boolean = False
        Public Overrides ReadOnly Property CanWrite As Boolean = False
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException()
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property
        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Dim n As Integer = Math.Min(Math.Min(count, _chunk), _data.Length - _pos)
            Array.Copy(_data, _pos, buffer, offset, n)
            _pos += n
            Return n
        End Function
        Public Overrides Sub Flush()
        End Sub
        Public Overrides Function Seek(offset As Long, origin As IO.SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
    End Class

    Private Shared Function ReadAll(r As SseFrameReader) As List(Of SseFrame)
        Dim list As New List(Of SseFrame)()
        While True
            Dim f As SseFrame = r.ReadFrameAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            If f Is Nothing Then Exit While
            list.Add(f)
        End While
        Return list
    End Function

    <Fact>
    Public Sub Sse_ParsesEventsIdsMultilineDataAndComments_IgnoringLineEndingStyle()
        Dim text As String = ": hb" & vbLf & vbLf &
                             "event: alarm" & vbLf & "id: 42" & vbLf & "data: {""a"":1}" & vbLf & vbLf &
                             "event: multi" & vbCr & "data: one" & vbCr & "data: two" & vbCr & vbCr &
                             "event: crlf" & vbCrLf & "data: x" & vbCrLf & vbCrLf &
                             "data: bare" & vbLf & vbLf
        For Each chunk As Integer In {0, 1, 3, 7}
            Dim frames As List(Of SseFrame) = ReadAll(Reader(text, chunk))
            Assert.Equal(5, frames.Count)
            Assert.True(frames(0).IsComment)
            Assert.Equal("alarm", frames(1).EventName)
            Assert.Equal("42", frames(1).Id)
            Assert.Equal("{""a"":1}", frames(1).Data)
            Assert.Equal("one" & vbLf & "two", frames(2).Data)
            Assert.Equal("crlf", frames(3).EventName)
            Assert.Equal("message", frames(4).EventName)
            Assert.Equal("bare", frames(4).Data)
        Next
    End Sub

    <Fact>
    Public Sub Sse_DropsEventsWithoutData_AndIncompleteTail_AndRejectsOversizedInput()
        Assert.Empty(ReadAll(Reader("event: x" & vbLf & vbLf & "id: 5" & vbLf & vbLf)))
        Assert.Empty(ReadAll(Reader("event: alarm" & vbLf & "data: partial")))             ' لم يُغلق الحدث
        Dim huge As String = "data: " & New String("x"c, 300000) & vbLf & vbLf
        Assert.Throws(Of IO.InvalidDataException)(Function() ReadAll(Reader(huge)))
        Dim many As String = String.Concat(Enumerable.Repeat("data: " & New String("y"c, 1000) & vbLf, 600)) & vbLf
        Assert.Throws(Of IO.InvalidDataException)(Function() ReadAll(Reader(many)))
        Assert.Equal("é", ReadAll(Reader("data: é" & vbLf & vbLf, 1)).Single().Data)           ' UTF-8 متعدد البايت عبر حدود القراءة
    End Sub

    ' ======================= الجودة =======================
    Private Shared Sub Feed(sel As TierSelector, link As LinkEstimator, bytes As Integer, bodyMs As Double, count As Integer)
        For i As Integer = 1 To count
            link.RecordFrame(bytes, bodyMs)
            sel.OnFrame(sel.Current, bytes, link)
        Next
    End Sub

    <Fact>
    Public Sub Link_IgnoresTinySamples_AndSmoothsThroughput()
        Dim l As New LinkEstimator()
        l.RecordFrame(1000, 1)
        Assert.Equal(0, l.Samples)
        l.RecordFrame(100000, 100)             ' 1 MB/s
        Assert.Equal(1_000_000.0, l.ThroughputBytesPerSecond, 0)
        l.RecordFrame(100000, 50)              ' 2 MB/s ⇒ متوسط أُسّي (0.3)
        Assert.Equal(1_300_000.0, l.ThroughputBytesPerSecond, 0)
        l.RecordFailure()
        l.RecordFailure()
        Assert.Equal(2, l.ConsecutiveFailures)
        l.RecordFrame(100000, 50)
        Assert.Equal(0, l.ConsecutiveFailures)
    End Sub

    <Fact>
    Public Sub Tier_StepsUpOneAtATimeAfterGoodFrames_AndDropsImmediatelyWhenTheLinkDegrades()
        Dim ids As String() = {"tiny", "low", "mid", "high", "hd"}
        Dim sel As New TierSelector(ids, "low")
        Dim fast As New LinkEstimator()
        Feed(sel, fast, 200000, 20, TierSelector.DefaultUpgradeAfterGoodFrames - 1)
        Assert.Equal("low", sel.Current)                         ' لم يكتمل عدد الإطارات الجيدة بعد
        Feed(sel, fast, 200000, 20, 1)
        Assert.Equal("mid", sel.Current)                         ' خطوة واحدة فقط
        Feed(sel, fast, 200000, 20, TierSelector.DefaultUpgradeAfterGoodFrames * 4)
        Assert.Equal("hd", sel.Current)
        ' تدهور الوصلة: 60 KB/s ⇒ نزول فوري لأعلى طبقة مستدامة
        Dim slow As New LinkEstimator()
        slow.RecordFrame(60000, 1000)
        sel.OnFrame("hd", 210000, slow)
        Assert.True(FrameLadder.IndexOf(sel.Current) < FrameLadder.IndexOf("mid"), sel.Current)
        sel.OnFailure()
        Assert.Equal("tiny", sel.Current)
        sel.OnFailure()
        Assert.Equal("tiny", sel.Current)                        ' لا ينزل تحت الأدنى
    End Sub

    <Fact>
    Public Sub Tier_RespectsServerAndUserLimits_AndManualOverride()
        Dim sel As New TierSelector({"tiny", "low", "mid"}, "hd")
        Assert.Equal("mid", sel.Current)                         ' الخادم لا يقدّم أعلى
        sel.SetAvailable({"tiny", "low"})
        Assert.Equal("low", sel.Current)
        Dim capped As New TierSelector({"tiny", "low", "mid", "high", "hd"}, "hd", userMaxId:="mid")
        Assert.Equal("mid", capped.Current)
        Dim link As New LinkEstimator()
        link.RecordFrame(500000, 10)
        Feed(capped, link, 500000, 10, 30)
        Assert.Equal("mid", capped.Current)                      ' سرعة الوصلة لا تتجاوز حدّ المستخدم
        capped.LimitTo("low")
        Assert.Equal("low", capped.Current)                      ' الخادم قصّ الطلب
        capped.SetManual("tiny")
        Assert.Equal("tiny", capped.Current)
        Assert.False(capped.IsAuto)
        capped.SetManual("hd")
        Assert.Equal("low", capped.Current)                      ' التثبيت اليدوي مقصوص بالحدود
        capped.SetManual(Nothing)
        Assert.True(capped.IsAuto)
        Assert.Throws(Of ArgumentException)(Sub() capped.SetManual("ultra"))
    End Sub

    <Fact>
    Public Sub Tier_LearnsActualFrameSizes_SoItDoesNotOverpromise()
        Dim sel As New TierSelector({"tiny", "low", "mid", "high", "hd"}, "mid")
        Dim link As New LinkEstimator()
        link.RecordFrame(300000, 1000)                           ' 300 KB/s
        ' بالقيم الافتراضية: mid يحتاج 38000×4×1.6 = 243 KB/s فيصلح؛ لكن الحجم الفعلي أكبر بكثير ⇒ يتعلّم وينزل
        sel.OnFrame("mid", 150000, link)
        sel.OnFrame("mid", 150000, link)
        sel.OnFrame("mid", 150000, link)
        Assert.True(FrameLadder.IndexOf(sel.Current) < FrameLadder.IndexOf("mid"), sel.Current)
    End Sub

    ' ======================= ترجمة الأخطاء =======================
    Private Shared Function Err(status As Integer, code As String, Optional required As String = Nothing, Optional retry As Integer? = Nothing, Optional isapi As Integer? = Nothing) As GatewayApiException
        Return New GatewayApiException(status, New ErrorBody With {.Code = code, .Required = required, .RetryAfterSeconds = retry, .IsapiStatus = isapi})
    End Function

    <Fact>
    Public Sub ErrorTranslation_MapsToTheSameExceptionTypesAsLocalMode()
        Dim denied = Assert.IsType(Of AccessDeniedException)(Err(403, "forbidden", "ptzControl").ToDomainException())
        Assert.Equal(Permission.PtzControl, denied.Required)
        Assert.False(denied.IsSessionExpired)
        Assert.True(Assert.IsType(Of AccessDeniedException)(Err(401, "session_expired").ToDomainException()).IsSessionExpired)
        Assert.False(Assert.IsType(Of AccessDeniedException)(Err(401, "invalid_credentials").ToDomainException()).IsSessionExpired)
        Assert.IsType(Of ArgumentException)(Err(400, "bad_request").ToDomainException())
        Assert.IsType(Of InvalidOperationException)(Err(404, "not_found").ToDomainException())
        Assert.IsType(Of InvalidOperationException)(Err(409, "conflict").ToDomainException())
        Assert.IsType(Of InvalidOperationException)(Err(412, "precondition_failed").ToDomainException())
        Assert.Contains("30", Err(429, "rate_limited", retry:=30).ToDomainException().Message)
        Assert.IsType(Of NotSupportedException)(Err(501, "unsupported").ToDomainException())
        Dim cam = Assert.IsType(Of IsapiException)(Err(502, "camera_error", isapi:=403).ToDomainException())
        Assert.Equal(403, cam.HttpStatus)
        Assert.Equal(0, Assert.IsType(Of IsapiException)(Err(503, "camera_unavailable").ToDomainException()).HttpStatus)
        Assert.IsType(Of IsapiException)(Err(500, "internal").ToDomainException())
        ' الرسائل عربية ولا تعكس نصاً قادماً من الخادم
        Assert.DoesNotContain("<", GatewayApiException.UiMessage("bad_request", "<script>"), StringComparison.Ordinal)
    End Sub

    ' ======================= GatewayClient على معالج مبرمج =======================
    Private NotInheritable Class Scripted
        Inherits HttpMessageHandler
        Public ReadOnly Property Log As New List(Of (Method As String, Path As String, Auth As String, Body As String, Headers As Dictionary(Of String, String)))()
        Public Property Responder As Func(Of HttpRequestMessage, String, HttpResponseMessage)

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
            Dim body As String = If(request.Content Is Nothing, "", Await request.Content.ReadAsStringAsync(cancellationToken))
            Dim headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each h In request.Headers
                headers(h.Key) = String.Join(",", h.Value)
            Next
            SyncLock Log
                Log.Add((request.Method.Method, request.RequestUri.AbsolutePath, request.Headers.Authorization?.ToString(), body, headers))
            End SyncLock
            Return Responder(request, body)
        End Function
    End Class

    Private Shared Function Json(status As HttpStatusCode, value As Object) As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(JsonSerializer.Serialize(value, value.GetType(), WireJson.Write), Encoding.UTF8, "application/json")}
    End Function

    Private Shared Function ErrorJson(status As Integer, code As String, Optional retry As Integer? = Nothing) As HttpResponseMessage
        Return Json(CType(status, HttpStatusCode), New ErrorEnvelope With {.Error = New ErrorBody With {.Code = code, .Message = code, .RetryAfterSeconds = retry}})
    End Function

    Private Shared Function LoginOk(Optional role As String = "operator") As HttpResponseMessage
        Return Json(HttpStatusCode.OK, New LoginOkDto With {
            .AccessToken = "ptzs_TESTTOKEN", .IdleTimeoutSeconds = 1800,
            .Session = New MeDto With {.UserName = "alice", .Role = role, .ServerTimeUtc = New DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                                       .IdleExpiresUtc = New DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc)}})
    End Function

    Private Shared Function NewClient(handler As Scripted, Optional provider As IGatewayCredentialProvider = Nothing) As GatewayClient
        Dim ep As New GatewayEndpoint With {.BaseUrl = "https://gw.example.test:8443", .TimeoutSeconds = 5}
        Return New GatewayClient(ep, provider, handler)
    End Function

    Private Shared Sub SignIn(c As GatewayClient)
        c.LoginAsync(New GatewayCredentials With {.UserName = "alice", .Password = "pw", .Totp = "123456"}, Threading.CancellationToken.None).GetAwaiter().GetResult()
    End Sub

    Private NotInheritable Class FixedPrompt
        Implements IGatewayCredentialProvider
        Public Calls As Integer
        Public Code As String
        Public Function GetStepUpCodeAsync(reason As String, ct As Threading.CancellationToken) As Task(Of String) Implements IGatewayCredentialProvider.GetStepUpCodeAsync
            Threading.Interlocked.Increment(Calls)
            Return Task.FromResult(Code)
        End Function
    End Class

    <Fact>
    Public Sub Client_SendsBearerOnlyAfterLogin_NeverInTheUrl_AndKeepsTheLoginBodyOutOfTheToken()
        Dim h As New Scripted()
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return LoginOk()
                          Return Json(HttpStatusCode.OK, New CameraListDto())
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            c.GetJsonAsync(Of CameraListDto)("/api/v1/cameras", Threading.CancellationToken.None).GetAwaiter().GetResult()
            Assert.Null(h.Log(0).Auth)
            Assert.Equal("Bearer ptzs_TESTTOKEN", h.Log(1).Auth)
            Assert.DoesNotContain("ptzs_", String.Join(" ", h.Log.Select(Function(l) l.Path)))
            Assert.Equal("Operator", c.ToUserSession().Role.ToString())
            Assert.Equal(TimeSpan.FromMinutes(30), c.ToUserSession().IdleTimeout)
        End Using
    End Sub

    <Fact>
    Public Sub Client_RejectsPathsOutsideTheApi_AndCapsHugeResponses()
        Dim h As New Scripted()
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return LoginOk()
                          Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(New Byte(GatewayClient.MaxBodyBytes + 10) {})}
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            For Each bad As String In {"/etc/passwd", "/api/v1/../admin", "//evil/api/v1/x", "/api/v1/x\y", "api/v1/cameras"}
                Assert.Throws(Of ArgumentException)(Function() c.RequestAsync(HttpMethod.Get, bad, Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult())
            Next
            Assert.Throws(Of GatewayUnreachableException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult())
        End Using
    End Sub

    <Fact>
    Public Sub Client_StepUp_PromptsOnce_RetriesTheRequest_AndStopsWhenTheUserCancels()
        Dim h As New Scripted()
        Dim stepped As Boolean = False
        h.Responder = Function(req, body)
                          Dim p As String = req.RequestUri.AbsolutePath
                          If p.EndsWith("/auth/login") Then Return LoginOk()
                          If p.EndsWith("/auth/stepup") Then
                              stepped = True
                              Return Json(HttpStatusCode.OK, New StepUpResultDto With {.StepUpUntilUtc = DateTime.UtcNow.AddMinutes(5)})
                          End If
                          If Not stepped Then Return ErrorJson(403, "step_up_required")
                          Return Json(HttpStatusCode.OK, New EngineStateDto With {.MotionEnabled = False})
                      End Function
        Dim prompt As New FixedPrompt With {.Code = "654321"}
        Using c As GatewayClient = NewClient(h, prompt)
            SignIn(c)
            Dim r As GatewayResponse = c.RequestAsync(HttpMethod.Put, "/api/v1/cameras/se7c432mw/smart/state", New EngineStateDto With {.MotionEnabled = False}, Threading.CancellationToken.None).GetAwaiter().GetResult()
            Assert.Equal(200, r.Status)
            Assert.Equal(1, prompt.Calls)
            Assert.Contains("""totp"":""654321""", h.Log.Single(Function(l) l.Path.EndsWith("/stepup")).Body)
            Assert.Equal(3, h.Log.Where(Function(l) l.Path.EndsWith("/smart/state")).Count() + 1)         ' محاولة مرفوضة ثم ناجحة (+1 للدخول)
        End Using
        ' المستخدم يلغي ⇒ AccessDenied بلا طلب إضافي
        Dim h2 As New Scripted()
        h2.Responder = Function(req, body) If(req.RequestUri.AbsolutePath.EndsWith("/auth/login"), LoginOk(), ErrorJson(403, "step_up_required"))
        Dim cancel As New FixedPrompt With {.Code = Nothing}
        Using c2 As GatewayClient = NewClient(h2, cancel)
            SignIn(c2)
            Assert.Throws(Of AccessDeniedException)(Function() c2.RequestAsync(HttpMethod.Put, "/api/v1/cameras/se7c432mw/smart/state", New EngineStateDto With {.MotionEnabled = False}, Threading.CancellationToken.None).GetAwaiter().GetResult())
            Assert.Equal(1, cancel.Calls)
            Assert.DoesNotContain(h2.Log, Function(l) l.Path.EndsWith("/stepup"))
        End Using
    End Sub

    <Fact>
    Public Sub Client_401_EndsTheSessionOnce_AndClearsTheToken_ButBadLoginDoesNot()
        Dim h As New Scripted()
        Dim authed As Boolean = True
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return If(authed, LoginOk(), ErrorJson(401, "invalid_credentials"))
                          Return ErrorJson(401, "session_expired")
                      End Function
        Using c As GatewayClient = NewClient(h)
            Dim ended As New List(Of String)()
            AddHandler c.SessionEnded, Sub(r) ended.Add(r)
            authed = False
            Dim bad = Assert.Throws(Of Ptz.Core.Security.AuthenticationException)(Function() c.LoginAsync(New GatewayCredentials With {.UserName = "a", .Password = "b"}, Threading.CancellationToken.None).GetAwaiter().GetResult())
            Assert.Empty(ended)
            authed = True
            SignIn(c)
            Assert.True(c.IsSignedIn)
            Assert.True(Assert.Throws(Of AccessDeniedException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult()).IsSessionExpired)
            Assert.Throws(Of AccessDeniedException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult())
            Assert.Equal({"session_expired"}, ended.ToArray())
            Assert.False(c.IsSignedIn)
            Assert.Equal(1, h.Log.Where(Function(l) l.Path.EndsWith("/cameras")).Count())            ' الثاني لم يغادر الجهاز
        End Using
    End Sub

    <Fact>
    Public Sub Client_TransportFailures_BecomeUnreachable_AndTimeoutsAreReported()
        Dim h As New Scripted()
        Dim mode As Integer = 0
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return LoginOk()
                          If mode = 0 Then Throw New HttpRequestException("boom", New System.Security.Authentication.AuthenticationException("pin"))
                          If mode = 1 Then Throw New HttpRequestException("refused")
                          Throw New IO.IOException("reset")
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Dim cert = Assert.Throws(Of GatewayUnreachableException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult())
            Assert.True(cert.IsCertificateProblem)
            Assert.Contains("شهادة", cert.Message)
            mode = 1
            Assert.False(Assert.Throws(Of GatewayUnreachableException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult()).IsCertificateProblem)
            mode = 2
            Assert.IsAssignableFrom(Of IsapiException)(Assert.Throws(Of GatewayUnreachableException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult()))
            Using cts As New Threading.CancellationTokenSource()
                cts.Cancel()
                Assert.ThrowsAny(Of OperationCanceledException)(Function() c.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, cts.Token).GetAwaiter().GetResult())
            End Using
        End Using
    End Sub

    ' ======================= المتحكم عن بُعد =======================
    <Fact>
    Public Sub Controller_ConcurrentMoves_ArriveInOrder_LatestWins_AndSupersededOnesAreSkipped()
        Dim h As New Scripted()
        Dim inFlight As Integer = 0
        Dim maxInFlight As Integer = 0
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return LoginOk()
                          Dim n As Integer = Threading.Interlocked.Increment(inFlight)
                          maxInFlight = Math.Max(maxInFlight, n)
                          Threading.Thread.Sleep(15)
                          Threading.Interlocked.Decrement(inFlight)
                          Return New HttpResponseMessage(HttpStatusCode.NoContent)
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Using ctl As New RemoteCameraController(c, "se7c432mw", CameraProfiles.Ds2Se7c432mwAeb, renewMs:=5000)
                Dim tasks As New List(Of Task)()
                For i As Integer = 1 To 20
                    Dim speed As Integer = i * 4
                    tasks.Add(Task.Run(Function() ctl.MoveAsync(New PtzVector(speed, 0, 0), Threading.CancellationToken.None)))
                    Threading.Thread.Sleep(1)
                Next
                Task.WaitAll(tasks.ToArray())
                Dim moves = h.Log.Where(Function(l) l.Path.EndsWith("/ptz/move")).ToList()
                Assert.True(moves.Count >= 1)
                Assert.True(moves.Count <= 20)
                Dim seqs As List(Of Long) = moves.Select(Function(m) JsonDocument.Parse(m.Body).RootElement.GetProperty("seq").GetInt64()).ToList()
                For i As Integer = 1 To seqs.Count - 1
                    Assert.True(seqs(i) > seqs(i - 1), "يجب أن يصل الأقدم قبل الأحدث")
                Next
                Assert.Equal(1, maxInFlight)                                       ' لا طلبات حركة متوازية
                Assert.Equal(80, JsonDocument.Parse(moves.Last().Body).RootElement.GetProperty("pan").GetInt32())      ' الأحدث يفوز
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Controller_StaleSeqRejection_IsSilent_AndRenewStopsAfterAFailure()
        Dim h As New Scripted()
        Dim failMoves As Boolean = False
        h.Responder = Function(req, body)
                          Dim p As String = req.RequestUri.AbsolutePath
                          If p.EndsWith("/auth/login") Then Return LoginOk()
                          If p.EndsWith("/ptz/move") Then Return If(failMoves, ErrorJson(503, "camera_unavailable"), New HttpResponseMessage(HttpStatusCode.NoContent))
                          If p.EndsWith("/ptz/stop") Then Return ErrorJson(409, "stale_seq")
                          Return New HttpResponseMessage(HttpStatusCode.NoContent)
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Using ctl As New RemoteCameraController(c, "se7c432mw", CameraProfiles.Ds2Se7c432mwAeb, renewMs:=40)
                ctl.MoveAsync(New PtzVector(10, 0, 0), Threading.CancellationToken.None).GetAwaiter().GetResult()
                Threading.Thread.Sleep(150)
                Dim before As Integer = h.Log.Where(Function(l) l.Path.EndsWith("/ptz/move")).Count()
                Assert.True(before >= 2, "التجديد لم يعمل")
                failMoves = True
                Threading.Thread.Sleep(200)
                Dim after As Integer = h.Log.Where(Function(l) l.Path.EndsWith("/ptz/move")).Count()
                Threading.Thread.Sleep(200)
                Assert.Equal(after, h.Log.Where(Function(l) l.Path.EndsWith("/ptz/move")).Count())       ' توقف التجديد بعد الفشل (البوابة ستوقف الكاميرا بعقدها)
                ctl.StopAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()                 ' 409 stale_seq على الإيقاف يُتجاهل بصمت
            End Using
        End Using
    End Sub

    ' ======================= بث الأحداث: إعادة الاتصال =======================
    Private Shared Function SseResponse(text As String) As HttpResponseMessage
        Dim resp As New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StreamContent(New IO.MemoryStream(Encoding.UTF8.GetBytes(text)))}
        resp.Content.Headers.ContentType = New Headers.MediaTypeHeaderValue("text/event-stream")
        Return resp
    End Function

    Private Shared Function Hello() As String
        Return "event: hello" & vbLf & "data: " & JsonSerializer.Serialize(New HelloWire With {.CameraId = "se7c432mw", .HeartbeatSeconds = 5, .Online = True}, WireJson.Write) & vbLf & vbLf
    End Function

    Private Shared Function AlarmFrame(seq As Long, message As String) As String
        Return "event: alarm" & vbLf & "id: " & seq & vbLf & "data: " &
               JsonSerializer.Serialize(New AlarmDto With {.Seq = seq, .Id = Guid.NewGuid(), .Kind = "Motion", .Severity = "warning", .Message = message, .CameraId = "se7c432mw", .TimeUtc = DateTime.UtcNow}, WireJson.Write) & vbLf & vbLf
    End Function

    <Fact>
    Public Sub EventStream_ReconnectsWithLastEventId_RaisesFaultsOnErrors_AndStopsOnRevocation()
        Dim h As New Scripted()
        Dim attempt As Integer = 0
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return LoginOk()
                          Dim n As Integer = Threading.Interlocked.Increment(attempt)
                          Select Case n
                              Case 1 : Return ErrorJson(503, "busy")                                              ' فشل أول
                              Case 2 : Return SseResponse(Hello() & AlarmFrame(100, "first") & "event: garbage" & vbLf & "data: not-json" & vbLf & vbLf)   ' ثم ينغلق
                              Case 3 : Return SseResponse(Hello() & AlarmFrame(101, "second") & "event: bye" & vbLf & "data: {""reason"":""revoked""}" & vbLf & vbLf)
                              Case Else : Return ErrorJson(500, "internal")
                          End Select
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Dim alarms As New List(Of String)()
            Dim faults As New List(Of Integer)()
            Dim ended As New List(Of String)()
            AddHandler c.SessionEnded, Sub(r) ended.Add(r)
            Using stream As New GatewayEventStream(c, "se7c432mw", Function(k) TimeSpan.FromMilliseconds(10))
                AddHandler stream.AlarmReceived, Sub(a) alarms.Add(a.Message)
                AddHandler stream.Faulted, Sub(ex, k) faults.Add(k)
                stream.Start()
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                While ended.Count = 0 AndAlso sw.Elapsed.TotalSeconds < 5
                    Threading.Thread.Sleep(10)
                End While
            End Using
            Assert.Equal({"first", "second"}, alarms.ToArray())
            Assert.Equal({"revoked"}, ended.ToArray())
            Assert.NotEmpty(faults)
            Dim opens = h.Log.Where(Function(l) l.Path.EndsWith("/events")).ToList()
            Assert.Equal(3, opens.Count)
            Assert.False(opens(0).Headers.ContainsKey("Last-Event-ID"))
            Assert.False(opens(1).Headers.ContainsKey("Last-Event-ID"))                  ' لم يصل إنذار قبل الفشل الأول
            Assert.Equal("100", opens(2).Headers("Last-Event-ID"))                       ' استئناف من آخر إنذار
        End Using
    End Sub

    <Fact>
    Public Sub EventStream_ResyncClearsTheResumePoint_AndOverflowReconnectsImmediately()
        Dim h As New Scripted()
        Dim attempt As Integer = 0
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return LoginOk()
                          Dim n As Integer = Threading.Interlocked.Increment(attempt)
                          Select Case n
                              Case 1 : Return SseResponse(Hello() & AlarmFrame(200, "a") & "event: resync" & vbLf & "data: {""what"":[""alarms""]}" & vbLf & vbLf & "event: bye" & vbLf & "data: {""reason"":""overflow""}" & vbLf & vbLf)
                              Case 2 : Return SseResponse(Hello() & "event: bye" & vbLf & "data: {""reason"":""revoked""}" & vbLf & vbLf)
                              Case Else : Return ErrorJson(500, "internal")
                          End Select
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Dim resyncs As Integer = 0
            Dim ended As New List(Of String)()
            AddHandler c.SessionEnded, Sub(r) ended.Add(r)
            Using stream As New GatewayEventStream(c, "se7c432mw", Function(k) TimeSpan.FromSeconds(30))
                AddHandler stream.ResyncRequired, Sub(w) Threading.Interlocked.Increment(resyncs)
                stream.Start()
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                While ended.Count = 0 AndAlso sw.Elapsed.TotalSeconds < 5
                    Threading.Thread.Sleep(10)
                End While
            End Using
            Assert.Equal(1, resyncs)
            Assert.Equal({"revoked"}, ended.ToArray())
            Dim opens = h.Log.Where(Function(l) l.Path.EndsWith("/events")).ToList()
            Assert.Equal(2, opens.Count)
            Assert.False(opens(1).Headers.ContainsKey("Last-Event-ID"), "بعد resync لا يصلح معرّف الاستئناف القديم")
        End Using
    End Sub

    <Fact>
    Public Sub EventStream_RejectsBadCameraIds()
        Dim h As New Scripted()
        Using c As GatewayClient = NewClient(h)
            Assert.Throws(Of ArgumentException)(Function() New GatewayEventStream(c, "../x"))
            Assert.Throws(Of ArgumentException)(Function() New GatewayEventStream(c, "UPPER"))
        End Using
    End Sub

    ' ======================= مراجعة الأمان: إصلاحات المتحكم =======================
    <Fact>
    Public Sub Controller_AStopThatOvertakesAnInFlightMove_NeverLeavesARenewTimerRunning()
        Dim h As New Scripted()
        h.Responder = Function(req, body)
                          If req.RequestUri.AbsolutePath.EndsWith("/auth/login") Then Return LoginOk()
                          If req.RequestUri.AbsolutePath.EndsWith("/ptz/move") Then Threading.Thread.Sleep(250)     ' رحلة بطيئة
                          Return New HttpResponseMessage(HttpStatusCode.NoContent)
                      End Function
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Using ctl As New RemoteCameraController(c, "se7c432mw", CameraProfiles.Ds2Se7c432mwAeb, renewMs:=40)
                Dim move As Task = Task.Run(Function() ctl.MoveAsync(New PtzVector(20, 0, 0), Threading.CancellationToken.None))
                Threading.Thread.Sleep(60)                                    ' الأمر ما زال في الرحلة
                ctl.StopAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
                move.Wait()
                Dim afterStop As Integer = h.Log.Where(Function(l) l.Path.EndsWith("/ptz/move")).Count()
                Threading.Thread.Sleep(600)
                Assert.Equal(afterStop, h.Log.Where(Function(l) l.Path.EndsWith("/ptz/move")).Count())          ' لا تجديد بعد الإيقاف
                Assert.Equal(1, h.Log.Where(Function(l) l.Path.EndsWith("/ptz/stop")).Count())
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Controller_MoveSequenceBelongsToTheSession_SoANewControllerContinuesWhereTheOldOneStopped()
        Dim h As New Scripted()
        h.Responder = Function(req, body) If(req.RequestUri.AbsolutePath.EndsWith("/auth/login"), LoginOk(), New HttpResponseMessage(HttpStatusCode.NoContent))
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Dim lastOfFirst As Long
            Using first As New RemoteCameraController(c, "se7c432mw", CameraProfiles.Ds2Se7c432mwAeb, renewMs:=5000)
                For i As Integer = 1 To 5
                    first.MoveAsync(New PtzVector(i * 5, 0, 0), Threading.CancellationToken.None).GetAwaiter().GetResult()
                Next
                first.StopAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            End Using
            Dim seqs As List(Of Long) = h.Log.Where(Function(l) l.Path.EndsWith("/ptz/move") OrElse l.Path.EndsWith("/ptz/stop")).Select(Function(m) JsonDocument.Parse(m.Body).RootElement.GetProperty("seq").GetInt64()).ToList()
            lastOfFirst = seqs.Max()
            Using second As New RemoteCameraController(c, "se7c432mw", CameraProfiles.Ds2Se7c432mwAeb, renewMs:=5000)
                second.MoveAsync(New PtzVector(10, 0, 0), Threading.CancellationToken.None).GetAwaiter().GetResult()
                Dim firstOfSecond As Long = JsonDocument.Parse(h.Log.Last(Function(l) l.Path.EndsWith("/ptz/move")).Body).RootElement.GetProperty("seq").GetInt64()
                Assert.True(firstOfSecond > lastOfFirst, $"البوابة سترفض {firstOfSecond} ≤ {lastOfFirst} بصمت (stale_seq)")
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Controller_LensHalt_AlwaysCarriesAValidSpeed()
        Dim h As New Scripted()
        h.Responder = Function(req, body) If(req.RequestUri.AbsolutePath.EndsWith("/auth/login"), LoginOk(), New HttpResponseMessage(HttpStatusCode.NoContent))
        Using c As GatewayClient = NewClient(h)
            SignIn(c)
            Using ctl As New RemoteCameraController(c, "se7c432mw", CameraProfiles.Ds2Se7c432mwAeb)
                ctl.MoveFocusAsync(LensDirection.Halt, 0, Threading.CancellationToken.None).GetAwaiter().GetResult()
                ctl.MoveIrisAsync(LensDirection.Halt, 0, Threading.CancellationToken.None).GetAwaiter().GetResult()
                ctl.MoveFocusAsync(LensDirection.Far, 40, Threading.CancellationToken.None).GetAwaiter().GetResult()
            End Using
            Dim lens = h.Log.Where(Function(l) l.Path.Contains("/lens/")).ToList()
            Assert.Equal(3, lens.Count)
            Assert.All(lens.Take(2), Sub(l) Assert.Equal(1, JsonDocument.Parse(l.Body).RootElement.GetProperty("speed").GetInt32()))
            Assert.Equal(40, JsonDocument.Parse(lens(2).Body).RootElement.GetProperty("speed").GetInt32())
        End Using
    End Sub
End Class
