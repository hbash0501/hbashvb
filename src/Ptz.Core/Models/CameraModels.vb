Namespace Models

    ''' <summary>الخصائص التي قد تدعمها كاميرا PTZ. تُحدَّد افتراضياً من الملف التعريفي ثم تُصحَّح بالفحص الفعلي للجهاز.</summary>
    <Flags>
    Public Enum CameraFeatures As Long
        None = 0
        Ptz = 1L << 0
        Presets = 1L << 1
        Patrol = 1L << 2
        Pattern = 1L << 3
        HomePosition = 1L << 4
        Position3D = 1L << 5
        Focus = 1L << 6
        IrCut = 1L << 7
        SupplementLight = 1L << 8
        Wiper = 1L << 9
        Defog = 1L << 10
        Wdr = 1L << 11
        Blc = 1L << 12
        WhiteBalance = 1L << 13
        Exposure = 1L << 14
        NoiseReduction = 1L << 15
        Snapshot = 1L << 16
        AlertStream = 1L << 17
        LowLight = 1L << 18
        All = (1L << 19) - 1L
    End Enum

    ''' <summary>الملف التعريفي الثابت لطراز كاميرا (قيم افتراضية من المواصفات المعلنة).</summary>
    Public NotInheritable Class CameraProfile
        Public ReadOnly Property ModelName As String
        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property ControlChannel As Integer
        Public ReadOnly Property StreamChannel As Integer
        Public ReadOnly Property ResolutionWidth As Integer
        Public ReadOnly Property ResolutionHeight As Integer
        Public ReadOnly Property OpticalZoomMax As Integer
        Public ReadOnly Property MaxPresets As Integer
        Public ReadOnly Property MaxPatrols As Integer
        Public ReadOnly Property MaxPatterns As Integer
        Public ReadOnly Property DefaultFeatures As CameraFeatures
        Public ReadOnly Property Notes As String

        Public Sub New(modelName As String, displayName As String, controlChannel As Integer, streamChannel As Integer,
                       resolutionWidth As Integer, resolutionHeight As Integer, opticalZoomMax As Integer,
                       maxPresets As Integer, maxPatrols As Integer, maxPatterns As Integer,
                       defaultFeatures As CameraFeatures, notes As String)
            Me.ModelName = modelName
            Me.DisplayName = displayName
            Me.ControlChannel = controlChannel
            Me.StreamChannel = streamChannel
            Me.ResolutionWidth = resolutionWidth
            Me.ResolutionHeight = resolutionHeight
            Me.OpticalZoomMax = opticalZoomMax
            Me.MaxPresets = maxPresets
            Me.MaxPatrols = maxPatrols
            Me.MaxPatterns = maxPatterns
            Me.DefaultFeatures = defaultFeatures
            Me.Notes = notes
        End Sub
    End Class

    ''' <summary>الملفات التعريفية للكاميرتين المدعومتين.</summary>
    Public NotInheritable Class CameraProfiles
        Private Sub New()
        End Sub

        Private Const Common As CameraFeatures =
            CameraFeatures.Ptz Or CameraFeatures.Presets Or CameraFeatures.Patrol Or CameraFeatures.Pattern Or
            CameraFeatures.HomePosition Or CameraFeatures.Position3D Or CameraFeatures.Focus Or CameraFeatures.IrCut Or
            CameraFeatures.SupplementLight Or CameraFeatures.Wiper Or CameraFeatures.Defog Or CameraFeatures.Wdr Or
            CameraFeatures.Blc Or CameraFeatures.WhiteBalance Or CameraFeatures.Exposure Or CameraFeatures.NoiseReduction Or
            CameraFeatures.Snapshot Or CameraFeatures.AlertStream

        ''' <summary>DS-2SE7C432MW-AEB — 4MP، تكبير بصري 32x.</summary>
        Public Shared ReadOnly Property Ds2Se7c432mwAeb As CameraProfile = New CameraProfile(
            "DS-2SE7C432MW-AEB", "Hikvision DS-2SE7C432MW-AEB (4MP PTZ)", 1, 101, 2560, 1440, 32, 300, 8, 4,
            Common Or CameraFeatures.LowLight,
            "قيم افتراضية من المواصفات المعلنة؛ تُصحَّح تلقائياً عبر فحص القدرات (CapabilityProbe) عند الاتصال.")

        ''' <summary>DS-2DE7232IW-AE — 2MP، تكبير بصري 32x.</summary>
        Public Shared ReadOnly Property Ds2De7232iwAe As CameraProfile = New CameraProfile(
            "DS-2DE7232IW-AE", "Hikvision DS-2DE7232IW-AE (2MP PTZ)", 1, 101, 1920, 1080, 32, 300, 8, 4,
            Common,
            "قيم افتراضية من المواصفات المعلنة؛ تُصحَّح تلقائياً عبر فحص القدرات (CapabilityProbe) عند الاتصال.")
    End Class

    ''' <summary>معلومات الجهاز كما تُرجعها الكاميرا.</summary>
    Public NotInheritable Class DeviceInfo
        Public Property DeviceName As String = ""
        Public Property Model As String = ""
        Public Property SerialNumber As String = ""
        Public Property FirmwareVersion As String = ""
    End Class

    ''' <summary>معاملات الاتصال بكاميرا. كلمة المرور لا تُسجَّل ولا تُطبع أبداً.</summary>
    Public NotInheritable Class CameraConnection
        Public Property Host As String = ""
        Public Property Port As Integer = 443
        Public Property UseHttps As Boolean = True
        ''' <summary>يجب تفعيله صراحةً للسماح بـ HTTP غير المشفّر (غير موصى به).</summary>
        Public Property AllowInsecureHttp As Boolean = False
        Public Property Username As String = ""
        Public Property Password As String = ""
        ''' <summary>بصمة SHA-256 لشهادة الكاميرا (hex). إن وُجدت تُقبل الشهادة المطابقة فقط (Certificate Pinning).</summary>
        Public Property PinnedCertSha256 As String = ""
        Public Property TimeoutSeconds As Integer = 10

        Public ReadOnly Property BaseUri As Uri
            Get
                Dim scheme As String = If(UseHttps, "https", "http")
                Return New Uri($"{scheme}://{Host}:{Port}/")
            End Get
        End Property

        ''' <summary>يتحقق من صحة الإعدادات ويرمي ArgumentException عند وجود خلل.</summary>
        Public Sub Validate()
            If String.IsNullOrWhiteSpace(Host) Then Throw New ArgumentException("عنوان الكاميرا مطلوب.")
            If Host.IndexOfAny({"/"c, "\"c, "@"c, "?"c, "#"c, " "c}) >= 0 Then Throw New ArgumentException("عنوان الكاميرا غير صالح.")
            If Port < 1 OrElse Port > 65535 Then Throw New ArgumentException("المنفذ يجب أن يكون بين 1 و65535.")
            If Not UseHttps AndAlso Not AllowInsecureHttp Then
                Throw New InvalidOperationException("الاتصال غير المشفّر (HTTP) ممنوع افتراضياً. فعّل HTTPS أو اسمح به صراحةً.")
            End If
            If String.IsNullOrWhiteSpace(Username) Then Throw New ArgumentException("اسم المستخدم مطلوب.")
            If TimeoutSeconds < 1 OrElse TimeoutSeconds > 120 Then Throw New ArgumentException("المهلة يجب أن تكون بين 1 و120 ثانية.")
        End Sub

        Public Overrides Function ToString() As String
            Return $"{If(UseHttps, "https", "http")}://{Host}:{Port} ({Username})"
        End Function
    End Class

End Namespace
