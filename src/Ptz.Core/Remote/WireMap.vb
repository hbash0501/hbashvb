Imports Ptz.Core.Abstractions
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision

Namespace Remote

    ''' <summary>طلب مرفوض لخلل في حقل محدد (يُحوَّل إلى 400 bad_request مع اسم الحقل).</summary>
    Public Class WireValidationException
        Inherits ArgumentException
        Public ReadOnly Property FieldName As String

        Public Sub New(field As String, message As String)
            MyBase.New(message)
            FieldName = field
        End Sub
    End Class

    ''' <summary>التحويل بين نماذج النطاق وDTO المنقولة + التحقق الصارم من المدخلات الواردة.</summary>
    Public NotInheritable Class WireMap
        Private Sub New()
        End Sub

        Public Const MaxZones As Integer = 32
        Public Const MaxLines As Integer = 32
        Public Const MaxMasks As Integer = 16
        Public Const MaxPolygonPoints As Integer = 64

        ' ---------------- الأسماء ----------------
        Public Shared Function CamelName(name As String) As String
            If String.IsNullOrEmpty(name) Then Return name
            Return Char.ToLowerInvariant(name(0)) & name.Substring(1)
        End Function

        Public Shared Function FeatureNames(features As CameraFeatures) As List(Of String)
            Dim list As New List(Of String)()
            For Each f As CameraFeatures In [Enum].GetValues(GetType(CameraFeatures))
                If f = CameraFeatures.None OrElse f = CameraFeatures.All Then Continue For
                If (features And f) = f Then list.Add(CamelName(f.ToString()))
            Next
            Return list
        End Function

        Public Shared Function ParseFeatures(names As IEnumerable(Of String)) As CameraFeatures
            Dim result As CameraFeatures = CameraFeatures.None
            If names Is Nothing Then Return result
            For Each f As CameraFeatures In [Enum].GetValues(GetType(CameraFeatures))
                If f = CameraFeatures.None OrElse f = CameraFeatures.All Then Continue For
                If names.Contains(CamelName(f.ToString())) Then result = result Or f
            Next
            Return result
        End Function

        Public Shared Function PermissionNames(permissions As Permission) As List(Of String)
            Dim list As New List(Of String)()
            For Each p As Permission In [Enum].GetValues(GetType(Permission))
                If p = Permission.None OrElse p = Permission.All Then Continue For
                If (permissions And p) = p Then list.Add(CamelName(p.ToString()))
            Next
            Return list
        End Function

        Public Shared Function ParsePermission(name As String) As Permission
            If name Is Nothing Then Return Permission.None
            For Each p As Permission In [Enum].GetValues(GetType(Permission))
                If p <> Permission.None AndAlso p <> Permission.All AndAlso String.Equals(CamelName(p.ToString()), name, StringComparison.OrdinalIgnoreCase) Then Return p
            Next
            Return Permission.None
        End Function

        Public Shared Function RoleName(r As Role) As String
            Return CamelName(r.ToString())
        End Function

        Public Shared Function ParseRole(name As String, ByRef role As Role) As Boolean
            For Each r As Role In [Enum].GetValues(GetType(Role))
                If String.Equals(r.ToString(), name, StringComparison.OrdinalIgnoreCase) Then
                    role = r
                    Return True
                End If
            Next
            Return False
        End Function

        Public Shared Function ImageKey(p As ImageParam) As String
            Return CamelName(p.ToString())
        End Function

        Public Shared Function TryParseImageKey(key As String, ByRef param As ImageParam) As Boolean
            For Each p As ImageParam In [Enum].GetValues(GetType(ImageParam))
                If String.Equals(ImageKey(p), key, StringComparison.Ordinal) Then
                    param = p
                    Return True
                End If
            Next
            Return False
        End Function

        Public Shared Function SeverityName(s As AlarmSeverity) As String
            Return CamelName(s.ToString())
        End Function

        Public Shared Function ParseSeverity(name As String) As AlarmSeverity
            For Each s As AlarmSeverity In [Enum].GetValues(GetType(AlarmSeverity))
                If String.Equals(s.ToString(), name, StringComparison.OrdinalIgnoreCase) Then Return s
            Next
            Return AlarmSeverity.Info
        End Function

        ' ---------------- التحقق من النصوص ----------------
        ''' <summary>اسم موضع/منطقة/خط: 1..32 حرفاً (حروف/أرقام Unicode ومسافة و _ - . ( ))، بلا محارف تحكم أو اتجاه.</summary>
        Public Shared Function ValidateName(value As String, field As String) As String
            If value Is Nothing OrElse value.Length < 1 OrElse value.Length > 32 Then Throw New WireValidationException(field, "الاسم من 1 إلى 32 حرفاً.")
            For Each c As Char In value
                If Char.IsLetterOrDigit(c) OrElse c = " "c OrElse c = "_"c OrElse c = "-"c OrElse c = "."c OrElse c = "("c OrElse c = ")"c Then Continue For
                Throw New WireValidationException(field, "الاسم يحوي محارف غير مسموحة.")
            Next
            Return value
        End Function

        Private Shared Function Finite01(v As Double, field As String) As Double
            If Double.IsNaN(v) OrElse Double.IsInfinity(v) OrElse v < 0.0 OrElse v > 1.0 Then Throw New WireValidationException(field, "الإحداثيات يجب أن تكون بين 0 و1.")
            Return v
        End Function

        Private Shared Function ToPoint(p As PointDto, field As String) As NormPoint
            If p Is Nothing Then Throw New WireValidationException(field, "نقطة مفقودة.")
            Return New NormPoint(Finite01(p.X, field), Finite01(p.Y, field))
        End Function

        Private Shared Function ToPolygon(points As List(Of PointDto), field As String) As List(Of NormPoint)
            If points Is Nothing OrElse points.Count < 3 OrElse points.Count > MaxPolygonPoints Then
                Throw New WireValidationException(field, $"المضلّع من 3 إلى {MaxPolygonPoints} نقطة.")
            End If
            Return points.Select(Function(p) ToPoint(p, field)).ToList()
        End Function

        ' ---------------- الهندسة ----------------
        Public Shared Function ToGeometry(dto As GeometryDto) As SmartGeometry
            If dto Is Nothing Then Throw New WireValidationException("geometry", "الجسم مفقود.")
            If dto.Zones Is Nothing OrElse dto.Lines Is Nothing OrElse dto.Masks Is Nothing Then Throw New WireValidationException("geometry", "حقول ناقصة.")
            If dto.Zones.Count > MaxZones Then Throw New WireValidationException("zones", $"الحد الأقصى {MaxZones} منطقة.")
            If dto.Lines.Count > MaxLines Then Throw New WireValidationException("lines", $"الحد الأقصى {MaxLines} خطاً.")
            If dto.Masks.Count > MaxMasks Then Throw New WireValidationException("masks", $"الحد الأقصى {MaxMasks} منطقة إخفاء.")
            Dim zones As New List(Of RestrictedZone)()
            For Each z As ZoneDto In dto.Zones
                If z Is Nothing Then Throw New WireValidationException("zones", "عنصر مفقود.")
                Dim sev As AlarmSeverity = ParseSeverityStrict(z.Severity, "zones.severity")
                zones.Add(New RestrictedZone(ValidateName(z.Name, "zones.name"), ToPolygon(z.Points, "zones.points"), sev))
            Next
            Dim lines As New List(Of TripLine)()
            For Each l As LineDto In dto.Lines
                If l Is Nothing Then Throw New WireValidationException("lines", "عنصر مفقود.")
                lines.Add(New TripLine(ValidateName(l.Name, "lines.name"), ToPoint(l.A, "lines.a"), ToPoint(l.B, "lines.b"), ParseDirectionStrict(l.Direction)))
            Next
            Dim masks As New List(Of IReadOnlyList(Of NormPoint))()
            For Each m As List(Of PointDto) In dto.Masks
                masks.Add(ToPolygon(m, "masks"))
            Next
            Return New SmartGeometry(zones, lines, masks)
        End Function

        Private Shared Function ParseSeverityStrict(name As String, field As String) As AlarmSeverity
            For Each s As AlarmSeverity In [Enum].GetValues(GetType(AlarmSeverity))
                If String.Equals(s.ToString(), name, StringComparison.OrdinalIgnoreCase) Then Return s
            Next
            Throw New WireValidationException(field, "خطورة غير معروفة.")
        End Function

        Private Shared Function ParseDirectionStrict(name As String) As LineDirection
            For Each d As LineDirection In [Enum].GetValues(GetType(LineDirection))
                If String.Equals(d.ToString(), name, StringComparison.OrdinalIgnoreCase) Then Return d
            Next
            Throw New WireValidationException("lines.direction", "اتجاه غير معروف.")
        End Function

        Public Shared Function FromGeometry(g As SmartGeometry) As GeometryDto
            Dim dto As New GeometryDto()
            For Each z As RestrictedZone In g.Zones
                dto.Zones.Add(New ZoneDto With {.Name = z.Name, .Severity = SeverityName(z.Severity), .Points = z.Points.Select(Function(p) New PointDto With {.X = p.X, .Y = p.Y}).ToList()})
            Next
            For Each l As TripLine In g.Lines
                dto.Lines.Add(New LineDto With {.Name = l.Name, .Direction = CamelName(l.Direction.ToString()),
                                                 .A = New PointDto With {.X = l.A.X, .Y = l.A.Y}, .B = New PointDto With {.X = l.B.X, .Y = l.B.Y}})
            Next
            For Each m As IReadOnlyList(Of NormPoint) In g.Masks
                dto.Masks.Add(m.Select(Function(p) New PointDto With {.X = p.X, .Y = p.Y}).ToList())
            Next
            Return dto
        End Function

        ' ---------------- عتبات السلوك ----------------
        Private Shared Function InRange(v As Double, lo As Double, hi As Double, field As String) As Double
            If Double.IsNaN(v) OrElse Double.IsInfinity(v) OrElse v < lo OrElse v > hi Then Throw New WireValidationException(field, $"القيمة خارج النطاق [{lo},{hi}].")
            Return v
        End Function

        Public Shared Function ToBehavior(d As BehaviorDto) As BehaviorOptions
            If d Is Nothing Then Throw New WireValidationException("behavior", "الجسم مفقود.")
            Return New BehaviorOptions With {
                .LoiterSeconds = InRange(d.LoiterSeconds, 0.1, 3600, "loiterSeconds"),
                .LoiterRadius = InRange(d.LoiterRadius, 0.005, 0.5, "loiterRadius"),
                .RunSpeed = InRange(d.RunSpeed, 0.01, 5, "runSpeed"),
                .RunMinSeconds = InRange(d.RunMinSeconds, 0.1, 60, "runMinSeconds"),
                .StationarySeconds = InRange(d.StationarySeconds, 0.1, 3600, "stationarySeconds"),
                .StationarySpeed = InRange(d.StationarySpeed, 0.0001, 1, "stationarySpeed"),
                .ErraticReversals = CInt(InRange(d.ErraticReversals, 1, 50, "erraticReversals")),
                .ErraticWindowSeconds = InRange(d.ErraticWindowSeconds, 0.5, 300, "erraticWindowSeconds"),
                .ErraticMinSegment = InRange(d.ErraticMinSegment, 0.001, 0.5, "erraticMinSegment"),
                .GatherCount = CInt(InRange(d.GatherCount, 2, 50, "gatherCount")),
                .GatherRadius = InRange(d.GatherRadius, 0.01, 0.5, "gatherRadius"),
                .EventCooldownSeconds = InRange(d.EventCooldownSeconds, 0, 3600, "eventCooldownSeconds"),
                .MinTrackAgeSeconds = InRange(d.MinTrackAgeSeconds, 0, 60, "minTrackAgeSeconds")}
        End Function

        Public Shared Function FromBehavior(o As BehaviorOptions) As BehaviorDto
            Return New BehaviorDto With {
                .LoiterSeconds = o.LoiterSeconds, .LoiterRadius = o.LoiterRadius, .RunSpeed = o.RunSpeed, .RunMinSeconds = o.RunMinSeconds,
                .StationarySeconds = o.StationarySeconds, .StationarySpeed = o.StationarySpeed, .ErraticReversals = o.ErraticReversals,
                .ErraticWindowSeconds = o.ErraticWindowSeconds, .ErraticMinSegment = o.ErraticMinSegment, .GatherCount = o.GatherCount,
                .GatherRadius = o.GatherRadius, .EventCooldownSeconds = o.EventCooldownSeconds, .MinTrackAgeSeconds = o.MinTrackAgeSeconds}
        End Function

        ' ---------------- الإنذارات ----------------
        Public Shared Function ToAlarmDto(a As AlarmEvent, cameraId As String, seq As Long, ackedBy As String) As AlarmDto
            Return New AlarmDto With {
                .Seq = seq, .Id = a.Id, .TimeUtc = DateTime.SpecifyKind(a.TimeUtc, DateTimeKind.Utc), .CameraId = cameraId, .Kind = AuditSanitizer.Clean(a.Kind, 64),
                .Severity = SeverityName(a.Severity), .Message = AuditSanitizer.Clean(a.Message, 200), .TrackId = a.TrackId,
                .X = a.Position.X, .Y = a.Position.Y, .Acknowledged = a.Acknowledged, .AckedBy = ackedBy}
        End Function

        Public Shared Function FromAlarmDto(d As AlarmDto, cameraName As String) As AlarmEvent
            Return New AlarmEvent With {
                .Id = d.Id, .TimeUtc = DateTime.SpecifyKind(d.TimeUtc, DateTimeKind.Utc), .Camera = cameraName, .Kind = AuditSanitizer.Clean(d.Kind, 64),
                .Severity = ParseSeverity(d.Severity), .Message = AuditSanitizer.Clean(d.Message, 200), .TrackId = d.TrackId,
                .Position = New NormPoint(Math.Min(1.0, Math.Max(0.0, d.X)), Math.Min(1.0, Math.Max(0.0, d.Y))), .Acknowledged = d.Acknowledged}
        End Function

        ' ---------------- التراكب (SSE ov) ----------------
        Public Const FlagSuspended As Integer = 1
        Public Const FlagWarmingUp As Integer = 2
        Public Const FlagIllumination As Integer = 4
        Public Const FlagAutoTrack As Integer = 8
        Public Const FlagLearning As Integer = 16

        Private Shared Function Q(v As Double) As Integer
            Return CInt(Math.Max(0.0, Math.Min(1000.0, Math.Round(v * 1000.0))))
        End Function

        Public Shared Function ToOverlay(r As SmartFrameResult, frameSeq As Long) As OverlayWire
            Dim w As New OverlayWire With {
                .Seq = frameSeq,
                .T = New DateTimeOffset(DateTime.SpecifyKind(r.TimestampUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                .Fl = (If(r.Suspended, FlagSuspended, 0)) Or (If(r.WarmingUp, FlagWarmingUp, 0)) Or (If(r.IlluminationChange, FlagIllumination, 0)) Or
                      (If(r.AutoTrackActive, FlagAutoTrack, 0)) Or (If(r.AnomalyLearning, FlagLearning, 0))}
            For Each t As TrackView In r.Tracks.OrderByDescending(Function(x) x.Box.Area).Take(8)
                Dim tw As New TrackWire With {.I = t.Id, .B = {Q(t.Box.X), Q(t.Box.Y), Q(t.Box.Width), Q(t.Box.Height)},
                                              .S = CInt(Math.Max(0, Math.Min(100000, Math.Round(t.Speed * 1000.0)))), .K = If(t.Confirmed, 1, 0)}
                Dim trail As List(Of NormPoint) = t.Trail.Skip(Math.Max(0, t.Trail.Count - 20)).ToList()
                Dim flat(trail.Count * 2 - 1) As Integer
                For i As Integer = 0 To trail.Count - 1
                    flat(i * 2) = Q(trail(i).X)
                    flat(i * 2 + 1) = Q(trail(i).Y)
                Next
                tw.P = flat
                w.Tr.Add(tw)
            Next
            If r.Events IsNot Nothing AndAlso r.Events.Count > 0 Then
                w.Ev = New List(Of EventWire)()
                For Each e As BehaviorEvent In r.Events.Take(8)
                    w.Ev.Add(New EventWire With {.K = e.Kind.ToString(), .I = e.TrackId, .X = Q(e.Position.X), .Y = Q(e.Position.Y), .V = CInt(e.Severity),
                                                 .M = AuditSanitizer.Clean(e.Message, 200)})
                Next
            End If
            Return w
        End Function

        Public Shared Function FromOverlay(w As OverlayWire) As SmartFrameResult
            Dim views As New List(Of TrackView)()
            For Each t As TrackWire In w.Tr
                If t.B Is Nothing OrElse t.B.Length <> 4 Then Continue For
                Dim trail As New List(Of NormPoint)()
                If t.P IsNot Nothing Then
                    For i As Integer = 0 To t.P.Length \ 2 - 1
                        trail.Add(New NormPoint(t.P(i * 2) / 1000.0, t.P(i * 2 + 1) / 1000.0))
                    Next
                End If
                views.Add(New TrackView(t.I, New NormRect(t.B(0) / 1000.0, t.B(1) / 1000.0, t.B(2) / 1000.0, t.B(3) / 1000.0), t.K = 1, t.S / 1000.0, trail))
            Next
            Dim events As New List(Of BehaviorEvent)()
            If w.Ev IsNot Nothing Then
                For Each e As EventWire In w.Ev
                    Dim kind As BehaviorKind
                    If Not [Enum].TryParse(Of BehaviorKind)(e.K, kind) Then Continue For
                    events.Add(New BehaviorEvent With {.Kind = kind, .TrackId = e.I, .TimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(w.T).UtcDateTime,
                                                       .Severity = CType(Math.Max(0, Math.Min(2, e.V)), AlarmSeverity), .Message = e.M,
                                                       .Position = New NormPoint(e.X / 1000.0, e.Y / 1000.0)})
                Next
            End If
            Return New SmartFrameResult With {
                .TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(w.T).UtcDateTime,
                .Tracks = views, .Events = events,
                .Suspended = (w.Fl And FlagSuspended) <> 0, .WarmingUp = (w.Fl And FlagWarmingUp) <> 0,
                .IlluminationChange = (w.Fl And FlagIllumination) <> 0, .AutoTrackActive = (w.Fl And FlagAutoTrack) <> 0,
                .AnomalyLearning = (w.Fl And FlagLearning) <> 0}
        End Function

        ' ---------------- إعدادات الصورة ----------------
        ''' <summary>يبني ImageSettings عبر الفهرس (فيتحقق بالمنطق نفسه محلياً). خطأ ⇒ WireValidationException باسم المفتاح.</summary>
        Public Shared Function ToImageSettings(values As Dictionary(Of String, String)) As ImageSettings
            If values Is Nothing OrElse values.Count = 0 Then Throw New WireValidationException("values", "لا قيم.")
            Dim s As New ImageSettings()
            For Each kv In values
                Dim p As ImageParam
                If Not TryParseImageKey(kv.Key, p) Then Throw New WireValidationException(kv.Key, "معامل مجهول.")
                Try
                    s(p) = kv.Value
                Catch ex As ArgumentException
                    Throw New WireValidationException(kv.Key, "قيمة غير مسموحة.")
                End Try
            Next
            Return s
        End Function

        Public Shared Function FromImageSettings(s As ImageSettings) As Dictionary(Of String, String)
            Dim d As New Dictionary(Of String, String)()
            For Each kv In s.Values
                d(ImageKey(kv.Key)) = kv.Value
            Next
            Return d
        End Function

        ' ---------------- مساعدات ----------------
        Public Shared Function ParseLensDirection(name As String) As LensDirection
            Select Case name
                Case "near" : Return LensDirection.Near
                Case "far" : Return LensDirection.Far
                Case "halt" : Return LensDirection.Halt
                Case Else : Throw New WireValidationException("direction", "اتجاه غير معروف.")
            End Select
        End Function

        Public Shared Function LensDirectionName(d As LensDirection) As String
            Select Case d
                Case LensDirection.Near : Return "near"
                Case LensDirection.Far : Return "far"
                Case Else : Return "halt"
            End Select
        End Function
    End Class

End Namespace
