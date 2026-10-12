Imports System.Net.Http
Imports System.Text.Json
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security

''' <summary>إعدادات اتصال البوابة المحفوظة محلياً: العنوان والبصمة فقط (لا كلمات مرور ولا رموز).</summary>
Public NotInheritable Class GatewaySettings
    Public Property BaseUrl As String = ""
    Public Property PinnedCert As String = ""
    Public Property AllowInsecureLoopbackHttp As Boolean
End Class

Public NotInheritable Class GatewaySettingsStore
    Private ReadOnly _path As String

    Public Sub New(dataDirectory As String)
        _path = IO.Path.Combine(dataDirectory, "gateway-endpoint.json")
    End Sub

    Public Function Load() As GatewaySettings
        Try
            If IO.File.Exists(_path) Then
                Dim s As GatewaySettings = JsonSerializer.Deserialize(Of GatewaySettings)(IO.File.ReadAllText(_path, Text.Encoding.UTF8))
                If s IsNot Nothing Then Return s
            End If
        Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is IO.IOException
        End Try
        Return New GatewaySettings()
    End Function

    Public Sub Save(settings As GatewaySettings)
        Try
            Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(_path))
            If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
            Dim tmp As String = _path & ".tmp"
            IO.File.WriteAllText(tmp, JsonSerializer.Serialize(settings), Text.Encoding.UTF8)
            IO.File.Move(tmp, _path, True)
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ' تعذّر حفظ آخر عنوان: غير حرج.
        End Try
    End Sub
End Class

''' <summary>ما يلزم المحور بعد دخول ناجح إلى البوابة (العميل المشترك + الجلسة + قائمة الكاميرات الأولية).</summary>
Public NotInheritable Class GatewayHubContext
    Public ReadOnly Property Client As GatewayClient
    Public ReadOnly Property Session As UserSession
    Public ReadOnly Property EndpointText As String
    Public ReadOnly Property Cameras As IReadOnlyList(Of CameraDto)

    Public Sub New(client As GatewayClient, session As UserSession, endpointText As String, cameras As IReadOnlyList(Of CameraDto))
        Me.Client = client
        Me.Session = session
        Me.EndpointText = endpointText
        Me.Cameras = cameras
    End Sub

    ''' <summary>يحوّل قائمة البوابة إلى إدخالات تعرضها بطاقات المحور (لا عناوين ولا أسرار: لا وجود لها هنا أصلاً).</summary>
    Public Shared Function ToConfigs(cameras As IEnumerable(Of CameraDto)) As List(Of CameraConfig)
        Return cameras.Select(Function(c) New CameraConfig With {.Key = c.Id, .DisplayName = If(c.DisplayName <> "", c.DisplayName, c.Id), .ModelName = c.Profile.ModelName}).ToList()
    End Function

    Public Shared Function StatusText(c As CameraDto) As String
        Return If(c.Online, "متصلة بالبوابة", "غير متصلة بالبوابة")
    End Function
End Class

Public NotInheritable Class GatewayLoginInput
    Public Property Url As String = ""
    Public Property Pin As String = ""
    Public Property AllowLoopbackHttp As Boolean
    Public Property UserName As String = ""
    Public Property Password As String = ""
    Public Property Totp As String = ""
End Class

Public Interface IGatewayLoginView
    Inherits IMessageSink
    Event ConnectRequested As Action(Of GatewayLoginInput)
    Event FetchCertificateRequested As Action(Of String)
    Sub SetBusy(busy As Boolean)
    Sub SetSettings(settings As GatewaySettings)
    ''' <summary>يعرض بصمة الشهادة المقروءة ليؤكدها المستخدم بمقارنتها بمخرجات أداة البوابة (cert show) ثم يملأ حقل التثبيت.</summary>
    Sub OfferCertificate(host As String, certSha256 As String)
    Sub CloseWithSuccess()
End Interface

