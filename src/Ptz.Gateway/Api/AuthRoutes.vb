Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>مسارات المصادقة: دخول (كلمة مرور + TOTP)، تحقق إضافي، خروج، معلومات الجلسة، جلساتي.</summary>
    Public NotInheritable Class AuthRoutes
        Private Sub New()
        End Sub

        Public Shared Sub Register(api As ApiRegistry, svc As GatewayServices)
            api.Map("POST", "/api/v1/auth/login", Permission.None, Function(c) Login(c),
                    New RouteOptions With {.Anonymous = True, .MaxBodyBytes = 4096, .IsLogin = True})
            api.Map("POST", "/api/v1/auth/stepup", Permission.ViewLive, Function(c) StepUp(c),
                    New RouteOptions With {.RateClass = RateClass.StepUp, .MaxBodyBytes = 1024})
            api.Map("POST", "/api/v1/auth/logout", Permission.ViewLive, Function(c) Logout(c),
                    New RouteOptions With {.Mutates = True, .AuditExempt = True})
            api.Map("GET", "/api/v1/auth/me", Permission.ViewLive, Function(c) Me_(c))
            api.Map("GET", "/api/v1/auth/sessions", Permission.ViewLive, Function(c) Sessions(c))
            api.Map("DELETE", "/api/v1/auth/sessions/{ref}", Permission.ViewLive, Function(c) DeleteSession(c),
                    New RouteOptions With {.Mutates = True, .AuditExempt = True})
        End Sub

        Public Shared Function BuildMe(c As ApiCall, rec As SessionRecord) As MeDto
            Dim now As DateTime = c.Svc.Clock.UtcNow
            Dim idle As TimeSpan = TimeSpan.FromMinutes(c.Svc.Options.Auth.IdleMinutes)
            Dim stepUp As DateTime = rec.StepUpUntilUtc
            Return New MeDto With {
                .UserName = rec.UserName, .Role = WireMap.RoleName(rec.Role), .Permissions = WireMap.PermissionNames(rec.UserSession.Permissions),
                .SessionRef = rec.SessionRef, .CreatedUtc = rec.CreatedUtc,
                .IdleExpiresUtc = Min(rec.LastActivityUtc + idle, rec.AbsoluteExpiryUtc), .AbsoluteExpiresUtc = rec.AbsoluteExpiryUtc,
                .StepUpUntilUtc = If(stepUp > now, stepUp, CType(Nothing, DateTime?)), .ServerTimeUtc = now}
        End Function

        Private Shared Function Min(a As DateTime, b As DateTime) As DateTime
            Return If(a < b, a, b)
        End Function

        Private Shared Async Function Login(c As ApiCall) As Task
            Dim svc As GatewayServices = c.Svc
            Dim req As LoginRequest = Await c.ReadJsonAsync(Of LoginRequest)().ConfigureAwait(False)
            ' --- 1) شكل المدخلات قبل أي تجزئة ---
            If String.IsNullOrEmpty(req.UserName) OrElse req.UserName.Length > 64 Then Throw ApiException.BadRequest("userName")
            If String.IsNullOrEmpty(req.Password) OrElse req.Password.Length > 128 Then Throw ApiException.BadRequest("password")
            Dim totpGiven As Boolean = Not String.IsNullOrEmpty(req.Totp)
            If totpGiven AndAlso Not IsSixDigits(req.Totp) Then Throw ApiException.BadRequest("totp")
            If svc.Options.Auth.RequireTotp AndAlso Not totpGiven Then Throw ApiException.BadRequest("totp")
            Dim ip As String = c.ClientIp
            Dim name As String = req.UserName

            ' --- 2) تراجع (مستخدم،IP) ---
            Dim wait As TimeSpan? = svc.Throttle.Precheck(ip, name)
            If wait.HasValue Then
                Throw New ApiException(429, "rate_limited") With {.RetryAfterSeconds = Math.Max(1, CInt(Math.Ceiling(wait.Value.TotalSeconds)))}
            End If

            ' --- 3) سقف التجزئة المتزامنة (تحمّل CPU) ---
            If Not svc.LoginGate.Wait(0) Then Throw New ApiException(503, "busy") With {.RetryAfterSeconds = 2}
            Dim rec As UserRecord
            Dim totpOk As Boolean
            Dim passwordOk As Boolean
            Dim softLocked As Boolean
            Try
                rec = svc.Users.VerifyCredentials(name, req.Password)
                passwordOk = rec IsNot Nothing
                Dim enrolled As Boolean = passwordOk AndAlso svc.Totp.IsEnrolled(rec.UserName)
                If enrolled Then
                    totpOk = svc.Totp.TryConsume(rec.UserName, req.Totp)
                Else
                    svc.Totp.VerifyDummy(req.Totp)
                    totpOk = passwordOk AndAlso Not svc.Options.Auth.RequireTotp
                End If
                softLocked = svc.Throttle.IsRemoteLocked(name)
            Finally
                svc.LoginGate.Release()
            End Try

            If Not (passwordOk AndAlso totpOk) OrElse softLocked Then
                If Not softLocked Then svc.Throttle.RecordFailure(ip, name, totpStage:=passwordOk)
                svc.Denied.Record(ip, "login", AuditSanitizer.Clean(name, 32), svc.Clock.UtcNow, Hosting.Middleware.IpKey(c.Http))
                If passwordOk AndAlso Not totpOk AndAlso svc.Throttle.IsRemoteLocked(name) Then
                    svc.Audit.Write("svc:gateway", "Auth.Lockout.Remote", name, "DENIED", "remote login locked after repeated TOTP failures")
                End If
                Throw New ApiException(401, "invalid_credentials")
            End If

            ' --- 4) نجاح ---
            svc.Throttle.RecordSuccess(ip, name)
            Dim effective As Role = If(rec.Role > svc.MaxRole, svc.MaxRole, rec.Role)
            Dim stamp As String = svc.Users.GetSecurityStamp(rec.UserName) & "|" & svc.Totp.StampOf(rec.UserName)
            Dim token As String = Nothing
            Dim session As SessionRecord = svc.Sessions.Create(rec.UserName, effective, stamp, ip, req.Client, token)
            session.GrantStepUp(svc.Clock.UtcNow.AddMinutes(svc.Options.Auth.StepUpMinutes))    ' الدخول الحديث يُعدّ تحققاً إضافياً
            svc.Audit.Write(rec.UserName, "Auth.RemoteLogin", "gateway", "OK", $"role={effective} sid={session.SessionRef} ip={ip}")
            Await c.Ok(New LoginOkDto With {.AccessToken = token, .IdleTimeoutSeconds = svc.Options.Auth.IdleMinutes * 60, .Session = BuildMe(c, session)}).ConfigureAwait(False)
        End Function

        Private Shared Function IsSixDigits(s As String) As Boolean
            If s.Length <> 6 Then Return False
            For Each ch As Char In s
                If ch < "0"c OrElse ch > "9"c Then Return False
            Next
            Return True
        End Function

        Private Shared Async Function StepUp(c As ApiCall) As Task
            Dim req As StepUpRequest = Await c.ReadJsonAsync(Of StepUpRequest)().ConfigureAwait(False)
            If req.Totp Is Nothing OrElse Not IsSixDigits(req.Totp) Then Throw ApiException.BadRequest("totp")
            Dim ok As Boolean = c.Svc.Totp.TryConsume(c.Rec.UserName, req.Totp)
            If Not ok Then
                ' إخفاقات متكررة على الجلسة نفسها (رمز مسروق) ⇒ إبطالها
                Dim fails As Integer = CInt(c.Rec.Items.AddOrUpdate("stepup-fail", 1, Function(k, v) CInt(v) + 1))
                c.Svc.Audit.Write(c.UserName, "Auth.StepUp", "gateway", "DENIED", "invalid code")
                If fails >= 5 Then
                    c.Svc.Sessions.Revoke(c.Rec)
                    c.Svc.Audit.Write(c.UserName, "Auth.Revoke", "gateway", "OK", "step-up brute force")
                End If
                Throw New ApiException(403, "invalid_totp")
            End If
            c.Rec.Items.TryRemove("stepup-fail", Nothing)
            Dim until As DateTime = c.Svc.Clock.UtcNow.AddMinutes(c.Svc.Options.Auth.StepUpMinutes)
            c.Rec.GrantStepUp(until)
            c.Svc.Audit.Write(c.UserName, "Auth.StepUp", "gateway", "OK", "")
            Await c.Ok(New StepUpResultDto With {.StepUpUntilUtc = until}).ConfigureAwait(False)
        End Function

        Private Shared Function Logout(c As ApiCall) As Task
            c.Svc.Audit.Write(c.UserName, "Auth.Logout", "gateway", "OK", "")
            c.Svc.Sessions.Revoke(c.Rec)
            Return c.NoContent()
        End Function

        Private Shared Function Me_(c As ApiCall) As Task
            c.Rec.Touch(c.Svc.Clock.UtcNow)       ' استدعاء صريح للإبقاء على الجلسة
            Return c.Ok(BuildMe(c, c.Rec))
        End Function

        Private Shared Function MaskIp(ip As String) As String
            Dim parts As String() = ip.Split("."c)
            If parts.Length = 4 Then Return parts(0) & "." & parts(1) & "." & parts(2) & ".x"
            Dim v6 As String() = ip.Split(":"c)
            If v6.Length > 3 Then Return String.Join(":", v6.Take(3)) & "::x"
            Return "x"
        End Function

        Private Shared Function Sessions(c As ApiCall) As Task
            Dim dto As New SessionListDto()
            For Each r As SessionRecord In c.Svc.Sessions.ForUser(c.Rec.UserName)
                dto.Items.Add(New SessionItemDto With {.SessionRef = r.SessionRef, .Client = r.ClientName, .IpMasked = MaskIp(r.ClientIp),
                                                       .CreatedUtc = r.CreatedUtc, .LastActivityUtc = r.LastActivityUtc, .Current = (r Is c.Rec)})
            Next
            Return c.Ok(dto)
        End Function

        Private Shared Function DeleteSession(c As ApiCall) As Task
            Dim target As String = c.RouteValue("ref")
            Dim rec As SessionRecord = c.Svc.Sessions.ForUser(c.Rec.UserName).FirstOrDefault(Function(r) r.SessionRef = target)
            If rec Is Nothing Then Throw ApiException.NotFound()
            c.Svc.Audit.Write(c.UserName, "Auth.Revoke", "gateway", "OK", "sid=" & rec.SessionRef)
            c.Svc.Sessions.Revoke(rec)
            Return c.NoContent()
        End Function
    End Class

    ''' <summary>مسارات النظام: معلومات الخادم (اختبار اتصال/انحراف ساعة)، صحة، وحالة الكاميرات.</summary>
    Public NotInheritable Class SystemRoutes
        Private Sub New()
        End Sub

        Public Shared Sub Register(api As ApiRegistry, svc As GatewayServices)
            api.Map("GET", "/api/v1/server", Permission.None,
                    Function(c) c.Ok(New ServerInfoDto With {.ServerTimeUtc = c.Svc.Clock.UtcNow, .Mfa = c.Svc.Options.Auth.RequireTotp}),
                    New RouteOptions With {.Anonymous = True})
            api.Map("GET", "/healthz", Permission.None, Function(c) Health(c), New RouteOptions With {.Anonymous = True})
            api.Map("GET", "/api/v1/status", Permission.ViewLive, Function(c) Status(c))
        End Sub

        Private Shared Async Function Health(c As ApiCall) As Task
            Dim ok As Boolean = c.Svc.AuditHealth.IsHealthy AndAlso c.Svc.Cameras.All.Count > 0
            Await JsonHttp.WriteJsonAsync(c.Http, If(ok, 200, 503), New With {.status = If(ok, "ok", "degraded")}).ConfigureAwait(False)
        End Function

        Private Shared Function Status(c As ApiCall) As Task
            Dim dto As New StatusDto With {.ServerTimeUtc = c.Svc.Clock.UtcNow, .UptimeSeconds = CLng((c.Svc.Clock.UtcNow - c.Svc.StartedUtc).TotalSeconds)}
            For Each h In c.Svc.Cameras.All
                dto.Cameras.Add(New CameraStatusDto With {.Id = h.Id, .Online = h.Online, .LastFrameAgeMs = h.Frames.AgeMs(), .ConsecutiveFailures = h.ConsecutiveFailures, .Viewers = h.ViewerCount})
            Next
            Return c.Ok(dto)
        End Function
    End Class

End Namespace
