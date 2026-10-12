Imports Ptz.Core.Abstractions
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Vision

''' <summary>
''' التخزين بملفين JSON لكل كاميرا في مجلد البيانات + التطبيق على SmartEngine في العملية نفسها.
''' يستعمله التطبيق في الوضع المحلي، وتستعمله البوابة أيضاً لحفظ إعداد محركها الذي يعمل على مدار الساعة.
''' </summary>
Public NotInheritable Class FileSmartConfigRepository
    Implements ISmartConfigRepository

    Private ReadOnly _engine As SmartEngine
    Private ReadOnly _geometryPath As String
    Private ReadOnly _modelPath As String

    Public Sub New(engine As SmartEngine, dataDirectory As String, cameraName As String)
        _engine = engine
        Dim safe As String = SafeFileName(cameraName)
        _geometryPath = IO.Path.Combine(dataDirectory, safe & "-geometry.json")
        _modelPath = IO.Path.Combine(dataDirectory, safe & "-anomaly.json")
    End Sub

    Friend Shared Function SafeFileName(name As String) As String
        Dim sb As New Text.StringBuilder()
        For Each c As Char In name
            sb.Append(If(Char.IsLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c, c, "_"c))
        Next
        Return If(sb.Length = 0, "camera", sb.ToString())
    End Function

    Public Function LoadAsync(ct As Threading.CancellationToken) As Task(Of SmartGeometry) Implements ISmartConfigRepository.LoadAsync
        Dim zones As New List(Of RestrictedZone)()
        Dim lines As New List(Of TripLine)()
        Dim masks As New List(Of IReadOnlyList(Of NormPoint))()
        GeometryStore.Load(_geometryPath, zones, lines, masks)
        If IO.File.Exists(_modelPath) Then
            Try
                _engine.Anomaly.LoadJson(IO.File.ReadAllText(_modelPath))
                _engine.ForceAnomalyLearning = False
            Catch ex As Exception When TypeOf ex Is IO.InvalidDataException OrElse TypeOf ex Is Text.Json.JsonException
                ' نموذج تالف: نبدأ التعلم من جديد.
            End Try
        End If
        Dim g As New SmartGeometry(zones, lines, masks)
        Apply(g)
        Return Task.FromResult(g)
    End Function

    Public Function SaveGeometryAsync(geometry As SmartGeometry, ct As Threading.CancellationToken) As Task Implements ISmartConfigRepository.SaveGeometryAsync
        Apply(geometry)
        GeometryStore.Save(_geometryPath, geometry.Zones, geometry.Lines, geometry.Masks)
        Return Task.CompletedTask
    End Function

    Public Function SaveAnomalyModelAsync(ct As Threading.CancellationToken) As Task Implements ISmartConfigRepository.SaveAnomalyModelAsync
        Dim dir As String = IO.Path.GetDirectoryName(_modelPath)
        If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
        IO.File.WriteAllText(_modelPath, _engine.Anomaly.ToJson())
        Return Task.CompletedTask
    End Function

    Private Sub Apply(g As SmartGeometry)
        _engine.SetZones(g.Zones)
        _engine.SetLines(g.Lines)
        _engine.SetMaskZones(g.Masks)
    End Sub
End Class
