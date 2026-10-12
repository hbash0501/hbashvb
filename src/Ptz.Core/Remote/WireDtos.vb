Namespace Remote

    ' ملاحظة: كل الأنواع المعدودة تُنقل كنصوص camelCase (انظر WireMap) فلا تعتمد صيغة الاتصال على أرقام داخلية.

    Public NotInheritable Class ErrorBody
        Public Property Code As String = ""
        Public Property Message As String = ""
        Public Property RequestId As String = ""
        Public Property Required As String
        Public Property Field As String
        Public Property RetryAfterSeconds As Integer?
        Public Property IsapiStatus As Integer?
        Public Property IsapiSubStatus As String
    End Class

    Public NotInheritable Class ErrorEnvelope
        Public Property [Error] As New ErrorBody()
    End Class

    Public NotInheritable Class LoginRequest
        Public Property UserName As String = ""
        Public Property Password As String = ""
        Public Property Totp As String
        Public Property Client As String
    End Class

    Public NotInheritable Class MeDto
        Public Property UserName As String = ""
        Public Property Role As String = ""
        Public Property Permissions As List(Of String) = New List(Of String)()
        Public Property SessionRef As String = ""
        Public Property CreatedUtc As DateTime
        Public Property IdleExpiresUtc As DateTime
        Public Property AbsoluteExpiresUtc As DateTime
        Public Property StepUpUntilUtc As DateTime?
        Public Property ServerTimeUtc As DateTime
        Public Property ApiVersion As Integer = 1
    End Class

    Public NotInheritable Class LoginOkDto
        Public Property AccessToken As String = ""
        Public Property TokenType As String = "Bearer"
        Public Property IdleTimeoutSeconds As Integer
        Public Property Session As New MeDto()
    End Class

    Public NotInheritable Class StepUpRequest
        Public Property Totp As String = ""
    End Class

    Public NotInheritable Class StepUpResultDto
        Public Property StepUpUntilUtc As DateTime
    End Class

    Public NotInheritable Class SessionItemDto
        Public Property SessionRef As String = ""
        Public Property Client As String = ""
        Public Property IpMasked As String = ""
        Public Property CreatedUtc As DateTime
        Public Property LastActivityUtc As DateTime
        Public Property Current As Boolean
    End Class

    Public NotInheritable Class SessionListDto
        Public Property Items As List(Of SessionItemDto) = New List(Of SessionItemDto)()
    End Class

    Public NotInheritable Class ServerInfoDto
        Public Property ApiVersion As Integer = 1
        Public Property ServerTimeUtc As DateTime
        Public Property Mfa As Boolean
    End Class

    Public NotInheritable Class EngineStateDto
        Public Property MotionEnabled As Boolean?
        Public Property BehaviorEnabled As Boolean?
        Public Property AutoTrackEnabled As Boolean?
        Public Property ForceAnomalyLearning As Boolean?
    End Class

    Public NotInheritable Class ProfileDto
        Public Property ModelName As String = ""
        Public Property ResolutionWidth As Integer
        Public Property ResolutionHeight As Integer
        Public Property OpticalZoomMax As Integer
        Public Property MaxPresets As Integer
        Public Property MaxPatrols As Integer
        Public Property MaxPatterns As Integer
    End Class

    Public NotInheritable Class CameraDto
        Public Property Id As String = ""
        Public Property DisplayName As String = ""
        Public Property Model As String = ""
        Public Property Online As Boolean
        Public Property LastFrameAgeMs As Long?
        Public Property Features As List(Of String) = New List(Of String)()
        Public Property Profile As New ProfileDto()
        Public Property Engine As New EngineStateDto()
        Public Property Viewers As Integer
    End Class

    Public NotInheritable Class CameraListDto
        Public Property Items As List(Of CameraDto) = New List(Of CameraDto)()
    End Class

    Public NotInheritable Class DeviceDto
        Public Property DeviceName As String = ""
        Public Property Model As String = ""
        Public Property SerialNumber As String = ""
        Public Property FirmwareVersion As String = ""
    End Class

    Public NotInheritable Class ConnectResultDto
        Public Property Device As New DeviceDto()
        Public Property Features As List(Of String) = New List(Of String)()
    End Class

    Public NotInheritable Class CameraStatusDto
        Public Property Id As String = ""
        Public Property Online As Boolean
        Public Property LastFrameAgeMs As Long?
        Public Property ConsecutiveFailures As Integer
        Public Property Viewers As Integer
    End Class

    Public NotInheritable Class StatusDto
        Public Property ServerTimeUtc As DateTime
        Public Property UptimeSeconds As Long
        Public Property ApiVersion As Integer = 1
        Public Property Cameras As List(Of CameraStatusDto) = New List(Of CameraStatusDto)()
    End Class

    Public NotInheritable Class MoveRequest
        Public Property Pan As Integer
        Public Property Tilt As Integer
        Public Property Zoom As Integer
        Public Property TtlMs As Integer?
        Public Property Seq As Long?
        ''' <summary>true = إعادة إرسال دورية للحفاظ على العقد (تُتجاهل إن أوقف مشغّل آخر الكاميرا).</summary>
        Public Property Renew As Boolean?
    End Class

    Public NotInheritable Class StopRequest
        Public Property Seq As Long?
    End Class

    Public NotInheritable Class PositionDto
        Public Property Azimuth As Double
        Public Property Elevation As Double
        Public Property Zoom As Double
    End Class

    Public NotInheritable Class ZoomAreaRequest
        Public Property X As Double
        Public Property Y As Double
        Public Property Width As Double
        Public Property Height As Double
    End Class

    Public NotInheritable Class PresetDto
        Public Property Id As Integer
        Public Property Name As String = ""
    End Class

    Public NotInheritable Class PresetListDto
        Public Property Items As List(Of PresetDto) = New List(Of PresetDto)()
    End Class

    Public NotInheritable Class PresetPutRequest
        Public Property Name As String = ""
    End Class

    Public NotInheritable Class LensRequest
        Public Property Direction As String = "halt"
        Public Property Speed As Integer = 50
    End Class

    Public NotInheritable Class ImageDto
        Public Property Values As Dictionary(Of String, String) = New Dictionary(Of String, String)()
        Public Property Supported As List(Of String) = New List(Of String)()
    End Class

    Public NotInheritable Class ImagePutRequest
        Public Property Values As Dictionary(Of String, String) = New Dictionary(Of String, String)()
    End Class

    Public NotInheritable Class AuxRequest
        Public Property Enabled As Boolean
    End Class

    Public NotInheritable Class RebootRequest
        Public Property Confirm As String = ""
    End Class

    Public NotInheritable Class PointDto
        Public Property X As Double
        Public Property Y As Double
    End Class

    Public NotInheritable Class ZoneDto
        Public Property Name As String = ""
        Public Property Severity As String = "critical"
        Public Property Points As List(Of PointDto) = New List(Of PointDto)()
    End Class

    Public NotInheritable Class LineDto
        Public Property Name As String = ""
        Public Property Direction As String = "both"
        Public Property A As New PointDto()
        Public Property B As New PointDto()
    End Class

    Public NotInheritable Class GeometryDto
        Public Property Zones As List(Of ZoneDto) = New List(Of ZoneDto)()
        Public Property Lines As List(Of LineDto) = New List(Of LineDto)()
        Public Property Masks As List(Of List(Of PointDto)) = New List(Of List(Of PointDto))()
    End Class

    Public NotInheritable Class BehaviorDto
        Public Property LoiterSeconds As Double
        Public Property LoiterRadius As Double
        Public Property RunSpeed As Double
        Public Property RunMinSeconds As Double
        Public Property StationarySeconds As Double
        Public Property StationarySpeed As Double
        Public Property ErraticReversals As Integer
        Public Property ErraticWindowSeconds As Double
        Public Property ErraticMinSegment As Double
        Public Property GatherCount As Integer
        Public Property GatherRadius As Double
        Public Property EventCooldownSeconds As Double
        Public Property MinTrackAgeSeconds As Double
    End Class

    Public NotInheritable Class TargetRequest
        Public Property X As Double
        Public Property Y As Double
    End Class

    Public NotInheritable Class AlarmDto
        Public Property Seq As Long
        Public Property Id As Guid
        Public Property TimeUtc As DateTime
        Public Property CameraId As String = ""
        Public Property Kind As String = ""
        Public Property Severity As String = "info"
        Public Property Message As String = ""
        Public Property TrackId As Integer
        Public Property X As Double
        Public Property Y As Double
        Public Property Acknowledged As Boolean
        Public Property AckedBy As String
    End Class

    Public NotInheritable Class AlarmListDto
        Public Property Items As List(Of AlarmDto) = New List(Of AlarmDto)()
        Public Property LastSeq As Long
        Public Property Resync As Boolean
    End Class

    Public NotInheritable Class AuditItemDto
        Public Property Seq As Long
        Public Property TimeUtc As DateTime
        Public Property User As String = ""
        Public Property Action As String = ""
        Public Property Target As String = ""
        Public Property Outcome As String = ""
        Public Property Details As String = ""
    End Class

    Public NotInheritable Class AuditListDto
        Public Property Items As List(Of AuditItemDto) = New List(Of AuditItemDto)()
        Public Property NextBeforeSeq As Long?
    End Class

    Public NotInheritable Class AuditVerifyDto
        Public Property IsValid As Boolean
        Public Property EntriesChecked As Long
        Public Property FirstBadSeq As Long
        Public Property LastHash As String = ""
    End Class

    ' ---------------- أحداث SSE ----------------
    Public NotInheritable Class TrackWire
        ''' <summary>معرّف المسار</summary>
        Public Property I As Integer
        ''' <summary>[x,y,w,h] بالألف من الإطار</summary>
        Public Property B As Integer() = New Integer(3) {}
        ''' <summary>السرعة ×1000</summary>
        Public Property S As Integer
        ''' <summary>1 = مؤكَّد</summary>
        Public Property K As Integer
        ''' <summary>أثر مسطّح [x0,y0,x1,y1,...]</summary>
        Public Property P As Integer() = New Integer() {}
    End Class

    Public NotInheritable Class EventWire
        Public Property K As String = ""
        Public Property I As Integer
        Public Property X As Integer
        Public Property Y As Integer
        Public Property V As Integer
        Public Property M As String = ""
    End Class

    Public NotInheritable Class OverlayWire
        Public Property Seq As Long
        Public Property T As Long
        Public Property Fl As Integer
        Public Property Tr As List(Of TrackWire) = New List(Of TrackWire)()
        Public Property Ev As List(Of EventWire)
    End Class

    Public NotInheritable Class HelloWire
        Public Property V As Integer = 1
        Public Property CameraId As String = ""
        Public Property ServerTimeUtc As DateTime
        Public Property FrameSeq As Long
        Public Property AlarmSeq As Long
        Public Property Online As Boolean
        Public Property Engine As New EngineStateDto()
        Public Property GeometryRev As String = ""
        Public Property Tiers As List(Of String) = New List(Of String)()
        Public Property HeartbeatSeconds As Integer = 15
    End Class

    Public NotInheritable Class AckWire
        Public Property Id As Guid
        Public Property By As String = ""
    End Class

    Public NotInheritable Class StateWire
        Public Property MotionEnabled As Boolean
        Public Property BehaviorEnabled As Boolean
        Public Property AutoTrackEnabled As Boolean
        Public Property ForceAnomalyLearning As Boolean?
        Public Property By As String = ""
    End Class

    Public NotInheritable Class GeoWire
        Public Property Rev As String = ""
        Public Property By As String = ""
    End Class

    Public NotInheritable Class HealthWire
        Public Property Online As Boolean
        Public Property Reason As String = "ok"
        Public Property ConsecutiveFailures As Integer
    End Class

    Public NotInheritable Class ResyncWire
        Public Property What As List(Of String) = New List(Of String)()
    End Class

    Public NotInheritable Class ByeWire
        Public Property Reason As String = ""
    End Class

End Namespace
