Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Abstractions

Namespace Cameras

    ''' <summary>
    ''' قاطع دائرة لطلبات الكاميرا: بعد إخفاقَي مصادقة (401 نهائيين) متتاليين يُفتح القاطع 5 دقائق فيُجاب بـ 503 اصطناعي دون
    ''' لمس الكاميرا، كي لا تقفل الكاميرا حساب الخدمة (قفل بعد محاولات فاشلة) ولا نُغرقها بطلبات فاشلة.
    ''' </summary>
    Public NotInheritable Class CameraGuardHandler
        Inherits DelegatingHandler

        Public Shared ReadOnly OpenDuration As TimeSpan = TimeSpan.FromMinutes(5)
        Private ReadOnly _clock As IClock
        Private ReadOnly _lock As New Object()
        Private _consecutive401 As Integer
        Private _openUntil As DateTime = DateTime.MinValue
        Private _probing As Boolean

        Public Event BreakerOpened As Action

        Public Sub New(inner As HttpMessageHandler, clock As IClock)
            MyBase.New(inner)
            _clock = clock
        End Sub

        Public ReadOnly Property IsOpen As Boolean
            Get
                SyncLock _lock
                    Return _openUntil > _clock.UtcNow
                End SyncLock
            End Get
        End Property

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
            Dim blocked As Boolean = False
            SyncLock _lock
                Dim now As DateTime = _clock.UtcNow
                If _openUntil > now Then
                    blocked = True
                ElseIf _openUntil <> DateTime.MinValue Then
                    If _probing Then
                        blocked = True              ' نصف مفتوح: مسبار واحد فقط
                    Else
                        _probing = True
                    End If
                End If
            End SyncLock
            If blocked Then Return New HttpResponseMessage(HttpStatusCode.ServiceUnavailable)

            Dim resp As HttpResponseMessage
            Try
                resp = Await MyBase.SendAsync(request, cancellationToken).ConfigureAwait(False)
            Catch
                ' فشل نقلي (انقطاع/مهلة/إلغاء): لا نترك علم المسبار مرفوعاً وإلا بقي القاطع مغلقاً حتى إعادة التشغيل.
                SyncLock _lock
                    _probing = False
                End SyncLock
                Throw
            End Try
            Dim opened As Boolean = False
            SyncLock _lock
                _probing = False
                If resp.StatusCode = HttpStatusCode.Unauthorized Then
                    _consecutive401 += 1
                    If _consecutive401 >= 2 Then
                        _openUntil = _clock.UtcNow + OpenDuration
                        _consecutive401 = 0
                        opened = True
                    End If
                Else
                    _consecutive401 = 0
                    _openUntil = DateTime.MinValue
                End If
            End SyncLock
            If opened Then RaiseEvent BreakerOpened()
            Return resp
        End Function
    End Class

End Namespace
