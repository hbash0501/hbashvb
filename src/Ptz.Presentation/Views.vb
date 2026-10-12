Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision

''' <summary>تُنفّذها الواجهة لتمرير عمل إلى خيط الواجهة (UI thread).</summary>
Public Interface IUiDispatcher
    Sub Post(action As Action)
End Interface

''' <summary>رسائل المستخدم المشتركة بين كل اللوحات.</summary>
Public Interface IMessageSink
    Sub ShowError(message As String)
    Sub ShowInfo(message As String)
End Interface

Public Interface IPtzPanelView
    Event MoveRequested As Action(Of PtzVector)
    Event StopRequested As Action
    Event GoHomeRequested As Action
    Event SetHomeRequested As Action
    Event GoToPositionRequested As Action(Of PtzPosition)
    Event RefreshPresetsRequested As Action
    Event PresetGoRequested As Action(Of Integer)
    Event PresetSaveRequested As Action(Of Integer, String)
    Event PresetDeleteRequested As Action(Of Integer)
    Event PatrolRequested As Action(Of Integer, Boolean)
    Event PatternRecordRequested As Action(Of Integer, Boolean)
    Event PatternRunRequested As Action(Of Integer, Boolean)

    Sub SetCapabilities(features As CameraFeatures, profile As CameraProfile)
    Sub ShowPresets(presets As IReadOnlyList(Of PtzPreset))
    Sub ShowPosition(position As PtzPosition)
End Interface

Public Interface IImagePanelView
    Event ApplyRequested As Action(Of ImageSettings)
    Event RefreshRequested As Action
    Event FocusRequested As Action(Of LensDirection, Integer)
    Event IrisRequested As Action(Of LensDirection, Integer)
    Event AuxRequested As Action(Of AuxKind, Boolean)

    Sub SetCapabilities(features As CameraFeatures)
    Sub ShowSettings(settings As ImageSettings)
End Interface

Public Enum GeometryDrawMode
    None
    Zone
    Line
    MaskZone
End Enum

Public Interface ISmartPanelView
    Event MotionToggled As Action(Of Boolean)
    Event BehaviorToggled As Action(Of Boolean)
    Event AutoTrackToggled As Action(Of Boolean)
    Event ReleaseTargetRequested As Action
    Event ClearGeometryRequested As Action
    Event DrawModeRequested As Action(Of GeometryDrawMode)
    Event AnomalyLearningToggled As Action(Of Boolean)
    Event SaveModelRequested As Action
    Event ApplyBehaviorOptionsRequested As Action(Of BehaviorOptions)

    Sub ShowStatus(autoTrackActive As Boolean, learning As Boolean, trackCount As Integer, suspended As Boolean)
    Sub ShowHint(text As String)
End Interface

Public Interface IAlarmPanelView
    Event AcknowledgeRequested As Action(Of Guid)
    Sub AddAlarm(alarm As AlarmEvent)
    Sub MarkAcknowledged(id As Guid)
End Interface

Public Interface IVideoView
    ''' <summary>نقر المستخدم: نقرة عادية (مركز) أو مستطيل (تكبير).</summary>
    Event AreaSelected As Action(Of NormRect)
    Event TargetClicked As Action(Of NormPoint)
    Event ZoneCompleted As Action(Of IReadOnlyList(Of NormPoint), GeometryDrawMode)
    Event LineCompleted As Action(Of NormPoint, NormPoint)

    Sub ShowFrame(jpeg As Byte())
    Sub ShowOverlay(result As SmartFrameResult)
    Sub ShowGeometry(zones As IReadOnlyList(Of RestrictedZone), lines As IReadOnlyList(Of TripLine), masks As IReadOnlyList(Of IReadOnlyList(Of NormPoint)))
    Sub SetDrawMode(mode As GeometryDrawMode)
End Interface

''' <summary>الفورم الجامع لإحدى الكاميرتين: يعرض اللوحات ويرتبط بالمقدِّم.</summary>
Public Interface ICameraFormView
    Inherits IMessageSink
    ReadOnly Property Ptz As IPtzPanelView
    ReadOnly Property Image As IImagePanelView
    ReadOnly Property Smart As ISmartPanelView
    ReadOnly Property Alarms As IAlarmPanelView
    ReadOnly Property Video As IVideoView
    ReadOnly Property Dispatcher As IUiDispatcher
    Event ReconnectRequested As Action

    ''' <summary>تفعيل/تعطيل عناصر الواجهة بحسب صلاحيات المستخدم (الفرض الفعلي يتم دائماً في الطبقة الأمنية).</summary>
    Sub ApplyPermissions(permissions As Permission)
    Sub SetTitle(text As String)
    Sub SetConnectionState(connected As Boolean, text As String)
    Sub ShowDeviceInfo(info As DeviceInfo)
    Sub SetUserInfo(userName As String, role As String)
    Sub SetFrameStats(fps As Double, trackCount As Integer)
End Interface
