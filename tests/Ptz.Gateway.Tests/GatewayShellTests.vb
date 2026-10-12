Imports System.Net.Http
Imports Ptz.Core.Alarms
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Presentation
Imports Microsoft.AspNetCore.TestHost
Imports Xunit

''' <summary>طبقة الصدفة (دخول البوابة، المحور في وضع البوابة، عارض التدقيق) فوق بوابة حقيقية عبر TestServer.</summary>
Public Class GatewayShellTests

    Private Const Se As String = "se7c432mw"

    ' ----------------------------- واجهات وهمية -----------------------------
    Private NotInheritable Class FakeLoginView
        Implements IGatewayLoginView
        Public Event ConnectRequested As Action(Of GatewayLoginInput) Implements IGatewayLoginView.ConnectRequested
        Public Event FetchCertificateRequested As Action(Of String) Implements IGatewayLoginView.FetchCertificateRequested
        Public ReadOnly Property Errors As New List(Of String)()
        Public ReadOnly Property Offered As New List(Of String)()
        Public Property Closed As Boolean
        Public Property Busy As Boolean
        Public Property Loaded As GatewaySettings
        Public Sub SetBusy(b As Boolean) Implements IGatewayLoginView.SetBusy
            Busy = b
        End Sub
        Public Sub SetSettings(s As GatewaySettings) Implements IGatewayLoginView.SetSettings
            Loaded = s
        End Sub
        Public Sub OfferCertificate(host As String, certSha256 As String) Implements IGatewayLoginView.OfferCertificate
            Offered.Add(host & "|" & certSha256)
        End Sub
        Public Sub CloseWithSuccess() Implements IGatewayLoginView.CloseWithSuccess
            Closed = True
        End Sub
        Public Sub ShowError(m As String) Implements IMessageSink.ShowError
            Errors.Add(m)
        End Sub
        Public Sub ShowInfo(m As String) Implements IMessageSink.ShowInfo
        End Sub
        Public Sub RaiseConnect(i As GatewayLoginInput)
            RaiseEvent ConnectRequested(i)
        End Sub
        Public Sub RaiseFetch(url As String)
            RaiseEvent FetchCertificateRequested(url)
        End Sub
    End Class

    Private NotInheritable Class FakeHubView
        Implements IHubView
        Public Event OpenCameraRequested As Action(Of String) Implements IHubView.OpenCameraRequested
        Public Event SaveConfigRequested As Action(Of CameraConfig, String) Implements IHubView.SaveConfigRequested
        Public Event FetchCertificateRequested As Action(Of String) Implements IHubView.FetchCertificateRequested
        Public Event ManageUsersRequested As Action Implements IHubView.ManageUsersRequested
        Public Event ViewAuditRequested As Action Implements IHubView.ViewAuditRequested
        Public Event LogoutRequested As Action Implements IHubView.LogoutRequested
        Public ReadOnly Property Errors As New List(Of String)()
        Public ReadOnly Property Cameras As New List(Of CameraConfig)()
        Public ReadOnly Property Status As New Dictionary(Of String, String)()
        Public Property GatewayText As String
        Public Sub SetUser(u As String, r As String) Implements IHubView.SetUser
        End Sub
        Public Sub ApplyPermissions(p As Permission) Implements IHubView.ApplyPermissions
        End Sub
        Public Sub ShowCameras(c As IReadOnlyList(Of CameraConfig)) Implements IHubView.ShowCameras
            Cameras.Clear()
            Cameras.AddRange(c)
        End Sub
        Public Sub ShowCertificateFingerprint(k As String, s As String) Implements IHubView.ShowCertificateFingerprint
        End Sub
        Public Sub SetGatewayMode(t As String) Implements IHubView.SetGatewayMode
            GatewayText = t
        End Sub
        Public Sub SetCameraStatus(k As String, t As String, online As Boolean) Implements IHubView.SetCameraStatus
            SyncLock Status
                Status(k) = t
            End SyncLock
        End Sub
        Public Sub ShowError(m As String) Implements IMessageSink.ShowError
            SyncLock Errors
                Errors.Add(m)
            End SyncLock
        End Sub
        Public Sub ShowInfo(m As String) Implements IMessageSink.ShowInfo
        End Sub
        Public Sub RaiseOpen(k As String)
            RaiseEvent OpenCameraRequested(k)
        End Sub
        Public Sub RaiseSave()
            RaiseEvent SaveConfigRequested(New CameraConfig With {.Key = Se}, "")
        End Sub
        Public Sub RaiseFetch()
            RaiseEvent FetchCertificateRequested(Se)
        End Sub
        Public Sub RaiseUsers()
            RaiseEvent ManageUsersRequested()
        End Sub
        Public Sub RaiseAudit()
            RaiseEvent ViewAuditRequested()
        End Sub
        Public Sub RaiseLogout()
            RaiseEvent LogoutRequested()
        End Sub
    End Class

    Private NotInheritable Class FakeNav
        Implements IShellNavigator
        Public ReadOnly Property Opened As New List(Of CameraSessionDeps)()
        Public ReadOnly Property OpenedConfigs As New List(Of CameraConfig)()
        Public Property GatewayAudits As Integer
        Public Property LocalAudits As Integer
        Public Property Users As Integer
        Public Property LoggedOut As Integer
        Public Sub OpenCamera(config As CameraConfig, deps As CameraSessionDeps) Implements IShellNavigator.OpenCamera
            Opened.Add(deps)
            OpenedConfigs.Add(config)
        End Sub
        Public Sub OpenUserAdmin(session As UserSession) Implements IShellNavigator.OpenUserAdmin
            Users += 1
        End Sub
        Public Sub OpenAuditViewer(session As UserSession) Implements IShellNavigator.OpenAuditViewer
            LocalAudits += 1
        End Sub
        Public Sub OpenGatewayAudit(session As UserSession) Implements IShellNavigator.OpenGatewayAudit
            GatewayAudits += 1
        End Sub
        Public Sub Logout() Implements IShellNavigator.Logout
            LoggedOut += 1
        End Sub
    End Class

    Private NotInheritable Class FakeAuditView
        Implements IAuditView
        Public Event RefreshRequested As Action Implements IAuditView.RefreshRequested
        Public Event VerifyRequested As Action Implements IAuditView.VerifyRequested
        Public Property Entries As IReadOnlyList(Of AuditEntry)
        Public Property Verification As AuditVerifyResult
        Public ReadOnly Property Errors As New List(Of String)()
        Public Sub ShowEntries(e As IReadOnlyList(Of AuditEntry)) Implements IAuditView.ShowEntries
            Entries = e
        End Sub
        Public Sub ShowVerification(r As AuditVerifyResult) Implements IAuditView.ShowVerification
            Verification = r
        End Sub
        Public Sub ShowError(m As String) Implements IMessageSink.ShowError
            Errors.Add(m)
        End Sub
        Public Sub ShowInfo(m As String) Implements IMessageSink.ShowInfo
        End Sub
        Public Sub RaiseVerify()
            RaiseEvent VerifyRequested()
        End Sub
    End Class

    ' ----------------------------- أدوات -----------------------------
    Private Shared Function Handler(h As Harness) As Func(Of HttpMessageHandler)
        Return Function() New AddressHeaderHandler(h.App.GetTestServer().CreateHandler())
    End Function

    Private Shared Function TempDir() As String
        Dim d As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-shell-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(d)
        Return d
    End Function

    Private Shared Function Input(h As Harness, user As String, Optional password As String = Harness.Pw, Optional totp As String = Nothing) As GatewayLoginInput
        Return New GatewayLoginInput With {.Url = "http://localhost/", .AllowLoopbackHttp = True, .Pin = "", .UserName = user, .Password = password, .Totp = If(totp, h.NextCode(user))}
    End Function

    Private Shared Function Connect(h As Harness, user As String, ByRef dir As String) As GatewayHubContext
        dir = TempDir()
        Dim view As New FakeLoginView()
        Dim presenter As New GatewayLoginPresenter(view, New GatewaySettingsStore(dir), Nothing, h.Clock, Handler(h))
        Dim ctx As GatewayHubContext = Nothing
        AddHandler presenter.Connected, Sub(c) ctx = c
        view.RaiseConnect(Input(h, user))
        WaitFor(Function() view.Closed OrElse view.Errors.Count > 0)
        Assert.True(view.Closed, String.Join(" | ", view.Errors))
        Return ctx
    End Function

    ' ----------------------------- دخول البوابة -----------------------------
    <Fact>
    Public Sub LoginPresenter_Success_RaisesContext_PersistsOnlyEndpointAndPin()
        Using h As New Harness()
            WaitOnline(h)
            Dim dir As String = TempDir()
            Dim view As New FakeLoginView()
            Dim presenter As New GatewayLoginPresenter(view, New GatewaySettingsStore(dir), Nothing, h.Clock, Handler(h))
            Dim ctx As GatewayHubContext = Nothing
            AddHandler presenter.Connected, Sub(c) ctx = c
            Assert.Equal("", view.Loaded.BaseUrl)
            Dim inp As GatewayLoginInput = Input(h, "alice")
            inp.Pin = ""
            view.RaiseConnect(inp)
            WaitFor(Function() view.Closed)
            Assert.True(view.Closed, String.Join(" | ", view.Errors))
            Assert.NotNull(ctx)
            Assert.Equal(Role.Operator, ctx.Session.Role)
            Assert.Equal({"de7232iw", "se7c432mw"}, ctx.Cameras.Select(Function(c) c.Id).OrderBy(Function(x) x).ToArray())
            Assert.True(ctx.Client.IsSignedIn)
            Assert.False(view.Busy)
            Dim saved As String = IO.File.ReadAllText(IO.Path.Combine(dir, "gateway-endpoint.json"))
            Assert.Contains("localhost", saved)
            Assert.DoesNotContain(Harness.Pw, saved)
            Assert.DoesNotContain("alice", saved)
            Assert.DoesNotContain("ptzs_", saved)
            ctx.Client.Dispose()
        End Using
    End Sub

    <Fact>
    Public Sub LoginPresenter_BadCredentials_ShowsAnArabicError_DisposesTheClient_AndRaisesNothing()
        Using h As New Harness()
            Dim view As New FakeLoginView()
            Dim presenter As New GatewayLoginPresenter(view, New GatewaySettingsStore(TempDir()), Nothing, h.Clock, Handler(h))
            Dim raised As Boolean = False
            AddHandler presenter.Connected, Sub(c) raised = True
            view.RaiseConnect(Input(h, "alice", "Wrong-Passw0rd!"))
            WaitFor(Function() view.Errors.Count > 0, 8)
            Assert.Contains("بيانات الدخول", view.Errors.Single())
            Assert.False(raised)
            Assert.False(view.Closed)
            Assert.False(view.Busy)
        End Using
    End Sub

    <Fact>
    Public Sub LoginPresenter_RejectsBadInputLocally_BeforeAnyNetworkCall()
        Using h As New Harness()
            Dim view As New FakeLoginView()
            Dim presenter As New GatewayLoginPresenter(view, New GatewaySettingsStore(TempDir()), Nothing, h.Clock, Handler(h))
            Dim before As Integer = h.RequestsSeen
            Dim bad As New List(Of GatewayLoginInput) From {
                New GatewayLoginInput With {.Url = "http://localhost/", .AllowLoopbackHttp = True, .UserName = "", .Password = "x", .Totp = "123456"},
                New GatewayLoginInput With {.Url = "http://localhost/", .AllowLoopbackHttp = True, .UserName = "alice", .Password = "", .Totp = "123456"},
                New GatewayLoginInput With {.Url = "http://localhost/", .AllowLoopbackHttp = True, .UserName = "alice", .Password = "x", .Totp = "12ab56"},
                New GatewayLoginInput With {.Url = "http://gateway.example/", .AllowLoopbackHttp = False, .UserName = "alice", .Password = "x", .Totp = "123456"},
                New GatewayLoginInput With {.Url = "https://gw/", .Pin = "md5:zzz", .UserName = "alice", .Password = "x", .Totp = "123456"},
                New GatewayLoginInput With {.Url = "not a url", .UserName = "alice", .Password = "x", .Totp = "123456"}}
            Dim n As Integer = 0
            For Each i As GatewayLoginInput In bad
                n += 1
                view.RaiseConnect(i)
                WaitFor(Function() view.Errors.Count >= n)
            Next
            Assert.Equal(bad.Count, view.Errors.Count)
            Assert.Equal(before, h.RequestsSeen)
        End Using
    End Sub

    <Fact>
    Public Sub LoginPresenter_CertificateFetch_ReportsUnreachableHosts_AndRejectsNonHttps()
        Using h As New Harness()
            Dim view As New FakeLoginView()
            Dim presenter As New GatewayLoginPresenter(view, New GatewaySettingsStore(TempDir()), Nothing, h.Clock, Handler(h))
            view.RaiseFetch("http://gw.example/")
            WaitFor(Function() view.Errors.Count >= 1)
            Assert.Contains("https", view.Errors(0))
            view.RaiseFetch("https://127.0.0.1:" & Harness.FreePort() & "/")
            WaitFor(Function() view.Errors.Count >= 2, 12)
            Assert.Contains("تعذّر", view.Errors(1))
            Assert.Empty(view.Offered)
        End Using
    End Sub

    ' ----------------------------- المحور في وضع البوابة -----------------------------
    Private Shared Function Hub(h As Harness, ctx As GatewayHubContext, view As FakeHubView, nav As FakeNav, dir As String) As HubPresenter
        Return New HubPresenter(view, ctx, New AccessControl(h.Clock), New ListAudit(), New AlarmBus(), New FakeDecoder(), New ImmediateDispatcher(), nav, dir)
    End Function

    <Fact>
    Public Sub Hub_GatewayMode_ListsTheGatewayCameras_AndOpensThemThroughTheGatewayBackend()
        Using h As New Harness()
            WaitOnline(h)
            Dim dir As String = Nothing
            Dim ctx As GatewayHubContext = Connect(h, "alice", dir)
            Dim view As New FakeHubView()
            Dim nav As New FakeNav()
            Using hub_ As HubPresenter = Hub(h, ctx, view, nav, dir)
                Assert.Equal("localhost", view.GatewayText)
                Assert.Equal({"Gate", "Yard"}, view.Cameras.Select(Function(c) c.DisplayName).OrderByDescending(Function(x) x).Reverse().ToArray())
                Assert.Equal(2, view.Cameras.Count)
                Assert.All(view.Cameras, Sub(c) Assert.Equal("", c.Host))                     ' لا عناوين كاميرات على جهاز العميل
                Assert.Equal(2, view.Status.Count)
                view.RaiseOpen(Se)
                Assert.Single(nav.Opened)
                Assert.IsType(Of GatewayBackendFactory)(nav.Opened(0).BackendFactory)
                Assert.Null(nav.Opened(0).Connection)
                Assert.Equal("Gate", nav.Opened(0).CameraName)
                ' الإعدادات المحلية للكاميرات معطّلة في هذا الوضع
                view.RaiseSave()
                view.RaiseFetch()
                Assert.Equal(2, view.Errors.Count)
                Assert.All(view.Errors, Sub(e) Assert.Equal(UiText.ManagedOnGateway, e))
                ' لا ملفات إعداد كاميرات محلية أُنشئت
                Assert.False(IO.File.Exists(IO.Path.Combine(dir, "cameras.json")))
            End Using
            ctx.Client.Dispose()
        End Using
    End Sub

    <Fact>
    Public Sub Hub_GatewayMode_UsersAreCliOnly_AuditOpensTheRemoteViewer_AndPermissionsStillApply()
        Using h As New Harness()
            WaitOnline(h)
            Dim dir As String = Nothing
            Dim sup As GatewayHubContext = Connect(h, "sup", dir)
            Dim viewS As New FakeHubView()
            Dim navS As New FakeNav()
            Using hubS As HubPresenter = Hub(h, sup, viewS, navS, dir)
                viewS.RaiseAudit()
                Assert.Equal(1, navS.GatewayAudits)
                Assert.Equal(0, navS.LocalAudits)
                viewS.RaiseUsers()                           ' المشرف لا يملك ManageUsers أصلاً
                Assert.Equal(0, navS.Users)
                Assert.Contains(UiText.NoPermission, viewS.Errors)
            End Using
            sup.Client.Dispose()
            Dim dir2 As String = Nothing
            Dim op As GatewayHubContext = Connect(h, "alice", dir2)
            Dim viewO As New FakeHubView()
            Dim navO As New FakeNav()
            Using hubO As HubPresenter = Hub(h, op, viewO, navO, dir2)
                viewO.RaiseAudit()
                Assert.Equal(0, navO.GatewayAudits)
                Assert.Contains(UiText.NoPermission, viewO.Errors)
            End Using
            op.Client.Dispose()
        End Using
    End Sub

    <Fact>
    Public Sub Hub_GatewayMode_Logout_RevokesTheServerSession_AndSessionEndSendsTheUserBackToLogin()
        Using h As New Harness()
            WaitOnline(h)
            Dim dir As String = Nothing
            Dim ctx As GatewayHubContext = Connect(h, "alice", dir)
            Dim view As New FakeHubView()
            Dim nav As New FakeNav()
            Using hub_ As HubPresenter = Hub(h, ctx, view, nav, dir)
                view.RaiseLogout()
                WaitFor(Function() nav.LoggedOut >= 1)
                Assert.Equal(1, nav.LoggedOut)
                Assert.False(ctx.Client.IsSignedIn)
                Assert.Contains("Auth.Logout", String.Join(vbLf, h.AuditLines()))
            End Using
            ctx.Client.Dispose()

            Dim dir2 As String = Nothing
            Dim ctx2 As GatewayHubContext = Connect(h, "sup", dir2)
            Dim view2 As New FakeHubView()
            Dim nav2 As New FakeNav()
            Using hub2 As HubPresenter = Hub(h, ctx2, view2, nav2, dir2)
                h.Svc.Users.SetDisabled("setup", "sup", True)
                Assert.ThrowsAny(Of Exception)(Function() ctx2.Client.RequestAsync(HttpMethod.Get, "/api/v1/cameras", Nothing, Threading.CancellationToken.None).GetAwaiter().GetResult())
                WaitFor(Function() nav2.LoggedOut >= 1)
                Assert.Equal(1, nav2.LoggedOut)
                SyncLock view2.Errors
                    Assert.Contains(UiText.SessionExpired, view2.Errors)
                End SyncLock
            End Using
            ctx2.Client.Dispose()
        End Using
    End Sub

    ' ----------------------------- عارض التدقيق -----------------------------
    <Fact>
    Public Sub GatewayAudit_ShowsEntriesAndVerification_ForSupervisors_AndDeniesOperators()
        Using h As New Harness()
            WaitOnline(h)
            Dim dir As String = Nothing
            Dim sup As GatewayHubContext = Connect(h, "sup", dir)
            Dim v As New FakeAuditView()
            Dim p As New GatewayAuditPresenter(v, sup.Client)
            WaitFor(Function() v.Entries IsNot Nothing)
            Assert.NotEmpty(v.Entries)
            Assert.Contains(v.Entries, Function(e) e.Action = "Auth.RemoteLogin")
            h.Clock.Advance(5)
            v.RaiseVerify()
            WaitFor(Function() v.Verification IsNot Nothing)
            Assert.True(v.Verification.IsValid)
            Assert.True(v.Verification.EntriesChecked > 0)
            sup.Client.Dispose()

            Dim dir2 As String = Nothing
            Dim op As GatewayHubContext = Connect(h, "alice", dir2)
            Dim v2 As New FakeAuditView()
            Dim p2 As New GatewayAuditPresenter(v2, op.Client)
            WaitFor(Function() v2.Errors.Count > 0)
            Assert.Equal(UiText.NoPermission, v2.Errors.First())
            Assert.Null(v2.Entries)
            op.Client.Dispose()
        End Using
    End Sub
End Class
