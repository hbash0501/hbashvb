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

        Public Sub New(serial As String, outcome As HppOutcome, code As String, httpStatus As Integer)
            Me.Serial = serial
            Me.Outcome = outcome
            Me.Code = code
            Me.HttpStatus = httpStatus
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

    ''' <summary>رمز وصول مخبّأ: يُجدَّد قبل انتهائه، ونطاق الخدمة (areaDomain) محقَّق من قائمة اللواحق المسموحة.</summary>
    Friend NotInheritable Class HppAuth
        Public ReadOnly Property Token As String
        ''' <summary>"https://host" بلا مسار.</summary>
        Public ReadOnly Property Area As String
        Public ReadOnly Property RefreshAtUtc As DateTime

        Public Sub New(token As String, area As String, refreshAtUtc As DateTime)
            Me.Token = token
            Me.Area = area
            Me.RefreshAtUtc = refreshAtUtc
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
        Private ReadOnly _gate As New Threading.SemaphoreSlim(1, 1)
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
        ''' بعد رفض المفاتيح يُحجب الجلب 60 ث (لا نُغرق الخدمة بمفاتيح خاطئة)، وبعد خطأ شبكة عابر ثانيتين.
        ''' </summary>
        Friend Async Function GetAuthAsync(rejectedToken As String, ct As Threading.CancellationToken) As Task(Of HppAuth)
            Dim cur As HppAuth = Threading.Volatile.Read(_auth)
            If IsUsable(cur, rejectedToken) Then Return cur
            Await _gate.WaitAsync(ct).ConfigureAwait(False)
            Try
                cur = _auth
                If IsUsable(cur, rejectedToken) Then Return cur
                Dim now As DateTime = _clock.UtcNow
                If _failure IsNot Nothing AndAlso now < _failureUntil Then Throw _failure
                If now < _transientUntil Then Throw New HttpRequestException("تعذّر الوصول إلى Hik-Partner Pro (مؤقت).")
                Dim fetched As HppAuth = Nothing
                Dim transient As Boolean = False
                Try
                    fetched = Await FetchTokenAsync(ct).ConfigureAwait(False)
                Catch ex As HppCloudException
                    _failure = ex
                    _failureUntil = _clock.UtcNow + AuthFailBackoff
                    Throw
                Catch ex As HttpRequestException
                    transient = True
                End Try
                If transient Then
                    _transientUntil = _clock.UtcNow + TransientBackoff
                    Throw New HttpRequestException("تعذّر الوصول إلى Hik-Partner Pro (مؤقت).")
                End If
                _failure = Nothing
                Threading.Volatile.Write(_auth, fetched)
                Return fetched
            Finally
                _gate.Release()
            End Try
        End Function

        Private Function IsUsable(a As HppAuth, rejectedToken As String) As Boolean
            Return a IsNot Nothing AndAlso _clock.UtcNow < a.RefreshAtUtc AndAlso (rejectedToken Is Nothing OrElse Not String.Equals(a.Token, rejectedToken, StringComparison.Ordinal))
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
            Dim code As String = "HTTP" & status.ToString(Globalization.CultureInfo.InvariantCulture)
            Dim tokenText As String = Nothing
            Dim areaText As String = Nothing
            Dim expMs As Long = 0
            Try
                Using doc As JsonDocument = JsonDocument.Parse(bytes)
                    Dim root As JsonElement = doc.RootElement
                    If root.ValueKind = JsonValueKind.Object Then
                        code = HppJson.ErrorCodeOf(root, code)
                        Dim data As JsonElement
                        If root.TryGetProperty("data", data) AndAlso data.ValueKind = JsonValueKind.Object Then
                            tokenText = HppJson.StringOf(data, "accessToken")
                            areaText = HppJson.StringOf(data, "areaDomain")
                            expMs = HppJson.LongOf(data, "expireTime")
                        End If
                    End If
                End Using
            Catch ex As JsonException
                Throw New HppCloudException("badResponse", "رد غير مفهوم من خدمة الرمز في Hik-Partner Pro.", False)
            End Try
            If code <> "0" OrElse String.IsNullOrEmpty(tokenText) Then
                Throw New HppCloudException(HppJson.SafeCode(code), $"رفضت Hik-Partner Pro جلب الرمز ({HppJson.SafeCode(code)}): تحقق من المفاتيح ومن الصلاحية.", True)
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
            Return New HppAuth(tokenText, area, expires - margin)
        End Function

        ' ---------------- نداءات عامة (للأوامر الإدارية) ----------------
        ''' <summary>قائمة الأجهزة المرتبطة بالحساب (للاكتشاف عند الإعداد). للقراءة فقط.</summary>
        Public Async Function ListDevicesAsync(ct As Threading.CancellationToken) As Task(Of IReadOnlyList(Of HppDevice))
            Dim result As New List(Of HppDevice)()
            Const pageSize As Integer = 20
            For page As Integer = 1 To 25
                Dim rows As JsonElement = Nothing
                Dim total As Long = 0
                Dim ok As Boolean = False
                For attempt As Integer = 0 To 1
                    Dim auth As HppAuth = Await GetAuthAsync(Nothing, ct).ConfigureAwait(False)
                    Dim status As Integer
                    Dim bytes As Byte()
                    Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, _options.TimeoutSeconds)))
                        Using req As New HttpRequestMessage(HttpMethod.Post, auth.Area & "/api/hpcgw/v1/device/list")
                            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " & auth.Token)
                            req.Content = New StringContent(JsonSerializer.Serialize(New With {.page = page, .pageSize = pageSize}), Encoding.UTF8, "application/json")
                            Try
                                Using resp As HttpResponseMessage = Await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(False)
                                    status = CInt(resp.StatusCode)
                                    bytes = Await resp.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(False)
                                End Using
                            Catch ex As OperationCanceledException When Not ct.IsCancellationRequested
                                Throw New HttpRequestException("انتهت مهلة طلب قائمة الأجهزة.")
                            End Try
                        End Using
                    End Using
                    Dim code As String = "HTTP" & status.ToString(Globalization.CultureInfo.InvariantCulture)
                    Dim parsed As Boolean = False
                    Try
                        Using doc As JsonDocument = JsonDocument.Parse(bytes)
                            Dim root As JsonElement = doc.RootElement
                            If root.ValueKind = JsonValueKind.Object Then
                                code = HppJson.ErrorCodeOf(root, code)
                                Dim data As JsonElement
                                If code = "0" AndAlso root.TryGetProperty("data", data) AndAlso data.ValueKind = JsonValueKind.Object Then
                                    total = HppJson.LongOf(data, "total")
                                    Dim r As JsonElement
                                    If data.TryGetProperty("rows", r) AndAlso r.ValueKind = JsonValueKind.Array Then
                                        rows = r.Clone()
                                        parsed = True
                                    End If
                                End If
                            End If
                        End Using
                    Catch ex As JsonException
                        Throw New HppCloudException("badResponse", "رد غير مفهوم من قائمة أجهزة Hik-Partner Pro.", False)
                    End Try
                    If parsed Then
                        ok = True
                        Exit For
                    End If
                    If attempt = 0 AndAlso HppResponseMapper.IsTokenRejected(code) Then
                        Await GetAuthAsync(auth.Token, ct).ConfigureAwait(False)
                        Continue For
                    End If
                    Throw New HppCloudException(HppJson.SafeCode(code), $"رفضت Hik-Partner Pro طلب قائمة الأجهزة ({HppJson.SafeCode(code)}).", HppResponseMapper.IsAuthCode(code))
                Next
                If Not ok Then Exit For
                Dim n As Integer = 0
                For Each row As JsonElement In rows.EnumerateArray()
                    If row.ValueKind <> JsonValueKind.Object Then Continue For
                    result.Add(New HppDevice With {
                        .Serial = HppJson.StringOf(row, "deviceSerial").Trim(),
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
            _gate.Dispose()
        End Sub
    End Class

    ''' <summary>أدوات JSON متسامحة لردود Hik-Partner Pro (errorCode نصي حسب الدليل لكنه يُقبل رقماً أيضاً).</summary>
    Public NotInheritable Class HppJson
        Private Sub New()
        End Sub

        Public Shared Function ErrorCodeOf(root As JsonElement, fallback As String) As String
            Dim v As JsonElement
            If Not root.TryGetProperty("errorCode", v) Then Return fallback
            Select Case v.ValueKind
                Case JsonValueKind.String : Return If(String.IsNullOrWhiteSpace(v.GetString()), "0", v.GetString().Trim())
                Case JsonValueKind.Number : Return v.GetRawText()
                Case JsonValueKind.Null : Return "0"
                Case Else : Return fallback
            End Select
        End Function

        Public Shared Function StringOf(o As JsonElement, name As String) As String
            Dim v As JsonElement
            If o.TryGetProperty(name, v) AndAlso v.ValueKind = JsonValueKind.String Then Return If(v.GetString(), "")
            Return ""
        End Function

        Public Shared Function LongOf(o As JsonElement, name As String) As Long
            Dim v As JsonElement
            If Not o.TryGetProperty(name, v) Then Return 0
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
