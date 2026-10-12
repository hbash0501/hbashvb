Namespace Models

    ''' <summary>معاملات الصورة القابلة للضبط. إضافة معامل جديد = صف جديد في ImageParamCatalog.</summary>
    Public Enum ImageParam
        Brightness
        Contrast
        Saturation
        Sharpness
        ExposureType
        IrCutMode
        WdrMode
        WdrLevel
        BlcMode
        WhiteBalanceStyle
        SupplementLightMode
        IrBrightness
        FocusStyle
        DefogMode
        NoiseReduceLevel
    End Enum

    ''' <summary>وصف معامل صورة: نقطة النهاية في ISAPI، اسم العنصر، والقيم المسموحة.</summary>
    Public NotInheritable Class ImageParamSpec
        Public ReadOnly Property Param As ImageParam
        ''' <summary>المسار مع {ch} للقناة.</summary>
        Public ReadOnly Property EndpointTemplate As String
        Public ReadOnly Property ElementName As String
        Public ReadOnly Property MinValue As Integer
        Public ReadOnly Property MaxValue As Integer
        Public ReadOnly Property AllowedValues As IReadOnlyList(Of String)
        Public ReadOnly Property Feature As CameraFeatures

        Public ReadOnly Property IsNumeric As Boolean
            Get
                Return AllowedValues Is Nothing
            End Get
        End Property

        Public Sub New(param As ImageParam, endpointTemplate As String, elementName As String, feature As CameraFeatures,
                       minValue As Integer, maxValue As Integer, allowed As IReadOnlyList(Of String))
            Me.Param = param
            Me.EndpointTemplate = endpointTemplate
            Me.ElementName = elementName
            Me.Feature = feature
            Me.MinValue = minValue
            Me.MaxValue = maxValue
            Me.AllowedValues = allowed
        End Sub

        ''' <summary>يتحقق من القيمة ويعيدها بصيغتها النصية الآمنة، أو يرمي ArgumentException.</summary>
        Public Function Normalize(value As String) As String
            If value Is Nothing Then Throw New ArgumentException($"القيمة مطلوبة للمعامل {Param}.")
            If IsNumeric Then
                Dim n As Integer
                If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, n) Then
                    Throw New ArgumentException($"قيمة غير رقمية للمعامل {Param}.")
                End If
                If n < MinValue OrElse n > MaxValue Then
                    Throw New ArgumentException($"القيمة {n} خارج النطاق [{MinValue},{MaxValue}] للمعامل {Param}.")
                End If
                Return n.ToString(Globalization.CultureInfo.InvariantCulture)
            End If
            For Each a As String In AllowedValues
                If String.Equals(a, value, StringComparison.OrdinalIgnoreCase) Then Return a
            Next
            Throw New ArgumentException($"قيمة غير مسموحة '{value}' للمعامل {Param}.")
        End Function
    End Class

    ''' <summary>كتالوج معاملات الصورة (مصدر وحيد للحقيقة للمسارات والنطاقات).</summary>
    Public NotInheritable Class ImageParamCatalog
        Private Sub New()
        End Sub

        Private Shared ReadOnly _all As IReadOnlyList(Of ImageParamSpec) = Build()

        Public Shared ReadOnly Property All As IReadOnlyList(Of ImageParamSpec)
            Get
                Return _all
            End Get
        End Property

        Public Shared Function [Get](param As ImageParam) As ImageParamSpec
            For Each s As ImageParamSpec In _all
                If s.Param = param Then Return s
            Next
            Throw New ArgumentOutOfRangeException(NameOf(param))
        End Function

        Private Shared Function Num(p As ImageParam, ep As String, el As String, f As CameraFeatures, lo As Integer, hi As Integer) As ImageParamSpec
            Return New ImageParamSpec(p, ep, el, f, lo, hi, Nothing)
        End Function

        Private Shared Function Enm(p As ImageParam, ep As String, el As String, f As CameraFeatures, ParamArray v() As String) As ImageParamSpec
            Return New ImageParamSpec(p, ep, el, f, 0, 0, v)
        End Function

        Private Shared Function Build() As IReadOnlyList(Of ImageParamSpec)
            Const Img As String = "/ISAPI/Image/channels/{ch}/"
            Return New List(Of ImageParamSpec) From {
                Num(ImageParam.Brightness, Img & "color", "brightnessLevel", CameraFeatures.Exposure, 0, 100),
                Num(ImageParam.Contrast, Img & "color", "contrastLevel", CameraFeatures.Exposure, 0, 100),
                Num(ImageParam.Saturation, Img & "color", "saturationLevel", CameraFeatures.Exposure, 0, 100),
                Num(ImageParam.Sharpness, Img & "sharpness", "SharpnessLevel", CameraFeatures.Exposure, 0, 100),
                Enm(ImageParam.ExposureType, Img & "exposure", "ExposureType", CameraFeatures.Exposure, "auto", "IrisFirst", "ShutterFirst", "gainFirst", "manual"),
                Enm(ImageParam.IrCutMode, Img & "irCutFilter", "IrcutFilterType", CameraFeatures.IrCut, "auto", "day", "night"),
                Enm(ImageParam.WdrMode, Img & "WDR", "mode", CameraFeatures.Wdr, "open", "close", "auto"),
                Num(ImageParam.WdrLevel, Img & "WDR", "WDRLevel", CameraFeatures.Wdr, 0, 100),
                Enm(ImageParam.BlcMode, Img & "BLC", "BLCMode", CameraFeatures.Blc, "off", "up", "down", "left", "right", "center", "multiArea", "auto", "custom"),
                Enm(ImageParam.WhiteBalanceStyle, Img & "whiteBalance", "WhiteBalanceStyle", CameraFeatures.WhiteBalance, "auto1", "auto2", "manual", "indoor", "outdoor", "daylightLamp", "sodiumLight"),
                Enm(ImageParam.SupplementLightMode, Img & "supplementLight", "supplementLightMode", CameraFeatures.SupplementLight, "close", "irLight", "colorVuWhiteLight", "eventIntelligence", "schedule"),
                Num(ImageParam.IrBrightness, Img & "supplementLight", "irLightBrightness", CameraFeatures.SupplementLight, 0, 100),
                Enm(ImageParam.FocusStyle, Img & "focusConfiguration", "focusStyle", CameraFeatures.Focus, "AUTO", "MANUAL", "SEMIAUTOMATIC"),
                Enm(ImageParam.DefogMode, Img & "defog", "DefogMode", CameraFeatures.Defog, "open", "close", "auto"),
                Num(ImageParam.NoiseReduceLevel, Img & "noiseReduce", "generalLevel", CameraFeatures.NoiseReduction, 0, 100)
            }
        End Function
    End Class

    ''' <summary>مجموعة قيم معاملات صورة (قراءة/كتابة جزئية).</summary>
    Public NotInheritable Class ImageSettings
        Private ReadOnly _values As New Dictionary(Of ImageParam, String)()

        Default Public Property Item(param As ImageParam) As String
            Get
                Dim v As String = Nothing
                If _values.TryGetValue(param, v) Then Return v
                Return Nothing
            End Get
            Set(value As String)
                If value Is Nothing Then
                    _values.Remove(param)
                Else
                    _values(param) = ImageParamCatalog.Get(param).Normalize(value)
                End If
            End Set
        End Property

        Public ReadOnly Property Values As IReadOnlyDictionary(Of ImageParam, String)
            Get
                Return _values
            End Get
        End Property

        Public ReadOnly Property Count As Integer
            Get
                Return _values.Count
            End Get
        End Property
    End Class

End Namespace
