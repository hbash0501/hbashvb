Imports System.Text.Json
Imports Ptz.Core.Models

Namespace Vision

    ''' <summary>خريطة حرارية لكثافة الحركة (شبكة عدّادات مع تضاؤل اختياري).</summary>
    Public NotInheritable Class HeatMap
        Private ReadOnly _cols As Integer
        Private ReadOnly _rows As Integer
        Private ReadOnly _cells As Double()
        Private ReadOnly _lock As New Object()

        Public Sub New(Optional cols As Integer = 32, Optional rows As Integer = 18)
            _cols = cols
            _rows = rows
            _cells = New Double(cols * rows - 1) {}
        End Sub

        Public ReadOnly Property Columns As Integer
            Get
                Return _cols
            End Get
        End Property

        Public ReadOnly Property Rows As Integer
            Get
                Return _rows
            End Get
        End Property

        Public Sub Add(p As NormPoint)
            Dim cx As Integer = Math.Min(_cols - 1, Math.Max(0, CInt(Math.Floor(p.X * _cols))))
            Dim cy As Integer = Math.Min(_rows - 1, Math.Max(0, CInt(Math.Floor(p.Y * _rows))))
            SyncLock _lock
                _cells(cy * _cols + cx) += 1.0
            End SyncLock
        End Sub

        Public Sub Decay(factor As Double)
            SyncLock _lock
                For i As Integer = 0 To _cells.Length - 1
                    _cells(i) *= factor
                Next
            End SyncLock
        End Sub

        Public Function Snapshot() As Double()
            SyncLock _lock
                Return CType(_cells.Clone(), Double())
            End SyncLock
        End Function
    End Class

    ''' <summary>
    ''' يسجّل مسارات الحركة المنتهية بصيغة JSONL (ملف يومي) لاستخدامها لاحقاً في التحليلات الأمنية.
    ''' آمن للاستخدام من عدة خيوط.
    ''' </summary>
    Public NotInheritable Class TrajectoryRecorder
        Private ReadOnly _directory As String
        Private ReadOnly _camera As String
        Private ReadOnly _lock As New Object()
        Private ReadOnly _maxPoints As Integer

        Public Sub New(directory As String, camera As String, Optional maxPointsPerRecord As Integer = 60)
            If String.IsNullOrWhiteSpace(directory) Then Throw New ArgumentException("مجلد التسجيل مطلوب.", NameOf(directory))
            _directory = directory
            _camera = camera
            _maxPoints = Math.Max(2, maxPointsPerRecord)
            IO.Directory.CreateDirectory(directory)
        End Sub

        ''' <summary>يبني سجلاً موجزاً للمسار (نقاط مُخفّضة العدد).</summary>
        Public Function BuildRecord(t As Track) As Dictionary(Of String, Object)
            Dim pts As New List(Of Double())()
            Dim n As Integer = t.Trajectory.Count
            Dim step_ As Double = Math.Max(1.0, n / CDbl(_maxPoints))
            Dim i As Double = 0
            While CInt(i) < n
                Dim tp As TrajectoryPoint = t.Trajectory(CInt(i))
                pts.Add({Math.Round(tp.Position.X, 4), Math.Round(tp.Position.Y, 4), Math.Round((tp.TimeUtc - t.FirstSeenUtc).TotalSeconds, 2)})
                i += step_
            End While
            Dim dur As Double = t.AgeSeconds
            Return New Dictionary(Of String, Object) From {
                {"camera", _camera},
                {"trackId", t.Id},
                {"startUtc", t.FirstSeenUtc.ToString("o", Globalization.CultureInfo.InvariantCulture)},
                {"endUtc", t.LastSeenUtc.ToString("o", Globalization.CultureInfo.InvariantCulture)},
                {"durationSec", Math.Round(dur, 2)},
                {"pathLength", Math.Round(t.PathLength, 4)},
                {"avgSpeed", Math.Round(If(dur > 0, t.PathLength / dur, 0.0), 4)},
                {"maxSpeed", Math.Round(t.MaxSpeed, 4)},
                {"points", pts}
            }
        End Function

        Public Sub Record(t As Track)
            Dim line As String = JsonSerializer.Serialize(BuildRecord(t))
            Dim path As String = IO.Path.Combine(_directory, $"trajectories-{t.LastSeenUtc:yyyyMMdd}.jsonl")
            SyncLock _lock
                IO.File.AppendAllText(path, line & Environment.NewLine, Text.Encoding.UTF8)
            End SyncLock
        End Sub
    End Class

End Namespace
