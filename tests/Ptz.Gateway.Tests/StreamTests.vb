Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports SkiaSharp
Imports Xunit

Public Class StreamTests

    Private Const Se As String = "/api/v1/cameras/se7c432mw"
    Private Const De As String = "/api/v1/cameras/de7232iw"

    Private Shared Sub Fast(o As GatewayOptions)
        o.Streams.HeartbeatSeconds = 1
        o.Streams.GuardSeconds = 1
    End Sub

    Private Shared Sub Raise(h As Harness, cam As String, kind As String, severity As AlarmSeverity, message As String, track As Integer)
        h.Svc.Cameras.Find(cam).Bus.Publish(New AlarmEvent With {.Camera = cam, .Kind = kind, .Severity = severity, .Message = message, .TrackId = track})
    End Sub

    ' ======================= SSE =======================
    <Fact>
    Public Sub Events_StartWithRetry_ThenHello_ThenHeartbeats()
        Using h As New Harness(AddressOf Fast)
            WaitOnline(h)
            Using s As SseTestReader = h.OpenSse(Se & "/events", h.Token("view"))
                Assert.Equal(HttpStatusCode.OK, s.Status)
                Assert.Equal("text/event-stream; charset=utf-8", s.Response.Content.Headers.ContentType.ToString())
                Assert.Equal("no-store", s.Response.Headers.CacheControl.ToString())
                Assert.Equal("#retry", s.ReadEvent().Name)
                Dim hello As SseTestEvent = s.ReadEvent()
                Assert.Equal("hello", hello.Name)
                Dim w As HelloWire = System.Text.Json.JsonSerializer.Deserialize(Of HelloWire)(hello.Data, WireJson.ClientRead)
                Assert.Equal("se7c432mw", w.CameraId)
                Assert.True(w.Online)
                Assert.Equal(1, w.HeartbeatSeconds)
                Assert.Equal({"tiny", "low", "mid", "high", "hd"}, w.Tiers.ToArray())
                Assert.NotNull(w.Engine)
                Assert.False(String.IsNullOrEmpty(w.GeometryRev))
                Assert.NotNull(s.WaitFor("#comment", 4))
            End Using
            Dim audit As String = String.Join(vbLf, h.AuditLines())
            Assert.Contains("Stream.Open", audit)
        End Using
    End Sub

    <Fact>
    Public Sub Events_DeliverAlarmsStateAndGeometry_OnlyForTheirCamera()
        Using h As New Harness(AddressOf Fast)
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Using s As SseTestReader = h.OpenSse(Se & "/events", sup)
                Assert.NotNull(s.WaitFor("hello"))
                Raise(h, "de7232iw", "Motion", AlarmSeverity.Info, "other-camera", 1)
                Raise(h, "se7c432mw", "Intrusion", AlarmSeverity.Critical, "mine", 2)
                Dim al As SseTestEvent = s.WaitFor("alarm")
                Assert.NotNull(al)
                Dim dto As AlarmDto = System.Text.Json.JsonSerializer.Deserialize(Of AlarmDto)(al.Data, WireJson.ClientRead)
                Assert.Equal("mine", dto.Message)
                Assert.Equal(dto.Seq.ToString(), al.Id)

                Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.AutoTrackEnabled = True}, sup).StatusCode)
                Dim st As SseTestEvent = s.WaitFor("state")
                Assert.NotNull(st)
                Assert.Contains("""by"":""sup""", st.Data)

                Dim etag As String = h.Send("GET", Se & "/smart/geometry", token:=sup).Headers.ETag.Tag
                Dim g As New GeometryDto()
                g.Masks.Add(New List(Of PointDto) From {New PointDto With {.X = 0.1, .Y = 0.1}, New PointDto With {.X = 0.3, .Y = 0.1}, New PointDto With {.X = 0.3, .Y = 0.3}})
                Refill(h)
                Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/geometry", g, sup, headers:=New Dictionary(Of String, String) From {{"If-Match", etag}}).StatusCode)
                Assert.NotNull(s.WaitFor("geo"))

                ' إقرار ⇒ حدث ack
                Dim ackId As Guid = dto.Id
                Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", "/api/v1/alarms/" & ackId.ToString() & "/ack", token:=sup).StatusCode)
                Dim ack As SseTestEvent = s.WaitFor("ack")
                Assert.NotNull(ack)
                Assert.Contains(ackId.ToString(), ack.Data)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Events_Resume_ReplaysMissedAlarms_AndAsksResyncWhenTooOld()
        Using h As New Harness(AddressOf Fast)
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Raise(h, "se7c432mw", "Motion", AlarmSeverity.Critical, "one", 1)
            Raise(h, "se7c432mw", "Motion", AlarmSeverity.Critical, "two", 2)
            Raise(h, "de7232iw", "Motion", AlarmSeverity.Critical, "elsewhere", 3)
            WaitFor(Function() Harness.Read(Of AlarmListDto)(h.Send("GET", "/api/v1/alarms", token:=v)).Items.Count >= 3)
            Dim items As List(Of AlarmDto) = Harness.Read(Of AlarmListDto)(h.Send("GET", "/api/v1/alarms?camera=se7c432mw", token:=v)).Items
            Using s As SseTestReader = h.OpenSse(Se & "/events", v, lastEventId:=items(0).Seq.ToString())
                Assert.NotNull(s.WaitFor("hello"))
                Dim replayed As SseTestEvent = s.WaitFor("alarm")
                Assert.NotNull(replayed)
                Assert.Equal(items(1).Seq.ToString(), replayed.Id)
                Assert.Contains("two", replayed.Data)
            End Using
            h.Clock.Advance(1)
            Using s As SseTestReader = h.OpenSse(Se & "/events", v, lastEventId:="1")
                Assert.NotNull(s.WaitFor("hello"))
                Dim rs As SseTestEvent = s.WaitFor("resync")
                Assert.NotNull(rs)
                Assert.Contains("alarms", rs.Data)
            End Using
            ' معرّف غير رقمي يُتجاهل بأمان
            Using s As SseTestReader = h.OpenSse(Se & "/events", v, lastEventId:="abc; DROP")
                Assert.Equal(HttpStatusCode.OK, s.Status)
                Assert.NotNull(s.WaitFor("hello"))
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Events_RequireAuth_ExistingCamera_AndEnforceQuotas()
        Using h As New Harness(Sub(o)
                                   Fast(o)
                                   o.Streams.MaxPerUser = 2
                                   o.Streams.MaxGlobal = 3
                               End Sub)
            WaitOnline(h)
            Using anon As SseTestReader = h.OpenSse(Se & "/events", Nothing)
                Assert.Equal(HttpStatusCode.Unauthorized, anon.Status)
            End Using
            Dim v As String = h.Token("view")
            Using bad As SseTestReader = h.OpenSse("/api/v1/cameras/nope/events", v)
                Assert.Equal(HttpStatusCode.NotFound, bad.Status)
            End Using
            Using q1 As SseTestReader = h.OpenSse(Se & "/events", v)
                Using q2 As SseTestReader = h.OpenSse(De & "/events", v)
                    Assert.NotNull(q1.WaitFor("hello"))
                    Assert.NotNull(q2.WaitFor("hello"))
                    Using q3 As SseTestReader = h.OpenSse(Se & "/events", v)
                        Assert.Equal(CType(429, HttpStatusCode), q3.Status)
                        Assert.True(q3.Response.Headers.Contains("Retry-After"))
                    End Using
                    ' سقف عام (3): مستخدم ثانٍ يأخذ المقعد الأخير ثم يُرفض الثالث
                    Using o1 As SseTestReader = h.OpenSse(Se & "/events", h.Token("alice"))
                        Assert.NotNull(o1.WaitFor("hello"))
                        Using o2 As SseTestReader = h.OpenSse(Se & "/events", h.Token("sup"))
                            Assert.Equal(CType(429, HttpStatusCode), o2.Status)
                        End Using
                    End Using
                End Using
            End Using
            ' بعد الإغلاق يتحرر الحد
            WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Events.SubscriberCount = 0 AndAlso h.Svc.Cameras.Find("de7232iw").Events.SubscriberCount = 0)
            Using again As SseTestReader = h.OpenSse(Se & "/events", v)
                Assert.Equal(HttpStatusCode.OK, again.Status)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub Events_EndWithBye_WhenSessionIsRevoked()
        Using h As New Harness(AddressOf Fast)
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            Using s As SseTestReader = h.OpenSse(Se & "/events", t)
                Assert.NotNull(s.WaitFor("hello"))
                Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", "/api/v1/auth/logout", token:=t).StatusCode)
                Dim bye As SseTestEvent = s.WaitFor("bye", 6)
                Assert.NotNull(bye)
                Assert.Contains("revoked", bye.Data)
                Assert.Null(s.ReadEvent(3))        ' انتهى البث فعلاً
            End Using
            WaitFor(Function() String.Join(vbLf, h.AuditLines()).Contains("Stream.Close"))
        End Using
    End Sub

    <Fact>
    Public Sub Events_EndWithBye_WhenSessionGoesIdle()
        Using h As New Harness(AddressOf Fast)
            WaitOnline(h)
            Dim t As String = h.Token("alice")
            Using s As SseTestReader = h.OpenSse(Se & "/events", t)
                Assert.NotNull(s.WaitFor("hello"))
                h.Clock.Advance((h.Options.Auth.IdleMinutes + 1) * 60.0)
                Dim bye As SseTestEvent = s.WaitFor("bye", 6)
                Assert.NotNull(bye)
                Assert.Contains("sessionExpired", bye.Data)
            End Using
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", "/api/v1/auth/me", token:=t).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Events_EndWithBye_WhenUserIsDisabled()
        Using h As New Harness(AddressOf Fast)
            WaitOnline(h)
            Dim t As String = h.Token("view")
            Using s As SseTestReader = h.OpenSse(Se & "/events", t)
                Assert.NotNull(s.WaitFor("hello"))
                h.Svc.Users.SetDisabled("setup", "view", True)
                Dim bye As SseTestEvent = s.WaitFor("bye", 6)
                Assert.NotNull(bye)
                Assert.Contains("revoked", bye.Data)
            End Using
        End Using
    End Sub

    ' ======================= الإطارات =======================
    <Fact>
    Public Sub Frame_ServesJpegPerTier_WithHeaders_AndNeverUpscales()
        Using h As New Harness(engines:=True)
            WaitOnline(h)
            Dim v As String = h.Token("view")
            WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Frames.Seq > 0, 10)
            Assert.True(h.Svc.Cameras.Find("se7c432mw").Frames.Seq > 0)
            For Each pair In {("tiny", 320), ("low", 480), ("mid", 640), ("high", 960), ("hd", 1280)}
                Refill(h)
                Dim r As HttpResponseMessage = h.Send("GET", Se & "/frame?tier=" & pair.Item1, token:=v)
                Assert.Equal(HttpStatusCode.OK, r.StatusCode)
                Assert.Equal("image/jpeg", r.Content.Headers.ContentType.MediaType)
                Assert.Equal(pair.Item1, r.Headers.GetValues("X-Frame-Tier").Single())
                Assert.True(Long.Parse(r.Headers.GetValues("X-Frame-Seq").Single()) > 0)
                Dim bytes As Byte() = r.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                Using bmp As SKBitmap = SKBitmap.Decode(bytes)
                    Assert.NotNull(bmp)
                    Assert.Equal(pair.Item2, bmp.Width)
                    Assert.Equal(Integer.Parse(r.Headers.GetValues("X-Frame-Width").Single()), bmp.Width)
                    Assert.Equal(Integer.Parse(r.Headers.GetValues("X-Frame-Height").Single()), bmp.Height)
                End Using
            Next
        End Using
    End Sub

    <Fact>
    Public Sub Frame_RejectsBadTier_RequiresAuth_AndHonoursTheConfiguredCap()
        Using h As New Harness(Sub(o) o.Frames.MaxTier = "low", engines:=True)
            WaitOnline(h)
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", Se & "/frame").StatusCode)
            Dim v As String = h.Token("view")
            WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Frames.Seq > 0, 10)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", Se & "/frame?tier=orig", token:=v).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", Se & "/frame?tier=../hd", token:=v).StatusCode)
            Refill(h)
            Dim r As HttpResponseMessage = h.Send("GET", Se & "/frame?tier=hd", token:=v)
            Assert.Equal("low", r.Headers.GetValues("X-Frame-Tier").Single())
            Dim hello As String = String.Empty
            Using s As SseTestReader = h.OpenSse(Se & "/events", v)
                Dim e As SseTestEvent = s.WaitFor("hello")
                hello = e.Data
            End Using
            Assert.DoesNotContain("""hd""", hello)
        End Using
    End Sub

    <Fact>
    Public Sub Frame_LongPoll_Returns204WithSeq_WhenNothingNewArrives()
        Using h As New Harness()                     ' المحركات متوقفة ⇒ لا إطارات جديدة
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Dim r As HttpResponseMessage = h.Send("GET", Se & "/frame?after=999999&wait=100", token:=v)
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode)
            Assert.True(r.Headers.Contains("X-Frame-Seq"))
            Assert.Equal("online", r.Headers.GetValues("X-Camera-State").Single())
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", Se & "/frame?wait=99999", token:=v).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Snapshot_FreshReturnsCameraBytes_AndIsRateLimitedPerSession()
        Using h As New Harness()
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Dim r As HttpResponseMessage = h.Send("GET", Se & "/snapshot?fresh=1", token:=v)
            Assert.Equal(HttpStatusCode.OK, r.StatusCode)
            Dim bytes As Byte() = r.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            Assert.Equal(&HFF, bytes(0))
            Assert.Equal(&HD8, bytes(1))
            Assert.Equal("orig", r.Headers.GetValues("X-Frame-Tier").Single())
            ' دفعة 2 ثم رفض
            h.Send("GET", Se & "/snapshot?fresh=1", token:=v)
            Dim limited As HttpResponseMessage = h.Send("GET", Se & "/snapshot?fresh=1", token:=v)
            Assert.Equal(CType(429, HttpStatusCode), limited.StatusCode)
            Assert.True(limited.Headers.Contains("Retry-After"))
            ' بلا fresh ولا إطار حديث ⇒ 503
            Assert.Equal(CType(503, HttpStatusCode), h.Send("GET", Se & "/snapshot", token:=v).StatusCode)
        End Using
    End Sub
End Class
