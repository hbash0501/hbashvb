Imports System.Net
Imports System.Security.Cryptography.X509Certificates
Imports Ptz.Core.Models

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
            r.Warnings.Add("وضع Tunnel بلا TrustedProxies: كل العملاء سيشاركون حصة تحديد معدل واحدة.")
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
        For Each c As CameraConnection In If(cameras, New List(Of CameraConnection)())
            If Not c.UseHttps AndAlso Not c.AllowInsecureHttp Then r.Violations.Add($"الكاميرا {c.Host} تستخدم HTTP بلا AllowInsecureHttp.")
        Next

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
