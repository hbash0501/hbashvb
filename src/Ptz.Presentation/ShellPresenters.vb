Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Cameras
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security

Public NotInheritable Class LoginPresenter
    Private ReadOnly _view As ILoginView
    Private ReadOnly _users As UserStore
    Private _busy As Boolean

    Public Event SessionEstablished As Action(Of UserSession)

    Public Sub New(view As ILoginView, users As UserStore)
        _view = view
        _users = users
        view.SetMode(If(users.HasAnyUser, LoginMode.SignIn, LoginMode.FirstRunCreateAdmin))
        AddHandler view.SubmitRequested, AddressOf OnSubmit
    End Sub

    Private Async Sub OnSubmit(userName As String, password As String, confirm As String)
        If _busy Then Return
        _busy = True
        _view.SetBusy(True)
        Dim errorText As String = Nothing
        Dim slowDown As Boolean = False
        Dim session As UserSession = Nothing
        Try
            If Not _users.HasAnyUser AndAlso password <> confirm Then
                errorText = "كلمتا المرور غير متطابقتين."
            Else
                If Not _users.HasAnyUser Then _users.CreateUser("(first-run)", userName, password, Role.Administrator)
                session = _users.Authenticate(userName, password)
            End If
        Catch ex As AuthenticationException
            errorText = ex.Message
            slowDown = True ' يبطئ التخمين الآلي من الواجهة (القفل الفعلي في UserStore)
        Catch ex As ArgumentException
            errorText = ex.Message
        Catch ex As InvalidOperationException
            errorText = ex.Message
        End Try
        If slowDown Then Await Task.Delay(800)
        _busy = False
        _view.SetBusy(False)
        If session IsNot Nothing Then
            RaiseEvent SessionEstablished(session)
            _view.CloseWithSuccess()
        ElseIf errorText IsNot Nothing Then
            _view.ShowError(errorText)
        End If
    End Sub
End Class

