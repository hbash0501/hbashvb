Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision

''' <summary>لوحة الذكاء: كشف الحركة، السلوك، التتبع الآلي، رسم المناطق والخطوط، نموذج الشذوذ.</summary>
Public NotInheritable Class SmartPresenter
    Inherits PanelPresenterBase

    Private ReadOnly _view As ISmartPanelView
    Private ReadOnly _video As IVideoView
    Private ReadOnly _zones As New List(Of RestrictedZone)()
    Private ReadOnly _lines As New List(Of TripLine)()
    Private ReadOnly _masks As New List(Of IReadOnlyList(Of NormPoint))()
    Private ReadOnly _geometryPath As String
    Private ReadOnly _modelPath As String

    Public Sub New(context As CameraContext, view As ISmartPanelView, video As IVideoView)
        MyBase.New(context)
        _view = view
        _video = video
        Dim safe As String = SafeFileName(context.CameraName)
        _geometryPath = IO.Path.Combine(context.Deps.DataDirectory, safe & "-geometry.json")
        _modelPath = IO.Path.Combine(context.Deps.DataDirectory, safe & "-anomaly.json")

        AddHandler view.MotionToggled, Sub(on_) Configure(Permission.SmartConfigure, "Smart.Motion", on_.ToString(), Sub() Context.Engine.MotionEnabled = on_)
        AddHandler view.BehaviorToggled, Sub(on_) Configure(Permission.SmartConfigure, "Smart.Behavior", on_.ToString(), Sub() Context.Engine.BehaviorEnabled = on_)
        AddHandler view.AutoTrackToggled, AddressOf OnAutoTrack
        AddHandler view.ReleaseTargetRequested, Sub() Configure(Permission.AutoTrack, "AutoTrack.Release", "", Sub() Context.Engine.ReleaseTarget())
        AddHandler view.AnomalyLearningToggled, Sub(on_) Configure(Permission.SmartConfigure, "Smart.AnomalyLearning", on_.ToString(), Sub() Context.Engine.ForceAnomalyLearning = on_)
        AddHandler view.SaveModelRequested, AddressOf OnSaveModel
        AddHandler view.ApplyBehaviorOptionsRequested, Sub(o) Configure(Permission.SmartConfigure, "Smart.BehaviorOptions", "", Sub() Context.Engine.ApplyBehaviorOptions(o))
        AddHandler view.ClearGeometryRequested, AddressOf OnClearGeometry
        AddHandler view.DrawModeRequested, AddressOf OnDrawMode
        AddHandler video.ZoneCompleted, AddressOf OnZoneCompleted
        AddHandler video.LineCompleted, AddressOf OnLineCompleted
        AddHandler video.TargetClicked, Sub(p) Configure(Permission.AutoTrack, "AutoTrack.Lock", p.ToString(), Sub() Context.Engine.LockTargetAt(p))
    End Sub

    Private Shared Function SafeFileName(name As String) As String
        Dim sb As New Text.StringBuilder()
        For Each c As Char In name
            sb.Append(If(Char.IsLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c, c, "_"c))
        Next
        Return If(sb.Length = 0, "camera", sb.ToString())
    End Function

    Public Sub Initialize()
        GeometryStore.Load(_geometryPath, _zones, _lines, _masks)
        If IO.File.Exists(_modelPath) Then
            Try
                Context.Engine.Anomaly.LoadJson(IO.File.ReadAllText(_modelPath))
                Context.Engine.ForceAnomalyLearning = False
            Catch ex As Exception When TypeOf ex Is IO.InvalidDataException OrElse TypeOf ex Is Text.Json.JsonException
                ' نموذج تالف: نبدأ التعلم من جديد.
            End Try
        End If
        PushGeometry()
    End Sub

    Private Sub Configure(permission As Permission, action As String, details As String, apply As Action)
        GuardSync(Sub()
                      Context.Authorize(permission, action, details)
                      apply()
                  End Sub)
    End Sub

    Private Sub OnAutoTrack(on_ As Boolean)
        GuardSync(Sub()
                      Context.Authorize(Permission.AutoTrack, "AutoTrack.Toggle", on_.ToString())
                      Context.Engine.AutoTrackEnabled = on_
                      _view.ShowHint(If(on_, UiText.AutoTrackOn, UiText.AutoTrackOff))
                  End Sub)
    End Sub

    Private Sub OnDrawMode(mode As GeometryDrawMode)
        _video.SetDrawMode(mode)
        Select Case mode
            Case GeometryDrawMode.Zone, GeometryDrawMode.MaskZone : _view.ShowHint(UiText.ZoneDrawHint)
            Case GeometryDrawMode.Line : _view.ShowHint(UiText.LineDrawHint)
            Case Else : _view.ShowHint("")
        End Select
    End Sub

    Private Sub OnZoneCompleted(points As IReadOnlyList(Of NormPoint), mode As GeometryDrawMode)
        GuardSync(Sub()
                      Context.Authorize(Permission.SmartConfigure, "Smart.AddZone", mode.ToString())
                      If points.Count < 3 Then Throw New ArgumentException("المنطقة تحتاج 3 نقاط على الأقل.")
                      If mode = GeometryDrawMode.MaskZone Then
                          _masks.Add(points)
                      Else
                          _zones.Add(New RestrictedZone($"Zone {_zones.Count + 1}", points))
                      End If
                      CommitGeometry()
                  End Sub)
    End Sub

    Private Sub OnLineCompleted(a As NormPoint, b As NormPoint)
        GuardSync(Sub()
                      Context.Authorize(Permission.SmartConfigure, "Smart.AddLine", "")
                      If a.DistanceTo(b) < 0.02 Then Throw New ArgumentException("الخط قصير جداً.")
                      _lines.Add(New TripLine($"Line {_lines.Count + 1}", a, b, LineDirection.Both))
                      CommitGeometry()
                  End Sub)
    End Sub

    Private Sub OnClearGeometry()
        GuardSync(Sub()
                      Context.Authorize(Permission.SmartConfigure, "Smart.ClearGeometry", "")
                      _zones.Clear()
                      _lines.Clear()
                      _masks.Clear()
                      CommitGeometry()
                  End Sub)
    End Sub

    Private Sub CommitGeometry()
        Context.Engine.SetZones(_zones)
        Context.Engine.SetLines(_lines)
        Context.Engine.SetMaskZones(_masks)
        Try
            GeometryStore.Save(_geometryPath, _zones, _lines, _masks)
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Messages.ShowError("تعذّر حفظ المناطق: " & ex.Message)
        End Try
        PushGeometry()
        _video.SetDrawMode(GeometryDrawMode.None)
        _view.ShowHint("")
    End Sub

    Private Sub PushGeometry()
        Context.Engine.SetZones(_zones)
        Context.Engine.SetLines(_lines)
        Context.Engine.SetMaskZones(_masks)
        _video.ShowGeometry(_zones.ToList(), _lines.ToList(), _masks.ToList())
    End Sub

    Private Sub OnSaveModel()
        GuardSync(Sub()
                      Context.Authorize(Permission.SmartConfigure, "Smart.SaveModel", "")
                      Dim dir As String = IO.Path.GetDirectoryName(_modelPath)
                      If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
                      IO.File.WriteAllText(_modelPath, Context.Engine.Anomaly.ToJson())
                      Messages.ShowInfo(UiText.Saved)
                  End Sub)
    End Sub

    ''' <summary>يُستدعى من الحلقة الرئيسية بعد كل إطار (من خيط الخلفية).</summary>
    Public Sub OnFrame(result As SmartFrameResult)
        Context.Ui.Post(Sub() _view.ShowStatus(result.AutoTrackActive, result.AnomalyLearning, result.Tracks.Count, result.Suspended))
    End Sub
End Class
