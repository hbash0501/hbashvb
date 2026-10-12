Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Gateway.Audit
Imports Ptz.Gateway.Cameras
Imports Ptz.Gateway.Imaging
Imports Ptz.Presentation

Namespace Hosting

    ''' <summary>
    ''' الخدمات المشتركة للبوابة (حاوية يدوية بلا حقن تبعيات: كل شيء صريح وقابل للاستبدال في الاختبارات).
    ''' </summary>
    Public NotInheritable Class GatewayServices
        Public ReadOnly Property Options As GatewayOptions
        Public ReadOnly Property Clock As IClock
        Public ReadOnly Property Users As UserStore
        Public ReadOnly Property Totp As TotpStore
        Public ReadOnly Property Vault As CredentialVault
        Public ReadOnly Property CameraConfigs As CameraConfigStore
        Public ReadOnly Property AuditFile As FileAuditLog
        Public ReadOnly Property AuditHealth As New AuditHealth()
        Public ReadOnly Property Audit As IAuditLog
        Public ReadOnly Property RemoteAudit As RemoteAuditLog
        Public ReadOnly Property Sessions As SessionRegistry
        Public ReadOnly Property Throttle As LoginThrottle
        Public ReadOnly Property Access As AccessControl
        Public ReadOnly Property Decoder As IFrameDecoder
        Public ReadOnly Property Transcoder As IJpegTranscoder
        Public ReadOnly Property Alarms As AlarmLog
        Public ReadOnly Property Denied As DeniedAggregator
        Public ReadOnly Property StartedUtc As DateTime
        Public ReadOnly Property LoginGate As New Threading.SemaphoreSlim(Math.Max(1, Environment.ProcessorCount \ 2), Math.Max(1, Environment.ProcessorCount \ 2))
        Public ReadOnly Property AlarmSinks As New List(Of IAlarmSink)()
        Public Property Cameras As CameraRegistry

        ''' <summary>مصنع معالج HTTP للكاميرا (الإنتاج: معالج آمن + قاطع دائرة؛ الاختبارات: معالج وهمي).</summary>
        Public Property CameraHandlerFactory As Func(Of CameraConnection, HttpMessageHandler)

        Public Property MaxRole As Role = Role.Supervisor

        Public Sub New(options As GatewayOptions, clock As IClock, users As UserStore, totp As TotpStore, vault As CredentialVault,
                       cameraConfigs As CameraConfigStore, auditFile As FileAuditLog, decoder As IFrameDecoder, transcoder As IJpegTranscoder)
            Me.Options = options
            Me.Clock = clock
            Me.Users = users
            Me.Totp = totp
            Me.Vault = vault
            Me.CameraConfigs = cameraConfigs
            Me.AuditFile = auditFile
            Me.Decoder = decoder
            Me.Transcoder = transcoder
            RemoteAudit = New RemoteAuditLog(auditFile, AuditHealth)
            Audit = RemoteAudit
            Sessions = New SessionRegistry(clock, TimeSpan.FromMinutes(options.Auth.IdleMinutes), TimeSpan.FromHours(options.Auth.AbsoluteHours), options.Auth.MaxSessionsPerUser)
            Throttle = New LoginThrottle(clock)
            Access = New AccessControl(clock)
            Alarms = New AlarmLog(clock)
            Denied = New DeniedAggregator(Audit)
            StartedUtc = clock.UtcNow
            Dim r As Role
            If WireMapParse(options.Auth.RemoteMaxRole, r) Then MaxRole = r
            CameraHandlerFactory = Function(conn) New CameraGuardHandler(IsapiClientHandler(conn), clock)
            Cameras = New CameraRegistry(Me)
        End Sub

        Private Shared Function WireMapParse(name As String, ByRef r As Role) As Boolean
            Return Ptz.Core.Remote.WireMap.ParseRole(name, r)
        End Function

        Private Shared Function IsapiClientHandler(conn As CameraConnection) As HttpMessageHandler
            Return Ptz.Core.Isapi.IsapiClient.CreateSecureHandler(conn)
        End Function

        ''' <summary>يُنشئ الخدمات للإنتاج من مجلد البيانات (المفتاح الرئيسي من PTZ_MASTER_KEY_FILE أو DataDir/master.key).</summary>
        Public Shared Function CreateProduction(options As GatewayOptions, Optional env As Func(Of String, String) = Nothing) As GatewayServices
            Dim getEnv As Func(Of String, String) = env
            If getEnv Is Nothing Then getEnv = Function(k As String) Environment.GetEnvironmentVariable(k)
            IO.Directory.CreateDirectory(options.DataDir)
            If Not OperatingSystem.IsWindows() Then IO.File.SetUnixFileMode(options.DataDir, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite Or IO.UnixFileMode.UserExecute)
            Dim keyPath As String = getEnv("PTZ_MASTER_KEY_FILE")
            If String.IsNullOrWhiteSpace(keyPath) Then keyPath = IO.Path.Combine(options.DataDir, "master.key")
            Dim keys As New FileKeyProvider(keyPath)
            Dim master As Byte() = keys.GetMasterKey()
            Dim clock As IClock = SystemClock.Instance
            Dim auditFile As New FileAuditLog(IO.Path.Combine(options.DataDir, "audit.log"), KeyDerivation.Derive(master, "audit"), clock)
            Dim users As New UserStore(IO.Path.Combine(options.DataDir, "users.json"), auditFile, clock)
            Dim vault As New CredentialVault(keys)
            Dim totp As New TotpStore(IO.Path.Combine(options.DataDir, "totp.json"), IO.Path.Combine(options.DataDir, "totp-state.json"), vault, clock)
            Dim cams As New CameraConfigStore(IO.Path.Combine(options.DataDir, "cameras.json"), vault)
            cams.EnsureDefaults({("se7c432mw", CameraProfiles.Ds2Se7c432mwAeb), ("de7232iw", CameraProfiles.Ds2De7232iwAe)})
            Dim svc As New GatewayServices(options, clock, users, totp, vault, cams, auditFile, New SkiaFrameDecoder(clock), New SkiaJpegTranscoder())
            svc.AlarmSinks.Add(New FileAlarmSink(IO.Path.Combine(options.DataDir, "alarms.jsonl")))
            If Not String.IsNullOrWhiteSpace(options.Webhook.Url) AndAlso Not String.IsNullOrWhiteSpace(options.Webhook.SecretEnv) Then
                Dim secret As String = getEnv(options.Webhook.SecretEnv)
                If Not String.IsNullOrEmpty(secret) Then
                    svc.AlarmSinks.Add(New WebhookAlarmSink(New Uri(options.Webhook.Url), Text.Encoding.UTF8.GetBytes(secret),
                                                            New SocketsHttpHandler With {.AllowAutoRedirect = False}))
                End If
            End If
            Return svc
        End Function
    End Class

End Namespace
