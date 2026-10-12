Imports System.Reflection
Imports Microsoft.AspNetCore.Server.Kestrel.Core.Features
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Http.Features
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Cameras
Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>بث الأحداث (SSE): تراكب التتبع، الإنذارات، الحالة، الصحة. بث واحد لكل كاميرا لكل مشاهد.</summary>
    Public NotInheritable Class StreamRoutes
        Private Sub New()
        End Sub

        Public Shared Sub Register(api As ApiRegistry, svc As GatewayServices)
            api.Map("GET", "/api/v1/cameras/{cam}/events", Permission.SmartView, Function(c) Events(c), New RouteOptions With {.TimeoutSeconds = 0, .RateClass = RateClass.Stream})
        End Sub

        Private Shared Async Function WriteTimed(c As ApiCall, text As String) As Task(Of Boolean)
            Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(c.Ct)
                cts.CancelAfter(TimeSpan.FromSeconds(5))
                Try
                    Await c.Http.Response.WriteAsync(text, cts.Token).ConfigureAwait(False)
                    Await c.Http.Response.Body.FlushAsync(cts.Token).ConfigureAwait(False)
                    Return True
                Catch ex As OperationCanceledException When Not c.Ct.IsCancellationRequested
                    Return False        ' عميل بطيء: انقضت مهلة الكتابة
                End Try
            End Using
        End Function

        Private Shared Async Function Events(c As ApiCall) As Task
            Dim svc As GatewayServices = c.Svc
            Dim opt As StreamOptions = svc.Options.Streams
            Dim cam As CameraHost = c.Camera
            Dim rec As SessionRecord = c.Rec
            If Not svc.StreamQuota.TryAcquire(c.UserName, opt.MaxPerUser, opt.MaxGlobal) Then Throw New ApiException(429, "too_many_streams") With {.RetryAfterSeconds = 5}

            Dim subscriber As SseSubscriber = Nothing
            Dim endReason As String = Nothing
            Dim clientGone As Boolean = False
            Dim watch As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Try
                Dim resp As HttpResponse = c.Http.Response
                resp.StatusCode = 200
                resp.ContentType = "text/event-stream; charset=utf-8"
                resp.Headers("Cache-Control") = "no-store"
                resp.Headers("X-Accel-Buffering") = "no"
                Dim bodyFeature As IHttpResponseBodyFeature = c.Http.Features.Get(Of IHttpResponseBodyFeature)()
                If bodyFeature IsNot Nothing Then bodyFeature.DisableBuffering()
                Dim rate As IHttpMinResponseDataRateFeature = c.Http.Features.Get(Of IHttpMinResponseDataRateFeature)()
                If rate IsNot Nothing Then rate.MinDataRate = Nothing
                c.AuditWrite("Stream.Open", "OK", cam.Id)

                subscriber = cam.Events.Subscribe()
                cam.Frames.ForceNext()
                ' الترويسات تُدفع فوراً قبل أي عمل بطيء
                If Not Await WriteTimed(c, "retry: 3000" & ControlChars.Lf & ControlChars.Lf).ConfigureAwait(False) Then Return

                Dim maxTier As Integer = FrameTier.IndexOf(svc.Options.Frames.MaxTier)
                Dim hello As New HelloWire With {
                    .CameraId = cam.Id, .ServerTimeUtc = svc.Clock.UtcNow, .FrameSeq = cam.Frames.Seq, .AlarmSeq = svc.Alarms.LastSeq, .Online = cam.Online,
                    .Engine = cam.EngineState(), .GeometryRev = cam.GeometryRev(), .HeartbeatSeconds = opt.HeartbeatSeconds,
                    .Tiers = FrameTier.Ladder.Take(maxTier + 1).Select(Function(t) t.Id).ToList()}
                If Not Await WriteTimed(c, New SseMessage("hello", Nothing, WireJson.Serialize(hello)).Format()).ConfigureAwait(False) Then Return

                Dim lastEventId As Long
                If Long.TryParse(c.Http.Request.Headers("Last-Event-ID").ToString(), Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, lastEventId) Then
                    Dim resync As Boolean
                    Dim missed As List(Of AlarmDto) = svc.Alarms.Replay(cam.Id, lastEventId, resync)
                    If resync Then
                        Await WriteTimed(c, New SseMessage("resync", Nothing, WireJson.Serialize(New ResyncWire With {.What = New List(Of String) From {"alarms"}})).Format()).ConfigureAwait(False)
                    Else
                        For Each a As AlarmDto In missed
                            If Not Await WriteTimed(c, New SseMessage("alarm", a.Seq.ToString(Globalization.CultureInfo.InvariantCulture), WireJson.Serialize(a)).Format()).ConfigureAwait(False) Then Return
                        Next
                    End If
                End If

                Dim ctlWait As Task(Of Boolean) = Nothing
                Dim ovWait As Task(Of Boolean) = Nothing
                Dim delay As Task = Nothing
                Dim lastWrite As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                Dim lastGuard As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                Dim heartbeat As TimeSpan = TimeSpan.FromSeconds(opt.HeartbeatSeconds)
                While endReason Is Nothing
                    If ctlWait Is Nothing Then ctlWait = subscriber.Control.Reader.WaitToReadAsync(c.Ct).AsTask()
                    If ovWait Is Nothing Then ovWait = subscriber.Overlay.Reader.WaitToReadAsync(c.Ct).AsTask()
                    If delay Is Nothing OrElse delay.IsCompleted Then
                        Dim remain As TimeSpan = heartbeat - lastWrite.Elapsed
                        If remain < TimeSpan.FromMilliseconds(50) Then remain = TimeSpan.FromMilliseconds(50)
                        delay = Task.Delay(remain, c.Ct)
                    End If
                    Await Task.WhenAny(ctlWait, ovWait, delay).ConfigureAwait(False)
                    c.Ct.ThrowIfCancellationRequested()

                    Dim m As SseMessage = Nothing
                    While subscriber.Control.Reader.TryRead(m)
                        If Not Await WriteTimed(c, m.Format()).ConfigureAwait(False) Then
                            clientGone = True
                            Return
                        End If
                        lastWrite.Restart()
                    End While
                    If ctlWait.IsCompleted Then
                        If Not ctlWait.Result Then endReason = "shutdown"
                        ctlWait = Nothing
                    End If
                    Dim latest As SseMessage = Nothing
                    While subscriber.Overlay.Reader.TryRead(m)
                        latest = m
                    End While
                    If latest IsNot Nothing Then
                        If Not Await WriteTimed(c, latest.Format()).ConfigureAwait(False) Then
                            clientGone = True
                            Return
                        End If
                        lastWrite.Restart()
                    End If
                    If ovWait.IsCompleted Then ovWait = Nothing
                    If subscriber.Overflowed Then endReason = "overflow"
                    If endReason Is Nothing AndAlso lastWrite.Elapsed >= heartbeat Then
                        If Not Await WriteTimed(c, ": hb" & ControlChars.Lf & ControlChars.Lf).ConfigureAwait(False) Then
                            clientGone = True
                            Return
                        End If
                        lastWrite.Restart()
                    End If
                    If endReason Is Nothing AndAlso lastGuard.Elapsed >= TimeSpan.FromSeconds(opt.GuardSeconds) Then
                        lastGuard.Restart()
                        endReason = GuardReason(c, rec, watch.Elapsed)
                    End If
                End While
            Catch ex As OperationCanceledException
                clientGone = c.Http.RequestAborted.IsCancellationRequested
                If endReason Is Nothing Then endReason = If(rec.IsRevoked, "revoked", "shutdown")
            Catch ex As IO.IOException
                clientGone = True
            Finally
                subscriber?.Dispose()
                svc.StreamQuota.Release(c.UserName)
                c.AuditWrite("Stream.Close", "OK", $"{cam.Id} seconds={CInt(watch.Elapsed.TotalSeconds)}")
            End Try
            If endReason IsNot Nothing AndAlso Not clientGone AndAlso Not c.Http.RequestAborted.IsCancellationRequested Then
                Using cts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(2))
                    Try
                        Await c.Http.Response.WriteAsync(New SseMessage("bye", Nothing, WireJson.Serialize(New ByeWire With {.Reason = endReason})).Format(), cts.Token).ConfigureAwait(False)
                        Await c.Http.Response.Body.FlushAsync(cts.Token).ConfigureAwait(False)
                    Catch ex As Exception When TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is IO.IOException
                    End Try
                End Using
            End If
        End Function

        ''' <summary>سبب إنهاء البث (أو Nothing للاستمرار): إبطال، انتهاء مطلق/خمول، تغيّر بصمة المستخدم، أو أقصى عمر للبث.</summary>
        Private Shared Function GuardReason(c As ApiCall, rec As SessionRecord, streamAge As TimeSpan) As String
            Dim svc As GatewayServices = c.Svc
            Dim now As DateTime = svc.Clock.UtcNow
            If rec.IsRevoked Then Return "revoked"
            If now >= rec.AbsoluteExpiryUtc OrElse now - rec.LastActivityUtc > TimeSpan.FromMinutes(svc.Options.Auth.IdleMinutes) Then
                svc.Sessions.Revoke(rec)
                Return "sessionExpired"
            End If
            Dim user As UserRecord = svc.Users.TryGet(rec.UserName)
            Dim stamp As String = svc.Users.GetSecurityStamp(rec.UserName) & "|" & svc.Totp.StampOf(rec.UserName)
            If user Is Nothing OrElse user.Disabled OrElse stamp <> rec.Stamp Then
                svc.Sessions.Revoke(rec)
                Return "revoked"
            End If
            If streamAge >= TimeSpan.FromMinutes(svc.Options.Streams.MaxLifetimeMinutes) Then Return "maxLifetime"
            Return Nothing
        End Function
    End Class

    ''' <summary>الكونسول الويب (ثلاثة موارد مضمَّنة بمسارات ثابتة؛ لا وصول لنظام الملفات).</summary>
    Public NotInheritable Class WebRoutes
        Private Sub New()
        End Sub

        Public Shared Sub Register(api As ApiRegistry, svc As GatewayServices)
            Dim anon As Func(Of RouteOptions) = Function() New RouteOptions With {.Anonymous = True}
            api.Map("GET", "/", Permission.None, Function(c) Serve(c, "web/index.html", "text/html; charset=utf-8", True), anon())
            api.Map("GET", "/app.js", Permission.None, Function(c) Serve(c, "web/app.js", "application/javascript; charset=utf-8", False), anon())
            api.Map("GET", "/app.css", Permission.None, Function(c) Serve(c, "web/app.css", "text/css; charset=utf-8", False), anon())
        End Sub

        Private Shared Async Function Serve(c As ApiCall, resource As String, contentType As String, isPage As Boolean) As Task
            Dim asm As Assembly = GetType(WebRoutes).Assembly
            Dim bytes As Byte()
            Using s As IO.Stream = asm.GetManifestResourceStream(resource)
                If s Is Nothing Then Throw ApiException.NotFound()
                Using ms As New IO.MemoryStream()
                    s.CopyTo(ms)
                    bytes = ms.ToArray()
                End Using
            End Using
            Dim h As IHeaderDictionary = c.Http.Response.Headers
            h("Content-Security-Policy") = Middleware.ConsoleCsp
            h("Cache-Control") = If(isPage, "no-store", "no-cache")
            c.Http.Response.StatusCode = 200
            c.Http.Response.ContentType = contentType
            c.Http.Response.ContentLength = bytes.Length
            Await c.Http.Response.Body.WriteAsync(bytes, 0, bytes.Length, c.Ct).ConfigureAwait(False)
        End Function
    End Class

End Namespace
