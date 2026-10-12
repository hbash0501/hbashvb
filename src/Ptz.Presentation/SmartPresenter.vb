Imports Ptz.Core.Abstractions
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

    Public Sub New(context As CameraContext, view As ISmartPanelView, video As IVideoView)
        MyBase.New(context)
        _view = view
        _video = video

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

    Public Async Function InitializeAsync() As Task
        Dim g As SmartGeometry = Await Context.SmartConfig.LoadAsync(Ct)
        _zones.Clear()
        _lines.Clear()
        _masks.Clear()
        _zones.AddRange(g.Zones)
        _lines.AddRange(g.Lines)
        _masks.AddRange(g.Masks)
        ShowGeometry()
    End Function

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

    Private Async Sub OnZoneCompleted(points As IReadOnlyList(Of NormPoint), mode As GeometryDrawMode)
        Await GuardAsync(Function()
                             Context.Authorize(Permission.SmartConfigure, "Smart.AddZone", mode.ToString())
                             If points.Count < 3 Then Throw New ArgumentException("المنطقة تحتاج 3 نقاط على الأقل.")
                             Return MutateGeometryAsync(Sub()
                                                            If mode = GeometryDrawMode.MaskZone Then
                                                                _masks.Add(points)
                                                            Else
                                                                _zones.Add(New RestrictedZone($"Zone {_zones.Count + 1}", points))
                                                            End If
                                                        End Sub)
                         End Function)
    End Sub

    Private Async Sub OnLineCompleted(a As NormPoint, b As NormPoint)
        Await GuardAsync(Function()
                             Context.Authorize(Permission.SmartConfigure, "Smart.AddLine", "")
                             If a.DistanceTo(b) < 0.02 Then Throw New ArgumentException("الخط قصير جداً.")
                             Return MutateGeometryAsync(Sub() _lines.Add(New TripLine($"Line {_lines.Count + 1}", a, b, LineDirection.Both)))
                         End Function)
    End Sub

    Private Async Sub OnClearGeometry()
        Await GuardAsync(Function()
                             Context.Authorize(Permission.SmartConfigure, "Smart.ClearGeometry", "")
                             Return MutateGeometryAsync(Sub()
                                                            _zones.Clear()
                                                            _lines.Clear()
                                                            _masks.Clear()
                                                        End Sub)
                         End Function)
    End Sub

    ''' <summary>يعدّل النسخة العاملة ثم يحفظها؛ عند رفض الجهة المالكة (بوابة بعيدة) تُستعاد النسخة السابقة.</summary>
    Private Async Function MutateGeometryAsync(mutate As Action) As Task
        Dim before As New SmartGeometry(_zones, _lines, _masks)
        mutate()
        Try
            Await CommitGeometryAsync()
        Catch ex As Exception
            _zones.Clear()
            _zones.AddRange(before.Zones)
            _lines.Clear()
            _lines.AddRange(before.Lines)
            _masks.Clear()
            _masks.AddRange(before.Masks)
            Throw
        End Try
    End Function

    ''' <summary>يطبّق الهندسة ويحفظها عبر المخزن (محلي: ملف + المحرك؛ بعيد: البوابة)، ثم يحدّث العرض.</summary>
    Private Async Function CommitGeometryAsync() As Task
        Try
            Await Context.SmartConfig.SaveGeometryAsync(New SmartGeometry(_zones, _lines, _masks), Ct)
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Messages.ShowError("تعذّر حفظ المناطق: " & ex.Message)
        End Try
        ShowGeometry()
        _video.SetDrawMode(GeometryDrawMode.None)
        _view.ShowHint("")
    End Function

    Private Sub ShowGeometry()
        _video.ShowGeometry(_zones.ToList(), _lines.ToList(), _masks.ToList())
    End Sub

    Private Async Sub OnSaveModel()
        Await GuardAsync(Function()
                             Context.Authorize(Permission.SmartConfigure, "Smart.SaveModel", "")
                             Return SaveModelAsync()
                         End Function)
    End Sub

    Private Async Function SaveModelAsync() As Task
        Await Context.SmartConfig.SaveAnomalyModelAsync(Ct)
        Messages.ShowInfo(UiText.Saved)
    End Function

    ''' <summary>يُستدعى من الحلقة الرئيسية بعد كل إطار (من خيط الخلفية).</summary>
    Public Sub OnFrame(result As SmartFrameResult)
        Context.Ui.Post(Sub() _view.ShowStatus(result.AutoTrackActive, result.AnomalyLearning, result.Tracks.Count, result.Suspended))
    End Sub
End Class
