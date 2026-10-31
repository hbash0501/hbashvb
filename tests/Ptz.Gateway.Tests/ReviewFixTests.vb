Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Gateway.Audit
Imports Ptz.Gateway.Cameras
Imports Xunit

''' <summary>اختبارات انحدار لثغرات كشفتها المراجعة العدائية للتشغيل عن بُعد (كل اختبار يفشل على الشيفرة القديمة).</summary>
Public Class ReviewFixTests

    Private Const Se As String = "/api/v1/cameras/se7c432mw"
    Private Const FocusPath As String = "/ISAPI/System/Video/inputs/channels/1/focus"

    Private NotInheritable Class ScriptedInner
        Inherits HttpMessageHandler
        Public Property Mode As String = "401"
        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
            Threading.Interlocked.Increment(_reached)
            Select Case Mode
                Case "throw" : Throw New HttpRequestException("camera down")
                Case "401" : Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.Unauthorized))
                Case Else : Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK))
            End Select
        End Function
        Private _reached As Integer
        Public ReadOnly Property ReachedCount As Integer
            Get
                Return Threading.Volatile.Read(_reached)
            End Get
        End Property
    End Class

    Private Shared Function FocusBodies(h As Harness, ByRef halts As Integer) As Integer
        SyncLock h.SeCamera.Requests
            Dim list = h.SeCamera.Requests.Where(Function(r) r.Method = "PUT" AndAlso r.Path = FocusPath).ToList()
            halts = list.Where(Function(r) r.Body.Contains("<focus>0</focus>")).Count()
            Return list.Count
        End SyncLock
    End Function

    ' ---------------- قاطع الدائرة ----------------
    <Fact>
    Public Sub Breaker_HalfOpenProbeThatThrows_DoesNotWedgeTheCameraLinkForever()
        Dim clock As New TestClock()
        Dim inner As New ScriptedInner()
        Using guard As New CameraGuardHandler(inner, clock)
            Using http As New HttpClient(guard)
                Dim get_ As Func(Of HttpStatusCode) = Function() http.GetAsync("http://cam.test/x").GetAwaiter().GetResult().StatusCode
                Assert.Equal(HttpStatusCode.Unauthorized, get_())
                Assert.Equal(HttpStatusCode.Unauthorized, get_())
                Assert.True(guard.IsOpen)
                Dim reached As Integer = inner.ReachedCount
                Assert.Equal(HttpStatusCode.ServiceUnavailable, get_())              ' مفتوح: 503 اصطناعي دون لمس الكاميرا
                Assert.Equal(reached, inner.ReachedCount)
                clock.Advance(CameraGuardHandler.OpenDuration.TotalSeconds + 1)
                inner.Mode = "throw"
                Assert.Throws(Of HttpRequestException)(Function() get_())             ' المسبار يفشل نقلياً
                inner.Mode = "ok"
                Assert.Equal(HttpStatusCode.OK, get_())                               ' المسبار التالي يصل ويُغلق القاطع
                Assert.False(guard.IsOpen)
            End Using
        End Using
    End Sub

    ' ---------------- فيض سجل التجاوزات ----------------
    <Fact>
    Public Sub Denied_Ipv6RotationInsideOneSlash64_WritesASingleLine()
        Dim audit As New ListAudit()
        Dim agg As New DeniedAggregator(audit)
        Dim now As DateTime = New DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        For i As Integer = 1 To 500
            agg.Record("2001:db8:0:1::" & i.ToString("x"), "unauthorized", Nothing, now, "20010db800000001")
        Next
        Assert.Equal(1, audit.Entries.Where(Function(e) e.Contains("Guard.Denied|")).Count())
    End Sub

    <Fact>
    Public Sub Denied_ManyDistinctKeys_AreCappedGlobally_AndTheOverflowIsSummarisedEvenAfterTheAttackStops()
        Dim audit As New ListAudit()
        Dim agg As New DeniedAggregator(audit)
        Dim now As DateTime = New DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        For i As Integer = 1 To 500
            agg.Record("10.1." & (i \ 250) & "." & (i Mod 250), "unauthorized", Nothing, now)
        Next
        Dim lines As Integer = audit.Entries.Where(Function(e) e.Contains("Guard.Denied|")).Count()
        Assert.Equal(DeniedAggregator.MaxLinesPerMinute, lines)
        Assert.Empty(audit.Entries.Where(Function(e) e.Contains("DeniedOverflow")))
        agg.Flush(now.AddSeconds(30))
        Assert.Empty(audit.Entries.Where(Function(e) e.Contains("DeniedOverflow")))            ' النافذة لم تنتهِ بعد
        agg.Flush(now.AddSeconds(61))                                                         ' توقف الهجوم لكن الملخص يُكتب
        Assert.Equal(1, audit.Entries.Where(Function(e) e.Contains("DeniedOverflow")).Count())
        agg.Flush(now.AddSeconds(200))
        Assert.Equal(1, audit.Entries.Where(Function(e) e.Contains("DeniedOverflow")).Count())  ' لا تكرار
    End Sub

    ' ---------------- العدسة ----------------
    <Fact>
    Public Sub Lens_HaltWithoutSpeed_IsAccepted_AndStillValidatesRealMoves()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "halt", .Speed = 0}, sup).StatusCode)
            Dim halts As Integer
            FocusBodies(h, halts)
            Assert.Equal(1, halts)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/lens/iris", New LensRequest With {.Direction = "halt", .Speed = 0}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "far", .Speed = 0}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "far", .Speed = 101}, sup).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Lens_WatchdogHaltsTheLensByItself_WhenTheHaltNeverArrives()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "far", .Speed = 50}, sup).StatusCode)
            Dim halts As Integer
            FocusBodies(h, halts)
            Assert.Equal(0, halts)
            WaitFor(Function()
                        Dim x As Integer
                        FocusBodies(h, x)
                        Return x > 0
                    End Function, 6)
            FocusBodies(h, halts)
            Assert.Equal(1, halts)
            Assert.Contains("Lens.Halt", String.Join(vbLf, h.AuditLines()))
        End Using
    End Sub

    <Fact>
    Public Sub Lens_ExplicitHalt_CancelsTheWatchdog_SoNoSecondHaltFollows()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "near", .Speed = 30}, sup)
            h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "halt", .Speed = 1}, sup)
            Threading.Thread.Sleep(3600)
            Dim halts As Integer
            FocusBodies(h, halts)
            Assert.Equal(1, halts)
        End Using
    End Sub

    <Fact>
    Public Sub Lens_SessionRevocation_HaltsTheLensImmediately_NotAfterTheWatchdog()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/lens/focus", New LensRequest With {.Direction = "far", .Speed = 50}, sup).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", "/api/v1/auth/logout", token:=sup).StatusCode)
            WaitFor(Function()
                        Dim x As Integer
                        FocusBodies(h, x)
                        Return x > 0
                    End Function, 2)
            Dim halts As Integer
            FocusBodies(h, halts)
            Assert.Equal(1, halts)
        End Using
    End Sub

    ' ---------------- تحديد معدل القراءات والبثوث ----------------
    <Fact>
    Public Sub CameraReads_ArePerSessionRateLimited_SoAViewerCannotHammerTheCamera()
        Using h As New Harness()
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Dim image As New List(Of HttpStatusCode)()
            For i As Integer = 1 To 4
                image.Add(h.Send("GET", Se & "/image", token:=v).StatusCode)
            Next
            Assert.Equal(CType(429, HttpStatusCode), image(2))                              ' Heavy: دفعة 2
            Assert.True(h.Send("GET", Se & "/image", token:=v).Headers.Contains("Retry-After"))
            Dim pos As New List(Of HttpStatusCode)()
            For i As Integer = 1 To 12
                pos.Add(h.Send("GET", Se & "/ptz/position", token:=v).StatusCode)
            Next
            Assert.Equal(HttpStatusCode.OK, pos(0))
            Assert.Contains(CType(429, HttpStatusCode), pos)
        End Using
    End Sub

    <Fact>
    Public Sub SseOpenCloseLoops_AreRateLimited_SoTheyCannotFloodTheAuditLog()
        Using h As New Harness()
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Dim codes As New List(Of HttpStatusCode)()
            For i As Integer = 1 To 9
                Using s As SseTestReader = h.OpenSse(Se & "/events", v)
                    codes.Add(s.Status)
                    If s.Status = HttpStatusCode.OK Then s.WaitFor("hello", 3)
                End Using
            Next
            Assert.Contains(CType(429, HttpStatusCode), codes)
            Assert.Equal(HttpStatusCode.OK, codes(0))
            Dim opens As Integer = h.AuditLines().Where(Function(l) l.Contains("Stream.Open")).Count()
            Assert.True(opens <= 7, "عدد Stream.Open في السجل: " & opens)
        End Using
    End Sub

    ' ---------------- تجديد من غير المالك ----------------
    <Fact>
    Public Sub Lease_OwnsActive_IsTrueOnlyForTheCurrentOwner()
        Dim clock As New TestClock()
        Dim lease As New PtzLease(clock)
        Dim a As Guid = Guid.NewGuid()
        Dim b As Guid = Guid.NewGuid()
        Assert.False(lease.OwnsActive(a))
        lease.OnMove(a, New PtzVector(10, 0, 0), 1200, 300)
        Assert.True(lease.OwnsActive(a))
        Assert.False(lease.OwnsActive(b))
        lease.OnStop(b)                                  ' إيقاف مشغّل آخر ينهي العقد
        Assert.False(lease.OwnsActive(a))
    End Sub

    <Fact>
    Public Sub Renew_AfterAnotherOperatorStopped_IsANoOp_AndNeverReachesTheCamera()
        Using h As New Harness()
            WaitOnline(h)
            Dim alice As String = h.Token("alice")
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .Seq = 1}, alice).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/stop", New StopRequest With {.Seq = 1}, sup).StatusCode)
            Dim before As Integer
            SyncLock h.SeCamera.Requests
                before = h.SeCamera.Requests.Where(Function(r) r.Path = "/ISAPI/PTZCtrl/channels/1/momentary").Count()
            End SyncLock
            h.Clock.Advance(5)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/ptz/move", New MoveRequest With {.Pan = 30, .Seq = 2, .Renew = True}, alice).StatusCode)
            SyncLock h.SeCamera.Requests
                Assert.Equal(before, h.SeCamera.Requests.Where(Function(r) r.Path = "/ISAPI/PTZCtrl/channels/1/momentary").Count())
            End SyncLock
            Assert.False(h.Svc.Cameras.Find("se7c432mw").Lease.IsActive)
        End Using
    End Sub

    ' ---------------- تدوير سجل التدقيق ----------------
    <Fact>
    Public Sub RotateAudit_ArchivesAVerifiedChain_AndRefusesACorruptOne()
        Dim dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-rot-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(dir)
        Dim keyPath As String = IO.Path.Combine(dir, "master.key")
        Dim keys As New Ptz.Core.Security.FileKeyProvider(keyPath)
        Dim log As New Ptz.Core.Security.FileAuditLog(IO.Path.Combine(dir, "audit.log"), Ptz.Core.Security.KeyDerivation.Derive(keys.GetMasterKey(), "audit"))
        For i As Integer = 1 To 5
            log.Write("u", "A", "t", "OK", "i=" & i)
        Next
        Dim sw As New IO.StringWriter()
        Dim cli As New Cli(New GatewayOptions With {.DataDir = dir}, sw, New IO.StringReader(""), Function(p) "", Function(k) Nothing)
        Assert.Equal(0, cli.Run({"rotate-audit"}))
        Assert.False(IO.File.Exists(IO.Path.Combine(dir, "audit.log")))
        Dim archive As String = IO.Directory.GetFiles(dir, "audit-2*.log").Single()
        Assert.Equal(5L, New Ptz.Core.Security.FileAuditLog(archive, Ptz.Core.Security.KeyDerivation.Derive(keys.GetMasterKey(), "audit")).Verify().EntriesChecked)
        Assert.Contains("Audit.Rotate", IO.File.ReadAllText(IO.Path.Combine(dir, "audit-admin.log")))
        ' سلسلة معطوبة ⇒ رفض وبقاء الملف
        Dim log2 As New Ptz.Core.Security.FileAuditLog(IO.Path.Combine(dir, "audit.log"), Ptz.Core.Security.KeyDerivation.Derive(keys.GetMasterKey(), "audit"))
        For i As Integer = 1 To 4
            log2.Write("u", "A", "t", "OK", "i=" & i)
        Next
        Dim lines As List(Of String) = IO.File.ReadAllLines(IO.Path.Combine(dir, "audit.log")).ToList()
        lines.RemoveAt(1)
        IO.File.WriteAllLines(IO.Path.Combine(dir, "audit.log"), lines)
        Assert.Equal(3, cli.Run({"rotate-audit"}))
        Assert.True(IO.File.Exists(IO.Path.Combine(dir, "audit.log")))
    End Sub
End Class
