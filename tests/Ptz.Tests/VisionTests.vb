Imports Ptz.Core.Models
Imports Ptz.Core.Vision
Imports Xunit

Public Class VisionTests

    <Fact>
    Public Sub Subtractor_StaticNoisyScene_ProducesNoForeground()
        Dim sub_ As New BackgroundSubtractor()
        Dim last As SubtractionResult = Nothing
        For i As Integer = 0 To 30
            last = sub_.Process(TestFrames.Scene(80, 45, 100, TestFrames.T0.AddSeconds(i), 100 + i))
        Next
        Assert.False(last.WarmingUp)
        Assert.True(last.ForegroundRatio < 0.01, $"ratio={last.ForegroundRatio}")
    End Sub

    <Fact>
    Public Sub Subtractor_GlobalBrightnessJump_IsTreatedAsIlluminationChange()
        Dim sub_ As New BackgroundSubtractor()
        For i As Integer = 0 To 15
            sub_.Process(TestFrames.Scene(80, 45, 100, TestFrames.T0.AddSeconds(i), i))
        Next
        Dim r As SubtractionResult = sub_.Process(TestFrames.Scene(80, 45, 200, TestFrames.T0.AddSeconds(20), 99))
        Assert.True(r.IlluminationChange)
        Assert.All(r.Mask, Sub(b) Assert.Equal(CByte(0), b))
    End Sub

    <Fact>
    Public Sub MotionDetector_FindsMovingSquare_NearItsPosition()
        Dim d As New MotionDetector(New MotionDetectorOptions With {.ProcessWidth = 320})
        For i As Integer = 0 To 12
            d.Process(TestFrames.Scene(320, 180, 90, TestFrames.T0.AddSeconds(i * 0.2), i))
        Next
        Dim r As MotionResult = d.Process(TestFrames.Scene(320, 180, 90, TestFrames.T0.AddSeconds(3), 50, (X:=160, Y:=80, Size:=24, Value:=220)))
        Assert.Single(r.Detections)
        Dim c As NormPoint = r.Detections(0).Box.Center
        Assert.InRange(c.X, (160 + 12) / 320.0 - 0.03, (160 + 12) / 320.0 + 0.03)
        Assert.InRange(c.Y, (80 + 12) / 180.0 - 0.04, (80 + 12) / 180.0 + 0.04)
    End Sub

    <Fact>
    Public Sub MotionDetector_ExclusionZone_SuppressesDetection()
        Dim d As New MotionDetector(New MotionDetectorOptions With {.ProcessWidth = 320})
        d.SetExclusionZones({New List(Of NormPoint) From {New NormPoint(0.4, 0.3), New NormPoint(0.7, 0.3), New NormPoint(0.7, 0.8), New NormPoint(0.4, 0.8)}})
        For i As Integer = 0 To 12
            d.Process(TestFrames.Scene(320, 180, 90, TestFrames.T0.AddSeconds(i * 0.2), i))
        Next
        Dim r As MotionResult = d.Process(TestFrames.Scene(320, 180, 90, TestFrames.T0.AddSeconds(3), 50, (X:=160, Y:=80, Size:=24, Value:=220)))
        Assert.Empty(r.Detections)
    End Sub

    <Fact>
    Public Sub MotionDetector_WhileSuspended_ReportsSuspendedAndNoDetections()
        Dim d As New MotionDetector(New MotionDetectorOptions With {.ProcessWidth = 160})
        d.SuspendUntil(TestFrames.T0.AddSeconds(10))
        Dim r As MotionResult = d.Process(TestFrames.Scene(160, 90, 90, TestFrames.T0.AddSeconds(1), 1))
        Assert.True(r.Suspended)
        Assert.Empty(r.Detections)
    End Sub

    Private Shared Function Det(cx As Double, cy As Double, Optional size As Double = 0.06) As Detection
        Return New Detection(New NormRect(cx - size / 2, cy - size / 2, size, size), 100)
    End Function

    <Fact>
    Public Sub Tracker_KeepsIdForMovingObject_AndConfirmsAfterHits()
        Dim tr As New ObjectTracker()
        Dim id As Integer = -1
        For i As Integer = 0 To 9
            Dim u As TrackerUpdate = tr.Update({Det(0.2 + i * 0.03, 0.5)}, TestFrames.T0.AddSeconds(i * 0.2))
            Assert.Single(u.Active)
            If id < 0 Then id = u.Active(0).Id
            Assert.Equal(id, u.Active(0).Id)
        Next
        Assert.Equal(TrackState.Confirmed, tr.Tracks(0).State)
        Assert.True(tr.Tracks(0).VelocityX > 0.05, $"vx={tr.Tracks(0).VelocityX}")
    End Sub

    <Fact>
    Public Sub Tracker_SeparatesTwoObjects_AndEndsLostTrack()
        Dim tr As New ObjectTracker()
        For i As Integer = 0 To 6
            tr.Update({Det(0.2, 0.3), Det(0.8, 0.7)}, TestFrames.T0.AddSeconds(i * 0.2))
        Next
        Assert.Equal(2, tr.Tracks.Count)
        Dim ended As Integer = 0
        For i As Integer = 7 To 20
            Dim u As TrackerUpdate = tr.Update({Det(0.2, 0.3)}, TestFrames.T0.AddSeconds(i * 0.2))
            ended += u.Ended.Count
        Next
        Assert.Equal(1, ended)
        Assert.Single(tr.Tracks)
    End Sub

    <Fact>
    Public Sub Tracker_TentativeNoiseDetection_NeverBecomesConfirmed()
        Dim tr As New ObjectTracker()
        tr.Update({Det(0.5, 0.5)}, TestFrames.T0)
        tr.Update(New List(Of Detection)(), TestFrames.T0.AddSeconds(0.2))
        Dim u As TrackerUpdate = tr.Update(New List(Of Detection)(), TestFrames.T0.AddSeconds(0.4))
        Assert.Empty(u.Active)
        Assert.Empty(u.Ended)
    End Sub

    <Fact>
    Public Sub TemplateTracker_FollowsShiftedPatch()
        Dim tt As New TemplateTracker()
        Dim f0 As GrayFrame = TestFrames.Textured(160, 90, 60, 30, 16, TestFrames.T0)
        Assert.True(tt.Initialize(f0, New NormRect(60 / 160.0, 30 / 90.0, 16 / 160.0, 16 / 90.0)))
        Dim r As TemplateTrackResult = tt.Update(TestFrames.Textured(160, 90, 66, 33, 16, TestFrames.T0.AddSeconds(0.2)))
        Assert.True(r.Found)
        Assert.InRange(r.Box.X * 160, 65, 67)
        Assert.InRange(r.Box.Y * 90, 32, 34)
    End Sub

    <Fact>
    Public Sub TemplateTracker_RefusesFlatTarget()
        Dim tt As New TemplateTracker()
        Dim flat As New GrayFrame(160, 90, Enumerable.Repeat(CByte(100), 160 * 90).ToArray(), TestFrames.T0)
        Assert.False(tt.Initialize(flat, New NormRect(0.4, 0.4, 0.1, 0.15)))
    End Sub

    ' ---------- السلوك ----------
    Private Shared Function RunTrack(analyzer As BehaviorAnalyzer, points As IEnumerable(Of (Sec As Double, X As Double, Y As Double))) As List(Of BehaviorEvent)
        Dim tr As New ObjectTracker()
        Dim all As New List(Of BehaviorEvent)()
        For Each p In points
            Dim t As DateTime = TestFrames.T0.AddSeconds(p.Sec)
            Dim u As TrackerUpdate = tr.Update({Det(p.X, p.Y)}, t)
            all.AddRange(analyzer.Analyze(u.Active, t))
        Next
        Return all
    End Function

    <Fact>
    Public Sub Behavior_ZoneIntrusion_RaisedOnceOnEntry()
        Dim a As New BehaviorAnalyzer()
        a.SetZones({New RestrictedZone("Gate", {New NormPoint(0.5, 0.2), New NormPoint(0.9, 0.2), New NormPoint(0.9, 0.8), New NormPoint(0.5, 0.8)})})
        Dim pts As New List(Of (Double, Double, Double))()
        For i As Integer = 0 To 30
            pts.Add((i * 0.2, 0.2 + i * 0.02, 0.5))
        Next
        Dim ev As List(Of BehaviorEvent) = RunTrack(a, pts.Select(Function(p) (p.Item1, p.Item2, p.Item3)))
        Assert.Single(ev.Where(Function(e) e.Kind = BehaviorKind.Intrusion))
        Assert.Equal(AlarmSeverity.Critical, ev.First(Function(e) e.Kind = BehaviorKind.Intrusion).Severity)
    End Sub

    <Theory>
    <InlineData(LineDirection.Both, 1)>
    <InlineData(LineDirection.LeftToRight, 1)>
    <InlineData(LineDirection.RightToLeft, 0)>
    Public Sub Behavior_LineCrossing_RespectsDirection(direction As LineDirection, expected As Integer)
        Dim a As New BehaviorAnalyzer()
        ' خط عمودي من (0.5,0.1) إلى (0.5,0.9) — الاتجاه a→b نحو الأسفل؛ اليسار بالنسبة للاتجاه = x أكبر.
        ' الحركة من x=0.3 إلى x=0.7 تعبر من "يمين" إلى "يسار" بحسب Side().
        a.SetLines({New TripLine("L", New NormPoint(0.5, 0.1), New NormPoint(0.5, 0.9), direction)})
        Dim pts As New List(Of (Double, Double, Double))()
        For i As Integer = 0 To 20
            pts.Add((i * 0.2, 0.3 + i * 0.02, 0.5))
        Next
        Dim fromLeft As Boolean = Geometry.Side(New NormPoint(0.5, 0.1), New NormPoint(0.5, 0.9), New NormPoint(0.3, 0.5)) > 0
        Dim ev As List(Of BehaviorEvent) = RunTrack(a, pts.Select(Function(p) (p.Item1, p.Item2, p.Item3)))
        Dim count As Integer = ev.Where(Function(e) e.Kind = BehaviorKind.LineCrossing).Count()
        If direction = LineDirection.Both Then
            Assert.Equal(expected, count)
        ElseIf (direction = LineDirection.LeftToRight) = fromLeft Then
            Assert.Equal(1, count)
        Else
            Assert.Equal(0, count)
        End If
    End Sub

    <Fact>
    Public Sub Behavior_Loitering_AfterConfiguredTime()
        Dim a As New BehaviorAnalyzer(New BehaviorOptions With {.LoiterSeconds = 10, .StationarySeconds = 1000})
        Dim pts As New List(Of (Double, Double, Double))()
        Dim rnd As New Random(1)
        For i As Integer = 0 To 80
            pts.Add((i * 0.25, 0.5 + (rnd.NextDouble() - 0.5) * 0.02, 0.5 + (rnd.NextDouble() - 0.5) * 0.02))
        Next
        Dim ev As List(Of BehaviorEvent) = RunTrack(a, pts.Select(Function(p) (p.Item1, p.Item2, p.Item3)))
        Assert.Contains(ev, Function(e) e.Kind = BehaviorKind.Loitering)
    End Sub

    <Fact>
    Public Sub Behavior_Running_WhenSpeedExceedsThreshold()
        Dim a As New BehaviorAnalyzer(New BehaviorOptions With {.RunSpeed = 0.3})
        Dim pts As New List(Of (Double, Double, Double))()
        For i As Integer = 0 To 14
            pts.Add((i * 0.2, 0.05 + i * 0.07, 0.5))   ' 0.35 إطار/ث
        Next
        Dim ev As List(Of BehaviorEvent) = RunTrack(a, pts.Select(Function(p) (p.Item1, p.Item2, p.Item3)))
        Assert.Contains(ev, Function(e) e.Kind = BehaviorKind.Running)
    End Sub

    <Fact>
    Public Sub Behavior_ErraticZigZag_Detected()
        Dim a As New BehaviorAnalyzer(New BehaviorOptions With {.RunSpeed = 99, .LoiterSeconds = 1000})
        Dim pts As New List(Of (Double, Double, Double))()
        For i As Integer = 0 To 40
            Dim phase As Integer = (i \ 3) Mod 2
            Dim x As Double = If(phase = 0, 0.3 + (i Mod 3) * 0.05, 0.45 - (i Mod 3) * 0.05)
            pts.Add((i * 0.2, x, 0.5))
        Next
        Dim ev As List(Of BehaviorEvent) = RunTrack(a, pts.Select(Function(p) (p.Item1, p.Item2, p.Item3)))
        Assert.Contains(ev, Function(e) e.Kind = BehaviorKind.ErraticMovement)
    End Sub

    <Fact>
    Public Sub Behavior_Gathering_WhenManyTracksClose()
        Dim a As New BehaviorAnalyzer(New BehaviorOptions With {.GatherCount = 4, .GatherRadius = 0.2})
        Dim tr As New ObjectTracker()
        Dim found As Boolean = False
        For i As Integer = 0 To 8
            Dim t As DateTime = TestFrames.T0.AddSeconds(i * 0.2)
            Dim u As TrackerUpdate = tr.Update({Det(0.4, 0.4), Det(0.45, 0.4), Det(0.4, 0.46), Det(0.46, 0.47)}, t)
            If a.Analyze(u.Active, t).Any(Function(e) e.Kind = BehaviorKind.Gathering) Then found = True
        Next
        Assert.True(found)
    End Sub

    <Fact>
    Public Sub AnomalyModel_FlagsSpeedFarFromLearnedNormal_AndRoundTrips()
        Dim m As New SpeedAnomalyModel(4, 4) With {.MinSamples = 30}
        Dim rnd As New Random(3)
        For i As Integer = 0 To 199
            m.Observe(New NormPoint(0.3, 0.3), 0.1 + (rnd.NextDouble() - 0.5) * 0.04)
        Next
        m.Learning = False
        Assert.False(m.IsAnomalous(New NormPoint(0.3, 0.3), 0.11))
        Assert.True(m.IsAnomalous(New NormPoint(0.3, 0.3), 0.9))
        Assert.False(m.IsAnomalous(New NormPoint(0.9, 0.9), 0.9), "خلية بلا عينات كافية لا تنذر")

        Dim copy As New SpeedAnomalyModel(4, 4) With {.MinSamples = 30, .Learning = False}
        copy.LoadJson(m.ToJson())
        Assert.True(copy.IsAnomalous(New NormPoint(0.3, 0.3), 0.9))
        Dim small As New SpeedAnomalyModel(2, 2)
        Assert.Throws(Of IO.InvalidDataException)(Sub() small.LoadJson(m.ToJson()))
    End Sub

    ' ---------- التتبع الآلي ----------
    <Theory>
    <InlineData(0.8, 0.5, 1, 0)>
    <InlineData(0.2, 0.5, -1, 0)>
    <InlineData(0.5, 0.2, 0, 1)>
    <InlineData(0.5, 0.8, 0, -1)>
    Public Sub AutoTracker_ComputeVector_DirectionMatchesTargetOffset(cx As Double, cy As Double, panSign As Integer, tiltSign As Integer)
        Dim t As New AutoTracker()
        Dim v As PtzVector = t.ComputeVector(New NormRect(cx - 0.05, cy - 0.1, 0.1, 0.2))
        Assert.Equal(panSign, Math.Sign(v.Pan))
        Assert.Equal(tiltSign, Math.Sign(v.Tilt))
    End Sub

    <Fact>
    Public Sub AutoTracker_CenteredTarget_InsideDeadBand_NoMotion_ZoomByHeight()
        Dim t As New AutoTracker()
        Assert.True(t.ComputeVector(New NormRect(0.46, 0.35, 0.08, 0.3)).IsZero)          ' حجم مناسب
        Assert.True(t.ComputeVector(New NormRect(0.47, 0.45, 0.06, 0.1)).Zoom > 0)        ' صغير → تكبير
        Assert.True(t.ComputeVector(New NormRect(0.3, 0.1, 0.4, 0.8)).Zoom < 0)           ' كبير → تصغير
    End Sub

    <Fact>
    Public Sub AutoTracker_FollowsPatchRightward_ThenReportsLost()
        Dim tr As New AutoTracker(New AutoTrackOptions With {.LostTimeoutSeconds = 1.0, .Smoothing = 1.0})
        Dim f0 As GrayFrame = TestFrames.Textured(160, 90, 100, 37, 16, TestFrames.T0)
        Assert.True(tr.Lock(f0, New NormRect(100 / 160.0, 37 / 90.0, 16 / 160.0, 16 / 90.0), TestFrames.T0))
        Dim c As FollowCommand = tr.Update(TestFrames.Textured(160, 90, 104, 37, 16, TestFrames.T0.AddSeconds(0.2)), TestFrames.T0.AddSeconds(0.2))
        Assert.Equal(FollowStatus.Following, c.Status)
        Assert.True(c.Vector.Pan > 0)
        ' اختفاء الهدف (مشهد مختلف تماماً)
        Dim noise As GrayFrame = TestFrames.Scene(160, 90, 30, TestFrames.T0.AddSeconds(0.4), 7)
        Dim mid As FollowCommand = tr.Update(noise, TestFrames.T0.AddSeconds(0.4))
        Assert.True(mid.Vector.IsZero)
        Dim lost As FollowCommand = tr.Update(TestFrames.Scene(160, 90, 30, TestFrames.T0.AddSeconds(2), 8), TestFrames.T0.AddSeconds(2))
        Assert.Equal(FollowStatus.Lost, lost.Status)
    End Sub

    ' ---------- الأداء ----------
    <Fact>
    Public Sub Pipeline_Performance_Handles_FullHdFrames_Quickly()
        Dim d As New MotionDetector()
        Dim frames As New List(Of GrayFrame)()
        For i As Integer = 0 To 19
            frames.Add(TestFrames.Scene(1920, 1080, 100, TestFrames.T0.AddSeconds(i * 0.2), i, (X:=300 + i * 20, Y:=500, Size:=120, Value:=230)))
        Next
        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        For Each f As GrayFrame In frames
            d.Process(f)
        Next
        sw.Stop()
        ' 20 إطار Full-HD: متوسط أقل من 100ms/إطار على أي جهاز معقول (يدعم >10 إطار/ث).
        Assert.True(sw.Elapsed.TotalMilliseconds / 20 < 100, $"avg={sw.Elapsed.TotalMilliseconds / 20:F1}ms")
    End Sub

End Class
