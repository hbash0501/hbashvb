Namespace Models

    ''' <summary>أمر حركة PTZ مستمر. القيم من -100 إلى 100 (الإشارة = الاتجاه).</summary>
    Public Structure PtzVector
        Public ReadOnly Pan As Integer
        Public ReadOnly Tilt As Integer
        Public ReadOnly Zoom As Integer

        Public Sub New(pan As Integer, tilt As Integer, zoom As Integer)
            Me.Pan = ClampSpeed(pan)
            Me.Tilt = ClampSpeed(tilt)
            Me.Zoom = ClampSpeed(zoom)
        End Sub

        Public Shared ReadOnly Property Zero As PtzVector = New PtzVector(0, 0, 0)

        Public ReadOnly Property IsZero As Boolean
            Get
                Return Pan = 0 AndAlso Tilt = 0 AndAlso Zoom = 0
            End Get
        End Property

        Private Shared Function ClampSpeed(v As Integer) As Integer
            If v < -100 Then Return -100
            If v > 100 Then Return 100
            Return v
        End Function

        Public Overrides Function ToString() As String
            Return $"pan={Pan} tilt={Tilt} zoom={Zoom}"
        End Function
    End Structure

    ''' <summary>موضع PTZ مطلق: السمت والارتفاع بالدرجات والتكبير كمضاعف.</summary>
    Public Structure PtzPosition
        Public ReadOnly Azimuth As Double
        Public ReadOnly Elevation As Double
        Public ReadOnly Zoom As Double

        Public Sub New(azimuth As Double, elevation As Double, zoom As Double)
            Me.Azimuth = azimuth
            Me.Elevation = elevation
            Me.Zoom = zoom
        End Sub

        Public Overrides Function ToString() As String
            Return $"az={Azimuth:F1}° el={Elevation:F1}° zoom={Zoom:F1}x"
        End Function
    End Structure

    Public NotInheritable Class PtzPreset
        Public Property Id As Integer
        Public Property Name As String = ""
    End Class

    ''' <summary>عناصر مساعدة قابلة للتشغيل (ممسحة، إضاءة).</summary>
    Public Enum AuxKind
        Wiper
        Light
    End Enum

    ''' <summary>اتجاه حركة العدسة (تركيز/فتحة): موجب أو سالب أو إيقاف.</summary>
    Public Enum LensDirection
        Halt = 0
        Near = -1
        Far = 1
    End Enum

End Namespace
