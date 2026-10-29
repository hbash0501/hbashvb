Imports Ptz.Presentation
Imports Ptz.Core.Models
Imports Ptz.Gateway.Hosting

Namespace Cameras

    ''' <summary>مجموعة كاميرات البوابة (من cameras.json). الإدخال بلا عنوان لا يُشغَّل.</summary>
    Public NotInheritable Class CameraRegistry
        Private ReadOnly _svc As GatewayServices
        Private ReadOnly _hosts As New Dictionary(Of String, CameraHost)(StringComparer.Ordinal)

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
        End Function

        Public Async Function StopAllAsync() As Task
            For Each h As CameraHost In All
                Await h.StopAsync().ConfigureAwait(False)
            Next
        End Function
    End Class

End Namespace
