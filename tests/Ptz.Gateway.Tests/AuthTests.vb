Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Api
Imports Xunit

Public Class AuthTests

    <Fact>
    Public Sub Login_Succeeds_WithPasswordAndTotp_AndReturnsEffectiveRole()
        Using h As New Harness()
            Dim resp As HttpResponseMessage = h.Login("alice")
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode)
            Dim ok As LoginOkDto = Harness.Read(Of LoginOkDto)(resp)
            Assert.StartsWith("ptzs_", ok.AccessToken)
            Assert.Equal(48, ok.AccessToken.Length)
            Assert.Equal("operator", ok.Session.Role)
            Assert.Contains("ptzControl", ok.Session.Permissions)
            Assert.DoesNotContain("imageControl", ok.Session.Permissions)
            Assert.Equal(1800, ok.IdleTimeoutSeconds)
            Assert.NotNull(ok.Session.StepUpUntilUtc)
            ' الترويسات الأمنية
            Assert.True(resp.Headers.Contains("X-Request-Id"))
            Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").Single())
            Assert.Equal("no-store", resp.Headers.GetValues("Cache-Control").Single())
            Assert.False(resp.Headers.Contains("Server"))
        End Using
    End Sub

    <Fact>
    Public Sub Login_Failures_AreByteIdentical_ForEveryCause()
        Using h As New Harness()
            Dim bodies As New List(Of String)()
            Dim wrongPw As HttpResponseMessage = h.Login("alice", "Wrong!Passw0rd1")
            Dim noUser As HttpResponseMessage = h.Login("ghost")
            Dim badTotp As HttpResponseMessage = h.Login("alice", Harness.Pw, "000000")
            For Each r As HttpResponseMessage In {wrongPw, noUser, badTotp}
                Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode)
                Dim env As ErrorEnvelope = Harness.Read(Of ErrorEnvelope)(r)
                Assert.Equal("invalid_credentials", env.Error.Code)
                bodies.Add(Harness.Body(r).Replace(env.Error.RequestId, "RID"))
            Next
            Assert.Single(bodies.Distinct())
        End Using
    End Sub

    <Fact>
    Public Sub Login_RejectsReplayedTotp_AndMissingTotp()
        Using h As New Harness()
            Dim code As String = h.NextCode("alice")
            Assert.Equal(HttpStatusCode.OK, h.Login("alice", Harness.Pw, code).StatusCode)
            Assert.Equal(HttpStatusCode.Unauthorized, h.Login("alice", Harness.Pw, code).StatusCode)
            Dim missing As HttpResponseMessage = h.Send("POST", "/api/v1/auth/login", New LoginRequest With {.UserName = "alice", .Password = Harness.Pw})
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode)
        End Using
    End Sub

    <Theory>
    <InlineData("{""userName"":""alice"",""password"":""x"",""totp"":""12345"",""extra"":1}")>
    <InlineData("{""userName"":""alice"",""password"":""x"",""totp"":""12ab56""}")>
    <InlineData("{""userName"":"""",""password"":""x"",""totp"":""123456""}")>
    <InlineData("not json")>
    Public Sub Login_MalformedBodies_Give400(json As String)
        Using h As New Harness()
            Dim r As HttpResponseMessage = h.Send("POST", "/api/v1/auth/login", json)
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode)
            Assert.Equal("bad_request", Harness.ErrorCode(r))
        End Using
    End Sub

    <Fact>
    Public Sub Login_OversizedBody_And_WrongContentType_AreRejected()
        Using h As New Harness()
            Dim huge As String = "{""userName"":""" & New String("a"c, 6000) & """,""password"":""x"",""totp"":""123456""}"
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, h.Send("POST", "/api/v1/auth/login", huge).StatusCode)
            Dim req As New HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") With {.Content = New StringContent("{}", Text.Encoding.UTF8, "text/plain")}
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, h.Client.SendAsync(req).GetAwaiter().GetResult().StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Admin_IsCappedToSupervisor_ByDefault()
        Using h As New Harness()
            Dim ok As LoginOkDto = Harness.Read(Of LoginOkDto)(h.Login("root1"))
            Assert.Equal("supervisor", ok.Session.Role)
            Assert.DoesNotContain("manageUsers", ok.Session.Permissions)
            Assert.DoesNotContain("deviceMaintenance", ok.Session.Permissions)
        End Using
    End Sub

    <Fact>
    Public Sub Endpoints_RequireValidBearerToken()
        Using h As New Harness()
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", "/api/v1/auth/me").StatusCode)
            Assert.Equal("unauthorized", Harness.ErrorCode(h.Send("GET", "/api/v1/auth/me", token:="ptzs_" & New String("A"c, 43))))
            Assert.Equal("unauthorized", Harness.ErrorCode(h.Send("GET", "/api/v1/auth/me", token:="garbage")))
            Dim t As String = h.Token("alice")
            Assert.Equal(HttpStatusCode.OK, h.Send("GET", "/api/v1/auth/me", token:=t).StatusCode)
            ' الرمز في الاستعلام ممنوع
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", "/api/v1/auth/me?access_token=" & t).StatusCode)
            ' مخطط غير Bearer
            Dim req As New HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me")
            req.Headers.TryAddWithoutValidation("Authorization", "Basic " & t)
            Assert.Equal(HttpStatusCode.Unauthorized, h.Client.SendAsync(req).GetAwaiter().GetResult().StatusCode)
            ' ترويستا Authorization
            Dim req2 As New HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me")
            req2.Headers.TryAddWithoutValidation("Authorization", {"Bearer " & t, "Bearer " & t})
            Assert.Equal(HttpStatusCode.Unauthorized, h.Client.SendAsync(req2).GetAwaiter().GetResult().StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Logout_RevokesToken()
        Using h As New Harness()
            Dim t As String = h.Token("alice")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", "/api/v1/auth/logout", token:=t).StatusCode)
            Dim r As HttpResponseMessage = h.Send("GET", "/api/v1/auth/me", token:=t)
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub IdleExpiry_OnlyExtendedByMutatingCalls_AndAbsoluteLimit()
        Using h As New Harness()
            Dim t As String = h.Token("alice")
            h.Clock.Advance(20 * 60)
            Assert.Equal(HttpStatusCode.OK, h.Send("GET", "/api/v1/status", token:=t).StatusCode)   ' قراءة: لا تجدد
            h.Clock.Advance(11 * 60)
            Dim r As HttpResponseMessage = h.Send("GET", "/api/v1/auth/me", token:=t)
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode)
            Assert.Equal("session_expired", Harness.ErrorCode(r))

            Dim t2 As String = h.Token("alice")
            For i As Integer = 1 To 5
                h.Clock.Advance(20 * 60)
                Assert.Equal(HttpStatusCode.OK, h.Send("GET", "/api/v1/auth/me", token:=t2).StatusCode)   ' me يجدد
            Next
        End Using
    End Sub

    <Fact>
    Public Sub RoleChange_And_Disable_RevokeExistingSessions_Within_Maintenance()
        Using h As New Harness()
            Dim t As String = h.Token("alice")
            ' أداة إدارة (عملية أخرى) تغيّر الدور
            Dim cli As New UserStore(IO.Path.Combine(h.Dir, "users.json"), Nothing, h.Clock, 1000)
            cli.SetRole("cli", "alice", Role.Viewer)
            Dim m As New GatewayMaintenanceProbe(h)
            m.Tick()
            Dim r As HttpResponseMessage = h.Send("GET", "/api/v1/auth/me", token:=t)
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode)
            Assert.Equal("session_expired", Harness.ErrorCode(r))
            ' ودخول جديد يحمل الدور الجديد
            Assert.Equal("viewer", Harness.Read(Of LoginOkDto)(h.Login("alice")).Session.Role)
            cli.SetDisabled("cli", "alice", True)
            m.Tick()
            Assert.Equal(HttpStatusCode.Unauthorized, h.Login("alice").StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub TotpReset_ByCli_InvalidatesSessions()
        Using h As New Harness()
            Dim t As String = h.Token("alice")
            Dim cliTotp As New TotpStore(IO.Path.Combine(h.Dir, "totp.json"), IO.Path.Combine(h.Dir, "totp-state-cli.json"), h.Svc.Vault, h.Clock)
            cliTotp.Reset("alice")
            Dim m As New GatewayMaintenanceProbe(h)
            m.Tick()
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", "/api/v1/auth/me", token:=t).StatusCode)
            ' RequireTotp ومستخدم غير مسجَّل ⇒ لا دخول
            Assert.Equal(HttpStatusCode.Unauthorized, h.Login("alice", Harness.Pw, "123456").StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub SessionLimit_EvictsOldest()
        Using h As New Harness(Sub(o) o.Auth.MaxSessionsPerUser = 2)
            Dim t1 As String = h.Token("alice")
            Dim t2 As String = h.Token("alice")
            Dim t3 As String = h.Token("alice")
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", "/api/v1/auth/me", token:=t1).StatusCode)
            Assert.Equal(HttpStatusCode.OK, h.Send("GET", "/api/v1/auth/me", token:=t2).StatusCode)
            Assert.Equal(HttpStatusCode.OK, h.Send("GET", "/api/v1/auth/me", token:=t3).StatusCode)
            Dim list As SessionListDto = Harness.Read(Of SessionListDto)(h.Send("GET", "/api/v1/auth/sessions", token:=t3))
            Assert.Equal(2, list.Items.Count)
            Assert.Single(list.Items.Where(Function(s) s.Current))
            Dim other As SessionItemDto = list.Items.Single(Function(s) Not s.Current)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("DELETE", "/api/v1/auth/sessions/" & other.SessionRef, token:=t3).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("DELETE", "/api/v1/auth/sessions/deadbeef", token:=t3).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub StepUp_NeedsFreshTotp_AndFailuresRevokeSession()
        Using h As New Harness()
            Dim t As String = h.Token("alice")
            Dim bad As HttpResponseMessage = h.Send("POST", "/api/v1/auth/stepup", New StepUpRequest With {.Totp = "000000"}, t)
            Assert.Equal(HttpStatusCode.Forbidden, bad.StatusCode)
            Assert.Equal("invalid_totp", Harness.ErrorCode(bad))
            Dim good As HttpResponseMessage = h.Send("POST", "/api/v1/auth/stepup", New StepUpRequest With {.Totp = h.NextCode("alice")}, t)
            Assert.Equal(HttpStatusCode.OK, good.StatusCode)
            ' محاولات متكررة خاطئة تُبطل الجلسة
            For i As Integer = 1 To 5
                h.Clock.Advance(20)          ' يملأ دلو التحديد (5/د) كي تصل الطلبات للمعالج
                h.Send("POST", "/api/v1/auth/stepup", New StepUpRequest With {.Totp = "11111" & i}, t)
            Next
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", "/api/v1/auth/me", token:=t).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub LoginRateLimit_UsesEndpointMetadata_NotPathText()
        Using h As New Harness()
            Dim codes As New List(Of HttpStatusCode)()
            ' 10 طلبات مسموحة ثم 429 — حتى بتغيير حالة الأحرف أو إضافة شرطة أخيرة
            Dim paths As String() = {"/api/v1/auth/login", "/API/V1/AUTH/LOGIN", "/api/v1/auth/login/", "/Api/V1/Auth/Login"}
            For i As Integer = 0 To 13
                Dim r As HttpResponseMessage = h.Send("POST", paths(i Mod paths.Length), New LoginRequest With {.UserName = "ghost" & i, .Password = "x", .Totp = "123456"}, ip:="203.0.113.7")
                codes.Add(r.StatusCode)
            Next
            Dim summary As String = String.Join(",", codes.Select(Function(c) CInt(c)))
            Assert.True(codes.Take(10).All(Function(c) c = HttpStatusCode.Unauthorized), summary)
            Assert.True(codes.Skip(10).All(Function(c) c = HttpStatusCode.TooManyRequests), summary)
        End Using
    End Sub

    <Fact>
    Public Sub UserPairBackoff_Returns429_AfterRepeatedFailures()
        Using h As New Harness()
            For i As Integer = 1 To 3
                Assert.Equal(HttpStatusCode.Unauthorized, h.Login("alice", "Wrong!Passw0rd1" & i, "123456").StatusCode)
            Next
            Dim r As HttpResponseMessage = h.Login("alice", "Wrong!Passw0rd1x", "123456")
            Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode)
            Assert.True(r.Headers.Contains("Retry-After"))
            Assert.Equal("rate_limited", Harness.ErrorCode(r))
        End Using
    End Sub

    <Fact>
    Public Sub PasswordGuessing_NeverLocksTheRealUserOut()
        Using h As New Harness()
            For i As Integer = 0 To 59
                h.Login("alice", "Wrong!Passw0rd" & i, ip:="198.51.100." & (i + 1))
            Next
            ' مستخدم شرعي من عنوان آخر يدخل بنجاح
            Assert.Equal(HttpStatusCode.OK, h.Login("alice", ip:="203.0.113.50").StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub RepeatedWrongTotp_SoftLocksRemoteLogin_ButNotLocalAccount()
        Using h As New Harness()
            For i As Integer = 1 To 24
                h.Clock.Advance(80)       ' يتجاوز تراجع (مستخدم،IP)
                h.Login("alice", Harness.Pw, "00000" & (i Mod 10), ip:="198.51.100." & i)
            Next
            Dim r As HttpResponseMessage = h.Login("alice", ip:="203.0.113.60")
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode)
            ' الحساب المحلي (UserStore) لم يُقفل
            Assert.NotNull(h.Svc.Users.Authenticate("alice", Harness.Pw))
            ' فك القفل عبر ملف الطلب
            IO.File.WriteAllText(IO.Path.Combine(h.Dir, "unlock.alice.request"), "")
            Dim m As New GatewayMaintenanceProbe(h)
            m.Tick()
            h.Clock.Advance(100)
            Assert.Equal(HttpStatusCode.OK, h.Login("alice", ip:="203.0.113.61").StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Https_IsRequired_ForNonLoopback_AndHostIsFiltered()
        Using h As New Harness()
            Dim plain As HttpResponseMessage = h.Send("GET", "/api/v1/server", ip:="203.0.113.9", https:=False)
            Assert.Equal(HttpStatusCode.Forbidden, plain.StatusCode)
            Assert.Equal("https_required", Harness.ErrorCode(plain))
            Dim secure As HttpResponseMessage = h.Send("GET", "/api/v1/server", ip:="203.0.113.9")
            Assert.Equal(HttpStatusCode.OK, secure.StatusCode)
            Assert.True(secure.Headers.Contains("Strict-Transport-Security"))
            Dim req As New HttpRequestMessage(HttpMethod.Get, "/api/v1/server")
            req.Headers.Host = "evil.example"
            Assert.Equal(HttpStatusCode.BadRequest, h.Client.SendAsync(req).GetAwaiter().GetResult().StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub UnknownRoutes_And_WrongMethods_GetJsonErrors()
        Using h As New Harness()
            Dim nf As HttpResponseMessage = h.Send("GET", "/nope")
            Assert.Equal(HttpStatusCode.NotFound, nf.StatusCode)
            Assert.Equal("not_found", Harness.ErrorCode(nf))
            Dim mna As HttpResponseMessage = h.Send("GET", "/api/v1/auth/login")
            Assert.Equal(HttpStatusCode.MethodNotAllowed, mna.StatusCode)
            Assert.Equal("method_not_allowed", Harness.ErrorCode(mna))
            Assert.Equal(HttpStatusCode.MethodNotAllowed, h.Send("OPTIONS", "/api/v1/auth/login").StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub EveryRoute_IsEitherPermissioned_OrOnTheAnonymousAllowList()
        Using h As New Harness()
            Dim anonymousOk As String() = {"POST /api/v1/auth/login", "GET /api/v1/server", "GET /healthz", "GET /", "GET /app.js", "GET /app.css"}
            For Each r As RouteInfo In h.Registry.Routes
                Dim id As String = r.Method & " " & r.Pattern
                If r.Options.Anonymous Then
                    Assert.Contains(id, anonymousOk)
                Else
                    Assert.NotEqual(Permission.None, r.Permission)
                End If
                If r.Method = "GET" Then Assert.False(r.Options.Mutates, id & " GET يجب ألا يغيّر الحالة")
            Next
        End Using
    End Sub

    <Fact>
    Public Sub AuditTrail_RecordsRemoteLogin_WithRealIp_AndNeverThePassword()
        Using h As New Harness()
            h.Login("alice", ip:="203.0.113.77")
            Dim lines As String = String.Join(vbLf, h.AuditLines())
            Assert.Contains("Auth.RemoteLogin", lines)
            Assert.Contains("ip=203.0.113.77", lines)
            Assert.DoesNotContain(Harness.Pw, lines)
            Assert.True(h.Svc.AuditFile.Verify().IsValid)
        End Using
    End Sub

    ' مساعد: يشغّل دورة صيانة يدوياً
    Private NotInheritable Class GatewayMaintenanceProbe
        Private ReadOnly _m As Ptz.Gateway.Hosting.GatewayMaintenance
        Public Sub New(h As Harness)
            _m = New Ptz.Gateway.Hosting.GatewayMaintenance(h.Svc)
        End Sub
        Public Sub Tick()
            _m.Tick()
        End Sub
    End Class
End Class
