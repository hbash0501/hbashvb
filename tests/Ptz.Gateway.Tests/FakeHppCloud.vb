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

    Public Property AreaDomain As String = "https://apiisgp.hik-partner.com"
    Public Property TokenLifetime As TimeSpan = TimeSpan.FromDays(7)
    ''' <summary>"json" (جسم JSON فيه errorCode) أو "header" (ترويسة X-ErrorCode) أو "http401" (HTTP 401 للرمز المرفوض).</summary>
    Public Property ErrorStyle As String = "json"
    ''' <summary>رمز يُعاد لكل نداءات المرور الشفاف التالية (حتى يُمسح) — لمحاكاة EVZ10013/EVZ10029...</summary>
    Public Property ForceCode As String
    Public Property TokenRequestsFail As Boolean
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
        Dim ok As Boolean = False
        Try
            Using doc As JsonDocument = JsonDocument.Parse(rec.Body)
                ok = doc.RootElement.GetProperty("appKey").GetString() = AppKey AndAlso doc.RootElement.GetProperty("secretKey").GetString() = SecretKey
            End Using
        Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is KeyNotFoundException
            ok = False
        End Try
        If Not ok Then Return Json("{""errorCode"":""LAP500005""}")
        Dim token As String
        SyncLock _lock
            _tokenSeq += 1
            token = "hpc.tok" & _tokenSeq.ToString(Globalization.CultureInfo.InvariantCulture)
            _validToken = token
        End SyncLock
        Dim exp As Long = New DateTimeOffset(_clock.UtcNow + TokenLifetime).ToUnixTimeMilliseconds()
        Return Json($"{{""data"":{{""accessToken"":""{token}"",""expireTime"":{exp},""areaDomain"":""{AreaDomain}""}},""errorCode"":""0""}}")
    End Function

    Private Function CheckBearer(rec As Recorded) As Boolean
        SyncLock _lock
            Return rec.Authorization = "Bearer " & _validToken AndAlso _validToken <> ""
        End SyncLock
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
