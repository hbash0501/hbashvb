Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Channels
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Models

Namespace Alarms

    Public Interface IAlarmSink
        Sub Accept(alarm As AlarmEvent)
    End Interface

    ''' <summary>
    ''' ناقل الإنذارات: إزالة التكرار بنافذة تبريد، سجل حديث محدود، وتوزيع على المشتركين والمستقبِلات
    ''' (ملف، Webhook للتكامل مع أنظمة الإنذار). أخطاء المشتركين معزولة ولا تُسقط الناقل.
    ''' </summary>
    Public NotInheritable Class AlarmBus
        Private ReadOnly _clock As IClock
        Private ReadOnly _cooldown As TimeSpan
        Private ReadOnly _capacity As Integer
        Private ReadOnly _lock As New Object()
        Private ReadOnly _recent As New LinkedList(Of AlarmEvent)()
        Private ReadOnly _lastByKey As New Dictionary(Of String, DateTime)()
        Private ReadOnly _handlers As New List(Of Action(Of AlarmEvent))()
        Private ReadOnly _sinks As New List(Of IAlarmSink)()

        Public Sub New(Optional clock As IClock = Nothing, Optional cooldownSeconds As Double = 5.0, Optional historyCapacity As Integer = 500)
            _clock = If(clock, SystemClock.Instance)
            _cooldown = TimeSpan.FromSeconds(cooldownSeconds)
            _capacity = historyCapacity
        End Sub

        Public Sub AddSink(sink As IAlarmSink)
            SyncLock _lock
                _sinks.Add(sink)
            End SyncLock
        End Sub

        Public Function Subscribe(handler As Action(Of AlarmEvent)) As IDisposable
            SyncLock _lock
                _handlers.Add(handler)
            End SyncLock
            Return New Unsubscriber(Me, handler)
        End Function

        Private NotInheritable Class Unsubscriber
            Implements IDisposable
            Private ReadOnly _bus As AlarmBus
            Private ReadOnly _h As Action(Of AlarmEvent)
            Public Sub New(bus As AlarmBus, h As Action(Of AlarmEvent))
                _bus = bus
                _h = h
            End Sub
            Public Sub Dispose() Implements IDisposable.Dispose
                SyncLock _bus._lock
                    _bus._handlers.Remove(_h)
                End SyncLock
            End Sub
        End Class

        ''' <summary>ينشر إنذاراً. يعيد False إن كُتم بسبب التكرار ضمن نافذة التبريد.</summary>
        Public Function Publish(alarm As AlarmEvent) As Boolean
            If alarm Is Nothing Then Throw New ArgumentNullException(NameOf(alarm))
            Dim now As DateTime = _clock.UtcNow
            Dim handlers As Action(Of AlarmEvent)()
            Dim sinks As IAlarmSink()
            SyncLock _lock
                Dim last As DateTime
                If _lastByKey.TryGetValue(alarm.DedupeKey, last) AndAlso now - last < _cooldown AndAlso alarm.Severity < AlarmSeverity.Critical Then
                    Return False
                End If
                _lastByKey(alarm.DedupeKey) = now
                If _lastByKey.Count > 5000 Then PruneKeys(now)
                alarm.TimeUtc = now
                _recent.AddFirst(alarm)
                While _recent.Count > _capacity
                    _recent.RemoveLast()
                End While
                handlers = _handlers.ToArray()
                sinks = _sinks.ToArray()
            End SyncLock
            For Each h As Action(Of AlarmEvent) In handlers
                Try
                    h(alarm)
                Catch ex As Exception
                    Diagnostics.Debug.WriteLine("Alarm handler failed: " & ex.Message)
                End Try
            Next
            For Each s As IAlarmSink In sinks
                Try
                    s.Accept(alarm)
                Catch ex As Exception
                    Diagnostics.Debug.WriteLine("Alarm sink failed: " & ex.Message)
                End Try
            Next
            Return True
        End Function

        ''' <summary>
        ''' يستورد إنذاراً صادراً من بوابة بعيدة: يحفظ معرّفه وزمنه الأصليين (لا يُستبدل الزمن)، ويتجاهل المكرَّر بالمعرّف
        ''' (إعادة بث بعد انقطاع)، ولا يطبّق نافذة التبريد لأن البوابة طبّقتها. يعيد False إن كان معروفاً مسبقاً.
        ''' </summary>
        Public Function PublishRemote(alarm As AlarmEvent) As Boolean
            If alarm Is Nothing Then Throw New ArgumentNullException(NameOf(alarm))
            Dim handlers As Action(Of AlarmEvent)()
            Dim sinks As IAlarmSink()
            SyncLock _lock
                For Each a As AlarmEvent In _recent
                    If a.Id = alarm.Id Then Return False
                Next
                _recent.AddFirst(alarm)
                While _recent.Count > _capacity
                    _recent.RemoveLast()
                End While
                handlers = _handlers.ToArray()
                sinks = _sinks.ToArray()
            End SyncLock
            Dispatch(alarm, handlers, sinks)
            Return True
        End Function

        Private Shared Sub Dispatch(alarm As AlarmEvent, handlers As Action(Of AlarmEvent)(), sinks As IAlarmSink())
            For Each h As Action(Of AlarmEvent) In handlers
                Try
                    h(alarm)
                Catch ex As Exception
                    Diagnostics.Debug.WriteLine("Alarm handler failed: " & ex.Message)
                End Try
            Next
            For Each s As IAlarmSink In sinks
                Try
                    s.Accept(alarm)
                Catch ex As Exception
                    Diagnostics.Debug.WriteLine("Alarm sink failed: " & ex.Message)
                End Try
            Next
        End Sub

        Private Sub PruneKeys(now As DateTime)
            Dim old As New List(Of String)()
            For Each kv In _lastByKey
                If now - kv.Value > _cooldown Then old.Add(kv.Key)
            Next
            For Each k As String In old
                _lastByKey.Remove(k)
            Next
        End Sub

        Public Function Recent(count As Integer) As List(Of AlarmEvent)
            SyncLock _lock
                Return _recent.Take(count).ToList()
            End SyncLock
        End Function

        ''' <summary>يُرفع عند إقرار محلي بإنذار (ليبلّغ الوضع البعيد البوابة). لا يُرفع عند <see cref="AcknowledgeRemote"/>.</summary>
        Public Event Acknowledged As Action(Of AlarmEvent)

        Private Function MarkAcknowledged(id As Guid) As AlarmEvent
            SyncLock _lock
                For Each a As AlarmEvent In _recent
                    If a.Id = id Then
                        a.Acknowledged = True
                        Return a
                    End If
                Next
            End SyncLock
            Return Nothing
        End Function

        Public Function Acknowledge(id As Guid) As Boolean
            Dim a As AlarmEvent = MarkAcknowledged(id)
            If a Is Nothing Then Return False
            Try
                RaiseEvent Acknowledged(a)
            Catch ex As Exception
                Diagnostics.Debug.WriteLine("Alarm ack handler failed: " & ex.Message)
            End Try
            Return True
        End Function

        ''' <summary>إقرار صادر من مشغّل آخر عبر البوابة: يحدّث الحالة دون إعادة الإرسال.</summary>
        Public Function AcknowledgeRemote(id As Guid) As Boolean
            Return MarkAcknowledged(id) IsNot Nothing
        End Function
    End Class

    ''' <summary>يكتب الإنذارات إلى ملف JSONL.</summary>
    Public NotInheritable Class FileAlarmSink
        Implements IAlarmSink
        Private ReadOnly _path As String
        Private ReadOnly _lock As New Object()

        Public Sub New(path As String)
            _path = path
            Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(path))
            If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
        End Sub

        Public Sub Accept(alarm As AlarmEvent) Implements IAlarmSink.Accept
            Dim line As String = JsonSerializer.Serialize(New With {
                alarm.Id, .TimeUtc = alarm.TimeUtc.ToString("o", Globalization.CultureInfo.InvariantCulture), alarm.Camera, alarm.Kind,
                .Severity = alarm.Severity.ToString(), alarm.Message, alarm.TrackId, .X = alarm.Position.X, .Y = alarm.Position.Y})
            SyncLock _lock
                IO.File.AppendAllText(_path, line & Environment.NewLine, Encoding.UTF8)
            End SyncLock
        End Sub
    End Class

    ''' <summary>
    ''' يرسل الإنذارات إلى نظام خارجي (POST JSON) عبر HTTPS فقط، موقّعةً بـ HMAC-SHA256 في ترويسة
    ''' X-Signature مع X-Timestamp لمنع إعادة التشغيل. قائمة انتظار محدودة لا تحجب مسار التحليل.
    ''' </summary>
    Public NotInheritable Class WebhookAlarmSink
        Implements IAlarmSink, IDisposable

        Private ReadOnly _url As Uri
        Private ReadOnly _secret As Byte()
        Private ReadOnly _http As HttpClient
        Private ReadOnly _queue As Channel(Of AlarmEvent)
        Private ReadOnly _cts As New Threading.CancellationTokenSource()
        Private ReadOnly _worker As Task

        Public Sub New(url As Uri, secret As Byte(), Optional handler As HttpMessageHandler = Nothing, Optional allowInsecure As Boolean = False)
            If url Is Nothing Then Throw New ArgumentNullException(NameOf(url))
            If url.Scheme <> Uri.UriSchemeHttps AndAlso Not allowInsecure Then Throw New ArgumentException("Webhook يتطلب HTTPS.", NameOf(url))
            If secret Is Nothing OrElse secret.Length < 16 Then Throw New ArgumentException("سر التوقيع قصير.", NameOf(secret))
            _url = url
            _secret = CType(secret.Clone(), Byte())
            _http = If(handler Is Nothing, New HttpClient(), New HttpClient(handler))
            _http.Timeout = TimeSpan.FromSeconds(10)
            _queue = Channel.CreateBounded(Of AlarmEvent)(New BoundedChannelOptions(1000) With {.FullMode = BoundedChannelFullMode.DropOldest})
            _worker = Task.Run(AddressOf PumpAsync)
        End Sub

        Public Sub Accept(alarm As AlarmEvent) Implements IAlarmSink.Accept
            _queue.Writer.TryWrite(alarm)
        End Sub

        Public Shared Function Sign(secret As Byte(), timestamp As String, body As String) As String
            Using h As New HMACSHA256(secret)
                Return "sha256=" & Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(timestamp & "." & body))).ToLowerInvariant()
            End Using
        End Function

        Private Async Function PumpAsync() As Task
            Try
                While Await _queue.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(False)
                    Dim a As AlarmEvent = Nothing
                    While _queue.Reader.TryRead(a)
                        Await SendAsync(a).ConfigureAwait(False)
                    End While
                End While
            Catch ex As OperationCanceledException
            End Try
        End Function

        Private Async Function SendAsync(a As AlarmEvent) As Task
            Dim body As String = JsonSerializer.Serialize(New With {
                a.Id, .TimeUtc = a.TimeUtc.ToString("o", Globalization.CultureInfo.InvariantCulture), a.Camera, a.Kind,
                .Severity = a.Severity.ToString(), a.Message, a.TrackId})
            Dim ts As String = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(Globalization.CultureInfo.InvariantCulture)
            Try
                Using req As New HttpRequestMessage(HttpMethod.Post, _url)
                    req.Content = New StringContent(body, Encoding.UTF8, "application/json")
                    req.Headers.Add("X-Timestamp", ts)
                    req.Headers.Add("X-Signature", Sign(_secret, ts, body))
                    Using resp As HttpResponseMessage = Await _http.SendAsync(req, _cts.Token).ConfigureAwait(False)
                        ' فشل التسليم لا يؤثر على التشغيل؛ يمكن إضافة إعادة محاولة لاحقاً.
                    End Using
                End Using
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is TaskCanceledException
                Diagnostics.Debug.WriteLine("Webhook delivery failed: " & ex.Message)
            End Try
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _queue.Writer.TryComplete()
            _cts.Cancel()
            Try
                _worker.Wait(TimeSpan.FromSeconds(2))
            Catch ex As AggregateException
            End Try
            _http.Dispose()
            _cts.Dispose()
        End Sub
    End Class

End Namespace
