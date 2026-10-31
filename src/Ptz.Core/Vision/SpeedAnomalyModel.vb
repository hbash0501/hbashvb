Imports System.Text.Json
Imports Ptz.Core.Models

Namespace Vision

    ''' <summary>
    ''' نموذج تعلّم آلي خفيف لاكتشاف السرعات الشاذة: يتعلّم توزيع السرعة "الطبيعية" لكل خلية من
    ''' شبكة المشهد (خوارزمية Welford المتدفقة)، ثم يرصد الانحرافات بمقياس z-score.
    ''' </summary>
    Public NotInheritable Class SpeedAnomalyModel
        Private ReadOnly _cols As Integer
        Private ReadOnly _rows As Integer
        Private ReadOnly _count As Integer()
        Private ReadOnly _mean As Double()
        Private ReadOnly _m2 As Double()
        Private ReadOnly _lock As New Object()

        Public Property MinSamples As Integer = 60
        Public Property ZThreshold As Double = 3.5
        ''' <summary>لا يُعتبر شاذاً ما كانت سرعته أقل من هذه القيمة (وحدات إطار/ث).</summary>
        Public Property MinAnomalySpeed As Double = 0.05
        ''' <summary>true = تعلّم، false = كشف فقط.</summary>
        Public Property Learning As Boolean = True

        Public Sub New(Optional cols As Integer = 8, Optional rows As Integer = 6)
            If cols < 1 OrElse rows < 1 Then Throw New ArgumentOutOfRangeException(NameOf(cols))
            _cols = cols
            _rows = rows
            _count = New Integer(cols * rows - 1) {}
            _mean = New Double(cols * rows - 1) {}
            _m2 = New Double(cols * rows - 1) {}
        End Sub

        Private Function CellOf(p As NormPoint) As Integer
            Dim cx As Integer = Math.Min(_cols - 1, Math.Max(0, CInt(Math.Floor(p.X * _cols))))
            Dim cy As Integer = Math.Min(_rows - 1, Math.Max(0, CInt(Math.Floor(p.Y * _rows))))
            Return cy * _cols + cx
        End Function

        Public Sub Observe(position As NormPoint, speed As Double)
            If Not Learning OrElse Double.IsNaN(speed) OrElse Double.IsInfinity(speed) Then Return
            Dim c As Integer = CellOf(position)
            SyncLock _lock
                _count(c) += 1
                Dim delta As Double = speed - _mean(c)
                _mean(c) += delta / _count(c)
                _m2(c) += delta * (speed - _mean(c))
            End SyncLock
        End Sub

        ''' <summary>درجة الشذوذ z؛ 0 إن لم تتوفر عيّنات كافية.</summary>
        Public Function Score(position As NormPoint, speed As Double) As Double
            Dim c As Integer = CellOf(position)
            SyncLock _lock
                If _count(c) < MinSamples Then Return 0.0
                Dim variance As Double = _m2(c) / (_count(c) - 1)
                Dim sd As Double = Math.Max(Math.Sqrt(variance), 0.02)
                Return (speed - _mean(c)) / sd
            End SyncLock
        End Function

        Public Function IsAnomalous(position As NormPoint, speed As Double) As Boolean
            If Learning OrElse speed < MinAnomalySpeed Then Return False
            Return Score(position, speed) > ZThreshold
        End Function

        Public Function SampleCount(position As NormPoint) As Integer
            SyncLock _lock
                Return _count(CellOf(position))
            End SyncLock
        End Function

        Private NotInheritable Class State
            Public Property Cols As Integer
            Public Property Rows As Integer
            Public Property Count As Integer()
            Public Property Mean As Double()
            Public Property M2 As Double()
        End Class

        Public Function ToJson() As String
            SyncLock _lock
                Return JsonSerializer.Serialize(New State With {.Cols = _cols, .Rows = _rows, .Count = _count, .Mean = _mean, .M2 = _m2})
            End SyncLock
        End Function

        ''' <summary>يستعيد حالة محفوظة؛ يرمي InvalidDataException إن لم تطابق الأبعاد.</summary>
        Public Sub LoadJson(json As String)
            Dim s As State = JsonSerializer.Deserialize(Of State)(json)
            If s Is Nothing OrElse s.Cols <> _cols OrElse s.Rows <> _rows OrElse s.Count Is Nothing OrElse
               s.Count.Length <> _count.Length OrElse s.Mean.Length <> _mean.Length OrElse s.M2.Length <> _m2.Length Then
                Throw New IO.InvalidDataException("حالة نموذج الشذوذ لا تطابق أبعاد الشبكة.")
            End If
            SyncLock _lock
                Array.Copy(s.Count, _count, _count.Length)
                Array.Copy(s.Mean, _mean, _mean.Length)
                Array.Copy(s.M2, _m2, _m2.Length)
            End SyncLock
        End Sub
    End Class

End Namespace
