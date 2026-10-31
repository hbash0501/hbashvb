Imports System.Security.Cryptography.X509Certificates

Public NotInheritable Class KestrelTls
    Private Sub New()
    End Sub

    ''' <summary>يحمّل شهادة الخادم من PFX (كلمة المرور من متغير بيئة) أو PEM+مفتاح. PEM يُعاد تصديره عبر PFX لتوافق SChannel.</summary>
    Public Shared Function LoadCertificate(tls As TlsOptions, env As Func(Of String, String)) As X509Certificate2
        If Not String.IsNullOrWhiteSpace(tls.PfxPath) Then
            Dim pw As String = If(String.IsNullOrEmpty(tls.PfxPasswordEnv), Nothing, env(tls.PfxPasswordEnv))
            Return New X509Certificate2(tls.PfxPath, pw)
        End If
        If Not String.IsNullOrWhiteSpace(tls.PemPath) Then
            Using pem As X509Certificate2 = X509Certificate2.CreateFromPemFile(tls.PemPath, If(String.IsNullOrWhiteSpace(tls.PemKeyPath), Nothing, tls.PemKeyPath))
                Return New X509Certificate2(pem.Export(X509ContentType.Pfx))
            End Using
        End If
        Throw New ArgumentException("لم تُحدَّد شهادة TLS.")
    End Function
End Class
