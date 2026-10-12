Imports Ptz.Core.Abstractions
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision
Imports Ptz.Presentation

Public NotInheritable Class ImmediateDispatcher
    Implements IUiDispatcher
    Public Sub Post(action As Action) Implements IUiDispatcher.Post
        action()
    End Sub
End Class

Public NotInheritable Class FakeDecoder
    Implements IFrameDecoder
    Public Function DecodeToGray(jpeg As Byte()) As GrayFrame Implements IFrameDecoder.DecodeToGray
        Return TestFrames.Scene(160, 90, 90, DateTime.UtcNow, jpeg.Length)
    End Function
End Class

Public NotInheritable Class FakePtzView
    Implements IPtzPanelView
    Public Event MoveRequested As Action(Of PtzVector) Implements IPtzPanelView.MoveRequested
    Public Event StopRequested As Action Implements IPtzPanelView.StopRequested
    Public Event GoHomeRequested As Action Implements IPtzPanelView.GoHomeRequested
    Public Event SetHomeRequested As Action Implements IPtzPanelView.SetHomeRequested
    Public Event GoToPositionRequested As Action(Of PtzPosition) Implements IPtzPanelView.GoToPositionRequested
    Public Event RefreshPresetsRequested As Action Implements IPtzPanelView.RefreshPresetsRequested
    Public Event PresetGoRequested As Action(Of Integer) Implements IPtzPanelView.PresetGoRequested
    Public Event PresetSaveRequested As Action(Of Integer, String) Implements IPtzPanelView.PresetSaveRequested
    Public Event PresetDeleteRequested As Action(Of Integer) Implements IPtzPanelView.PresetDeleteRequested
    Public Event PatrolRequested As Action(Of Integer, Boolean) Implements IPtzPanelView.PatrolRequested
    Public Event PatternRecordRequested As Action(Of Integer, Boolean) Implements IPtzPanelView.PatternRecordRequested
    Public Event PatternRunRequested As Action(Of Integer, Boolean) Implements IPtzPanelView.PatternRunRequested
    Public Property Presets As IReadOnlyList(Of PtzPreset)
    Public Property Capabilities As CameraFeatures
    Public Sub SetCapabilities(features As CameraFeatures, profile As CameraProfile) Implements IPtzPanelView.SetCapabilities
        Capabilities = features
    End Sub
    Public Sub ShowPresets(list As IReadOnlyList(Of PtzPreset)) Implements IPtzPanelView.ShowPresets
        Presets = list
    End Sub
    Public Sub ShowPosition(position As PtzPosition) Implements IPtzPanelView.ShowPosition
    End Sub
    Public Sub RaiseMove(v As PtzVector)
        RaiseEvent MoveRequested(v)
    End Sub
    Public Sub RaiseStop()
        RaiseEvent StopRequested()
    End Sub
    Public Sub RaisePresetGo(id As Integer)
        RaiseEvent PresetGoRequested(id)
    End Sub
End Class

Public NotInheritable Class FakeImageView
    Implements IImagePanelView
    Public Event ApplyRequested As Action(Of ImageSettings) Implements IImagePanelView.ApplyRequested
    Public Event RefreshRequested As Action Implements IImagePanelView.RefreshRequested
    Public Event FocusRequested As Action(Of LensDirection, Integer) Implements IImagePanelView.FocusRequested
    Public Event IrisRequested As Action(Of LensDirection, Integer) Implements IImagePanelView.IrisRequested
    Public Event AuxRequested As Action(Of AuxKind, Boolean) Implements IImagePanelView.AuxRequested
    Public Property Shown As ImageSettings
    Public Sub SetCapabilities(features As CameraFeatures) Implements IImagePanelView.SetCapabilities
    End Sub
    Public Sub ShowSettings(settings As ImageSettings) Implements IImagePanelView.ShowSettings
        Shown = settings
    End Sub
    Public Sub RaiseApply(s As ImageSettings)
        RaiseEvent ApplyRequested(s)
    End Sub
End Class

Public NotInheritable Class FakeSmartView
    Implements ISmartPanelView
    Public Event MotionToggled As Action(Of Boolean) Implements ISmartPanelView.MotionToggled
    Public Event BehaviorToggled As Action(Of Boolean) Implements ISmartPanelView.BehaviorToggled
    Public Event AutoTrackToggled As Action(Of Boolean) Implements ISmartPanelView.AutoTrackToggled
    Public Event ReleaseTargetRequested As Action Implements ISmartPanelView.ReleaseTargetRequested
    Public Event ClearGeometryRequested As Action Implements ISmartPanelView.ClearGeometryRequested
    Public Event DrawModeRequested As Action(Of GeometryDrawMode) Implements ISmartPanelView.DrawModeRequested
    Public Event AnomalyLearningToggled As Action(Of Boolean) Implements ISmartPanelView.AnomalyLearningToggled
    Public Event SaveModelRequested As Action Implements ISmartPanelView.SaveModelRequested
    Public Event ApplyBehaviorOptionsRequested As Action(Of BehaviorOptions) Implements ISmartPanelView.ApplyBehaviorOptionsRequested
    Public Property Hint As String = ""
    Public Sub ShowStatus(autoTrackActive As Boolean, learning As Boolean, trackCount As Integer, suspended As Boolean) Implements ISmartPanelView.ShowStatus
    End Sub
    Public Sub ShowHint(text As String) Implements ISmartPanelView.ShowHint
        Hint = text
    End Sub
    Public Sub RaiseAutoTrack(on_ As Boolean)
        RaiseEvent AutoTrackToggled(on_)
    End Sub
    Public Sub RaiseSaveModel()
        RaiseEvent SaveModelRequested()
    End Sub
