Imports Ptz.Core.Security

Public Enum LoginMode
    SignIn
    FirstRunCreateAdmin
End Enum

Public Interface ILoginView
    Inherits IMessageSink
    Event SubmitRequested As Action(Of String, String, String)
    Sub SetMode(mode As LoginMode)
    Sub SetBusy(busy As Boolean)
    Sub CloseWithSuccess()
End Interface

Public Interface IHubView
    Inherits IMessageSink
    Event OpenCameraRequested As Action(Of String)
    Event SaveConfigRequested As Action(Of CameraConfig, String)
    Event FetchCertificateRequested As Action(Of String)
    Event ManageUsersRequested As Action
    Event ViewAuditRequested As Action
    Event LogoutRequested As Action

    Sub SetUser(userName As String, role As String)
    Sub ApplyPermissions(permissions As Permission)
    Sub ShowCameras(cameras As IReadOnlyList(Of CameraConfig))
    ''' <summary>يعرض بصمة الشهادة ليؤكدها المسؤول قبل حفظها.</summary>
    Sub ShowCertificateFingerprint(key As String, sha256 As String)
End Interface

''' <summary>تفتح نوافذ الكاميرات والإدارة (تنفيذها في طبقة الواجهة).</summary>
Public Interface IShellNavigator
    Sub OpenCamera(config As CameraConfig, deps As CameraSessionDeps)
    Sub OpenUserAdmin(session As UserSession)
    Sub OpenAuditViewer(session As UserSession)
    Sub Logout()
End Interface

Public Interface IUserAdminView
    Inherits IMessageSink
    Event CreateRequested As Action(Of String, String, Role)
    Event SetRoleRequested As Action(Of String, Role)
    Event SetDisabledRequested As Action(Of String, Boolean)
    Event ChangePasswordRequested As Action(Of String, String)
    Sub ShowUsers(users As IReadOnlyList(Of UserRecord))
End Interface

Public Interface IAuditView
    Inherits IMessageSink
    Event RefreshRequested As Action
    Event VerifyRequested As Action
    Sub ShowEntries(entries As IReadOnlyList(Of AuditEntry))
    Sub ShowVerification(result As AuditVerifyResult)
End Interface
