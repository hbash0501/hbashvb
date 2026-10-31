Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Xunit

Public Class CliTests
    Implements IDisposable

    Private Const Pw As String = "Str0ng!Passw0rd"
    Private ReadOnly _dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-cli-" & Guid.NewGuid().ToString("N"))
    Private ReadOnly _secrets As New Queue(Of String)()

    Public Sub New()
        IO.Directory.CreateDirectory(_dir)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            IO.Directory.Delete(_dir, True)
        Catch ex As IO.IOException
        End Try
    End Sub

    Private Function Run(ParamArray args As String()) As (Code As Integer, Output As String)
        Dim sw As New IO.StringWriter()
        Dim cli As New Cli(New GatewayOptions With {.DataDir = _dir}, sw, New IO.StringReader(""),
                           Function(prompt) If(_secrets.Count > 0, _secrets.Dequeue(), ""),
                           Function(k) Nothing)
        Dim code As Integer = cli.Run(args)
        Return (code, sw.ToString())
    End Function

    Private Sub Pass(ParamArray values As String())
        For Each v As String In values
            _secrets.Enqueue(v)
        Next
    End Sub

    Private Function MakeUser(name As String, role As String) As (Code As Integer, Output As String)
        Pass(Pw, Pw)
        Return Run("user", "add", name, role)
    End Function

    <Fact>
    Public Sub UserAdd_CreatesAccount_AndListShowsRoleAndTotpState()
        Assert.Equal(0, MakeUser("bob", "operator").Code)
        Dim list As String = Run("user", "list").Output
        Assert.Contains("bob", list)
        Assert.Contains("Operator", list)
        Assert.Contains("TOTP=لا", list)
        Assert.DoesNotContain(Pw, list)
    End Sub

    <Fact>
    Public Sub UserAdd_RejectsWeakMismatchedAndUnknownRole_WithoutStackTraces()
        Pass("short", "short")
        Dim weak = Run("user", "add", "bob", "operator")
        Assert.Equal(1, weak.Code)
        Assert.StartsWith("خطأ:", weak.Output)
        Pass(Pw, Pw & "x")
        Assert.Equal(1, Run("user", "add", "bob", "operator").Code)
        Pass(Pw, Pw)
        Assert.Equal(1, Run("user", "add", "bob", "emperor").Code)
        Assert.DoesNotContain("   at ", Run("user", "add", "bob", "emperor").Output)
        Assert.DoesNotContain("bob", Run("user", "list").Output)
        ' كلمة مرور تحوي اسم المستخدم (نفرّغ ما تبقّى من الطابور بعد رفض الدور قبل قراءة كلمة المرور)
        _secrets.Clear()
        Pass("bobBOB!Passw0rd1", "bobBOB!Passw0rd1")
        Assert.Equal(1, Run("user", "add", "bob", "viewer").Code)
    End Sub

    <Fact>
    Public Sub UserLifecycle_Role_Disable_Enable_Passwd_Unlock_AreAppliedAndAudited()
        MakeUser("bob", "viewer")
        Assert.Equal(0, Run("user", "role", "bob", "supervisor").Code)
        Assert.Contains("Supervisor", Run("user", "list").Output)
        Assert.Equal(0, Run("user", "disable", "bob").Code)
        Assert.Contains("معطّل", Run("user", "list").Output)
        Assert.Equal(0, Run("user", "enable", "bob").Code)
        Pass("An0ther!Passw0rd", "An0ther!Passw0rd")
        Assert.Equal(0, Run("user", "passwd", "bob").Code)
        Assert.Equal(0, Run("user", "unlock", "bob").Code)
        Assert.True(IO.File.Exists(IO.Path.Combine(_dir, "unlock.bob.request")))
        Assert.Equal(1, Run("user", "disable", "ghost").Code)
        Dim adminLog As String = IO.File.ReadAllText(IO.Path.Combine(_dir, "audit-admin.log"))
        For Each action As String In {"User.Create", "User.SetRole", "User.Disable", "User.Enable", "User.ChangePassword", "User.Unlock"}
            Assert.Contains(action, adminLog)
        Next
        Assert.Contains("cli:", adminLog)
    End Sub

    <Fact>
    Public Sub Totp_Enroll_PrintsSecretOnce_AndTheSecretVerifiesCodes_Reset_Removes()
        MakeUser("bob", "operator")
        Dim r = Run("totp", "enroll", "bob")
        Assert.Equal(0, r.Code)
        Dim line As String = r.Output.Split(ControlChars.Lf).First(Function(l) l.Trim().Length >= 16 AndAlso l.Trim().All(Function(ch) ch >= "A"c AndAlso ch <= "Z"c OrElse ch >= "2"c AndAlso ch <= "7"c))
        Dim secret As Byte() = Totp.FromBase32(line.Trim())
        Assert.Contains("otpauth://totp/", r.Output)
        Assert.Contains("TOTP=نعم", Run("user", "list").Output)
        Dim now As Long = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        Assert.True(Totp.Verify(secret, Totp.Compute(secret, Totp.StepOf(now)), now, -1) >= 0)
        Assert.Equal(0, Run("totp", "reset", "bob").Code)
        Assert.Contains("TOTP=لا", Run("user", "list").Output)
        Assert.Equal(1, Run("totp", "enroll", "ghost").Code)
        ' لا يُخزَّن السر نصاً صريحاً
        Assert.DoesNotContain(line.Trim(), IO.File.ReadAllText(IO.Path.Combine(_dir, "totp.json")))
    End Sub

    <Fact>
    Public Sub Camera_Set_PersistsEncryptedPassword_AndNeverEchoesIt()
        Pass("Cam-Secret-Pw-9")
        Dim r = Run("camera", "set", "se7c432mw", "--host", "192.0.2.10", "--user", "svc", "--name", "Gate", "--pin", "AB" & New String("0"c, 62))
        Assert.Equal(0, r.Code)
        Assert.DoesNotContain("Cam-Secret-Pw-9", r.Output)
        Dim cfg As String = IO.File.ReadAllText(IO.Path.Combine(_dir, "cameras.json"))
        Assert.DoesNotContain("Cam-Secret-Pw-9", cfg)
        Dim list As String = Run("camera", "list").Output
        Assert.Contains("192.0.2.10:443", list)
        Assert.Contains("pinned=True", list)
        Assert.Contains("de7232iw", list)
        Assert.DoesNotContain("192.0.2.10", IO.File.ReadAllText(IO.Path.Combine(_dir, "audit-admin.log")).Replace("192.0.2.10:443 https=True", ""))
        Assert.Equal(1, Run("camera", "set", "nope", "--host", "x").Code)
    End Sub

    <Fact>
    Public Sub Camera_HttpInsecure_RequiresTheExplicitFlag_AndSwitchesPort()
        Pass("")
        Assert.Equal(0, Run("camera", "set", "de7232iw", "--host", "192.0.2.11", "--user", "svc", "--http-insecure").Code)
        Dim list As String = Run("camera", "list").Output
        Assert.Contains("192.0.2.11:80", list)
        Assert.Contains("https=False", list)
    End Sub

    <Fact>
    Public Sub Cert_Create_WritesPrivatePfx_AndPrintsBothPins_AndShowMatches()
        Dim r = Run("cert", "create", "--host", "gw.example.test", "--days", "30")
        Assert.Equal(0, r.Code)
        Dim pfx As String = IO.Path.Combine(_dir, "tls", "gw.pfx")
        Assert.True(IO.File.Exists(pfx))
        If Not OperatingSystem.IsWindows() Then
            Assert.Equal(IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite, IO.File.GetUnixFileMode(pfx))
        End If
        Assert.Contains("spki-sha256:", r.Output)
        Assert.Contains("cert-sha256:", r.Output)
        Dim show = Run("cert", "show")
        Assert.Equal(0, show.Code)
        Dim spki As String = r.Output.Split(ControlChars.Lf).First(Function(l) l.Contains("spki-sha256:"))
        Assert.Contains(spki.Trim(), show.Output)
        Assert.Equal(1, Run("cert", "create").Code)
        Assert.Equal(1, Run("cert", "create", "--host", "x", "--days", "9999").Code)
    End Sub

    <Fact>
    Public Sub RevokeAll_WritesEpochFile_AndVerifyAudit_ReportsChains()
        MakeUser("bob", "viewer")
        Assert.Equal(0, Run("revoke-all").Code)
        Dim epoch As String = IO.File.ReadAllText(IO.Path.Combine(_dir, "revoke.epoch"))
        Assert.True(Long.Parse(epoch) > 1_700_000_000_000L)
        Dim ok = Run("verify-audit")
        Assert.Equal(0, ok.Code)
        Assert.Contains("audit-admin.log", ok.Output)
        Dim path As String = IO.Path.Combine(_dir, "audit-admin.log")
        Dim lines As List(Of String) = IO.File.ReadAllLines(path).ToList()
        lines(0) = lines(0).Replace("User.Create", "User.Creatf")
        IO.File.WriteAllLines(path, lines)
        Assert.Equal(3, Run("verify-audit").Code)
    End Sub

    <Fact>
    Public Sub UnknownOrMissingCommands_PrintUsage_WithExitCode64()
        Assert.Equal(64, Run().Code)
        Assert.Equal(64, Run("frobnicate").Code)
        Assert.Equal(64, Run("user").Code)
        Assert.Equal(64, Run("user", "add").Code)
        Assert.Contains("الاستخدام", Run("camera").Output)
    End Sub

    <Fact>
    Public Sub CliUsesItsOwnAuditChain_AndLeavesTheServiceChainUntouched()
        MakeUser("bob", "viewer")
        Assert.False(IO.File.Exists(IO.Path.Combine(_dir, "audit.log")))
        Assert.True(IO.File.Exists(IO.Path.Combine(_dir, "audit-admin.log")))
    End Sub
End Class
