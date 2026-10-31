Imports System.Net
Imports System.Security.Cryptography.X509Certificates
Imports Ptz.Core.Models
Imports Ptz.Gateway.Cloud

''' <summary>نتيجة فحص الإعداد: المخالفات تمنع الإقلاع، والتحذيرات تُعرض فقط.</summary>
Public NotInheritable Class ValidationReport
    Public ReadOnly Property Violations As New List(Of String)()
    Public ReadOnly Property Warnings As New List(Of String)()
    Public ReadOnly Property IsOk As Boolean
        Get
            Return Violations.Count = 0
        End Get
    End Property
End Class

''' <summary>
''' فحص أمان عند الإقلاع: يرفض الإعدادات الخطرة (تعريض خارج الحلقة المحلية بلا شهادة، استضافة غير مقيّدة، بلا TOTP...)
''' بدل الاعتماد على انتباه المشغّل. دالة نقية قابلة للاختبار (البيئة والملفات تُحقن).
''' </summary>
Public NotInheritable Class StartupSecurityValidator
    Private Sub New()
    End Sub

    Public Shared ReadOnly ValidModes As String() = {"Loopback", "Vpn", "Tunnel", "Public"}

    Private Shared Function IsLoopback(address As String) As Boolean
        Dim ip As IPAddress = Nothing
        If IPAddress.TryParse(address, ip) Then Return IPAddress.IsLoopback(ip)
        Return String.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' قواعد النقل السحابي (Hik-Partner Pro): لا كاميرا hpp بلا تفعيل ومفاتيح، عناوين الخدمة https ضمن اللواحق، مسارات إضافية صالحة،
    ''' وحدود معقولة للمعدل. المفتاح يتحكم بكل أجهزة الحساب فيُحذَّر منه دائماً عند التفعيل.
    ''' </summary>
    Private Shared Sub CheckHpp(o As GatewayOptions, env As Func(Of String, String), relayCams As List(Of CameraConnection), r As ValidationReport)
        Dim h As HppOptions = o.Hpp
        If h Is Nothing Then Return
        If relayCams.Count > 0 AndAlso Not h.Enabled Then
            r.Violations.Add("توجد كاميرات بنقل hpp لكن Hpp.Enabled=false.")
        End If
        Dim serials As New HashSet(Of String)(StringComparer.Ordinal)
        For Each c As CameraConnection In relayCams
            If Not serials.Add(c.CloudSerial) Then r.Violations.Add("رقم تسلسلي سحابي مكرر بين كاميرتين.")
            If Not CameraTransports.LooksLikeGuideSerial(c.CloudSerial) Then
                r.Warnings.Add($"الرقم التسلسلي السحابي '{c.CloudSerial}' ليس بصيغة الدليل (1–9 حروف/أرقام): تأكد أنه الرقم الذي يعرضه 'hpp devices' (غالباً آخر 9 محارف من الرقم الكامل).")
            End If
        Next
        If Not h.Enabled Then Return

        Dim suffixesOk As Boolean = h.AllowedHostSuffixes IsNot Nothing AndAlso h.AllowedHostSuffixes.Count > 0
        If suffixesOk Then
            For Each sfx As String In h.AllowedHostSuffixes
                Dim t As String = If(sfx, "").Trim().TrimStart("."c)
                If t.IndexOf("."c) < 0 OrElse t.Contains("*") OrElse t.Contains("/") OrElse t.Contains(":") Then
                    r.Violations.Add("Hpp.AllowedHostSuffixes يحوي لاحقة غير صالحة (يلزم نطاق كامل مثل hik-partner.com بلا أنماط).")
                    suffixesOk = False
                    Exit For
                End If
            Next
        Else
            r.Violations.Add("Hpp.AllowedHostSuffixes فارغ.")
        End If
        If suffixesOk AndAlso HppCloud.NormalizeServiceUrl(h.TokenUrl, h.AllowedHostSuffixes) Is Nothing Then
            r.Violations.Add("Hpp.TokenUrl يجب أن يكون https بلا منفذ/مسار ومضيفه ضمن Hpp.AllowedHostSuffixes.")
        End If

        ' appKeyEnv/secretKeyEnv أسماء متغيرات لا قيم: إن وُضع المفتاح نفسه هنا خطأً لا نطبعه في أي رسالة.
        Dim namesOk As Boolean = IsEnvName(h.AppKeyEnv) AndAlso IsEnvName(h.SecretKeyEnv)
        If Not namesOk Then r.Violations.Add("Hpp.appKeyEnv/secretKeyEnv يجب أن يكونا اسمي متغيري بيئة (حروف كبيرة وأرقام و_) لا المفاتيح نفسها.")
        Dim ak As String = If(namesOk, env(h.AppKeyEnv), Nothing)
        Dim sk As String = If(namesOk, env(h.SecretKeyEnv), Nothing)
        If namesOk AndAlso (String.IsNullOrWhiteSpace(ak) OrElse String.IsNullOrWhiteSpace(sk)) Then
            If relayCams.Count > 0 Then
                r.Violations.Add($"مفاتيح Hik-Partner Pro غير مضبوطة: عيّن متغيري البيئة {h.AppKeyEnv}/{h.SecretKeyEnv}.")
            Else
                r.Warnings.Add("Hpp.Enabled=true بلا مفاتيح في البيئة (لا كاميرات hpp حالياً).")
            End If
        End If

        If h.TimeoutSeconds < 3 OrElse h.TimeoutSeconds > 60 Then r.Violations.Add("Hpp.TimeoutSeconds خارج [3,60].")
        If h.MaxRequestsPerSecond < 0.5 OrElse h.MaxRequestsPerSecond > 20 Then r.Violations.Add("Hpp.MaxRequestsPerSecond خارج [0.5,20].")
        If h.MaxBurst < 1 OrElse h.MaxBurst > 50 Then r.Violations.Add("Hpp.MaxBurst خارج [1,50].")
        For Each rule As String In If(h.ExtraAllowed, New List(Of String)())
            If Not HppPathPolicy.IsValidRule(rule) Then r.Violations.Add($"Hpp.ExtraAllowed يحوي قاعدة غير صالحة '{HppJson.SafeCode(rule)}' (الصيغة: METHOD /ISAPI/...).")
        Next
        If h.ExtraAllowed IsNot Nothing AndAlso h.ExtraAllowed.Count > 0 Then
            r.Warnings.Add("Hpp.ExtraAllowed يفتح مسارات ISAPI إضافية خارج الحد الأدنى الموثَّق: تأكد أنها مدعومة سحابياً ولا تسمح بما يتجاوز حاجتك.")
        End If
        r.Warnings.Add("مفاتيح Hik-Partner Pro تتحكم بكل أجهزة الحساب لا بالكاميرتين وحدهما: استعمل حساب شركة مخصصاً إن أمكن، واحمِ بيئة تشغيل البوابة.")
    End Sub

    Private Shared Function IsEnvName(name As String) As Boolean
        If String.IsNullOrEmpty(name) OrElse name.Length > 64 OrElse Char.IsDigit(name(0)) Then Return False
        For Each ch As Char In name
            If Not ((ch >= "A"c AndAlso ch <= "Z"c) OrElse (ch >= "0"c AndAlso ch <= "9"c) OrElse ch = "_"c) Then Return False
        Next
        Return True
    End Function

    Public Shared Function Check(o As GatewayOptions, env As Func(Of String, String), userCount As Integer, anyTotpEnrolled As Boolean,
                                 cameras As IEnumerable(Of CameraConnection), nowUtc As DateTime) As ValidationReport
        Dim r As New ValidationReport()
        Dim mode As String = ValidModes.FirstOrDefault(Function(m) String.Equals(m, o.ExposureMode, StringComparison.OrdinalIgnoreCase))
        If mode Is Nothing Then r.Violations.Add($"ExposureMode غير معروف '{o.ExposureMode}' (Loopback|Vpn|Tunnel|Public).")

        If o.Listen Is Nothing OrElse o.Listen.Count = 0 Then r.Violations.Add("لا عنوان استماع.")
        Dim nonLoopback As Boolean = o.Listen IsNot Nothing AndAlso o.Listen.Any(Function(l) Not IsLoopback(l.Address))
        For Each l As ListenEndpoint In If(o.Listen, New List(Of ListenEndpoint)())
            If l.Port < 1 OrElse l.Port > 65535 Then r.Violations.Add($"منفذ استماع غير صالح {l.Port}.")
        Next

        ' --- TLS ---
        Dim certOk As Boolean = False
        Dim certNote As String = ""
        If Not String.IsNullOrWhiteSpace(o.Tls.PfxPath) OrElse Not String.IsNullOrWhiteSpace(o.Tls.PemPath) Then
            Try
                Dim cert As X509Certificate2 = KestrelTls.LoadCertificate(o.Tls, env)
                certOk = True
                If cert.NotAfter.ToUniversalTime() < nowUtc Then
                    certOk = False
                    certNote = "الشهادة منتهية."
                ElseIf cert.NotAfter.ToUniversalTime() < nowUtc.AddDays(7) Then
                    r.Warnings.Add("الشهادة تنتهي خلال أقل من 7 أيام.")
                End If
            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is System.Security.Cryptography.CryptographicException OrElse TypeOf ex Is ArgumentException
                certNote = "تعذّر تحميل الشهادة: " & ex.Message
            End Try
        End If
        If nonLoopback AndAlso Not certOk Then r.Violations.Add("استماع خارج الحلقة المحلية يتطلب شهادة TLS صالحة. " & certNote)

        ' --- المضيفون ---
        If nonLoopback Then
            If o.AllowedHosts Is Nothing OrElse o.AllowedHosts.Count = 0 OrElse o.AllowedHosts.Any(Function(h) h.Trim() = "*") Then
                r.Violations.Add("AllowedHosts فارغ أو يحوي '*' مع استماع خارجي.")
            End If
        End If

        ' --- الوكلاء الموثوقون ---
        For Each p As String In If(o.TrustedProxies, New List(Of String)())
            Dim ip As IPAddress = Nothing
            If Not IPAddress.TryParse(p, ip) Then
                r.Violations.Add($"TrustedProxies يحوي قيمة غير صالحة '{p}' (عناوين مفردة فقط).")
            ElseIf ip.Equals(IPAddress.Any) OrElse ip.Equals(IPAddress.IPv6Any) Then
                r.Violations.Add("TrustedProxies لا يجوز أن يحوي 0.0.0.0 أو ::.")
            End If
        Next
        If String.Equals(mode, "Tunnel", StringComparison.OrdinalIgnoreCase) AndAlso (o.TrustedProxies Is Nothing OrElse o.TrustedProxies.Count = 0) Then
            r.Violations.Add("وضع Tunnel يتطلب TrustedProxies (بدونه يتشارك كل العملاء مفتاح تحديد معدل واحداً فيستطيع مجهول واحد حجب دخول الجميع).")
        End If

        ' --- المصادقة ---
        If o.Auth.IdleMinutes < 5 OrElse o.Auth.IdleMinutes > 120 Then r.Violations.Add("Auth.IdleMinutes خارج [5,120].")
        If o.Auth.AbsoluteHours < 1 OrElse o.Auth.AbsoluteHours > 24 Then r.Violations.Add("Auth.AbsoluteHours خارج [1,24].")
        If o.Auth.MaxSessionsPerUser < 1 OrElse o.Auth.MaxSessionsPerUser > 20 Then r.Violations.Add("Auth.MaxSessionsPerUser خارج [1,20].")
        If Not o.Auth.RequireTotp Then
            Dim allowed As Boolean = o.Auth.AllowNoMfa AndAlso (String.Equals(mode, "Loopback", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(mode, "Vpn", StringComparison.OrdinalIgnoreCase))
            If Not allowed Then r.Violations.Add("RequireTotp=false غير مسموح إلا مع AllowNoMfa=true وExposureMode=Loopback أو Vpn.")
        ElseIf Not anyTotpEnrolled Then
            r.Warnings.Add("RequireTotp=true ولا يوجد مستخدم مسجَّل بـ TOTP: لن يتمكن أحد من الدخول عن بُعد (استخدم: totp enroll <user>).")
        End If
        Dim maxRole As Ptz.Core.Security.Role
        If Not Ptz.Core.Remote.WireMap.ParseRole(o.Auth.RemoteMaxRole, maxRole) Then
            r.Violations.Add($"Auth.RemoteMaxRole غير معروف '{o.Auth.RemoteMaxRole}'.")
        ElseIf maxRole = Ptz.Core.Security.Role.Administrator AndAlso env("PTZ_ALLOW_REMOTE_ADMIN") <> "1" Then
            r.Violations.Add("RemoteMaxRole=Administrator يتطلب متغير البيئة PTZ_ALLOW_REMOTE_ADMIN=1.")
        End If

        ' --- المستخدمون والكاميرات ---
        If userCount = 0 Then r.Violations.Add("لا يوجد مستخدمون (استخدم: user add <name> <role>).")
        Dim relayCams As New List(Of CameraConnection)()
        For Each c As CameraConnection In If(cameras, New List(Of CameraConnection)())
            If c.IsRelay Then
                relayCams.Add(c)
                Continue For
            End If
            If Not c.UseHttps AndAlso Not c.AllowInsecureHttp Then r.Violations.Add($"الكاميرا {c.Host} تستخدم HTTP بلا AllowInsecureHttp.")
        Next
        CheckHpp(o, env, relayCams, r)

        ' --- مفتاح الأسرار (يونكس) ---
        Dim keyFile As String = env("PTZ_MASTER_KEY_FILE")
        If Not String.IsNullOrEmpty(keyFile) AndAlso IO.File.Exists(keyFile) AndAlso Not OperatingSystem.IsWindows() Then
            Dim mode2 As IO.UnixFileMode = IO.File.GetUnixFileMode(keyFile)
            Const loose As IO.UnixFileMode = IO.UnixFileMode.GroupRead Or IO.UnixFileMode.GroupWrite Or IO.UnixFileMode.GroupExecute Or
                                             IO.UnixFileMode.OtherRead Or IO.UnixFileMode.OtherWrite Or IO.UnixFileMode.OtherExecute
            If (mode2 And loose) <> 0 Then r.Violations.Add("ملف المفتاح الرئيسي يملك صلاحيات للمجموعة/الآخرين (يجب 0600).")
        End If
        If Not String.IsNullOrEmpty(keyFile) AndAlso Not String.IsNullOrEmpty(o.DataDir) AndAlso
           IO.Path.GetFullPath(keyFile).StartsWith(IO.Path.GetFullPath(o.DataDir), StringComparison.Ordinal) Then
            r.Warnings.Add("ملف المفتاح الرئيسي داخل مجلد البيانات (يُفضّل مكان منفصل).")
        End If
        Return r
    End Function
End Class
