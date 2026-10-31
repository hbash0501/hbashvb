Imports Ptz.Core.Models

Namespace Vision

    Public NotInheritable Class TemplateTrackResult
        Public ReadOnly Property Found As Boolean
        Public ReadOnly Property Box As NormRect
        ''' <summary>متوسط الفرق المطلق (MAD) للتطابق الأفضل؛ الأقل أفضل.</summary>
        Public ReadOnly Property Score As Double

        Public Sub New(found As Boolean, box As NormRect, score As Double)
            Me.Found = found
            Me.Box = box
            Me.Score = score
        End Sub
    End Class

    ''' <summary>
    ''' متتبّع قالب (Template matching بمقياس SAD) لملاحقة هدف أثناء حركة الكاميرا نفسها،
    ''' حيث يفشل فصل الخلفية. يعمل على إطارات مصغّرة.
    ''' </summary>
    Public NotInheritable Class TemplateTracker
        Private _template As Byte()
        Private _tw As Integer
        Private _th As Integer
        Private _cx As Double
        Private _cy As Double
        Private _fw As Integer
        Private _fh As Integer

        Public Property MaxMad As Double = 22.0
        Public Property MinTextureStdDev As Double = 4.0
        Public Property TemplateUpdateRate As Double = 0.12

        Public ReadOnly Property IsInitialized As Boolean
            Get
                Return _template IsNot Nothing
            End Get
        End Property

        Public Sub Clear()
            _template = Nothing
        End Sub

        ''' <summary>يُهيّئ القالب من الصندوق؛ يفشل إن كان الجسم بلا تفاصيل كافية.</summary>
        Public Function Initialize(frame As GrayFrame, box As NormRect) As Boolean
            _fw = frame.Width
            _fh = frame.Height
            Dim tw As Integer = Math.Max(6, Math.Min(64, CInt(box.Width * _fw)))
            Dim th As Integer = Math.Max(6, Math.Min(64, CInt(box.Height * _fh)))
            If tw >= _fw OrElse th >= _fh Then Return False
            Dim cx As Double = box.Center.X * _fw
            Dim cy As Double = box.Center.Y * _fh
            Dim x0 As Integer = CInt(Math.Round(cx - tw / 2.0))
            Dim y0 As Integer = CInt(Math.Round(cy - th / 2.0))
            If x0 < 0 OrElse y0 < 0 OrElse x0 + tw > _fw OrElse y0 + th > _fh Then Return False
            Dim t(tw * th - 1) As Byte
            Dim sum As Double = 0
            Dim sum2 As Double = 0
            For y As Integer = 0 To th - 1
                For x As Integer = 0 To tw - 1
                    Dim v As Byte = frame.Pixels((y0 + y) * _fw + x0 + x)
                    t(y * tw + x) = v
                    sum += v
                    sum2 += CDbl(v) * v
                Next
            Next
            Dim n As Double = tw * th
            Dim variance As Double = sum2 / n - (sum / n) ^ 2
            If Math.Sqrt(Math.Max(0.0, variance)) < MinTextureStdDev Then Return False
            _template = t
            _tw = tw
            _th = th
            _cx = x0 + tw / 2.0
            _cy = y0 + th / 2.0
            Return True
        End Function

        Public Function Update(frame As GrayFrame) As TemplateTrackResult
            If _template Is Nothing OrElse frame.Width <> _fw OrElse frame.Height <> _fh Then
                Return New TemplateTrackResult(False, New NormRect(), Double.MaxValue)
            End If
            Dim radius As Integer = Math.Max(10, Math.Max(_tw, _th))
            Dim baseX As Integer = CInt(Math.Round(_cx - _tw / 2.0))
            Dim baseY As Integer = CInt(Math.Round(_cy - _th / 2.0))
            Dim best As Double = Double.MaxValue
            Dim bx As Integer = baseX
            Dim by As Integer = baseY
            Dim n As Integer = _tw * _th
            Dim px As Byte() = frame.Pixels

            For dy As Integer = -radius To radius
                Dim y0 As Integer = baseY + dy
                If y0 < 0 OrElse y0 + _th > _fh Then Continue For
                For dx As Integer = -radius To radius
                    Dim x0 As Integer = baseX + dx
                    If x0 < 0 OrElse x0 + _tw > _fw Then Continue For
                    Dim sad As Integer = 0
                    Dim limit As Integer = If(best = Double.MaxValue, Integer.MaxValue, CInt(best * n) + 1)
                    For ty As Integer = 0 To _th - 1
                        Dim fRow As Integer = (y0 + ty) * _fw + x0
                        Dim tRow As Integer = ty * _tw
                        For tx As Integer = 0 To _tw - 1
                            sad += Math.Abs(CInt(px(fRow + tx)) - CInt(_template(tRow + tx)))
                        Next
                        If sad > limit Then Exit For
                    Next
                    Dim mad As Double = sad / CDbl(n)
                    If mad < best Then
                        best = mad
                        bx = x0
                        by = y0
                    End If
                Next
            Next

            If best > MaxMad Then Return New TemplateTrackResult(False, New NormRect(), best)

            If best < MaxMad * 0.5 Then
                For ty As Integer = 0 To _th - 1
                    For tx As Integer = 0 To _tw - 1
                        Dim i As Integer = ty * _tw + tx
                        Dim v As Double = (1.0 - TemplateUpdateRate) * _template(i) + TemplateUpdateRate * px((by + ty) * _fw + bx + tx)
                        _template(i) = CByte(Math.Min(255.0, Math.Max(0.0, v)))
                    Next
                Next
            End If
            _cx = bx + _tw / 2.0
            _cy = by + _th / 2.0
            Return New TemplateTrackResult(True, New NormRect(bx / CDbl(_fw), by / CDbl(_fh), _tw / CDbl(_fw), _th / CDbl(_fh)), best)
        End Function
    End Class

End Namespace