Public NotInheritable Class HubPresenter
    Implements IDisposable

    Private ReadOnly _view As IHubView
    Private ReadOnly _session As UserSession
    Private ReadOnly _store As CameraConfigStore
    Private ReadOnly _gateway As GatewayHubContext
    Private ReadOnly _access As AccessControl
    Private ReadOnly _audit As IAuditLog
    Private ReadOnly _bus As AlarmBus
    Private ReadOnly _decoder As IFrameDecoder
    Private ReadOnly _ui As IUiDispatcher
    Private ReadOnly _nav As IShellNavigator
    Private ReadOnly _dataDir As String
    Private _pollCts As Threading.CancellationTokenSource
    Private _gatewayBackend As GatewayBackendFactory

    Public Const KeySe7c432mw As String = "se7c432mw"
    Public Const KeyDe7232iw As String = "de7232iw"

    ''' <summary>الوضع المحلي: الاتصال المباشر بالكاميرات بإعدادات مخزّنة محلياً.</summary>
    Public Sub New(view As IHubView, session As UserSession, store As CameraConfigStore, access As AccessControl,
                   audit As IAuditLog, bus As AlarmBus, decoder As IFrameDecoder, ui As IUiDispatcher,
                   nav As IShellNavigator, dataDirectory As String)
        Me.New(view, session, store, Nothing, access, audit, bus, decoder, ui, nav, dataDirectory)
    End Sub

    ''' <summary>وضع البوابة: كل شيء عبر بوابة LAN؛ لا إعدادات أو أسرار كاميرات على هذا الجهاز.</summary>
    Public Sub New(view As IHubView, gateway As GatewayHubContext, access As AccessControl,
                   audit As IAuditLog, bus As AlarmBus, decoder As IFrameDecoder, ui As IUiDispatcher,
                   nav As IShellNavigator, dataDirectory As String)
        Me.New(view, gateway.Session, Nothing, gateway, access, audit, bus, decoder, ui, nav, dataDirectory)
    End Sub

    Private Sub New(view As IHubView, session As UserSession, store As CameraConfigStore, gateway As GatewayHubContext, access As AccessControl,
                    audit As IAuditLog, bus As AlarmBus, decoder As IFrameDecoder, ui As IUiDispatcher,
                    nav As IShellNavigator, dataDirectory As String)
        _view = view
        _session = session
        _store = store
        _gateway = gateway
        _access = access
        _audit = audit
        _bus = bus
        _decoder = decoder
        _ui = ui
        _nav = nav
        _dataDir = dataDirectory
        view.SetUser(session.UserName, session.Role.ToString())
        view.ApplyPermissions(session.Permissions)
        If gateway Is Nothing Then
            store.EnsureDefaults({(KeySe7c432mw, CameraProfiles.Ds2Se7c432mwAeb), (KeyDe7232iw, CameraProfiles.Ds2De7232iwAe)})
            view.ShowCameras(store.All())
        Else
            _gatewayBackend = New GatewayBackendFactory(gateway.Client)
            view.SetGatewayMode(gateway.EndpointText)
            view.ShowCameras(GatewayHubContext.ToConfigs(gateway.Cameras))
            For Each c As CameraDto In gateway.Cameras
                view.SetCameraStatus(c.Id, GatewayHubContext.StatusText(c), c.Online)
            Next
            AddHandler gateway.Client.SessionEnded, AddressOf OnGatewaySessionEnded
            _pollCts = New Threading.CancellationTokenSource()
            Dim ct As Threading.CancellationToken = _pollCts.Token
            Task.Run(Function() PollStatusAsync(ct))
        End If
        AddHandler view.OpenCameraRequested, AddressOf OnOpen
        AddHandler view.SaveConfigRequested, AddressOf OnSave
        AddHandler view.FetchCertificateRequested, AddressOf OnFetchCertificate
        AddHandler view.ManageUsersRequested, Sub() Guard(Permission.ManageUsers, "Admin.OpenUsers", Sub() OpenUsers())
        AddHandler view.ViewAuditRequested, Sub() Guard(Permission.ViewAudit, "Admin.OpenAudit", Sub() OpenAudit())
        AddHandler view.LogoutRequested, AddressOf OnLogout
    End Sub

    Private Sub OpenUsers()
        If _gateway IsNot Nothing Then
            _view.ShowError(UiText.GatewayUsersCli)
        Else
            _nav.OpenUserAdmin(_session)
        End If
    End Sub

    Private Sub OpenAudit()
        If _gateway IsNot Nothing Then _nav.OpenGatewayAudit(_session) Else _nav.OpenAuditViewer(_session)
    End Sub

    Private Sub OnGatewaySessionEnded(reason As String)
        _ui.Post(Sub()
                     _view.ShowError(UiText.SessionExpired)
                     _nav.Logout()
                 End Sub)
    End Sub

    ''' <summary>يحدّث حالة اتصال الكاميرات بالبوابة كل 10 ثوانٍ (طلب خفيف). يتوقف بانتهاء الجلسة أو التخلص.</summary>
    Private Async Function PollStatusAsync(ct As Threading.CancellationToken) As Task
        Try
            While Not ct.IsCancellationRequested AndAlso _gateway.Client.IsSignedIn
                Await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(False)
                Dim list As CameraListDto = Nothing
                Try
                    list = Await _gateway.Client.GetJsonAsync(Of CameraListDto)("/api/v1/cameras", ct).ConfigureAwait(False)
                Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is AccessDeniedException OrElse TypeOf ex Is InvalidOperationException
                    list = Nothing
                End Try
                If list IsNot Nothing Then
                    _ui.Post(Sub()
                                 For Each c As CameraDto In list.Items
                                     _view.SetCameraStatus(c.Id, GatewayHubContext.StatusText(c), c.Online)
                                 Next
                             End Sub)
                End If
            End While
        Catch ex As OperationCanceledException
        End Try
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _pollCts IsNot Nothing Then
            _pollCts.Cancel()
            _pollCts.Dispose()
            _pollCts = Nothing
        End If
        If _gateway IsNot Nothing Then RemoveHandler _gateway.Client.SessionEnded, AddressOf OnGatewaySessionEnded
    End Sub

    Private Sub Guard(p As Permission, action As String, op As Action)
        Try
            _access.Demand(_session, p)
            op()
        Catch ex As AccessDeniedException
            _audit.Write(_session.UserName, action, "", "DENIED", ex.Message)
            _view.ShowError(If(ex.IsSessionExpired, UiText.SessionExpired, UiText.NoPermission))
        End Try
    End Sub

    Private Sub OnOpen(key As String)
        Guard(Permission.ViewLive, "Camera.Open", Sub()
                                                      If _gateway IsNot Nothing Then
                                                          OpenViaGateway(key)
                                                          Return
                                                      End If
                                                      Dim cfg As CameraConfig = _store.Get(key)
                                                      If cfg Is Nothing Then Throw New InvalidOperationException("الكاميرا غير معرّفة.")
                                                      Dim conn As CameraConnection
                                                      Try
                                                          conn = _store.BuildConnection(key)
                                                          conn.Validate()
                                                      Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException
                                                          _view.ShowError("إعدادات الاتصال غير مكتملة: " & ex.Message)
                                                          Return
                                                      Catch ex As Security.Cryptography.CryptographicException
                                                          _view.ShowError("تعذّر فك تشفير كلمة المرور المخزنة؛ أعد إدخالها.")
                                                          Return
                                                      End Try
                                                      Dim deps As New CameraSessionDeps With {
                                                          .Connection = conn, .Session = _session, .Access = _access, .Audit = _audit, .Bus = _bus,
                                                          .Decoder = _decoder, .Dispatcher = _ui, .DataDirectory = _dataDir,
                                                          .CameraName = If(cfg.DisplayName <> "", cfg.DisplayName, cfg.Key)}
                                                      _nav.OpenCamera(cfg, deps)
                                                  End Sub)
    End Sub

    Private Sub OpenViaGateway(key As String)
        Dim cam As CameraDto = _gateway.Cameras.FirstOrDefault(Function(c) c.Id = key)
        If cam Is Nothing Then Throw New InvalidOperationException("الكاميرا غير معرّفة في البوابة.")
        Dim cfg As New CameraConfig With {.Key = cam.Id, .DisplayName = If(cam.DisplayName <> "", cam.DisplayName, cam.Id), .ModelName = cam.Profile.ModelName}
        Dim deps As New CameraSessionDeps With {
            .Session = _session, .Access = _access, .Audit = _audit, .Bus = _bus, .Decoder = _decoder, .Dispatcher = _ui,
            .DataDirectory = _dataDir, .CameraName = cfg.DisplayName, .BackendFactory = _gatewayBackend}
        _nav.OpenCamera(cfg, deps)
    End Sub

    Private Sub OnSave(cfg As CameraConfig, newPassword As String)
        If _gateway IsNot Nothing Then
            _view.ShowError(UiText.ManagedOnGateway)
            Return
        End If
        Guard(Permission.ManageConnections, "Camera.SaveConfig", Sub()
                                                                     Try
                                                                         _store.Update(cfg, If(newPassword = "", Nothing, newPassword))
                                                                         _audit.Write(_session.UserName, "Camera.SaveConfig", cfg.Key, "OK", $"{cfg.Host}:{cfg.Port} https={cfg.UseHttps} pinned={cfg.PinnedCertSha256 <> ""}")
                                                                         _view.ShowCameras(_store.All())
                                                                         _view.ShowInfo(UiText.Saved)
                                                                     Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException
                                                                         _audit.Write(_session.UserName, "Camera.SaveConfig", cfg.Key, "FAIL", ex.Message)
                                                                         _view.ShowError(ex.Message)
                                                                     End Try
                                                                 End Sub)
    End Sub

    Private Async Sub OnFetchCertificate(key As String)
        If _gateway IsNot Nothing Then
            _view.ShowError(UiText.ManagedOnGateway)
            Return
        End If
        Try
            _access.Demand(_session, Permission.ManageConnections)
            Dim cfg As CameraConfig = _store.Get(key)
            If cfg Is Nothing OrElse Not cfg.UseHttps Then
                _view.ShowError("فعّل HTTPS واحفظ العنوان أولاً.")
                Return
            End If
            Dim fp As String = Await CertificateProbe.FetchSha256Async(cfg.Host, cfg.Port, TimeSpan.FromSeconds(8), Threading.CancellationToken.None)
            _audit.Write(_session.UserName, "Camera.FetchCertificate", key, "OK", fp)
            _view.ShowCertificateFingerprint(key, fp)
        Catch ex As AccessDeniedException
            _view.ShowError(UiText.NoPermission)
        Catch ex As Exception When TypeOf ex Is Net.Sockets.SocketException OrElse TypeOf ex Is Security.Authentication.AuthenticationException OrElse
                                  TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is IO.IOException OrElse TypeOf ex Is InvalidOperationException
            _view.ShowError("تعذّر قراءة شهادة الكاميرا: " & ex.Message)
        End Try
    End Sub

    Private Async Sub OnLogout()
        _audit.Write(_session.UserName, "Auth.Logout", "", "OK", If(_gateway Is Nothing, "", "gateway"))
        If _gateway IsNot Nothing Then
            Try
                Await _gateway.Client.LogoutAsync()          ' يُبطل الرمز على الخادم؛ لا يرمي أبداً
            Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is OperationCanceledException
            End Try
        End If
        _nav.Logout()
    End Sub
End Class

Public NotInheritable Class UserAdminPresenter
    Private ReadOnly _view As IUserAdminView
    Private ReadOnly _users As UserStore
    Private ReadOnly _session As UserSession
    Private ReadOnly _access As AccessControl

    Public Sub New(view As IUserAdminView, users As UserStore, session As UserSession, access As AccessControl)
        _view = view
        _users = users
        _session = session
        _access = access
        AddHandler view.CreateRequested, Sub(n, p, r) Run(Sub() _users.CreateUser(_session.UserName, n, p, r))
        AddHandler view.SetRoleRequested, Sub(n, r) Run(Sub() _users.SetRole(_session.UserName, n, r))
        AddHandler view.SetDisabledRequested, Sub(n, d) Run(Sub() _users.SetDisabled(_session.UserName, n, d))
        AddHandler view.ChangePasswordRequested, Sub(n, p) Run(Sub() _users.ChangePassword(_session.UserName, n, p))
        Refresh()
    End Sub

    Private Sub Refresh()
        _view.ShowUsers(_users.ListUsers())
    End Sub

    Private Sub Run(op As Action)
        Try
            _access.Demand(_session, Permission.ManageUsers)
            op()
            Refresh()
            _view.ShowInfo(UiText.Done)
        Catch ex As AccessDeniedException
            _view.ShowError(UiText.NoPermission)
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is KeyNotFoundException
            _view.ShowError(ex.Message)
        End Try
    End Sub
End Class

Public NotInheritable Class AuditPresenter
    Private ReadOnly _view As IAuditView
    Private ReadOnly _log As FileAuditLog
    Private ReadOnly _session As UserSession
    Private ReadOnly _access As AccessControl

    Public Sub New(view As IAuditView, log As FileAuditLog, session As UserSession, access As AccessControl)
        _view = view
        _log = log
        _session = session
        _access = access
        AddHandler view.RefreshRequested, AddressOf Refresh
        AddHandler view.VerifyRequested, AddressOf Verify
        Refresh()
    End Sub

    Private Sub Refresh()
        Try
            _access.Demand(_session, Permission.ViewAudit)
            _view.ShowEntries(_log.ReadRecent(500))
        Catch ex As AccessDeniedException
            _view.ShowError(UiText.NoPermission)
        End Try
    End Sub

    Private Sub Verify()
        Try
            _access.Demand(_session, Permission.ViewAudit)
            _view.ShowVerification(_log.Verify())
        Catch ex As AccessDeniedException
            _view.ShowError(UiText.NoPermission)
        End Try
    End Sub
End Class
