Imports System.Text.Json
Imports Microsoft.AspNetCore.Http
Imports Ptz.Core.Remote

Namespace Api

    ''' <summary>مساعدات HTTP للكتابة/القراءة (بلا ASP.NET Mvc: كتابة مباشرة للجسم).</summary>
    Public NotInheritable Class JsonHttp
        Private Sub New()
        End Sub

        Public Shared Async Function WriteJsonAsync(ctx As HttpContext, status As Integer, value As Object) As Task
            Dim bytes As Byte() = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), WireJson.Write)
            ctx.Response.StatusCode = status
            ctx.Response.ContentType = "application/json; charset=utf-8"
            ctx.Response.ContentLength = bytes.Length
            Await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length, ctx.RequestAborted).ConfigureAwait(False)
        End Function

        Public Shared Async Function WriteErrorAsync(ctx As HttpContext, ae As ApiException, requestId As String) As Task
            If ctx.Response.HasStarted Then Return
            ctx.Response.Clear()
            ctx.Response.Headers("X-Request-Id") = requestId
            If ae.RetryAfterSeconds.HasValue Then ctx.Response.Headers("Retry-After") = ae.RetryAfterSeconds.Value.ToString(Globalization.CultureInfo.InvariantCulture)
            For Each kv In ae.ExtraHeaders
                ctx.Response.Headers(kv.Key) = kv.Value
            Next
            Await WriteJsonAsync(ctx, ae.Status, ErrorMap.ToEnvelope(ae, requestId)).ConfigureAwait(False)
        End Function
    End Class

End Namespace
