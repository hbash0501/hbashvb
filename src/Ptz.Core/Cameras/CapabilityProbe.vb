Imports Ptz.Core.Isapi
Imports Ptz.Core.Models

Namespace Cameras

    ''' <summary>
    ''' يفحص الجهاز فعلياً لتحديد الميزات المدعومة، فيصحّح افتراضات الملف التعريفي حسب
    ''' الإصدار البرمجي الثابت (404/403/400/501 = غير مدعوم؛ 401 = خطأ مصادقة يُرمى).
    ''' </summary>
    Public NotInheritable Class CapabilityProbe
        Private Sub New()
        End Sub

        Public Shared Async Function ProbeAsync(client As IsapiClient, profile As CameraProfile, ct As Threading.CancellationToken) As Task(Of CameraFeatures)
            Dim features As CameraFeatures = profile.DefaultFeatures
            Dim ch As Integer = profile.ControlChannel

            Dim probes As New List(Of (Feature As CameraFeatures, Path As String)) From {
                (CameraFeatures.Ptz, $"/ISAPI/PTZCtrl/channels/{ch}/capabilities"),
                (CameraFeatures.Presets, IsapiPaths.Presets(ch)),
                (CameraFeatures.Snapshot, IsapiPaths.Snapshot(profile.StreamChannel))
            }
            ' ميزات الصورة: كل ميزة تُحذف فقط إن لم تعمل أيٌّ من نقاط نهايتها.
            Dim imageOk As New Dictionary(Of CameraFeatures, Boolean)()
            For Each spec As ImageParamSpec In ImageParamCatalog.All
                If (profile.DefaultFeatures And spec.Feature) <> spec.Feature Then Continue For
                Dim supported As Boolean = Await IsSupportedAsync(client, IsapiPaths.Image(spec.EndpointTemplate, ch), ct).ConfigureAwait(False)
                Dim prev As Boolean = False
                imageOk.TryGetValue(spec.Feature, prev)
                imageOk(spec.Feature) = prev OrElse supported
            Next
            For Each kv In imageOk
                If Not kv.Value Then features = features And Not kv.Key
            Next

            For Each p In probes
                ' لا نجلب اللقطة كاملة هنا؛ الـ Snapshot يُفترض مدعوماً ويظهر خطؤه عند الاستخدام.
                If p.Feature = CameraFeatures.Snapshot Then Continue For
                If Not Await IsSupportedAsync(client, p.Path, ct).ConfigureAwait(False) Then features = features And Not p.Feature
            Next
            Return features
        End Function

        Private Shared Async Function IsSupportedAsync(client As IsapiClient, path As String, ct As Threading.CancellationToken) As Task(Of Boolean)
            Try
                Await client.GetXmlAsync(path, ct).ConfigureAwait(False)
                Return True
            Catch ex As IsapiException When ex.HttpStatus = 404 OrElse ex.HttpStatus = 403 OrElse ex.HttpStatus = 400 OrElse ex.HttpStatus = 405 OrElse ex.HttpStatus = 501
                Return False
            End Try
        End Function
    End Class

End Namespace
