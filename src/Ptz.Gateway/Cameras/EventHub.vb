Imports System.Threading.Channels

Namespace Cameras

    ''' <summary>رسالة SSE جاهزة: اسم الحدث و(اختيارياً) معرّف ونص JSON أحادي السطر. تُسلسل مرة واحدة وتُشارك بين المشتركين.</summary>
    Public NotInheritable Class SseMessage
        Public ReadOnly Property EventName As String
        Public ReadOnly Property Id As String
        Public ReadOnly Property Data As String

        Public Sub New(eventName As String, id As String, data As String)
            Me.EventName = eventName
            Me.Id = id
            Me.Data = data
        End Sub

        ''' <summary>صيغة السلك: LF، سطر data واحد لكل سطر من البيانات (CR يُحذف) وسطر فارغ ينهي الحدث.</summary>
        Public Function Format() As String
            Dim sb As New Text.StringBuilder()
            sb.Append("event: ").Append(Sanitize(EventName)).Append(ControlChars.Lf)
            If Id IsNot Nothing Then sb.Append("id: ").Append(Sanitize(Id)).Append(ControlChars.Lf)
            For Each line As String In Data.Replace(vbCr, "").Split(ControlChars.Lf)
                sb.Append("data: ").Append(line).Append(ControlChars.Lf)
            Next
            sb.Append(ControlChars.Lf)
            Return sb.ToString()
        End Function

        Private Shared Function Sanitize(s As String) As String
            Return s.Replace(vbCr, "").Replace(vbLf, "")
        End Function
    End Class

    ''' <summary>مشترك واحد في بث الكاميرا: قناة تحكم لا تُفقد (محدودة) وقناة تراكب تُسقط الأقدم (الأحدث يفوز).</summary>
    Public NotInheritable Class SseSubscriber
        Implements IDisposable

        Private ReadOnly _hub As EventHub
        Private _overflowed As Integer
        Public ReadOnly Property Control As Channel(Of SseMessage) = Channel.CreateBounded(Of SseMessage)(New BoundedChannelOptions(256) With {.FullMode = BoundedChannelFullMode.Wait, .SingleReader = True})
        Public ReadOnly Property Overlay As Channel(Of SseMessage) = Channel.CreateBounded(Of SseMessage)(New BoundedChannelOptions(2) With {.FullMode = BoundedChannelFullMode.DropOldest, .SingleReader = True})

        Friend Sub New(hub As EventHub)
            _hub = hub
        End Sub

        Public ReadOnly Property Overflowed As Boolean
            Get
                Return Threading.Volatile.Read(_overflowed) = 1
            End Get
        End Property

        Friend Sub PushControl(m As SseMessage)
            If Not Control.Writer.TryWrite(m) Then Threading.Volatile.Write(_overflowed, 1)
        End Sub

        Friend Sub PushOverlay(m As SseMessage)
            Overlay.Writer.TryWrite(m)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _hub.Remove(Me)
            Control.Writer.TryComplete()
            Overlay.Writer.TryComplete()
        End Sub
    End Class

    ''' <summary>موزّع أحداث SSE لكاميرا واحدة. الناشرون (خيط المحرك) لا ينتظرون أبداً عميلاً بطيئاً.</summary>
    Public NotInheritable Class EventHub
        Private ReadOnly _lock As New Object()
        Private ReadOnly _subs As New List(Of SseSubscriber)()

        Public ReadOnly Property SubscriberCount As Integer
            Get
                SyncLock _lock
                    Return _subs.Count
                End SyncLock
            End Get
        End Property

        Public Function Subscribe() As SseSubscriber
            Dim s As New SseSubscriber(Me)
            SyncLock _lock
                _subs.Add(s)
            End SyncLock
            Return s
        End Function

        Friend Sub Remove(s As SseSubscriber)
            SyncLock _lock
                _subs.Remove(s)
            End SyncLock
        End Sub

        Private Function Snapshot() As SseSubscriber()
            SyncLock _lock
                Return _subs.ToArray()
            End SyncLock
        End Function

        Public Sub PublishControl(m As SseMessage)
            For Each s As SseSubscriber In Snapshot()
                s.PushControl(m)
            Next
        End Sub

        Public Sub PublishOverlay(m As SseMessage)
            For Each s As SseSubscriber In Snapshot()
                s.PushOverlay(m)
            Next
        End Sub
    End Class

End Namespace
