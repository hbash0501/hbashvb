Imports Ptz.Core.Models

Namespace Vision

    Public Enum BehaviorKind
        Intrusion
        LineCrossing
        Loitering
        Running
        ErraticMovement
        Stationary
        Gathering
        SpeedAnomaly
    End Enum

    ''' <summary>اتجاه العبور المطلوب عبر الخط، بالنسبة للاتجاه من A إلى B.</summary>
    Public Enum LineDirection
        Both
        LeftToRight
        RightToLeft
    End Enum

    Public NotInheritable Class RestrictedZone
        Public ReadOnly Property Name As String
        Public ReadOnly Property Points As IReadOnlyList(Of NormPoint)
        Public ReadOnly Property Severity As AlarmSeverity

        Public Sub New(name As String, points As IReadOnlyList(Of NormPoint), Optional severity As AlarmSeverity = AlarmSeverity.Critical)
            If points Is Nothing OrElse points.Count < 3 Then Throw New ArgumentException("المنطقة تحتاج 3 نقاط على الأقل.", NameOf(points))
            Me.Name = name
            Me.Points = points
            Me.Severity = severity
        End Sub
    End Class

    Public NotInheritable Class TripLine
        Public ReadOnly Property Name As String
        Public ReadOnly Property A As NormPoint
        Public ReadOnly Property B As NormPoint
        Public ReadOnly Property Direction As LineDirection

        Public Sub New(name As String, a As NormPoint, b As NormPoint, Optional direction As LineDirection = LineDirection.Both)
            Me.Name = name
            Me.A = a
            Me.B = b
            Me.Direction = direction
        End Sub
    End Class

    Public NotInheritable Class BehaviorEvent
        Public Property Kind As BehaviorKind
        Public Property TrackId As Integer
        Public Property TimeUtc As DateTime
        Public Property Severity As AlarmSeverity
        Public Property Message As String = ""
        Public Property Position As NormPoint
    End Class

    Public NotInheritable Class BehaviorOptions
        Public Property LoiterSeconds As Double = 20.0
        Public Property LoiterRadius As Double = 0.06
        ''' <summary>سرعة الركض بوحدات الإطار/ثانية.</summary>
        Public Property RunSpeed As Double = 0.45
        Public Property RunMinSeconds As Double = 1.0
        Public Property StationarySeconds As Double = 45.0
        Public Property StationarySpeed As Double = 0.01
        Public Property ErraticReversals As Integer = 3
        Public Property ErraticWindowSeconds As Double = 8.0
        Public Property ErraticMinSegment As Double = 0.03
        Public Property GatherCount As Integer = 4
        Public Property GatherRadius As Double = 0.15
        Public Property EventCooldownSeconds As Double = 10.0
        Public Property MinTrackAgeSeconds As Double = 0.5
    End Class

    ''' <summary>
    ''' محلّل السلوك المبني على قواعد معرفية + نموذج شذوذ إحصائي:
    ''' اقتحام منطقة، عبور خط، تسكّع، ركض، حركة متخبطة، سكون مطوّل، تجمّع، وسرعة شاذة.
    ''' </summary>
    Public NotInheritable Class BehaviorAnalyzer
        Private NotInheritable Class TrackMemory
            Public AnchorPoint As NormPoint
            Public AnchorSince As DateTime
            Public StillSince As DateTime = DateTime.MinValue
            Public RunSince As DateTime = DateTime.MinValue
            Public ReadOnly InZones As New HashSet(Of String)()
            Public ReadOnly LastCooldown As New Dictionary(Of BehaviorKind, DateTime)()
            Public LastEvaluatedPoint As Integer = 0
        End Class

        Private ReadOnly _opt As BehaviorOptions
        Private ReadOnly _memory As New Dictionary(Of Integer, TrackMemory)()
        Private ReadOnly _zones As New List(Of RestrictedZone)()
        Private ReadOnly _lines As New List(Of TripLine)()
        Private _lastGatherUtc As DateTime = DateTime.MinValue

        Public ReadOnly Property Anomaly As SpeedAnomalyModel

        Public Sub New(Optional options As BehaviorOptions = Nothing, Optional anomalyModel As SpeedAnomalyModel = Nothing)
            _opt = If(options, New BehaviorOptions())
            Anomaly = If(anomalyModel, New SpeedAnomalyModel())
        End Sub

        Public Sub SetZones(zones As IEnumerable(Of RestrictedZone))
            _zones.Clear()
            If zones IsNot Nothing Then _zones.AddRange(zones)
            For Each m As TrackMemory In _memory.Values
                m.InZones.Clear()
            Next
        End Sub

        Public Sub SetLines(lines As IEnumerable(Of TripLine))
            _lines.Clear()
            If lines IsNot Nothing Then _lines.AddRange(lines)
        End Sub

        Public Sub Reset()
            _memory.Clear()
        End Sub

        Public Function Analyze(tracks As IReadOnlyList(Of Track), nowUtc As DateTime) As List(Of BehaviorEvent)
            Dim events As New List(Of BehaviorEvent)()
            Dim alive As New HashSet(Of Integer)()
            Dim confirmed As New List(Of Track)()

            For Each t As Track In tracks
                If t.State <> TrackState.Confirmed OrElse t.AgeSeconds < _opt.MinTrackAgeSeconds Then Continue For
                confirmed.Add(t)
                alive.Add(t.Id)
                Dim m As TrackMemory = GetMemory(t, nowUtc)
                CheckZones(t, m, nowUtc, events)
                CheckLines(t, m, nowUtc, events)
                CheckLoiter(t, m, nowUtc, events)
                CheckStationary(t, m, nowUtc, events)
                CheckRunning(t, m, nowUtc, events)
                CheckErratic(t, m, nowUtc, events)
                CheckAnomaly(t, m, nowUtc, events)
                m.LastEvaluatedPoint = t.Trajectory.Count
            Next

            CheckGathering(confirmed, nowUtc, events)

            Dim stale As New List(Of Integer)()
            For Each id As Integer In _memory.Keys
                If Not alive.Contains(id) Then stale.Add(id)
            Next
            For Each id As Integer In stale
                _memory.Remove(id)
            Next
            Return events
        End Function

        Private Function GetMemory(t As Track, nowUtc As DateTime) As TrackMemory
            Dim m As TrackMemory = Nothing
            If Not _memory.TryGetValue(t.Id, m) Then
                m = New TrackMemory With {.AnchorPoint = t.Center, .AnchorSince = nowUtc}
                m.LastEvaluatedPoint = Math.Max(0, t.Trajectory.Count - 1)
                _memory(t.Id) = m
            End If
            Return m
        End Function

        Private Function Emit(events As List(Of BehaviorEvent), m As TrackMemory, kind As BehaviorKind, t As Track,
                              nowUtc As DateTime, severity As AlarmSeverity, message As String) As Boolean
            Dim last As DateTime = DateTime.MinValue
            If m.LastCooldown.TryGetValue(kind, last) AndAlso (nowUtc - last).TotalSeconds < _opt.EventCooldownSeconds Then Return False
            m.LastCooldown(kind) = nowUtc
            events.Add(New BehaviorEvent With {.Kind = kind, .TrackId = t.Id, .TimeUtc = nowUtc, .Severity = severity, .Message = message, .Position = t.Center})
            Return True
        End Function

        Private Sub CheckZones(t As Track, m As TrackMemory, nowUtc As DateTime, events As List(Of BehaviorEvent))
            For Each z As RestrictedZone In _zones
                Dim inside As Boolean = Geometry.PointInPolygon(t.Center, z.Points)
                If inside AndAlso m.InZones.Add(z.Name) Then
                    ' الدخول الأول يُنذر دائماً؛ التبريد يمنع التكرار السريع فقط.
                    m.LastCooldown.Remove(BehaviorKind.Intrusion)
                    Emit(events, m, BehaviorKind.Intrusion, t, nowUtc, z.Severity, $"اقتحام المنطقة المحظورة '{z.Name}' (مسار {t.Id})")
                ElseIf Not inside Then
                    m.InZones.Remove(z.Name)
                End If
            Next
        End Sub

        Private Sub CheckLines(t As Track, m As TrackMemory, nowUtc As DateTime, events As List(Of BehaviorEvent))
            If _lines.Count = 0 OrElse t.Trajectory.Count < 2 Then Return
            Dim start As Integer = Math.Max(1, m.LastEvaluatedPoint)
            For i As Integer = start To t.Trajectory.Count - 1
                Dim p0 As NormPoint = t.Trajectory(i - 1).Position
                Dim p1 As NormPoint = t.Trajectory(i).Position
                For Each ln As TripLine In _lines
                    If Not Geometry.SegmentsIntersect(p0, p1, ln.A, ln.B) Then Continue For
                    Dim fromLeft As Boolean = Geometry.Side(ln.A, ln.B, p0) > 0
                    Dim ok As Boolean = ln.Direction = LineDirection.Both OrElse
                                        (ln.Direction = LineDirection.LeftToRight AndAlso fromLeft) OrElse
                                        (ln.Direction = LineDirection.RightToLeft AndAlso Not fromLeft)
                    If ok Then
                        m.LastCooldown.Remove(BehaviorKind.LineCrossing)
                        Emit(events, m, BehaviorKind.LineCrossing, t, nowUtc, AlarmSeverity.Warning,
                             $"عبور الخط '{ln.Name}' ({If(fromLeft, "يسار→يمين", "يمين→يسار")}) مسار {t.Id}")
                    End If
                Next
            Next
        End Sub

        Private Sub CheckLoiter(t As Track, m As TrackMemory, nowUtc As DateTime, events As List(Of BehaviorEvent))
            If t.Center.DistanceTo(m.AnchorPoint) > _opt.LoiterRadius Then
                m.AnchorPoint = t.Center
                m.AnchorSince = nowUtc
                Return
            End If
            If (nowUtc - m.AnchorSince).TotalSeconds >= _opt.LoiterSeconds Then
                Emit(events, m, BehaviorKind.Loitering, t, nowUtc, AlarmSeverity.Warning,
                     $"تسكّع لأكثر من {_opt.LoiterSeconds:F0} ث (مسار {t.Id})")
            End If
        End Sub

        Private Sub CheckStationary(t As Track, m As TrackMemory, nowUtc As DateTime, events As List(Of BehaviorEvent))
            If t.Speed > _opt.StationarySpeed Then
                m.StillSince = DateTime.MinValue
                Return
            End If
            If m.StillSince = DateTime.MinValue Then m.StillSince = nowUtc
            If (nowUtc - m.StillSince).TotalSeconds >= _opt.StationarySeconds Then
                Emit(events, m, BehaviorKind.Stationary, t, nowUtc, AlarmSeverity.Warning,
                     $"جسم ساكن لأكثر من {_opt.StationarySeconds:F0} ث (مسار {t.Id})")
            End If
        End Sub

        Private Sub CheckRunning(t As Track, m As TrackMemory, nowUtc As DateTime, events As List(Of BehaviorEvent))
            If t.Speed < _opt.RunSpeed Then
                m.RunSince = DateTime.MinValue
                Return
            End If
            If m.RunSince = DateTime.MinValue Then m.RunSince = nowUtc
            If (nowUtc - m.RunSince).TotalSeconds >= _opt.RunMinSeconds Then
                Emit(events, m, BehaviorKind.Running, t, nowUtc, AlarmSeverity.Warning,
                     $"حركة سريعة/ركض (السرعة {t.Speed:F2} إطار/ث) مسار {t.Id}")
            End If
        End Sub

        Private Sub CheckErratic(t As Track, m As TrackMemory, nowUtc As DateTime, events As List(Of BehaviorEvent))
            Dim cutoff As DateTime = nowUtc.AddSeconds(-_opt.ErraticWindowSeconds)
            Dim pts As New List(Of NormPoint)()
            For Each tp As TrajectoryPoint In t.Trajectory
                If tp.TimeUtc < cutoff Then Continue For
                If pts.Count = 0 OrElse pts(pts.Count - 1).DistanceTo(tp.Position) >= _opt.ErraticMinSegment Then pts.Add(tp.Position)
            Next
            If pts.Count < 4 Then Return
            Dim reversals As Integer = 0
            For i As Integer = 2 To pts.Count - 1
                Dim ax As Double = pts(i - 1).X - pts(i - 2).X
                Dim ay As Double = pts(i - 1).Y - pts(i - 2).Y
                Dim bx As Double = pts(i).X - pts(i - 1).X
                Dim by As Double = pts(i).Y - pts(i - 1).Y
                Dim dot As Double = ax * bx + ay * by
                Dim mag As Double = Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by)
                If mag > 0 AndAlso dot / mag < -0.5 Then reversals += 1 ' زاوية > 120°
            Next
            If reversals >= _opt.ErraticReversals Then
                Emit(events, m, BehaviorKind.ErraticMovement, t, nowUtc, AlarmSeverity.Warning,
                     $"حركة متخبطة/تغيّر اتجاه متكرر ({reversals} مرات) مسار {t.Id}")
            End If
        End Sub

        Private Sub CheckAnomaly(t As Track, m As TrackMemory, nowUtc As DateTime, events As List(Of BehaviorEvent))
            Anomaly.Observe(t.Center, t.Speed)
            If Anomaly.IsAnomalous(t.Center, t.Speed) Then
                Emit(events, m, BehaviorKind.SpeedAnomaly, t, nowUtc, AlarmSeverity.Warning,
                     $"سرعة شاذة عن النمط المتعلَّم (z={Anomaly.Score(t.Center, t.Speed):F1}) مسار {t.Id}")
            End If
        End Sub

        Private Sub CheckGathering(confirmed As List(Of Track), nowUtc As DateTime, events As List(Of BehaviorEvent))
            If confirmed.Count < _opt.GatherCount Then Return
            If (nowUtc - _lastGatherUtc).TotalSeconds < _opt.EventCooldownSeconds Then Return
            For Each seed As Track In confirmed
                Dim near As Integer = 0
                For Each o As Track In confirmed
                    If seed.Center.DistanceTo(o.Center) <= _opt.GatherRadius Then near += 1
                Next
                If near >= _opt.GatherCount Then
                    _lastGatherUtc = nowUtc
                    events.Add(New BehaviorEvent With {.Kind = BehaviorKind.Gathering, .TrackId = 0, .TimeUtc = nowUtc,
                                                       .Severity = AlarmSeverity.Warning, .Position = seed.Center,
                                                       .Message = $"تجمّع {near} أجسام في منطقة صغيرة"})
                    Return
                End If
            Next
        End Sub
    End Class

End Namespace
