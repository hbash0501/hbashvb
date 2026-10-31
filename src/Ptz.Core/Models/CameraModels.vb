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

    ''' <summary>
    ''' طريقة الوصول إلى الكاميرا: <see cref="Lan"/> اتصال ISAPI مباشر داخل الشبكة، أو <see cref="HikPartnerPro"/> تمرير ISAPI الشفاف
    ''' عبر واجهة Hik-Partner Pro OpenAPI السحابية (تنفّذه البوابة فقط، والكاميرا تُعرَّف بالرقم التسلسلي لا بالعنوان).
    ''' </summary>
    Public Enum CameraTransport
        Lan = 0
        HikPartnerPro = 1
    End Enum

    ''' <summary>أدوات النقل: الأسماء النصية في الإعداد، ومجموعة الميزات الموثَّقة عبر النقل السحابي.</summary>
    Public NotInheritable Class CameraTransports
        Private Sub New()
        End Sub

        ''' <summary>
        ''' ما يوثّقه دليل Hik-Partner Pro OpenAPI عبر المرور الشفاف لجهاز PTZ: الحركة المستمرة والمواضع المسبقة فقط.
        ''' لا لقطات ولا فيديو ولا عدسة/صورة/إضاءة (غير مدرجة في قائمة المسارات المدعومة)؛ لذلك لا يعمل المحرك الذكي بهذا النقل.
        ''' </summary>
        Public Shared ReadOnly RelayFeatures As CameraFeatures = CameraFeatures.Ptz Or CameraFeatures.Presets

        Public Shared Function Parse(text As String) As CameraTransport
            If String.IsNullOrWhiteSpace(text) OrElse text.Equals("lan", StringComparison.OrdinalIgnoreCase) Then Return CameraTransport.Lan
            If text.Equals("hpp", StringComparison.OrdinalIgnoreCase) Then Return CameraTransport.HikPartnerPro
            Throw New ArgumentException("النقل غير معروف (lan | hpp).")
        End Function

        Public Shared Function Name(t As CameraTransport) As String
            Return If(t = CameraTransport.HikPartnerPro, "hpp", "lan")
        End Function

        ''' <summary>
        ''' هل يطابق الصيغة التي يذكرها الدليل للرقم التسلسلي السحابي (1–9 حروف/أرقام، مثل Q01728482)؟ ليس شرطاً صارماً
        ''' (قد تختلف أجهزة)، بل يُستعمل للتحذير من خطأ شائع: إدخال الرقم الكامل من واجهة الكاميرا بدل الرقم السحابي القصير.
        ''' </summary>
        Public Shared Function LooksLikeGuideSerial(serial As String) As Boolean
            If String.IsNullOrEmpty(serial) OrElse serial.Length > 9 Then Return False
            For Each ch As Char In serial
                If Not Char.IsAsciiLetterOrDigit(ch) Then Return False
            Next
            Return True
        End Function

        ''' <summary>الرقم التسلسلي السحابي: حروف وأرقام وشرطتان فقط (يُرسَل في ترويسة طلب فلا يجوز أي محرف تحكمي).</summary>
        Public Shared Function IsValidCloudSerial(serial As String) As Boolean
            If String.IsNullOrEmpty(serial) OrElse serial.Length > 64 Then Return False
            For Each ch As Char In serial
                If Not (Char.IsAsciiLetterOrDigit(ch) OrElse ch = "-"c OrElse ch = "_"c) Then Return False
            Next
            Return True
        End Function
    End Class

    ''' <summary>معاملات الاتصال بكاميرا. كلمة المرور لا تُسجَّل ولا تُطبع أبداً.</summary>
    Public NotInheritable Class CameraConnection
        ''' <summary>Lan: Host/Port/Username/Password تُستعمل. HikPartnerPro: <see cref="CloudSerial"/> فقط (والبوابة تحمل مفاتيح السحابة).</summary>
        Public Property Transport As CameraTransport = CameraTransport.Lan
        Public Property CloudSerial As String = ""
        Public ReadOnly Property IsRelay As Boolean
            Get
                Return Transport = CameraTransport.HikPartnerPro
            End Get
        End Property

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
                ' عنوان وهمي للنقل السحابي: المعالج يعيد كتابة الطلب كله ولا يخرج شيء إلى هذا المضيف أبداً (نطاق .invalid محجوز).
                If IsRelay Then Return New Uri("https://hpp-relay.invalid/")
                Dim scheme As String = If(UseHttps, "https", "http")
                Return New Uri($"{scheme}://{Host}:{Port}/")
            End Get
        End Property

        ''' <summary>يتحقق من صحة الإعدادات ويرمي ArgumentException عند وجود خلل.</summary>
        Public Sub Validate()
            If TimeoutSeconds < 1 OrElse TimeoutSeconds > 120 Then Throw New ArgumentException("المهلة يجب أن تكون بين 1 و120 ثانية.")
            If IsRelay Then
                If Not CameraTransports.IsValidCloudSerial(CloudSerial) Then Throw New ArgumentException("الرقم التسلسلي السحابي مطلوب (حروف وأرقام و- و_ حتى 64).")
                Return
            End If
            If String.IsNullOrWhiteSpace(Host) Then Throw New ArgumentException("عنوان الكاميرا مطلوب.")
            If Host.IndexOfAny({"/"c, "\"c, "@"c, "?"c, "#"c, " "c}) >= 0 Then Throw New ArgumentException("عنوان الكاميرا غير صالح.")
            If Port < 1 OrElse Port > 65535 Then Throw New ArgumentException("المنفذ يجب أن يكون بين 1 و65535.")
            If Not UseHttps AndAlso Not AllowInsecureHttp Then
                Throw New InvalidOperationException("الاتصال غير المشفّر (HTTP) ممنوع افتراضياً. فعّل HTTPS أو اسمح به صراحةً.")
            End If
            If String.IsNullOrWhiteSpace(Username) Then Throw New ArgumentException("اسم المستخدم مطلوب.")
        End Sub

        Public Overrides Function ToString() As String
            If IsRelay Then Return $"hpp://{CloudSerial}"
            Return $"{If(UseHttps, "https", "http")}://{Host}:{Port} ({Username})"
        End Function
    End Class

End Namespace
