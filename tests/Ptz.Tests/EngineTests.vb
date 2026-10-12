Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Vision
Imports Xunit

Public Class EngineTests

    Private Shared Function NewEngine(bus As AlarmBus, Optional name As String = "camA", Optional mover As IPtzMover = Nothing,
                                      Optional recorder As TrajectoryRecorder = Nothing) As SmartEngine
        Dim o As New SmartEngineOptions With {.CameraName = name, .ProcessWidth = 160, .AnomalyLearningSeconds = 0}
        o.Motion.Subtractor.WarmupFrames = 6
        Return New SmartEngine(o, Nothing, mover, bus, recorder)
    End Function

    Private Shared Function Warmup(e As SmartEngine, ByRef t As Double) As SmartFrameResult
        Dim r As SmartFrameResult = Nothing
        For i As Integer = 0 To 9
            r = e.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t), 200 + i))
            t += 0.2
        Next
        Return r
    End Function

    <Fact>
    Public Sub Engine_MovingObject_RaisesOneMotionAlarmPerTrack()
        Dim bus As New AlarmBus(cooldownSeconds:=0)
        Dim alarms As New List(Of AlarmEvent)()
        bus.Subscribe(Sub(a) alarms.Add(a))
        Dim e As SmartEngine = NewEngine(bus)
        Dim t As Double = 0
        Warmup(e, t)
        Dim last As SmartFrameResult = Nothing
        For i As Integer = 0 To 14
            last = e.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t), 300 + i, (X:=20 + i * 4, Y:=35, Size:=14, Value:=220)))
            t += 0.2
        Next
        Assert.NotEmpty(last.Tracks)
        Assert.Equal(1, alarms.Where(Function(a) a.Kind = "Motion").Count())
        Assert.Equal("camA", alarms(0).Camera)
    End Sub

    <Fact>
    Public Sub Engine_ObjectEntersRestrictedZone_RaisesCriticalIntrusion()
        Dim bus As New AlarmBus(cooldownSeconds:=0)
        Dim alarms As New List(Of AlarmEvent)()
        bus.Subscribe(Sub(a) alarms.Add(a))
        Dim e As SmartEngine = NewEngine(bus)
        e.SetZones({New RestrictedZone("Vault", {New NormPoint(0.5, 0.1), New NormPoint(0.95, 0.1), New NormPoint(0.95, 0.95), New NormPoint(0.5, 0.95)})})
        Dim t As Double = 0
        Warmup(e, t)
        For i As Integer = 0 To 24
            e.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t), 400 + i, (X:=10 + i * 4, Y:=35, Size:=14, Value:=220)))
            t += 0.2
        Next
        Dim intr As AlarmEvent = alarms.FirstOrDefault(Function(a) a.Kind = "Intrusion")
        Assert.NotNull(intr)
        Assert.Equal(AlarmSeverity.Critical, intr.Severity)
    End Sub

    <Fact>
    Public Sub Engine_SuddenLightingChange_RaisesNoMotionAlarm()
        Dim bus As New AlarmBus(cooldownSeconds:=0)
        Dim alarms As New List(Of AlarmEvent)()
        bus.Subscribe(Sub(a) alarms.Add(a))
        Dim e As SmartEngine = NewEngine(bus)
        Dim t As Double = 0
        Warmup(e, t)
        Dim r As SmartFrameResult = e.ProcessFrame(TestFrames.Scene(160, 90, 200, TestFrames.T0.AddSeconds(t), 1))
        Assert.True(r.IlluminationChange)
        Assert.Empty(alarms)
    End Sub

    <Fact>
    Public Sub Engine_NotifyCameraMotion_SuspendsDetection()
        Dim e As SmartEngine = NewEngine(New AlarmBus())
        Dim clock As DateTime = DateTime.UtcNow
        e.NotifyCameraMotion(5)
        Dim r As SmartFrameResult = e.ProcessFrame(TestFrames.Scene(160, 90, 90, clock.AddSeconds(1), 1))
        Assert.True(r.Suspended)
        Assert.Empty(r.Tracks)
    End Sub

    <Fact>
    Public Sub Engine_AutoTrack_LocksMovingTarget_DrivesPanTowardIt_ThenReportsLost()
        Dim bus As New AlarmBus(cooldownSeconds:=0)
        Dim alarms As New List(Of AlarmEvent)()
        bus.Subscribe(Sub(a) alarms.Add(a))
        Dim e As SmartEngine = NewEngine(bus)
        e.AutoTrackEnabled = True
        Dim t As Double = 0
        Warmup(e, t)
        Dim lockedAt As Integer = -1
        Dim lastMove As PtzVector? = Nothing
        For i As Integer = 0 To 30
            Dim x As Integer = 20 + i * 3
            Dim r As SmartFrameResult = e.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t), 500 + i, (X:=x, Y:=35, Size:=16, Value:=200)))
            t += 0.2
            If r.AutoTrackActive AndAlso lockedAt < 0 Then lockedAt = i
            If r.MoveCommand.HasValue Then lastMove = r.MoveCommand
            If lockedAt >= 0 AndAlso i > lockedAt + 3 Then Exit For
        Next
        Assert.True(lockedAt >= 0, "لم يُقفل على الهدف")
        Assert.True(lastMove.HasValue)
        Assert.True(lastMove.Value.Pan < 0, "الهدف على يسار المركز فيجب أن تدور الكاميرا يساراً")

        ' اختفاء الهدف تماماً حتى انتهاء المهلة
        Dim lost As Boolean = False
        For i As Integer = 0 To 20
            Dim r As SmartFrameResult = e.ProcessFrame(TestFrames.Scene(160, 90, 20, TestFrames.T0.AddSeconds(t), 900 + i))
            t += 0.2
            If r.MoveCommand.HasValue AndAlso r.MoveCommand.Value.IsZero AndAlso Not r.AutoTrackActive Then
                lost = True
                Exit For
            End If
        Next
        Assert.True(lost)
        Assert.Contains(alarms, Function(a) a.Kind = "AutoTrack")
    End Sub

    <Fact>
    Public Sub Engine_ManualLock_OnTrackedObject_StartsFollowing()
        Dim e As SmartEngine = NewEngine(New AlarmBus())
        Dim t As Double = 0
        Warmup(e, t)
        Dim center As NormPoint
        For i As Integer = 0 To 5
            Dim x As Integer = 40 + i * 3
            Dim r As SmartFrameResult = e.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t), 600 + i, (X:=x, Y:=30, Size:=16, Value:=200)))
            t += 0.2
            If r.Tracks.Count > 0 Then center = r.Tracks(0).Box.Center
        Next
        e.LockTargetAt(center)
        Dim active As Boolean = False
        For i As Integer = 6 To 9
            Dim r As SmartFrameResult = e.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t), 600 + i, (X:=40 + i * 3, Y:=30, Size:=16, Value:=200)))
            t += 0.2
            active = active OrElse r.AutoTrackActive
        Next
        Assert.True(active)
    End Sub

    <Fact>
    Public Async Function Engine_FailingSource_RaisesFaults_ThenCriticalAlarm_AndStopsCleanly() As Task
        Dim bus As New AlarmBus(cooldownSeconds:=0)
        Dim alarms As New List(Of AlarmEvent)()
        bus.Subscribe(Sub(a) alarms.Add(a))
        Dim o As New SmartEngineOptions With {.CameraName = "camB", .TargetFps = 100, .MaxConsecutiveFailuresBeforeAlarm = 3, .FailureBackoffMs = 5}
        Dim faults As Integer = 0
        Dim e As New SmartEngine(o, New FailingSource(), Nothing, bus)
        AddHandler e.Faulted, Sub(ex, n) Threading.Interlocked.Increment(faults)
        e.Start()
        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While sw.Elapsed.TotalSeconds < 5 AndAlso faults < 4
            Await Task.Delay(20)
        End While
        Await e.StopAsync()
        Assert.True(faults >= 4)
        Assert.Contains(alarms, Function(a) a.Kind = "System" AndAlso a.Severity = AlarmSeverity.Critical)
    End Function

    Private NotInheritable Class FailingSource
        Implements IFrameSource
        Public Function GetFrameAsync(ct As Threading.CancellationToken) As Task(Of GrayFrame) Implements IFrameSource.GetFrameAsync
            Throw New IO.IOException("camera offline")
        End Function
    End Class

    Private NotInheritable Class CountingMover
        Implements IPtzMover
        Public Moves As Integer
        Public Stops As Integer
        Public Function MoveAsync(vector As PtzVector, ct As Threading.CancellationToken) As Task Implements IPtzMover.MoveAsync
            Threading.Interlocked.Increment(Moves)
            Return Task.CompletedTask
        End Function
        Public Function StopAsync(ct As Threading.CancellationToken) As Task Implements IPtzMover.StopAsync
            Threading.Interlocked.Increment(Stops)
            Return Task.CompletedTask
        End Function
    End Class

    <Fact>
    Public Async Function Engine_Loop_ProcessesScriptedFrames_AndDeliversResults() As Task
        Dim frames As New List(Of GrayFrame)()
        For i As Integer = 0 To 14
            frames.Add(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(i * 0.2), i))
        Next
        Dim o As New SmartEngineOptions With {.CameraName = "camC", .TargetFps = 200}
        Dim e As New SmartEngine(o, New ScriptedFrameSource(frames), New CountingMover(), New AlarmBus())
        Dim processed As Integer = 0
        AddHandler e.FrameProcessed, Sub(r) Threading.Interlocked.Increment(processed)
        e.Start()
        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While sw.Elapsed.TotalSeconds < 5 AndAlso processed < 15
            Await Task.Delay(10)
        End While
        Await e.StopAsync()
        Assert.Equal(15, processed)
    End Function

    <Fact>
    Public Sub TwoEngines_AreIsolated_PerCameraNames()
        Dim bus As New AlarmBus(cooldownSeconds:=0)
        Dim alarms As New List(Of AlarmEvent)()
        bus.Subscribe(Sub(a) alarms.Add(a))
        Dim e1 As SmartEngine = NewEngine(bus, "cam1")
        Dim e2 As SmartEngine = NewEngine(bus, "cam2")
        Dim t1 As Double = 0
        Dim t2 As Double = 0
        Warmup(e1, t1)
        Warmup(e2, t2)
        For i As Integer = 0 To 8
            e1.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t1), 700 + i, (X:=20 + i * 4, Y:=35, Size:=14, Value:=220)))
            t1 += 0.2
            e2.ProcessFrame(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(t2), 800 + i))
            t2 += 0.2
        Next
        Assert.All(alarms, Sub(a) Assert.Equal("cam1", a.Camera))
        Assert.NotEmpty(alarms)
    End Sub

    ' ---------- تسجيل المسارات ----------
    <Fact>
    Public Sub TrajectoryRecorder_WritesJsonlWithDownsampledPoints()
        Dim dir As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-traj-" & Guid.NewGuid().ToString("N"))
        Dim rec As New TrajectoryRecorder(dir, "camA", 10)
        Dim tr As New ObjectTracker()
        Dim last As Track = Nothing
        For i As Integer = 0 To 59
            tr.Update({New Detection(New NormRect(0.1 + i * 0.01, 0.4, 0.05, 0.05), 50)}, TestFrames.T0.AddSeconds(i * 0.2))
        Next
        last = tr.Tracks(0)
        rec.Record(last)
        Dim file As String = IO.Directory.GetFiles(dir, "trajectories-*.jsonl").Single()
        Dim doc As Text.Json.JsonDocument = Text.Json.JsonDocument.Parse(IO.File.ReadAllLines(file).Single())
        Assert.Equal("camA", doc.RootElement.GetProperty("camera").GetString())
        Assert.InRange(doc.RootElement.GetProperty("points").GetArrayLength(), 5, 12)
        Assert.True(doc.RootElement.GetProperty("pathLength").GetDouble() > 0.3)
    End Sub

    <Fact>
    Public Sub HeatMap_AccumulatesAndDecays()
        Dim h As New HeatMap(4, 4)
        h.Add(New NormPoint(0.1, 0.1))
        h.Add(New NormPoint(0.1, 0.1))
        h.Add(New NormPoint(1.0, 1.0))  ' الحد الأقصى يُقصّ داخل الشبكة
        Assert.Equal(2.0, h.Snapshot()(0))
        Assert.Equal(1.0, h.Snapshot()(15))
        h.Decay(0.5)
        Assert.Equal(1.0, h.Snapshot()(0))
    End Sub

    ' ---------- ناقل الإنذارات ----------
    <Fact>
    Public Sub AlarmBus_DedupesWithinCooldown_ButNeverSuppressesCritical()
        Dim clock As New TestClock()
        Dim bus As New AlarmBus(clock, 5)
        Dim n As Integer = 0
        bus.Subscribe(Sub(a) n += 1)
        Dim mk As Func(Of AlarmSeverity, AlarmEvent) = Function(s) New AlarmEvent With {.Camera = "c", .Kind = "Motion", .TrackId = 1, .Severity = s}
        Assert.True(bus.Publish(mk(AlarmSeverity.Info)))
        Assert.False(bus.Publish(mk(AlarmSeverity.Info)))
        Assert.True(bus.Publish(mk(AlarmSeverity.Critical)))
        clock.Advance(6)
        Assert.True(bus.Publish(mk(AlarmSeverity.Info)))
        Assert.Equal(3, n)
    End Sub

    <Fact>
    Public Sub AlarmBus_FaultyHandlerAndSink_DoNotBreakOthers_AndHistoryIsBounded()
        Dim bus As New AlarmBus(New TestClock(), 0, 5)
        Dim good As Integer = 0
        bus.Subscribe(Sub(a) Throw New InvalidOperationException("bad handler"))
        Dim sub_ As IDisposable = bus.Subscribe(Sub(a) good += 1)
        For i As Integer = 0 To 9
            bus.Publish(New AlarmEvent With {.Camera = "c", .Kind = "K", .TrackId = i})
        Next
        Assert.Equal(10, good)
        Assert.Equal(5, bus.Recent(100).Count)
        sub_.Dispose()
        bus.Publish(New AlarmEvent With {.Camera = "c", .Kind = "K", .TrackId = 99})
        Assert.Equal(10, good)
        Dim id As Guid = bus.Recent(1)(0).Id
        Assert.True(bus.Acknowledge(id))
        Assert.True(bus.Recent(1)(0).Acknowledged)
        Assert.False(bus.Acknowledge(Guid.NewGuid()))
    End Sub

    <Fact>
    Public Sub FileAlarmSink_WritesOneJsonLinePerAlarm()
        Dim path As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-alarm-" & Guid.NewGuid().ToString("N") & ".jsonl")
        Dim bus As New AlarmBus(cooldownSeconds:=0)
        bus.AddSink(New FileAlarmSink(path))
        bus.Publish(New AlarmEvent With {.Camera = "c", .Kind = "Intrusion", .Severity = AlarmSeverity.Critical, .Message = "x" & vbLf & "y"})
        Assert.Single(IO.File.ReadAllLines(path))
    End Sub

    Private NotInheritable Class CaptureHandler
        Inherits HttpMessageHandler
        Public ReadOnly Captured As New Collections.Concurrent.ConcurrentQueue(Of (Body As String, Ts As String, Sig As String))()
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
            Dim body As String = Await request.Content.ReadAsStringAsync(cancellationToken)
            Captured.Enqueue((body, request.Headers.GetValues("X-Timestamp").First(), request.Headers.GetValues("X-Signature").First()))
            Return New HttpResponseMessage(Net.HttpStatusCode.OK)
        End Function
    End Class

    <Fact>
    Public Sub Webhook_RequiresHttpsAndStrongSecret()
        Dim secret As Byte() = New Byte(31) {}
        Assert.Throws(Of ArgumentException)(Function() New WebhookAlarmSink(New Uri("http://siem.local/hook"), secret))
        Assert.Throws(Of ArgumentException)(Function() New WebhookAlarmSink(New Uri("https://siem.local/hook"), New Byte(3) {}))
    End Sub

    <Fact>
    Public Async Function Webhook_PostsSignedPayload_VerifiableByReceiver() As Task
        Dim secret As Byte() = System.Text.Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef")
        Dim cap As New CaptureHandler()
        Using sink As New WebhookAlarmSink(New Uri("https://siem.local/hook"), secret, cap)
            sink.Accept(New AlarmEvent With {.Camera = "c", .Kind = "Intrusion", .Severity = AlarmSeverity.Critical, .Message = "m"})
            Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            While cap.Captured.IsEmpty AndAlso sw.Elapsed.TotalSeconds < 5
                Await Task.Delay(10)
            End While
        End Using
        Dim item As (Body As String, Ts As String, Sig As String) = Nothing
        Assert.True(cap.Captured.TryDequeue(item))
        Assert.Equal(WebhookAlarmSink.Sign(secret, item.Ts, item.Body), item.Sig)
        Assert.NotEqual(WebhookAlarmSink.Sign(secret, item.Ts, item.Body & " "), item.Sig)
        Assert.Contains("Intrusion", item.Body)
    End Function
End Class
