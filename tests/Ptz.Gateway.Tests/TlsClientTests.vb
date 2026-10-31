Imports System.Security.Cryptography.X509Certificates
Imports Ptz.Core.Remote
Imports Xunit

''' <summary>
''' مصافحة TLS حقيقية (Kestrel + SocketsHttpHandler الإنتاجي، لا TestServer): تثبيت الشهادة يجب أن يمنع إرسال أي بيان دخول
''' إلى خادم بشهادة غير مطابقة، ويقبل البصمتين (SPKI والشهادة الكاملة) والبصمة الاحتياطية.
''' </summary>
Public Class TlsClientTests
    Implements IDisposable

    Private ReadOnly _dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-tls-" & Guid.NewGuid().ToString("N"))
    Private ReadOnly _pfx As String
    Private ReadOnly _spki As String
    Private ReadOnly _certSha As String

    Public Sub New()
        IO.Directory.CreateDirectory(_dir)
        _pfx = IO.Path.Combine(_dir, "gw.pfx")
        IO.File.WriteAllBytes(_pfx, GatewayCert.CreateSelfSigned("localhost", 30))
        Using cert As New X509Certificate2(_pfx)
            _spki = GatewayCert.SpkiSha256(cert)
            _certSha = GatewayCert.CertSha256(cert)
        End Using
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            IO.Directory.Delete(_dir, True)
        Catch ex As IO.IOException
        End Try
    End Sub

    Private Function Server(port As Integer) As Harness
        Return New Harness(Sub(o) o.Tls.PfxPath = _pfx, kestrelPort:=port)
    End Function

    Private Shared Function Client(port As Integer, pins As String) As GatewayClient
        Return New GatewayClient(New GatewayEndpoint With {.BaseUrl = $"https://localhost:{port}/", .PinnedCert = pins, .TimeoutSeconds = 10}, Nothing)
    End Function

    Private Shared Function Creds(h As Harness) As GatewayCredentials
        Return New GatewayCredentials With {.UserName = "alice", .Password = Harness.Pw, .Totp = h.NextCode("alice")}
    End Function

    <Fact>
    Public Sub CorrectSpkiPin_LogsInOverRealTls_AndReadsTheApi()
        Dim port As Integer = Harness.FreePort()
        Using h As Server_ = New Server_(Server(port))
            Using c As GatewayClient = Client(port, "spki-sha256:" & _spki)
                c.LoginAsync(Creds(h.H), Threading.CancellationToken.None).GetAwaiter().GetResult()
                Dim list As CameraListDto = c.GetJsonAsync(Of CameraListDto)("/api/v1/cameras", Threading.CancellationToken.None).GetAwaiter().GetResult()
                Assert.Equal(2, list.Items.Count)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub CertPin_AndTheBackupPin_AreAlsoAccepted()
        Dim port As Integer = Harness.FreePort()
        Using h As Server_ = New Server_(Server(port))
            Dim wrong As String = "spki-sha256:" & New String("A"c, 64)
            For Each pins As String In {"cert-sha256:" & _certSha, wrong & ",cert-sha256:" & _certSha, "spki-sha256:" & _spki.ToLowerInvariant()}
                Using c As GatewayClient = Client(port, pins)
                    c.LoginAsync(Creds(h.H), Threading.CancellationToken.None).GetAwaiter().GetResult()
                    Assert.True(c.IsSignedIn, pins)
                End Using
            Next
        End Using
    End Sub

    <Fact>
    Public Sub WrongPin_RefusesTheHandshake_AndNoCredentialEverReachesTheServer()
        Dim port As Integer = Harness.FreePort()
        Using h As Server_ = New Server_(Server(port))
            Using c As GatewayClient = Client(port, "spki-sha256:" & New String("0"c, 64))
                Dim ex = Assert.Throws(Of GatewayUnreachableException)(Function() c.LoginAsync(Creds(h.H), Threading.CancellationToken.None).GetAwaiter().GetResult())
                Assert.True(ex.IsCertificateProblem)
                Assert.False(c.IsSignedIn)
            End Using
            Assert.Equal(0, h.H.RequestsSeen)                                  ' لم يصل أي طلب (ولا كلمة مرور) إلى الخادم
        End Using
    End Sub

    <Fact>
    Public Sub NoPin_WithASelfSignedCertificate_IsRejected_NeverSilentlyTrusted()
        Dim port As Integer = Harness.FreePort()
        Using h As Server_ = New Server_(Server(port))
            Using c As GatewayClient = Client(port, "")
                Dim ex = Assert.Throws(Of GatewayUnreachableException)(Function() c.LoginAsync(Creds(h.H), Threading.CancellationToken.None).GetAwaiter().GetResult())
                Assert.True(ex.IsCertificateProblem)
            End Using
            Assert.Equal(0, h.H.RequestsSeen)
        End Using
    End Sub

    <Fact>
    Public Sub PlainHttpToATlsPort_DoesNotLeakAnything_AndHttpsIsRequiredByTheClient()
        Dim port As Integer = Harness.FreePort()
        Using h As Server_ = New Server_(Server(port))
            Assert.Throws(Of ArgumentException)(Function() New GatewayClient(New GatewayEndpoint With {.BaseUrl = $"http://localhost:{port}/"}, Nothing))
            Assert.Throws(Of ArgumentException)(Function() Client(port, "spki-sha256:xyz"))
        End Using
    End Sub

    ''' <summary>غلاف صغير يضمن إيقاف الخادم الحقيقي في نهاية الاختبار.</summary>
    Private NotInheritable Class Server_
        Implements IDisposable
        Public ReadOnly H As Harness
        Public Sub New(h As Harness)
            Me.H = h
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            H.Dispose()
        End Sub
    End Class
End Class
