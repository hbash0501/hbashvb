Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Xml.Linq
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models

Namespace Cloud

    ''' <summary>
    ''' قائمة بيضاء لمسارات ISAPI التي يجوز تمريرها عبر Hik-Partner Pro (الأقل صلاحية): ما يستعمله هذا النظام فعلاً من المسارات
    ''' الموثَّقة في ملحق «Request URIs» بدليل V2.15.500 لجهاز PTZ. كل ما عداها يُرفض محلياً بـ 404 قبل أي نداء سحابي. الصيغة:
    ''' "METHOD /ISAPI/path/&lt;ID&gt;/more" حيث &lt;ID&gt; = رقم من 1 إلى 4 خانات. الدليل نفسه يقول إن القائمة الكاملة للمسارات
    ''' المدعومة تؤخذ من الدعم الفني/tpp.hikvision.com؛ لذلك يمكن للمشغّل إضافة مسارات عبر <see cref="HppOptions.ExtraAllowed"/>.
    ''' </summary>
    Public NotInheritable Class HppPathPolicy
        ''' <summary>الحد الأدنى الموثَّق: معلومات الجهاز، حركة مستمرة، مواضع مسبقة (قراءة/حفظ/حذف/استدعاء). لا «حذف كل المواضع».</summary>
        Public Shared ReadOnly Documented As String() = {
            "GET /ISAPI/System/deviceInfo",
            "PUT /ISAPI/PTZCtrl/channels/<ID>/continuous",
            "GET /ISAPI/PTZCtrl/channels/<ID>/presets",
            "PUT /ISAPI/PTZCtrl/channels/<ID>/presets/<ID>",
            "DELETE /ISAPI/PTZCtrl/channels/<ID>/presets/<ID>",
            "PUT /ISAPI/PTZCtrl/channels/<ID>/presets/<ID>/goto"}

        Private NotInheritable Class Rule
            Public Method As String
            Public Segments As String()
            Public Query As String
        End Class

        Private Shared ReadOnly Methods As String() = {"GET", "PUT", "POST", "DELETE"}
        Private ReadOnly _rules As New List(Of Rule)()

        Public Sub New(extra As IEnumerable(Of String))
            For Each text As String In Documented
                _rules.Add(Parse(text))
            Next
            If extra IsNot Nothing Then
                For Each text As String In extra
                    Dim r As Rule = TryParse(text)
                    If r Is Nothing Then Throw New ArgumentException($"قاعدة مسار غير صالحة في Hpp.ExtraAllowed: '{HppJson.SafeCode(text)}'.")
                    _rules.Add(r)
                Next
            End If
        End Sub

        ''' <summary>للمدقّق: هل النص قاعدة صالحة؟</summary>
        Public Shared Function IsValidRule(text As String) As Boolean
            Return TryParse(text) IsNot Nothing
        End Function

        Private Shared Function Parse(text As String) As Rule
            Dim r As Rule = TryParse(text)
            If r Is Nothing Then Throw New ArgumentException("قاعدة مسار مضمَّنة غير صالحة.")
            Return r
        End Function

        Private Shared Function SegmentChars(seg As String) As Boolean
            If seg.Length = 0 Then Return False
            If seg = "<ID>" Then Return True
            For Each ch As Char In seg
                If Not (Char.IsAsciiLetterOrDigit(ch) OrElse ch = "_"c OrElse ch = "-"c OrElse ch = "."c) Then Return False
            Next
            Return seg <> "." AndAlso seg <> ".."
        End Function

        Private Shared Function TryParse(text As String) As Rule
            If String.IsNullOrWhiteSpace(text) OrElse text.Length > 200 Then Return Nothing
            Dim sp As Integer = text.IndexOf(" "c)
            If sp <= 0 Then Return Nothing
            Dim method As String = text.Substring(0, sp).ToUpperInvariant()
            If Not Methods.Contains(method) Then Return Nothing
            Dim target As String = text.Substring(sp + 1).Trim()
            Dim q As Integer = target.IndexOf("?"c)
            Dim path As String = If(q >= 0, target.Substring(0, q), target)
            Dim query As String = If(q >= 0, target.Substring(q + 1), "")
            If Not path.StartsWith("/ISAPI/", StringComparison.Ordinal) Then Return Nothing
            If query.Contains("<") OrElse query.Contains(">") OrElse query.Contains("%") OrElse query.Contains("#") Then Return Nothing
            For Each ch As Char In query
                If Char.IsControl(ch) OrElse ch = " "c Then Return Nothing
            Next
            Dim segs As String() = path.Substring(1).Split("/"c)
            For Each s As String In segs
                If Not SegmentChars(s) Then Return Nothing
            Next
            Return New Rule With {.Method = method, .Segments = segs, .Query = query}
        End Function

        Private Shared Function IsId(seg As String) As Boolean
            If seg.Length < 1 OrElse seg.Length > 4 Then Return False
            For Each ch As Char In seg
                If ch < "0"c OrElse ch > "9"c Then Return False
            Next
            Return True
        End Function

        ''' <summary>
        ''' هل (الطريقة، المسار، الاستعلام) مسموح؟ يُرفض أي مسار فيه ترميز نسبة مئوية أو تجاوز مجلد أو محرف تحكم، والمطابقة تامة
        ''' (الطريقة والمسار حساسان لحالة الأحرف). <paramref name="query"/> بدون أو مع علامة ?.
        ''' </summary>
        Public Function IsAllowed(method As String, path As String, query As String) As Boolean
            If String.IsNullOrEmpty(method) OrElse String.IsNullOrEmpty(path) Then Return False
            If Not path.StartsWith("/ISAPI/", StringComparison.Ordinal) OrElse path.Length > 200 Then Return False
            For Each ch As Char In path
                If Not (Char.IsAsciiLetterOrDigit(ch) OrElse ch = "/"c OrElse ch = "_"c OrElse ch = "-"c OrElse ch = "."c) Then Return False
            Next
            Dim segs As String() = path.Substring(1).Split("/"c)
            For Each s As String In segs
                If s.Length = 0 OrElse s = "." OrElse s = ".." Then Return False
            Next
            Dim qs As String = If(query, "")
            If qs.StartsWith("?", StringComparison.Ordinal) Then qs = qs.Substring(1)
            Dim m As String = method.ToUpperInvariant()
            For Each r As Rule In _rules
                If r.Method <> m OrElse r.Segments.Length <> segs.Length OrElse Not String.Equals(r.Query, qs, StringComparison.Ordinal) Then Continue For
                Dim ok As Boolean = True
                For i As Integer = 0 To segs.Length - 1
                    If r.Segments(i) = "<ID>" Then
                        If Not IsId(segs(i)) Then ok = False
                    ElseIf Not String.Equals(r.Segments(i), segs(i), StringComparison.Ordinal) Then
                        ok = False
                    End If
                    If Not ok Then Exit For
                Next
                If ok Then Return True
            Next
            Return False
        End Function
    End Class

    ''' <summary>ناتج تحويل رد السحابة إلى رد يفهمه عميل ISAPI.</summary>
    Public NotInheritable Class HppMapped
        Public Property Status As Integer
        Public Property Outcome As HppOutcome
        Public Property Code As String = ""
        Public Property TokenRejected As Boolean
        Public Property Body As Byte() = Array.Empty(Of Byte)()
        Public Property ContentType As String = "application/xml"
    End Class

    ''' <summary>
    ''' يحوّل ردود المرور الشفاف إلى ما يفهمه <see cref="IsapiClient"/>. الدليل لا يحدد شكل الخطأ على مستوى HTTP للمرور الشفاف
    ''' (يذكر فقط ترويسات X-ErrorCode/X-EZO-Code/X-DeviceCode ورموز LAP/EVZ)، فيُقرأ الرمز من الترويسة أو من جسم JSON، ويُتعامل مع
    ''' كل ما لا يُفهم بأمان: ردّ غير XML لطلب قراءة، أو جسم HTML/نص، يُعدّ فشلاً (502) لا نجاحاً.
    ''' </summary>
    Public NotInheritable Class HppResponseMapper
        Private Sub New()
        End Sub

        Private NotInheritable Class CodeInfo
            Public ReadOnly Status As Integer
            Public ReadOnly Outcome As HppOutcome
            Public ReadOnly Text As String

            Public Sub New(status As Integer, outcome As HppOutcome, text As String)
                Me.Status = status
                Me.Outcome = outcome
                Me.Text = text
            End Sub
        End Class

        ' الرموز من ملحق «Status or Error Code» في الدليل. 401 يغذّي قاطع الدائرة (لا نُغرق خدمة رفضت المفاتيح أو حساب جهاز).
        Private Shared ReadOnly Table As New Dictionary(Of String, CodeInfo)(StringComparer.OrdinalIgnoreCase) From {
            {"LAP500002", New CodeInfo(401, HppOutcome.AuthRejected, "open token is empty")},
            {"LAP500003", New CodeInfo(401, HppOutcome.AuthRejected, "invalid open token format")},
            {"LAP500004", New CodeInfo(401, HppOutcome.AuthRejected, "open token expired or incorrect")},
            {"LAP500005", New CodeInfo(401, HppOutcome.AuthRejected, "open token verification failed")},
            {"EVZ10013", New CodeInfo(401, HppOutcome.AuthRejected, "application has no permission to call this API")},
            {"EVZ10006", New CodeInfo(401, HppOutcome.AuthRejected, "IP address restricted")},
            {"EVZ60042", New CodeInfo(401, HppOutcome.AuthRejected, "incorrect device password")},
            {"EVZ60054", New CodeInfo(401, HppOutcome.AuthRejected, "device account locked")},
            {"LAP500007", New CodeInfo(503, HppOutcome.RateLimited, "number of calls exceeds limit")},
            {"EVZ10007", New CodeInfo(503, HppOutcome.RateLimited, "times of calling reached limit")},
            {"EVZ10029", New CodeInfo(503, HppOutcome.RateLimited, "API calling frequency exceeded limit")},
            {"EVZ20007", New CodeInfo(503, HppOutcome.DeviceUnreachable, "device is offline")},
            {"EVZ20008", New CodeInfo(503, HppOutcome.DeviceUnreachable, "device response timed out")},
            {"EVZ20006", New CodeInfo(503, HppOutcome.DeviceUnreachable, "network exception")},
            {"EVZ20609", New CodeInfo(503, HppOutcome.DeviceUnreachable, "device response timed out")},
            {"EVZ60044", New CodeInfo(503, HppOutcome.DeviceUnreachable, "accessing device network failed")},
            {"EVZ60041", New CodeInfo(503, HppOutcome.DeviceUnreachable, "device response timed out")},
            {"LAP006006", New CodeInfo(503, HppOutcome.DeviceUnreachable, "network exception")},
            {"LAP008008", New CodeInfo(503, HppOutcome.DeviceUnreachable, "network error")},
            {"LAP500001", New CodeInfo(503, HppOutcome.DeviceUnreachable, "service unavailable")},
            {"EVZ50000", New CodeInfo(503, HppOutcome.DeviceUnreachable, "server exception")},
            {"EVZ49999", New CodeInfo(503, HppOutcome.DeviceUnreachable, "data exception")},
            {"LAP000000", New CodeInfo(503, HppOutcome.DeviceUnreachable, "system error")},
            {"EVZ20015", New CodeInfo(404, HppOutcome.Rejected, "function not supported by the device")},
            {"EVZ60020", New CodeInfo(404, HppOutcome.Rejected, "command not supported")},
            {"EVZ60051", New CodeInfo(404, HppOutcome.Rejected, "device not supported")},
            {"EVZ20001", New CodeInfo(404, HppOutcome.Rejected, "channel does not exist")},
            {"EVZ60052", New CodeInfo(404, HppOutcome.Rejected, "incorrect channel number")},
            {"EVZ20002", New CodeInfo(403, HppOutcome.Rejected, "device does not exist")},
            {"EVZ20014", New CodeInfo(403, HppOutcome.Rejected, "incorrect device serial number")},
            {"EVZ20018", New CodeInfo(403, HppOutcome.Rejected, "device is not linked to this account")},
            {"LAP006009", New CodeInfo(403, HppOutcome.Rejected, "device does not exist or no permission")},
            {"LAP034001", New CodeInfo(403, HppOutcome.Rejected, "no permission to access the site")},
            {"LAP034002", New CodeInfo(403, HppOutcome.Rejected, "no permission to access the company")},
            {"EVZ10001", New CodeInfo(400, HppOutcome.Rejected, "incorrect parameter")},
            {"LAP000001", New CodeInfo(400, HppOutcome.Rejected, "parameter error")},
            {"EVZ10020", New CodeInfo(400, HppOutcome.Rejected, "request method is required")}}

        Public Shared Function IsTokenRejected(code As String) As Boolean
            Return code IsNot Nothing AndAlso (code.Equals("LAP500002", StringComparison.OrdinalIgnoreCase) OrElse code.Equals("LAP500003", StringComparison.OrdinalIgnoreCase) OrElse
                                              code.Equals("LAP500004", StringComparison.OrdinalIgnoreCase) OrElse code.Equals("LAP500005", StringComparison.OrdinalIgnoreCase))
        End Function

        Public Shared Function IsAuthCode(code As String) As Boolean
            Dim info As CodeInfo = Nothing
            Return code IsNot Nothing AndAlso Table.TryGetValue(code, info) AndAlso info.Outcome = HppOutcome.AuthRejected
        End Function

        ''' <summary>رمز الخطأ الحاسم من ترويسة X-ErrorCode (وغير الصفري)، وإلا من جسم JSON فيه errorCode؛ وإلا "".</summary>
        Private Shared Function DetectCode(xErrorCode As String, kind As BodyKind, text As String) As String
            Dim h As String = If(xErrorCode, "").Trim()
            If h <> "" AndAlso h <> "0" Then Return HppJson.SafeCode(h)
            If kind = BodyKind.Json Then
                Try
                    Using doc As System.Text.Json.JsonDocument = System.Text.Json.JsonDocument.Parse(text)
                        If doc.RootElement.ValueKind = System.Text.Json.JsonValueKind.Object Then
                            Dim c As String = HppJson.ErrorCodeOf(doc.RootElement, "0")
                            If c <> "0" Then Return HppJson.SafeCode(c)
                        End If
                    End Using
                Catch ex As System.Text.Json.JsonException
                    Return ""
                End Try
            End If
            Return ""
        End Function

        Public Enum BodyKind
            Empty
            Xml
            Json
            Other
        End Enum

        Public Shared Function Classify(body As Byte(), ByRef text As String) As BodyKind
            text = ""
            If body Is Nothing OrElse body.Length = 0 Then Return BodyKind.Empty
            text = Encoding.UTF8.GetString(body).TrimStart(ChrW(&HFEFF), " "c, ChrW(9), ChrW(10), ChrW(13))
            If text.Length = 0 Then Return BodyKind.Empty
            If text(0) = "<"c Then Return BodyKind.Xml
            If text(0) = "{"c OrElse text(0) = "["c Then Return BodyKind.Json
            Return BodyKind.Other
        End Function

        ''' <summary>XML سليم وليس صفحة HTML (وكيل/بوابة أخطاء): وإلا نعدّه ردّاً غير مفهوم بدل تسليمه لمحلل ISAPI.</summary>
        Private Shared Function IsUsableXml(text As String) As Boolean
            Try
                Dim doc As XDocument = IsapiXml.Parse(text)
                Return doc.Root IsNot Nothing AndAlso Not String.Equals(doc.Root.Name.LocalName, "html", StringComparison.OrdinalIgnoreCase)
            Catch ex As Exception When TypeOf ex Is Xml.XmlException OrElse TypeOf ex Is ArgumentException
                Return False
            End Try
        End Function

        Public Shared Function StatusXml(statusCode As Integer, statusString As String, subStatus As String) As Byte()
            Dim ns As XNamespace = "http://www.isapi.org/ver20/XMLSchema"
            Dim doc As New XDocument(New XDeclaration("1.0", "UTF-8", Nothing),
                New XElement(ns + "ResponseStatus", New XAttribute("version", "2.0"),
                    New XElement(ns + "statusCode", statusCode.ToString(Globalization.CultureInfo.InvariantCulture)),
                    New XElement(ns + "statusString", statusString),
                    New XElement(ns + "subStatusCode", subStatus)))
            Return Encoding.UTF8.GetBytes(doc.ToString())
        End Function

        Public Shared Function Synthesize(status As Integer, outcome As HppOutcome, code As String, text As String) As HppMapped
            Return New HppMapped With {.Status = status, .Outcome = outcome, .Code = code, .Body = StatusXml(If(status = 200, 1, 3), "Hik-Partner Pro: " & text, code)}
        End Function

        ''' <param name="mutating">PUT/POST/DELETE: جسم فارغ أو JSON {errorCode:0} يُعدّ نجاحاً؛ للقراءة يجب أن يكون الرد XML.</param>
        Public Shared Function Map(mutating As Boolean, httpStatus As Integer, xErrorCode As String, contentType As String, body As Byte()) As HppMapped
            Dim text As String = ""
            Dim kind As BodyKind = Classify(body, text)
            Dim code As String = DetectCode(xErrorCode, kind, text)
            If code <> "" Then
                Dim info As CodeInfo = Nothing
                If Table.TryGetValue(code, info) Then
                    Dim m As HppMapped = Synthesize(info.Status, info.Outcome, code, info.Text)
                    m.TokenRejected = IsTokenRejected(code)
                    Return m
                End If
                Return Synthesize(502, HppOutcome.Rejected, code, "request rejected")
            End If
            If httpStatus >= 200 AndAlso httpStatus < 300 Then
                Select Case kind
                    Case BodyKind.Xml
                        If Not IsUsableXml(text) Then Return Synthesize(502, HppOutcome.Rejected, "badResponse", "unexpected response")
                        Return New HppMapped With {.Status = httpStatus, .Outcome = HppOutcome.Ok, .Body = body, .ContentType = If(String.IsNullOrEmpty(contentType), "application/xml", contentType)}
                    Case BodyKind.Empty, BodyKind.Json
                        If mutating Then Return New HppMapped With {.Status = 200, .Outcome = HppOutcome.Ok, .Body = StatusXml(1, "OK", "")}
                        Return Synthesize(502, HppOutcome.Rejected, "badResponse", "unexpected response")
                    Case Else
                        Return Synthesize(502, HppOutcome.Rejected, "badResponse", "unexpected response")
                End Select
            End If
            ' حالة HTTP غير ناجحة بلا رمز معروف.
            If kind = BodyKind.Xml AndAlso IsUsableXml(text) Then
                Dim outcome As HppOutcome = If(httpStatus = 401, HppOutcome.AuthRejected, If(httpStatus >= 500, HppOutcome.DeviceUnreachable, HppOutcome.Rejected))
                Return New HppMapped With {.Status = httpStatus, .Outcome = outcome, .Code = "HTTP" & httpStatus.ToString(Globalization.CultureInfo.InvariantCulture), .Body = body, .ContentType = "application/xml"}
            End If
            Dim hs As String = "HTTP" & httpStatus.ToString(Globalization.CultureInfo.InvariantCulture)
            Select Case httpStatus
                Case 401 : Return Synthesize(401, HppOutcome.AuthRejected, hs, "unauthorized")
                Case 403 : Return Synthesize(403, HppOutcome.Rejected, hs, "forbidden")
                Case 404 : Return Synthesize(404, HppOutcome.Rejected, hs, "not found")
                Case 408, 504 : Return Synthesize(503, HppOutcome.DeviceUnreachable, hs, "timeout")
                Case 429 : Return Synthesize(503, HppOutcome.RateLimited, hs, "too many requests")
                Case Else
                    If httpStatus >= 500 Then Return Synthesize(503, HppOutcome.DeviceUnreachable, hs, "service error")
                    Return Synthesize(502, HppOutcome.Rejected, hs, "request rejected")
            End Select
        End Function
    End Class

    ''' <summary>
    ''' معالج HTTP يستبدل الاتصال المباشر بالكاميرا: يستقبل طلب ISAPI العادي من <see cref="IsapiClient"/> ويعيد توجيهه إلى
    ''' <c>POST/PUT/GET/DELETE https://{areaDomain}/api/hpcgw/v1/device/transparent/{isapi uri}</c> بترويستي Authorization: Bearer وX-Devserial.
    ''' الرقم التسلسلي ثابت لكل معالج (من الإعداد)؛ لا يصل أي حقل من العميل البعيد إلى السحابة إلا عبر أوامر PTZ المصدَّقة في البوابة.
    ''' </summary>
    Public NotInheritable Class HppRelayHandler
        Inherits HttpMessageHandler

        Private Const MaxBodyBytes As Integer = 64 * 1024
        Private Shared ReadOnly MaxQueueWait As TimeSpan = TimeSpan.FromSeconds(2)
        Private ReadOnly _cloud As HppCloud
        Private ReadOnly _serial As String

        Public Sub New(cloud As HppCloud, serial As String)
            If cloud Is Nothing Then Throw New ArgumentNullException(NameOf(cloud))
            If Not CameraTransports.IsValidCloudSerial(serial) Then Throw New ArgumentException("الرقم التسلسلي السحابي غير صالح.")
            _cloud = cloud
            _serial = serial
        End Sub

        Private Shared Function ToResponse(m As HppMapped, request As HttpRequestMessage) As HttpResponseMessage
            Dim resp As New HttpResponseMessage(CType(m.Status, HttpStatusCode)) With {.RequestMessage = request, .Content = New ByteArrayContent(m.Body)}
            resp.Content.Headers.TryAddWithoutValidation("Content-Type", m.ContentType)
            Return resp
        End Function

        ''' <summary>أمر إيقاف = PUT continuous وكل pan/tilt/zoom يساوي 0: يتجاوز المحدِّد كي لا يتأخر خلف طابور حركات.</summary>
        Public Shared Function IsStopCommand(method As String, path As String, body As Byte()) As Boolean
            If method <> "PUT" OrElse body Is Nothing OrElse Not path.EndsWith("/continuous", StringComparison.Ordinal) Then Return False
            Try
                Dim doc As XDocument = IsapiXml.Parse(Encoding.UTF8.GetString(body))
                Dim any As Boolean = False
                For Each name As String In {"pan", "tilt", "zoom"}
                    Dim v As String = IsapiXml.ValueOf(doc, name)
                    If v Is Nothing Then Return False
                    Dim n As Integer
                    If Not Integer.TryParse(v.Trim(), Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, n) Then Return False
                    If n <> 0 Then Return False
                    any = True
                Next
                Return any
            Catch ex As Exception When TypeOf ex Is Xml.XmlException OrElse TypeOf ex Is ArgumentException
                Return False
            End Try
        End Function

        Private Shared Function FirstHeader(resp As HttpResponseMessage, name As String) As String
            Dim vals As IEnumerable(Of String) = Nothing
            If resp.Headers.TryGetValues(name, vals) Then Return vals.FirstOrDefault()
            If resp.Content IsNot Nothing AndAlso resp.Content.Headers.TryGetValues(name, vals) Then Return vals.FirstOrDefault()
            Return Nothing
        End Function

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
            Dim uri As Uri = request.RequestUri
            Dim method As String = request.Method.Method.ToUpperInvariant()
            Dim path As String = If(uri Is Nothing, "", uri.AbsolutePath)
            Dim query As String = If(uri Is Nothing, "", uri.Query)
            If Not _cloud.Policy.IsAllowed(method, path, query) Then
                Return ToResponse(HppResponseMapper.Synthesize(404, HppOutcome.Rejected, "notAllowed", "path not allowed by gateway policy"), request)
            End If
            Dim mutating As Boolean = method <> "GET"
            Dim body As Byte() = Nothing
            If request.Content IsNot Nothing Then
                body = Await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(False)
                If body.Length > MaxBodyBytes Then Return ToResponse(HppResponseMapper.Synthesize(413, HppOutcome.Rejected, "bodyTooLarge", "request too large"), request)
                If body.Length = 0 Then
                    body = Nothing
                Else
                    ' يطابق أمثلة الدليل (مساحة أسماء isapi.org) بدل مساحة hikvision.com القديمة التي يبنيها العميل.
                    body = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("http://www.hikvision.com/ver20/XMLSchema", "http://www.isapi.org/ver20/XMLSchema"))
                End If
            End If

            If Not IsStopCommand(method, path, body) Then
                If Not Await _cloud.Limiter.WaitAsync(MaxQueueWait, cancellationToken).ConfigureAwait(False) Then
                    _cloud.RaiseCallCompleted(New HppCallEventArgs(_serial, HppOutcome.RateLimited, "localRate", 0))
                    Return ToResponse(HppResponseMapper.Synthesize(503, HppOutcome.RateLimited, "localRate", "gateway rate limit"), request)
                End If
            End If

            Dim rejectedToken As String = Nothing
            For attempt As Integer = 0 To 1
                Dim auth As HppAuth
                Try
                    auth = Await _cloud.GetAuthAsync(rejectedToken, cancellationToken).ConfigureAwait(False)
                Catch ex As HppCloudException
                    Dim outcome As HppOutcome = If(ex.IsAuthFailure, HppOutcome.AuthRejected, HppOutcome.Rejected)
                    _cloud.RaiseCallCompleted(New HppCallEventArgs(_serial, outcome, ex.Code, 0))
                    Return ToResponse(HppResponseMapper.Synthesize(If(ex.IsAuthFailure, 401, 502), outcome, ex.Code, "cloud credentials rejected"), request)
                Catch ex As HttpRequestException
                    _cloud.RaiseCallCompleted(New HppCallEventArgs(_serial, HppOutcome.DeviceUnreachable, "cloudUnreachable", 0))
                    Throw
                End Try

                Dim status As Integer
                Dim xErr As String
                Dim contentType As String
                Dim bytes As Byte()
                Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, _cloud.Options.TimeoutSeconds)))
                    Using creq As New HttpRequestMessage(request.Method, auth.Area & "/api/hpcgw/v1/device/transparent" & path & query)
                        creq.Headers.TryAddWithoutValidation("Authorization", "Bearer " & auth.Token)
                        creq.Headers.TryAddWithoutValidation("X-Devserial", _serial)
                        If body IsNot Nothing Then
                            creq.Content = New ByteArrayContent(body)
                            creq.Content.Headers.TryAddWithoutValidation("Content-Type", "application/xml")
                        End If
                        Try
                            Using resp As HttpResponseMessage = Await _cloud.SendRawAsync(creq, cts.Token).ConfigureAwait(False)
                                status = CInt(resp.StatusCode)
                                xErr = FirstHeader(resp, "X-ErrorCode")
                                contentType = resp.Content.Headers.ContentType?.MediaType
                                bytes = Await resp.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(False)
                            End Using
                        Catch ex As OperationCanceledException When Not cancellationToken.IsCancellationRequested
                            _cloud.RaiseCallCompleted(New HppCallEventArgs(_serial, HppOutcome.DeviceUnreachable, "cloudTimeout", 0))
                            Throw New HttpRequestException("انتهت مهلة نداء Hik-Partner Pro.")
                        Catch ex As HttpRequestException
                            _cloud.RaiseCallCompleted(New HppCallEventArgs(_serial, HppOutcome.DeviceUnreachable, "cloudUnreachable", 0))
                            Throw
                        End Try
                    End Using
                End Using

                Dim mapped As HppMapped = HppResponseMapper.Map(mutating, status, xErr, contentType, bytes)
                If mapped.TokenRejected AndAlso attempt = 0 Then
                    rejectedToken = auth.Token
                    Continue For
                End If
                If mapped.Outcome = HppOutcome.RateLimited Then _cloud.Limiter.Penalize(3)
                _cloud.RaiseCallCompleted(New HppCallEventArgs(_serial, mapped.Outcome, mapped.Code, status))
                Return ToResponse(mapped, request)
            Next
            Return ToResponse(HppResponseMapper.Synthesize(401, HppOutcome.AuthRejected, "LAP500004", "open token rejected"), request)
        End Function
    End Class

End Namespace
