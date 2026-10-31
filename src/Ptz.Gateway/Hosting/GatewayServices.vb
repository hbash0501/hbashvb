Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Gateway.Audit
Imports Ptz.Gateway.Cameras
Imports Ptz.Gateway.Cloud
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
        ''' <summary>تخزين مؤقت عام صغير (مثل نتيجة التحقق من سلسلة التدقيق).</summary>
        Public ReadOnly Property Cache As New Collections.Concurrent.ConcurrentDictionary(Of String, Object)()
        Public ReadOnly Property StreamQuota As New StreamQuotas()

        ''' <summary>مصنع معالج HTTP للكاميرا (الإنتاج: معالج آمن + قاطع دائرة؛ الاختبارات: معالج وهمي).</summary>
        Public Property CameraHandlerFactory As Func(Of CameraConnection, HttpMessageHandler)

        Public Property MaxRole As Role = Role.Supervisor

        ''' <summary>عميل Hik-Partner Pro (Nothing ما لم يُفعَّل الخيار وتُضبط المفاتيح). مشترك بين كاميرات النقل hpp.</summary>
        Public Property Hpp As HppCloud
        Private ReadOnly _hppAuditThrottle As New Collections.Concurrent.ConcurrentDictionary(Of String, DateTime)(StringComparer.Ordinal)

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
            CameraHandlerFactory = Function(conn) CreateCameraHandler(conn)
            Cameras = New CameraRegistry(Me)
        End Sub

        ''' <summary>
        ''' معالج الكاميرا الإنتاجي: اتصال ISAPI مباشر آمن (lan) أو معالج ترحيل Hik-Partner Pro (hpp)؛ كلاهما خلف قاطع الدائرة.
        ''' الاختبارات تستبدل <see cref="CameraHandlerFactory"/> كله أو تفوّض إلى هذه الدالة لكاميرات hpp.
        ''' </summary>
        Public Function CreateCameraHandler(conn As CameraConnection) As HttpMessageHandler
            If conn.IsRelay Then
                If Hpp Is Nothing Then Throw New InvalidOperationException("النقل السحابي غير مفعّل أو مفاتيحه غير مضبوطة (Hpp.Enabled + متغيرا البيئة).")
                Return New CameraGuardHandler(New HppRelayHandler(Hpp, conn.CloudSerial, conn.TimeoutSeconds), Clock)
            End If
            Return New CameraGuardHandler(IsapiClientHandler(conn), Clock)
        End Function

        ''' <summary>يربط عميل السحابة ويدوّن أخطاءه في التدقيق (مرة كل دقيقة لكل (رمز،جهاز) كي لا يفيض السجل عند انقطاع طويل).</summary>
        Public Sub AttachHpp(cloud As HppCloud)
            Hpp = cloud
            AddHandler cloud.CallCompleted, AddressOf OnHppCall
        End Sub

        Private Sub OnHppCall(sender As Object, e As HppCallEventArgs)
            If e.Outcome = HppOutcome.Ok Then Return
            Dim key As String = e.Serial & "|" & e.Code
            Dim now As DateTime = Clock.UtcNow
            Dim last As DateTime = DateTime.MinValue
            If _hppAuditThrottle.TryGetValue(key, last) AndAlso (now - last).TotalSeconds < 60 Then Return
            _hppAuditThrottle(key) = now
            If _hppAuditThrottle.Count > 256 Then _hppAuditThrottle.Clear()
            Dim extra As String = If(e.DeviceCode <> "", " dev=" & e.DeviceCode, "") & If(e.EzoCode <> "", " ezo=" & e.EzoCode, "")
            Audit.Write("svc:hpp", "Hpp.Error", e.Serial, "FAIL", $"{e.Outcome} {e.Code} http={e.HttpStatus}{extra}")
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
            If options.Hpp.Enabled Then
                Dim ak As String = getEnv(options.Hpp.AppKeyEnv)
                Dim sk As String = getEnv(options.Hpp.SecretKeyEnv)
                If Not String.IsNullOrWhiteSpace(ak) AndAlso Not String.IsNullOrWhiteSpace(sk) Then svc.AttachHpp(New HppCloud(options.Hpp, ak, sk, clock))
            End If
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

''' <summary>حصص بثوث SSE: لكل مستخدم وعامة (تُحرَّر في Finally متزامن).</summary>
Public NotInheritable Class StreamQuotas
    Private ReadOnly _lock As New Object()
    Private ReadOnly _perUser As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private _global As Integer

    Public Function TryAcquire(user As String, maxPerUser As Integer, maxGlobal As Integer) As Boolean
        SyncLock _lock
            Dim n As Integer = 0
            _perUser.TryGetValue(user, n)
            If n >= maxPerUser OrElse _global >= maxGlobal Then Return False
            _perUser(user) = n + 1
            _global += 1
            Return True
        End SyncLock
    End Function

    Public Sub Release(user As String)
        SyncLock _lock
            Dim n As Integer = 0
            If _perUser.TryGetValue(user, n) Then
                If n <= 1 Then _perUser.Remove(user) Else _perUser(user) = n - 1
                _global = Math.Max(0, _global - 1)
            End If
        End SyncLock
    End Sub

    Public ReadOnly Property Total As Integer
        Get
            SyncLock _lock
                Return _global
            End SyncLock
        End Get
    End Property
End Class

End Namespace
