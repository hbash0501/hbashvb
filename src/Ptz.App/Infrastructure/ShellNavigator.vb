Imports System.Windows.Forms
Imports Ptz.App.Views
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Presentation

Namespace Infrastructure

    ''' <summary>يفتح نوافذ الكاميرات والإدارة ويغلقها عند الخروج. لكل كاميرا نافذة مستقلة.</summary>
    Public NotInheritable Class ShellNavigator
        Implements IShellNavigator

        Private ReadOnly _services As AppServices
        Private ReadOnly _hub As Form
        Private ReadOnly _gateway As GatewayClient
        Private ReadOnly _open As New Dictionary(Of String, Form)()

        Public Property LogoutRequested As Boolean

        Public Sub New(services As AppServices, hub As Form, Optional gateway As GatewayClient = Nothing)
            _services = services
            _hub = hub
            _gateway = gateway
        End Sub

        Public Sub OpenCamera(config As CameraConfig, deps As CameraSessionDeps) Implements IShellNavigator.OpenCamera
            Dim existing As Form = Nothing
            If _open.TryGetValue(config.Key, existing) AndAlso Not existing.IsDisposed Then
                existing.Activate()
                Return
            End If
            Dim frm As Form
            Select Case config.Key
                Case HubPresenter.KeySe7c432mw : frm = New FrmDs2Se7c432mw(deps)
                Case HubPresenter.KeyDe7232iw : frm = New FrmDs2De7232iw(deps)
                Case Else : Throw New InvalidOperationException("طراز غير مدعوم: " & config.Key)
            End Select
            _open(config.Key) = frm
            AddHandler frm.FormClosed, Sub() _open.Remove(config.Key)
            frm.Show(_hub)
        End Sub

        Public Sub OpenUserAdmin(session As UserSession) Implements IShellNavigator.OpenUserAdmin
            Dim frm As New FrmUsers()
            Dim presenter As New UserAdminPresenter(frm, _services.Users, session, _services.Access)
            frm.ShowDialog(_hub)
            frm.Dispose()
        End Sub

        Public Sub OpenAuditViewer(session As UserSession) Implements IShellNavigator.OpenAuditViewer
            Dim frm As New FrmAudit()
            Dim presenter As New AuditPresenter(frm, _services.Audit, session, _services.Access)
            frm.ShowDialog(_hub)
            frm.Dispose()
        End Sub

        Public Sub OpenGatewayAudit(session As UserSession) Implements IShellNavigator.OpenGatewayAudit
            If _gateway Is Nothing Then Throw New InvalidOperationException("لا اتصال ببوابة.")
            Dim frm As New FrmAudit()
            Dim presenter As New GatewayAuditPresenter(frm, _gateway)
            frm.ShowDialog(_hub)
            frm.Dispose()
        End Sub

        Public Sub Logout() Implements IShellNavigator.Logout
            LogoutRequested = True
            For Each f As Form In _open.Values.ToList()
                If Not f.IsDisposed Then f.Close()
            Next
            _hub.Close()
        End Sub
    End Class

End Namespace
