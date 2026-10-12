Imports Ptz.Core.Security

Namespace Audit

    ''' <summary>حالة صحة التدقيق: إن تعذّرت الكتابة تُرفض العمليات المغيِّرة (لا تنفيذ بلا أثر تدقيقي).</summary>
    Public NotInheritable Class AuditHealth
        Private _healthy As Integer = 1
        Public ReadOnly Property IsHealthy As Boolean
            Get
                Return Threading.Volatile.Read(_healthy) = 1
            End Get
        End Property
        Public Sub MarkFailed()
            Threading.Volatile.Write(_healthy, 0)
        End Sub
        Public Sub MarkHealthy()
            Threading.Volatile.Write(_healthy, 1)
        End Sub
    End Class

    ''' <summary>
    ''' يلفّ سجل التدقيق المشترك: يضيف لكل سجل بادئة [ip= sid= rid= via=gw] من سياق الطلب الحالي، وينظّف الحقول.
    ''' <b>لا يرمي أبداً</b>: عند فشل الكتابة يضع السجل في طابور ذاكرة محدود (1000، الأقدم يُسقَط مع عدّاد) ويحوّل الحالة إلى
    ''' "غير سليم" فتُرفض العمليات المغيِّرة (عدا الإيقاف وتسجيل الخروج) إلى أن ينجح <see cref="TryRecover"/> في تفريغ الطابور.
    ''' </summary>
    Public NotInheritable Class RemoteAuditLog
        Implements IAuditLog

        Private Structure Pending
            Public User As String
            Public Action As String
            Public Target As String
            Public Outcome As String
            Public Details As String
        End Structure

        Private Const SpoolCapacity As Integer = 1000
        Private ReadOnly _inner As IAuditLog
        Private ReadOnly _health As AuditHealth
        Private ReadOnly _spool As New Queue(Of Pending)()
        Private ReadOnly _lock As New Object()
        Private _dropped As Integer

        Public Sub New(inner As IAuditLog, health As AuditHealth)
            _inner = inner
            _health = health
        End Sub

        Public ReadOnly Property DroppedCount As Integer
            Get
                SyncLock _lock
                    Return _dropped
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property SpoolCount As Integer
            Get
                SyncLock _lock
                    Return _spool.Count
                End SyncLock
            End Get
        End Property

        Public Sub Write(user As String, action As String, target As String, outcome As String, details As String) Implements IAuditLog.Write
            Dim s As AuditScopeData = AuditScope.Current
            Dim prefix As String = ""
            If s IsNot Nothing Then prefix = $"[ip={s.Ip} sid={s.SessionRef} rid={s.RequestId} via=gw] "
            Dim p As New Pending With {.User = user, .Action = action, .Target = target, .Outcome = outcome, .Details = prefix & AuditSanitizer.Clean(details, 1000)}
            SyncLock _lock
                If _spool.Count > 0 Then
                    Enqueue(p)
                    Return
                End If
                If Not TryWrite(p) Then Enqueue(p)
            End SyncLock
        End Sub

        Private Sub Enqueue(p As Pending)
            If _spool.Count >= SpoolCapacity Then
                _spool.Dequeue()
                _dropped += 1
            End If
            _spool.Enqueue(p)
            _health.MarkFailed()
        End Sub

        Private Function TryWrite(p As Pending) As Boolean
            Try
                _inner.Write(p.User, p.Action, p.Target, p.Outcome, p.Details)
                Return True
            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Return False
            End Try
        End Function

        ''' <summary>يفرّغ الطابور بالترتيب؛ عند النجاح الكامل تعود الصحة. يُستدعى دورياً (كل 10 ث) من الصيانة.</summary>
        Public Function TryRecover() As Boolean
            SyncLock _lock
                While _spool.Count > 0
                    If Not TryWrite(_spool.Peek()) Then Return False
                    _spool.Dequeue()
                End While
                _health.MarkHealthy()
                Return True
            End SyncLock
        End Function
    End Class

    ''' <summary>
    ''' يجمّع حالات الرفض (401/403/429) في سجل واحد لكل (IP، سبب) في الدقيقة كي لا يملأ فيضان طلبات القرص.
    ''' </summary>
    Public NotInheritable Class DeniedAggregator
        Private NotInheritable Class Bucket
            Public WindowStartUtc As DateTime
            Public Count As Integer
        End Class

        Private ReadOnly _audit As IAuditLog
        Private ReadOnly _lock As New Object()
        Private ReadOnly _buckets As New Dictionary(Of String, Bucket)()
        Private Const MaxKeys As Integer = 5000

        Public Sub New(audit As IAuditLog)
            _audit = audit
        End Sub

        Public Sub Record(ip As String, reason As String, user As String, nowUtc As DateTime)
            Dim key As String = ip & "|" & reason
            Dim flushCount As Integer = 0
            Dim first As Boolean = False
            SyncLock _lock
                Dim b As Bucket = Nothing
                If Not _buckets.TryGetValue(key, b) Then
                    If _buckets.Count >= MaxKeys Then _buckets.Clear()
                    b = New Bucket With {.WindowStartUtc = nowUtc, .Count = 0}
                    _buckets(key) = b
                    first = True
                ElseIf nowUtc - b.WindowStartUtc >= TimeSpan.FromMinutes(1) Then
                    If b.Count > 1 Then flushCount = b.Count
                    b.WindowStartUtc = nowUtc
                    b.Count = 0
                    first = True
                End If
                b.Count += 1
            End SyncLock
            If flushCount > 0 Then _audit.Write("svc:gateway", "Guard.DeniedSummary", reason, "DENIED", $"ip={ip} count={flushCount}")
            If first Then _audit.Write(If(user, "(anonymous)"), "Guard.Denied", reason, "DENIED", $"ip={ip}")
        End Sub
    End Class

End Namespace
