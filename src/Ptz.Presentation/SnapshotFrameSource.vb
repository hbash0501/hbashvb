Imports Ptz.Core.Abstractions
Imports Ptz.Core.Vision

''' <summary>
''' مصدر إطارات يعتمد لقطات ISAPI الرسمية: يجلب JPEG، يبثّه للعرض، ثم يفك ترميزه إلى تدرج رمادي للتحليل.
''' (بث RTSP عالي المعدل يُضاف بتنفيذ آخر لـ IFrameSource عبر SDK الشركة المصنّعة.)
''' </summary>
Public NotInheritable Class SnapshotFrameSource
    Implements IFrameSource, IJpegFeed

    Private ReadOnly _camera As ICameraController
    Private ReadOnly _decoder As IFrameDecoder
    Private ReadOnly _clock As IClock

    Public Event FrameCaptured As Action(Of Byte()) Implements IJpegFeed.FrameCaptured

    Public Sub New(camera As ICameraController, decoder As IFrameDecoder, Optional clock As IClock = Nothing)
        _camera = camera
        _decoder = decoder
        _clock = If(clock, SystemClock.Instance)
    End Sub

    Public Async Function GetFrameAsync(ct As Threading.CancellationToken) As Task(Of GrayFrame) Implements IFrameSource.GetFrameAsync
        Dim jpeg As Byte() = Await _camera.GetSnapshotAsync(ct).ConfigureAwait(False)
        RaiseEvent FrameCaptured(jpeg)
        Dim gray As GrayFrame = _decoder.DecodeToGray(jpeg)
        ' الطابع الزمني من ساعة النظام لا من الصورة، ليتسق مع Suspend/Settle.
        Return New GrayFrame(gray.Width, gray.Height, gray.Pixels, _clock.UtcNow)
    End Function
End Class
