Imports System.Net
Imports System.Text.Json
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Net.Http.Headers
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Cameras
Imports Ptz.Gateway.Hosting

Namespace Api

    Public Enum RateClass
        None
        Ptz
        Cmd
        StepUp
        Heavy
        AuditList
        Verify
        Connect
        ''' <summary>فتح بثوث SSE: يمنع حلقة فتح/إغلاق من إغراق السجل (كل فتح/إغلاق يُدقَّق).</summary>
        [Stream]
    End Enum

    ''' <summary>خيارات مسار: كل مسار يُسجَّل حصراً عبر <see cref="ApiRegistry.Map"/> فيكون رفض الافتراضي بنيوياً.</summary>
    Public NotInheritable Class RouteOptions
        Public Property Anonymous As Boolean
        Public Property StepUp As Boolean
        Public Property Mutates As Boolean
        Public Property RateClass As RateClass = RateClass.None
        Public Property MaxBodyBytes As Integer = 16384
        ''' <summary>0 = بلا مهلة (البثوث).</summary>
        Public Property TimeoutSeconds As Integer = 15
        ''' <summary>مسار الدخول: يُحدَّد تقييد المعدل الصارم من بيانات المسار الوصفية لا من نص المسار الخام.</summary>
        Public Property IsLogin As Boolean
        ''' <summary>عمليات احتواء (إيقاف PTZ، تسجيل الخروج) لا تُحجب أبداً بسبب تعطل التدقيق.</summary>
        Public Property AuditExempt As Boolean
    End Class

    Public NotInheritable Class RouteInfo
        Public ReadOnly Property Method As String
        Public ReadOnly Property Pattern As String
        Public ReadOnly Property Permission As Permission
        Public ReadOnly Property Options As RouteOptions
        Public ReadOnly Property Handler As Func(Of ApiCall, Task)

        Public Sub New(method As String, pattern As String, permission As Permission, options As RouteOptions, handler As Func(Of ApiCall, Task))
            Me.Method = method
            Me.Pattern = pattern
            Me.Permission = permission
            Me.Options = options
            Me.Handler = handler
        End Sub

        Public ReadOnly Property NeedsCamera As Boolean
            Get
                Return Pattern.Contains("{cam}")
            End Get
        End Property
    End Class

    ''' <summary>سياق استدعاء واحد بعد اجتياز بوابة الدخول (مصادقة، صلاحية، معدل): هذا كل ما يراه المعالج.</summary>
    Public NotInheritable Class ApiCall
        Public ReadOnly Property Http As HttpContext
        Public ReadOnly Property Svc As GatewayServices
        Public ReadOnly Property Route As RouteInfo
        Public ReadOnly Property RequestId As String
        Public Property Rec As SessionRecord
        Public Property Camera As CameraHost
        Public Property Ct As Threading.CancellationToken

        Friend Sub New(http As HttpContext, svc As GatewayServices, route As RouteInfo, requestId As String)
            Me.Http = http
            Me.Svc = svc
            Me.Route = route
            Me.RequestId = requestId
            Ct = http.RequestAborted
        End Sub

        Public ReadOnly Property User As UserSession
            Get
                Return Rec?.UserSession
            End Get
        End Property

        Public ReadOnly Property UserName As String
            Get
                Return If(Rec Is Nothing, "(anonymous)", Rec.UserName)
            End Get
        End Property

        Public ReadOnly Property ClientIp As String
            Get
                Return IpOf(Http)
            End Get
        End Property

        Public Shared Function IpOf(ctx As HttpContext) As String
            Dim ip As IPAddress = ctx.Connection.RemoteIpAddress
            If ip Is Nothing Then Return "unknown"
            If ip.IsIPv4MappedToIPv6 Then ip = ip.MapToIPv4()
            Return ip.ToString()
        End Function

        ''' <summary>المتحكم المؤمَّن لهذه الجلسة وهذه الكاميرا (يفرض الصلاحية ويدقّق).</summary>
        Public ReadOnly Property Controller As SecuredCameraController
            Get
                If Camera.Raw Is Nothing Then Throw New ApiException(503, "camera_unavailable")
                Return Camera.SecuredFor(Rec)
            End Get
        End Property

        Public Function RouteValue(name As String) As String
            Dim v As Object = Nothing
            If Http.Request.RouteValues.TryGetValue(name, v) AndAlso v IsNot Nothing Then Return Convert.ToString(v, Globalization.CultureInfo.InvariantCulture)
            Return Nothing
        End Function

        ''' <summary>رقم صحيح من المسار ضمن [1, max] وإلا 400.</summary>
        Public Function RouteInt(name As String, max As Integer) As Integer
            Dim v As Integer
            If Not Integer.TryParse(RouteValue(name), Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, v) OrElse v < 1 OrElse v > max Then
                Throw ApiException.BadRequest(name)
            End If
            Return v
        End Function

        Public Function Query(name As String) As String
            Dim v As Microsoft.Extensions.Primitives.StringValues = Http.Request.Query(name)
            Return If(v.Count = 0, Nothing, v(0))
        End Function

        Public Function QueryLong(name As String, defaultValue As Long, lo As Long, hi As Long) As Long
            Dim s As String = Query(name)
            If s Is Nothing Then Return defaultValue
            Dim v As Long
            If Not Long.TryParse(s, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, v) OrElse v < lo OrElse v > hi Then Throw ApiException.BadRequest(name)
            Return v
        End Function

        ''' <summary>يقرأ جسم JSON بسقف بايتات صارم (يعدّ البايتات الفعلية لا ترويسة الطول فقط) ويتحقق من النوع.</summary>
        Public Async Function ReadJsonAsync(Of T As Class)() As Task(Of T)
            Dim contentType As String = Http.Request.ContentType
            Dim mt As MediaTypeHeaderValue = Nothing
            If String.IsNullOrEmpty(contentType) OrElse Not MediaTypeHeaderValue.TryParse(contentType, mt) OrElse Not String.Equals(mt.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase) Then
                Throw New ApiException(415, "unsupported_media_type")
            End If
            Dim max As Integer = Route.Options.MaxBodyBytes
            If Http.Request.ContentLength.HasValue AndAlso Http.Request.ContentLength.Value > max Then Throw New ApiException(413, "payload_too_large")
            Dim ms As New IO.MemoryStream()
            Dim buf(4095) As Byte
            Dim total As Integer = 0
            While True
                Dim n As Integer = Await Http.Request.Body.ReadAsync(buf, 0, buf.Length, Ct).ConfigureAwait(False)
                If n = 0 Then Exit While
                total += n
                If total > max Then Throw New ApiException(413, "payload_too_large")
                ms.Write(buf, 0, n)
            End While
            If total = 0 Then Throw ApiException.BadRequest()
            Dim value As T = Nothing
            Dim bad As Boolean = False
            Try
                value = JsonSerializer.Deserialize(Of T)(ms.ToArray(), WireJson.ServerRead)
            Catch ex As JsonException
                bad = True
            End Try
            If bad OrElse value Is Nothing Then Throw ApiException.BadRequest()
            Return value
        End Function

        Public Function NoContent() As Task
            Http.Response.StatusCode = 204
            Return Task.CompletedTask
        End Function

        Public Function Ok(Of T)(value As T) As Task
            Return JsonHttp.WriteJsonAsync(Http, 200, value)
        End Function

        ''' <summary>يكتب سجل تدقيق باسم المستخدم البعيد الحقيقي (سياق الطلب يضيف IP/الجلسة تلقائياً).</summary>
        Public Sub AuditWrite(action As String, outcome As String, details As String)
            Svc.Audit.Write(UserName, action, If(Camera Is Nothing, "gateway", Camera.Profile.ModelName), outcome, details)
        End Sub

        ''' <summary>فحص صلاحية إضافية داخل المعالج (لحقول متعددة الصلاحيات). يدقّق الرفض ويرمي AccessDeniedException.</summary>
        Public Sub Authorize(permission As Permission, action As String)
            Try
                Svc.Access.Demand(Rec.UserSession, permission, touch:=False)
            Catch ex As AccessDeniedException
                AuditWrite(action, "DENIED", ex.Message)
                Throw
            End Try
        End Sub

        ''' <summary>يشترط خطوة تحقق حديثة (TOTP خلال المدة) ما لم يكن TOTP غير مفعّل ومعفى بالإعداد.</summary>
        Public Sub RequireStepUp()
            If Rec Is Nothing Then Return
            If Not Svc.Options.Auth.RequireTotp AndAlso Not Svc.Totp.IsEnrolled(Rec.UserName) Then Return
            If Rec.StepUpUntilUtc <= Svc.Clock.UtcNow Then Throw New ApiException(403, "step_up_required")
        End Sub
    End Class

End Namespace
