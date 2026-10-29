Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Cameras
Imports Ptz.Core.Engine
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Core.Vision
Imports Ptz.Gateway.Cloud
Imports Ptz.Gateway.Hosting
Imports Ptz.Presentation

Namespace Cameras

    ''' <summary>يلفّ فاكّ الترميز: يمرّر كل إطار مفكوك إلى FrameHub (الذي يقرر النشر) قبل التحليل.</summary>
    Friend NotInheritable Class TapDecoder
        Implements IFrameDecoder
        Private ReadOnly _inner As IFrameDecoder
        Private ReadOnly _hub As FrameHub

        Public Sub New(inner As IFrameDecoder, hub As FrameHub)
            _inner = inner
            _hub = hub
        End Sub

        Public Function DecodeToGray(jpeg As Byte()) As GrayFrame Implements IFrameDecoder.DecodeToGray
            Dim g As GrayFrame = _inner.DecodeToGray(jpeg)
            _hub.Offer(jpeg, g)
            Return g
        End Function
    End Class

    ''' <summary>
    ''' كاميرا واحدة داخل البوابة: المتحكم الخام المشترك، محرك الذكاء العامل على مدار الساعة، مركز اللقطات، موزّع SSE،
    ''' عقد PTZ، وناقل إنذارات خاص. هي الجهة الوحيدة التي تحمل بيانات اعتماد الكاميرا وتكلّمها.
    ''' </summary>
    Public NotInheritable Class CameraHost
        Implements IDisposable

        Private ReadOnly _svc As GatewayServices
        Private ReadOnly _config As CameraConfig
        Private ReadOnly _stateStore As EngineStateStore
        Private ReadOnly _lock As New Object()
        Private _cts As Threading.CancellationTokenSource
        Private _connectTask As Task
        Private _leaseTimer As Threading.Timer
        Private _lastOverlaySig As String = ""
        Private _geometry As SmartGeometry = SmartGeometry.Empty
        Private _online As Boolean
        Private _failures As Integer
        Private _breakerOpen As Boolean
        Private _device As New DeviceInfo()
        Private _lastCommandUtc As DateTime = DateTime.MinValue
        Private _leaseControllerUser As UserSession
        Private _leaseController As SecuredCameraController
        Private ReadOnly _relay As Boolean
        Private ReadOnly _cloudSerial As String
        Private _cloudHooked As HppCloud

        Public ReadOnly Property Id As String
        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property Profile As CameraProfile
        Public ReadOnly Property Bus As AlarmBus
        Public ReadOnly Property Frames As FrameHub
        Public ReadOnly Property Events As New EventHub()
        Public ReadOnly Property Lease As PtzLease
        Public ReadOnly Property GeometryGate As New Threading.SemaphoreSlim(1, 1)
        ''' <summary>يسلسل أوامر الحركة لكل كاميرا؛ الإيقاف يرفع <see cref="StopGeneration"/> قبل الانتظار فتُهمَل حركة سابقة عالقة.</summary>
        Public ReadOnly Property PtzGate As New Threading.SemaphoreSlim(1, 1)
        ''' <summary>يحدّ تزامن قراءات الكاميرا الثقيلة (صورة/موضع/مواضع) فلا يستطيع عدة مشاهدين إغراق خادم الكاميرا المدمج.</summary>
        Public ReadOnly Property ReadGate As New Threading.SemaphoreSlim(2, 2)
        Private ReadOnly _lensTimers As New Dictionary(Of String, Threading.Timer)()
        Private Const LensWatchdogMs As Integer = 3000
        Private _stopGeneration As Integer
        Public ReadOnly Property StopGeneration As Integer
            Get
                Return Threading.Volatile.Read(_stopGeneration)
            End Get
        End Property

        Public Function BumpStopGeneration() As Integer
            Return Threading.Interlocked.Increment(_stopGeneration)
        End Function

        ''' <summary>يسجّل مرة واحدة لكل (جلسة،كاميرا): عند إبطال الجلسة المالكة للعقد تُصدر البوابة إيقافاً.</summary>
        Public Sub RegisterLeaseRevoke(rec As SessionRecord)
            rec.Items.GetOrAdd("leasereg:" & Id,
                Function(k) As Object
                    Return rec.RevokedToken.Register(Sub()
                                                         If Lease.OnSessionRevoked(rec.SessionId) Then Task.Run(AddressOf SafeStopAsync)
                                                     End Sub)
                End Function)
        End Sub

        ''' <summary>
        ''' مراقب العدسة: أي أمر تركيز/حدقة غير halt يُسلَّح له مؤقّت 3 ث؛ إن لم يصل halt (ضغط 429، شبكة، انهيار العميل) توقف البوابة
        ''' العدسة بنفسها. بلا هذا تجري العدسة حتى نهايتها الميكانيكية (لا عقد/ترقيم لأوامر العدسة).
        ''' </summary>
        Public Sub ArmLensWatchdog(kind As String)
            SyncLock _lock
                Dim t As Threading.Timer = Nothing
                If _lensTimers.TryGetValue(kind, t) Then t.Dispose()
                If _cts Is Nothing Then Return
                _lensTimers(kind) = New Threading.Timer(AddressOf LensWatchdogFired, kind, LensWatchdogMs, Threading.Timeout.Infinite)
            End SyncLock
        End Sub

        Public Sub DisarmLensWatchdog(kind As String)
            SyncLock _lock
                Dim t As Threading.Timer = Nothing
                If _lensTimers.TryGetValue(kind, t) Then
                    t.Dispose()
                    _lensTimers.Remove(kind)
                End If
            End SyncLock
        End Sub

        Private Sub LensWatchdogFired(state As Object)
            Dim kind As String = CStr(state)
            DisarmLensWatchdog(kind)
            Task.Run(Function() HaltLensAsync(kind, "watchdog"))
        End Sub

        Private Async Function HaltLensAsync(kind As String, reason As String) As Task
            Dim device As ICameraController = Me.Raw
            If device Is Nothing Then Return
            Try
                Using cts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(3))
                    If kind = "focus" Then
                        Await device.MoveFocusAsync(LensDirection.Halt, 1, cts.Token).ConfigureAwait(False)
                    Else
                        Await device.MoveIrisAsync(LensDirection.Halt, 1, cts.Token).ConfigureAwait(False)
                    End If
                End Using
                _svc.Audit.Write("svc:lease", "Lens.Halt", Id, "OK", $"{kind} {reason}")
            Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is NotSupportedException
                _svc.Audit.Write("svc:lease", "Lens.Halt", Id, "FAIL", $"{kind} {reason}")
            End Try
        End Function

        ''' <summary>عند إبطال جلسة أمرت العدسة بالتحرك: توقيفها فوراً (لا ننتظر المراقب).</summary>
        Public Sub RegisterLensRevoke(rec As SessionRecord)
            rec.Items.GetOrAdd("lensreg:" & Id,
                Function(k) As Object
                    Return rec.RevokedToken.Register(Sub()
                                                         Dim active As Boolean
                                                         SyncLock _lock
                                                             active = _lensTimers.Count > 0
                                                         End SyncLock
                                                         If active Then
                                                             Task.Run(Function() HaltLensAsync("focus", "revoke"))
                                                             Task.Run(Function() HaltLensAsync("iris", "revoke"))
                                                             DisarmLensWatchdog("focus")
                                                             DisarmLensWatchdog("iris")
                                                         End If
                                                     End Sub)
                End Function)
        End Sub

        ''' <summary>تعليق كشف الحركة مؤقتاً عند تحريك الكاميرا (السياسة تُطبَّق على الخادم لا العميل).</summary>
        Public Sub NotifyMotion(Optional extraSeconds As Double = 0)
            Engine?.NotifyCameraMotion(extraSeconds)
            Frames.ForceNext()
        End Sub
        Public ReadOnly Property ViewerCount As Integer
            Get
                Return Events.SubscriberCount
            End Get
        End Property

        Public Property Raw As ICameraController
        Public Property Engine As SmartEngine
        Public Property SmartConfig As FileSmartConfigRepository

        Public Sub New(svc As GatewayServices, config As CameraConfig)
            _svc = svc
            _config = config
            _relay = String.Equals(config.Transport, "hpp", StringComparison.OrdinalIgnoreCase)
            _cloudSerial = If(config.CloudSerial, "")
            Id = config.Key
            DisplayName = If(String.IsNullOrWhiteSpace(config.DisplayName), config.Key, config.DisplayName)
            Profile = If(config.ModelName = CameraProfiles.Ds2De7232iwAe.ModelName, CameraProfiles.Ds2De7232iwAe, CameraProfiles.Ds2Se7c432mwAeb)
            Bus = New AlarmBus(svc.Clock, 5.0)
            Lease = New PtzLease(svc.Clock)
            Frames = New FrameHub(svc.Options.Frames, svc.Transcoder, svc.Clock, Function() RecentPtzActivity())
            _stateStore = New EngineStateStore(svc.Options.DataDir, Id)
            _device.Model = Profile.ModelName
            For Each sink As IAlarmSink In svc.AlarmSinks
                Bus.AddSink(sink)
            Next
            Bus.Subscribe(AddressOf OnAlarm)
        End Sub

        Public ReadOnly Property Online As Boolean
            Get
                SyncLock _lock
                    Return _online
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property ConsecutiveFailures As Integer
            Get
                SyncLock _lock
                    Return _failures
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property Device As DeviceInfo
            Get
                SyncLock _lock
                    Return _device
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property Features As CameraFeatures
            Get
                If Raw Is Nothing Then Return If(_relay, Profile.DefaultFeatures And CameraTransports.RelayFeatures, Profile.DefaultFeatures)
                Return Raw.Features
            End Get
        End Property

        ''' <summary>True حين تُدار الكاميرا عبر Hik-Partner Pro (حركة ومواضع مسبقة فقط؛ بلا فيديو ولا محرك ذكي).</summary>
        Public ReadOnly Property IsRelay As Boolean
            Get
                Return _relay
            End Get
        End Property

        Private Function RecentPtzActivity() As Boolean
            Return (_svc.Clock.UtcNow - Lease.LastActivityUtc).TotalSeconds < 3.0 OrElse (Engine IsNot Nothing AndAlso Engine.AutoTrackEnabled)
        End Function

        ' ---------------- دورة الحياة ----------------
        Public Function StartAsync(ct As Threading.CancellationToken) As Task
            SyncLock _lock
                If _cts IsNot Nothing Then Return Task.CompletedTask
                _cts = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
            End SyncLock
            Dim conn As CameraConnection = _svc.CameraConfigs.BuildConnection(Id)
            Dim handler As Net.Http.HttpMessageHandler = _svc.CameraHandlerFactory(conn)
            Dim guard As CameraGuardHandler = TryCast(handler, CameraGuardHandler)
            If guard IsNot Nothing Then AddHandler guard.BreakerOpened, AddressOf OnBreakerOpened
            Raw = CameraControllerFactory.Create(Profile, conn, handler)
            If _relay AndAlso _svc.Hpp IsNot Nothing Then
                _cloudHooked = _svc.Hpp
                AddHandler _cloudHooked.CallCompleted, AddressOf OnCloudCall
            End If

            Dim opts As New SmartEngineOptions With {.CameraName = DisplayName}
            EnginePresets.Apply(Profile, opts)
            Dim source As New SnapshotFrameSource(Raw, New TapDecoder(_svc.Decoder, Frames), _svc.Clock)
            _leaseControllerUser = New UserSession("svc:lease", Role.Operator, _svc.Clock.UtcNow, TimeSpan.FromDays(3650))
            _leaseController = New SecuredCameraController(Raw, _leaseControllerUser, _svc.Access, _svc.Audit, _svc.Clock, ownsInner:=False)
            Dim autoUser As New UserSession("svc:autotrack", Role.Operator, _svc.Clock.UtcNow, TimeSpan.FromDays(3650))
            Dim autoController As New SecuredCameraController(Raw, autoUser, _svc.Access, _svc.Audit, _svc.Clock, ownsInner:=False)
            Dim recorder As New TrajectoryRecorder(IO.Path.Combine(_svc.Options.DataDir, "trajectories"), DisplayName)
            Engine = New SmartEngine(opts, source, autoController.AsAutoTrackMover(), Bus, recorder, New HeatMap(), _svc.Clock)
            SmartConfig = New FileSmartConfigRepository(Engine, _svc.Options.DataDir, Id)
            AddHandler Engine.FrameProcessed, AddressOf OnFrameProcessed
            AddHandler Engine.Faulted, AddressOf OnFaulted

            Dim state As EngineStateStore.Model = _stateStore.Load()
            Engine.MotionEnabled = state.MotionEnabled
            Engine.BehaviorEnabled = state.BehaviorEnabled
            Engine.AutoTrackEnabled = state.AutoTrackEnabled
            If state.Behavior IsNot Nothing Then
                Try
                    Engine.ApplyBehaviorOptions(WireMap.ToBehavior(state.Behavior))
                Catch ex As WireValidationException
                    ' ملف حالة تالف: نبقي القيم الافتراضية.
                End Try
            End If
            _geometry = SmartConfig.LoadAsync(ct).GetAwaiter().GetResult()

            _leaseTimer = New Threading.Timer(AddressOf LeaseTick, Nothing, 100, 100)
            Dim token As Threading.CancellationToken = _cts.Token
            _connectTask = Task.Run(Function() ConnectLoopAsync(conn, handler, token))
            Return Task.CompletedTask
        End Function

        Private Async Function ConnectLoopAsync(conn As CameraConnection, handler As Net.Http.HttpMessageHandler, ct As Threading.CancellationToken) As Task
            Dim attempt As Integer = 0
            While Not ct.IsCancellationRequested
                Dim info As DeviceInfo = Nothing
                Dim failed As Boolean = False
                Try
                    info = Await Raw.ConnectAsync(ct).ConfigureAwait(False)
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                    Return
                Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is System.Xml.XmlException
                    ' XmlException: ردّ تالف من الجهاز/السحابة لا يجوز أن يُسقط حلقة الاتصال بصمت (لا إعادة محاولة بعدها).
                    failed = True
                End Try
                If Not failed Then
                    SyncLock _lock
                        _device = info
                        _online = True
                        _failures = 0
                    End SyncLock
                    PublishHealth()
                    Exit While
                End If
                attempt += 1
                SyncLock _lock
                    _failures = attempt
                End SyncLock
                PublishHealth()
                Dim backoff As Double = Math.Min(60.0, Math.Pow(2, Math.Min(attempt, 6)))
                Dim jitter As Double = 0.8 + 0.4 * ((attempt * 37) Mod 100) / 100.0
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(backoff * jitter), ct).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    Return
                End Try
            End While
            If ct.IsCancellationRequested Then Return
            If _relay Then
                ' لا لقطات عبر السحابة ⇒ لا محرك ذكي ولا جسر إنذارات؛ نراقب الاتصال فقط ونستعيده عند الانقطاع.
                Await RelayRecoveryLoopAsync(ct).ConfigureAwait(False)
                Return
            End If
            If _svc.Options.StartEngines Then Engine.Start()
            If (Raw.Features And CameraFeatures.AlertStream) <> 0 Then
                Try
                    Await DeviceAlertBridge.RunAsync(conn, handler, DisplayName, Bus, ct).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                End Try
            End If
        End Function

        Public Async Function StopAsync() As Task
            Dim cts As Threading.CancellationTokenSource
            SyncLock _lock
                cts = _cts
                _cts = Nothing
            End SyncLock
            If cts Is Nothing Then Return
            cts.Cancel()
            If _cloudHooked IsNot Nothing Then
                RemoveHandler _cloudHooked.CallCompleted, AddressOf OnCloudCall
                _cloudHooked = Nothing
            End If
            _leaseTimer?.Dispose()
            _leaseTimer = Nothing
            _forceTimer?.Dispose()
            _forceTimer = Nothing
            Dim lensKinds As String()
            SyncLock _lock
                lensKinds = _lensTimers.Keys.ToArray()
            End SyncLock
            For Each k As String In lensKinds
                DisarmLensWatchdog(k)
                Await HaltLensAsync(k, "shutdown").ConfigureAwait(False)
            Next
            If Lease.IsActive Then
                Lease.OnStop(Guid.Empty)
                Await SafeStopAsync().ConfigureAwait(False)
            End If
            If Engine IsNot Nothing Then
                Try
                    Await Engine.StopAsync().ConfigureAwait(False)
                Catch ex As OperationCanceledException
                End Try
                Try
                    Await SmartConfig.SaveAnomalyModelAsync(Threading.CancellationToken.None).ConfigureAwait(False)
                Catch ex As IO.IOException
                End Try
            End If
            If _connectTask IsNot Nothing Then
                Try
                    Await _connectTask.ConfigureAwait(False)
                Catch ex As Exception When Not (TypeOf ex Is OutOfMemoryException)
                    ' أي عطل سابق في حلقة الاتصال لا يمنع إكمال الإيقاف (التخلص من الاتصال وإيقاف بقية الكاميرات).
                    Diagnostics.Debug.WriteLine("connect loop ended with " & ex.GetType().Name)
                End Try
            End If
            Raw?.Dispose()
            cts.Dispose()
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            StopAsync().GetAwaiter().GetResult()
        End Sub

        ' ---------------- أمان الحركة (العقد) ----------------
        Private Sub LeaseTick(state As Object)
            Try
                If Lease.Tick() Then Task.Run(AddressOf SafeStopAsync)
            Catch ex As Exception
                Diagnostics.Debug.WriteLine("lease tick failed: " & ex.Message)
            End Try
        End Sub

        ''' <summary>يوقف الكاميرا بإعادة محاولة (3×200ms) عبر مستخدم الخدمة svc:lease كي يُدقَّق.</summary>
        ''' <summary>مهلة إجمالية لمحاولات الإيقاف عبر السحابة (أقل من مهلة إيقاف الخدمة 20 ث كي يكتمل الإنذار والإيقاف قبل القتل).</summary>
        Public Shared Property RelayStopDeadlineSeconds As Double = 15

        Public Async Function SafeStopAsync() As Task
            If _leaseController Is Nothing Then Return
            ' عبر السحابة لا توجد مهلة حركة على الكاميرا (momentary غير موثَّق) ⇒ الإيقاف الضائع يعني دوراناً مستمراً:
            ' نعيد المحاولة بتراجع أسّي ضمن مهلة إجمالية ثم ننبّه المشغّل بإنذار حرج إن فشلت كلها.
            ' إن بدأ أحدٌ حركة جديدة أثناء المحاولات (عقد نشط من جديد) نتوقف: عقده هو الذي يحرسها الآن، ولا نقطع حركته بإيقاف متأخر.
            Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim deadline As TimeSpan = TimeSpan.FromSeconds(If(_relay, RelayStopDeadlineSeconds, 3))
            Dim attempts As Integer = 0
            Dim i As Integer = 0
            While True
                i += 1
                If i > 1 AndAlso Lease.IsActive Then Return
                Dim remaining As TimeSpan = deadline - sw.Elapsed
                If i > 1 AndAlso remaining <= TimeSpan.Zero Then Exit While
                If i > If(_relay, 12, 3) Then Exit While
                attempts = i
                Dim ok As Boolean = False
                Dim perAttempt As TimeSpan = TimeSpan.FromSeconds(If(_relay, 5, 3))
                If _relay AndAlso remaining < perAttempt AndAlso remaining > TimeSpan.FromSeconds(1) Then perAttempt = remaining
                Try
                    Using cts As New Threading.CancellationTokenSource(perAttempt)
                        Await _leaseController.StopAsync(cts.Token).ConfigureAwait(False)
                    End Using
                    ok = True
                Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is NotSupportedException
                    ok = False
                End Try
                If ok Then Return
                Dim pause As TimeSpan = TimeSpan.FromMilliseconds(If(_relay, Math.Min(2000, 250 * (1 << Math.Min(i - 1, 4))), 200))
                If _relay AndAlso sw.Elapsed + pause >= deadline Then Exit While
                Await Task.Delay(pause).ConfigureAwait(False)
            End While
            If _relay Then
                Bus.Publish(New AlarmEvent With {.Camera = DisplayName, .Kind = "System", .Severity = AlarmSeverity.Critical,
                                                  .Message = "تعذّر إيقاف حركة الكاميرا عبر Hik-Partner Pro بعد عدة محاولات؛ قد تستمر بالدوران. أوقفها من تطبيق الشركة أو افحص الاتصال."})
                _svc.Audit.Write("svc:lease", "Ptz.StopFailed", Id, "FAIL", $"attempts={attempts} elapsedMs={CLng(sw.Elapsed.TotalMilliseconds)}")
            End If
        End Function

        ''' <summary>
        ''' بعد إيقاف صريح فشل: العقد أُلغي قبل النداء فلن يطلق إيقافاً بنفسه، فنعيد الإيقاف في الخلفية بمحاولات (وإنذار عبر السحابة إن فشلت).
        ''' بلا هذا يبقى ترحيل سحابي (لا مهلة حركة على الجهاز) يدور بعد إغلاق العميل أو فشل نداء واحد.
        ''' </summary>
        Public Sub ScheduleStopRetry()
            Dim t As Task = Task.Run(AddressOf SafeStopAsync)
        End Sub

        ''' <summary>المتحكم المؤمَّن لهذه الجلسة (مخبّأ لتعمل خانقة تدقيق الحركة 1 ث).</summary>
        Public Function SecuredFor(rec As SessionRecord) As SecuredCameraController
            Return CType(rec.Items.GetOrAdd("ctl:" & Id, Function(k) New SecuredCameraController(Raw, rec.UserSession, _svc.Access, _svc.Audit, _svc.Clock, ownsInner:=False)), SecuredCameraController)
        End Function

        ' ---------------- النقل السحابي: الصحة والاستعادة ----------------
        ''' <summary>
        ''' نتيجة نداء سحابي لهذه الكاميرا: النجاح يعيدها متصلة؛ انقطاع الجهاز مرتين متتاليتين يعلنها غير متصلة.
        ''' تقييد المعدل أو رفض الأمر لا يغيّران الحالة (ليسا انقطاعاً)، ورفض المفاتيح يتولاه قاطع الدائرة.
        ''' </summary>
        Private Sub OnCloudCall(sender As Object, e As HppCallEventArgs)
            If Not String.Equals(e.Serial, _cloudSerial, StringComparison.Ordinal) Then Return
            Dim changed As Boolean = False
            SyncLock _lock
                Select Case e.Outcome
                    Case HppOutcome.Ok
                        _failures = 0
                        _breakerOpen = False
                        If Not _online AndAlso Raw IsNot Nothing Then
                            _online = True
                            changed = True
                        End If
                    Case HppOutcome.DeviceUnreachable
                        _failures += 1
                        If _failures >= 2 AndAlso _online Then
                            _online = False
                            changed = True
                        End If
                End Select
            End SyncLock
            If changed Then PublishHealth()
        End Sub

        ''' <summary>دورية محاولة استعادة كاميرا الترحيل بعد الانقطاع (ثوانٍ؛ قابلة للتعديل للاختبارات).</summary>
        Public Shared Property RelayRecoveryIntervalSeconds As Double = 10

        Private Async Function RelayRecoveryLoopAsync(ct As Threading.CancellationToken) As Task
            While Not ct.IsCancellationRequested
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(RelayRecoveryIntervalSeconds), ct).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    Return
                End Try
                If Online Then Continue While
                Dim info As DeviceInfo = Nothing
                Try
                    info = Await Raw.ConnectAsync(ct).ConfigureAwait(False)
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                    Return
                Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse TypeOf ex Is System.Xml.XmlException
                    info = Nothing
                End Try
                If info IsNot Nothing Then
                    SyncLock _lock
                        _device = info
                        _online = True
                        _failures = 0
                    End SyncLock
                    PublishHealth()
                End If
            End While
        End Function

        ' ---------------- الأحداث ----------------
        Private Sub OnBreakerOpened()
            SyncLock _lock
                _breakerOpen = True
                _online = False
            End SyncLock
            Bus.Publish(New AlarmEvent With {.Camera = DisplayName, .Kind = "System", .Severity = AlarmSeverity.Critical,
                                              .Message = "رفضت الكاميرا بيانات اعتماد البوابة مرتين؛ فُتح القاطع 5 دقائق."})
            PublishHealth()
        End Sub

        Private Sub OnAlarm(alarm As AlarmEvent)
            Dim dto As AlarmDto = _svc.Alarms.Add(Id, alarm)
            Events.PublishControl(New SseMessage("alarm", dto.Seq.ToString(Globalization.CultureInfo.InvariantCulture), WireJson.Serialize(dto)))
        End Sub

        Private Sub OnFrameProcessed(result As SmartFrameResult)
            Dim recovered As Boolean = False
            SyncLock _lock
                _failures = 0
                _breakerOpen = False
                If Not _online Then
                    _online = True
                    recovered = True
                End If
            End SyncLock
            If recovered Then PublishHealth()
            Frames.NotifyResult(result.Tracks.Count > 0)
            Dim ov As OverlayWire = WireMap.ToOverlay(result, Frames.Seq)
            Dim sig As String = WireJson.Serialize(New With {ov.Fl, ov.Tr, ov.Ev})
            If sig = _lastOverlaySig Then Return
            _lastOverlaySig = sig
            Events.PublishOverlay(New SseMessage("ov", Nothing, WireJson.Serialize(ov)))
        End Sub

        Private Sub OnFaulted(ex As Exception, count As Integer)
            Dim changed As Boolean
            SyncLock _lock
                _failures = count
                changed = _online AndAlso count >= 2
                If count >= 2 Then _online = False
            End SyncLock
            If changed Then PublishHealth()
        End Sub

        Public Sub PublishHealth()
            Dim online As Boolean
            Dim fails As Integer
            Dim breaker As Boolean
            SyncLock _lock
                online = _online
                fails = _failures
                breaker = _breakerOpen
            End SyncLock
            Dim reason As String = If(online, "ok", If(breaker, "cameraAuth", "cameraUnreachable"))
            Events.PublishControl(New SseMessage("health", Nothing, WireJson.Serialize(New HealthWire With {.Online = online, .Reason = reason, .ConsecutiveFailures = fails})))
        End Sub

        ' ---------------- تعلّم الشذوذ المفروض: يعود تلقائياً بعد 60 دقيقة (حتى لا يُترك الكشف معطّلاً بصمت) ----------------
        Private _forceTimer As Threading.Timer

        Public Sub OnForceLearningChanged(value As Boolean?)
            _forceTimer?.Dispose()
            _forceTimer = Nothing
            If value.HasValue AndAlso value.Value Then
                _forceTimer = New Threading.Timer(Sub(st)
                                                      Engine.ForceAnomalyLearning = Nothing
                                                      PublishState("svc:gateway")
                                                  End Sub, Nothing, TimeSpan.FromMinutes(60), Threading.Timeout.InfiniteTimeSpan)
            End If
        End Sub

        ' ---------------- حالة المحرك والهندسة ----------------
        Public Function EngineState() As EngineStateDto
            Return New EngineStateDto With {.MotionEnabled = Engine?.MotionEnabled, .BehaviorEnabled = Engine?.BehaviorEnabled,
                                            .AutoTrackEnabled = Engine?.AutoTrackEnabled, .ForceAnomalyLearning = Engine?.ForceAnomalyLearning}
        End Function

        Public Sub PersistEngineState()
            If Engine Is Nothing Then Return
            _stateStore.Save(New EngineStateStore.Model With {
                .MotionEnabled = Engine.MotionEnabled, .BehaviorEnabled = Engine.BehaviorEnabled, .AutoTrackEnabled = Engine.AutoTrackEnabled,
                .Behavior = WireMap.FromBehavior(Engine.SnapshotBehaviorOptions())})
        End Sub

        Public Sub PublishState(by As String)
            Events.PublishControl(New SseMessage("state", Nothing, WireJson.Serialize(New StateWire With {
                .MotionEnabled = Engine.MotionEnabled, .BehaviorEnabled = Engine.BehaviorEnabled, .AutoTrackEnabled = Engine.AutoTrackEnabled,
                .ForceAnomalyLearning = Engine.ForceAnomalyLearning, .By = by})))
        End Sub

        Public ReadOnly Property Geometry As SmartGeometry
            Get
                SyncLock _lock
                    Return _geometry
                End SyncLock
            End Get
        End Property

        Public Sub SetGeometry(g As SmartGeometry)
            SyncLock _lock
                _geometry = g
            End SyncLock
        End Sub

        ''' <summary>ETag الهندسة: "g-" + أول 16 hex من SHA-256 للـ JSON القانوني.</summary>
        Public Function GeometryRev() As String
            Return "g-" & Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WireJson.Serialize(WireMap.FromGeometry(Geometry))))).Substring(0, 16).ToLowerInvariant()
        End Function

        Public Function ToDto() As CameraDto
            Dim age As Long? = Frames.AgeMs()
            Return New CameraDto With {
                .Id = Id, .DisplayName = DisplayName, .Model = Profile.ModelName, .Online = Online, .LastFrameAgeMs = age,
                .Features = WireMap.FeatureNames(Features),
                .Profile = New ProfileDto With {.ModelName = Profile.ModelName, .ResolutionWidth = Profile.ResolutionWidth, .ResolutionHeight = Profile.ResolutionHeight,
                                                .OpticalZoomMax = Profile.OpticalZoomMax, .MaxPresets = Profile.MaxPresets, .MaxPatrols = Profile.MaxPatrols, .MaxPatterns = Profile.MaxPatterns},
                .Engine = EngineState(), .Viewers = ViewerCount}
        End Function
    End Class

End Namespace
