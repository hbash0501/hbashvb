Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Net.Security
Imports System.Security.Authentication
Imports System.Text
Imports System.Text.Json
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Isapi
Imports Ptz.Core.Security

Namespace Remote

    ''' <summary>تعذّر الوصول إلى البوابة (شبكة/مهلة/شهادة). يرث IsapiException لتعالجه المقدِّمات كأي عطل اتصال بالجهاز.</summary>
    Public NotInheritable Class GatewayUnreachableException
        Inherits IsapiException

        Public ReadOnly Property IsCertificateProblem As Boolean

        Public Sub New(message As String, Optional certificateProblem As Boolean = False)
            MyBase.New(0, "", message)
            IsCertificateProblem = certificateProblem
        End Sub
    End Class

    ''' <summary>استجابة ناجحة مقروءة بالكامل مع قياسات توقيت لتقدير جودة الوصلة.</summary>
    Public NotInheritable Class GatewayResponse
        Public Property Status As Integer
        Public Property Body As Byte() = Array.Empty(Of Byte)()
        Public Property ContentType As String = ""
        Public Property Headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        ''' <summary>زمن الوصول لترويسات الاستجابة (يتضمن انتظار الخادم).</summary>
        Public Property HeadersMs As Double
        ''' <summary>زمن تنزيل الجسم بعد وصول الترويسات (لقياس الإنتاجية).</summary>
        Public Property BodyMs As Double

        Public Function Header(name As String) As String
            Dim v As String = Nothing
            Return If(Headers.TryGetValue(name, v), v, Nothing)
        End Function

        Public Function Json(Of T)() As T
            Return JsonSerializer.Deserialize(Of T)(Body, WireJson.ClientRead)
        End Function
    End Class

    ''' <summary>
    ''' عميل HTTPS للبوابة: رمز Bearer في الذاكرة فقط، تثبيت الشهادة، بلا إعادة توجيه، سقف لحجم الاستجابة،
    ''' ومعالجة التحقق المعزَّز (يطلب رمزاً من المستخدم ويعيد الطلب مرة واحدة). الأخطاء تُترجم لاستثناءات النطاق نفسها.
    ''' </summary>
    Public NotInheritable Class GatewayClient
        Implements IDisposable

        Public Const MaxBodyBytes As Integer = 8 * 1024 * 1024
        Public Const ClientName As String = "PtzWinForms/1.0"

        Private ReadOnly _endpoint As GatewayEndpoint
        Private ReadOnly _baseUri As Uri
        Private ReadOnly _credentials As IGatewayCredentialProvider
        Private ReadOnly _http As HttpClient
        Private ReadOnly _clock As IClock
        Private ReadOnly _stepUpGate As New Threading.SemaphoreSlim(1, 1)
        Private _stepUpEpoch As Integer
        Private _token As String
        Private _me As MeDto
        Private _loginUtc As DateTime
        Private _ended As Integer

        ''' <summary>انتهت الجلسة (انتهاء/إبطال/خروج من مسؤول). يُرفع مرة واحدة؛ الوسيط هو رمز السبب.</summary>
        Public Event SessionEnded As Action(Of String)

        Public Sub New(endpoint As GatewayEndpoint, credentials As IGatewayCredentialProvider, Optional handler As HttpMessageHandler = Nothing, Optional clock As IClock = Nothing)
            If endpoint Is Nothing Then Throw New ArgumentNullException(NameOf(endpoint))
            _endpoint = endpoint
            _baseUri = endpoint.Validate()
            _credentials = credentials
            _clock = If(clock, SystemClock.Instance)
            Dim owns As Boolean = (handler Is Nothing)
            _http = New HttpClient(If(handler, CreateHandler(endpoint)), owns) With {
                .BaseAddress = _baseUri,
                .Timeout = Threading.Timeout.InfiniteTimeSpan,
                .MaxResponseContentBufferSize = MaxBodyBytes
            }
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json, image/jpeg, text/event-stream")
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(ClientName)
        End Sub

        Private Shared Function CreateHandler(endpoint As GatewayEndpoint) As HttpMessageHandler
            Dim pins As String = endpoint.PinnedCert
            Dim h As New SocketsHttpHandler With {
                .AllowAutoRedirect = False,
                .UseCookies = False,
                .UseProxy = False,
                .PreAuthenticate = False,
                .ConnectTimeout = TimeSpan.FromSeconds(endpoint.TimeoutSeconds),
                .PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            }
            h.SslOptions = New SslClientAuthenticationOptions With {
                .EnabledSslProtocols = SslProtocols.Tls12 Or SslProtocols.Tls13,
                .RemoteCertificateValidationCallback = Function(sender, cert, chain, errors) GatewayCert.IsAcceptable(pins, cert, errors)
            }
            Return h
        End Function

        Public ReadOnly Property Endpoint As GatewayEndpoint
            Get
                Return _endpoint
            End Get
        End Property

        Public ReadOnly Property IsSignedIn As Boolean
            Get
                Return Threading.Volatile.Read(_token) IsNot Nothing AndAlso Threading.Volatile.Read(_ended) = 0
            End Get
        End Property

        Public ReadOnly Property Me_ As MeDto
            Get
                Return _me
            End Get
        End Property

        ''' <summary>جلسة محلية تعكس الدور الفعلي الذي منحته البوابة (قد يكون أدنى من دور الحساب)، لفحوص الواجهة المبكرة.</summary>
        Public Function ToUserSession() As UserSession
            If _me Is Nothing Then Throw New InvalidOperationException("لم يتم تسجيل الدخول.")
            Dim role As Role
            If Not WireMap.ParseRole(_me.Role, role) Then role = Security.Role.Viewer
            Dim idle As TimeSpan = If(_me.IdleExpiresUtc > _me.ServerTimeUtc, _me.IdleExpiresUtc - _me.ServerTimeUtc, TimeSpan.FromMinutes(30))
            Return New UserSession(_me.UserName, role, _clock.UtcNow, idle)
        End Function

        ' ======================= الدخول والخروج =======================
        ''' <summary>يسجّل الدخول (كلمة المرور + TOTP). الإخفاق ⇒ AuthenticationException/ArgumentException/GatewayUnreachableException.</summary>
        Public Async Function LoginAsync(credentials As GatewayCredentials, ct As Threading.CancellationToken) As Task(Of MeDto)
            If credentials Is Nothing Then Throw New ArgumentNullException(NameOf(credentials))
            Dim failure As GatewayApiException = Nothing
            Dim ok As LoginOkDto = Nothing
            Try
                Dim resp As GatewayResponse = Await SendCoreAsync(HttpMethod.Post, "/api/v1/auth/login",
                    New LoginRequest With {.UserName = credentials.UserName, .Password = credentials.Password, .Totp = If(credentials.Totp = "", Nothing, credentials.Totp), .Client = ClientName},
                    ct, authenticated:=False, allowStepUp:=False).ConfigureAwait(False)
                ok = resp.Json(Of LoginOkDto)()
            Catch ex As GatewayApiException
                failure = ex
            End Try
            If failure IsNot Nothing Then
                Select Case failure.Status
                    Case 401, 429, 503 : Throw New Ptz.Core.Security.AuthenticationException(failure.Message & If(failure.RetryAfterSeconds.HasValue, $" ({failure.RetryAfterSeconds} ث)", ""))
                    Case Else : Throw failure.ToDomainException()
                End Select
            End If
            If ok Is Nothing OrElse String.IsNullOrEmpty(ok.AccessToken) Then Throw New GatewayUnreachableException("استجابة دخول غير صالحة من البوابة.")
            Threading.Volatile.Write(_ended, 0)
            _me = ok.Session
            _loginUtc = _clock.UtcNow
            Threading.Volatile.Write(_token, ok.AccessToken)
            Return ok.Session
        End Function

        Public Async Function LogoutAsync() As Task
            If Threading.Volatile.Read(_token) Is Nothing Then Return
            Try
                Using cts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(5))
                    Await SendCoreAsync(HttpMethod.Post, "/api/v1/auth/logout", Nothing, cts.Token, authenticated:=True, allowStepUp:=False, endSessionOn401:=False).ConfigureAwait(False)
                End Using
            Catch ex As Exception When TypeOf ex Is GatewayApiException OrElse TypeOf ex Is GatewayUnreachableException OrElse TypeOf ex Is OperationCanceledException
                ' الخروج المحلي يحدث دائماً؛ فشل إبلاغ الخادم لا يُبقي الرمز.
            End Try
            Threading.Volatile.Write(_token, Nothing)
        End Function

        ' ======================= الطلبات =======================
        Private Shared Sub ValidatePath(path As String)
            If String.IsNullOrEmpty(path) OrElse Not path.StartsWith("/api/v1/", StringComparison.Ordinal) Then Throw New ArgumentException("مسار غير صالح.")
            If path.Contains("..") OrElse path.Contains("//") OrElse path.IndexOfAny({"\"c, ChrW(0), ControlChars.Cr, ControlChars.Lf}) >= 0 Then Throw New ArgumentException("مسار غير صالح.")
        End Sub

        ''' <summary>طلب موثَّق يعيد استجابة ناجحة أو يرمي استثناء نطاق مترجَماً.</summary>
        Public Async Function RequestAsync(method As HttpMethod, path As String, body As Object, ct As Threading.CancellationToken,
                                           Optional headers As IDictionary(Of String, String) = Nothing, Optional timeoutSeconds As Integer = 0) As Task(Of GatewayResponse)
            Dim failure As GatewayApiException = Nothing
            Dim result As GatewayResponse = Nothing
            Try
                result = Await SendCoreAsync(method, path, body, ct, authenticated:=True, allowStepUp:=True, headers:=headers, timeoutSeconds:=timeoutSeconds).ConfigureAwait(False)
            Catch ex As GatewayApiException
                failure = ex
            End Try
            If failure IsNot Nothing Then Throw failure.ToDomainException()
            Return result
        End Function

        ''' <summary>كـ <see cref="RequestAsync"/> لكن يعيد الخطأ الخام (لمن يحتاج رمز الخطأ، مثل 412 للهندسة).</summary>
        Public Async Function TryRequestAsync(method As HttpMethod, path As String, body As Object, ct As Threading.CancellationToken,
                                              Optional headers As IDictionary(Of String, String) = Nothing, Optional timeoutSeconds As Integer = 0) As Task(Of (Response As GatewayResponse, [Error] As GatewayApiException))
            Try
                Dim r As GatewayResponse = Await SendCoreAsync(method, path, body, ct, authenticated:=True, allowStepUp:=True, headers:=headers, timeoutSeconds:=timeoutSeconds).ConfigureAwait(False)
                Return (r, Nothing)
            Catch ex As GatewayApiException
                Return (Nothing, ex)
            End Try
        End Function

        Public Async Function GetJsonAsync(Of T)(path As String, ct As Threading.CancellationToken) As Task(Of T)
            Dim r As GatewayResponse = Await RequestAsync(HttpMethod.Get, path, Nothing, ct).ConfigureAwait(False)
            Dim v As T = r.Json(Of T)()
            If v Is Nothing Then Throw New GatewayUnreachableException("استجابة فارغة من البوابة.")
            Return v
        End Function

        Private Async Function SendCoreAsync(method As HttpMethod, path As String, body As Object, ct As Threading.CancellationToken,
                                             authenticated As Boolean, allowStepUp As Boolean,
                                             Optional headers As IDictionary(Of String, String) = Nothing, Optional timeoutSeconds As Integer = 0,
                                             Optional endSessionOn401 As Boolean = True) As Task(Of GatewayResponse)
            ValidatePath(path)
            Dim token As String = Threading.Volatile.Read(_token)
            If authenticated AndAlso (token Is Nothing OrElse Threading.Volatile.Read(_ended) <> 0) Then
                Throw New GatewayApiException(401, New ErrorBody With {.Code = "session_expired"})
            End If
            Dim epoch As Integer = Threading.Volatile.Read(_stepUpEpoch)
            Dim attempt As Integer = 0
            While True
                attempt += 1
                Dim resp As GatewayResponse = Await TransportAsync(method, path, body, ct, If(authenticated, token, Nothing), headers, timeoutSeconds).ConfigureAwait(False)
                If resp.Status >= 200 AndAlso resp.Status < 300 Then Return resp
                Dim err As New GatewayApiException(resp.Status, ParseError(resp))
                If resp.Status = 401 AndAlso authenticated AndAlso endSessionOn401 AndAlso err.Code <> "invalid_credentials" Then EndSession(err.Code)
                If allowStepUp AndAlso attempt = 1 AndAlso resp.Status = 403 AndAlso err.Code = "step_up_required" Then
                    Dim proceed As Boolean = Await StepUpAsync(epoch, ct).ConfigureAwait(False)
                    If proceed Then
                        epoch = Threading.Volatile.Read(_stepUpEpoch)
                        Continue While
                    End If
                    Throw New GatewayApiException(403, New ErrorBody With {.Code = "forbidden", .Required = Nothing})
                End If
                Throw err
            End While
            Return Nothing
        End Function

        Private Shared Function ParseError(resp As GatewayResponse) As ErrorBody
            Try
                If resp.Body.Length > 0 AndAlso resp.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) Then
                    Dim env As ErrorEnvelope = JsonSerializer.Deserialize(Of ErrorEnvelope)(resp.Body, WireJson.ClientRead)
                    If env IsNot Nothing AndAlso env.Error IsNot Nothing AndAlso Not String.IsNullOrEmpty(env.Error.Code) Then Return env.Error
                End If
            Catch ex As JsonException
            End Try
            Dim retry As Integer
            Dim hdr As String = resp.Header("Retry-After")
            Dim body As New ErrorBody With {.Code = GenericCode(resp.Status)}
            If hdr IsNot Nothing AndAlso Integer.TryParse(hdr, retry) Then body.RetryAfterSeconds = retry
            Return body
        End Function

        Private Shared Function GenericCode(status As Integer) As String
            Select Case status
                Case 400 : Return "bad_request"
                Case 401 : Return "unauthorized"
                Case 403 : Return "forbidden"
                Case 404 : Return "not_found"
                Case 429 : Return "rate_limited"
                Case 501 : Return "unsupported"
                Case 502, 503 : Return "camera_unavailable"
                Case 504 : Return "timeout"
                Case Else : Return "internal"
            End Select
        End Function

        Private Sub EndSession(reason As String)
            Threading.Volatile.Write(_token, Nothing)
            If Threading.Interlocked.Exchange(_ended, 1) = 0 Then RaiseEvent SessionEnded(reason)
        End Sub

        ''' <summary>يُستدعى من بث SSE حين تنتهي الجلسة من جهة الخادم (bye: revoked/sessionExpired).</summary>
        Friend Sub NotifySessionEnded(reason As String)
            EndSession(reason)
        End Sub

        Private Async Function StepUpAsync(epochAtSend As Integer, ct As Threading.CancellationToken) As Task(Of Boolean)
            Await _stepUpGate.WaitAsync(ct).ConfigureAwait(False)
            Try
                ' مستخدم آخر (مسار متزامن) أنجز التحقق أثناء انتظارنا ⇒ نعيد المحاولة فقط
                If Threading.Volatile.Read(_stepUpEpoch) <> epochAtSend Then Return True
                If _credentials Is Nothing Then Return False
                Dim code As String = Await _credentials.GetStepUpCodeAsync("إجراء حساس يتطلب إدخال رمز التحقق الحالي", ct).ConfigureAwait(False)
                If String.IsNullOrWhiteSpace(code) Then Return False
                Await SendCoreAsync(HttpMethod.Post, "/api/v1/auth/stepup", New StepUpRequest With {.Totp = code.Trim()}, ct, authenticated:=True, allowStepUp:=False).ConfigureAwait(False)
                Threading.Interlocked.Increment(_stepUpEpoch)
                Return True
            Finally
                _stepUpGate.Release()
            End Try
        End Function

        ' ======================= النقل =======================
        Private Function BuildRequest(method As HttpMethod, path As String, body As Object, token As String, headers As IDictionary(Of String, String)) As HttpRequestMessage
            Dim req As New HttpRequestMessage(method, path.TrimStart("/"c))
            If token IsNot Nothing Then req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", token)
            If body IsNot Nothing Then
                Dim json As String = If(TypeOf body Is String, CStr(body), JsonSerializer.Serialize(body, body.GetType(), WireJson.Write))
                req.Content = New StringContent(json, Encoding.UTF8, "application/json")
            End If
            If headers IsNot Nothing Then
                For Each kv In headers
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value)
                Next
            End If
            Return req
        End Function

        Private Async Function TransportAsync(method As HttpMethod, path As String, body As Object, ct As Threading.CancellationToken, token As String,
                                               headers As IDictionary(Of String, String), timeoutSeconds As Integer) As Task(Of GatewayResponse)
            Dim limit As Integer = If(timeoutSeconds > 0, timeoutSeconds, _endpoint.TimeoutSeconds)
            Dim problem As GatewayUnreachableException = Nothing
            Dim result As GatewayResponse = Nothing
            Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                cts.CancelAfter(TimeSpan.FromSeconds(limit))
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                Try
                    Using req As HttpRequestMessage = BuildRequest(method, path, body, token, headers)
                        Using resp As HttpResponseMessage = Await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(False)
                            Dim headersMs As Double = sw.Elapsed.TotalMilliseconds
                            sw.Restart()
                            Dim bytes As Byte() = Await ReadCappedAsync(resp, cts.Token).ConfigureAwait(False)
                            result = New GatewayResponse With {.Status = CInt(resp.StatusCode), .Body = bytes, .HeadersMs = headersMs, .BodyMs = sw.Elapsed.TotalMilliseconds,
                                                               .ContentType = If(resp.Content.Headers.ContentType?.ToString(), "")}
                            For Each h In resp.Headers
                                result.Headers(h.Key) = String.Join(",", h.Value)
                            Next
                            For Each h In resp.Content.Headers
                                result.Headers(h.Key) = String.Join(",", h.Value)
                            Next
                        End Using
                    End Using
                Catch ex As OperationCanceledException When Not ct.IsCancellationRequested
                    problem = New GatewayUnreachableException("انتهت مهلة الاتصال بالبوابة.")
                Catch ex As Exception When ct.IsCancellationRequested AndAlso (TypeOf ex Is HttpRequestException OrElse TypeOf ex Is IO.IOException)
                    ' إلغاء مقصود من المستدعي قد يظهر كخطأ إدخال/إخراج من النقل: يبقى إلغاءً لا عطلاً
                    Throw New OperationCanceledException(ct)
                Catch ex As HttpRequestException
                    problem = Describe(ex)
                Catch ex As IO.IOException
                    problem = New GatewayUnreachableException("انقطع الاتصال بالبوابة.")
                End Try
            End Using
            If problem IsNot Nothing Then Throw problem
            Return result
        End Function

        Private Shared Function Describe(ex As HttpRequestException) As GatewayUnreachableException
            Dim inner As Exception = ex
            While inner IsNot Nothing
                If TypeOf inner Is System.Security.Authentication.AuthenticationException Then
                    Return New GatewayUnreachableException("تعذّر التحقق من شهادة البوابة (بصمة غير مطابقة أو شهادة غير موثوقة). لن يُرسل أي بيان دخول.", certificateProblem:=True)
                End If
                inner = inner.InnerException
            End While
            Return New GatewayUnreachableException("تعذّر الاتصال بالبوابة.")
        End Function

        Private Shared Async Function ReadCappedAsync(resp As HttpResponseMessage, ct As Threading.CancellationToken) As Task(Of Byte())
            Dim declared As Long? = resp.Content.Headers.ContentLength
            If declared.HasValue AndAlso declared.Value > MaxBodyBytes Then Throw New GatewayUnreachableException("استجابة البوابة أكبر من المسموح.")
            Using s As IO.Stream = Await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(False)
                Using ms As New IO.MemoryStream()
                    Dim buf(16383) As Byte
                    While True
                        Dim n As Integer = Await s.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(False)
                        If n = 0 Then Exit While
                        If ms.Length + n > MaxBodyBytes Then Throw New GatewayUnreachableException("استجابة البوابة أكبر من المسموح.")
                        ms.Write(buf, 0, n)
                    End While
                    Return ms.ToArray()
                End Using
            End Using
        End Function

        ''' <summary>يفتح بث SSE (بلا مهلة كلية). الاستجابة غير الناجحة تُترجم لاستثناء؛ المستدعي يملك الاستجابة الناجحة ويتخلص منها.</summary>
        Friend Async Function OpenStreamAsync(path As String, lastEventId As String, ct As Threading.CancellationToken) As Task(Of HttpResponseMessage)
            ValidatePath(path)
            Dim token As String = Threading.Volatile.Read(_token)
            If token Is Nothing OrElse Threading.Volatile.Read(_ended) <> 0 Then Throw New GatewayApiException(401, New ErrorBody With {.Code = "session_expired"})
            Dim problem As GatewayUnreachableException = Nothing
            Dim resp As HttpResponseMessage = Nothing
            Try
                Using req As HttpRequestMessage = BuildRequest(HttpMethod.Get, path, Nothing, token, Nothing)
                    If lastEventId IsNot Nothing Then req.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId)
                    Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                        cts.CancelAfter(TimeSpan.FromSeconds(_endpoint.TimeoutSeconds))   ' مهلة الترويسات فقط
                        resp = Await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(False)
                    End Using
                End Using
            Catch ex As OperationCanceledException When Not ct.IsCancellationRequested
                problem = New GatewayUnreachableException("انتهت مهلة فتح بث الأحداث.")
            Catch ex As HttpRequestException
                problem = Describe(ex)
            End Try
            If problem IsNot Nothing Then Throw problem
            If resp.IsSuccessStatusCode Then Return resp
            Dim code As Integer = CInt(resp.StatusCode)
            Dim bytes As Byte() = Array.Empty(Of Byte)()
            Try
                bytes = Await ReadCappedAsync(resp, ct).ConfigureAwait(False)
            Catch ex As Exception When TypeOf ex Is GatewayUnreachableException OrElse TypeOf ex Is HttpRequestException OrElse TypeOf ex Is IO.IOException
            End Try
            Dim holder As New GatewayResponse With {.Status = code, .Body = bytes, .ContentType = If(resp.Content.Headers.ContentType?.ToString(), "")}
            If resp.Headers.RetryAfter IsNot Nothing AndAlso resp.Headers.RetryAfter.Delta.HasValue Then holder.Headers("Retry-After") = CInt(resp.Headers.RetryAfter.Delta.Value.TotalSeconds).ToString(Globalization.CultureInfo.InvariantCulture)
            resp.Dispose()
            Dim err As New GatewayApiException(code, ParseError(holder))
            If code = 401 AndAlso err.Code <> "invalid_credentials" Then EndSession(err.Code)
            Throw err
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            Threading.Volatile.Write(_token, Nothing)
            _http.Dispose()
            _stepUpGate.Dispose()
        End Sub
    End Class

End Namespace
