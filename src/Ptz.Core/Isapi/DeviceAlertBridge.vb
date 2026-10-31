Imports Ptz.Core.Alarms
Imports Ptz.Core.Models

Namespace Isapi

    ''' <summary>يستورد أحداث الكاميرا الذكية الأصلية (VMD/خط/اقتحام...) إلى ناقل الإنذارات. يُشغَّل محلياً أو داخل البوابة.</summary>
    Public NotInheritable Class DeviceAlertBridge
        Private Sub New()
        End Sub

        Public Shared Async Function RunAsync(connection As CameraConnection, handler As Net.Http.HttpMessageHandler, cameraName As String,
                                              bus As AlarmBus, ct As Threading.CancellationToken) As Task
            ' المعالج المُمرَّر مشترك مع المتحكم (وقاطع الحماية)، فلا يجوز أن يتخلص منه عميل التنبيهات.
            Using client As New IsapiClient(connection, handler, ownsHandler:=False)
                Dim stream As New AlertStreamClient(client)
                Await stream.RunAsync(
                    Sub(ev)
                        bus.Publish(New AlarmEvent With {
                            .Camera = cameraName,
                            .Kind = "Device:" & ev.EventType,
                            .Severity = AlarmSeverity.Warning,
                            .Message = $"حدث من الكاميرا: {ev.EventType} {ev.Description}".Trim()})
                    End Sub, Nothing, ct).ConfigureAwait(False)
            End Using
        End Function
    End Class

End Namespace
