Imports System.Net.Http
Imports System.Net.Security
Imports System.Security.Authentication
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports Ptz.Core.Abstractions

Namespace Cloud

    ''' <summary>تصنيف نتيجة نداء سحابي لأغراض الصحة: لا يُخلط انقطاع الجهاز بتقييد المعدل أو برفض المفاتيح.</summary>
    Public Enum HppOutcome
        Ok = 0
        DeviceUnreachable = 1
        AuthRejected = 2
        RateLimited = 3
        Rejected = 4
    End Enum

    ''' <summary>خطأ من خدمة Hik-Partner Pro (رمز errorCode منها). لا تحمل رسالته مفاتيح ولا رمز وصول أبداً.</summary>
    Public NotInheritable Class HppCloudException
        Inherits Exception

        Public ReadOnly Property Code As String
        Public ReadOnly Property IsAuthFailure As Boolean

        Public Sub New(code As String, message As String, isAuthFailure As Boolean)
            MyBase.New(message)
            Me.Code = code
            Me.IsAuthFailure = isAuthFailure
        End Sub
    End Class

    Public NotInheritable Class HppCallEventArgs
        Inherits EventArgs
        Public ReadOnly Property Serial As String
        Public ReadOnly Property Outcome As HppOutcome
        Public ReadOnly Property Code As String
        Public ReadOnly Property HttpStatus As Integer
        ''' <summary>ترويستا X-DeviceCode وX-EZO-Code (منقّاتان) إن وُجدتا — للتشخيص فقط.</summary>
        Public ReadOnly Property DeviceCode As String
        Public ReadOnly Property EzoCode As String

        Public Sub New(serial As String, outcome As HppOutcome, code As String, httpStatus As Integer, Optional deviceCode As String = "", Optional ezoCode As String = "")
            Me.Serial = serial
            Me.Outcome = outcome
            Me.Code = code
            Me.HttpStatus = httpStatus
            Me.DeviceCode = If(deviceCode, "")
            Me.EzoCode = If(ezoCode, "")
        End Sub
    End Class

    ''' <summary>جهاز كما تعيده POST /api/hpcgw/v1/device/list (حقول التعريف فقط).</summary>
    Public NotInheritable Class HppDevice
        Public Property Serial As String = ""
        Public Property Name As String = ""
        Public Property DeviceType As String = ""
        Public Property Category As Integer
        ''' <summary>0 غير متصل، 1 متصل، 2 غير معروف.</summary>
        Public Property OnlineStatus As Integer
        Public Property SiteName As String = ""
    End Class

    ''' <summary>نتيجة نداء JSON عام: errorCode ("0" نجاح) و"data" المستقلة عن المستند.</summary>
    Public NotInheritable Class HppApiResult
        Public Property HttpStatus As Integer
        Public Property Code As String = ""
        Public Property Data As JsonElement
    End Class

    ''' <summary>رمز وصول مخبّأ: يُجدَّد قبل انتهائه، ونطاق الخدمة (areaDomain) محقَّق من قائمة اللواحق المسموحة.</summary>
    Friend NotInheritable Class HppAuth
        Public ReadOnly Property Token As String
        ''' <summary>"https://host" بلا مسار.</summary>
        Public ReadOnly Property Area As String
        Public ReadOnly Property RefreshAtUtc As DateTime
        ''' <summary>الانتهاء الفعلي (expireTime): حتى هذه اللحظة يبقى الرمز صالحاً للاستعمال إن تعذّر التجديد.</summary>
        Public ReadOnly Property ExpiresUtc As DateTime

        Public Sub New(token As String, area As String, refreshAtUtc As DateTime, expiresUtc As DateTime)
            Me.Token = token
            Me.Area = area
            Me.RefreshAtUtc = refreshAtUtc
            Me.ExpiresUtc = expiresUtc
        End Sub
    End Class

    ''' <summary>
    ''' محدِّد معدل (دلو رموز) لنداءات السحابة، مع إيقاف مؤقت عند تلقي رد "تجاوز المعدل". يعتمد ساعة Stopwatch لا ساعة النظام.
    ''' أوامر الإيقاف تتجاوزه (لا تُحجَب خلف طابور حركات).
    ''' </summary>
    Public NotInheritable Class HppRateLimiter
        Private ReadOnly _lock As New Object()
        Private ReadOnly _rate As Double
        Private ReadOnly _burst As Double
        Private ReadOnly _now As Func(Of Long)
        Private ReadOnly _freq As Double
        Private _tokens As Double
        Private _last As Long
        Private _blockedUntil As Long

        Public Sub New(ratePerSecond As Double, burst As Integer, Optional nowTicks As Func(Of Long) = Nothing, Optional ticksPerSecond As Double = 0)
            If ratePerSecond <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(ratePerSecond))
            _rate = ratePerSecond
            _burst = Math.Max(1, burst)
            _tokens = _burst
            If nowTicks Is Nothing Then
                _now = Function() Diagnostics.Stopwatch.GetTimestamp()
                _freq = Diagnostics.Stopwatch.Frequency
            Else
                _now = nowTicks
                _freq = If(ticksPerSecond > 0, ticksPerSecond, 1000.0)
            End If
            _last = _now()
        End Sub

        ''' <summary>يحجز رمزاً: صفر = مُنح؛ غير ذلك = مدة الانتظار المقدَّرة.</summary>
        Public Function Reserve() As TimeSpan
            SyncLock _lock
                Dim t As Long = _now()
                If t < _blockedUntil Then Return TimeSpan.FromSeconds((_blockedUntil - t) / _freq)
                Dim elapsed As Double = Math.Max(0, (t - _last) / _freq)
                _last = t
                _tokens = Math.Min(_burst, _tokens + elapsed * _rate)
                If _tokens >= 1.0 Then
                    _tokens -= 1.0
                    Return TimeSpan.Zero
                End If
                Return TimeSpan.FromSeconds((1.0 - _tokens) / _rate)
            End SyncLock
        End Function

        ''' <summary>رد تجاوز معدل من السحابة: يوقف الطلبات غير الإيقافية مدة قصيرة.</summary>
        Public Sub Penalize(seconds As Double)
            SyncLock _lock
                Dim until As Long = _now() + CLng(seconds * _freq)
                If until > _blockedUntil Then _blockedUntil = until
                _tokens = 0
                _last = _blockedUntil     ' لا يُحتسب زمن الحجب رصيداً: بعده يعود المعدل تدريجياً لا دفعة كاملة
            End SyncLock
        End Sub

        ''' <summary>ينتظر دوره حتى <paramref name="maxWait"/>؛ False إن كان سيتجاوزه (الطالب يفشل بدل أن يتراكم).</summary>
        Public Async Function WaitAsync(maxWait As TimeSpan, ct As Threading.CancellationToken) As Task(Of Boolean)
            Dim waited As TimeSpan = TimeSpan.Zero
            While True
                Dim w As TimeSpan = Reserve()
                If w <= TimeSpan.Zero Then Return True
                If waited + w > maxWait Then Return False
                Await Task.Delay(w, ct).ConfigureAwait(False)
                waited += w
            End While
            Return False
        End Function
    End Class

    Friend NotInheritable Class HppTokenRequest
        <JsonPropertyName("appKey")>
        Public Property AppKey As String
        <JsonPropertyName("secretKey")>
        Public Property SecretKey As String
    End Class

    ''' <summary>
    ''' عميل Hik-Partner Pro OpenAPI (دليل V2.15.500): رمز وصول (POST /api/hpcgw/v1/token/get، صالح 7 أيام، Authorization: Bearer)
    ''' يُجلب من النطاق العام ثم تُوجَّه كل النداءات إلى areaDomain الذي يعيده الجلب. الرمز لا يُرسَل إلى أي مضيف خارج اللواحق المسموحة،
    ''' ولا تظهر المفاتيح/الرمز في أي رسالة خطأ أو حدث. مشترك بين كل كاميرات الترحيل (رمز واحد).
    ''' </summary>
    Public NotInheritable Class HppCloud
        Implements IDisposable

        Private Shared ReadOnly MaxRefreshMargin As TimeSpan = TimeSpan.FromHours(12)
        Private Shared ReadOnly AuthFailBackoff As TimeSpan = TimeSpan.FromSeconds(60)
        Private Shared ReadOnly TransientBackoff As TimeSpan = TimeSpan.FromSeconds(2)

        Private ReadOnly _options As HppOptions
        Private ReadOnly _appKey As String
        Private ReadOnly _secretKey As String
        Private ReadOnly _clock As IClock
        Private ReadOnly _http As HttpClient
        Private ReadOnly _tokenUri As Uri
        Private ReadOnly _sync As New Object()
        Private _inflight As Task(Of HppAuth)
        Private _auth As HppAuth
        Private _failure As HppCloudException
        Private _transientUntil As DateTime = DateTime.MinValue
        Private _failureUntil As DateTime = DateTime.MinValue

        Public ReadOnly Property Policy As HppPathPolicy
        Public ReadOnly Property Limiter As HppRateLimiter
        Public ReadOnly Property Options As HppOptions
            Get
                Return _options
            End Get
        End Property

        ''' <summary>يُطلق بعد كل نداء ترحيل (من خيط المعالج): للمشتركين أن يكونوا سريعين وغير رامين.</summary>
        Public Event CallCompleted As EventHandler(Of HppCallEventArgs)

        Public Sub New(options As HppOptions, appKey As String, secretKey As String, clock As IClock, Optional handler As HttpMessageHandler = Nothing)
            If options Is Nothing Then Throw New ArgumentNullException(NameOf(options))
            If String.IsNullOrWhiteSpace(appKey) OrElse String.IsNullOrWhiteSpace(secretKey) Then Throw New ArgumentException("مفاتيح Hik-Partner Pro مطلوبة.")
            If clock Is Nothing Then Throw New ArgumentNullException(NameOf(clock))
            _options = options
            _appKey = appKey.Trim()
            _secretKey = secretKey.Trim()
            _clock = clock
            _tokenUri = ParseTokenUrl(options)
            Policy = New HppPathPolicy(options.ExtraAllowed)
            Limiter = New HppRateLimiter(Math.Max(0.1, options.MaxRequestsPerSecond), Math.Max(1, options.MaxBurst))
            If handler Is Nothing Then
                handler = New SocketsHttpHandler With {
                    .AllowAutoRedirect = False,
                    .UseCookies = False,
                    .ConnectTimeout = TimeSpan.FromSeconds(10),
                    .PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    .SslOptions = New SslClientAuthenticationOptions With {.EnabledSslProtocols = SslProtocols.Tls12 Or SslProtocols.Tls13}
                }
            End If
            _http = New HttpClient(handler) With {.Timeout = Threading.Timeout.InfiniteTimeSpan, .MaxResponseContentBufferSize = 2 * 1024 * 1024}
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json, application/xml")
        End Sub

        ' ---------------- التحقق من العناوين ----------------
        ''' <summary>هل المضيف مطابق لإحدى اللواحق (مضيف تام أو نطاق فرعي)؟ المقارنة بلا حساسية لحالة الأحرف.</summary>
        Public Shared Function HostAllowed(host As String, suffixes As IEnumerable(Of String)) As Boolean
            If String.IsNullOrWhiteSpace(host) OrElse suffixes Is Nothing Then Return False
            Dim h As String = host.Trim().TrimEnd("."c).ToLowerInvariant()
            For Each raw As String In suffixes
                If String.IsNullOrWhiteSpace(raw) Then Continue For
                Dim s As String = raw.Trim().TrimStart("."c).ToLowerInvariant()
                If s.Contains("*") OrElse s.IndexOf("."c) < 0 Then Continue For ' لاحقة مفردة (com) أو بنمط: مرفوضة
                If h = s OrElse h.EndsWith("." & s, StringComparison.Ordinal) Then Return True
            Next
            Return False
        End Function

        ''' <summary>يتحقق من عنوان خدمة: https فقط، بلا بيانات اعتماد/منفذ مخصّص/مسار، ومضيفه ضمن اللواحق. يعيد "https://host" أو Nothing.</summary>
        Public Shared Function NormalizeServiceUrl(url As String, suffixes As IEnumerable(Of String)) As String
            Dim u As Uri = Nothing
            If Not Uri.TryCreate(url, UriKind.Absolute, u) Then Return Nothing
            If u.Scheme <> Uri.UriSchemeHttps OrElse Not String.IsNullOrEmpty(u.UserInfo) OrElse Not u.IsDefaultPort Then Return Nothing
            If u.AbsolutePath <> "/" OrElse Not String.IsNullOrEmpty(u.Query) OrElse Not String.IsNullOrEmpty(u.Fragment) Then Return Nothing
            If Not HostAllowed(u.Host, suffixes) Then Return Nothing
            Return "https://" & u.Host.ToLowerInvariant()
        End Function

        Private Shared Function ParseTokenUrl(o As HppOptions) As Uri
            Dim norm As String = NormalizeServiceUrl(o.TokenUrl, o.AllowedHostSuffixes)
            If norm Is Nothing Then Throw New ArgumentException("TokenUrl غير مقبول: يجب https بلا منفذ/مسار ومضيف ضمن AllowedHostSuffixes.")
            Return New Uri(norm & "/api/hpcgw/v1/token/get")
        End Function

        ' ---------------- الرمز ----------------
        ''' <summary>
        ''' رمز صالح (مخبّأ). <paramref name="rejectedToken"/> = رمز رفضته السحابة للتو: لا يُعاد، فيُجلب غيره (أو يُعاد رمز جلبه خيط آخر).
        ''' التجديد يبدأ قبل الانتهاء؛ إن تعذّر (انقطاع خدمة الرمز، رفض، مهلة) يبقى الرمز الحالي مستعملاً حتى انتهائه الفعلي، ولا ينتظر
        ''' أي نداء خلف تجديد معلّق ما دام رمز صالح متاحاً (أوامر الإيقاف لا تتأخر). بعد رفض المفاتيح يُحجب الجلب 60 ث، وبعد خطأ عابر ثانيتين.
        ''' </summary>
        Friend Async Function GetAuthAsync(rejectedToken As String, ct As Threading.CancellationToken) As Task(Of HppAuth)
            Dim cur As HppAuth = Threading.Volatile.Read(_auth)
            If IsFresh(cur, rejectedToken) Then Return cur
            Dim fallback As HppAuth
            Dim fetch As Task(Of HppAuth)
            SyncLock _sync
                cur = _auth
                If IsFresh(cur, rejectedToken) Then Return cur
                fallback = If(IsStillValid(cur, rejectedToken), cur, Nothing)
                Dim now As DateTime = _clock.UtcNow
                If _failure IsNot Nothing AndAlso now < _failureUntil Then
                    If fallback IsNot Nothing Then Return fallback
                    Throw New HppCloudException(_failure.Code, _failure.Message, _failure.IsAuthFailure)
                End If
                If now < _transientUntil Then
                    If fallback IsNot Nothing Then Return fallback
                    Throw New HttpRequestException("تعذّر الوصول إلى Hik-Partner Pro (مؤقت).")
                End If
                ' جلب واحد مشترك (single-flight) بمهلته الخاصة، غير مربوط بإلغاء المستدعي: مهلة طلب واحد لا تُجهض الجلب لغيره.
                If _inflight Is Nothing OrElse _inflight.IsCompleted Then
                    _inflight = FetchAndStoreAsync()
                    Dim observer As Task = _inflight.ContinueWith(Sub(t) ObserveFault(t), Threading.CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default)
                End If
                fetch = _inflight
            End SyncLock
            ' رمز ما زال صالحاً ⇒ يُستعمل فوراً والتجديد يكمل في الخلفية (أوامر الإيقاف لا تنتظر تجديداً معلّقاً).
            If fallback IsNot Nothing Then Return fallback
            Return Await fetch.WaitAsync(ct).ConfigureAwait(False)
        End Function

        Private Shared Sub ObserveFault(t As Task)
            Dim unused As AggregateException = t.Exception
        End Sub

        Private Async Function FetchAndStoreAsync() As Task(Of HppAuth)
            Try
                Dim a As HppAuth = Await FetchTokenAsync(Threading.CancellationToken.None).ConfigureAwait(False)
                SyncLock _sync
                    _failure = Nothing
                    Threading.Volatile.Write(_auth, a)
                End SyncLock
                Return a
            Catch ex As HppCloudException
                SyncLock _sync
                    _failure = ex
                    _failureUntil = _clock.UtcNow + AuthFailBackoff
                End SyncLock
                Throw New HppCloudException(ex.Code, ex.Message, ex.IsAuthFailure)
            Catch ex As HttpRequestException
                SyncLock _sync
                    _transientUntil = _clock.UtcNow + TransientBackoff
                End SyncLock
                Throw New HttpRequestException("تعذّر الوصول إلى Hik-Partner Pro (مؤقت).")
            End Try
        End Function

        Private Function IsFresh(a As HppAuth, rejectedToken As String) As Boolean
            Return a IsNot Nothing AndAlso _clock.UtcNow < a.RefreshAtUtc AndAlso NotRejected(a, rejectedToken)
        End Function

        Private Function IsStillValid(a As HppAuth, rejectedToken As String) As Boolean
            Return a IsNot Nothing AndAlso _clock.UtcNow < a.ExpiresUtc AndAlso NotRejected(a, rejectedToken)
        End Function

        Private Shared Function NotRejected(a As HppAuth, rejectedToken As String) As Boolean
            Return rejectedToken Is Nothing OrElse Not String.Equals(a.Token, rejectedToken, StringComparison.Ordinal)
        End Function

        ''' <summary>
        ''' تصنيف فشل جلب الرمز: رفض المفاتيح فعلاً (حسب رموز الدليل أو HTTP 401/403) ⇒ فشل مصادقة؛ عطل الخدمة أو تقييد المعدل ⇒ عابر
        ''' (لا يُعامَل كمفاتيح خاطئة فلا يفتح قاطع الكاميرا)؛ غير ذلك ⇒ رفض عام يُغلق بأمان.
        ''' </summary>
        Private Shared ReadOnly KeyRejectionCodes As String() = {"LAP300001", "LAP300002", "LAP300003", "LAP500002", "LAP500003", "LAP500004", "LAP500005", "EVZ10013", "EVZ10006"}
        Private Shared ReadOnly TransientTokenCodes As String() = {"LAP500001", "LAP500007", "EVZ10007", "EVZ10029", "LAP000000", "EVZ50000", "EVZ49999", "LAP033005", "LAP008008", "LAP006006"}

        Friend Shared Function ClassifyTokenFailure(code As String, httpStatus As Integer) As String
            If KeyRejectionCodes.Contains(code, StringComparer.OrdinalIgnoreCase) Then Return "auth"
            If TransientTokenCodes.Contains(code, StringComparer.OrdinalIgnoreCase) Then Return "transient"
            If httpStatus >= 500 OrElse httpStatus = 429 OrElse httpStatus = 408 Then Return "transient"
            If httpStatus = 401 OrElse httpStatus = 403 Then Return "auth"
            Return "other"
        End Function

        Private Async Function FetchTokenAsync(ct As Threading.CancellationToken) As Task(Of HppAuth)
            Dim status As Integer
            Dim bytes As Byte()
            Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, _options.TimeoutSeconds)))
                Using req As New HttpRequestMessage(HttpMethod.Post, _tokenUri)
                    req.Content = New StringContent(JsonSerializer.Serialize(New HppTokenRequest With {.AppKey = _appKey, .SecretKey = _secretKey}), Encoding.UTF8, "application/json")
                    Try
                        Using resp As HttpResponseMessage = Await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(False)
                            status = CInt(resp.StatusCode)
                            bytes = Await resp.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(False)
                        End Using
                    Catch ex As OperationCanceledException When Not ct.IsCancellationRequested
                        Throw New HttpRequestException("انتهت مهلة جلب رمز Hik-Partner Pro.")
                    End Try
                End Using
            End Using
            Dim httpCode As String = "HTTP" & status.ToString(Globalization.CultureInfo.InvariantCulture)
            Dim code As String = Nothing
            Dim tokenText As String = ""
            Dim areaText As String = ""
            Dim expMs As Long = 0
            Dim parsed As Boolean = False
            Try
                Using doc As JsonDocument = JsonDocument.Parse(bytes)
                    Dim root As JsonElement = doc.RootElement
                    If root.ValueKind = JsonValueKind.Object Then
                        parsed = True
                        code = HppJson.ErrorCodeOf(root, Nothing)
                        ' الدليل نفسه يعرض شكلين: §2.2 داخل "data" مع errorCode، و§3.1.1 الحقول في الجذر (وبمسافات زائدة في الأسماء).
                        Dim data As JsonElement
                        Dim holder As JsonElement = root
                        If HppJson.TryGetLoose(root, "data", data) AndAlso data.ValueKind = JsonValueKind.Object Then holder = data
                        tokenText = HppJson.StringOf(holder, "accessToken")
                        areaText = HppJson.StringOf(holder, "areaDomain")
                        expMs = HppJson.LongOf(holder, "expireTime")
                    End If
                End Using
            Catch ex As JsonException
                parsed = False
            End Try
            If Not parsed AndAlso status >= 200 AndAlso status < 300 Then Throw New HppCloudException("badResponse", "رد غير مفهوم من خدمة الرمز في Hik-Partner Pro.", False)
            If code Is Nothing Then code = If(status >= 200 AndAlso status < 300 AndAlso tokenText <> "", "0", httpCode)
            If code <> "0" OrElse status < 200 OrElse status >= 300 OrElse tokenText = "" Then
                Dim effective As String = If(code = "0", httpCode, code)
                Select Case ClassifyTokenFailure(effective, status)
                    Case "transient"
                        Throw New HttpRequestException("خدمة الرمز في Hik-Partner Pro غير متاحة مؤقتاً.")
                    Case "auth"
                        Throw New HppCloudException(HppJson.SafeCode(effective), $"رفضت Hik-Partner Pro المفاتيح ({HppJson.SafeCode(effective)}): تحقق من API Key/Secret ومن الصلاحية.", True)
                    Case Else
                        Throw New HppCloudException(HppJson.SafeCode(effective), $"تعذّر جلب رمز Hik-Partner Pro ({HppJson.SafeCode(effective)}).", False)
                End Select
            End If
            Dim area As String = NormalizeServiceUrl(areaText, _options.AllowedHostSuffixes)
            If area Is Nothing Then Throw New HppCloudException("badAreaDomain", "نطاق الخدمة المُعاد من Hik-Partner Pro مرفوض (ليس https ضمن اللواحق المسموحة).", False)
            Dim now As DateTime = _clock.UtcNow
            Dim expires As DateTime
            Try
                expires = DateTimeOffset.FromUnixTimeMilliseconds(expMs).UtcDateTime
            Catch ex As ArgumentOutOfRangeException
                Throw New HppCloudException("badExpiry", "مدة صلاحية الرمز غير صالحة.", False)
            End Try
            Dim lifetime As TimeSpan = expires - now
            If lifetime < TimeSpan.FromMinutes(2) Then Throw New HppCloudException("badExpiry", "مدة صلاحية الرمز غير صالحة.", False)
            Dim margin As TimeSpan = If(lifetime / 10 < MaxRefreshMargin, lifetime / 10, MaxRefreshMargin)
            Return New HppAuth(tokenText, area, expires - margin, expires)
        End Function

        ' ---------------- نداءات عامة (للأوامر الإدارية) ----------------
        ''' <summary>
        ''' نداء JSON عام إلى areaDomain (POST، Bearer): يعيد errorCode و"data" (نسخة مستقلة). رمز مرفوض (LAP50000x أو 401 مجرّد) يُجدَّد
        ''' ويُعاد مرة واحدة. أعطال جلب الرمز تُرمى كما هي (HppCloudException / HttpRequestException).
        ''' </summary>
        Friend Async Function PostApiAsync(path As String, body As String, timeout As TimeSpan, ct As Threading.CancellationToken) As Task(Of HppApiResult)
            Dim rejected As String = Nothing
            For attempt As Integer = 0 To 1
                Dim auth As HppAuth = Await GetAuthAsync(rejected, ct).ConfigureAwait(False)
                Dim status As Integer
                Dim bytes As Byte()
                Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                    cts.CancelAfter(timeout)
                    Using req As New HttpRequestMessage(HttpMethod.Post, auth.Area & path)
                        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " & auth.Token)
                        req.Content = New StringContent(body, Encoding.UTF8, "application/json")
                        Try
                            Using resp As HttpResponseMessage = Await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(False)
                                status = CInt(resp.StatusCode)
                                bytes = Await resp.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(False)
                            End Using
                        Catch ex As OperationCanceledException When Not ct.IsCancellationRequested
                            Throw New HttpRequestException("انتهت مهلة نداء Hik-Partner Pro.")
                        End Try
                    End Using
                End Using
                Dim result As New HppApiResult With {.HttpStatus = status, .Code = "HTTP" & status.ToString(Globalization.CultureInfo.InvariantCulture)}
                Try
                    Using doc As JsonDocument = JsonDocument.Parse(bytes)
                        Dim root As JsonElement = doc.RootElement
                        If root.ValueKind = JsonValueKind.Object Then
                            result.Code = HppJson.ErrorCodeOf(root, If(status >= 200 AndAlso status < 300, "0", result.Code))
                            Dim data As JsonElement
                            If HppJson.TryGetLoose(root, "data", data) Then result.Data = data.Clone()
                        End If
                    End Using
                Catch ex As JsonException
                    If status >= 200 AndAlso status < 300 Then result.Code = "badResponse"
                End Try
                If status = 401 AndAlso result.Code.StartsWith("HTTP", StringComparison.Ordinal) Then result.Code = "LAP500004"
                If attempt = 0 AndAlso HppResponseMapper.IsTokenRejected(result.Code) Then
                    rejected = auth.Token
                    Continue For
                End If
                Return result
            Next
            Return New HppApiResult With {.HttpStatus = 401, .Code = "LAP500004"}
        End Function

        ''' <summary>قائمة الأجهزة المرتبطة بالحساب (للاكتشاف عند الإعداد). للقراءة فقط.</summary>
        Public Async Function ListDevicesAsync(ct As Threading.CancellationToken) As Task(Of IReadOnlyList(Of HppDevice))
            Dim result As New List(Of HppDevice)()
            Const pageSize As Integer = 20
            For page As Integer = 1 To 25
                Dim body As String = JsonSerializer.Serialize(New With {.page = page, .pageSize = pageSize})
                Dim r As HppApiResult = Await PostApiAsync("/api/hpcgw/v1/device/list", body, TimeSpan.FromSeconds(Math.Max(3, _options.TimeoutSeconds)), ct).ConfigureAwait(False)
                If r.Code = "badResponse" Then Throw New HppCloudException("badResponse", "رد غير مفهوم من قائمة أجهزة Hik-Partner Pro.", False)
                If r.Code <> "0" Then
                    Throw New HppCloudException(HppJson.SafeCode(r.Code), $"رفضت Hik-Partner Pro طلب قائمة الأجهزة ({HppJson.SafeCode(r.Code)}).", HppResponseMapper.IsAuthCode(r.Code))
                End If
                Dim rows As JsonElement
                If r.Data.ValueKind <> JsonValueKind.Object OrElse Not HppJson.TryGetLoose(r.Data, "rows", rows) OrElse rows.ValueKind <> JsonValueKind.Array Then Exit For   ' نجاح بلا صفوف ⇒ نهاية القائمة
                Dim total As Long = HppJson.LongOf(r.Data, "total")
                Dim n As Integer = 0
                For Each row As JsonElement In rows.EnumerateArray()
                    If row.ValueKind <> JsonValueKind.Object Then Continue For
                    result.Add(New HppDevice With {
                        .Serial = HppJson.StringOf(row, "deviceSerial"),
                        .Name = HppJson.StringOf(row, "deviceName"),
                        .DeviceType = HppJson.StringOf(row, "deviceType"),
                        .Category = CInt(HppJson.LongOf(row, "deviceCategory")),
                        .OnlineStatus = CInt(HppJson.LongOf(row, "deviceOnlineStatus")),
                        .SiteName = HppJson.StringOf(row, "siteName")})
                    n += 1
                Next
                If n < pageSize OrElse (total > 0 AndAlso result.Count >= total) Then Exit For
            Next
            Return result
        End Function

        ' ---------------- لمعالج الترحيل ----------------
        Friend Function SendRawAsync(req As HttpRequestMessage, ct As Threading.CancellationToken) As Task(Of HttpResponseMessage)
            Return _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct)
        End Function

        Friend Sub RaiseCallCompleted(e As HppCallEventArgs)
            Try
                RaiseEvent CallCompleted(Me, e)
            Catch ex As Exception When Not (TypeOf ex Is OutOfMemoryException)
                Diagnostics.Debug.WriteLine("hpp subscriber failed: " & ex.GetType().Name)
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _http.Dispose()
        End Sub
    End Class

    ''' <summary>أدوات JSON متسامحة لردود Hik-Partner Pro (errorCode نصي حسب الدليل لكنه يُقبل رقماً أيضاً).</summary>
    Public NotInheritable Class HppJson
        Private Sub New()
        End Sub

        Public Shared Function ErrorCodeOf(root As JsonElement, fallback As String) As String
            Dim v As JsonElement
            If Not TryGetLoose(root, "errorCode", v) Then Return fallback
            Select Case v.ValueKind
                Case JsonValueKind.String : Return If(String.IsNullOrWhiteSpace(v.GetString()), "0", v.GetString().Trim())
                Case JsonValueKind.Number : Return v.GetRawText()
                Case JsonValueKind.Null : Return "0"
                Case Else : Return fallback
            End Select
        End Function

        ''' <summary>بحث متسامح عن خاصية: مطابقة تامة أولاً ثم بتجاهل المسافات المحيطة وحالة الأحرف (أمثلة الدليل فيها "accessToken ").</summary>
        Public Shared Function TryGetLoose(o As JsonElement, name As String, ByRef value As JsonElement) As Boolean
            If o.ValueKind <> JsonValueKind.Object Then Return False
            If o.TryGetProperty(name, value) Then Return True
            For Each p As JsonProperty In o.EnumerateObject()
                If String.Equals(p.Name.Trim(), name, StringComparison.OrdinalIgnoreCase) Then
                    value = p.Value
                    Return True
                End If
            Next
            Return False
        End Function

        Public Shared Function StringOf(o As JsonElement, name As String) As String
            Dim v As JsonElement
            If TryGetLoose(o, name, v) AndAlso v.ValueKind = JsonValueKind.String Then Return If(v.GetString(), "").Trim()
            Return ""
        End Function

        Public Shared Function LongOf(o As JsonElement, name As String) As Long
            Dim v As JsonElement
            If Not TryGetLoose(o, name, v) Then Return 0
            Dim n As Long
            If v.ValueKind = JsonValueKind.Number AndAlso v.TryGetInt64(n) Then Return n
            If v.ValueKind = JsonValueKind.String AndAlso Long.TryParse(v.GetString(), Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, n) Then Return n
            Return 0
        End Function

        ''' <summary>رمز آمن للعرض والتدقيق: حروف وأرقام فقط وحتى 24 محرفاً (لا يُسرَّب نص حر من الخادم).</summary>
        Public Shared Function SafeCode(code As String) As String
            If String.IsNullOrEmpty(code) Then Return ""
            Dim sb As New StringBuilder()
            For Each ch As Char In code
                If Char.IsAsciiLetterOrDigit(ch) Then sb.Append(ch)
                If sb.Length >= 24 Then Exit For
            Next
            Return sb.ToString()
        End Function
    End Class

End Namespace
