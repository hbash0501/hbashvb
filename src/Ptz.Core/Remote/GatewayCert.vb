Imports System.Net.Security
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates

Namespace Remote

    ''' <summary>
    ''' سياسة شهادة البوابة على العميل + إنشاء شهادة ذاتية التوقيع للبوابة. التثبيت (Pinning) بصيغتين:
    ''' <c>spki-sha256:HEX</c> (بصمة المفتاح العام — تبقى صالحة عند تجديد الشهادة بالمفتاح نفسه) أو <c>cert-sha256:HEX</c>.
    ''' يُسمح بدبوسين مفصولين بفاصلة (الثاني احتياطي). بلا دبابيس: تحقق عادي كامل بسلسلة الثقة واسم المضيف.
    ''' لا يوجد "قبول الكل" أبداً.
    ''' </summary>
    Public NotInheritable Class GatewayCert
        Private Sub New()
        End Sub

        Public Shared Function SpkiSha256(cert As X509Certificate) As String
            Using c2 As New X509Certificate2(cert.GetRawCertData())
                Return Convert.ToHexString(SHA256.HashData(c2.PublicKey.ExportSubjectPublicKeyInfo()))
            End Using
        End Function

        Public Shared Function CertSha256(cert As X509Certificate) As String
            Return Convert.ToHexString(SHA256.HashData(cert.GetRawCertData()))
        End Function

        Public Shared Function IsValidPinSyntax(pins As String) As Boolean
            If String.IsNullOrWhiteSpace(pins) Then Return True
            Dim parts As String() = pins.Split(","c)
            If parts.Length > 2 Then Return False
            For Each p As String In parts
                Dim t As String = p.Trim()
                Dim hex As String
                If t.StartsWith("spki-sha256:", StringComparison.OrdinalIgnoreCase) Then
                    hex = t.Substring(12)
                ElseIf t.StartsWith("cert-sha256:", StringComparison.OrdinalIgnoreCase) Then
                    hex = t.Substring(12)
                Else
                    Return False
                End If
                hex = hex.Replace(":", "")
                If hex.Length <> 64 Then Return False
                Try
                    Convert.FromHexString(hex)
                Catch ex As FormatException
                    Return False
                End Try
            Next
            Return True
        End Function

        Public Shared Function IsAcceptable(pins As String, cert As X509Certificate, errors As SslPolicyErrors) As Boolean
            If cert Is Nothing Then Return False
            If String.IsNullOrWhiteSpace(pins) Then Return errors = SslPolicyErrors.None
            If Not IsValidPinSyntax(pins) Then Return False
            Dim accepted As Boolean = False
            For Each p As String In pins.Split(","c)
                Dim t As String = p.Trim()
                Dim actual As Byte()
                Dim wanted As Byte()
                If t.StartsWith("spki-sha256:", StringComparison.OrdinalIgnoreCase) Then
                    actual = Convert.FromHexString(SpkiSha256(cert))
                    wanted = Convert.FromHexString(t.Substring(12).Replace(":", ""))
                Else
                    actual = Convert.FromHexString(CertSha256(cert))
                    wanted = Convert.FromHexString(t.Substring(12).Replace(":", ""))
                End If
                If CryptographicOperations.FixedTimeEquals(actual, wanted) Then accepted = True
            Next
            Return accepted
        End Function

        ''' <summary>شهادة خادم ذاتية التوقيع (RSA-2048، SAN)، تُعاد PFX بلا كلمة مرور (يحمي الملف صلاحيات النظام).</summary>
        Public Shared Function CreateSelfSigned(dnsName As String, days As Integer) As Byte()
            Using rsa As RSA = RSA.Create(2048)
                Dim req As New CertificateRequest("CN=" & dnsName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                Dim san As New SubjectAlternativeNameBuilder()
                Dim ip As Net.IPAddress = Nothing
                If Net.IPAddress.TryParse(dnsName, ip) Then san.AddIpAddress(ip) Else san.AddDnsName(dnsName)
                req.CertificateExtensions.Add(san.Build())
                req.CertificateExtensions.Add(New X509BasicConstraintsExtension(False, False, 0, False))
                req.CertificateExtensions.Add(New X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature Or X509KeyUsageFlags.KeyEncipherment, False))
                req.CertificateExtensions.Add(New X509EnhancedKeyUsageExtension(New OidCollection From {New Oid("1.3.6.1.5.5.7.3.1")}, False))
                Using cert As X509Certificate2 = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(days))
                    Return cert.Export(X509ContentType.Pfx)
                End Using
            End Using
        End Function
    End Class

End Namespace
