Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Xunit

Public Class SecurityTests
    Private Const StrongPw As String = "Str0ng!Passw0rd"

    Private Shared Function TempDir() As String
        Dim d As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-tests-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(d)
        Return d
    End Function

    ' ---------- المستخدمون ----------
    <Theory>
    <InlineData("short1!A")>
    <InlineData("alllowercase123!")>
    <InlineData("ALLUPPERCASE123!")>
    <InlineData("NoDigitsHere!!!!")>
    <InlineData("NoSymbols123456Aa")>
    <InlineData("xxAdminxxPass1!")>
    Public Sub PasswordPolicy_RejectsWeakPasswords(pw As String)
        Assert.NotNull(UserStore.ValidatePasswordPolicy("admin", pw))
    End Sub

    <Fact>
    Public Sub PasswordPolicy_AcceptsStrong()
        Assert.Null(UserStore.ValidatePasswordPolicy("admin", StrongPw))
    End Sub

    Private Shared Function NewStore(dir As String, clock As TestClock, Optional audit As IAuditLog = Nothing) As UserStore
        Return New UserStore(IO.Path.Combine(dir, "users.json"), audit, clock, 1000)
    End Function

    <Fact>
    Public Sub UserStore_AuthenticatesAndPersists_NoPlaintextOnDisk()
        Dim dir As String = TempDir()
        Dim clock As New TestClock()
        Dim s As UserStore = NewStore(dir, clock)
        s.CreateUser("sys", "admin", StrongPw, Role.Administrator)
        Dim sess As UserSession = s.Authenticate("admin", StrongPw)
        Assert.Equal(Role.Administrator, sess.Role)
        Dim reloaded As UserStore = NewStore(dir, clock)
        Assert.Equal(Role.Administrator, reloaded.Authenticate("admin", StrongPw).Role)
        Assert.DoesNotContain(StrongPw, IO.File.ReadAllText(IO.Path.Combine(dir, "users.json")))
    End Sub

    <Fact>
    Public Sub UserStore_LocksAfterRepeatedFailures_ThenUnlocksByTime()
        Dim clock As New TestClock()
        Dim audit As New ListAudit()
        Dim s As UserStore = NewStore(TempDir(), clock, audit)
        s.CreateUser("sys", "operator1", StrongPw, Role.Operator)
        For i As Integer = 1 To UserStore.MaxFailedAttempts
            Assert.Throws(Of AuthenticationException)(Function() s.Authenticate("operator1", "wrong"))
        Next
        ' حتى كلمة المرور الصحيحة مرفوضة أثناء القفل
        Assert.Throws(Of AuthenticationException)(Function() s.Authenticate("operator1", StrongPw))
        clock.Advance(UserStore.LockDuration.TotalSeconds + 1)
        Assert.NotNull(s.Authenticate("operator1", StrongPw))
        Assert.Contains(audit.Entries, Function(e) e.Contains("Auth.Lockout"))
    End Sub

    <Fact>
    Public Sub UserStore_UnknownUser_GivesSameGenericError()
        Dim s As UserStore = NewStore(TempDir(), New TestClock())
        s.CreateUser("sys", "someone", StrongPw, Role.Viewer)
        Dim e1 As AuthenticationException = Assert.Throws(Of AuthenticationException)(Function() s.Authenticate("nobody", "x"))
        Dim e2 As AuthenticationException = Assert.Throws(Of AuthenticationException)(Function() s.Authenticate("someone", "x"))
        Assert.Equal(e1.Message, e2.Message)
    End Sub

    <Fact>
    Public Sub UserStore_ProtectsLastAdministrator()
        Dim s As UserStore = NewStore(TempDir(), New TestClock())
        s.CreateUser("sys", "boss", StrongPw, Role.Administrator)
        Assert.Throws(Of InvalidOperationException)(Sub() s.SetRole("boss", "boss", Role.Viewer))
        Assert.Throws(Of InvalidOperationException)(Sub() s.SetDisabled("boss", "boss", True))
        s.CreateUser("boss", "boss2", StrongPw, Role.Administrator)
        s.SetDisabled("boss", "boss", True)
        Assert.Throws(Of AuthenticationException)(Function() s.Authenticate("boss", StrongPw))
    End Sub

    <Fact>
    Public Sub UserStore_RejectsBadUserNamesAndDuplicates()
        Dim s As UserStore = NewStore(TempDir(), New TestClock())
        Assert.Throws(Of ArgumentException)(Sub() s.CreateUser("sys", "a b", StrongPw, Role.Viewer))
        Assert.Throws(Of ArgumentException)(Sub() s.CreateUser("sys", "../x", StrongPw, Role.Viewer))
        s.CreateUser("sys", "dupuser", StrongPw, Role.Viewer)
        Assert.Throws(Of InvalidOperationException)(Sub() s.CreateUser("sys", "DUPUSER", StrongPw, Role.Viewer))
    End Sub

    ' ---------- الصلاحيات ----------
    <Theory>
    <InlineData(Role.Viewer, Permission.ViewLive, True)>
    <InlineData(Role.Viewer, Permission.PtzControl, False)>
    <InlineData(Role.Operator, Permission.PtzControl, True)>
    <InlineData(Role.Operator, Permission.ImageControl, False)>
    <InlineData(Role.Operator, Permission.AutoTrack, True)>
    <InlineData(Role.Supervisor, Permission.ImageControl, True)>
    <InlineData(Role.Supervisor, Permission.ManageUsers, False)>
    <InlineData(Role.Supervisor, Permission.DeviceMaintenance, False)>
    <InlineData(Role.Administrator, Permission.ManageUsers, True)>
    Public Sub RolePermissions_Matrix(r As Role, p As Permission, expected As Boolean)
        Assert.Equal(expected, (RolePermissions.For(r) And p) = p)
    End Sub

    <Fact>
    Public Sub AccessControl_ExpiredSession_IsDenied()
        Dim clock As New TestClock()
        Dim ac As New AccessControl(clock)
        Dim sess As New UserSession("u", Role.Operator, clock.UtcNow, TimeSpan.FromMinutes(5))
        ac.Demand(sess, Permission.PtzControl)
        clock.Advance(301)
        Assert.Throws(Of AccessDeniedException)(Sub() ac.Demand(sess, Permission.PtzControl))
        Assert.Throws(Of AccessDeniedException)(Sub() ac.Demand(Nothing, Permission.ViewLive))
    End Sub

    ' ---------- سجل التدقيق ----------
    Private Shared ReadOnly Key As Byte() = RandomNumberGenerator.GetBytes(32)

    <Fact>
    Public Sub AuditLog_ChainVerifies_AndDetectsTampering()
        Dim path As String = IO.Path.Combine(TempDir(), "audit.jsonl")
        Dim log As New FileAuditLog(path, Key)
        For i As Integer = 1 To 20
            log.Write("u", "Ptz.Move", "cam", "OK", "i=" & i)
        Next
        Assert.True(log.Verify().IsValid)
        Assert.Equal(20L, log.Verify().EntriesChecked)

        Dim lines As List(Of String) = IO.File.ReadAllLines(path).ToList()
        lines(9) = lines(9).Replace("i=10", "i=99")
        IO.File.WriteAllLines(path, lines)
        Dim r As AuditVerifyResult = New FileAuditLog(path, Key).Verify()
        Assert.False(r.IsValid)
        Assert.Equal(10L, r.FirstBadSeq)
    End Sub

    <Fact>
    Public Sub AuditLog_DetectsDeletedEntry()
        Dim path As String = IO.Path.Combine(TempDir(), "audit.jsonl")
        Dim log As New FileAuditLog(path, Key)
        For i As Integer = 1 To 5
            log.Write("u", "A", "t", "OK", "")
        Next
        Dim lines As List(Of String) = IO.File.ReadAllLines(path).ToList()
        lines.RemoveAt(2)
        IO.File.WriteAllLines(path, lines)
        Assert.False(New FileAuditLog(path, Key).Verify().IsValid)
    End Sub

    <Fact>
    Public Sub AuditLog_WrongKey_FailsVerification()
        Dim path As String = IO.Path.Combine(TempDir(), "audit.jsonl")
        Dim log As New FileAuditLog(path, Key)
        log.Write("u", "A", "t", "OK", "")
        Assert.False(New FileAuditLog(path, RandomNumberGenerator.GetBytes(32)).Verify().IsValid)
    End Sub

    <Fact>
    Public Sub AuditLog_ConcurrentWriters_KeepChainIntact_AndResumesAfterReopen()
        Dim path As String = IO.Path.Combine(TempDir(), "audit.jsonl")
        Dim log As New FileAuditLog(path, Key)
        Parallel.For(0, 8, Sub(t)
                               For i As Integer = 0 To 99
                                   log.Write("user" & t, "Op", "cam", "OK", i.ToString())
                               Next
                           End Sub)
        Dim v As AuditVerifyResult = log.Verify()
        Assert.True(v.IsValid)
        Assert.Equal(800L, v.EntriesChecked)
        Dim again As New FileAuditLog(path, Key)
        again.Write("x", "After", "t", "OK", "")
        Assert.Equal(801L, again.Verify().EntriesChecked)
        Assert.True(again.Verify().IsValid)
    End Sub

    <Fact>
    Public Sub AuditLog_InjectionAttempt_StaysSingleLineEntry()
        Dim path As String = IO.Path.Combine(TempDir(), "audit.jsonl")
        Dim log As New FileAuditLog(path, Key)
        log.Write("evil" & vbLf & "{""Seq"":99}", "A", "t", "OK", "line1" & vbCrLf & "line2")
        Assert.Single(IO.File.ReadAllLines(path))
        Assert.True(log.Verify().IsValid)
    End Sub

    ' ---------- الخزنة ----------
    <Fact>
    Public Sub Vault_RoundTrip_BindsToContext_AndDetectsTamper()
        Dim keyFile As String = IO.Path.Combine(TempDir(), "master.key")
        Dim vault As New CredentialVault(New FileKeyProvider(keyFile))
        Dim blob As String = vault.Protect("cam-secret-123", "cam1")
        Assert.DoesNotContain("cam-secret-123", blob)
        Assert.Equal("cam-secret-123", vault.Unprotect(blob, "cam1"))
        Assert.NotEqual(blob, vault.Protect("cam-secret-123", "cam1")) ' nonce عشوائي
        Assert.ThrowsAny(Of CryptographicException)(Function() vault.Unprotect(blob, "cam2"))
        Dim raw As Byte() = Convert.FromBase64String(blob.Substring(3))
        raw(raw.Length - 1) = CByte(raw(raw.Length - 1) Xor 1)
        Assert.ThrowsAny(Of CryptographicException)(Function() vault.Unprotect("v1:" & Convert.ToBase64String(raw), "cam1"))
        ' مفتاح ثابت بين نسختين من الموفّر
        Dim vault2 As New CredentialVault(New FileKeyProvider(keyFile))
        Assert.Equal("cam-secret-123", vault2.Unprotect(blob, "cam1"))
    End Sub

    ' ---------- الشهادات ----------
    Private Shared Function SelfSigned() As X509Certificate2
        Using rsa As RSA = RSA.Create(2048)
            Dim req As New CertificateRequest("CN=cam", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            Return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30))
        End Using
    End Function

    <Fact>
    Public Sub CertificatePolicy_PinnedFingerprint_AcceptsOnlyExactMatch()
        Using c1 As X509Certificate2 = SelfSigned(), c2 As X509Certificate2 = SelfSigned()
            Dim fp As String = IsapiClient.Fingerprint(c1)
            Dim withColons As String = String.Join(":", Enumerable.Range(0, fp.Length \ 2).Select(Function(i) fp.Substring(i * 2, 2)))
            Const untrusted As Net.Security.SslPolicyErrors = Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
            Assert.True(IsapiClient.IsCertificateAcceptable(fp, c1, untrusted))
            Assert.True(IsapiClient.IsCertificateAcceptable(withColons.ToLowerInvariant(), c1, untrusted))
            Assert.False(IsapiClient.IsCertificateAcceptable(fp, c2, Net.Security.SslPolicyErrors.None))
            Assert.False(IsapiClient.IsCertificateAcceptable("not-hex", c1, untrusted))
        End Using
    End Sub

    <Fact>
    Public Sub CertificatePolicy_WithoutPin_RequiresValidChain()
        Using c As X509Certificate2 = SelfSigned()
            Assert.True(IsapiClient.IsCertificateAcceptable("", c, Net.Security.SslPolicyErrors.None))
            Assert.False(IsapiClient.IsCertificateAcceptable("", c, Net.Security.SslPolicyErrors.RemoteCertificateChainErrors))
            Assert.False(IsapiClient.IsCertificateAcceptable("", Nothing, Net.Security.SslPolicyErrors.None))
        End Using
    End Sub

    <Fact>
    Public Sub Connection_InsecureHttp_RequiresExplicitOptIn()
        Dim c As New CameraConnection With {.Host = "10.0.0.5", .Port = 80, .UseHttps = False, .Username = "u", .Password = "p"}
        Assert.Throws(Of InvalidOperationException)(Sub() c.Validate())
        c.AllowInsecureHttp = True
        c.Validate()
        c.Host = "bad host/../"
        Assert.Throws(Of ArgumentException)(Sub() c.Validate())
    End Sub

    <Fact>
    Public Sub Connection_ToString_NeverContainsPassword()
        Dim c As New CameraConnection With {.Host = "10.0.0.5", .Username = "u", .Password = "SuperSecret!"}
        Assert.DoesNotContain("SuperSecret!", c.ToString())
    End Sub
End Class
