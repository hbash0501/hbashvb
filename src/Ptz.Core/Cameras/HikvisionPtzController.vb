Imports System.Xml.Linq
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models

Namespace Cameras

    ''' <summary>
    ''' التنفيذ المشترك للتحكم عبر ISAPI (البروتوكول الرسمي لشركة Hikvision). الطرازان يرثانه
    ''' ويحدّدان ملفهما التعريفي وخصوصياتهما فقط.
    ''' </summary>
    Public MustInherit Class HikvisionPtzController
        Implements ICameraController

        Private ReadOnly _client As IsapiClient
        Private ReadOnly _ptzGate As New Threading.SemaphoreSlim(1, 1)
        Private _features As CameraFeatures
        Private _useMomentary As Boolean = True

        Protected Sub New(profile As CameraProfile, client As IsapiClient)
            If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))
            If client Is Nothing Then Throw New ArgumentNullException(NameOf(client))
            Me.Profile = profile
            _client = client
            _features = profile.DefaultFeatures
        End Sub

        Public ReadOnly Property Profile As CameraProfile Implements ICameraController.Profile

        Public ReadOnly Property Features As CameraFeatures Implements ICameraController.Features
            Get
                Return _features
            End Get
        End Property

        ''' <summary>مهلة أمان بالميلي ثانية: تتوقف الكاميرا تلقائياً إن انقطع التحكم (0 = حركة مستمرة بلا مهلة).</summary>
        Public Property MoveWatchdogMs As Integer = 1000

        Protected ReadOnly Property Client As IsapiClient
            Get
                Return _client
            End Get
        End Property

        Protected ReadOnly Property Channel As Integer
            Get
                Return Profile.ControlChannel
            End Get
        End Property

        ''' <summary>معرّفات الأجهزة الإضافية — قابلة للتجاوز لكل طراز/إصدار.</summary>
        Protected Overridable Function AuxId(kind As AuxKind) As Integer
            Return If(kind = AuxKind.Wiper, 1, 2)
        End Function

        Protected Overridable Function AuxType(kind As AuxKind) As String
            Return If(kind = AuxKind.Wiper, "WIPER", "LIGHT")
        End Function

        Private Sub Require(feature As CameraFeatures)
            If (_features And feature) <> feature Then
                Throw New NotSupportedException($"الميزة {feature} غير مدعومة في {Profile.ModelName}.")
            End If
        End Sub

        ' ============ الاتصال والفحص ============
        Public Overridable Async Function ConnectAsync(ct As Threading.CancellationToken) As Task(Of DeviceInfo) Implements ICameraController.ConnectAsync
            Dim doc As XDocument = Await _client.GetXmlAsync(IsapiPaths.DeviceInfo, ct).ConfigureAwait(False)
            Dim info As New DeviceInfo With {
                .DeviceName = If(IsapiXml.ValueOf(doc, "deviceName"), ""),
                .Model = If(IsapiXml.ValueOf(doc, "model"), ""),
                .SerialNumber = If(IsapiXml.ValueOf(doc, "serialNumber"), ""),
                .FirmwareVersion = If(IsapiXml.ValueOf(doc, "firmwareVersion"), "")
            }
            _features = Await CapabilityProbe.ProbeAsync(_client, Profile, ct).ConfigureAwait(False)
            Return info
        End Function

        ' ============ PTZ ============
        Public Async Function MoveAsync(vector As PtzVector, ct As Threading.CancellationToken) As Task Implements IPtzMover.MoveAsync
            Require(CameraFeatures.Ptz)
            Await _ptzGate.WaitAsync(ct).ConfigureAwait(False)
            Try
                Dim body As XElement = IsapiXml.Build("PTZData", IsapiXml.Num("pan", vector.Pan), IsapiXml.Num("tilt", vector.Tilt), IsapiXml.Num("zoom", vector.Zoom))
                If _useMomentary AndAlso MoveWatchdogMs > 0 AndAlso Not vector.IsZero Then
                    body.Add(IsapiXml.El("Momentary", IsapiXml.Num("duration", MoveWatchdogMs)))
                    Try
                        Await _client.PutAsync(IsapiPaths.Momentary(Channel), body, ct).ConfigureAwait(False)
                        Return
                    Catch ex As IsapiException When ex.HttpStatus = 404 OrElse ex.HttpStatus = 501
                        _useMomentary = False ' الطراز لا يدعم الحركة المؤقتة: نرجع للحركة المستمرة
                        body.Element(IsapiXml.Ns + "Momentary").Remove()
                    End Try
                End If
                Await _client.PutAsync(IsapiPaths.Continuous(Channel), body, ct).ConfigureAwait(False)
            Finally
                _ptzGate.Release()
            End Try
        End Function

        Public Function StopAsync(ct As Threading.CancellationToken) As Task Implements IPtzMover.StopAsync
            Return StopCoreAsync(ct)
        End Function

        Private Async Function StopCoreAsync(ct As Threading.CancellationToken) As Task
            Require(CameraFeatures.Ptz)
            Await _ptzGate.WaitAsync(ct).ConfigureAwait(False)
            Try
                Dim body As XElement = IsapiXml.Build("PTZData", IsapiXml.Num("pan", 0), IsapiXml.Num("tilt", 0), IsapiXml.Num("zoom", 0))
                Await _client.PutAsync(IsapiPaths.Continuous(Channel), body, ct).ConfigureAwait(False)
            Finally
                _ptzGate.Release()
            End Try
        End Function

        Public Async Function GetPositionAsync(ct As Threading.CancellationToken) As Task(Of PtzPosition) Implements ICameraController.GetPositionAsync
            Require(CameraFeatures.Ptz)
            Dim doc As XDocument = Await _client.GetXmlAsync(IsapiPaths.PtzStatus(Channel), ct).ConfigureAwait(False)
            Dim az As Double = ParseTenths(IsapiXml.ValueOf(doc, "azimuth"))
            Dim el As Double = ParseTenths(IsapiXml.ValueOf(doc, "elevation"))
            Dim zm As Double = ParseTenths(IsapiXml.ValueOf(doc, "absoluteZoom"))
            Return New PtzPosition(az, el, zm)
        End Function

        Private Shared Function ParseTenths(text As String) As Double
            Dim n As Integer
            If text IsNot Nothing AndAlso Integer.TryParse(text, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, n) Then
                Return n / 10.0
            End If
            Return 0.0
        End Function

        Public Async Function GoToPositionAsync(position As PtzPosition, ct As Threading.CancellationToken) As Task Implements ICameraController.GoToPositionAsync
            Require(CameraFeatures.Ptz)
            If Double.IsNaN(position.Azimuth) OrElse Double.IsNaN(position.Elevation) OrElse Double.IsNaN(position.Zoom) Then
                Throw New ArgumentException("الموضع يحوي قيمة غير صالحة.")
            End If
            Dim az As Double = ((position.Azimuth Mod 360.0) + 360.0) Mod 360.0
            Dim el As Double = Geometry.Clamp(position.Elevation, -90.0, 90.0)
            Dim zm As Double = Geometry.Clamp(position.Zoom, 1.0, Profile.OpticalZoomMax)
            Dim body As XElement = IsapiXml.Build("PTZData", IsapiXml.El("AbsoluteHigh",
                IsapiXml.Num("elevation", CInt(Math.Round(el * 10))),
                IsapiXml.Num("azimuth", CInt(Math.Round(az * 10))),
                IsapiXml.Num("absoluteZoom", CInt(Math.Round(zm * 10)))))
            Await _client.PutAsync(IsapiPaths.Absolute(Channel), body, ct).ConfigureAwait(False)
        End Function

        Public Async Function ZoomToAreaAsync(area As NormRect, ct As Threading.CancellationToken) As Task Implements ICameraController.ZoomToAreaAsync
            Require(CameraFeatures.Position3D)
            Dim x0 As Double = Geometry.Clamp(area.X, 0, 1)
            Dim y0 As Double = Geometry.Clamp(area.Y, 0, 1)
            Dim x1 As Double = Geometry.Clamp(area.Right, 0, 1)
            Dim y1 As Double = Geometry.Clamp(area.Bottom, 0, 1)
            If x1 - x0 < 0.02 OrElse y1 - y0 < 0.02 Then ' نقرة بدل مستطيل: نوسّط على النقطة بصندوق 10%
                Dim c As NormPoint = area.Center
                x0 = Geometry.Clamp(c.X - 0.05, 0, 1)
                x1 = Geometry.Clamp(c.X + 0.05, 0, 1)
                y0 = Geometry.Clamp(c.Y - 0.05, 0, 1)
                y1 = Geometry.Clamp(c.Y + 0.05, 0, 1)
            End If
            Dim body As XElement = IsapiXml.Build("Position3D",
                IsapiXml.El("StartPoint", IsapiXml.Num("positionX", CInt(Math.Round(x0 * 255))), IsapiXml.Num("positionY", CInt(Math.Round(y0 * 255)))),
                IsapiXml.El("EndPoint", IsapiXml.Num("positionX", CInt(Math.Round(x1 * 255))), IsapiXml.Num("positionY", CInt(Math.Round(y1 * 255)))))
            Await _client.PutAsync(IsapiPaths.Position3D(Channel), body, ct).ConfigureAwait(False)
        End Function

        Public Function GoHomeAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.GoHomeAsync
            Require(CameraFeatures.HomePosition)
            Return _client.PutAsync(IsapiPaths.Home(Channel) & "/goto", Nothing, ct)
        End Function

        Public Function SetHomeAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.SetHomeAsync
            Require(CameraFeatures.HomePosition)
            Return _client.PutAsync(IsapiPaths.Home(Channel), Nothing, ct)
        End Function

        ' ============ المواضع المسبقة والدوريات والأنماط ============
        Private Sub CheckId(id As Integer, max As Integer, what As String)
            If id < 1 OrElse id > max Then Throw New ArgumentOutOfRangeException(NameOf(id), $"{what} يجب أن يكون بين 1 و{max}.")
        End Sub

        Public Async Function GetPresetsAsync(ct As Threading.CancellationToken) As Task(Of IReadOnlyList(Of PtzPreset)) Implements ICameraController.GetPresetsAsync
            Require(CameraFeatures.Presets)
            Dim doc As XDocument = Await _client.GetXmlAsync(IsapiPaths.Presets(Channel), ct).ConfigureAwait(False)
            Dim list As New List(Of PtzPreset)()
            For Each e As XElement In doc.Descendants().Where(Function(x) x.Name.LocalName = "PTZPreset")
                Dim idText As String = IsapiXml.ValueOf(e, "id")
                Dim id As Integer
                If Not Integer.TryParse(idText, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, id) Then Continue For
                Dim enabled As String = IsapiXml.ValueOf(e, "enabled")
                If enabled IsNot Nothing AndAlso enabled.Equals("false", StringComparison.OrdinalIgnoreCase) Then Continue For
                list.Add(New PtzPreset With {.Id = id, .Name = If(IsapiXml.ValueOf(e, "presetName"), $"Preset {id}")})
            Next
            Return list
        End Function

        ''' <summary>ينظّف اسم الموضع: يحذف المحارف التحكمية ويقصّه على 32 حرفاً.</summary>
        Public Shared Function SanitizeName(name As String) As String
            If name Is Nothing Then Return ""
            Dim clean As New Text.StringBuilder()
            For Each c As Char In name
                If Not Char.IsControl(c) AndAlso c <> "<"c AndAlso c <> ">"c AndAlso c <> "&"c Then clean.Append(c)
            Next
            Dim s As String = clean.ToString().Trim()
            Return If(s.Length > 32, s.Substring(0, 32), s)
        End Function

        Public Function SavePresetAsync(id As Integer, name As String, ct As Threading.CancellationToken) As Task Implements ICameraController.SavePresetAsync
            Require(CameraFeatures.Presets)
            CheckId(id, Profile.MaxPresets, "رقم الموضع المسبق")
            Dim body As XElement = IsapiXml.Build("PTZPreset", IsapiXml.Num("id", id), IsapiXml.Txt("presetName", SanitizeName(name)))
            Return _client.PutAsync(IsapiPaths.Preset(Channel, id), body, ct)
        End Function

        Public Function GoToPresetAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.GoToPresetAsync
            Require(CameraFeatures.Presets)
            CheckId(id, Profile.MaxPresets, "رقم الموضع المسبق")
            Return _client.PutAsync(IsapiPaths.Preset(Channel, id) & "/goto", Nothing, ct)
        End Function

        Public Function DeletePresetAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.DeletePresetAsync
            Require(CameraFeatures.Presets)
            CheckId(id, Profile.MaxPresets, "رقم الموضع المسبق")
            Return _client.DeleteAsync(IsapiPaths.Preset(Channel, id), ct)
        End Function

        Public Function StartPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StartPatrolAsync
            Require(CameraFeatures.Patrol)
            CheckId(id, Profile.MaxPatrols, "رقم الدورية")
            Return _client.PutAsync(IsapiPaths.Patrol(Channel, id, "start"), Nothing, ct)
        End Function

        Public Function StopPatrolAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatrolAsync
            Require(CameraFeatures.Patrol)
            CheckId(id, Profile.MaxPatrols, "رقم الدورية")
            Return _client.PutAsync(IsapiPaths.Patrol(Channel, id, "stop"), Nothing, ct)
        End Function

        Public Function StartPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StartPatternRecordingAsync
            Require(CameraFeatures.Pattern)
            CheckId(id, Profile.MaxPatterns, "رقم النمط")
            Return _client.PutAsync(IsapiPaths.Pattern(Channel, id, "recordStart"), Nothing, ct)
        End Function

        Public Function StopPatternRecordingAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatternRecordingAsync
            Require(CameraFeatures.Pattern)
            CheckId(id, Profile.MaxPatterns, "رقم النمط")
            Return _client.PutAsync(IsapiPaths.Pattern(Channel, id, "recordStop"), Nothing, ct)
        End Function

        Public Function RunPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.RunPatternAsync
            Require(CameraFeatures.Pattern)
            CheckId(id, Profile.MaxPatterns, "رقم النمط")
            Return _client.PutAsync(IsapiPaths.Pattern(Channel, id, "run"), Nothing, ct)
        End Function

        Public Function StopPatternAsync(id As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.StopPatternAsync
            Require(CameraFeatures.Pattern)
            CheckId(id, Profile.MaxPatterns, "رقم النمط")
            Return _client.PutAsync(IsapiPaths.Pattern(Channel, id, "stop"), Nothing, ct)
        End Function

        ' ============ العدسة ============
        Public Function MoveFocusAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.MoveFocusAsync
            Require(CameraFeatures.Focus)
            Dim v As Integer = CInt(direction) * Math.Min(100, Math.Max(1, Math.Abs(speed)))
            If direction = LensDirection.Halt Then v = 0
            Return _client.PutAsync(IsapiPaths.Focus(Channel), IsapiXml.Build("FocusData", IsapiXml.Num("focus", v)), ct)
        End Function

        Public Function MoveIrisAsync(direction As LensDirection, speed As Integer, ct As Threading.CancellationToken) As Task Implements ICameraController.MoveIrisAsync
            Require(CameraFeatures.Exposure)
            Dim v As Integer = CInt(direction) * Math.Min(100, Math.Max(1, Math.Abs(speed)))
            If direction = LensDirection.Halt Then v = 0
            Return _client.PutAsync(IsapiPaths.Iris(Channel), IsapiXml.Build("IrisData", IsapiXml.Num("iris", v)), ct)
        End Function

        ' ============ الصورة ============
        Public Async Function GetImageSettingsAsync(ct As Threading.CancellationToken) As Task(Of ImageSettings) Implements ICameraController.GetImageSettingsAsync
            Dim result As New ImageSettings()
            Dim cache As New Dictionary(Of String, XDocument)()
            For Each spec As ImageParamSpec In ImageParamCatalog.All
                If (_features And spec.Feature) <> spec.Feature Then Continue For
                Dim path As String = IsapiPaths.Image(spec.EndpointTemplate, Channel)
                Dim doc As XDocument = Nothing
                If Not cache.TryGetValue(path, doc) Then
                    Try
                        doc = Await _client.GetXmlAsync(path, ct).ConfigureAwait(False)
                    Catch ex As IsapiException When ex.HttpStatus = 404 OrElse ex.HttpStatus = 403 OrElse ex.HttpStatus = 400 OrElse ex.HttpStatus = 501
                        doc = Nothing
                    End Try
                    cache(path) = doc
                End If
                If doc Is Nothing Then Continue For
                Dim v As String = IsapiXml.ValueOf(doc, spec.ElementName)
                If v Is Nothing Then Continue For
                Try
                    result(spec.Param) = v
                Catch ex As ArgumentException
                    ' قيمة غير معروفة من البرنامج الثابت: نتجاهلها بدل الفشل الكامل.
                End Try
            Next
            Return result
        End Function

        Public Async Function ApplyImageSettingsAsync(settings As ImageSettings, ct As Threading.CancellationToken) As Task Implements ICameraController.ApplyImageSettingsAsync
            If settings Is Nothing Then Throw New ArgumentNullException(NameOf(settings))
            Dim byEndpoint As New Dictionary(Of String, List(Of KeyValuePair(Of ImageParamSpec, String)))()
            For Each kv As KeyValuePair(Of ImageParam, String) In settings.Values
                Dim spec As ImageParamSpec = ImageParamCatalog.Get(kv.Key)
                Require(spec.Feature)
                Dim path As String = IsapiPaths.Image(spec.EndpointTemplate, Channel)
                If Not byEndpoint.ContainsKey(path) Then byEndpoint(path) = New List(Of KeyValuePair(Of ImageParamSpec, String))()
                byEndpoint(path).Add(New KeyValuePair(Of ImageParamSpec, String)(spec, spec.Normalize(kv.Value)))
            Next
            For Each entry In byEndpoint
                ' قراءة-تعديل-كتابة: نحافظ على بقية إعدادات القسم كما هي.
                Dim doc As XDocument = Await _client.GetXmlAsync(entry.Key, ct).ConfigureAwait(False)
                For Each item In entry.Value
                    If Not IsapiXml.SetValue(doc, item.Key.ElementName, item.Value) Then
                        Throw New NotSupportedException($"المعامل {item.Key.Param} غير موجود في استجابة الكاميرا.")
                    End If
                Next
                Await _client.PutAsync(entry.Key, doc.Root, ct).ConfigureAwait(False)
            Next
        End Function

        Public Function SetAuxAsync(kind As AuxKind, enabled As Boolean, ct As Threading.CancellationToken) As Task Implements ICameraController.SetAuxAsync
            If kind = AuxKind.Wiper Then Require(CameraFeatures.Wiper) Else Require(CameraFeatures.SupplementLight)
            Dim id As Integer = AuxId(kind)
            Dim body As XElement = IsapiXml.Build("PTZAux", IsapiXml.Num("id", id), IsapiXml.Txt("type", AuxType(kind)), IsapiXml.Txt("status", If(enabled, "on", "off")))
            Return _client.PutAsync(IsapiPaths.Aux(Channel, id), body, ct)
        End Function

        ' ============ الفيديو والصيانة ============
        Public Async Function GetSnapshotAsync(ct As Threading.CancellationToken) As Task(Of Byte()) Implements ICameraController.GetSnapshotAsync
            Require(CameraFeatures.Snapshot)
            Dim data As Byte() = Await _client.GetBytesAsync(IsapiPaths.Snapshot(Profile.StreamChannel), ct).ConfigureAwait(False)
            If data.Length < 4 OrElse data(0) <> &HFF OrElse data(1) <> &HD8 Then
                Throw New IsapiException(200, "", "استجابة اللقطة ليست صورة JPEG صالحة.")
            End If
            Return data
        End Function

        Public Function RebootAsync(ct As Threading.CancellationToken) As Task Implements ICameraController.RebootAsync
            Return _client.PutAsync(IsapiPaths.Reboot, Nothing, ct)
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _client.Dispose()
            _ptzGate.Dispose()
        End Sub
    End Class

    ''' <summary>متحكم الكاميرا DS-2SE7C432MW-AEB.</summary>
    Public NotInheritable Class Ds2Se7c432mwAebController
        Inherits HikvisionPtzController
        Public Sub New(client As IsapiClient)
            MyBase.New(CameraProfiles.Ds2Se7c432mwAeb, client)
        End Sub
    End Class

    ''' <summary>متحكم الكاميرا DS-2DE7232IW-AE.</summary>
    Public NotInheritable Class Ds2De7232iwAeController
        Inherits HikvisionPtzController
        Public Sub New(client As IsapiClient)
            MyBase.New(CameraProfiles.Ds2De7232iwAe, client)
        End Sub
    End Class

    ''' <summary>مصنع المتحكمات: يربط الطراز بالتنفيذ.</summary>
    Public NotInheritable Class CameraControllerFactory
        Private Sub New()
        End Sub

        Public Shared Function Create(profile As CameraProfile, connection As CameraConnection, Optional handler As Net.Http.HttpMessageHandler = Nothing) As ICameraController
            Dim client As New IsapiClient(connection, handler)
            If profile Is CameraProfiles.Ds2Se7c432mwAeb Then Return New Ds2Se7c432mwAebController(client)
            If profile Is CameraProfiles.Ds2De7232iwAe Then Return New Ds2De7232iwAeController(client)
            client.Dispose()
            Throw New NotSupportedException($"الطراز {profile.ModelName} غير مدعوم.")
        End Function
    End Class

End Namespace
