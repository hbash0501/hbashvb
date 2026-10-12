Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Models
Imports Ptz.Core.Vision

Namespace Engine

    Public NotInheritable Class SmartEngineOptions
        Public Property CameraName As String = ""
        Public Property TargetFps As Double = 5.0
        Public Property ProcessWidth As Integer = 160
        Public Property EnableMotion As Boolean = True
        Public Property EnableBehavior As Boolean = True
        ''' <summary>تعلّم النمط الطبيعي لهذه المدة (ثوانٍ) قبل تفعيل كشف الشذوذ. 0 = تعطيل التعلم المؤقت.</summary>
        Public Property AnomalyLearningSeconds As Double = 600
        ''' <summary>زمن السكون بعد توقف الكاميرا قبل استئناف كشف الحركة.</summary>
        Public Property SettleSeconds As Double = 1.0
        Public Property MaxConsecutiveFailuresBeforeAlarm As Integer = 5
        ''' <summary>أساس التراجع الأسّي (ms) عند فشل جلب/معالجة الإطار؛ الحد الأقصى 30 ث.</summary>
        Public Property FailureBackoffMs As Double = 500
        Public Property Motion As New MotionDetectorOptions()
        Public Property Tracker As New ObjectTrackerOptions()
        Public Property Behavior As New BehaviorOptions()
        Public Property AutoTrack As New AutoTrackOptions()
    End Class

    ''' <summary>بيانات مسار للعرض فوق الفيديو.</summary>
    Public NotInheritable Class TrackView
        Public ReadOnly Property Id As Integer
        Public ReadOnly Property Box As NormRect
        Public ReadOnly Property Confirmed As Boolean
        Public ReadOnly Property Speed As Double
        Public ReadOnly Property Trail As IReadOnlyList(Of NormPoint)

        Public Sub New(id As Integer, box As NormRect, confirmed As Boolean, speed As Double, trail As IReadOnlyList(Of NormPoint))
            Me.Id = id
            Me.Box = box
            Me.Confirmed = confirmed
            Me.Speed = speed
            Me.Trail = trail
        End Sub
    End Class

    Public NotInheritable Class SmartFrameResult
        Public Property TimestampUtc As DateTime
        Public Property Tracks As IReadOnlyList(Of TrackView) = New List(Of TrackView)()
        Public Property Events As IReadOnlyList(Of BehaviorEvent) = New List(Of BehaviorEvent)()
        Public Property Suspended As Boolean
        Public Property WarmingUp As Boolean
        Public Property IlluminationChange As Boolean
        Public Property Follow As FollowCommand
        Public Property AutoTrackActive As Boolean
        Public Property AnomalyLearning As Boolean
        ''' <summary>الأمر الذي يجب إرساله للكاميرا (Nothing = لا تغيير).</summary>
        Public Property MoveCommand As PtzVector?
    End Class

    ''' <summary>
    ''' محرك التحليل الذكي لكاميرا واحدة. <c>ProcessFrame</c> نقي (بلا إدخال/إخراج) وقابل للاختبار؛
    ''' حلقة <c>RunAsync</c> تتولى الجلب والتنفيذ وإدارة الأعطال.
    ''' </summary>
    Public NotInheritable Class SmartEngine
        Implements ISmartEngine

        Private ReadOnly _opt As SmartEngineOptions
        Private ReadOnly _source As IFrameSource
        Private ReadOnly _mover As IPtzMover
        Private ReadOnly _bus As AlarmBus
        Private ReadOnly _clock As IClock
        Private ReadOnly _detector As MotionDetector
        Private ReadOnly _tracker As ObjectTracker
        Private ReadOnly _behavior As BehaviorAnalyzer
        Private ReadOnly _auto As AutoTracker
        Private ReadOnly _recorder As TrajectoryRecorder
        Private ReadOnly _heat As HeatMap
        Private ReadOnly _sync As New Object()
        Private ReadOnly _announced As New HashSet(Of Integer)()
        Private _autoTrackEnabled As Boolean
        Private _lockPoint As NormPoint?
        Private _forceLearning As Boolean?
        Private _startedUtc As DateTime = DateTime.MinValue
        Private _moving As Boolean
        Private _cts As Threading.CancellationTokenSource
        Private _task As Task

        Public Event FrameProcessed As Action(Of SmartFrameResult) Implements ISmartEngine.FrameProcessed
        Public Event Faulted As Action(Of Exception, Integer) Implements ISmartEngine.Faulted
        Public Event CommandFailed As Action(Of Exception) Implements ISmartEngine.CommandFailed

        Public Sub New(options As SmartEngineOptions, source As IFrameSource, mover As IPtzMover, bus As AlarmBus,
                       Optional recorder As TrajectoryRecorder = Nothing, Optional heatMap As HeatMap = Nothing,
                       Optional clock As IClock = Nothing, Optional anomaly As SpeedAnomalyModel = Nothing)
            _opt = If(options, New SmartEngineOptions())
            _source = source
            _mover = mover
            _bus = bus
            _recorder = recorder
            _heat = heatMap
            _clock = If(clock, SystemClock.Instance)
            _opt.Motion.ProcessWidth = _opt.ProcessWidth
            _detector = New MotionDetector(_opt.Motion)
            _tracker = New ObjectTracker(_opt.Tracker)
            _behavior = New BehaviorAnalyzer(_opt.Behavior, anomaly)
            _auto = New AutoTracker(_opt.AutoTrack)
        End Sub

        Public ReadOnly Property Anomaly As SpeedAnomalyModel
            Get
                Return _behavior.Anomaly
            End Get
        End Property

        ' ---------------- الإعداد (آمن عبر الخيوط) ----------------
        Public Sub SetZones(zones As IEnumerable(Of RestrictedZone))
            SyncLock _sync
                _behavior.SetZones(zones)
            End SyncLock
        End Sub

        Public Sub SetLines(lines As IEnumerable(Of TripLine))
            SyncLock _sync
                _behavior.SetLines(lines)
            End SyncLock
        End Sub

        Public Sub SetMaskZones(zones As IEnumerable(Of IReadOnlyList(Of NormPoint)))
            SyncLock _sync
                _detector.SetExclusionZones(zones)
            End SyncLock
        End Sub

        Public Property MotionEnabled As Boolean Implements ISmartEngine.MotionEnabled
            Get
                SyncLock _sync
                    Return _opt.EnableMotion
                End SyncLock
            End Get
            Set(value As Boolean)
                SyncLock _sync
                    _opt.EnableMotion = value
                    If Not value Then
                        _tracker.Reset()
                        _announced.Clear()
                    End If
                End SyncLock
            End Set
        End Property

        Public Property BehaviorEnabled As Boolean Implements ISmartEngine.BehaviorEnabled
            Get
                SyncLock _sync
                    Return _opt.EnableBehavior
                End SyncLock
            End Get
            Set(value As Boolean)
                SyncLock _sync
                    _opt.EnableBehavior = value
                End SyncLock
            End Set
        End Property

        ''' <summary>يفرض حالة التعلّم (True/False) أو يعيد القرار للمؤقّت التلقائي (Nothing).</summary>
        Public Property ForceAnomalyLearning As Boolean? Implements ISmartEngine.ForceAnomalyLearning
            Get
                SyncLock _sync
                    Return _forceLearning
                End SyncLock
            End Get
            Set(value As Boolean?)
                SyncLock _sync
                    _forceLearning = value
                End SyncLock
            End Set
        End Property

        ''' <summary>نسخة من عتبات السلوك الحالية (للعرض عن بُعد).</summary>
        Public Function SnapshotBehaviorOptions() As BehaviorOptions
            SyncLock _sync
                Dim s As BehaviorOptions = _opt.Behavior
                Return New BehaviorOptions With {
                    .LoiterSeconds = s.LoiterSeconds, .LoiterRadius = s.LoiterRadius, .RunSpeed = s.RunSpeed, .RunMinSeconds = s.RunMinSeconds,
                    .StationarySeconds = s.StationarySeconds, .StationarySpeed = s.StationarySpeed, .ErraticReversals = s.ErraticReversals,
                    .ErraticWindowSeconds = s.ErraticWindowSeconds, .ErraticMinSegment = s.ErraticMinSegment, .GatherCount = s.GatherCount,
                    .GatherRadius = s.GatherRadius, .EventCooldownSeconds = s.EventCooldownSeconds, .MinTrackAgeSeconds = s.MinTrackAgeSeconds}
            End SyncLock
        End Function

        ''' <summary>يطبّق عتبات السلوك الجديدة فوراً (نسخ القيم داخل القفل).</summary>
        Public Sub ApplyBehaviorOptions(src As BehaviorOptions) Implements ISmartEngine.ApplyBehaviorOptions
            If src Is Nothing Then Throw New ArgumentNullException(NameOf(src))
            SyncLock _sync
                Dim d As BehaviorOptions = _opt.Behavior
                d.LoiterSeconds = src.LoiterSeconds
                d.LoiterRadius = src.LoiterRadius
                d.RunSpeed = src.RunSpeed
                d.RunMinSeconds = src.RunMinSeconds
                d.StationarySeconds = src.StationarySeconds
                d.StationarySpeed = src.StationarySpeed
                d.ErraticReversals = src.ErraticReversals
                d.ErraticWindowSeconds = src.ErraticWindowSeconds
                d.ErraticMinSegment = src.ErraticMinSegment
                d.GatherCount = src.GatherCount
                d.GatherRadius = src.GatherRadius
                d.EventCooldownSeconds = src.EventCooldownSeconds
                d.MinTrackAgeSeconds = src.MinTrackAgeSeconds
            End SyncLock
        End Sub

        Public Property AutoTrackEnabled As Boolean Implements ISmartEngine.AutoTrackEnabled
            Get
                SyncLock _sync
                    Return _autoTrackEnabled
                End SyncLock
            End Get
            Set(value As Boolean)
                SyncLock _sync
                    _autoTrackEnabled = value
                    If Not value Then _auto.Release()
                End SyncLock
            End Set
        End Property

        ''' <summary>يطلب القفل على الهدف عند هذه النقطة (يُعالَج في الإطار التالي).</summary>
        Public Sub LockTargetAt(point As NormPoint) Implements ISmartEngine.LockTargetAt
            SyncLock _sync
                _lockPoint = point
            End SyncLock
        End Sub

        Public Sub ReleaseTarget() Implements ISmartEngine.ReleaseTarget
            SyncLock _sync
                _auto.Release()
                _lockPoint = Nothing
            End SyncLock
        End Sub

        ''' <summary>تُستدعى عند تحريك الكاميرا يدوياً أو بموضع مسبق كي لا تُعتبر الحركة نتيجة إنذارات.</summary>
        Public Sub NotifyCameraMotion(Optional extraSeconds As Double = 0) Implements ISmartEngine.NotifyCameraMotion
            SyncLock _sync
                _detector.SuspendUntil(_clock.UtcNow.AddSeconds(_opt.SettleSeconds + extraSeconds))
                _tracker.Reset()
                _behavior.Reset()
            End SyncLock
        End Sub

        ' ---------------- المعالجة ----------------
        Public Function ProcessFrame(frame As GrayFrame) As SmartFrameResult
            SyncLock _sync
                Dim now As DateTime = frame.TimestampUtc
                If _startedUtc = DateTime.MinValue Then _startedUtc = now
                Dim learning As Boolean = _opt.AnomalyLearningSeconds > 0 AndAlso (now - _startedUtc).TotalSeconds < _opt.AnomalyLearningSeconds
                If _forceLearning.HasValue Then learning = _forceLearning.Value
                _behavior.Anomaly.Learning = learning

                Dim result As New SmartFrameResult With {.TimestampUtc = now, .AnomalyLearning = learning}
                Dim small As GrayFrame = frame.DownscaleToWidth(_opt.ProcessWidth)

                If _auto.Status = FollowStatus.Following Then
                    Dim cmd As FollowCommand = _auto.Update(small, now)
                    result.Follow = cmd
                    result.AutoTrackActive = True
                    result.Suspended = True
                    If cmd.Status = FollowStatus.Lost Then
                        result.AutoTrackActive = False
                        result.MoveCommand = PtzVector.Zero
                        ResetAfterCameraMotion(now)
                        Raise(AlarmSeverity.Warning, "AutoTrack", 0, "فُقد هدف التتبع الآلي.", New NormPoint(0.5, 0.5))
                    Else
                        result.MoveCommand = cmd.Vector
                        Dim box As NormRect = cmd.TargetBox
                        result.Tracks = New List(Of TrackView) From {New TrackView(0, box, True, 0.0, New List(Of NormPoint)())}
                    End If
                    Return result
                End If

                If _moving Then ' كنا نتحرك والآن انتهى التتبع: نوقف ونسمح بالاستقرار
                    result.MoveCommand = PtzVector.Zero
                    ResetAfterCameraMotion(now)
                End If

                If Not _opt.EnableMotion Then Return result

                Dim motion As MotionResult = _detector.Process(frame)
                result.Suspended = motion.Suspended
                result.WarmingUp = motion.WarmingUp
                result.IlluminationChange = motion.IlluminationChange
                If motion.Suspended OrElse motion.WarmingUp OrElse motion.IlluminationChange Then
                    If motion.IlluminationChange Then _tracker.Reset()
                    Return result
                End If

                Dim upd As TrackerUpdate = _tracker.Update(motion.Detections, now)
                For Each ended As Track In upd.Ended
                    _announced.Remove(ended.Id)
                    If _recorder IsNot Nothing Then
                        Try
                            _recorder.Record(ended)
                        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                            Raise(AlarmSeverity.Warning, "System", 0, "تعذّر تسجيل المسار: " & ex.Message, New NormPoint(0.5, 0.5))
                        End Try
                    End If
                Next

                Dim views As New List(Of TrackView)()
                For Each t As Track In upd.Active
                    Dim trail As New List(Of NormPoint)()
                    Dim from As Integer = Math.Max(0, t.Trajectory.Count - 40)
                    For i As Integer = from To t.Trajectory.Count - 1
                        trail.Add(t.Trajectory(i).Position)
                    Next
                    views.Add(New TrackView(t.Id, t.Box, t.State = TrackState.Confirmed, t.Speed, trail))
                    If t.State = TrackState.Confirmed Then
                        If _heat IsNot Nothing Then _heat.Add(t.Center)
                        If _announced.Add(t.Id) Then Raise(AlarmSeverity.Info, "Motion", t.Id, $"حركة جسم جديد (مسار {t.Id})", t.Center)
                    End If
                Next
                result.Tracks = views

                If _opt.EnableBehavior Then
                    Dim evs As List(Of BehaviorEvent) = _behavior.Analyze(upd.Active, now)
                    result.Events = evs
                    For Each e As BehaviorEvent In evs
                        Raise(e.Severity, e.Kind.ToString(), e.TrackId, e.Message, e.Position)
                    Next
                End If

                HandleTargetLock(small, upd.Active, now, result)
                Return result
            End SyncLock
        End Function

        Private Sub HandleTargetLock(small As GrayFrame, active As IReadOnlyList(Of Track), now As DateTime, result As SmartFrameResult)
            Dim target As Track = Nothing
            Dim box As NormRect

            If _lockPoint.HasValue Then
                Dim p As NormPoint = _lockPoint.Value
                _lockPoint = Nothing
                Dim bestD As Double = 0.12
                For Each t As Track In active
                    Dim d As Double = t.Box.Center.DistanceTo(p)
                    If t.Box.Contains(p) OrElse d < bestD Then
                        target = t
                        bestD = d
                    End If
                Next
                box = If(target IsNot Nothing, target.Box, New NormRect(p.X - 0.04, p.Y - 0.06, 0.08, 0.12))
            ElseIf _autoTrackEnabled Then
                For Each t As Track In active
                    If t.State <> TrackState.Confirmed Then Continue For
                    If target Is Nothing OrElse t.Box.Area * t.Hits > target.Box.Area * target.Hits Then target = t
                Next
                If target Is Nothing Then Return
                box = target.Box
            Else
                Return
            End If

            If _auto.Lock(small, box, now) Then
                _moving = True
                _detector.SuspendUntil(DateTime.MaxValue) ' أثناء التتبع لا نشغّل فصل الخلفية (يُستأنف عبر ResumeAt)
                result.AutoTrackActive = True
            End If
        End Sub

        Private Sub ResetAfterCameraMotion(now As DateTime)
            _moving = False
            _detector.ResumeAt(now.AddSeconds(_opt.SettleSeconds))
            _tracker.Reset()
            _behavior.Reset()
            _announced.Clear()
        End Sub

        Private Sub Raise(sev As AlarmSeverity, kind As String, trackId As Integer, message As String, pos As NormPoint)
            If _bus Is Nothing Then Return
            _bus.Publish(New AlarmEvent With {.Camera = _opt.CameraName, .Kind = kind, .Severity = sev, .Message = message, .TrackId = trackId, .Position = pos})
        End Sub

        ' ---------------- الحلقة ----------------
        Public Sub Start() Implements ISmartEngine.Start
            If _task IsNot Nothing Then Return
            _cts = New Threading.CancellationTokenSource()
            Dim token As Threading.CancellationToken = _cts.Token
            _task = Task.Run(Function() RunAsync(token))
        End Sub

        Public Async Function StopAsync() As Task Implements ISmartEngine.StopAsync
            If _task Is Nothing Then Return
            _cts.Cancel()
            Try
                Await _task.ConfigureAwait(False)
            Catch ex As OperationCanceledException
            End Try
            _task = Nothing
            _cts.Dispose()
            _cts = Nothing
        End Function

        Private Async Function RunAsync(ct As Threading.CancellationToken) As Task
            Dim period As TimeSpan = TimeSpan.FromSeconds(1.0 / Math.Max(0.5, _opt.TargetFps))
            Dim failures As Integer = 0
            Dim lastSent As PtzVector = PtzVector.Zero
            While Not ct.IsCancellationRequested
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                Dim errorBackoff As TimeSpan = TimeSpan.Zero
                Try
                    Dim frame As GrayFrame = Await _source.GetFrameAsync(ct).ConfigureAwait(False)
                    Dim result As SmartFrameResult = ProcessFrame(frame)
                    If result.MoveCommand.HasValue AndAlso _mover IsNot Nothing Then
                        Dim v As PtzVector = result.MoveCommand.Value
                        If v.IsZero Then
                            If Not lastSent.IsZero Then Await _mover.StopAsync(ct).ConfigureAwait(False)
                        Else
                            Await _mover.MoveAsync(v, ct).ConfigureAwait(False)
                        End If
                        lastSent = v
                    End If
                    failures = 0
                    RaiseEvent FrameProcessed(result)
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                    Exit While
                Catch ex As Exception
                    failures += 1
                    RaiseEvent Faulted(ex, failures)
                    If failures = _opt.MaxConsecutiveFailuresBeforeAlarm Then
                        Raise(AlarmSeverity.Critical, "System", 0, "فقدان الاتصال بالكاميرا: " & ex.Message, New NormPoint(0.5, 0.5))
                    End If
                    errorBackoff = TimeSpan.FromMilliseconds(Math.Min(30000.0, _opt.FailureBackoffMs * Math.Pow(2, Math.Min(failures, 6))))
                End Try
                If errorBackoff > TimeSpan.Zero Then
                    Try
                        Await Task.Delay(errorBackoff, ct).ConfigureAwait(False)
                    Catch ex As OperationCanceledException
                        Exit While
                    End Try
                End If
                Dim remain As TimeSpan = period - sw.Elapsed
                If remain > TimeSpan.Zero Then
                    Try
                        Await Task.Delay(remain, ct).ConfigureAwait(False)
                    Catch ex As OperationCanceledException
                        Exit While
                    End Try
                End If
            End While
            ' أمان: لا نترك الكاميرا تتحرك إن توقف المحرك أثناء التتبع.
            If _moving AndAlso _mover IsNot Nothing Then
                Try
                    Using stopCts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(3))
                        Await _mover.StopAsync(stopCts.Token).ConfigureAwait(False)
                    End Using
                Catch ex As Exception
                    Diagnostics.Debug.WriteLine("Failed to stop camera at shutdown: " & ex.Message)
                End Try
            End If
        End Function
    End Class

End Namespace
