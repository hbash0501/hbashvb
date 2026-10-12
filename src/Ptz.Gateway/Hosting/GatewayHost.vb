Imports System.Net
Imports System.Security.Authentication
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.HttpOverrides
Imports Microsoft.AspNetCore.Server.Kestrel.Core
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports Ptz.Gateway.Api

Namespace Hosting

    ''' <summary>جذر التركيب الوحيد للخادم: يستعمله Main والاختبارات (التي تمرّر UseTestServer عبر configureBuilder).</summary>
    Public NotInheritable Class GatewayHost
        Private Sub New()
        End Sub

        Public Shared Function Build(options As GatewayOptions, svc As GatewayServices,
                                     Optional configureBuilder As Action(Of WebApplicationBuilder) = Nothing,
                                     Optional configurePipeline As Action(Of WebApplication) = Nothing,
                                     Optional registryOut As Action(Of ApiRegistry) = Nothing) As WebApplication
            Dim builder As WebApplicationBuilder = WebApplication.CreateBuilder(New WebApplicationOptions With {.ContentRootPath = AppContext.BaseDirectory, .Args = Array.Empty(Of String)()})
            builder.Logging.ClearProviders()
            builder.Logging.AddConsole()
            builder.Logging.SetMinimumLevel(LogLevel.Warning)
            builder.Host.UseWindowsService(Sub(o) o.ServiceName = "PtzGateway")
            builder.WebHost.ConfigureKestrel(Sub(k) ConfigureKestrel(k, options))
            builder.Services.AddSingleton(svc)
            builder.Services.AddRateLimiter(Sub(o)
                                                o.GlobalLimiter = Threading.RateLimiting.PartitionedRateLimiter.Create(Of Microsoft.AspNetCore.Http.HttpContext, String)(AddressOf Middleware.Partition)
                                                o.OnRejected = Function(c, ct) New ValueTask(Middleware.WriteRejectedAsync(c, ct))
                                            End Sub)
            builder.Services.AddHostedService(Function(sp) New GatewayLifecycle(svc))
            builder.Services.AddHostedService(Function(sp) New GatewayMaintenance(svc))
            If configureBuilder IsNot Nothing Then configureBuilder(builder)

            Dim app As WebApplication = builder.Build()
            If configurePipeline IsNot Nothing Then configurePipeline(app)

            If options.TrustedProxies IsNot Nothing AndAlso options.TrustedProxies.Count > 0 Then
                Dim fh As New ForwardedHeadersOptions With {.ForwardedHeaders = ForwardedHeaders.XForwardedFor Or ForwardedHeaders.XForwardedProto, .ForwardLimit = 1}
                fh.KnownProxies.Clear()
                fh.KnownNetworks.Clear()
                For Each p As String In options.TrustedProxies
                    fh.KnownProxies.Add(IPAddress.Parse(p))
                Next
                app.UseForwardedHeaders(fh)
            End If
            app.Use(AddressOf Middleware.SecurityHeaders)
            app.Use(Middleware.HostFilter(options.AllowedHosts))
            app.Use(AddressOf Middleware.RequireHttpsUnlessLoopback)
            app.UseRouting()
            app.UseRateLimiter()
            app.UseStatusCodePages(Function(sc As Microsoft.AspNetCore.Diagnostics.StatusCodeContext) Middleware.WriteStatusAsync(sc.HttpContext))

            Dim registry As New ApiRegistry(app, svc)
            Routes.RegisterAll(registry, svc)
            If registryOut IsNot Nothing Then registryOut(registry)
            Return app
        End Function

        Private Shared Sub ConfigureKestrel(k As KestrelServerOptions, options As GatewayOptions)
            k.AddServerHeader = False
            k.Limits.MaxRequestBodySize = 16384
            k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10)
            k.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60)
            k.Limits.MinRequestBodyDataRate = New MinDataRate(240, TimeSpan.FromSeconds(5))
            k.Limits.MaxConcurrentConnections = 100
            k.Limits.MaxRequestLineSize = 4096
            k.Limits.MaxRequestHeadersTotalSize = 16384
            k.Limits.MaxRequestHeaderCount = 50
            k.Limits.Http2.MaxStreamsPerConnection = 20
            Dim cert As System.Security.Cryptography.X509Certificates.X509Certificate2 = Nothing
            If Not String.IsNullOrWhiteSpace(options.Tls.PfxPath) OrElse Not String.IsNullOrWhiteSpace(options.Tls.PemPath) Then
                cert = KestrelTls.LoadCertificate(options.Tls, Function(n) Environment.GetEnvironmentVariable(n))
            End If
            For Each l As ListenEndpoint In options.Listen
                Dim address As IPAddress = IPAddress.Parse(l.Address)
                k.Listen(address, l.Port,
                         Sub(lo)
                             lo.Protocols = HttpProtocols.Http1AndHttp2
                             If cert IsNot Nothing Then
                                 lo.UseHttps(Sub(h)
                                                 h.ServerCertificate = cert
                                                 h.SslProtocols = SslProtocols.Tls12 Or SslProtocols.Tls13
                                                 h.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.NoCertificate
                                             End Sub)
                             End If
                         End Sub)
            Next
        End Sub
    End Class

    ''' <summary>يشغّل كاميرات البوابة عند الإقلاع ويوقفها بنظافة (توقف الكاميرا إن كانت تتحرك).</summary>
    Public NotInheritable Class GatewayLifecycle
        Implements IHostedService

        Private ReadOnly _svc As GatewayServices

        Public Sub New(svc As GatewayServices)
            _svc = svc
        End Sub

        Public Function StartAsync(cancellationToken As Threading.CancellationToken) As Task Implements IHostedService.StartAsync
            Return _svc.Cameras.StartAllAsync(cancellationToken)
        End Function

        Public Function StopAsync(cancellationToken As Threading.CancellationToken) As Task Implements IHostedService.StopAsync
            Return _svc.Cameras.StopAllAsync()
        End Function
    End Class

End Namespace
