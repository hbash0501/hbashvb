Imports Ptz.Core.Models
Imports Ptz.Core.Vision

Namespace Abstractions

    ''' <summary>مصدر لقطات JPEG للعرض: محلي (لقطات ISAPI) أو بعيد (البوابة).</summary>
    Public Interface IJpegFeed
        Event FrameCaptured As Action(Of Byte())
    End Interface

    ''' <summary>الهندسة الفعّالة لكاميرا: مناطق حظر، خطوط عبور، مناطق إخفاء (إحداثيات مُطبَّعة). غير قابلة للتعديل.</summary>
    Public NotInheritable Class SmartGeometry
        Public ReadOnly Property Zones As IReadOnlyList(Of RestrictedZone)
        Public ReadOnly Property Lines As IReadOnlyList(Of TripLine)
        Public ReadOnly Property Masks As IReadOnlyList(Of IReadOnlyList(Of NormPoint))

        Public Sub New(zones As IEnumerable(Of RestrictedZone), lines As IEnumerable(Of TripLine),
                       masks As IEnumerable(Of IReadOnlyList(Of NormPoint)))
            Me.Zones = zones.ToList()
            Me.Lines = lines.ToList()
            Me.Masks = masks.ToList()
        End Sub

        Public Shared ReadOnly Property Empty As New SmartGeometry(New RestrictedZone() {}, New TripLine() {}, New IReadOnlyList(Of NormPoint)() {})
    End Class

    ''' <summary>
    ''' المخزن الدائم لإعداد الذكاء لكاميرا واحدة (الهندسة + نموذج الشذوذ المتعلَّم) مع تطبيقه على المحرك.
    ''' محلياً: ملفات JSON على جهاز المشغّل. عن بُعد: بوابة LAN هي المالك الأصلي والمحرك يعمل فيها على مدار الساعة.
    ''' </summary>
    Public Interface ISmartConfigRepository
        ''' <summary>يحمّل المحفوظ ويطبّقه على المحرك، ويعيد الهندسة الفعّالة لعرضها.</summary>
        Function LoadAsync(ct As Threading.CancellationToken) As Task(Of SmartGeometry)
        ''' <summary>يطبّق الهندسة على المحرك ويحفظها (ذرّياً في الجهة المالكة).</summary>
        Function SaveGeometryAsync(geometry As SmartGeometry, ct As Threading.CancellationToken) As Task
        ''' <summary>يحفظ نموذج الشذوذ المتعلَّم حالياً في المحرك.</summary>
        Function SaveAnomalyModelAsync(ct As Threading.CancellationToken) As Task
    End Interface

End Namespace
