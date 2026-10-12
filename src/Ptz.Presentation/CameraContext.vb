Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Cameras
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Security

''' <summary>الخدمات المشتركة التي يحتاجها مقدِّم الكاميرا (تُحقن من جذر التركيب).</summary>
Public NotInheritable Class CameraSessionDeps
    Public Property Connection As CameraConnection
    Public Property Session As UserSession
    Public Property Access As AccessControl
    Public Property Audit As IAuditLog
    Public Property Bus As AlarmBus
    Public Property Decoder As IFrameDecoder
    Public Property Dispatcher As IUiDispatcher
    Public Property DataDirectory As String
    Public Property Clock As IClock = SystemClock.Instance
    ''' <summary>للاختبارات: معالج HTTP بديل.</summary>
    Public Property HttpHandler As Net.Http.HttpMessageHandler
    ''' <summary>اسم عرض الكاميرا (يظهر في الإنذارات وملفات البيانات).</summary>
    Public Property CameraName As String = ""
    ''' <summary>Nothing = الوضع المحلي (<see cref="LocalCameraBackendFactory"/>)؛ وإلا الواجهة الخلفية المحددة (بوابة بعيدة).</summary>
    Public Property BackendFactory As ICameraBackendFactory
End Class

''' <summary>سياق جلسة كاميرا واحدة تتشاركه المقدِّمات الفرعية.</summary>
Public NotInheritable Class CameraContext
    Public ReadOnly Property Deps As CameraSessionDeps
    Public ReadOnly Property Controller As SecuredCameraController
    Public ReadOnly Property Engine As ISmartEngine
    Public ReadOnly Property SmartConfig As ISmartConfigRepository
    Public ReadOnly Property Messages As IMessageSink
    Public ReadOnly Property Ui As IUiDispatcher
    Public ReadOnly Property Cancellation As Threading.CancellationToken

    Public Sub New(deps As CameraSessionDeps, controller As SecuredCameraController, engine As ISmartEngine,
                   smartConfig As ISmartConfigRepository, messages As IMessageSink, ct As Threading.CancellationToken)
        Me.Deps = deps
        Me.Controller = controller
        Me.Engine = engine
        Me.SmartConfig = smartConfig
        Me.Messages = messages
        Ui = deps.Dispatcher
        Cancellation = ct
    End Sub

    Public ReadOnly Property CameraName As String
        Get
            Return Deps.CameraName
        End Get
    End Property

    ''' <summary>فحص صلاحية لعمليات لا تمر عبر المتحكم (إعدادات الذكاء)، مع تدقيق النتيجة.</summary>
    Public Sub Authorize(permission As Permission, action As String, details As String)
        Try
            Deps.Access.Demand(Deps.Session, permission)
            Deps.Audit.Write(Deps.Session.UserName, action, CameraName, "OK", details)
        Catch ex As AccessDeniedException
            Deps.Audit.Write(Deps.Session.UserName, action, CameraName, "DENIED", ex.Message)
            Throw
        End Try
    End Sub
End Class

''' <summary>قاعدة المقدِّمات الفرعية: معالجة موحّدة للأخطاء بحيث لا تنهار الواجهة أبداً.</summary>
Public MustInherit Class PanelPresenterBase
    Protected ReadOnly Context As CameraContext

    Protected Sub New(context As CameraContext)
        Me.Context = context
    End Sub

    Protected ReadOnly Property Messages As IMessageSink
        Get
            Return Context.Messages
        End Get
    End Property

    Protected ReadOnly Property Ct As Threading.CancellationToken
        Get
            Return Context.Cancellation
        End Get
    End Property

    ''' <summary>ينفّذ عملية ويحوّل الاستثناءات المتوقعة إلى رسائل مفهومة. يعيد true عند النجاح.</summary>
    Protected Async Function GuardAsync(op As Func(Of Task), Optional okMessage As String = Nothing) As Task(Of Boolean)
        Try
            Await op()
            If okMessage IsNot Nothing Then Messages.ShowInfo(okMessage)
            Return True
        Catch ex As AccessDeniedException
            Messages.ShowError(If(ex.IsSessionExpired, UiText.SessionExpired, UiText.NoPermission))
        Catch ex As NotSupportedException
            Messages.ShowError(UiText.Unsupported)
        Catch ex As Ptz.Core.Isapi.IsapiException
            Messages.ShowError(ex.Message)
        Catch ex As ArgumentException
            Messages.ShowError(ex.Message)
        Catch ex As InvalidOperationException
            Messages.ShowError(ex.Message)
        Catch ex As OperationCanceledException
            ' إغلاق النافذة: لا رسالة.
        End Try
        Return False
    End Function

    Protected Sub GuardSync(op As Action)
        Try
            op()
        Catch ex As AccessDeniedException
            Messages.ShowError(If(ex.IsSessionExpired, UiText.SessionExpired, UiText.NoPermission))
        Catch ex As ArgumentException
            Messages.ShowError(ex.Message)
        Catch ex As InvalidOperationException
            Messages.ShowError(ex.Message)
        End Try
    End Sub
End Class
