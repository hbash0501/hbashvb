Imports Ptz.Core.Models
Imports Ptz.Core.Vision

Namespace Abstractions

    ''' <summary>مصدر وقت قابل للاستبدال في الاختبارات.</summary>
    Public Interface IClock
        ReadOnly Property UtcNow As DateTime
    End Interface

    Public NotInheritable Class SystemClock
        Implements IClock
        Public Shared ReadOnly Property Instance As New SystemClock()
        Public ReadOnly Property UtcNow As DateTime Implements IClock.UtcNow
            Get
                Return DateTime.UtcNow
            End Get
        End Property
    End Class

    ''' <summary>الأوامر الحركية فقط — الواجهة التي يحتاجها التتبع الآلي.</summary>
    Public Interface IPtzMover
        Function MoveAsync(vector As PtzVector, ct As Threading.CancellationToken) As Task
        Function StopAsync(ct As Threading.CancellationToken) As Task
    End Interface

    ''' <summary>مصدر إطارات رمادية (لقطات ISAPI أو بث RTSP عبر SDK الشركة المصنّعة).</summary>
    Public Interface IFrameSource
        Function GetFrameAsync(ct As Threading.CancellationToken) As Task(Of GrayFrame)
    End Interface

    ''' <summary>يفك ترميز JPEG إلى إطار رمادي. التنفيذ يقع في طبقة المنصّة (System.Drawing في تطبيق Windows).</summary>
    Public Interface IFrameDecoder
        Function DecodeToGray(jpeg As Byte()) As GrayFrame
    End Interface

    ''' <summary>
    ''' واجهة التحكم الكاملة بكاميرا PTZ. التنفيذ الخام في <c>Cameras</c>، ويلفّه
    ''' <c>SecuredCameraController</c> لفرض الصلاحيات والتدقيق.
    ''' </summary>
    Public Interface ICameraController
        Inherits IPtzMover, IDisposable

        ReadOnly Property Profile As CameraProfile
        ''' <summary>الخصائص المدعومة فعلياً بعد الفحص (أو الافتراضية قبله).</summary>
        ReadOnly Property Features As CameraFeatures

        Function ConnectAsync(ct As Threading.CancellationToken) As Task(Of DeviceInfo)

        ' --- PTZ ---
        Function GetPositionAsync(ct As Threading.CancellationToken) As Task(Of PtzPosition)
        Function GoToPositionAsync(position As PtzPosition, ct As Threading.CancellationToken) As Task
        ''' <summary>تكبير على منطقة من الصورة (إحداثيات مُطبَّعة) — Position3D.</summary>
        Function ZoomToAreaAsync(area As NormRect, ct As Threading.CancellationToken) As Task
        Function GoHomeAsync(ct As Threading.CancellationToken) As Task
        Function SetHomeAsync(ct As Threading.CancellationToken) As Task

        ' --- المواضع المسبقة والدوريات والأنماط ---
        Function GetPresetsAsync(ct As Threading.CancellationToken) As Task(Of IReadOnlyList(Of PtzPreset))
        Function SavePresetAsync(id As Integer, name As String, ct As Threading.CancellationToken) As Task
        Function GoToPresetAsync(id As Integer, ct As Threading.CancellationToken) As Task
        Function DeletePresetAsync(id As Integer, ct As Threading.CancellationToken) As Task
        Function StartPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task
        Function StopPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task
        Function StartPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task
        Function StopPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task
        Function RunPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task
        Function StopPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task

        ' --- العدسة ---
        Function MoveFocusAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task
        Function MoveIrisAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task

        ' --- الصورة والإضاءة ---
        Function GetImageSettingsAsync(ct As Threading.CancellationToken) As Task(Of ImageSettings)
        Function ApplyImageSettingsAsync(settings As ImageSettings, ct As Threading.CancellationToken) As Task
        Function SetAuxAsync(kind As AuxKind, enabled As Boolean, ct As Threading.CancellationToken) As Task

        ' --- الفيديو والصيانة ---
        Function GetSnapshotAsync(ct As Threading.CancellationToken) As Task(Of Byte())
        Function RebootAsync(ct As Threading.CancellationToken) As Task
    End Interface

End Namespace
