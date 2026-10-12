Imports Ptz.Core.Models

Namespace Vision

    Public NotInheritable Class AutoTrackOptions
        ''' <summary>منطقة ميتة حول مركز الصورة (مُطبَّعة) لا تُحرَّك الكاميرا داخلها.</summary>
        Public Property DeadBand As Double = 0.06
        Public Property MinSpeed As Integer = 8
        Public Property MaxSpeed As Integer = 60
        Public Property ZoomEnabled As Boolean = True
        ''' <summary>الارتفاع المرغوب لصندوق الهدف (نسبة من الإطار) — خارج هذا المجال يتغيّر التكبير.</summary>
        Public Property DesiredHeightMin As Double = 0.14
        Public Property DesiredHeightMax As Double = 0.45
        Public Property ZoomSpeed As Integer = 15
        ''' <summary>معامل تنعيم أسّي للأوامر (0..1، الأكبر = أسرع استجابة).</summary>
        Public Property Smoothing As Double = 0.6
        Public Property LostTimeoutSeconds As Double = 2.0
    End Class

    Public Enum FollowStatus
        Idle
        Following
        Lost
    End Enum

    Public NotInheritable Class FollowCommand
        Public ReadOnly Property Status As FollowStatus
        Public ReadOnly Property Vector As PtzVector
        Public ReadOnly Property TargetBox As NormRect

        Public Sub New(status As FollowStatus, vector As PtzVector, targetBox As NormRect)
            Me.Status = status
            Me.Vector = vector
            Me.TargetBox = targetBox
        End Sub
    End Class

    ''' <summary>
    ''' التتبع الآلي: يقفل على هدف (يُختار تلقائياً أو يدوياً) ويلاحقه بمتتبّع قالب أثناء حركة الكاميرا،
    ''' ويحوّل خطأ الموضع إلى أوامر PTZ متناسبة مع منطقة ميتة وتنعيم وتحكم تكبير.
    ''' </summary>
    Public NotInheritable Class AutoTracker
        Private ReadOnly _opt As AutoTrackOptions
        Private ReadOnly _template As New TemplateTracker()
        Private _lastBox As NormRect
        Private _lastSeenUtc As DateTime
        Private _smoothed As PtzVector = PtzVector.Zero
        Private _status As FollowStatus = FollowStatus.Idle

        Public Sub New(Optional options As AutoTrackOptions = Nothing)
            _opt = If(options, New AutoTrackOptions())
        End Sub

        Public ReadOnly Property Status As FollowStatus
            Get
                Return _status
            End Get
        End Property

        ''' <summary>يقفل على الصندوق. يعيد False إن كان الهدف بلا تفاصيل كافية للمطابقة.</summary>
        Public Function Lock(frame As GrayFrame, box As NormRect, nowUtc As DateTime) As Boolean
            If Not _template.Initialize(frame, box) Then
                _status = FollowStatus.Idle
                Return False
            End If
            _lastBox = box
            _lastSeenUtc = nowUtc
            _smoothed = PtzVector.Zero
            _status = FollowStatus.Following
            Return True
        End Function

        Public Sub Release()
            _template.Clear()
            _smoothed = PtzVector.Zero
            _status = FollowStatus.Idle
        End Sub

        ''' <summary>يعالج إطاراً جديداً ويعيد الأمر المطلوب. الإطار يجب أن يكون بنفس دقة إطار القفل.</summary>
        Public Function Update(frame As GrayFrame, nowUtc As DateTime) As FollowCommand
            If _status <> FollowStatus.Following Then Return New FollowCommand(_status, PtzVector.Zero, _lastBox)
            Dim r As TemplateTrackResult = _template.Update(frame)
            If r.Found Then
                _lastBox = r.Box
                _lastSeenUtc = nowUtc
                Dim target As PtzVector = ComputeVector(r.Box)
                _smoothed = Smooth(_smoothed, target)
                Return New FollowCommand(FollowStatus.Following, _smoothed, r.Box)
            End If
            If (nowUtc - _lastSeenUtc).TotalSeconds > _opt.LostTimeoutSeconds Then
                Release()
                _status = FollowStatus.Lost
                Return New FollowCommand(FollowStatus.Lost, PtzVector.Zero, _lastBox)
            End If
            ' فقدان قصير: نوقف الحركة ونواصل المحاولة.
            _smoothed = PtzVector.Zero
            Return New FollowCommand(FollowStatus.Following, PtzVector.Zero, _lastBox)
        End Function

        ''' <summary>تحويل موضع/حجم الهدف إلى أمر PTZ (دالة نقية قابلة للاختبار).</summary>
        Public Function ComputeVector(box As NormRect) As PtzVector
            Dim c As NormPoint = box.Center
            Dim ex As Double = c.X - 0.5
            Dim ey As Double = c.Y - 0.5
            Dim pan As Integer = AxisSpeed(ex)
            Dim tilt As Integer = -AxisSpeed(ey) ' الهدف أعلى المركز (ey<0) → إمالة للأعلى (موجب)
            Dim zoom As Integer = 0
            If _opt.ZoomEnabled AndAlso Math.Abs(ex) < _opt.DeadBand * 2 AndAlso Math.Abs(ey) < _opt.DeadBand * 2 Then
                If box.Height < _opt.DesiredHeightMin Then
                    zoom = _opt.ZoomSpeed
                ElseIf box.Height > _opt.DesiredHeightMax Then
                    zoom = -_opt.ZoomSpeed
                End If
            End If
            Return New PtzVector(pan, tilt, zoom)
        End Function

        Private Function AxisSpeed(err As Double) As Integer
            Dim a As Double = Math.Abs(err)
            If a <= _opt.DeadBand Then Return 0
            Dim span As Double = Math.Max(0.01, 0.5 - _opt.DeadBand)
            Dim k As Double = Math.Min(1.0, (a - _opt.DeadBand) / span)
            Dim s As Integer = CInt(Math.Round(_opt.MinSpeed + (_opt.MaxSpeed - _opt.MinSpeed) * k))
            Return If(err < 0, -s, s)
        End Function

        ''' <summary>تنعيم أسّي مع إنهاء القيم الصغيرة جداً إلى صفر كي لا تُرسل سرعات تافهة مستمرة.</summary>
        Private Function SmoothAxis(prev As Integer, target As Integer, a As Double) As Integer
            Dim v As Integer = CInt(Math.Round(prev + a * (target - prev)))
            If target = 0 AndAlso Math.Abs(v) < _opt.MinSpeed \ 2 Then Return 0
            Return v
        End Function

        Private Function Smooth(prev As PtzVector, target As PtzVector) As PtzVector
            Dim a As Double = Geometry.Clamp(_opt.Smoothing, 0.05, 1.0)
            Return New PtzVector(SmoothAxis(prev.Pan, target.Pan, a), SmoothAxis(prev.Tilt, target.Tilt, a), target.Zoom)
        End Function
    End Class

End Namespace
