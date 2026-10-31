Namespace Models

    ''' <summary>نقطة بإحداثيات مُطبَّعة (0..1) مستقلة عن دقة الصورة.</summary>
    Public Structure NormPoint
        Public ReadOnly X As Double
        Public ReadOnly Y As Double

        Public Sub New(x As Double, y As Double)
            Me.X = x
            Me.Y = y
        End Sub

        Public Function DistanceTo(other As NormPoint) As Double
            Dim dx As Double = X - other.X
            Dim dy As Double = Y - other.Y
            Return Math.Sqrt(dx * dx + dy * dy)
        End Function

        Public Overrides Function ToString() As String
            Return $"({X:F3},{Y:F3})"
        End Function
    End Structure

    ''' <summary>مستطيل بإحداثيات مُطبَّعة (0..1).</summary>
    Public Structure NormRect
        Public ReadOnly X As Double
        Public ReadOnly Y As Double
        Public ReadOnly Width As Double
        Public ReadOnly Height As Double

        Public Sub New(x As Double, y As Double, width As Double, height As Double)
            Me.X = x
            Me.Y = y
            Me.Width = width
            Me.Height = height
        End Sub

        Public ReadOnly Property Right As Double
            Get
                Return X + Width
            End Get
        End Property

        Public ReadOnly Property Bottom As Double
            Get
                Return Y + Height
            End Get
        End Property

        Public ReadOnly Property Center As NormPoint
            Get
                Return New NormPoint(X + Width / 2.0, Y + Height / 2.0)
            End Get
        End Property

        Public ReadOnly Property Area As Double
            Get
                Return Width * Height
            End Get
        End Property

        Public Function Contains(p As NormPoint) As Boolean
            Return p.X >= X AndAlso p.X <= Right AndAlso p.Y >= Y AndAlso p.Y <= Bottom
        End Function

        ''' <summary>أصغر مستطيل يحوي المستطيلين.</summary>
        Public Function Union(other As NormRect) As NormRect
            Dim l As Double = Math.Min(X, other.X)
            Dim t As Double = Math.Min(Y, other.Y)
            Dim r As Double = Math.Max(Right, other.Right)
            Dim b As Double = Math.Max(Bottom, other.Bottom)
            Return New NormRect(l, t, r - l, b - t)
        End Function

        ''' <summary>الفجوة الأفقية/الرأسية الدنيا بين مستطيلين (0 إذا تقاطعا).</summary>
        Public Function GapTo(other As NormRect) As Double
            Dim gx As Double = Math.Max(0.0, Math.Max(other.X - Right, X - other.Right))
            Dim gy As Double = Math.Max(0.0, Math.Max(other.Y - Bottom, Y - other.Bottom))
            Return Math.Sqrt(gx * gx + gy * gy)
        End Function
    End Structure

    ''' <summary>مضلّع منطقة أو خط عبور بإحداثيات مُطبَّعة.</summary>
    Public NotInheritable Class Geometry
        Private Sub New()
        End Sub

        ''' <summary>اختبار انتماء نقطة لمضلّع (Ray casting).</summary>
        Public Shared Function PointInPolygon(p As NormPoint, polygon As IReadOnlyList(Of NormPoint)) As Boolean
            If polygon Is Nothing OrElse polygon.Count < 3 Then Return False
            Dim inside As Boolean = False
            Dim j As Integer = polygon.Count - 1
            For i As Integer = 0 To polygon.Count - 1
                Dim pi As NormPoint = polygon(i)
                Dim pj As NormPoint = polygon(j)
                If ((pi.Y > p.Y) <> (pj.Y > p.Y)) AndAlso
                   (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X) Then
                    inside = Not inside
                End If
                j = i
            Next
            Return inside
        End Function

        ''' <summary>إشارة الجانب: موجبة يسار الخط a→b، سالبة يمينه، صفر على الخط.</summary>
        Public Shared Function Side(a As NormPoint, b As NormPoint, p As NormPoint) As Double
            Return (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X)
        End Function

        ''' <summary>هل يتقاطع القطعتان (p1→p2) و(a→b)؟</summary>
        Public Shared Function SegmentsIntersect(p1 As NormPoint, p2 As NormPoint, a As NormPoint, b As NormPoint) As Boolean
            Dim d1 As Double = Side(a, b, p1)
            Dim d2 As Double = Side(a, b, p2)
            Dim d3 As Double = Side(p1, p2, a)
            Dim d4 As Double = Side(p1, p2, b)
            Return ((d1 > 0) <> (d2 > 0)) AndAlso ((d3 > 0) <> (d4 > 0)) AndAlso d1 <> 0 AndAlso d2 <> 0
        End Function

        Public Shared Function Clamp(value As Double, lo As Double, hi As Double) As Double
            If value < lo Then Return lo
            If value > hi Then Return hi
            Return value
        End Function
    End Class

End Namespace
