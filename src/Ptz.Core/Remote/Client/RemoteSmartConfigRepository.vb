Imports System.Net.Http
Imports Ptz.Core.Abstractions

Namespace Remote

    ''' <summary>
    ''' إعداد الذكاء عن بُعد: البوابة هي المالك الأصلي. الكتابة مشروطة بـ If-Match (ETag) كي لا يطمس مشغّلٌ إعدادَ زميله
    ''' دون علمه؛ عند التعارض يُعاد تحميل الإصدار الأحدث ويُبلَّغ المستخدم ليراجع قبل الحفظ.
    ''' </summary>
    Public NotInheritable Class RemoteSmartConfigRepository
        Implements ISmartConfigRepository

        Private ReadOnly _client As GatewayClient
        Private ReadOnly _cameraId As String
        Private _etag As String

        Public Sub New(client As GatewayClient, cameraId As String)
            _client = client
            _cameraId = cameraId
        End Sub

        Private Function Base() As String
            Return "/api/v1/cameras/" & _cameraId & "/smart"
        End Function

        Public Async Function LoadAsync(ct As Threading.CancellationToken) As Task(Of SmartGeometry) Implements ISmartConfigRepository.LoadAsync
            Dim r As GatewayResponse = Await _client.RequestAsync(HttpMethod.Get, Base() & "/geometry", Nothing, ct).ConfigureAwait(False)
            _etag = r.Header("ETag")
            Return WireMap.ToGeometry(r.Json(Of GeometryDto)())
        End Function

        Public Async Function SaveGeometryAsync(geometry As SmartGeometry, ct As Threading.CancellationToken) As Task Implements ISmartConfigRepository.SaveGeometryAsync
            If _etag Is Nothing Then Await LoadAsync(ct).ConfigureAwait(False)
            Dim dto As GeometryDto = WireMap.FromGeometry(geometry)
            Dim res = Await _client.TryRequestAsync(HttpMethod.Put, Base() & "/geometry", dto, ct,
                                                    New Dictionary(Of String, String) From {{"If-Match", _etag}}).ConfigureAwait(False)
            If res.Error Is Nothing Then
                _etag = res.Response.Header("ETag")
                Return
            End If
            If res.Error.Status = 412 Then
                Await LoadAsync(ct).ConfigureAwait(False)          ' ننتقل لأحدث إصدار ثم نُبلغ
                Throw New InvalidOperationException("عدّل مشغّل آخر المناطق والخطوط أثناء عملك؛ حُمّلت النسخة الأحدث. راجعها ثم احفظ من جديد.")
            End If
            Throw res.Error.ToDomainException()
        End Function

        Public Async Function SaveAnomalyModelAsync(ct As Threading.CancellationToken) As Task Implements ISmartConfigRepository.SaveAnomalyModelAsync
            Await _client.RequestAsync(HttpMethod.Post, Base() & "/anomaly/save", Nothing, ct).ConfigureAwait(False)
        End Function
    End Class

End Namespace
