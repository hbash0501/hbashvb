Namespace Vision

    Public NotInheritable Class BackgroundSubtractorOptions
        ''' <summary>معدل تعلّم الخلفية للبكسلات الخلفية.</summary>
        Public Property LearningRate As Single = 0.02F
        ''' <summary>معدل التعلّم للبكسلات الأمامية (بطيء، ليمتص الأجسام الساكنة طويلاً تدريجياً).</summary>
        Public Property ForegroundLearningRate As Single = 0.002F
        ''' <summary>عتبة الانحراف بعدد الانحرافات المعيارية. أقل = حساسية أعلى.</summary>
        Public Property ThresholdSigma As Single = 3.0F
        Public Property MinSigma As Single = 6.0F
        Public Property InitialSigma As Single = 12.0F
        Public Property WarmupFrames As Integer = 8
        ''' <summary>إن تجاوزت نسبة الأمامية هذه القيمة تُعتبر تغيّر إضاءة شامل فتُعاد تهيئة الخلفية.</summary>
        Public Property MaxForegroundRatio As Double = 0.5
    End Class

    Public NotInheritable Class SubtractionResult
        Public ReadOnly Property Mask As Byte()
        Public ReadOnly Property ForegroundRatio As Double
        Public ReadOnly Property IlluminationChange As Boolean
        Public ReadOnly Property WarmingUp As Boolean

        Public Sub New(mask As Byte(), ratio As Double, illumination As Boolean, warming As Boolean)
            Me.Mask = mask
            Me.ForegroundRatio = ratio
            Me.IlluminationChange = illumination
            Me.WarmingUp = warming
        End Sub
    End Class

    ''' <summary>
    ''' فصل الخلفية بنموذج غاوسي متكيّف لكل بكسل (متوسط + تباين متحرك).
    ''' يعالج: تغيّر الإضاءة العام، الضجيج، امتصاص الأجسام الساكنة تدريجياً.
    ''' </summary>
    Public NotInheritable Class BackgroundSubtractor
        Private ReadOnly _opt As BackgroundSubtractorOptions
        Private _mean As Single()
        Private _var As Single()
        Private _width As Integer
        Private _height As Integer
        Private _frames As Integer

        Public Sub New(Optional options As BackgroundSubtractorOptions = Nothing)
            _opt = If(options, New BackgroundSubtractorOptions())
        End Sub

        Public ReadOnly Property IsReady As Boolean
            Get
                Return _frames >= _opt.WarmupFrames
            End Get
        End Property

        Public Sub Reset()
            _mean = Nothing
            _var = Nothing
            _frames = 0
        End Sub

        Public Function Process(frame As GrayFrame) As SubtractionResult
            If frame Is Nothing Then Throw New ArgumentNullException(NameOf(frame))
            Dim n As Integer = frame.Width * frame.Height
            If _mean Is Nothing OrElse frame.Width <> _width OrElse frame.Height <> _height Then
                Seed(frame)
                Return New SubtractionResult(New Byte(n - 1) {}, 0.0, False, True)
            End If

            Dim px As Byte() = frame.Pixels
            Dim mask(n - 1) As Byte
            Dim fg As Integer = 0
            Dim minVar As Single = _opt.MinSigma * _opt.MinSigma
            Dim k2 As Single = _opt.ThresholdSigma * _opt.ThresholdSigma
            For i As Integer = 0 To n - 1
                Dim d As Single = px(i) - _mean(i)
                Dim v As Single = Math.Max(_var(i), minVar)
                If d * d > k2 * v Then
                    mask(i) = 255
                    fg += 1
                End If
            Next

            Dim ratio As Double = fg / CDbl(n)
            If _frames >= _opt.WarmupFrames AndAlso ratio > _opt.MaxForegroundRatio Then
                ' تغيّر إضاءة/حركة كاميرا: لا نُطلق إنذاراً بل نعيد التعلّم.
                Seed(frame)
                Return New SubtractionResult(New Byte(n - 1) {}, ratio, True, True)
            End If

            Dim a As Single = _opt.LearningRate
            Dim af As Single = _opt.ForegroundLearningRate
            If _frames < _opt.WarmupFrames Then a = Math.Max(a, 1.0F / (_frames + 2)) ' تعلّم أسرع في الإحماء
            For i As Integer = 0 To n - 1
                Dim rate As Single = If(mask(i) = 0, a, If(_frames < _opt.WarmupFrames, a, af))
                Dim d As Single = px(i) - _mean(i)
                _mean(i) += rate * d
                _var(i) += rate * (d * d - _var(i))
            Next
            _frames += 1
            Dim warming As Boolean = _frames <= _opt.WarmupFrames
            If warming Then Array.Clear(mask, 0, mask.Length)
            Return New SubtractionResult(mask, If(warming, 0.0, ratio), False, warming)
        End Function

        Private Sub Seed(frame As GrayFrame)
            _width = frame.Width
            _height = frame.Height
            Dim n As Integer = _width * _height
            ReDim _mean(n - 1)
            ReDim _var(n - 1)
            Dim v0 As Single = _opt.InitialSigma * _opt.InitialSigma
            For i As Integer = 0 To n - 1
                _mean(i) = frame.Pixels(i)
                _var(i) = v0
            Next
            _frames = 0
        End Sub
    End Class

End Namespace
