Imports Ptz.Core.Models
Imports Ptz.Gateway.Cloud
Imports Xunit

''' <summary>إعداد النقل السحابي: قواعد المدقّق عند الإقلاع، وأوامر CLI (camera set / hpp devices / hpp check) على خادم Hik-Partner Pro وهمي.</summary>
Public Class HppConfigTests
    Implements IDisposable

    Private ReadOnly _dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-hppcfg-" & Guid.NewGuid().ToString("N"))
    Private ReadOnly _clock As New TestClock()

    Public Sub New()
        IO.Directory.CreateDirectory(_dir)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            IO.Directory.Delete(_dir, True)
        Catch ex As IO.IOException
        End Try
    End Sub

    ' ===================== المدقّق =====================
    Private Shared Function Relay(serial As String) As CameraConnection
        Return New CameraConnection With {.Transport = CameraTransport.HikPartnerPro, .CloudSerial = serial}
    End Function

    Private Shared Function KeysEnv() As Func(Of String, String)
        Return Function(k) If(k = "PTZ_HPP_AK", FakeHppCloud.AppKey, If(k = "PTZ_HPP_SK", FakeHppCloud.SecretKey, Nothing))
    End Function

    Private Function Check(o As GatewayOptions, env As Func(Of String, String), ParamArray cams As CameraConnection()) As ValidationReport
        o.DataDir = _dir
        Return StartupSecurityValidator.Check(o, env, 1, True, cams, DateTime.UtcNow)
    End Function

    Private Shared Function Has(r As ValidationReport, fragment As String) As Boolean
        Return r.Violations.Any(Function(v) v.Contains(fragment))
    End Function

    <Fact>
    Public Sub RelayCamera_NeedsHppEnabled()
        Dim r As ValidationReport = Check(New GatewayOptions(), KeysEnv(), Relay("SE7SERIAL"))
        Assert.True(Has(r, "Hpp.Enabled=false"))
    End Sub

    <Fact>
    Public Sub RelayCamera_NeedsTheKeysInTheEnvironment_NotInTheConfig()
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        Dim missing As ValidationReport = Check(o, Function(k) Nothing, Relay("SE7SERIAL"))
        Assert.True(Has(missing, "PTZ_HPP_AK"))
        Dim ok As ValidationReport = Check(o, KeysEnv(), Relay("SE7SERIAL"))
        Assert.True(ok.IsOk, String.Join("; ", ok.Violations))
        Assert.Contains(ok.Warnings, Function(w) w.Contains("كل أجهزة الحساب"))
    End Sub

    <Fact>
    Public Sub EnabledWithoutRelayCameras_IsOnlyAWarningWhenKeysAreMissing()
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        Dim r As ValidationReport = Check(o, Function(k) Nothing)
        Assert.True(r.IsOk, String.Join("; ", r.Violations))
        Assert.Contains(r.Warnings, Function(w) w.Contains("بلا مفاتيح"))
    End Sub

    <Fact>
    Public Sub Disabled_AddsNoWarningsOrViolations()
        Dim r As ValidationReport = Check(New GatewayOptions(), Function(k) Nothing)
        Assert.True(r.IsOk)
        Assert.Empty(r.Warnings)
    End Sub

    <Theory>
    <InlineData("http://api.hik-partner.com")>
    <InlineData("https://api.evil.example")>
    <InlineData("https://api.hik-partner.com:8443")>
    <InlineData("https://api.hik-partner.com/x")>
    <InlineData("")>
    Public Sub BadTokenUrl_IsAViolation(url As String)
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        o.Hpp.TokenUrl = url
        Assert.True(Has(Check(o, KeysEnv()), "Hpp.TokenUrl"))
    End Sub

    <Theory>
    <InlineData("com")>
    <InlineData("*.hik-partner.com")>
    <InlineData("hik-partner.com/")>
    <InlineData("hik-partner.com:443")>
    Public Sub UnsafeSuffix_IsAViolation(suffix As String)
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        o.Hpp.AllowedHostSuffixes = New List(Of String) From {suffix}
        Assert.True(Has(Check(o, KeysEnv()), "AllowedHostSuffixes"))
    End Sub

    <Fact>
    Public Sub EmptySuffixList_IsAViolation()
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        o.Hpp.AllowedHostSuffixes = New List(Of String)()
        Assert.True(Has(Check(o, KeysEnv()), "AllowedHostSuffixes"))
    End Sub

    <Fact>
    Public Sub ExtraAllowed_InvalidRule_IsAViolation_ValidRuleOnlyWarns()
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        o.Hpp.ExtraAllowed = New List(Of String) From {"PUT /ISAPI/../etc"}
        Assert.True(Has(Check(o, KeysEnv()), "ExtraAllowed"))
        o.Hpp.ExtraAllowed = New List(Of String) From {"GET /ISAPI/PTZCtrl/channels/<ID>/status"}
        Dim r As ValidationReport = Check(o, KeysEnv())
        Assert.True(r.IsOk, String.Join("; ", r.Violations))
        Assert.Contains(r.Warnings, Function(w) w.Contains("ExtraAllowed"))
    End Sub

    <Fact>
    Public Sub Limits_AreRangeChecked()
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        o.Hpp.TimeoutSeconds = 1
        o.Hpp.MaxRequestsPerSecond = 100
        o.Hpp.MaxBurst = 0
        Dim r As ValidationReport = Check(o, KeysEnv())
        Assert.True(Has(r, "TimeoutSeconds"))
        Assert.True(Has(r, "MaxRequestsPerSecond"))
        Assert.True(Has(r, "MaxBurst"))
    End Sub

    <Fact>
    Public Sub DuplicateSerials_AreAViolation_AndLanCamerasKeepTheirRules()
        Dim o As New GatewayOptions()
        o.Hpp.Enabled = True
        Dim dup As ValidationReport = Check(o, KeysEnv(), Relay("A1"), Relay("A1"))
        Assert.True(Has(dup, "مكرر"))
        Dim mixed As ValidationReport = Check(o, KeysEnv(), Relay("A1"), New CameraConnection With {.Host = "10.0.0.2", .UseHttps = False})
        Assert.True(Has(mixed, "HTTP بلا AllowInsecureHttp"))
    End Sub

    ' ===================== CLI =====================
    Private Function NewCli(sw As IO.StringWriter, fake As FakeHppCloud, Optional withKeys As Boolean = True) As Cli
        Dim env As Func(Of String, String) = If(withKeys, KeysEnv(), Function(k) Nothing)
        Return New Cli(New GatewayOptions With {.DataDir = _dir}, sw, New IO.StringReader(""), Function(prompt) "", env, fake)
    End Function

    Private Function Run(fake As FakeHppCloud, withKeys As Boolean, ParamArray args As String()) As (Code As Integer, Output As String)
        Dim sw As New IO.StringWriter()
        Dim code As Integer = NewCli(sw, fake, withKeys).Run(args)
        Return (code, sw.ToString())
    End Function

    Private Function Fake() As FakeHppCloud
        Dim f As New FakeHppCloud(Ptz.Core.Abstractions.SystemClock.Instance)   ' الـ CLI يستعمل ساعة النظام
        f.AddDevice("SE7SERIAL")
        f.AddDevice("DE7SERIAL", "DS-2DE7232IW-AE")
        Return f
    End Function

    <Fact>
    Public Sub CameraSet_Hpp_StoresSerialOnly_NoPasswordPrompt_AndListShowsIt()
        Dim f As FakeHppCloud = Fake()
        Dim r = Run(f, True, "camera", "set", "se7c432mw", "--transport", "hpp", "--cloud-serial", "SE7SERIAL")
        Assert.Equal(0, r.Code)
        Dim list As String = Run(f, True, "camera", "list").Output
        Assert.Contains("se7c432mw", list)
        Assert.Contains("hpp serial=SE7SERIAL", list)
        Dim json As String = IO.File.ReadAllText(IO.Path.Combine(_dir, "cameras.json"))
        Assert.Contains("""Transport"":""hpp""", json)
        Assert.Contains("SE7SERIAL", json)
        Assert.Contains("Camera.Set", IO.File.ReadAllText(IO.Path.Combine(_dir, "audit-admin.log")))
    End Sub

    <Theory>
    <InlineData("--cloud-serial", "bad serial")>
    <InlineData("--cloud-serial", "x/y")>
    <InlineData("--transport", "ftp")>
    Public Sub CameraSet_Hpp_RejectsBadValues_WithoutStackTrace(flag As String, value As String)
        Dim args As New List(Of String) From {"camera", "set", "se7c432mw", "--transport", "hpp", "--cloud-serial", "OK1"}
        If flag = "--transport" Then args(4) = value Else args(6) = value
        Dim r = Run(Fake(), True, args.ToArray())
        Assert.Equal(1, r.Code)
        Assert.StartsWith("خطأ:", r.Output)
    End Sub

    <Fact>
    Public Sub CameraSet_HppWithoutSerial_IsRejected()
        Dim r = Run(Fake(), True, "camera", "set", "se7c432mw", "--transport", "hpp")
        Assert.Equal(1, r.Code)
    End Sub

    <Fact>
    Public Sub HppDevices_ListsAccountDevices_MarksConfigured_AndNeverPrintsKeys()
        Dim f As FakeHppCloud = Fake()
        Run(f, True, "camera", "set", "se7c432mw", "--transport", "hpp", "--cloud-serial", "SE7SERIAL")
        Dim r = Run(f, True, "hpp", "devices")
        Assert.Equal(0, r.Code)
        Assert.Contains("SE7SERIAL", r.Output)
        Assert.Contains("DE7SERIAL", r.Output)
        Assert.Contains("متصل", r.Output)
        Assert.Matches("SE7SERIAL.*\[مضبوط\]", r.Output)
        Assert.DoesNotContain(FakeHppCloud.AppKey, r.Output)
        Assert.DoesNotContain(FakeHppCloud.SecretKey, r.Output)
        Assert.DoesNotContain("hpc.tok", r.Output)
    End Sub

    <Fact>
    Public Sub HppDevices_WithoutKeys_ExplainsTheEnvironmentVariables()
        Dim r = Run(Fake(), False, "hpp", "devices")
        Assert.Equal(1, r.Code)
        Assert.Contains("PTZ_HPP_AK", r.Output)
        Assert.Contains("PTZ_HPP_SK", r.Output)
    End Sub

    <Fact>
    Public Sub HppDevices_WithWrongKeys_FailsCleanly_WithoutEchoingThem()
        Dim sw As New IO.StringWriter()
        Dim cli As New Cli(New GatewayOptions With {.DataDir = _dir}, sw, New IO.StringReader(""), Function(p) "",
                           Function(k) If(k = "PTZ_HPP_AK", "wrong-ak-1", If(k = "PTZ_HPP_SK", "wrong-sk-2", Nothing)), Fake())
        Assert.Equal(1, cli.Run({"hpp", "devices"}))
        Assert.StartsWith("خطأ:", sw.ToString())
        Assert.DoesNotContain("wrong-ak-1", sw.ToString())
        Assert.DoesNotContain("wrong-sk-2", sw.ToString())
    End Sub

    <Fact>
    Public Sub HppCheck_ReadsInfoAndPresets_AndWithStopSendsOnlyAZeroVector()
        Dim f As FakeHppCloud = Fake()
        Run(f, True, "camera", "set", "se7c432mw", "--transport", "hpp", "--cloud-serial", "SE7SERIAL")
        Dim r = Run(f, True, "hpp", "check", "--stop")
        Assert.Equal(0, r.Code)
        Assert.Contains("OK    اتصال/معلومات الجهاز", r.Output)
        Assert.Contains("OK    قراءة المواضع المسبقة", r.Output)
        Assert.Contains("OK    أمر إيقاف", r.Output)
        Assert.Equal({"stop"}, f.MoveLog("SE7SERIAL"))        ' لا حركة أبداً
        Assert.Contains("Hpp.Check", IO.File.ReadAllText(IO.Path.Combine(_dir, "audit-admin.log")))
    End Sub

    <Fact>
    Public Sub HppCheck_WithoutStopFlag_SendsNoCommandAtAll()
        Dim f As FakeHppCloud = Fake()
        Run(f, True, "camera", "set", "se7c432mw", "--transport", "hpp", "--cloud-serial", "SE7SERIAL")
        Assert.Equal(0, Run(f, True, "hpp", "check").Code)
        Assert.Empty(f.MoveLog("SE7SERIAL"))
        Assert.DoesNotContain(f.Transparent(), Function(x) x.Method <> "GET")
    End Sub

    <Fact>
    Public Sub HppCheck_ReportsOfflineDevice_WithTheCloudCode_AndExitCode4()
        Dim f As FakeHppCloud = Fake()
        f.Devices("SE7SERIAL").Online = False
        Run(f, True, "camera", "set", "se7c432mw", "--transport", "hpp", "--cloud-serial", "SE7SERIAL")
        Dim r = Run(f, True, "hpp", "check")
        Assert.Equal(4, r.Code)
        Assert.Contains("FAIL  اتصال/معلومات الجهاز", r.Output)
        Assert.Contains("EVZ20007", r.Output)
    End Sub

    <Fact>
    Public Sub HppCheck_WithNoRelayCameras_SaysSo()
        Dim r = Run(Fake(), True, "hpp", "check")
        Assert.Equal(1, r.Code)
        Assert.Contains("transport hpp", r.Output)
    End Sub

    <Fact>
    Public Sub Usage_MentionsTheNewCommands()
        Dim out As String = Run(Fake(), True, "bogus").Output
        Assert.Contains("hpp devices", out)
        Assert.Contains("--transport hpp", out)
    End Sub

    <Fact>
    Public Sub OldCamerasJson_WithoutTransportFields_StillLoadsAsLan()
        IO.File.WriteAllText(IO.Path.Combine(_dir, "cameras.json"),
            "[{""Key"":""se7c432mw"",""DisplayName"":""x"",""ModelName"":""DS-2SE7C432MW-AEB"",""Host"":""10.0.0.9"",""Port"":443,""UseHttps"":true,""AllowInsecureHttp"":false,""Username"":""svc"",""PasswordProtected"":"""",""PinnedCertSha256"":"""",""TimeoutSeconds"":10}]")
        Dim r = Run(Fake(), True, "camera", "list")
        Assert.Equal(0, r.Code)
        Assert.Contains("10.0.0.9:443", r.Output)
        Assert.DoesNotContain("hpp", r.Output)
    End Sub
End Class
