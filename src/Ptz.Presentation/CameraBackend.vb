Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Cameras
Imports Ptz.Core.Engine
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision

''' <summary>
''' الواجهة الخلفية لجلسة كاميرا: كل ما تحتاجه المقدِّمات من «المصدر الحقيقي» للتحكم والتحليل.
''' محلي (ISAPI مباشر + SmartEngine داخل التطبيق) أو بعيد (بوابة LAN تشغّل المحرك على مدار الساعة).
''' </summary>
Public Interface ICameraBackend
    Inherits IDisposable
    ''' <summary>المتحكم المؤمَّن (RBAC + تدقيق) فوق النقل الخام. تبقى واجهة المستخدم تراه وحده.</summary>
    ReadOnly Property Controller As SecuredCameraController
    ReadOnly Property Engine As ISmartEngine
    ReadOnly Property SmartConfig As ISmartConfigRepository
    ReadOnly Property Video As IJpegFeed
    ''' <summary>يبدأ المضخات الخلفية غير التابعة للمحرك بعد نجاح الاتصال (مثل جسر أحداث الكاميرا محلياً).</summary>
    Function StartBackground(ct As Threading.CancellationToken) As Task
End Interface

Public Interface ICameraBackendFactory
    ''' <param name="secure">يلفّ المتحكم الخام بالمزخرف الأمني (تملكه المقدِّمة لتوحيد الإعداد).</param>
    ''' <param name="configureEngine">ضبط المحرك الخاص بالطراز (يُهمله التنفيذ البعيد: البوابة تملك الضبط).</param>
    Function Create(profile As CameraProfile, deps As CameraSessionDeps,
                    secure As Func(Of ICameraController, SecuredCameraController),
                    configureEngine As Action(Of SmartEngineOptions)) As ICameraBackend
End Interface

''' <summary>الوضع الأصلي: يتحدث التطبيق مع الكاميرا مباشرة ويشغّل SmartEngine في العملية نفسها.</summary>
Public NotInheritable Class LocalCameraBackendFactory
    Implements ICameraBackendFactory

    Public Shared ReadOnly Property Instance As New LocalCameraBackendFactory()

    Public Function Create(profile As CameraProfile, deps As CameraSessionDeps,
                           secure As Func(Of ICameraController, SecuredCameraController),
                           configureEngine As Action(Of SmartEngineOptions)) As ICameraBackend Implements ICameraBackendFactory.Create
        Dim inner As ICameraController = CameraControllerFactory.Create(profile, deps.Connection, deps.HttpHandler)
        Dim controller As SecuredCameraController = secure(inner)
        Dim options As New SmartEngineOptions With {.CameraName = deps.CameraName}
        If configureEngine IsNot Nothing Then configureEngine(options)
        Dim source As New SnapshotFrameSource(controller, deps.Decoder, deps.Clock)
        Dim recorder As New TrajectoryRecorder(IO.Path.Combine(deps.DataDirectory, "trajectories"), deps.CameraName)
        Dim engine As New SmartEngine(options, source, controller.AsAutoTrackMover(), deps.Bus, recorder, New HeatMap(), deps.Clock)
        Return New LocalCameraBackend(deps, controller, engine, source,
                                      New FileSmartConfigRepository(engine, deps.DataDirectory, deps.CameraName))
    End Function
End Class

Friend NotInheritable Class LocalCameraBackend
    Implements ICameraBackend

    Private ReadOnly _deps As CameraSessionDeps

    Public Sub New(deps As CameraSessionDeps, controller As SecuredCameraController, engine As SmartEngine,
                   source As SnapshotFrameSource, config As ISmartConfigRepository)
        _deps = deps
        Me.Controller = controller
        Me.Engine = engine
        Video = source
        SmartConfig = config
    End Sub

    Public ReadOnly Property Controller As SecuredCameraController Implements ICameraBackend.Controller
    Public ReadOnly Property Engine As ISmartEngine Implements ICameraBackend.Engine
    Public ReadOnly Property SmartConfig As ISmartConfigRepository Implements ICameraBackend.SmartConfig
    Public ReadOnly Property Video As IJpegFeed Implements ICameraBackend.Video

    Public Function StartBackground(ct As Threading.CancellationToken) As Task Implements ICameraBackend.StartBackground
        If (Controller.Features And CameraFeatures.AlertStream) = 0 Then Return Task.CompletedTask
        Return Task.Run(Function() AlertLoopAsync(ct))
    End Function

    Private Function AlertLoopAsync(ct As Threading.CancellationToken) As Task
        Return DeviceAlertBridge.RunAsync(_deps.Connection, _deps.HttpHandler, _deps.CameraName, _deps.Bus, ct)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        ' المتحكم تملكه المقدِّمة (تتخلص منه)؛ لا موارد أخرى محلياً.
    End Sub
End Class
