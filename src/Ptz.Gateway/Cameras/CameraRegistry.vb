Imports Ptz.Presentation
Imports Ptz.Core.Models
Imports Ptz.Gateway.Hosting

Namespace Cameras

    ''' <summary>مجموعة كاميرات البوابة (من cameras.json). الإدخال بلا عنوان لا يُشغَّل.</summary>
    Public NotInheritable Class CameraRegistry
        Private ReadOnly _svc As GatewayServices
        Private ReadOnly _hosts As New Dictionary(Of String, CameraHost)(StringComparer.Ordinal)
        Private _pump As Cloud.HppEventPump

        Public Sub New(svc As GatewayServices)
            _svc = svc
            For Each cfg In svc.CameraConfigs.All()
                If Not IsRunnable(cfg) Then Continue For
                If Not System.Text.RegularExpressions.Regex.IsMatch(cfg.Key, "^[a-z0-9-]{1,32}$") Then Continue For
                _hosts(cfg.Key) = New CameraHost(svc, cfg)
            Next
        End Sub

        ''' <summary>تُشغَّل الكاميرا إن كان لها عنوان (lan) أو رقم تسلسلي سحابي صالح (hpp).</summary>
        Public Shared Function IsRunnable(cfg As CameraConfig) As Boolean
            If String.Equals(cfg.Transport, "hpp", StringComparison.OrdinalIgnoreCase) Then Return CameraTransports.IsValidCloudSerial(cfg.CloudSerial)
            Return Not String.IsNullOrWhiteSpace(cfg.Host)
        End Function

        Public ReadOnly Property All As IReadOnlyList(Of CameraHost)
            Get
                Return _hosts.Values.OrderBy(Function(h) h.Id, StringComparer.Ordinal).ToList()
            End Get
        End Property

        Public Function Find(id As String) As CameraHost
            Dim h As CameraHost = Nothing
            Return If(id IsNot Nothing AndAlso _hosts.TryGetValue(id, h), h, Nothing)
        End Function

        Public Async Function StartAllAsync(ct As Threading.CancellationToken) As Task
            For Each h As CameraHost In All
                Await h.StartAsync(ct).ConfigureAwait(False)
            Next
            Dim relays As List(Of CameraHost) = All.Where(Function(h) h.IsRelay).ToList()
            If _svc.Hpp IsNot Nothing AndAlso _svc.Options.Hpp.Events AndAlso relays.Count > 0 Then
                _pump = New Cloud.HppEventPump(_svc.Hpp, relays)
                _pump.Start()
            End If
        End Function

        ''' <summary>مضخة إنذارات السحابة (Nothing إن لم تكن هناك كاميرات hpp أو عُطّلت الأحداث).</summary>
        Public ReadOnly Property EventPump As Cloud.HppEventPump
            Get
                Return _pump
            End Get
        End Property

        ''' <summary>بالتوازي: إيقاف كاميرا معلّقة (محاولات إيقاف عبر السحابة) لا يؤخر إيقاف الأخرى حتى تقتل الخدمةُ العملية.</summary>
        Public Async Function StopAllAsync() As Task
            Dim pumpStop As Task = If(_pump Is Nothing, Task.CompletedTask, _pump.StopAsync())
            Await Task.WhenAll(All.Select(Function(h) h.StopAsync()).Append(pumpStop)).ConfigureAwait(False)
        End Function
    End Class

End Namespace
