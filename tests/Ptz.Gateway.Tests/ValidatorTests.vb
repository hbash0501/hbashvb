Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Xunit

Public Class ValidatorTests
    Implements IDisposable

    Private ReadOnly _dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-val-" & Guid.NewGuid().ToString("N"))
    Private ReadOnly _now As DateTime = DateTime.UtcNow

    Public Sub New()
        IO.Directory.CreateDirectory(_dir)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            IO.Directory.Delete(_dir, True)
        Catch ex As IO.IOException
        End Try
    End Sub

    Private Shared Function NoEnv() As Func(Of String, String)
        Return Function(k) Nothing
    End Function

    Private Function Pfx(days As Integer, Optional password As String = Nothing) As String
        Dim path As String = IO.Path.Combine(_dir, Guid.NewGuid().ToString("N") & ".pfx")
        IO.File.WriteAllBytes(path, GatewayCert.CreateSelfSigned("gw.test", days))
        Return path
    End Function

    Private Shared Function Cams(Optional https As Boolean = True, Optional allowHttp As Boolean = False) As CameraConnection()
        Return {New CameraConnection With {.Host = "10.0.0.1", .UseHttps = https, .AllowInsecureHttp = allowHttp}}
    End Function

    Private Function Check(o As GatewayOptions, Optional env As Func(Of String, String) = Nothing, Optional users As Integer = 1, Optional totp As Boolean = True,
                           Optional cameras As IEnumerable(Of CameraConnection) = Nothing, Optional now As DateTime? = Nothing) As ValidationReport
        o.DataDir = _dir
        Return StartupSecurityValidator.Check(o, If(env, NoEnv()), users, totp, If(cameras, Cams()), If(now, _now))
    End Function

    Private Function Exposed(Optional mode As String = "Vpn") As GatewayOptions
        Dim o As New GatewayOptions With {.ExposureMode = mode}
        o.Listen = New List(Of ListenEndpoint) From {New ListenEndpoint With {.Address = "0.0.0.0", .Port = 8443}}
        o.AllowedHosts = New List(Of String) From {"gw.example.test"}
        o.Tls.PfxPath = Pfx(90)
        Return o
    End Function

    Private Shared Function Has(report As ValidationReport, fragment As String) As Boolean
        Return report.Violations.Any(Function(v) v.Contains(fragment))
    End Function

    <Fact>
    Public Sub Defaults_OnLoopback_AreAccepted()
        Dim r As ValidationReport = Check(New GatewayOptions())
        Assert.True(r.IsOk, String.Join("; ", r.Violations))
        Assert.Empty(r.Warnings)
    End Sub

    <Fact>
    Public Sub WellFormedExposedSetup_IsAccepted()
        Dim r As ValidationReport = Check(Exposed())
        Assert.True(r.IsOk, String.Join("; ", r.Violations))
    End Sub

    <Fact>
    Public Sub ExternalListening_NeedsAValidCertificate()
        Dim noCert As GatewayOptions = Exposed()
        noCert.Tls.PfxPath = ""
        Assert.True(Has(Check(noCert), "TLS"))
        Dim missing As GatewayOptions = Exposed()
        missing.Tls.PfxPath = IO.Path.Combine(_dir, "nope.pfx")
        Assert.True(Has(Check(missing), "TLS"))
        Dim expired As GatewayOptions = Exposed()
        Assert.True(Has(Check(expired, now:=_now.AddYears(5)), "منتهية"))
        Dim garbage As GatewayOptions = Exposed()
        Dim bad As String = IO.Path.Combine(_dir, "bad.pfx")
        IO.File.WriteAllText(bad, "not a certificate")
        garbage.Tls.PfxPath = bad
        Assert.True(Has(Check(garbage), "TLS"))
        ' الحلقة المحلية لا تحتاج شهادة
        Assert.True(Check(New GatewayOptions()).IsOk)
    End Sub

    <Fact>
    Public Sub NearExpiryCertificate_OnlyWarns()
        Dim o As GatewayOptions = Exposed()
        o.Tls.PfxPath = Pfx(3)
        Dim r As ValidationReport = Check(o)
        Assert.True(r.IsOk)
        Assert.Contains(r.Warnings, Function(w) w.Contains("7 أيام"))
    End Sub

    <Fact>
    Public Sub ExternalListening_RejectsOpenAllowedHosts()
        Dim star As GatewayOptions = Exposed()
        star.AllowedHosts = New List(Of String) From {"*"}
        Assert.True(Has(Check(star), "AllowedHosts"))
        Dim empty As GatewayOptions = Exposed()
        empty.AllowedHosts = New List(Of String)()
        Assert.True(Has(Check(empty), "AllowedHosts"))
    End Sub

    <Fact>
    Public Sub UnknownMode_BadPort_AndNoListener_AreRejected()
        Assert.True(Has(Check(New GatewayOptions With {.ExposureMode = "Internet"}), "ExposureMode"))
        Dim port As New GatewayOptions()
        port.Listen = New List(Of ListenEndpoint) From {New ListenEndpoint With {.Address = "127.0.0.1", .Port = 70000}}
        Assert.True(Has(Check(port), "منفذ"))
        Dim none As New GatewayOptions()
        none.Listen = New List(Of ListenEndpoint)()
        Assert.True(Has(Check(none), "عنوان استماع"))
    End Sub

    <Fact>
    Public Sub TrustedProxies_MustBeSingleAddresses_NeverWildcards()
        Dim o As GatewayOptions = Exposed("Tunnel")
        o.TrustedProxies = New List(Of String) From {"10.0.0.0/8"}
        Assert.True(Has(Check(o), "TrustedProxies"))
        o.TrustedProxies = New List(Of String) From {"0.0.0.0"}
        Assert.True(Has(Check(o), "0.0.0.0"))
        o.TrustedProxies = New List(Of String) From {"::"}
        Assert.True(Has(Check(o), "TrustedProxies"))
        o.TrustedProxies = New List(Of String) From {"172.16.0.5"}
        Assert.True(Check(o).IsOk)
        o.TrustedProxies = New List(Of String)()
        Assert.Contains(Check(o).Warnings, Function(w) w.Contains("Tunnel"))
    End Sub

    <Fact>
    Public Sub Totp_CanBeDisabledOnlyOnLoopbackOrVpn_WithExplicitConsent()
        Dim vpn As GatewayOptions = Exposed("Vpn")
        vpn.Auth.RequireTotp = False
        Assert.True(Has(Check(vpn), "RequireTotp"))                 ' بلا AllowNoMfa
        vpn.Auth.AllowNoMfa = True
        Assert.True(Check(vpn).IsOk)
        Dim pub As GatewayOptions = Exposed("Public")
        pub.Auth.RequireTotp = False
        pub.Auth.AllowNoMfa = True
        Assert.True(Has(Check(pub), "RequireTotp"))
        Dim tunnel As GatewayOptions = Exposed("Tunnel")
        tunnel.Auth.RequireTotp = False
        tunnel.Auth.AllowNoMfa = True
        Assert.True(Has(Check(tunnel), "RequireTotp"))
    End Sub

    <Fact>
    Public Sub NobodyEnrolled_WarnsButDoesNotBlock()
        Dim r As ValidationReport = Check(New GatewayOptions(), totp:=False)
        Assert.True(r.IsOk)
        Assert.Contains(r.Warnings, Function(w) w.Contains("totp enroll"))
    End Sub

    <Fact>
    Public Sub SessionLimits_AreBounded()
        Dim o As New GatewayOptions()
        o.Auth.IdleMinutes = 4
        o.Auth.AbsoluteHours = 25
        o.Auth.MaxSessionsPerUser = 0
        Dim r As ValidationReport = Check(o)
        Assert.True(Has(r, "IdleMinutes"))
        Assert.True(Has(r, "AbsoluteHours"))
        Assert.True(Has(r, "MaxSessionsPerUser"))
    End Sub

    <Fact>
    Public Sub RemoteAdministrator_NeedsExplicitEnvironmentConsent()
        Dim o As New GatewayOptions()
        o.Auth.RemoteMaxRole = "Administrator"
        Assert.True(Has(Check(o), "PTZ_ALLOW_REMOTE_ADMIN"))
        Assert.True(Check(o, env:=Function(k) If(k = "PTZ_ALLOW_REMOTE_ADMIN", "1", Nothing)).IsOk)
        o.Auth.RemoteMaxRole = "Emperor"
        Assert.True(Has(Check(o), "RemoteMaxRole"))
    End Sub

    <Fact>
    Public Sub NoUsers_AndInsecureCameraHttp_AreRejected()
        Assert.True(Has(Check(New GatewayOptions(), users:=0), "مستخدمون"))
        Assert.True(Has(Check(New GatewayOptions(), cameras:=Cams(https:=False)), "HTTP"))
        Assert.True(Check(New GatewayOptions(), cameras:=Cams(https:=False, allowHttp:=True)).IsOk)
    End Sub

    <Fact>
    Public Sub MasterKeyFile_MustBePrivate_AndShouldLiveOutsideTheDataDir()
        If OperatingSystem.IsWindows() Then Return
        Dim key As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-key-" & Guid.NewGuid().ToString("N"))
        Try
            IO.File.WriteAllText(key, "k")
            IO.File.SetUnixFileMode(key, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite Or IO.UnixFileMode.GroupRead)
            Dim env As Func(Of String, String) = Function(k) If(k = "PTZ_MASTER_KEY_FILE", key, Nothing)
            Assert.True(Has(Check(New GatewayOptions(), env), "المفتاح الرئيسي"))
            IO.File.SetUnixFileMode(key, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite)
            Assert.True(Check(New GatewayOptions(), env).IsOk)
            Dim inside As String = IO.Path.Combine(_dir, "master.key")
            IO.File.WriteAllText(inside, "k")
            IO.File.SetUnixFileMode(inside, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite)
            Dim r As ValidationReport = Check(New GatewayOptions(), Function(k) If(k = "PTZ_MASTER_KEY_FILE", inside, Nothing))
            Assert.True(r.IsOk)
            Assert.Contains(r.Warnings, Function(w) w.Contains("مجلد البيانات"))
        Finally
            IO.File.Delete(key)
        End Try
    End Sub
End Class
