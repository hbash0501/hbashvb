Imports System.Security.Cryptography.X509Certificates
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
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

    Public Sub New(options As GatewayOptions, [out] As IO.TextWriter, [in] As IO.TextReader, readSecret As Func(Of String, String), Optional env As Func(Of String, String) = Nothing)
        _options = options
        _out = [out]
        _in = [in]
        _readSecret = readSecret
        Dim getEnv As Func(Of String, String) = env
        If getEnv Is Nothing Then getEnv = Function(k As String) Environment.GetEnvironmentVariable(k)
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
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is KeyNotFoundException
            _out.WriteLine("خطأ: " & ex.Message)
            Return 1
        End Try
    End Function

    Private Function Dispatch(args As String()) As Integer
        If args.Length < 1 Then Return Usage()
        Select Case args(0)
            Case "user" : Return UserCmd(args)
            Case "totp" : Return TotpCmd(args)
            Case "camera" : Return CameraCmd(args)
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
        _out.WriteLine("الاستخدام: PtzGateway [serve|check|user|totp|camera|cert|revoke-all|verify-audit|rotate-audit] ...")
        _out.WriteLine("  user add <name> <role> | passwd <name> | role <name> <role> | disable|enable|unlock <name> | list")
        _out.WriteLine("  totp enroll <name> | reset <name>")
        _out.WriteLine("  camera list | set <key> --host H [--port P] --user U [--http-insecure] [--pin <sha256>] [--name N]")
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
                    _out.WriteLine($"{c.Key}  {c.ModelName}  {If(c.Host = "", "(غير مضبوطة)", c.Host & ":" & c.Port)}  user={c.Username}  https={c.UseHttps}  pinned={c.PinnedCertSha256 <> ""}")
                Next
            Case "set"
                If args.Length < 3 Then Return Usage()
                Dim cfg As CameraConfig = store.Get(args(2))
                If cfg Is Nothing Then Throw New KeyNotFoundException("كاميرا غير معروفة (se7c432mw | de7232iw).")
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
                Dim pw As String = _readSecret("كلمة مرور حساب الخدمة على الكاميرا (فارغة = بلا تغيير): ")
                store.Update(cfg, If(pw = "", Nothing, pw))
                _admin.Write(_actor, "Camera.Set", cfg.Key, "OK", $"{cfg.Host}:{cfg.Port} https={cfg.UseHttps} pinned={cfg.PinnedCertSha256 <> ""}")
                _out.WriteLine("تم الحفظ. أعد تشغيل الخدمة لتطبيق التغيير.")
            Case Else : Return Usage()
        End Select
        Return 0
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
