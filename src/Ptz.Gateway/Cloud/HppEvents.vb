Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Xml.Linq
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models

Namespace Cloud

    ''' <summary>حدث جهاز وارد من Hik-Partner Pro بعد التنقية: لا نص حر من السحابة يصل إلى الإنذارات (النوع حروف وأرقام فقط).</summary>
    Public NotInheritable Class HppDeviceEvent
        Public Property Serial As String = ""
        ''' <summary>نوع الحدث كما في ملحق Event Details (VMD، fielddetection، deviceoffline...) بعد التنقية: [A-Za-z0-9_] حتى 32.</summary>
        Public Property EventType As String = ""
        Public Property ChannelId As Integer
        ''' <summary>human | vehicle | others | "" (من DetectionRegionEntry/detectionTarget إن وُجد).</summary>
        Public Property Target As String = ""
        ''' <summary>مركز إطار الهدف المطبَّع [0,1] إن أرسلته الكاميرا (TargetRect)، وإلا Nothing.</summary>
        Public Property Position As NormPoint?
        ''' <summary>JSON أو XML كما ورد في formatType (للتشخيص فقط).</summary>
        Public Property Format As String = ""
    End Class

    ''' <summary>مستقبل أحداث كاميرا واحدة (CameraHost بنقل hpp).</summary>
    Public Interface IHppEventTarget
        ReadOnly Property CloudSerial As String
        Sub OnCloudEvent(e As HppDeviceEvent)
    End Interface

    ''' <summary>
    ''' يحلّل عنصراً واحداً من data.list في POST /api/hpcgw/v1/mq/messages (دليل §3.6.2 والملحق A.4). alarmData نص (XML أو JSON)
    ''' حسب الجدول، لكن مثال الدليل نفسه يعرضه كائن JSON؛ يُقبل الشكلان. كل ما لا يُفهم يُتجاهل (Nothing) ولا يرمي.
    ''' </summary>
    Public NotInheritable Class HppEventParser
        Private Sub New()
        End Sub

        Public Const MaxAlarmDataChars As Integer = 64 * 1024

        Public Shared Function SanitizeType(t As String) As String
            If String.IsNullOrEmpty(t) Then Return ""
            Dim sb As New StringBuilder()
            For Each ch As Char In t.Trim()
                If Char.IsAsciiLetterOrDigit(ch) OrElse ch = "_"c Then sb.Append(ch)
                If sb.Length >= 32 Then Exit For
            Next
            Return sb.ToString()
        End Function

        Private Shared Function Unit(s As String, ByRef v As Double) As Boolean
            If s Is Nothing Then Return False
            Return Double.TryParse(s.Trim(), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, v) AndAlso v >= 0 AndAlso v <= 1
        End Function

        ''' <summary>نص alarmData كما ورد (للتشخيص في CLI فقط)، أو "".</summary>
        Public Shared Function RawAlarmData(item As JsonElement) As String
            Dim raw As JsonElement
            If item.ValueKind <> JsonValueKind.Object OrElse Not HppJson.TryGetLoose(item, "alarmData", raw) Then Return ""
            If raw.ValueKind = JsonValueKind.String Then Return If(raw.GetString(), "")
            Return raw.GetRawText()
        End Function

        Public Shared Function Parse(item As JsonElement) As HppDeviceEvent
            If item.ValueKind <> JsonValueKind.Object Then Return Nothing
            Dim serial As String = HppJson.StringOf(item, "deviceSerial")
            Dim format As String = HppJson.StringOf(item, "formatType")
            Dim raw As JsonElement
            If Not HppJson.TryGetLoose(item, "alarmData", raw) Then Return Nothing
            Dim text As String
            Select Case raw.ValueKind
                Case JsonValueKind.String : text = If(raw.GetString(), "")
                Case JsonValueKind.Object : text = raw.GetRawText()
                Case Else : Return Nothing
            End Select
            If text.Length = 0 OrElse text.Length > MaxAlarmDataChars Then Return Nothing
            Dim trimmed As String = text.TrimStart(ChrW(&HFEFF), " "c, ChrW(9), ChrW(10), ChrW(13))
            Dim ev As HppDeviceEvent = If(trimmed.StartsWith("<", StringComparison.Ordinal), FromXml(trimmed), FromJson(trimmed))
            If ev Is Nothing Then Return Nothing
            If serial <> "" Then ev.Serial = serial
            ev.Format = SanitizeType(format)
            If ev.EventType = "" OrElse Not CameraTransports.IsValidCloudSerial(ev.Serial) Then Return Nothing
            Return ev
        End Function

        Private Shared Function FromXml(xml As String) As HppDeviceEvent
            Dim doc As XDocument
            Try
                doc = IsapiXml.Parse(xml)      ' DTD ممنوع، بلا محلّل كيانات
            Catch ex As Exception When TypeOf ex Is Xml.XmlException OrElse TypeOf ex Is ArgumentException
                Return Nothing
            End Try
            If doc.Root Is Nothing OrElse doc.Root.Name.LocalName <> "EventNotificationAlert" Then Return Nothing
            Dim ev As New HppDeviceEvent With {
                .Serial = If(IsapiXml.ValueOf(doc, "deviceSerial"), "").Trim(),
                .EventType = SanitizeType(IsapiXml.ValueOf(doc, "eventType"))}
            Dim ch As Integer
            If Integer.TryParse(IsapiXml.ValueOf(doc, "channelID"), Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, ch) Then ev.ChannelId = ch
            Dim target As String = If(IsapiXml.ValueOf(doc, "detectionTarget"), "").Trim().ToLowerInvariant()
            If target = "human" OrElse target = "vehicle" OrElse target = "others" Then ev.Target = target
            Dim rect As XElement = IsapiXml.FindLocal(doc, "TargetRect")
            If rect IsNot Nothing Then
                Dim x, y, w, h As Double
                If Unit(IsapiXml.ValueOf(rect, "X"), x) AndAlso Unit(IsapiXml.ValueOf(rect, "Y"), y) AndAlso Unit(IsapiXml.ValueOf(rect, "width"), w) AndAlso Unit(IsapiXml.ValueOf(rect, "height"), h) Then
                    ev.Position = New NormPoint(Math.Min(1.0, x + w / 2), Math.Min(1.0, y + h / 2))
                End If
            End If
            Return ev
        End Function

        Private Shared Function FromJson(json As String) As HppDeviceEvent
            Try
                Using doc As JsonDocument = JsonDocument.Parse(json, New JsonDocumentOptions With {.MaxDepth = 16})
                    If doc.RootElement.ValueKind <> JsonValueKind.Object Then Return Nothing
                    Return New HppDeviceEvent With {
                        .Serial = HppJson.StringOf(doc.RootElement, "deviceSerial"),
                        .EventType = SanitizeType(HppJson.StringOf(doc.RootElement, "eventType"))}
                End Using
            Catch ex As JsonException
                Return Nothing
            End Try
        End Function
    End Class

    ''' <summary>
    ''' مضخة إنذارات Hik-Partner Pro (دليل §2.5 و§3.6): اشتراك بقائمة الأرقام التسلسلية المضبوطة فقط (subMode=list، لا "all")، ثم سحب طويل
    ''' (mq/messages يحجب حتى 20 ث) وتأكيد كل دفعة (mq/offset) بعد توزيعها؛ الدفعة غير المؤكَّدة تعيدها السحابة (استلام مرة على الأقل).
    ''' مضخة واحدة فقط لكل مفتاح: الدليل يرفض السحب المتزامن (LAP068001)، فتنتظر ثم تعيد. لا تستهلك حصة محدِّد أوامر PTZ.
    ''' </summary>
    Public NotInheritable Class HppEventPump
        Implements IDisposable

        Public Shared Property MinPollInterval As TimeSpan = TimeSpan.FromSeconds(1)
        Public Shared Property MaxBackoff As TimeSpan = TimeSpan.FromSeconds(60)
        Public Shared Property BusyBackoff As TimeSpan = TimeSpan.FromSeconds(10)
        Private Shared ReadOnly PollTimeout As TimeSpan = TimeSpan.FromSeconds(35)
        Private Const MaxItemsPerBatch As Integer = 500

        Private ReadOnly _cloud As HppCloud
        Private ReadOnly _targets As Dictionary(Of String, IHppEventTarget)
        Private ReadOnly _lock As New Object()
        Private _cts As Threading.CancellationTokenSource
        Private _task As Task
        Private _confirmed As Long
        Private _delivered As Long
        Private _subscribed As Boolean

        Public Sub New(cloud As HppCloud, targets As IEnumerable(Of IHppEventTarget))
            _cloud = cloud
            _targets = New Dictionary(Of String, IHppEventTarget)(StringComparer.Ordinal)
            For Each t As IHppEventTarget In targets
                If CameraTransports.IsValidCloudSerial(t.CloudSerial) Then _targets(t.CloudSerial) = t
            Next
        End Sub

        Public ReadOnly Property IsSubscribed As Boolean
            Get
                Return Threading.Volatile.Read(_subscribed)
            End Get
        End Property

        Public ReadOnly Property BatchesConfirmed As Long
            Get
                Return Threading.Interlocked.Read(_confirmed)
            End Get
        End Property

        Public ReadOnly Property EventsDelivered As Long
            Get
                Return Threading.Interlocked.Read(_delivered)
            End Get
        End Property

        Public Sub Start()
            SyncLock _lock
                If _task IsNot Nothing OrElse _targets.Count = 0 Then Return
                _cts = New Threading.CancellationTokenSource()
                Dim token As Threading.CancellationToken = _cts.Token
                _task = Task.Run(Function() RunAsync(token))
            End SyncLock
        End Sub

        Public Async Function StopAsync() As Task
            Dim t As Task
            SyncLock _lock
                t = _task
                _task = Nothing
                _cts?.Cancel()
            End SyncLock
            If t Is Nothing Then Return
            Try
                Await t.ConfigureAwait(False)
            Catch ex As OperationCanceledException
            End Try
            _cts.Dispose()
        End Function

        Private Sub Report(outcome As HppOutcome, code As String, status As Integer)
            _cloud.RaiseCallCompleted(New HppCallEventArgs("mq", outcome, HppJson.SafeCode(code), status))
        End Sub

        ''' <summary>مدة الانتظار بعد رمز خطأ من السحابة (Nothing = بلا انتظار خاص).</summary>
        Private Shared Function DelayFor(code As String, failures As Integer) As TimeSpan
            If code = "LAP068001" Then Return BusyBackoff
            Dim exp As Double = Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, Math.Min(failures, 6)))
            Return TimeSpan.FromSeconds(Math.Max(1, exp))
        End Function

        Private Async Function RunAsync(ct As Threading.CancellationToken) As Task
            Dim failures As Integer = 0
            While Not ct.IsCancellationRequested
                Dim wait As TimeSpan = TimeSpan.Zero
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                Try
                    If Not _subscribed Then
                        Dim body As String = JsonSerializer.Serialize(New With {.subType = 1, .subMode = "list", .deviceSerialList = _targets.Keys.OrderBy(Function(k) k, StringComparer.Ordinal).ToArray()})
                        Dim sr As HppApiResult = Await _cloud.PostApiAsync("/api/hpcgw/v1/mq/subscribe", body, TimeSpan.FromSeconds(15), ct).ConfigureAwait(False)
                        If sr.Code <> "0" Then
                            failures += 1
                            Report(If(HppResponseMapper.IsAuthCode(sr.Code), HppOutcome.AuthRejected, HppOutcome.Rejected), sr.Code, sr.HttpStatus)
                            wait = DelayFor(sr.Code, failures)
                        Else
                            Threading.Volatile.Write(_subscribed, True)
                        End If
                    End If
                    If _subscribed AndAlso wait = TimeSpan.Zero Then
                        Dim mr As HppApiResult = Await _cloud.PostApiAsync("/api/hpcgw/v1/mq/messages", "{}", PollTimeout, ct).ConfigureAwait(False)
                        If mr.Code <> "0" Then
                            failures += 1
                            Report(If(HppResponseMapper.IsAuthCode(mr.Code), HppOutcome.AuthRejected, HppOutcome.Rejected), mr.Code, mr.HttpStatus)
                            wait = DelayFor(mr.Code, failures)
                        Else
                            failures = 0
                            Dim batchId As String = Deliver(mr.Data)
                            If batchId <> "" Then
                                Dim body As String = JsonSerializer.Serialize(New With {.batchId = batchId})
                                Dim orr As HppApiResult = Await _cloud.PostApiAsync("/api/hpcgw/v1/mq/offset", body, TimeSpan.FromSeconds(15), ct).ConfigureAwait(False)
                                If orr.Code = "0" Then
                                    Threading.Interlocked.Increment(_confirmed)
                                Else
                                    Report(HppOutcome.Rejected, orr.Code, orr.HttpStatus)      ' تُعاد الدفعة لاحقاً؛ التكرار تكبحه ناقلة الإنذارات
                                End If
                            End If
                        End If
                    End If
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                    Exit While
                Catch ex As HppCloudException
                    failures += 1
                    Report(If(ex.IsAuthFailure, HppOutcome.AuthRejected, HppOutcome.Rejected), ex.Code, 0)
                    wait = DelayFor(ex.Code, failures)
                Catch ex As HttpRequestException
                    failures += 1
                    Report(HppOutcome.DeviceUnreachable, "cloudUnreachable", 0)
                    wait = DelayFor("", failures)
                End Try
                ' لا حلقة ساخنة إن عاد السحب فوراً بلا حجب (أو بعد خطأ).
                If wait < MinPollInterval - sw.Elapsed Then wait = MinPollInterval - sw.Elapsed
                If wait > TimeSpan.Zero Then
                    Try
                        Await Task.Delay(wait, ct).ConfigureAwait(False)
                    Catch ex As OperationCanceledException
                        Exit While
                    End Try
                End If
            End While
        End Function

        ''' <summary>يوزّع عناصر الدفعة على كاميراتها ويعيد batchId (أو "" إن لم يوجد). أعطال مستقبل واحد لا توقف البقية.</summary>
        Private Function Deliver(data As JsonElement) As String
            If data.ValueKind <> JsonValueKind.Object Then Return ""
            Dim batchId As String = HppJson.StringOf(data, "batchId")
            Dim list As JsonElement
            If HppJson.TryGetLoose(data, "list", list) AndAlso list.ValueKind = JsonValueKind.Array Then
                Dim n As Integer = 0
                For Each item As JsonElement In list.EnumerateArray()
                    n += 1
                    If n > MaxItemsPerBatch Then Exit For
                    Dim ev As HppDeviceEvent = HppEventParser.Parse(item)
                    If ev Is Nothing Then Continue For
                    Dim target As IHppEventTarget = Nothing
                    If Not _targets.TryGetValue(ev.Serial, target) Then Continue For   ' أجهزة أخرى في الحساب: لا تخصّنا
                    Try
                        target.OnCloudEvent(ev)
                        Threading.Interlocked.Increment(_delivered)
                    Catch ex As Exception When Not (TypeOf ex Is OutOfMemoryException)
                        Diagnostics.Debug.WriteLine("hpp event target failed: " & ex.GetType().Name)
                    End Try
                Next
            End If
            ' batchId يعود إلى السحابة فقط داخل JSON مُسلسَل (لا حقن)؛ نرفض الطول غير المعقول فقط.
            Return If(batchId.Length <= 256, batchId, "")
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            StopAsync().GetAwaiter().GetResult()
        End Sub
    End Class

End Namespace
