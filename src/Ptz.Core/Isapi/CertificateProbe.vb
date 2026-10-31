Imports System.Net.Security
Imports System.Net.Sockets
Imports System.Security.Authentication
Imports System.Security.Cryptography.X509Certificates

Namespace Isapi

    ''' <summary>
    ''' يقرأ بصمة شهادة الكاميرا (دون إرسال أي بيانات اعتماد) ليؤكدها المسؤول يدوياً ثم تُثبَّت
    ''' (Trust-On-First-Use). الكاميرات تستخدم عادةً شهادات ذاتية التوقيع فلا بديل عن التثبيت.
    ''' </summary>
    Public NotInheritable Class CertificateProbe
        Private Sub New()
        End Sub

        Public Shared Async Function FetchSha256Async(host As String, port As Integer, timeout As TimeSpan, ct As Threading.CancellationToken) As Task(Of String)
            If String.IsNullOrWhiteSpace(host) OrElse port < 1 OrElse port > 65535 Then Throw New ArgumentException("عنوان أو منفذ غير صالح.")
            Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                cts.CancelAfter(timeout)
                Using client As New TcpClient()
                    Await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(False)
                    Dim captured As String = Nothing
                    Using ssl As New SslStream(client.GetStream(), False,
                        Function(sender, cert, chain, errors)
                            If cert IsNot Nothing Then captured = IsapiClient.Fingerprint(cert)
                            Return True ' للقراءة فقط؛ لا تُرسل بيانات بعد هذا الاتصال
                        End Function)
                        Await ssl.AuthenticateAsClientAsync(New SslClientAuthenticationOptions With {
                            .TargetHost = host,
                            .EnabledSslProtocols = SslProtocols.Tls12 Or SslProtocols.Tls13}, cts.Token).ConfigureAwait(False)
                    End Using
                    If captured Is Nothing Then Throw New InvalidOperationException("لم تُرجع الكاميرا شهادة.")
                    Return captured
                End Using
            End Using
        End Function
    End Class

End Namespace
