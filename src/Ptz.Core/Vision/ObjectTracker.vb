Imports Ptz.Core.Models

Namespace Vision

    Public Enum TrackState
        Tentative
        Confirmed
    End Enum

    Public NotInheritable Class TrajectoryPoint
        Public ReadOnly Property TimeUtc As DateTime
        Public ReadOnly Property Position As NormPoint

        Public Sub New(timeUtc As DateTime, position As NormPoint)
            Me.TimeUtc = timeUtc
            Me.Position = position
        End Sub
    End Class

    ''' <summary>مسار جسم متحرك مع سرعته وتاريخ مواضعه.</summary>
    Public NotInheritable Class Track
        Public Property Id As Integer
        Public Property Box As NormRect
        Public Property Center As NormPoint
        ''' <summary>السرعة بوحدات الإطار/ثانية.</summary>
        Public Property VelocityX As Double
        Public Property VelocityY As Double
        Public Property State As TrackState = TrackState.Tentative
        Public Property Hits As Integer
        Public Property Misses As Integer
        Public Property FirstSeenUtc As DateTime
        Public Property LastSeenUtc As DateTime
        Public Property LastUpdateUtc As DateTime
        Public Property PathLength As Double
        Public Property MaxSpeed As Double
        Public ReadOnly Property Trajectory As New List(Of TrajectoryPoint)()

        Public ReadOnly Property Speed As Double
            Get
                Return Math.Sqrt(VelocityX * VelocityX + VelocityY * VelocityY)
            End Get
        End Property

        Public ReadOnly Property AgeSeconds As Double
            Get
                Return (LastSeenUtc - FirstSeenUtc).TotalSeconds
            End Get
        End Property
    End Class

    Public NotInheritable Class ObjectTrackerOptions
        ''' <summary>أقصى مسافة مطابقة أساسية (مُطبَّعة) تُضاف إليها نصف قطر الصندوق.</summary>
        Public Property MaxMatchDistance As Double = 0.12
        Public Property ConfirmHits As Integer = 3
        Public Property MaxUnseenSeconds As Double = 1.5
        Public Property PositionGain As Double = 0.6
        Public Property VelocityGain As Double = 0.4
        Public Property MaxTrajectoryPoints As Integer = 600
    End Class

    Public NotInheritable Class TrackerUpdate
        Public ReadOnly Property Active As IReadOnlyList(Of Track)
        Public ReadOnly Property Ended As IReadOnlyList(Of Track)

        Public Sub New(active As IReadOnlyList(Of Track), ended As IReadOnlyList(Of Track))
            Me.Active = active
            Me.Ended = ended
        End Sub
    End Class

    ''' <summary>
    ''' تتبّع متعدد الأجسام: تنبؤ بسرعة ثابتة، مطابقة جشعة بأقرب مسافة مع بوابة (gating)،
    ''' ترشيح α-β للموضع والسرعة، وتأكيد المسارات بعد عدة إصابات متتالية.
    ''' </summary>
    Public NotInheritable Class ObjectTracker
        Private ReadOnly _opt As ObjectTrackerOptions
        Private ReadOnly _tracks As New List(Of Track)()
        Private _nextId As Integer = 1

        Public Sub New(Optional options As ObjectTrackerOptions = Nothing)
            _opt = If(options, New ObjectTrackerOptions())
        End Sub

        Public ReadOnly Property Tracks As IReadOnlyList(Of Track)
            Get
                Return _tracks
            End Get
        End Property

        Public Sub Reset()
            _tracks.Clear()
        End Sub

        Public Function Update(detections As IReadOnlyList(Of Detection), nowUtc As DateTime) As TrackerUpdate
            Dim pairs As New List(Of (Dist As Double, T As Integer, D As Integer))()
            For ti As Integer = 0 To _tracks.Count - 1
                Dim t As Track = _tracks(ti)
                Dim dt As Double = Math.Min(1.0, Math.Max(0.0, (nowUtc - t.LastUpdateUtc).TotalSeconds))
                Dim pred As New NormPoint(t.Center.X + t.VelocityX * dt, t.Center.Y + t.VelocityY * dt)
                Dim gate As Double = _opt.MaxMatchDistance + 0.5 * Math.Sqrt(t.Box.Width * t.Box.Width + t.Box.Height * t.Box.Height)
                For di As Integer = 0 To detections.Count - 1
                    Dim d As Double = pred.DistanceTo(detections(di).Box.Center)
                    If d <= gate Then pairs.Add((d, ti, di))
                Next
            Next
            pairs.Sort(Function(a, b) a.Dist.CompareTo(b.Dist))

            Dim trackUsed(_tracks.Count - 1) As Boolean
            Dim detUsed(detections.Count - 1) As Boolean
            For Each p In pairs
                If trackUsed(p.T) OrElse detUsed(p.D) Then Continue For
                trackUsed(p.T) = True
                detUsed(p.D) = True
                ApplyMatch(_tracks(p.T), detections(p.D), nowUtc)
            Next

            Dim ended As New List(Of Track)()
            For ti As Integer = _tracks.Count - 1 To 0 Step -1
                If trackUsed(ti) Then Continue For
                Dim t As Track = _tracks(ti)
                t.Misses += 1
                Dim unseen As Double = (nowUtc - t.LastSeenUtc).TotalSeconds
                If (t.State = TrackState.Tentative AndAlso t.Misses >= 2) OrElse unseen > _opt.MaxUnseenSeconds Then
                    If t.State = TrackState.Confirmed Then ended.Insert(0, t)
                    _tracks.RemoveAt(ti)
                End If
            Next

            For di As Integer = 0 To detections.Count - 1
                If detUsed(di) Then Continue For
                Dim det As Detection = detections(di)
                Dim t As New Track With {
                    .Id = _nextId,
                    .Box = det.Box,
                    .Center = det.Box.Center,
                    .Hits = 1,
                    .FirstSeenUtc = nowUtc,
                    .LastSeenUtc = nowUtc,
                    .LastUpdateUtc = nowUtc
                }
                _nextId += 1
                t.Trajectory.Add(New TrajectoryPoint(nowUtc, t.Center))
                _tracks.Add(t)
            Next

            Return New TrackerUpdate(_tracks.ToList(), ended)
        End Function

        Private Sub ApplyMatch(t As Track, det As Detection, nowUtc As DateTime)
            Dim dt As Double = (nowUtc - t.LastUpdateUtc).TotalSeconds
            If dt <= 0.0 Then dt = 0.001
            Dim dtc As Double = Math.Min(1.0, dt)
            Dim predX As Double = t.Center.X + t.VelocityX * dtc
            Dim predY As Double = t.Center.Y + t.VelocityY * dtc
            Dim z As NormPoint = det.Box.Center
            Dim rx As Double = z.X - predX
            Dim ry As Double = z.Y - predY
            Dim nx As Double = predX + _opt.PositionGain * rx
            Dim ny As Double = predY + _opt.PositionGain * ry
            If dt >= 0.01 Then
                t.VelocityX += _opt.VelocityGain * rx / dtc
                t.VelocityY += _opt.VelocityGain * ry / dtc
            End If
            Dim moved As Double = Math.Sqrt((nx - t.Center.X) ^ 2 + (ny - t.Center.Y) ^ 2)
            t.PathLength += moved
            t.Center = New NormPoint(nx, ny)
            Dim w As Double = 0.5 * t.Box.Width + 0.5 * det.Box.Width
            Dim h As Double = 0.5 * t.Box.Height + 0.5 * det.Box.Height
            t.Box = New NormRect(nx - w / 2.0, ny - h / 2.0, w, h)
            t.Hits += 1
            t.Misses = 0
            t.LastSeenUtc = nowUtc
            t.LastUpdateUtc = nowUtc
            If t.Speed > t.MaxSpeed Then t.MaxSpeed = t.Speed
            If t.State = TrackState.Tentative AndAlso t.Hits >= _opt.ConfirmHits Then t.State = TrackState.Confirmed
            t.Trajectory.Add(New TrajectoryPoint(nowUtc, t.Center))
            If t.Trajectory.Count > _opt.MaxTrajectoryPoints Then t.Trajectory.RemoveRange(0, t.Trajectory.Count - _opt.MaxTrajectoryPoints)
        End Sub
    End Class

End Namespace