''' <summary>
''' مقدِّم دخول البوابة: يتحقق من العنوان، يدخل (كلمة مرور + TOTP)، يجلب الكاميرات، ويحفظ العنوان والبصمة فقط.
''' فشل الشهادة يمنع إرسال أي بيان دخول (انظر GatewayClient). الفشل يُبطئ المحاولة التالية قليلاً.
''' </summary>
Public NotInheritable Class GatewayLoginPresenter
    Private ReadOnly _view As IGatewayLoginView
    Private ReadOnly _store As GatewaySettingsStore
    Private ReadOnly _stepUp As IGatewayCredentialProvider
    Private ReadOnly _clock As IClock
    Private ReadOnly _handlerFactory As Func(Of HttpMessageHandler)
    Private _busy As Boolean

    Public Event Connected As Action(Of GatewayHubContext)

    ''' <param name="handlerFactory">للاختبارات فقط: معالج HTTP بديل (الإنتاج: معالج TLS مثبَّت داخل العميل).</param>
    Public Sub New(view As IGatewayLoginView, store As GatewaySettingsStore, stepUp As IGatewayCredentialProvider,
                   Optional clock As IClock = Nothing, Optional handlerFactory As Func(Of HttpMessageHandler) = Nothing)
        _view = view
        _store = store
        _stepUp = stepUp
        _clock = If(clock, SystemClock.Instance)
        _handlerFactory = handlerFactory
        view.SetSettings(store.Load())
        AddHandler view.ConnectRequested, AddressOf OnConnect
        AddHandler view.FetchCertificateRequested, AddressOf OnFetchCertificate
    End Sub

    Private Async Sub OnConnect(input As GatewayLoginInput)
        If _busy Then Return
        _busy = True
        _view.SetBusy(True)
        Dim errorText As String = Nothing
        Dim slowDown As Boolean = False
        Dim context As GatewayHubContext = Nothing
        Dim client As GatewayClient = Nothing
        Try
            If String.IsNullOrWhiteSpace(input.UserName) OrElse String.IsNullOrEmpty(input.Password) Then Throw New ArgumentException("أدخل اسم المستخدم وكلمة المرور.")
            If Not System.Text.RegularExpressions.Regex.IsMatch(If(input.Totp, "").Trim(), "^\d{6}\z") Then Throw New ArgumentException("رمز التحقق يتكوّن من 6 أرقام.")
            Dim endpoint As New GatewayEndpoint With {.BaseUrl = input.Url.Trim(), .PinnedCert = input.Pin.Trim(), .AllowInsecureLoopbackHttp = input.AllowLoopbackHttp}
            endpoint.Validate()
            client = New GatewayClient(endpoint, _stepUp, If(_handlerFactory Is Nothing, Nothing, _handlerFactory()), _clock)
            Await client.LoginAsync(New GatewayCredentials With {.UserName = input.UserName.Trim(), .Password = input.Password, .Totp = input.Totp.Trim()}, Threading.CancellationToken.None)
            Dim list As CameraListDto = Await client.GetJsonAsync(Of CameraListDto)("/api/v1/cameras", Threading.CancellationToken.None)
            context = New GatewayHubContext(client, client.ToUserSession(), endpoint.Validate().Authority, list.Items)
            _store.Save(New GatewaySettings With {.BaseUrl = endpoint.BaseUrl, .PinnedCert = endpoint.PinnedCert, .AllowInsecureLoopbackHttp = endpoint.AllowInsecureLoopbackHttp})
        Catch ex As AuthenticationException
            errorText = ex.Message
            slowDown = True
        Catch ex As GatewayUnreachableException
            errorText = ex.Message
            slowDown = True
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is IsapiException OrElse
                                  TypeOf ex Is AccessDeniedException OrElse TypeOf ex Is NotSupportedException
            errorText = ex.Message
        End Try
        If errorText IsNot Nothing Then
            client?.Dispose()
            If slowDown Then Await Task.Delay(800)
        End If
        _busy = False
        _view.SetBusy(False)
        If context IsNot Nothing Then
            RaiseEvent Connected(context)
            _view.CloseWithSuccess()
        ElseIf errorText IsNot Nothing Then
            _view.ShowError(errorText)
        End If
    End Sub

    Private Async Sub OnFetchCertificate(url As String)
        Dim host As String = Nothing
        Dim fp As String = Nothing
        Dim errorText As String = Nothing
        Try
            Dim uri As Uri = Nothing
            If Not Uri.TryCreate(If(url, "").Trim(), UriKind.Absolute, uri) OrElse uri.Scheme <> Uri.UriSchemeHttps Then Throw New ArgumentException("أدخل عنوان https:// صحيحاً أولاً.")
            host = uri.Host
            fp = Await CertificateProbe.FetchSha256Async(uri.Host, uri.Port, TimeSpan.FromSeconds(8), Threading.CancellationToken.None)
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is Net.Sockets.SocketException OrElse TypeOf ex Is IO.IOException OrElse
                                  TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is InvalidOperationException OrElse
                                  TypeOf ex Is System.Security.Authentication.AuthenticationException
            errorText = "تعذّر قراءة شهادة البوابة: " & ex.Message
        End Try
        If errorText IsNot Nothing Then _view.ShowError(errorText) Else _view.OfferCertificate(host, fp)
    End Sub
End Class

''' <summary>عارض سجل التدقيق للبوابة (قراءة فقط: السلسلة تحت حراسة البوابة، والتحقق من سلامتها يجري هناك).</summary>
Public NotInheritable Class GatewayAuditPresenter
    Private ReadOnly _view As IAuditView
    Private ReadOnly _client As GatewayClient

    Public Sub New(view As IAuditView, client As GatewayClient)
        _view = view
        _client = client
        AddHandler view.RefreshRequested, AddressOf OnRefresh
        AddHandler view.VerifyRequested, AddressOf OnVerify
        OnRefresh()
    End Sub

    Private Shared Function Describe(ex As Exception) As String
        If TypeOf ex Is AccessDeniedException Then Return If(CType(ex, AccessDeniedException).IsSessionExpired, UiText.SessionExpired, UiText.NoPermission)
        Return ex.Message
    End Function

    Private Async Sub OnRefresh()
        Dim entries As List(Of AuditEntry) = Nothing
        Dim errorText As String = Nothing
        Try
            Dim dto As AuditListDto = Await _client.GetJsonAsync(Of AuditListDto)("/api/v1/audit?limit=200", Threading.CancellationToken.None)
            entries = dto.Items.Select(Function(i) New AuditEntry With {.Seq = i.Seq, .TimeUtc = i.TimeUtc, .User = i.User, .Action = i.Action,
                                                                         .Target = i.Target, .Outcome = i.Outcome, .Details = i.Details}).ToList()
        Catch ex As Exception When TypeOf ex Is AccessDeniedException OrElse TypeOf ex Is IsapiException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ArgumentException
            errorText = Describe(ex)
        End Try
        If entries IsNot Nothing Then _view.ShowEntries(entries) Else _view.ShowError(errorText)
    End Sub

    Private Async Sub OnVerify()
        Dim result As AuditVerifyResult = Nothing
        Dim errorText As String = Nothing
        Try
            Dim dto As AuditVerifyDto = Await _client.GetJsonAsync(Of AuditVerifyDto)("/api/v1/audit/verify", Threading.CancellationToken.None)
            result = New AuditVerifyResult(dto.IsValid, dto.EntriesChecked, dto.FirstBadSeq)
        Catch ex As Exception When TypeOf ex Is AccessDeniedException OrElse TypeOf ex Is IsapiException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ArgumentException
            errorText = Describe(ex)
        End Try
        If result IsNot Nothing Then _view.ShowVerification(result) Else _view.ShowError(errorText)
    End Sub
End Class
