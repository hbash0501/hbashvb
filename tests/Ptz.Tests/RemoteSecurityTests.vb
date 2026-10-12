Imports System.Security.Cryptography
Imports System.Text
Imports Ptz.Core.Security
Imports Xunit

Public Class RemoteSecurityTests

    Private Shared Function TempDir() As String
        Dim d As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-rs-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(d)
        Return d
    End Function

    ' ---------------- TOTP ----------------
    <Fact>
    Public Sub Totp_MatchesRfc6238Vectors()
        Dim secret As Byte() = Encoding.ASCII.GetBytes("12345678901234567890")
        Assert.Equal("94287082", Totp.Compute(secret, Totp.StepOf(59), 8))
        Assert.Equal("07081804", Totp.Compute(secret, Totp.StepOf(1111111109), 8))
        Assert.Equal("65353130", Totp.Compute(secret, Totp.StepOf(20000000000L), 8))
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Totp.Base32(secret))
        Assert.Equal(secret, Totp.FromBase32("gezdgnbvgy3tqojqgezdgnbvgy3tqojq"))
        Assert.Throws(Of FormatException)(Function() Totp.FromBase32("not*base32"))
    End Sub

    <Fact>
    Public Sub Totp_Verify_WindowReplayAndShape()
        Dim secret As Byte() = Totp.NewSecret()
        Assert.Equal(20, secret.Length)
        Dim code As String = Totp.Compute(secret, Totp.StepOf(1000))
        Assert.Equal(Totp.StepOf(1000), Totp.Verify(secret, code, 1010, -1))
        Assert.Equal(-1L, Totp.Verify(secret, code, 1010, Totp.StepOf(1000)))   ' إعادة استخدام
        Assert.Equal(Totp.StepOf(1000), Totp.Verify(secret, code, 1035, -1))    ' انحراف +30 ث
        Assert.Equal(-1L, Totp.Verify(secret, code, 1100, -1))                  ' انحراف > نافذة
        Assert.Equal(-1L, Totp.Verify(secret, "12345", 1010, -1))
        Assert.Equal(-1L, Totp.Verify(secret, "1234567", 1010, -1))
        Assert.Equal(-1L, Totp.Verify(secret, "12a456", 1010, -1))
        Assert.Equal(-1L, Totp.Verify(secret, "١٢٣٤٥٦", 1010, -1))               ' أرقام عربية ليست ASCII
        Assert.Equal(-1L, Totp.Verify(secret, Nothing, 1010, -1))
    End Sub

    <Fact>
    Public Sub Totp_ProvisioningUri_IsWellFormed()
        Dim uri As String = Totp.ProvisioningUri("PTZ Gateway", "alice", New Byte() {1, 2, 3, 4, 5})
        Assert.StartsWith("otpauth://totp/PTZ%20Gateway:alice?secret=", uri)
        Assert.Contains("issuer=PTZ%20Gateway", uri)
        Assert.Contains("digits=6&period=30", uri)
    End Sub

    Private Shared Function NewTotpStore(dir As String, clock As TestClock) As TotpStore
        Dim vault As New CredentialVault(New FileKeyProvider(IO.Path.Combine(dir, "k")))
        Return New TotpStore(IO.Path.Combine(dir, "totp.json"), IO.Path.Combine(dir, "totp-state.json"), vault, clock)
    End Function

    <Fact>
    Public Sub TotpStore_ConsumesOnce_RejectsReplay_AndPersistsState()
        Dim dir As String = TempDir()
        Dim clock As New TestClock()
        Dim store As TotpStore = NewTotpStore(dir, clock)
        Assert.False(store.IsEnrolled("alice"))
        Assert.False(store.TryConsume("alice", "123456"))
        Dim secret As Byte() = store.Enroll("alice")
        Dim unix As Long = New DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds()
        Dim code As String = Totp.Compute(secret, Totp.StepOf(unix))
        Assert.True(store.TryConsume("alice", code))
        Assert.False(store.TryConsume("alice", code), "الرمز المستهلك لا يعاد")
        ' الحالة تبقى بعد إعادة التحميل
        Dim again As TotpStore = NewTotpStore(dir, clock)
        Assert.False(again.TryConsume("alice", code))
        Assert.DoesNotContain(Totp.Base32(secret), IO.File.ReadAllText(IO.Path.Combine(dir, "totp.json")))
    End Sub

    <Fact>
    Public Async Function TotpStore_ParallelUseOfOneCode_SucceedsExactlyOnce() As Task
        Dim clock As New TestClock()
        Dim store As TotpStore = NewTotpStore(TempDir(), clock)
        Dim secret As Byte() = store.Enroll("bob")
        Dim code As String = Totp.Compute(secret, Totp.StepOf(New DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds()))
        Dim results As Boolean() = Await Task.WhenAll(Enumerable.Range(0, 32).Select(Function(i) Task.Run(Function() store.TryConsume("bob", code))))
        Assert.Equal(1, results.Where(Function(r) r).Count())
    End Function

    <Fact>
    Public Sub TotpStore_Stamp_ChangesOnEnrollReset_AndReloadSeesCliChanges()
        Dim dir As String = TempDir()
        Dim clock As New TestClock()
        Dim service As TotpStore = NewTotpStore(dir, clock)
        Dim cli As TotpStore = NewTotpStore(dir, clock)
        Assert.Equal("", service.StampOf("carol"))
        cli.Enroll("carol")
        Assert.False(service.IsEnrolled("carol"))
        Assert.True(service.ReloadIfChanged())
        Assert.True(service.IsEnrolled("carol"))
        Dim s1 As String = service.StampOf("carol")
        cli.Enroll("carol")
        service.ReloadIfChanged()
        Assert.NotEqual(s1, service.StampOf("carol"))
        cli.Reset("carol")
        service.ReloadIfChanged()
        Assert.Equal("", service.StampOf("carol"))
        Assert.False(service.ReloadIfChanged())
    End Sub

    ' ---------------- LoginThrottle ----------------
    <Fact>
    Public Sub LoginThrottle_BackoffAfterThreeFailures_PerUserAndIp()
        Dim clock As New TestClock()
        Dim t As New LoginThrottle(clock)
        For i As Integer = 1 To 2
            t.RecordFailure("1.1.1.1", "alice")
            Assert.Null(t.Precheck("1.1.1.1", "alice"))
        Next
        t.RecordFailure("1.1.1.1", "alice")                       ' الثالثة: انتظار 1 ث
        Assert.NotNull(t.Precheck("1.1.1.1", "alice"))
        Assert.Null(t.Precheck("2.2.2.2", "alice"))               ' عنوان آخر لا يتأثر
        clock.Advance(1.1)
        Assert.Null(t.Precheck("1.1.1.1", "alice"))
        t.RecordFailure("1.1.1.1", "alice")                       ' الرابعة: 2 ث
        Assert.True(t.Precheck("1.1.1.1", "alice").Value.TotalSeconds > 1.0)
        clock.Advance(60 * 11)                                    ' تنتهي النافذة
        Assert.Null(t.Precheck("1.1.1.1", "alice"))
        t.RecordFailure("1.1.1.1", "alice")
        t.RecordSuccess("1.1.1.1", "alice")
        Assert.Null(t.Precheck("1.1.1.1", "alice"))
    End Sub

    <Fact>
    Public Sub LoginThrottle_UserSoftLock_AcrossIps_ExpiresAndCanBeUnlocked()
        Dim clock As New TestClock()
        Dim t As New LoginThrottle(clock)
        For i As Integer = 1 To LoginThrottle.UserFailureLimit
            t.RecordFailure("10.0.0." & i, "Admin")
        Next
        Assert.True(t.IsRemoteLocked("admin"))
        Assert.False(t.IsRemoteLocked("other"))
        clock.Advance(31 * 60)
        Assert.False(t.IsRemoteLocked("admin"))
        For i As Integer = 1 To LoginThrottle.UserFailureLimit
            t.RecordFailure("10.0.1." & i, "admin")
        Next
        Assert.True(t.IsRemoteLocked("admin"))
        t.Unlock("admin")
        Assert.False(t.IsRemoteLocked("admin"))
    End Sub

    <Fact>
    Public Sub LoginThrottle_IsBounded_UnderKeyFlood()
        Dim t As New LoginThrottle(New TestClock(), 100)
        For i As Integer = 0 To 5000
            t.RecordFailure("9.9." & (i \ 250) & "." & (i Mod 250), "user" & i)
        Next
        ' لا استثناء ولا نمو غير محدود؛ السلوك سليم بعد الإخلاء
        Assert.Null(t.Precheck("1.2.3.4", "fresh"))
    End Sub

    ' ---------------- TokenBucket ----------------
    <Fact>
    Public Sub TokenBucket_BurstThenRefill()
        Dim clock As New TestClock()
        Dim b As New TokenBucket(5, 10, clock)
        For i As Integer = 1 To 10
            Assert.True(b.TryTake())
        Next
        Assert.False(b.TryTake())
        Assert.Equal(1, b.RetryAfterSeconds())
        clock.Advance(1.0)
        Assert.True(b.TryTake(5))
        Assert.False(b.TryTake())
        clock.Advance(100)
        For i As Integer = 1 To 10
            Assert.True(b.TryTake())
        Next
        Assert.False(b.TryTake(), "السعة محدودة بالاندفاع")
    End Sub

    ' ---------------- SessionRegistry ----------------
    Private Shared Function NewRegistry(clock As TestClock, Optional maxPerUser As Integer = 3) As SessionRegistry
        Return New SessionRegistry(clock, TimeSpan.FromMinutes(30), TimeSpan.FromHours(12), maxPerUser)
    End Function

    <Fact>
    Public Sub Sessions_TokenFormat_AndLookup()
        Dim clock As New TestClock()
        Dim reg As SessionRegistry = NewRegistry(clock)
        Dim token As String = Nothing
        Dim rec As SessionRecord = reg.Create("alice", Role.Operator, "stamp", "1.2.3.4", "client" & vbLf & "evil", token)
        Assert.True(TokenHasher.IsWellFormed(token))
        Assert.Equal(48, token.Length)
        Assert.DoesNotContain(vbLf, rec.ClientName)
        Assert.Equal(8, rec.SessionRef.Length)
        Dim res As SessionAuthResult
        Assert.Same(rec, reg.TryAuthenticate(token, res))
        Assert.Equal(SessionAuthResult.Ok, res)
        Assert.Null(reg.TryAuthenticate("ptzs_" & New String("A"c, 43), res))
        Assert.Equal(SessionAuthResult.Unknown, res)
        Assert.Null(reg.TryAuthenticate("garbage", res))
        Assert.Null(reg.TryAuthenticate(Nothing, res))
        Dim token2 As String = Nothing
        reg.Create("alice", Role.Operator, "stamp", "1.2.3.4", "", token2)
        Assert.NotEqual(token, token2)
    End Sub

    <Fact>
    Public Sub Sessions_IdleExpiry_OnlyExtendedByTouch_AndAbsoluteExpiry()
        Dim clock As New TestClock()
        Dim reg As SessionRegistry = NewRegistry(clock)
        Dim token As String = Nothing
        Dim rec As SessionRecord = reg.Create("alice", Role.Viewer, "s", "ip", "", token)
        Dim res As SessionAuthResult
        clock.Advance(20 * 60)
        Assert.NotNull(reg.TryAuthenticate(token, res))            ' القراءة لا تجدد
        clock.Advance(11 * 60)                                       ' 31 د بلا نشاط
        Assert.Null(reg.TryAuthenticate(token, res))
        Assert.Equal(SessionAuthResult.Expired, res)
        Assert.True(rec.IsRevoked)

        Dim token2 As String = Nothing
        Dim rec2 As SessionRecord = reg.Create("bob", Role.Viewer, "s", "ip", "", token2)
        For i As Integer = 1 To 40
            clock.Advance(20 * 60)
            rec2.Touch(clock.UtcNow)           ' نشاط مستمر لأكثر من 12 ساعة
        Next
        Assert.Null(reg.TryAuthenticate(token2, res))               ' الحد المطلق 12 س رغم النشاط
        Assert.Equal(SessionAuthResult.Expired, res)
    End Sub

    <Fact>
    Public Sub Sessions_MaxPerUser_EvictsOldest_AndRevokeCancelsToken()
        Dim clock As New TestClock()
        Dim reg As SessionRegistry = NewRegistry(clock, 2)
        Dim t1 As String = Nothing, t2 As String = Nothing, t3 As String = Nothing
        Dim r1 As SessionRecord = reg.Create("alice", Role.Operator, "s", "ip", "", t1)
        clock.Advance(1)
        Dim r2 As SessionRecord = reg.Create("alice", Role.Operator, "s", "ip", "", t2)
        clock.Advance(1)
        Dim r3 As SessionRecord = reg.Create("alice", Role.Operator, "s", "ip", "", t3)
        Dim res As SessionAuthResult
        Assert.True(r1.RevokedToken.IsCancellationRequested)
        Assert.Null(reg.TryAuthenticate(t1, res))
        Assert.NotNull(reg.TryAuthenticate(t2, res))
        Assert.NotNull(reg.TryAuthenticate(t3, res))
        Assert.Equal(2, reg.ForUser("ALICE").Count)
        reg.RevokeUser("alice")
        Assert.True(r2.IsRevoked AndAlso r3.IsRevoked)
        Assert.Equal(0, reg.Count)
    End Sub

    <Fact>
    Public Sub Sessions_RevokeCreatedBefore_AndPurge()
        Dim clock As New TestClock()
        Dim reg As SessionRegistry = NewRegistry(clock, 5)
        Dim a As String = Nothing, b As String = Nothing
        reg.Create("u1", Role.Viewer, "s", "ip", "", a)
        clock.Advance(10)
        Dim cutoff As DateTime = clock.UtcNow
        clock.Advance(10)
        Dim rb As SessionRecord = reg.Create("u2", Role.Viewer, "s", "ip", "", b)
        reg.RevokeCreatedBefore(cutoff)
        Dim res As SessionAuthResult
        Assert.Null(reg.TryAuthenticate(a, res))
        Assert.NotNull(reg.TryAuthenticate(b, res))
        clock.Advance(40 * 60)
        Assert.Equal(1, reg.PurgeExpired())
        Assert.True(rb.IsRevoked)
    End Sub

    ' ---------------- AuditSanitizer ----------------
    <Fact>
    Public Sub AuditSanitizer_StripsControlBidiAnsi_AndCaps()
        Dim hostile As String = "ali" & vbCr & vbLf & "ce" & ChrW(&H202E) & "txt" & ChrW(&H2028) & ChrW(&H1B) & "[31mred" & ChrW(&H85) & ChrW(&H200F)
        Assert.Equal("alicetxtred", AuditSanitizer.Clean(hostile, 100))
        Assert.Equal("abc", AuditSanitizer.Clean("abcdef", 3))
        Assert.Equal("", AuditSanitizer.Clean(Nothing, 10))
        Assert.Equal("مرحبا", AuditSanitizer.Clean("مرحبا", 10))
    End Sub

    <Fact>
    Public Sub AuditLog_HostileFields_CannotForgeEntries()
        Dim path As String = IO.Path.Combine(TempDir(), "a.jsonl")
        Dim log As New FileAuditLog(path, RandomNumberGenerator.GetBytes(32))
        log.Write("evil" & vbLf & "{""Seq"":99}" & New String("x"c, 500), "Act" & ChrW(&H202E), New String("t"c, 500), "OK" & vbLf, "d" & ChrW(&H1B) & "[2Jetails")
        Dim lines As String() = IO.File.ReadAllLines(path)
        Assert.Single(lines)
        Dim e As AuditEntry = log.ReadRecent(1).Single()
        Assert.True(e.User.Length <= 64)
        Assert.True(e.Target.Length <= 128)
        Assert.False(e.User.Contains(ControlChars.Lf))
        Assert.False(e.Action.Contains(ChrW(&H202E)))
        Assert.Equal("details", e.Details)
        Assert.True(log.Verify().IsValid)
    End Sub

    ' ---------------- UserStore additions ----------------
    Private Const Pw As String = "Str0ng!Passw0rd"

    Private Shared Function NewUsers(dir As String, clock As TestClock) As UserStore
        Return New UserStore(IO.Path.Combine(dir, "users.json"), Nothing, clock, 1000)
    End Function

    <Fact>
    Public Sub UserStore_VerifyCredentials_HasNoSideEffects()
        Dim dir As String = TempDir()
        Dim s As UserStore = NewUsers(dir, New TestClock())
        s.CreateUser("sys", "alice", Pw, Role.Operator)
        Dim before As String = IO.File.ReadAllText(IO.Path.Combine(dir, "users.json"))
        For i As Integer = 1 To 12
            Assert.Null(s.VerifyCredentials("alice", "wrong-password-" & i))
            Assert.Null(s.VerifyCredentials("ghost", "whatever"))
        Next
        Assert.Equal(before, IO.File.ReadAllText(IO.Path.Combine(dir, "users.json")))
        Assert.Equal(0, s.TryGet("alice").FailedCount)
        Dim ok As UserRecord = s.VerifyCredentials("ALICE", Pw)
        Assert.Equal(Role.Operator, ok.Role)
        Assert.NotNull(s.Authenticate("alice", Pw))   ' الدخول المحلي لم يُقفل بسبب المحاولات البعيدة
    End Sub

    <Fact>
    Public Sub UserStore_VerifyCredentials_RejectsDisabledAndLocked()
        Dim clock As New TestClock()
        Dim s As UserStore = NewUsers(TempDir(), clock)
        s.CreateUser("sys", "root1", Pw, Role.Administrator)
        s.CreateUser("sys", "bob", Pw, Role.Viewer)
        s.SetDisabled("sys", "bob", True)
        Assert.Null(s.VerifyCredentials("bob", Pw))
        s.SetDisabled("sys", "bob", False)
        For i As Integer = 1 To UserStore.MaxFailedAttempts
            Assert.Throws(Of AuthenticationException)(Function() s.Authenticate("bob", "bad"))
        Next
        Assert.Null(s.VerifyCredentials("bob", Pw))     ' قفل محلي
        s.Unlock("sys", "bob")
        Assert.NotNull(s.VerifyCredentials("bob", Pw))
    End Sub

    <Fact>
    Public Sub UserStore_SecurityStamp_ChangesWithRolePasswordDisable()
        Dim s As UserStore = NewUsers(TempDir(), New TestClock())
        s.CreateUser("sys", "root1", Pw, Role.Administrator)
        s.CreateUser("sys", "carol", Pw, Role.Viewer)
        Dim s0 As String = s.GetSecurityStamp("carol")
        Assert.Equal(s0, s.GetSecurityStamp("CAROL"))
        s.SetRole("sys", "carol", Role.Operator)
        Dim s1 As String = s.GetSecurityStamp("carol")
        Assert.NotEqual(s0, s1)
        s.ChangePassword("sys", "carol", "An0ther!Passw0rd")
        Dim s2 As String = s.GetSecurityStamp("carol")
        Assert.NotEqual(s1, s2)
        s.SetDisabled("sys", "carol", True)
        Assert.NotEqual(s2, s.GetSecurityStamp("carol"))
        Assert.Null(s.GetSecurityStamp("nobody"))
    End Sub

    <Fact>
    Public Sub UserStore_ReloadIfChanged_PicksUpOtherProcessWrites()
        Dim dir As String = TempDir()
        Dim clock As New TestClock()
        Dim service As UserStore = NewUsers(dir, clock)
        service.CreateUser("sys", "root1", Pw, Role.Administrator)
        Dim cli As UserStore = NewUsers(dir, clock)
        Assert.False(service.ReloadIfChanged())
        cli.CreateUser("cli", "newuser", Pw, Role.Supervisor)
        Assert.Null(service.TryGet("newuser"))
        Assert.True(service.ReloadIfChanged())
        Assert.Equal(Role.Supervisor, service.TryGet("newuser").Role)
        Assert.False(service.ReloadIfChanged())
    End Sub

    <Theory>
    <InlineData("аdmin")>          ' Cyrillic а
    <InlineData("admın")>
    <InlineData("us er")>
    <InlineData("ab")>
    Public Sub UserStore_RejectsNonAsciiOrMalformedNames(name As String)
        Dim s As UserStore = NewUsers(TempDir(), New TestClock())
        Assert.Throws(Of ArgumentException)(Sub() s.CreateUser("sys", name, Pw, Role.Viewer))
    End Sub
End Class
