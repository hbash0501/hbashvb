Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Routing
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Audit
Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>
    ''' التسجيل الوحيد للمسارات + نقطة الدخول الوحيدة (Dispatch): مصادقة ← فحص البصمة ← صلاحية ← خطوة تحقق ← معدل ←
    ''' صحة التدقيق ← سياق التدقيق ← المعالج. لا يوجد مسار بلا صلاحية إلا المسموحة صراحةً (ومعروضة للاختبار).
    ''' </summary>
    Public NotInheritable Class ApiRegistry
        Private ReadOnly _app As IEndpointRouteBuilder
        Private ReadOnly _svc As GatewayServices
        Private ReadOnly _routes As New List(Of RouteInfo)()

        Public Sub New(app As IEndpointRouteBuilder, svc As GatewayServices)
            _app = app
            _svc = svc
        End Sub

        Public ReadOnly Property Routes As IReadOnlyList(Of RouteInfo)
            Get
                Return _routes
            End Get
        End Property

        Public Sub Map(method As String, pattern As String, permission As Permission, handler As Func(Of ApiCall, Task), Optional options As RouteOptions = Nothing)
            Dim opt As RouteOptions = If(options, New RouteOptions())
            If Not opt.Anonymous AndAlso permission = Permission.None Then
                Throw New InvalidOperationException($"المسار {method} {pattern} بلا صلاحية ولم يُصرَّح كمجهول.")
            End If
            Dim route As New RouteInfo(method, pattern, permission, opt, handler)
            _routes.Add(route)
            Dim rd As RequestDelegate = Function(ctx As HttpContext) Dispatch(ctx, route)
            _app.MapMethods(pattern, New String() {method}, rd).WithMetadata(route)
        End Sub

        Private Shared Function RequestIdOf(ctx As HttpContext) As String
            Dim o As Object = Nothing
            If ctx.Items.TryGetValue("rid", o) Then
                Dim s As String = TryCast(o, String)
                If s IsNot Nothing Then Return s
            End If
            Return Guid.NewGuid().ToString("N")
        End Function

        Private Shared Function RateParams(c As RateClass, ByRef rate As Double, ByRef burst As Double) As Boolean
            Select Case c
                Case RateClass.Ptz : rate = 25 : burst = 50
                Case RateClass.Cmd : rate = 5 : burst = 10
                Case RateClass.StepUp : rate = 5.0 / 60.0 : burst = 5
                Case RateClass.Heavy : rate = 1 : burst = 2
                Case RateClass.AuditList : rate = 0.5 : burst = 1
                Case RateClass.Verify : rate = 1.0 / 60.0 : burst = 1
                Case RateClass.Connect : rate = 6.0 / 60.0 : burst = 6
                Case Else : Return False
            End Select
            Return True
        End Function

        Private Async Function Dispatch(ctx As HttpContext, route As RouteInfo) As Task
            Dim requestId As String = RequestIdOf(ctx)
            Dim ac As New ApiCall(ctx, _svc, route, requestId)
            Dim failure As Exception = Nothing
            Dim linked As Threading.CancellationTokenSource = Nothing
            Dim now As DateTime = _svc.Clock.UtcNow
            Try
                If Not route.Options.Anonymous Then
                    Authenticate(ac)
                    now = _svc.Clock.UtcNow
                    Try
                        _svc.Access.Demand(ac.Rec.UserSession, route.Permission, touch:=False)
                    Catch ex As AccessDeniedException
                        _svc.Denied.Record(ac.ClientIp, "forbidden:" & route.Permission.ToString(), ac.UserName, now)
                        Throw
                    End Try
                    If route.NeedsCamera Then
                        Dim camId As String = ac.RouteValue("cam")
                        If camId Is Nothing OrElse Not System.Text.RegularExpressions.Regex.IsMatch(camId, "^[a-z0-9-]{1,32}\z") Then Throw ApiException.NotFound()
                        ac.Camera = _svc.Cameras.Find(camId)
                        If ac.Camera Is Nothing Then Throw ApiException.NotFound()
                    End If
                    If route.Options.StepUp Then ac.RequireStepUp()
                    Dim rate As Double, burst As Double
                    If RateParams(route.Options.RateClass, rate, burst) Then
                        Dim b As TokenBucket = ac.Rec.Bucket(route.Options.RateClass.ToString(), rate, burst, _svc.Clock)
                        If Not b.TryTake() Then
                            Throw New ApiException(429, "rate_limited") With {.RetryAfterSeconds = Math.Max(1, b.RetryAfterSeconds())}
                        End If
                    End If
                    If route.Options.Mutates AndAlso Not route.Options.AuditExempt AndAlso Not _svc.AuditHealth.IsHealthy Then
                        Throw New ApiException(503, "audit_unavailable") With {.RetryAfterSeconds = 10}
                    End If
                End If
                If ctx.Request.ContentLength.HasValue AndAlso ctx.Request.ContentLength.Value > route.Options.MaxBodyBytes Then Throw New ApiException(413, "payload_too_large")
                Dim sizeFeature As Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature = ctx.Features.Get(Of Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature)()
                If sizeFeature IsNot Nothing AndAlso Not sizeFeature.IsReadOnly Then sizeFeature.MaxRequestBodySize = route.Options.MaxBodyBytes

                linked = Threading.CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, If(ac.Rec Is Nothing, Threading.CancellationToken.None, ac.Rec.RevokedToken))
                If route.Options.TimeoutSeconds > 0 Then linked.CancelAfter(TimeSpan.FromSeconds(route.Options.TimeoutSeconds))
                ac.Ct = linked.Token
                AuditScope.Current = New AuditScopeData(ac.ClientIp, If(ac.Rec Is Nothing, "-", ac.Rec.SessionRef), requestId, ac.UserName)
                Await route.Handler(ac).ConfigureAwait(False)
                If route.Options.Mutates AndAlso ac.Rec IsNot Nothing Then ac.Rec.Touch(_svc.Clock.UtcNow)
            Catch ex As Exception
                failure = ex
            End Try

            If failure Is Nothing Then
                If linked IsNot Nothing Then linked.Dispose()
                Return
            End If
            Dim timedOut As Boolean = linked IsNot Nothing AndAlso linked.IsCancellationRequested AndAlso Not ctx.RequestAborted.IsCancellationRequested AndAlso
                                      (ac.Rec Is Nothing OrElse Not ac.Rec.IsRevoked)
            If linked IsNot Nothing Then linked.Dispose()
            Dim ae As ApiException = ErrorMap.ToApiException(failure, timedOut)
            If ae Is Nothing Then Return                          ' العميل أغلق الاتصال أو أُبطلت الجلسة
            If ae.Status = 401 OrElse ae.Status = 403 OrElse ae.Status = 429 Then
                If ae.Code <> "forbidden" Then _svc.Denied.Record(ac.ClientIp, ae.Code, ac.UserName, _svc.Clock.UtcNow)
            End If
            If ae.Status = 500 Then Diagnostics.Debug.WriteLine($"[{requestId}] unhandled {failure.GetType().Name}")
            Await JsonHttp.WriteErrorAsync(ctx, ae, requestId).ConfigureAwait(False)
        End Function

        Private Sub Authenticate(ac As ApiCall)
            Dim req As HttpRequest = ac.Http.Request
            If req.Query.ContainsKey("access_token") OrElse req.Query.ContainsKey("token") Then Throw ApiException.BadRequest()
            Dim auth As Microsoft.Extensions.Primitives.StringValues = req.Headers.Authorization
            If auth.Count <> 1 Then Throw ApiException.Unauthorized()
            Dim header As String = auth(0)
            If header Is Nothing OrElse Not header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) Then Throw ApiException.Unauthorized()
            Dim token As String = header.Substring(7).Trim()
            Dim res As SessionAuthResult
            Dim rec As SessionRecord = _svc.Sessions.TryAuthenticate(token, res)
            If rec Is Nothing Then Throw ApiException.Unauthorized(If(res = SessionAuthResult.Expired, "session_expired", "unauthorized"))
            ' إبطال فوري عند تغيّر الدور/كلمة المرور/التعطيل/TOTP (لا صلاحيات قديمة)
            Dim user As UserRecord = _svc.Users.TryGet(rec.UserName)
            Dim stamp As String = _svc.Users.GetSecurityStamp(rec.UserName) & "|" & _svc.Totp.StampOf(rec.UserName)
            If user Is Nothing OrElse user.Disabled OrElse stamp <> rec.Stamp Then
                _svc.Sessions.Revoke(rec)
                Throw ApiException.Unauthorized("session_expired")
            End If
            ac.Rec = rec
        End Sub
    End Class

End Namespace
