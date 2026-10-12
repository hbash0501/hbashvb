Imports System.Net

Namespace Remote

    ''' <summary>
    ''' عنوان البوابة وسياسة الثقة بها. HTTPS إلزامي؛ HTTP مسموح فقط على الحلقة المحلية وبموافقة صريحة (للتطوير).
    ''' بلا دبابيس: تحقق كامل بسلسلة الثقة واسم المضيف. معها: يُقبل التطابق التام فقط (انظر <see cref="GatewayCert"/>).
    ''' </summary>
    Public NotInheritable Class GatewayEndpoint
        Public Property BaseUrl As String = ""
        ''' <summary>spki-sha256:HEX أو cert-sha256:HEX (حتى اثنين مفصولين بفاصلة).</summary>
        Public Property PinnedCert As String = ""
        Public Property AllowInsecureLoopbackHttp As Boolean
        Public Property TimeoutSeconds As Integer = 15

        Public Shared Function IsLoopbackHost(host As String) As Boolean
            If String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) Then Return True
            Dim ip As IPAddress = Nothing
            Return IPAddress.TryParse(host.Trim("["c, "]"c), ip) AndAlso IPAddress.IsLoopback(ip)
        End Function

        ''' <summary>يتحقق من الإعداد ويعيد العنوان الأساسي مطبَّعاً (ينتهي بـ /). يرمي ArgumentException بنص عربي واضح.</summary>
        Public Function Validate() As Uri
            Dim uri As Uri = Nothing
            If String.IsNullOrWhiteSpace(BaseUrl) OrElse Not Uri.TryCreate(BaseUrl.Trim(), UriKind.Absolute, uri) Then
                Throw New ArgumentException("عنوان البوابة غير صالح.")
            End If
            If uri.Scheme <> Uri.UriSchemeHttps AndAlso uri.Scheme <> Uri.UriSchemeHttp Then Throw New ArgumentException("عنوان البوابة يجب أن يبدأ بـ https://")
            If Not String.IsNullOrEmpty(uri.UserInfo) Then Throw New ArgumentException("لا يجوز تضمين بيانات دخول في العنوان.")
            If Not String.IsNullOrEmpty(uri.Query) OrElse Not String.IsNullOrEmpty(uri.Fragment) Then Throw New ArgumentException("العنوان لا يحوي استعلاماً أو جزءاً.")
            If uri.AbsolutePath <> "/" Then Throw New ArgumentException("عنوان البوابة هو المضيف والمنفذ فقط (بلا مسار).")
            If uri.Scheme = Uri.UriSchemeHttp Then
                If Not (AllowInsecureLoopbackHttp AndAlso IsLoopbackHost(uri.Host)) Then
                    Throw New ArgumentException("البوابة تتطلب HTTPS (HTTP مسموح على الحلقة المحلية فقط وبتفعيل صريح).")
                End If
            End If
            If Not GatewayCert.IsValidPinSyntax(PinnedCert) Then Throw New ArgumentException("صيغة بصمة الشهادة غير صحيحة (spki-sha256:HEX أو cert-sha256:HEX).")
            If TimeoutSeconds < 2 OrElse TimeoutSeconds > 120 Then Throw New ArgumentException("مهلة الاتصال خارج [2,120] ثانية.")
            Return uri
        End Function
    End Class

    ''' <summary>بيانات دخول بوابة (لا تُحفظ؛ تمر في الذاكرة لحظة الدخول فقط).</summary>
    Public NotInheritable Class GatewayCredentials
        Public Property UserName As String = ""
        Public Property Password As String = ""
        Public Property Totp As String = ""
    End Class

    ''' <summary>
    ''' مصدر التفاعل مع المستخدم للمصادقة (تنفيذه في الواجهة). الإلغاء ⇒ Nothing.
    ''' </summary>
    Public Interface IGatewayCredentialProvider
        ''' <summary>رمز TOTP جديد للتحقق المعزَّز (خطوة حساسة). Nothing = ألغى المستخدم.</summary>
        Function GetStepUpCodeAsync(reason As String, ct As Threading.CancellationToken) As Task(Of String)
    End Interface

End Namespace
