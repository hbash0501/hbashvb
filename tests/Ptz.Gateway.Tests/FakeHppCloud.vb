Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports Ptz.Core.Abstractions

''' <summary>
''' خادم Hik-Partner Pro وهمي مبني على دليل OpenAPI V2.15.500: POST /api/hpcgw/v1/token/get من النطاق العام، ثم نداءات
''' /api/hpcgw/v1/device/transparent/{isapi uri} على areaDomain بترويستي Authorization: Bearer وX-Devserial، وPOST /device/list.
''' شكل أخطاء المرور الشفاف على مستوى HTTP غير محدد في الدليل: نحاكي صيغتين (JSON {errorCode} وترويسة X-ErrorCode) ويمكن اختيار أيهما.
''' </summary>
Public NotInheritable Class FakeHppCloud
    Inherits HttpMessageHandler

    Public Const AppKey As String = "ak-test-1234"
    Public Const SecretKey As String = "sk-test-secret-5678"
    Public Const TokenHost As String = "api.hik-partner.com"

    Public NotInheritable Class Device
        Public Property Serial As String = ""
        Public Property Model As String = "DS-2SE7C432MW-AEB"
        Public Property Online As Boolean = True
        Public ReadOnly Property Moves As New List(Of String)()
        Public ReadOnly Property Presets As New SortedDictionary(Of Integer, String)()
        Public Property StopFailuresLeft As Integer
    End Class

    Public NotInheritable Class Recorded
        Public Property Method As String = ""
        Public Property Host As String = ""
        Public Property Path As String = ""
        Public Property Query As String = ""
        Public Property Authorization As String = ""
        Public Property DevSerial As String = ""
        Public Property ContentType As String = ""
        Public Property Body As String = ""
    End Class

    Private ReadOnly _clock As IClock
    Private ReadOnly _lock As New Object()
    Private _tokenSeq As Integer
    Private _validToken As String = ""

    Public Property AreaDomain As String = "https://isgpapi.hik-partner.com"
    Public Property TokenLifetime As TimeSpan = TimeSpan.FromDays(7)
    ''' <summary>"json" (جسم JSON فيه errorCode) أو "header" (ترويسة X-ErrorCode) أو "http401" (HTTP 401 للرمز المرفوض).</summary>
    Public Property ErrorStyle As String = "json"
    ''' <summary>رمز يُعاد لكل نداءات المرور الشفاف التالية (حتى يُمسح) — لمحاكاة EVZ10013/EVZ10029...</summary>
    Public Property ForceCode As String
    Public Property TokenRequestsFail As Boolean
    ''' <summary>"wrapped" (§2.2: data + errorCode) أو "flat" (§3.1.1: الحقول في الجذر بأسماء فيها مسافات زائدة كما في مثال الدليل).</summary>
    Public Property TokenShape As String = "wrapped"
    ''' <summary>رد خدمة الرمز البديل (لمحاكاة الأعطال): Nothing = السلوك العادي.</summary>
    Public Property TokenOverride As Func(Of HttpResponseMessage)
    ''' <summary>رد المرور الشفاف البديل لكل النداءات (لمحاكاة أشكال ردود غير موثَّقة).</summary>
    Public Property TransparentOverride As Func(Of Recorded, HttpResponseMessage)
    ''' <summary>تأخير صناعي لنداءات المرور الشفاف وقائمة الأجهزة (ميلي ثانية) — لاختبار المهل.</summary>
    Public Property DelayMs As Integer
    Public Property TokenDelayMs As Integer
    ''' <summary>أخطاء صارمة على شكل الطلب كما يصفه الدليل (Content-Type إلزامي، XML بتصريح ومساحة isapi.org، enabled في المواضع).</summary>
    Public ReadOnly Property Violations As New List(Of String)()
    Public Property TransparentCalls As Integer
    Public Property TokenCalls As Integer
    Public ReadOnly Property Requests As New List(Of Recorded)()
    Public ReadOnly Property Devices As New Dictionary(Of String, Device)(StringComparer.Ordinal)

    Public Sub New(clock As IClock)
        _clock = clock
    End Sub

    Public Function AddDevice(serial As String, Optional model As String = "DS-2SE7C432MW-AEB") As Device
        Dim d As New Device With {.Serial = serial, .Model = model}
        SyncLock _lock
            Devices(serial) = d
        End SyncLock
        Return d
    End Function

    ''' <summary>يبطل الرمز الحالي من جهة الخادم (كأنه انتهى) فيرد LAP500004 على أول نداء به.</summary>
    Public Sub ExpireCurrentToken()
        SyncLock _lock
            _validToken = ""
        End SyncLock
    End Sub

    Public Function MoveLog(serial As String) As String()
        SyncLock _lock
            Return Devices(serial).Moves.ToArray()
        End SyncLock
    End Function

    Public Function Transparent(Optional serial As String = Nothing) As List(Of Recorded)
        SyncLock _lock
            Return Requests.Where(Function(r) r.Path.StartsWith("/api/hpcgw/v1/device/transparent", StringComparison.Ordinal) AndAlso (serial Is Nothing OrElse r.DevSerial = serial)).ToList()
        End SyncLock
    End Function

    Private Shared Function Json(body As String) As HttpResponseMessage
        Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
    End Function

    Private Function ErrorReply(code As String) As HttpResponseMessage
        Select Case ErrorStyle
            Case "header"
                Dim r As New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("", Encoding.UTF8, "application/json")}
                r.Headers.TryAddWithoutValidation("X-ErrorCode", code)
                Return r
            Case "http401"
                If code.StartsWith("LAP5000", StringComparison.Ordinal) Then Return New HttpResponseMessage(HttpStatusCode.Unauthorized) With {.Content = New StringContent("{""errorCode"":""" & code & """}", Encoding.UTF8, "application/json")}
                Return Json("{""errorCode"":""" & code & """}")
            Case Else
                Return Json("{""errorCode"":""" & code & """}")
        End Select
    End Function

    Private Shared Function Xml(body As String) As HttpResponseMessage
        Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(body, Encoding.UTF8, "application/xml")}
    End Function

    Private Const Ns As String = "http://www.isapi.org/ver20/XMLSchema"
    Private Const OkXml As String = "<ResponseStatus version=""2.0"" xmlns=""http://www.isapi.org/ver20/XMLSchema""><requestURL>x</requestURL><statusCode>1</statusCode><statusString>OK</statusString><subStatusCode>ok</subStatusCode></ResponseStatus>"

    Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
        Dim body As String = If(request.Content Is Nothing, "", Await request.Content.ReadAsStringAsync(cancellationToken))
        Dim rec As New Recorded With {
            .Method = request.Method.Method, .Host = request.RequestUri.Host, .Path = request.RequestUri.AbsolutePath, .Query = request.RequestUri.Query,
            .Authorization = If(request.Headers.Authorization?.ToString(), ""), .DevSerial = If(request.Headers.Contains("X-Devserial"), request.Headers.GetValues("X-Devserial").First(), ""),
            .ContentType = If(request.Content?.Headers.ContentType?.MediaType, ""), .Body = body}
        SyncLock _lock
            Requests.Add(rec)
        End SyncLock
        If rec.Path = "/api/hpcgw/v1/token/get" Then
            If TokenDelayMs > 0 Then Await Task.Delay(TokenDelayMs, cancellationToken)
        ElseIf rec.Path.StartsWith("/api/hpcgw/v1/mq/", StringComparison.Ordinal) Then
            Return Await HandleMqAsync(rec, cancellationToken)
        ElseIf DelayMs > 0 Then
            Await Task.Delay(DelayMs, cancellationToken)
        End If

        If rec.Path = "/api/hpcgw/v1/token/get" Then Return HandleToken(rec)
        If rec.Path = "/api/hpcgw/v1/device/list" Then Return HandleList(rec)
        If rec.Path.StartsWith("/api/hpcgw/v1/device/transparent/", StringComparison.Ordinal) Then Return HandleTransparent(rec)
        Return New HttpResponseMessage(HttpStatusCode.NotFound)
    End Function

    Private Function HandleToken(rec As Recorded) As HttpResponseMessage
        SyncLock _lock
            TokenCalls += 1
        End SyncLock
        If rec.Host <> TokenHost OrElse rec.Method <> "POST" Then Return New HttpResponseMessage(HttpStatusCode.NotFound)
        If TokenRequestsFail Then Throw New HttpRequestException("simulated cloud outage")
        If TokenOverride IsNot Nothing Then Return TokenOverride.Invoke()
        Dim ok As Boolean = False
        Try
            Using doc As JsonDocument = JsonDocument.Parse(rec.Body)
                ok = doc.RootElement.GetProperty("appKey").GetString() = AppKey AndAlso doc.RootElement.GetProperty("secretKey").GetString() = SecretKey
            End Using
        Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is KeyNotFoundException
            ok = False
        End Try
        If Not ok Then Return Json("{""errorCode"":""LAP300002""}")     ' الدليل: AK and SK mismatches
        Dim token As String
        SyncLock _lock
            _tokenSeq += 1
            token = "hpc.tok" & _tokenSeq.ToString(Globalization.CultureInfo.InvariantCulture)
            _validToken = token
        End SyncLock
        Dim exp As Long = New DateTimeOffset(_clock.UtcNow + TokenLifetime).ToUnixTimeMilliseconds()
        If TokenShape = "flat" Then Return Json($"{{""accessToken "":""{token}"",""expireTime "":{exp},""areaDomain"":""{AreaDomain}""}}")
        Return Json($"{{""data"":{{""accessToken"":""{token}"",""expireTime"":{exp},""areaDomain"":""{AreaDomain}""}},""errorCode"":""0""}}")
    End Function

    Private Function CheckBearer(rec As Recorded) As Boolean
        SyncLock _lock
            Return rec.Authorization = "Bearer " & _validToken AndAlso _validToken <> ""
        End SyncLock
    End Function

    ' ===================== إنذارات (§3.6) =====================
    Public NotInheritable Class Batch
        Public Property Id As String = ""
        Public Property Items As String() = Array.Empty(Of String)()
    End Class

    Private ReadOnly _batches As New List(Of Batch)()
    Private ReadOnly _batchSignal As New Threading.SemaphoreSlim(0)
    Private _batchSeq As Integer
    Public ReadOnly Property Subscriptions As New List(Of String)()
    Public ReadOnly Property ConfirmedBatches As New List(Of String)()
    Public Property MessagesCalls As Integer
    ''' <summary>رمز خطأ يُعاد لكل نداء messages (مثل LAP068001) حتى يُمسح.</summary>
    Public Property MessagesCode As String
    Public Property SubscribeCode As String
    Public Property OffsetCode As String
    ''' <summary>مدة حجب السحب الطويل حين لا توجد رسائل (الدليل: حتى 20 ث؛ أقصر في الاختبار).</summary>
    Public Property PollHoldMs As Integer = 300

    ''' <summary>يضيف دفعة رسائل؛ كل عنصر JSON خام لعنصر في data.list.</summary>
    Public Function EnqueueBatch(ParamArray items As String()) As String
        Dim id As String
        SyncLock _lock
            _batchSeq += 1
            id = "batch-" & _batchSeq.ToString(Globalization.CultureInfo.InvariantCulture)
            _batches.Add(New Batch With {.Id = id, .Items = items})
        End SyncLock
        _batchSignal.Release()
        Return id
    End Function

    Public Function PendingBatches() As Integer
        SyncLock _lock
            Return _batches.Count
        End SyncLock
    End Function

    ''' <summary>عنصر رسالة بصيغة XML (alarmData نص XML كما في جدول الدليل).</summary>
    Public Shared Function XmlItem(serial As String, eventType As String, Optional inner As String = "") As String
        Dim xml As String = $"<?xml version=""1.0"" encoding=""utf-8""?><EventNotificationAlert version=""2.0"" xmlns=""{Ns}""><deviceSerial>{serial}</deviceSerial><channelID>1</channelID><triggerTime>2026-01-01T12:00:00</triggerTime><eventType>{eventType}</eventType><eventDescription>x</eventDescription>{inner}</EventNotificationAlert>"
        Return "{""formatType"":""XML"",""accountNumber"":""1"",""deviceSerial"":""" & serial & """,""alarmData"":" & JsonSerializer.Serialize(xml) & "}"
    End Function

    ''' <summary>عنصر رسالة بصيغة JSON (alarmData كائن كما في مثال الدليل).</summary>
    Public Shared Function JsonItem(serial As String, eventType As String) As String
        Return "{""formatType"":""JSON"",""accountNumber"":""1"",""deviceSerial"":""" & serial & """,""alarmData"":{""deviceSerial"":""" & serial & """,""dateTime"":""2026-01-01T12:00:00Z"",""eventType"":""" & eventType & """,""eventDescription"":""""}}"
    End Function

    Private Async Function HandleMqAsync(rec As Recorded, ct As Threading.CancellationToken) As Task(Of HttpResponseMessage)
        If Not CheckBearer(rec) Then Return ErrorReply("LAP500004")
        If rec.ContentType <> "application/json" Then
            SyncLock _lock
                Violations.Add("mq content-type: " & rec.Path)
            End SyncLock
        End If
        Select Case rec.Path
            Case "/api/hpcgw/v1/mq/subscribe"
                If SubscribeCode IsNot Nothing Then Return Json("{""errorCode"":""" & SubscribeCode & """}")
                SyncLock _lock
                    Subscriptions.Add(rec.Body)
                End SyncLock
                Return Json("{""errorCode"":""0""}")
            Case "/api/hpcgw/v1/mq/messages"
                SyncLock _lock
                    MessagesCalls += 1
                End SyncLock
                If MessagesCode IsNot Nothing Then Return Json("{""errorCode"":""" & MessagesCode & """}")
                Dim b As Batch = Nothing
                SyncLock _lock
                    b = _batches.FirstOrDefault()
                End SyncLock
                If b Is Nothing Then
                    ' سحب طويل: ننتظر دفعة جديدة أو انقضاء مدة الحجب.
                    If Await _batchSignal.WaitAsync(PollHoldMs, ct) Then
                        SyncLock _lock
                            b = _batches.FirstOrDefault()
                        End SyncLock
                    End If
                End If
                If b Is Nothing Then Return Json("{""data"":{""batchId"":"""",""list"":[]},""errorCode"":""0""}")
                Return Json("{""data"":{""batchId"":""" & b.Id & """,""list"":[" & String.Join(",", b.Items) & "]},""errorCode"":""0""}")
            Case "/api/hpcgw/v1/mq/offset"
                If OffsetCode IsNot Nothing Then Return Json("{""errorCode"":""" & OffsetCode & """}")
                Dim id As String = ""
                Try
                    Using doc As JsonDocument = JsonDocument.Parse(rec.Body)
                        id = doc.RootElement.GetProperty("batchId").GetString()
                    End Using
                Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is KeyNotFoundException
                    Return Json("{""errorCode"":""LAP000001""}")
                End Try
                SyncLock _lock
                    ConfirmedBatches.Add(id)
                    _batches.RemoveAll(Function(x) x.Id = id)        ' غير المؤكَّد يبقى ويُعاد في السحب التالي
                End SyncLock
                Return Json("{""errorCode"":""0""}")
        End Select
        Return New HttpResponseMessage(HttpStatusCode.NotFound)
    End Function

    Private Function HandleList(rec As Recorded) As HttpResponseMessage
        If Not CheckBearer(rec) Then Return ErrorReply("LAP500004")
        Dim rows As New StringBuilder()
        SyncLock _lock
            For Each d As Device In Devices.Values
                If rows.Length > 0 Then rows.Append(","c)
                rows.Append($"{{""deviceSerial"":""{d.Serial}"",""deviceName"":""cam {d.Serial}"",""deviceType"":""{d.Model}"",""deviceCategory"":7,""deviceOnlineStatus"":{If(d.Online, 1, 0)},""siteName"":""home""}}")
            Next
            Return Json($"{{""data"":{{""page"":1,""pageSize"":20,""rows"":[{rows}],""total"":{Devices.Count},""totalPage"":1}},""errorCode"":""0""}}")
        End SyncLock
    End Function

    Private Function HandleTransparent(rec As Recorded) As HttpResponseMessage
        SyncLock _lock
            TransparentCalls += 1
        End SyncLock
        If rec.Host <> New Uri(AreaDomain).Host Then Return New HttpResponseMessage(HttpStatusCode.NotFound)
        If Not CheckBearer(rec) Then Return ErrorReply("LAP500004")
        ' الدليل (جدول 3-101): Content-Type إلزامي لكل نداء شفاف.
        If rec.ContentType <> "application/xml" AndAlso rec.ContentType <> "application/json" Then
            SyncLock _lock
                Violations.Add("content-type missing: " & rec.Method & " " & rec.Path)
            End SyncLock
            Return ErrorReply("EVZ10001")
        End If
        If rec.Body <> "" AndAlso (Not rec.Body.StartsWith("<?xml", StringComparison.Ordinal) OrElse Not rec.Body.Contains(Ns) OrElse rec.Body.Contains("hikvision.com")) Then
            SyncLock _lock
                Violations.Add("xml shape: " & rec.Path)
            End SyncLock
            Return ErrorReply("EVZ10001")
        End If
        If TransparentOverride IsNot Nothing Then Return TransparentOverride.Invoke(rec)
        If ForceCode IsNot Nothing Then Return ErrorReply(ForceCode)
        Dim d As Device = Nothing
        SyncLock _lock
            Devices.TryGetValue(rec.DevSerial, d)
        End SyncLock
        If d Is Nothing Then Return ErrorReply("EVZ20002")
        If Not d.Online Then Return ErrorReply("EVZ20007")
        Dim isapi As String = rec.Path.Substring("/api/hpcgw/v1/device/transparent".Length)
        Select Case rec.Method & " " & isapi
            Case "GET /ISAPI/System/deviceInfo"
                Return Xml($"<DeviceInfo version=""2.0"" xmlns=""{Ns}""><deviceName>Cloud Cam</deviceName><model>{d.Model}</model><serialNumber>{d.Serial}</serialNumber><firmwareVersion>V5.7.0</firmwareVersion></DeviceInfo>")
            Case "PUT /ISAPI/PTZCtrl/channels/1/continuous"
                Dim isStop As Boolean = rec.Body.Contains("<pan>0</pan>") AndAlso rec.Body.Contains("<tilt>0</tilt>") AndAlso rec.Body.Contains("<zoom>0</zoom>")
                SyncLock _lock
                    If isStop AndAlso d.StopFailuresLeft > 0 Then
                        d.StopFailuresLeft -= 1
                        Return ErrorReply("EVZ20008")
                    End If
                    d.Moves.Add(If(isStop, "stop", ExtractVector(rec.Body)))
                End SyncLock
                Return Xml(OkXml)
            Case "GET /ISAPI/PTZCtrl/channels/1/presets"
                Dim sb As New StringBuilder()
                SyncLock _lock
                    For Each kv In d.Presets
                        sb.Append($"<PTZPreset><enabled>true</enabled><id>{kv.Key}</id><presetName>{kv.Value}</presetName></PTZPreset>")
                    Next
                End SyncLock
                Return Xml($"<PTZPresetList version=""2.0"" xmlns=""{Ns}"">{sb}</PTZPresetList>")
            Case Else
                Dim m As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(isapi, "^/ISAPI/PTZCtrl/channels/1/presets/(\d+)(/goto)?$")
                If m.Success Then
                    Dim id As Integer = Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                    SyncLock _lock
                        If m.Groups(2).Success AndAlso rec.Method = "PUT" Then
                            d.Moves.Add("goto:" & id)
                            Return Xml(OkXml)
                        End If
                        If rec.Method = "PUT" Then
                            ' XML_Set_PTZPreset: enabled وid وpresetName كلها إلزامية، وid يطابق المسار.
                            Dim idm As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(rec.Body, "<id>(\d+)</id>")
                            If Not rec.Body.Contains("<enabled>true</enabled>") OrElse Not idm.Success OrElse idm.Groups(1).Value <> id.ToString(Globalization.CultureInfo.InvariantCulture) Then
                                Violations.Add("preset body: " & rec.Body)
                                Return ErrorReply("EVZ10001")
                            End If
                            Dim nm As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(rec.Body, "<presetName>(.*?)</presetName>")
                            d.Presets(id) = If(nm.Success, nm.Groups(1).Value, "")
                            d.Moves.Add("save:" & id & ":" & If(rec.Body.Contains("<enabled>true</enabled>"), "enabled", "noflag"))
                            Return Xml(OkXml)
                        End If
                        If rec.Method = "DELETE" Then
                            d.Presets.Remove(id)
                            d.Moves.Add("delete:" & id)
                            Return Xml(OkXml)
                        End If
                    End SyncLock
                End If
                Return ErrorReply("EVZ60020")
        End Select
    End Function

    Private Shared Function ExtractVector(xml As String) As String
        Dim pan As String = System.Text.RegularExpressions.Regex.Match(xml, "<pan>(-?\d+)</pan>").Groups(1).Value
        Dim tilt As String = System.Text.RegularExpressions.Regex.Match(xml, "<tilt>(-?\d+)</tilt>").Groups(1).Value
        Dim zoom As String = System.Text.RegularExpressions.Regex.Match(xml, "<zoom>(-?\d+)</zoom>").Groups(1).Value
        Return $"move:{pan},{tilt},{zoom}"
    End Function
End Class
