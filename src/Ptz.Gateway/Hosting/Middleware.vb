Imports System.Net
Imports System.Threading.RateLimiting
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.RateLimiting
Imports Ptz.Gateway.Api

Namespace Hosting

    ''' <summary>
    ''' طبقات الأمان العامة قبل المسارات: ترويسات أمان، معرّف الطلب، تصفية المضيف، HTTPS إلزامي لغير الحلقة المحلية،
    ''' وتحديد معدل يُختار من البيانات الوصفية للمسار لا من نص المسار (فلا يتجاوزه تغيير الحالة أو الشرطة الأخيرة).
    ''' </summary>
    Public NotInheritable Class Middleware
        Private Sub New()
        End Sub

        Public Const ConsoleCsp As String = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' blob:; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'"
        Public Const ApiCsp As String = "default-src 'none'; frame-ancestors 'none'"

        ''' <summary>معرّف الطلب + ترويسات الأمان (تُضبط داخل OnStarting فتنجو من Response.Clear).</summary>
        Public Shared Function SecurityHeaders(ctx As HttpContext, nxt As RequestDelegate) As Task
            Dim rid As String = Guid.NewGuid().ToString("N")
            ctx.Items("rid") = rid
            ctx.Response.OnStarting(
                Function() As Task
                    Dim h As IHeaderDictionary = ctx.Response.Headers
                    h("X-Request-Id") = rid
                    h("X-Api-Version") = "1"
                    h("X-Content-Type-Options") = "nosniff"
                    h("Referrer-Policy") = "no-referrer"
                    h("X-Frame-Options") = "DENY"
                    h("Cross-Origin-Resource-Policy") = "same-origin"
                    h("Cross-Origin-Opener-Policy") = "same-origin"
                    h("Permissions-Policy") = "camera=(), microphone=(), geolocation=(), payment=()"
                    If Not h.ContainsKey("Content-Security-Policy") Then h("Content-Security-Policy") = ApiCsp
                    If Not h.ContainsKey("Cache-Control") Then h("Cache-Control") = "no-store"
                    If ctx.Request.IsHttps Then h("Strict-Transport-Security") = "max-age=31536000; includeSubDomains"
                    h.Remove("Server")
                    Return Task.CompletedTask
                End Function)
            Return nxt(ctx)
        End Function

        Public Shared Function HostFilter(allowed As IReadOnlyList(Of String)) As Func(Of HttpContext, RequestDelegate, Task)
            Dim set_ As New HashSet(Of String)(allowed.Select(Function(h) h.Trim().ToLowerInvariant()))
            Return Async Function(ctx As HttpContext, nxt As RequestDelegate) As Task
                       Dim host As String = ctx.Request.Host.Host.ToLowerInvariant()
                       If Not set_.Contains(host) Then
                           Await JsonHttp.WriteErrorAsync(ctx, ApiException.BadRequest(), CStr(ctx.Items("rid"))).ConfigureAwait(False)
                           Return
                       End If
                       Await nxt(ctx).ConfigureAwait(False)
                   End Function
        End Function

        Public Shared Async Function RequireHttpsUnlessLoopback(ctx As HttpContext, nxt As RequestDelegate) As Task
            Dim ip As IPAddress = ctx.Connection.RemoteIpAddress
            Dim loopback As Boolean = ip IsNot Nothing AndAlso IPAddress.IsLoopback(ip)
            If Not ctx.Request.IsHttps AndAlso Not loopback Then
                Await JsonHttp.WriteErrorAsync(ctx, New ApiException(403, "https_required"), CStr(ctx.Items("rid"))).ConfigureAwait(False)
                Return
            End If
            Await nxt(ctx).ConfigureAwait(False)
        End Function

        ''' <summary>مفتاح التقسيم لكل عنوان؛ IPv6 يُجمَّع على /64 كي لا يدور المهاجم على عناوين الشبكة نفسها.</summary>
        Public Shared Function IpKey(ctx As HttpContext) As String
            Dim ip As IPAddress = ctx.Connection.RemoteIpAddress
            If ip Is Nothing Then Return "unknown"
            If ip.IsIPv4MappedToIPv6 Then ip = ip.MapToIPv4()
            If ip.AddressFamily = Sockets.AddressFamily.InterNetworkV6 Then
                Dim b As Byte() = ip.GetAddressBytes()
                Return Convert.ToHexString(b, 0, 8)
            End If
            Return ip.ToString()
        End Function

        Public Shared Function Partition(ctx As HttpContext) As RateLimitPartition(Of String)
            Dim route As RouteInfo = ctx.GetEndpoint()?.Metadata.GetMetadata(Of RouteInfo)()
            Dim key As String = IpKey(ctx)
            If route IsNot Nothing AndAlso route.Options.IsLogin Then
                Return RateLimitPartition.GetSlidingWindowLimiter("auth:" & key,
                    Function(k) New SlidingWindowRateLimiterOptions With {.PermitLimit = 10, .Window = TimeSpan.FromMinutes(10), .SegmentsPerWindow = 5, .QueueLimit = 0, .AutoReplenishment = True})
            End If
            Return RateLimitPartition.GetFixedWindowLimiter("ip:" & key,
                Function(k) New FixedWindowRateLimiterOptions With {.PermitLimit = 300, .Window = TimeSpan.FromSeconds(5), .QueueLimit = 0, .AutoReplenishment = True})
        End Function

        Public Shared Async Function WriteRejectedAsync(context As OnRejectedContext, ct As Threading.CancellationToken) As Task
            Dim retry As TimeSpan
            Dim seconds As Integer = 30
            If context.Lease.TryGetMetadata(MetadataName.RetryAfter, retry) Then seconds = Math.Max(1, CInt(Math.Ceiling(retry.TotalSeconds)))
            Dim ae As New ApiException(429, "rate_limited") With {.RetryAfterSeconds = seconds}
            Await JsonHttp.WriteErrorAsync(context.HttpContext, ae, CStr(context.HttpContext.Items("rid"))).ConfigureAwait(False)
        End Function

        ''' <summary>أجسام استجابة JSON موحّدة لحالات 404/405 التي يولّدها التوجيه نفسه (بلا جسم).</summary>
        Public Shared Async Function WriteStatusAsync(ctx As HttpContext) As Task
            Dim status As Integer = ctx.Response.StatusCode
            Dim ae As ApiException
            Select Case status
                Case 404 : ae = ApiException.NotFound()
                Case 405
                    ae = New ApiException(405, "method_not_allowed")
                    Dim allow As String = ctx.Response.Headers("Allow").ToString()
                    If Not String.IsNullOrEmpty(allow) Then ae.ExtraHeaders("Allow") = allow
                Case 400 : ae = ApiException.BadRequest()
                Case 413 : ae = New ApiException(413, "payload_too_large")
                Case Else : ae = New ApiException(status, If(status >= 500, "internal", "bad_request"))
            End Select
            ctx.Response.StatusCode = status
            Await JsonHttp.WriteErrorAsync(ctx, ae, CStr(ctx.Items("rid"))).ConfigureAwait(False)
        End Function
    End Class

End Namespace
