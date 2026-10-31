Imports Ptz.Core.Models
Imports Ptz.Core.Vision

Namespace Engine

    ''' <summary>
    ''' عقد التحكم بمحرك الذكاء كما تراه المقدِّمات: تُنفّذه <see cref="SmartEngine"/> محلياً، و<c>RemoteSmartEngine</c>
    ''' كوكيل لمحرك يعمل داخل البوابة. الأوامر متزامنة ورخيصة (تفاؤلية)؛ تنفيذ الوكيل البعيد يرسلها
    ''' في الخلفية ويبلّغ عن فشلها عبر <see cref="CommandFailed"/>.
    ''' </summary>
    Public Interface ISmartEngine
        Event FrameProcessed As Action(Of SmartFrameResult)
        Event Faulted As Action(Of Exception, Integer)
        ''' <summary>فشل أمر تحكم بعيد (رفض صلاحية/شبكة). المحرك المحلي لا يرفعه أبداً.</summary>
        Event CommandFailed As Action(Of Exception)

        Property MotionEnabled As Boolean
        Property BehaviorEnabled As Boolean
        Property AutoTrackEnabled As Boolean
        Property ForceAnomalyLearning As Boolean?

        Sub ApplyBehaviorOptions(src As BehaviorOptions)
        Sub LockTargetAt(point As NormPoint)
        Sub ReleaseTarget()
        Sub NotifyCameraMotion(Optional extraSeconds As Double = 0)

        Sub Start()
        Function StopAsync() As Task
    End Interface

End Namespace