End Class

Public NotInheritable Class FakeAlarmView
    Implements IAlarmPanelView
    Public Event AcknowledgeRequested As Action(Of Guid) Implements IAlarmPanelView.AcknowledgeRequested
    Public ReadOnly Property Alarms As New List(Of AlarmEvent)()
    Public ReadOnly Property Acked As New List(Of Guid)()
    Public Sub AddAlarm(alarm As AlarmEvent) Implements IAlarmPanelView.AddAlarm
        SyncLock Alarms
            Alarms.Add(alarm)
        End SyncLock
    End Sub
    Public Sub MarkAcknowledged(id As Guid) Implements IAlarmPanelView.MarkAcknowledged
        Acked.Add(id)
    End Sub
    Public Sub RaiseAck(id As Guid)
        RaiseEvent AcknowledgeRequested(id)
    End Sub
End Class

Public NotInheritable Class FakeVideoView
    Implements IVideoView
    Public Event AreaSelected As Action(Of NormRect) Implements IVideoView.AreaSelected
    Public Event TargetClicked As Action(Of NormPoint) Implements IVideoView.TargetClicked
    Public Event ZoneCompleted As Action(Of IReadOnlyList(Of NormPoint), GeometryDrawMode) Implements IVideoView.ZoneCompleted
    Public Event LineCompleted As Action(Of NormPoint, NormPoint) Implements IVideoView.LineCompleted
    Public Frames As Integer
    Public Zones As Integer
    Public Lines As Integer
    Public Masks As Integer
    Public Sub ShowFrame(jpeg As Byte()) Implements IVideoView.ShowFrame
        Threading.Interlocked.Increment(Frames)
    End Sub
    Public Sub ShowOverlay(result As SmartFrameResult) Implements IVideoView.ShowOverlay
    End Sub
    Public Sub ShowGeometry(z As IReadOnlyList(Of RestrictedZone), l As IReadOnlyList(Of TripLine), m As IReadOnlyList(Of IReadOnlyList(Of NormPoint))) Implements IVideoView.ShowGeometry
        Zones = z.Count
        Lines = l.Count
        Masks = m.Count
    End Sub
    Public Sub SetDrawMode(mode As GeometryDrawMode) Implements IVideoView.SetDrawMode
    End Sub
    Public Sub RaiseZone(points As IReadOnlyList(Of NormPoint), mode As GeometryDrawMode)
        RaiseEvent ZoneCompleted(points, mode)
    End Sub
    Public Sub RaiseLine(a As NormPoint, b As NormPoint)
        RaiseEvent LineCompleted(a, b)
    End Sub
    Public Sub RaiseArea(r As NormRect)
        RaiseEvent AreaSelected(r)
    End Sub
End Class

Public NotInheritable Class FakeFormView
    Implements ICameraFormView
    Public ReadOnly Property PtzView As New FakePtzView()
    Public ReadOnly Property ImageView As New FakeImageView()
    Public ReadOnly Property SmartView As New FakeSmartView()
    Public ReadOnly Property AlarmView As New FakeAlarmView()
    Public ReadOnly Property VideoView As New FakeVideoView()
    Public ReadOnly Property Errors As New List(Of String)()
    Public ReadOnly Property Infos As New List(Of String)()
    Public Property Title As String = ""
    Public Property Connected As Boolean
    Public Property StateText As String = ""
    Public Property Device As DeviceInfo
    Public Property LastPermissions As Permission
    Public Event ReconnectRequested As Action Implements ICameraFormView.ReconnectRequested

    Public ReadOnly Property Ptz As IPtzPanelView Implements ICameraFormView.Ptz
        Get
            Return PtzView
        End Get
    End Property
    Public ReadOnly Property Image As IImagePanelView Implements ICameraFormView.Image
        Get
            Return ImageView
        End Get
    End Property
    Public ReadOnly Property Smart As ISmartPanelView Implements ICameraFormView.Smart
        Get
            Return SmartView
        End Get
    End Property
    Public ReadOnly Property Alarms As IAlarmPanelView Implements ICameraFormView.Alarms
        Get
            Return AlarmView
        End Get
    End Property
    Public ReadOnly Property Video As IVideoView Implements ICameraFormView.Video
        Get
            Return VideoView
        End Get
    End Property
    Public ReadOnly Property Dispatcher As IUiDispatcher Implements ICameraFormView.Dispatcher
        Get
            Return New ImmediateDispatcher()
        End Get
    End Property

    Public Sub ApplyPermissions(permissions As Permission) Implements ICameraFormView.ApplyPermissions
        LastPermissions = permissions
    End Sub
    Public Sub SetTitle(text As String) Implements ICameraFormView.SetTitle
        Title = text
    End Sub
    Public Sub SetConnectionState(isConnected As Boolean, text As String) Implements ICameraFormView.SetConnectionState
        Connected = isConnected
        StateText = text
    End Sub
    Public Sub ShowDeviceInfo(info As DeviceInfo) Implements ICameraFormView.ShowDeviceInfo
        Device = info
    End Sub
    Public Sub SetUserInfo(userName As String, role As String) Implements ICameraFormView.SetUserInfo
    End Sub
    Public Sub SetFrameStats(fps As Double, trackCount As Integer) Implements ICameraFormView.SetFrameStats
    End Sub
    Public Sub ShowError(message As String) Implements IMessageSink.ShowError
        SyncLock Errors
            Errors.Add(message)
        End SyncLock
    End Sub
    Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
        SyncLock Infos
            Infos.Add(message)
        End SyncLock
    End Sub
    Public Sub RaiseReconnect()
        RaiseEvent ReconnectRequested()
    End Sub
End Class
