Imports Microsoft.AspNetCore.Builder
Imports Ptz.Core.Models
Imports Ptz.Gateway.Hosting

Public Module Program

    Private Function ConfigPath(args As String()) As String
        For i As Integer = 0 To args.Length - 2
            If args(i) = "--config" Then Return args(i + 1)
        Next
        Dim env As String = Environment.GetEnvironmentVariable("PTZ_GATEWAY_CONFIG")
        If Not String.IsNullOrWhiteSpace(env) Then Return env
        Return IO.Path.Combine(AppContext.BaseDirectory, "gateway.json")
    End Function

    Public Function Main(args As String()) As Integer
        Dim opts As GatewayOptions
        Try
            opts = GatewayOptions.Load(ConfigPath(args))
        Catch ex As Exception When TypeOf ex Is Text.Json.JsonException OrElse TypeOf ex Is IO.IOException
            Console.Error.WriteLine("ملف الإعداد غير صالح: " & ex.Message)
            Return 2
        End Try
        Dim verb As String = If(args.Length = 0 OrElse args(0).StartsWith("--"), "serve", args(0))
        Select Case verb
            Case "serve" : Return Serve(opts)
            Case "check" : Return Check(opts, True)
            Case Else
                Dim cli As New Cli(opts, Console.Out, Console.In, AddressOf Cli.ReadSecretFromConsole)
                Return cli.Run(args)
        End Select
    End Function

    Private Function Check(opts As GatewayOptions, print As Boolean) As Integer
        Dim svc As GatewayServices = GatewayServices.CreateProduction(opts)
        Dim conns As New List(Of CameraConnection)()
        For Each c In svc.CameraConfigs.All()
            If Not String.IsNullOrWhiteSpace(c.Host) Then conns.Add(svc.CameraConfigs.BuildConnection(c.Key))
        Next
        Dim report As ValidationReport = StartupSecurityValidator.Check(opts, Function(k) Environment.GetEnvironmentVariable(k), svc.Users.ListUsers().Count,
                                                                         svc.Totp.AnyEnrolled(), conns, DateTime.UtcNow)
        For Each w As String In report.Warnings
            Console.Error.WriteLine("تحذير: " & w)
        Next
        For Each v As String In report.Violations
            Console.Error.WriteLine("مخالفة: " & v)
        Next
        If print AndAlso report.IsOk Then Console.WriteLine("الإعداد سليم.")
        Return If(report.IsOk, 0, 2)
    End Function

    Private Function Serve(opts As GatewayOptions) As Integer
        If Check(opts, False) <> 0 Then
            Console.Error.WriteLine("رُفض الإقلاع بسبب مخالفات أمنية في الإعداد.")
            Return 2
        End If
        Dim svc As GatewayServices = GatewayServices.CreateProduction(opts)
        Dim app As WebApplication = GatewayHost.Build(opts, svc)
        app.Run()
        Return 0
    End Function

End Module
