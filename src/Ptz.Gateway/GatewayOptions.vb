Imports System.Text.Json

''' <summary>إعدادات البوابة (ملف JSON). القيم الافتراضية هي الآمنة: استماع على الحلقة المحلية فقط، TOTP إلزامي.</summary>
Public NotInheritable Class ListenEndpoint
    Public Property Address As String = "127.0.0.1"
    Public Property Port As Integer = 8443
End Class

Public NotInheritable Class TlsOptions
    Public Property PfxPath As String = ""
    ''' <summary>اسم متغير البيئة الذي يحوي كلمة مرور PFX (لا تُكتب في الملف).</summary>
    Public Property PfxPasswordEnv As String = ""
    Public Property PemPath As String = ""
    Public Property PemKeyPath As String = ""
End Class

Public NotInheritable Class AuthOptions
    Public Property RequireTotp As Boolean = True
    Public Property AllowNoMfa As Boolean = False
    Public Property IdleMinutes As Integer = 30
    Public Property AbsoluteHours As Integer = 12
    Public Property MaxSessionsPerUser As Integer = 5
    Public Property StepUpMinutes As Integer = 5
    ''' <summary>الدور الأقصى للجلسات البعيدة (Viewer/Operator/Supervisor/Administrator).</summary>
    Public Property RemoteMaxRole As String = "Supervisor"
End Class

Public NotInheritable Class StreamOptions
    Public Property MaxPerUser As Integer = 6
    Public Property MaxGlobal As Integer = 12
    Public Property HeartbeatSeconds As Integer = 15
    Public Property MaxLifetimeMinutes As Integer = 60
End Class

Public NotInheritable Class PtzOptions
    Public Property DefaultTtlMs As Integer = 1200
    Public Property MinTtlMs As Integer = 200
    Public Property MaxTtlMs As Integer = 2000
    Public Property ForwardMinIntervalMs As Integer = 300
End Class

Public NotInheritable Class FrameOptions
    Public Property GatingEnabled As Boolean = True
    Public Property IdleRefreshSeconds As Double = 3
    Public Property ChangeThreshold As Double = 2.5
    Public Property MaxTier As String = "hd"
End Class

Public NotInheritable Class WebhookOptions
    Public Property Url As String = ""
    Public Property SecretEnv As String = ""
End Class

Public NotInheritable Class GatewayOptions
    ''' <summary>Loopback | Vpn | Tunnel | Public — يحدد صرامة المدقّق عند الإقلاع فقط.</summary>
    Public Property ExposureMode As String = "Loopback"
    Public Property Listen As List(Of ListenEndpoint) = New List(Of ListenEndpoint) From {New ListenEndpoint()}
    Public Property Tls As New TlsOptions()
    Public Property AllowedHosts As List(Of String) = New List(Of String) From {"localhost", "127.0.0.1", "[::1]"}
    Public Property TrustedProxies As List(Of String) = New List(Of String)()
    Public Property DataDir As String = ""
    Public Property AuditAnchorFile As String = ""
    Public Property Auth As New AuthOptions()
    Public Property Streams As New StreamOptions()
    Public Property Ptz As New PtzOptions()
    Public Property Frames As New FrameOptions()
    Public Property StartEngines As Boolean = True
    Public Property Webhook As New WebhookOptions()

    Public Shared Function DefaultDataDir() As String
        If OperatingSystem.IsWindows() Then
            Return IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PtzGateway")
        End If
        Return "/var/lib/ptz-gateway"
    End Function

    ''' <summary>يحمّل الإعداد من الملف (أو الافتراضي إن لم يوجد). ملف تالف ⇒ استثناء واضح (الإقلاع يتوقف).</summary>
    Public Shared Function Load(path As String) As GatewayOptions
        Dim o As GatewayOptions
        If String.IsNullOrEmpty(path) OrElse Not IO.File.Exists(path) Then
            o = New GatewayOptions()
        Else
            Dim opts As New JsonSerializerOptions(JsonSerializerDefaults.Web) With {.ReadCommentHandling = JsonCommentHandling.Skip, .AllowTrailingCommas = True}
            o = JsonSerializer.Deserialize(Of GatewayOptions)(IO.File.ReadAllText(path), opts)
            If o Is Nothing Then o = New GatewayOptions()
        End If
        If String.IsNullOrWhiteSpace(o.DataDir) Then o.DataDir = DefaultDataDir()
        Return o
    End Function
End Class
