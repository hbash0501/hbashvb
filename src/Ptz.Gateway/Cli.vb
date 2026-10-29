Imports System.Net.Http
Imports System.Security.Cryptography.X509Certificates
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Cameras
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Cloud
Imports Ptz.Presentation

''' <summary>
''' أداة الإدارة المحلية على مضيف البوابة (لا يوجد API بعيد لإدارة المستخدمين أو TOTP أو الكاميرات أو الشهادات).
''' تكتب سلسلة تدقيق مستقلة (audit-admin.log) كي لا تتفرع سلسلة الخدمة (لها كاتب واحد).
''' </summary>
Public NotInheritable Class Cli
    Private ReadOnly _out As IO.TextWriter
    Private ReadOnly _in As IO.TextReader
    Private ReadOnly _options As GatewayOptions
    Private ReadOnly _clock As IClock = SystemClock.Instance
    Private ReadOnly _keys As FileKeyProvider
    Private ReadOnly _admin As FileAuditLog
    Private ReadOnly _actor As String
    Private ReadOnly _readSecret As Func(Of String, String)
    Private ReadOnly _env As Func(Of String, String)
    Private ReadOnly _hppHandler As HttpMessageHandler

    ''' <param name="hppHandler">للاختبارات: معالج بديل لنداءات Hik-Partner Pro (الإنتاج: اتصال حقيقي).</param>
    Public Sub New(options As GatewayOptions, [out] As IO.TextWriter, [in] As IO.TextReader, readSecret As Func(Of String, String), Optional env As Func(Of String, String) = Nothing,
                   Optional hppHandler As HttpMessageHandler = Nothing)
        _options = options
        _hppHandler = hppHandler
        _out = [out]
        _in = [in]
        _readSecret = readSecret
        Dim getEnv As Func(Of String, String) = env
        If getEnv Is Nothing Then getEnv = Function(k As String) Environment.GetEnvironmentVariable(k)
        _env = getEnv
        IO.Directory.CreateDirectory(options.DataDir)
        Dim keyPath As String = getEnv("PTZ_MASTER_KEY_FILE")
        If String.IsNullOrWhiteSpace(keyPath) Then keyPath = IO.Path.Combine(options.DataDir, "master.key")
        _keys = New FileKeyProvider(keyPath)
        _admin = New FileAuditLog(IO.Path.Combine(options.DataDir, "audit-admin.log"), KeyDerivation.Derive(_keys.GetMasterKey(), "audit-admin"), _clock)
        _actor = "cli:" & Environment.UserName
    End Sub

    Private Function Users() As UserStore
        Return New UserStore(IO.Path.Combine(_options.DataDir, "users.json"), _admin, _clock)
    End Function

    Private Function Totps() As TotpStore
        Return New TotpStore(IO.Path.Combine(_options.DataDir, "totp.json"), IO.Path.Combine(_options.DataDir, "totp-state-cli.json"), New CredentialVault(_keys), _clock)
    End Function

    Private Function Cameras() As CameraConfigStore
        Dim s As New CameraConfigStore(IO.Path.Combine(_options.DataDir, "cameras.json"), New CredentialVault(_keys))
        s.EnsureDefaults({("se7c432mw", CameraProfiles.Ds2Se7c432mwAeb), ("de7232iw", CameraProfiles.Ds2De7232iwAe)})
        Return s
    End Function

    Private Function ReadNewPassword(userName As String) As String
        Dim p1 As String = _readSecret("كلمة المرور الجديدة: ")
        Dim p2 As String = _readSecret("أعد كتابتها: ")
        If p1 <> p2 Then Throw New ArgumentException("كلمتا المرور غير متطابقتين.")
        Dim err As String = UserStore.ValidatePasswordPolicy(userName, p1)
        If err IsNot Nothing Then Throw New ArgumentException(err)
        Return p1
    End Function

    ''' <summary>ينفّذ أمراً ويعيد رمز الخروج (0 نجاح). الأخطاء المتوقعة تُطبع بلا أثر مكدّس.</summary>
    Public Function Run(args As String()) As Integer
        Try
            Return Dispatch(args)
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is KeyNotFoundException OrElse TypeOf ex Is HppCloudException
            _out.WriteLine("خطأ: " & ex.Message)
            Return 1
        Catch ex As HttpRequestException
            _out.WriteLine("خطأ: تعذّر الاتصال بخدمة Hik-Partner Pro (شبكة/مهلة).")
            Return 1
        End Try
    End Function

    Private Function Dispatch(args As String()) As Integer
        If args.Length < 1 Then Return Usage()
        Select Case args(0)
            Case "user" : Return UserCmd(args)
            Case "totp" : Return TotpCmd(args)
            Case "camera" : Return CameraCmd(args)
            Case "hpp" : Return HppCmd(args)
            Case "cert" : Return CertCmd(args)
            Case "revoke-all"
                IO.File.WriteAllText(IO.Path.Combine(_options.DataDir, "revoke.epoch"), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(Globalization.CultureInfo.InvariantCulture))
                _admin.Write(_actor, "Session.RevokeAll", "gateway", "OK", "")
                _out.WriteLine("سيُبطل الخادم كل الجلسات الحالية خلال ثوانٍ.")
                Return 0
            Case "rotate-audit"
                ' يُنفَّذ والخدمة متوقفة: يتحقق من سلامة السلسلة ثم يؤرشف audit.log ليبدأ سجل جديد (Seq=1) فلا ينمو بلا حدّ.
                Dim logPath As String = IO.Path.Combine(_options.DataDir, "audit.log")
                If Not IO.File.Exists(logPath) Then
                    _out.WriteLine("لا يوجد audit.log.")
                    Return 0
                End If
                Dim cur As New FileAuditLog(logPath, KeyDerivation.Derive(_keys.GetMasterKey(), "audit"), _clock)
                Dim chk As AuditVerifyResult = cur.Verify()
                If Not chk.IsValid Then
                    _out.WriteLine($"رُفض التدوير: السلسلة معطوبة عند السجل {chk.FirstBadSeq}. أرشفها يدوياً بعد التحقيق.")
                    Return 3
                End If
                Dim archive As String = IO.Path.Combine(_options.DataDir, $"audit-{_clock.UtcNow:yyyyMMdd-HHmmss}.log")
                Dim lastHash As String = cur.LastHash
                IO.File.Move(logPath, archive)
                If Not String.IsNullOrWhiteSpace(_options.AuditAnchorFile) Then
                    IO.File.AppendAllText(_options.AuditAnchorFile, $"{_clock.UtcNow:o} ROTATED {IO.Path.GetFileName(archive)} {lastHash}{Environment.NewLine}")
                End If
                _admin.Write(_actor, "Audit.Rotate", IO.Path.GetFileName(archive), "OK", $"entries={chk.EntriesChecked} last={lastHash}")
                _out.WriteLine($"أُرشف السجل ({chk.EntriesChecked} سجل) إلى {archive}. سيبدأ سجل جديد عند تشغيل الخدمة؛ احتفظ بالأرشيف (يُتحقَّق منه بمفرده بالمفتاح نفسه).")
                Return 0
            Case "verify-audit"
                Dim svcLog As New FileAuditLog(IO.Path.Combine(_options.DataDir, "audit.log"), KeyDerivation.Derive(_keys.GetMasterKey(), "audit"), _clock)
                Dim a As AuditVerifyResult = svcLog.Verify()
                Dim b As AuditVerifyResult = _admin.Verify()
                _out.WriteLine($"audit.log: {If(a.IsValid, "سليم", "معطوب عند " & a.FirstBadSeq)} ({a.EntriesChecked} سجل)")
                _out.WriteLine($"audit-admin.log: {If(b.IsValid, "سليم", "معطوب عند " & b.FirstBadSeq)} ({b.EntriesChecked} سجل)")
                Return If(a.IsValid AndAlso b.IsValid, 0, 3)
            Case Else : Return Usage()
        End Select
    End Function

    Private Function Usage() As Integer
        _out.WriteLine("الاستخدام: PtzGateway [serve|check|user|totp|camera|hpp|cert|revoke-all|verify-audit|rotate-audit] ...")
        _out.WriteLine("  user add <name> <role> | passwd <name> | role <name> <role> | disable|enable|unlock <name> | list")
        _out.WriteLine("  totp enroll <name> | reset <name>")
        _out.WriteLine("  camera list | set <key> --host H [--port P] --user U [--http-insecure] [--pin <sha256>] [--name N]")
        _out.WriteLine("  camera set <key> --transport hpp --cloud-serial <SERIAL> [--name N]   (Hik-Partner Pro بدل الاتصال المباشر)")
        _out.WriteLine("  hpp devices | hpp check [<key>] [--stop]   (يتطلب متغيري البيئة للمفاتيح؛ check يقرأ ولا يحرّك، و--stop يرسل إيقافاً فقط)")
        _out.WriteLine("  cert create --host <dns|ip> [--days 365] | cert show")
        Return 64
    End Function

    Private Function UserCmd(args As String()) As Integer
        If args.Length < 2 Then Return Usage()
        Dim store As UserStore = Users()
        Select Case args(1)
            Case "add"
                If args.Length < 4 Then Return Usage()
                Dim role As Role
                If Not WireMap.ParseRole(args(3), role) Then Throw New ArgumentException("دور غير معروف (viewer|operator|supervisor|administrator).")
                store.CreateUser(_actor, args(2), ReadNewPassword(args(2)), role)
                _out.WriteLine($"أُنشئ المستخدم {args(2)} ({role}). فعّل TOTP: totp enroll {args(2)}")
            Case "passwd"
                If args.Length < 3 Then Return Usage()
                store.ChangePassword(_actor, args(2), ReadNewPassword(args(2)))
                _out.WriteLine("تم تغيير كلمة المرور (تُبطل الجلسات القائمة).")
            Case "role"
                If args.Length < 4 Then Return Usage()
                Dim role As Role
                If Not WireMap.ParseRole(args(3), role) Then Throw New ArgumentException("دور غير معروف.")
                store.SetRole(_actor, args(2), role)
                _out.WriteLine("تم تغيير الدور.")
            Case "disable", "enable"
                If args.Length < 3 Then Return Usage()
                store.SetDisabled(_actor, args(2), args(1) = "disable")
                _out.WriteLine("تم.")
            Case "unlock"
                If args.Length < 3 Then Return Usage()
                store.Unlock(_actor, args(2))
                ' قفل الدخول البعيد في ذاكرة الخدمة: نطلب فكّه عبر ملف تلتقطه الخدمة خلال ثوانٍ
                IO.File.WriteAllText(IO.Path.Combine(_options.DataDir, "unlock." & args(2).ToLowerInvariant() & ".request"), "")
                _out.WriteLine("تم فك القفل المحلي وطُلب فك قفل الدخول البعيد.")
            Case "list"
                For Each u As UserRecord In store.ListUsers()
                    _out.WriteLine($"{u.UserName}  {u.Role}  {If(u.Disabled, "معطّل", "فعّال")}  TOTP={If(Totps().IsEnrolled(u.UserName), "نعم", "لا")}")
                Next
            Case Else : Return Usage()
        End Select
        Return 0
    End Function

    Private Function TotpCmd(args As String()) As Integer
        If args.Length < 3 Then Return Usage()
        Dim name As String = args(2)
        If Users().TryGet(name) Is Nothing Then Throw New KeyNotFoundException("المستخدم غير موجود.")
        Dim store As TotpStore = Totps()
        Select Case args(1)
            Case "enroll"
                Dim secret As Byte() = store.Enroll(name)
                _admin.Write(_actor, "Totp.Enroll", name, "OK", "")
                _out.WriteLine("سر TOTP (يُعرض مرة واحدة فقط — أضفه إلى تطبيق المصادقة الآن):")
                _out.WriteLine(Totp.Base32(secret))
                _out.WriteLine(Totp.ProvisioningUri("PTZ Gateway", name, secret))
            Case "reset"
                store.Reset(name)
                _admin.Write(_actor, "Totp.Reset", name, "OK", "")
                _out.WriteLine("أُزيل TOTP (تُبطل الجلسات القائمة).")
            Case Else : Return Usage()
        End Select
        Return 0
    End Function

    Private Shared Function Opt(args As String(), name As String) As String
        For i As Integer = 0 To args.Length - 2
            If args(i) = name Then Return args(i + 1)
        Next
        Return Nothing
    End Function

    Private Shared Function Flag(args As String(), name As String) As Boolean
        Return args.Contains(name)
    End Function

    Private Function CameraCmd(args As String()) As Integer
        If args.Length < 2 Then Return Usage()
        Dim store As CameraConfigStore = Cameras()
        Select Case args(1)
            Case "list"
                For Each c As CameraConfig In store.All()
                    If String.Equals(c.Transport, "hpp", StringComparison.OrdinalIgnoreCase) Then
                        _out.WriteLine($"{c.Key}  {c.ModelName}  hpp serial={If(c.CloudSerial = "", "(غير مضبوط)", c.CloudSerial)}")
                    Else
                        _out.WriteLine($"{c.Key}  {c.ModelName}  {If(c.Host = "", "(غير مضبوطة)", c.Host & ":" & c.Port)}  user={c.Username}  https={c.UseHttps}  pinned={c.PinnedCertSha256 <> ""}")
                    End If
                Next
            Case "set"
                If args.Length < 3 Then Return Usage()
                Dim cfg As CameraConfig = store.Get(args(2))
                If cfg Is Nothing Then Throw New KeyNotFoundException("كاميرا غير معروفة (se7c432mw | de7232iw).")
                Dim transport As String = Opt(args, "--transport")
                If transport IsNot Nothing Then cfg.Transport = CameraTransports.Name(CameraTransports.Parse(transport))
                Dim serial As String = Opt(args, "--cloud-serial")
                If serial IsNot Nothing Then cfg.CloudSerial = serial.Trim()
                Dim host As String = Opt(args, "--host")
                If host IsNot Nothing Then cfg.Host = host
                Dim port As String = Opt(args, "--port")
                If port IsNot Nothing Then cfg.Port = Integer.Parse(port, Globalization.CultureInfo.InvariantCulture)
                Dim user As String = Opt(args, "--user")
                If user IsNot Nothing Then cfg.Username = user
                Dim nm As String = Opt(args, "--name")
                If nm IsNot Nothing Then cfg.DisplayName = nm
                Dim pin As String = Opt(args, "--pin")
                If pin IsNot Nothing Then cfg.PinnedCertSha256 = pin
                If Flag(args, "--http-insecure") Then
                    cfg.UseHttps = False
                    cfg.AllowInsecureHttp = True
                    If port Is Nothing Then cfg.Port = 80
                End If
                Dim relay As Boolean = String.Equals(cfg.Transport, "hpp", StringComparison.OrdinalIgnoreCase)
                Dim pw As String = If(relay, "", _readSecret("كلمة مرور حساب الخدمة على الكاميرا (فارغة = بلا تغيير): "))
                store.Update(cfg, If(pw = "", Nothing, pw))
                If relay Then
                    _admin.Write(_actor, "Camera.Set", cfg.Key, "OK", $"transport=hpp serial={cfg.CloudSerial}")
                Else
                    _admin.Write(_actor, "Camera.Set", cfg.Key, "OK", $"{cfg.Host}:{cfg.Port} https={cfg.UseHttps} pinned={cfg.PinnedCertSha256 <> ""}")
                End If
                _out.WriteLine("تم الحفظ. أعد تشغيل الخدمة لتطبيق التغيير.")
            Case Else : Return Usage()
        End Select
        Return 0
    End Function

    ''' <summary>يبني عميل Hik-Partner Pro للأوامر الإدارية (المفاتيح من متغيرات البيئة فقط).</summary>
    Private Function BuildHpp() As HppCloud
        Dim ak As String = _env(_options.Hpp.AppKeyEnv)
        Dim sk As String = _env(_options.Hpp.SecretKeyEnv)
        If String.IsNullOrWhiteSpace(ak) OrElse String.IsNullOrWhiteSpace(sk) Then
            Throw New InvalidOperationException($"عيّن متغيري البيئة {_options.Hpp.AppKeyEnv} و{_options.Hpp.SecretKeyEnv} بمفاتيح Hik-Partner Pro (API Key/Secret).")
        End If
        Return New HppCloud(_options.Hpp, ak, sk, _clock, _hppHandler)
    End Function

    Private Shared Function OnlineText(status As Integer) As String
        Select Case status
            Case 1 : Return "متصل"
            Case 0 : Return "غير متصل"
            Case Else : Return "غير معروف"
        End Select
    End Function

    Private Function HppCmd(args As String()) As Integer
        If args.Length < 2 Then Return Usage()
        Select Case args(1)
            Case "devices"
                Using cloud As HppCloud = BuildHpp()
                    Dim devices As IReadOnlyList(Of HppDevice) = cloud.ListDevicesAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
                    Dim configured As New HashSet(Of String)(Cameras().All().Where(Function(c) c.CloudSerial <> "").Select(Function(c) c.CloudSerial), StringComparer.Ordinal)
                    _out.WriteLine($"{devices.Count} جهاز في الحساب:")
                    For Each d As HppDevice In devices
                        _out.WriteLine($"  {d.Serial,-20} {OnlineText(d.OnlineStatus),-9} فئة={d.Category} طراز={d.DeviceType} اسم={d.Name} موقع={d.SiteName}{If(configured.Contains(d.Serial), "   [مضبوط]", "")}")
                    Next
                    _admin.Write(_actor, "Hpp.Devices", "hpp", "OK", "count=" & devices.Count)
                    _out.WriteLine("استعمل: camera set <key> --transport hpp --cloud-serial <SERIAL>")
                End Using
                Return 0
            Case "check" : Return HppCheck(args)
            Case Else : Return Usage()
        End Select
    End Function

    ''' <summary>
    ''' تحقق ميداني بالمسار الإنتاجي نفسه (معالج الترحيل + المتحكم): اتصال/معلومات الجهاز، قراءة المواضع المسبقة، وبـ --stop أمر إيقاف
    ''' (متجه صفري فقط؛ لا حركة). لا يحرّك الكاميرا أبداً ولا يعدّل شيئاً.
    ''' </summary>
    Private Function HppCheck(args As String()) As Integer
        Dim only As String = If(args.Length > 2 AndAlso Not args(2).StartsWith("--", StringComparison.Ordinal), args(2), Nothing)
        Dim doStop As Boolean = Flag(args, "--stop")
        Dim store As CameraConfigStore = Cameras()
        Dim targets As List(Of CameraConfig) = store.All().Where(Function(c) String.Equals(c.Transport, "hpp", StringComparison.OrdinalIgnoreCase) AndAlso (only Is Nothing OrElse c.Key = only)).ToList()
        If targets.Count = 0 Then
            _out.WriteLine("لا كاميرات بنقل hpp (camera set <key> --transport hpp --cloud-serial S).")
            Return 1
        End If
        Dim failures As Integer = 0
        Using cloud As HppCloud = BuildHpp()
            For Each cfg As CameraConfig In targets
                _out.WriteLine($"[{cfg.Key}] serial={cfg.CloudSerial}")
                Dim conn As CameraConnection = store.BuildConnection(cfg.Key)
                Dim profile As CameraProfile = If(cfg.ModelName = CameraProfiles.Ds2De7232iwAe.ModelName, CameraProfiles.Ds2De7232iwAe, CameraProfiles.Ds2Se7c432mwAeb)
                Using ctl As ICameraController = CameraControllerFactory.Create(profile, conn, New HppRelayHandler(cloud, conn.CloudSerial))
                    If Not Step_("اتصال/معلومات الجهاز", Sub()
                                                          Dim info As DeviceInfo = ctl.ConnectAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
                                                          _out.WriteLine($"      طراز={info.Model} إصدار={info.FirmwareVersion} ميزات={ctl.Features}")
                                                      End Sub) Then failures += 1
                    If Not Step_("قراءة المواضع المسبقة", Sub()
                                                           Dim presets As IReadOnlyList(Of PtzPreset) = ctl.GetPresetsAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
                                                           _out.WriteLine($"      {presets.Count} موضع")
                                                       End Sub) Then failures += 1
                    If doStop AndAlso Not Step_("أمر إيقاف (لا حركة)", Sub() ctl.StopAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()) Then failures += 1
                End Using
            Next
        End Using
        _admin.Write(_actor, "Hpp.Check", only, If(failures = 0, "OK", "FAIL"), $"failures={failures} stop={doStop}")
        _out.WriteLine(If(failures = 0, "كل الفحوص نجحت.", $"فشل {failures} فحص."))
        Return If(failures = 0, 0, 4)
    End Function

    Private Function Step_(title As String, action As Action) As Boolean
        Try
            action()
            _out.WriteLine($"  OK    {title}")
            Return True
        Catch ex As Exception When TypeOf ex Is Ptz.Core.Isapi.IsapiException OrElse TypeOf ex Is NotSupportedException OrElse TypeOf ex Is HppCloudException OrElse TypeOf ex Is HttpRequestException
            Dim sub_ As String = ""
            Dim ie As Ptz.Core.Isapi.IsapiException = TryCast(ex, Ptz.Core.Isapi.IsapiException)
            If ie IsNot Nothing Then sub_ = $" http={ie.HttpStatus} [{AuditSanitizer.Clean(ie.SubStatus, 32)}]"
            Dim he As HppCloudException = TryCast(ex, HppCloudException)
            If he IsNot Nothing Then sub_ = $" [{he.Code}]"
            _out.WriteLine($"  FAIL  {title}:{sub_}")
            Return False
        End Try
    End Function

    Private Function CertCmd(args As String()) As Integer
        If args.Length < 2 Then Return Usage()
        Select Case args(1)
            Case "create"
                Dim host As String = Opt(args, "--host")
                If String.IsNullOrWhiteSpace(host) Then Throw New ArgumentException("--host مطلوب.")
                Dim days As Integer = 365
                Dim d As String = Opt(args, "--days")
                If d IsNot Nothing Then days = Integer.Parse(d, Globalization.CultureInfo.InvariantCulture)
                If days < 1 OrElse days > 825 Then Throw New ArgumentException("--days بين 1 و825.")
                Dim dir As String = IO.Path.Combine(_options.DataDir, "tls")
                IO.Directory.CreateDirectory(dir)
                Dim pfx As String = IO.Path.Combine(dir, "gw.pfx")
                IO.File.WriteAllBytes(pfx, GatewayCert.CreateSelfSigned(host, days))
                If Not OperatingSystem.IsWindows() Then IO.File.SetUnixFileMode(pfx, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite)
                _admin.Write(_actor, "Cert.Create", host, "OK", "days=" & days)
                _out.WriteLine("أُنشئت الشهادة: " & pfx & "  (ضع PfxPath في gateway.json)")
                PrintPins(New X509Certificate2(pfx))
            Case "show"
                Dim path As String = _options.Tls.PfxPath
                If String.IsNullOrWhiteSpace(path) Then path = IO.Path.Combine(_options.DataDir, "tls", "gw.pfx")
                PrintPins(New X509Certificate2(path))
            Case Else : Return Usage()
        End Select
        Return 0
    End Function

    Private Sub PrintPins(cert As X509Certificate2)
        _out.WriteLine("الموضوع: " & cert.Subject & "  ينتهي: " & cert.NotAfter.ToString("yyyy-MM-dd"))
        _out.WriteLine("للعملاء (GatewayPinnedCert):")
        _out.WriteLine("  spki-sha256:" & GatewayCert.SpkiSha256(cert))
        _out.WriteLine("  cert-sha256:" & GatewayCert.CertSha256(cert))
    End Sub

    ''' <summary>يقرأ كلمة سر بلا صدى عند وجود طرفية تفاعلية، وإلا من سطر الإدخال القياسي (للأتمتة).</summary>
    Public Shared Function ReadSecretFromConsole(prompt As String) As String
        Console.Error.Write(prompt)
        If Console.IsInputRedirected Then Return If(Console.In.ReadLine(), "")
        Dim sb As New Text.StringBuilder()
        While True
            Dim k As ConsoleKeyInfo = Console.ReadKey(True)
            If k.Key = ConsoleKey.Enter Then Exit While
            If k.Key = ConsoleKey.Backspace Then
                If sb.Length > 0 Then sb.Length -= 1
            ElseIf Not Char.IsControl(k.KeyChar) Then
                sb.Append(k.KeyChar)
            End If
        End While
        Console.Error.WriteLine()
        Return sb.ToString()
    End Function
End Class
