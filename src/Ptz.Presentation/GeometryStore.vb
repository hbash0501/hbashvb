Imports System.Text.Json
Imports Ptz.Core.Models
Imports Ptz.Core.Vision

''' <summary>يحفظ مناطق الحظر وخطوط العبور ومناطق الإخفاء لكل كاميرا في ملف JSON.</summary>
Public NotInheritable Class GeometryStore
    Public NotInheritable Class PointDto
        Public Property X As Double
        Public Property Y As Double
    End Class

    Public NotInheritable Class ZoneDto
        Public Property Name As String = ""
        Public Property Severity As Integer
        Public Property Points As List(Of PointDto) = New List(Of PointDto)()
    End Class

    Public NotInheritable Class LineDto
        Public Property Name As String = ""
        Public Property Direction As Integer
        Public Property A As New PointDto()
        Public Property B As New PointDto()
    End Class

    Public NotInheritable Class Model
        Public Property Zones As List(Of ZoneDto) = New List(Of ZoneDto)()
        Public Property Lines As List(Of LineDto) = New List(Of LineDto)()
        Public Property Masks As List(Of List(Of PointDto)) = New List(Of List(Of PointDto))()
    End Class

    Private Shared Function ToDto(p As NormPoint) As PointDto
        Return New PointDto With {.X = p.X, .Y = p.Y}
    End Function

    Private Shared Function Clamp01(v As Double) As Double
        If Double.IsNaN(v) Then Return 0.0
        Return Math.Min(1.0, Math.Max(0.0, v))
    End Function

    Private Shared Function FromDto(d As PointDto) As NormPoint
        Return New NormPoint(Clamp01(d.X), Clamp01(d.Y))
    End Function

    Public Shared Sub Save(path As String, zones As IEnumerable(Of RestrictedZone), lines As IEnumerable(Of TripLine),
                           masks As IEnumerable(Of IReadOnlyList(Of NormPoint)))
        Dim m As New Model()
        For Each z As RestrictedZone In zones
            m.Zones.Add(New ZoneDto With {.Name = z.Name, .Severity = CInt(z.Severity), .Points = z.Points.Select(AddressOf ToDto).ToList()})
        Next
        For Each l As TripLine In lines
            m.Lines.Add(New LineDto With {.Name = l.Name, .Direction = CInt(l.Direction), .A = ToDto(l.A), .B = ToDto(l.B)})
        Next
        For Each mk As IReadOnlyList(Of NormPoint) In masks
            m.Masks.Add(mk.Select(AddressOf ToDto).ToList())
        Next
        Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(path))
        If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
        Dim tmp As String = path & ".tmp"
        IO.File.WriteAllText(tmp, JsonSerializer.Serialize(m), Text.Encoding.UTF8)
        IO.File.Move(tmp, path, True)
    End Sub

    ''' <summary>يحمّل الهندسة؛ ملف تالف أو غائب = قوائم فارغة (لا يُسقط التطبيق).</summary>
    Public Shared Sub Load(path As String, zones As List(Of RestrictedZone), lines As List(Of TripLine),
                           masks As List(Of IReadOnlyList(Of NormPoint)))
        zones.Clear()
        lines.Clear()
        masks.Clear()
        If Not IO.File.Exists(path) Then Return
        Try
            Dim m As Model = JsonSerializer.Deserialize(Of Model)(IO.File.ReadAllText(path, Text.Encoding.UTF8))
            If m Is Nothing Then Return
            For Each z As ZoneDto In m.Zones
                If z.Points.Count >= 3 Then
                    zones.Add(New RestrictedZone(z.Name, z.Points.Select(AddressOf FromDto).ToList(), CType(Math.Min(2, Math.Max(0, z.Severity)), AlarmSeverity)))
                End If
            Next
            For Each l As LineDto In m.Lines
                lines.Add(New TripLine(l.Name, FromDto(l.A), FromDto(l.B), CType(Math.Min(2, Math.Max(0, l.Direction)), LineDirection)))
            Next
            For Each mk As List(Of PointDto) In m.Masks
                If mk.Count >= 3 Then masks.Add(mk.Select(AddressOf FromDto).ToList())
            Next
        Catch ex As JsonException
            zones.Clear()
            lines.Clear()
            masks.Clear()
        End Try
    End Sub
End Class
