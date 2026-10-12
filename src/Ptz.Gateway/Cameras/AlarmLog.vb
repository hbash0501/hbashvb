Imports Ptz.Core.Abstractions
Imports Ptz.Core.Models
Imports Ptz.Core.Remote

Namespace Cameras

    ''' <summary>
    ''' سجل إنذارات البوابة (حلقة 500). الرقم التسلسلي seq = max(آخر+1، زمن unix بالميلي ثانية) فيبقى متزايداً عبر إعادة التشغيل
    ''' بلا تخزين. الإنذارات كائنات مشتركة مع ناقل الإنذارات فتبقى حالة الإقرار حيّة.
    ''' </summary>
    Public NotInheritable Class AlarmLog
        Private NotInheritable Class Row
            Public Seq As Long
            Public CameraId As String
            Public Alarm As AlarmEvent
            Public AckedBy As String
        End Class

        Private Const Capacity As Integer = 500
        Private ReadOnly _lock As New Object()
        Private ReadOnly _rows As New LinkedList(Of Row)()
        Private ReadOnly _clock As IClock
        Private _lastSeq As Long
        Private _evictedUpTo As Long

        Public Sub New(clock As IClock)
            _clock = clock
            _evictedUpTo = NowMs()
            _lastSeq = _evictedUpTo
        End Sub

        Private Function NowMs() As Long
            Return New DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
        End Function

        Public ReadOnly Property LastSeq As Long
            Get
                SyncLock _lock
                    Return _lastSeq
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property EvictedUpToSeq As Long
            Get
                SyncLock _lock
                    Return _evictedUpTo
                End SyncLock
            End Get
        End Property

        Public Function Add(cameraId As String, alarm As AlarmEvent) As AlarmDto
            SyncLock _lock
                _lastSeq = Math.Max(_lastSeq + 1, NowMs())
                Dim r As New Row With {.Seq = _lastSeq, .CameraId = cameraId, .Alarm = alarm}
                _rows.AddLast(r)
                While _rows.Count > Capacity
                    _evictedUpTo = _rows.First.Value.Seq
                    _rows.RemoveFirst()
                End While
                Return WireMap.ToAlarmDto(alarm, cameraId, r.Seq, Nothing)
            End SyncLock
        End Function

        ''' <summary>إنذارات seq &gt; afterSeq من الأقدم للأحدث. resync=True إن كان afterSeq أقدم مما تحتفظ به الحلقة.</summary>
        Public Function Query(afterSeq As Long, hasAfter As Boolean, limit As Integer, cameraId As String, unackedOnly As Boolean, ByRef resync As Boolean) As List(Of AlarmDto)
            SyncLock _lock
                resync = False
                Dim filtered As IEnumerable(Of Row) = _rows.Where(Function(r) (cameraId Is Nothing OrElse r.CameraId = cameraId) AndAlso (Not unackedOnly OrElse Not r.Alarm.Acknowledged))
                Dim list As List(Of Row)
                If hasAfter AndAlso afterSeq > 0 Then
                    If afterSeq < _evictedUpTo Then
                        resync = True
                        list = filtered.Reverse().Take(Math.Min(50, limit)).Reverse().ToList()
                    Else
                        list = filtered.Where(Function(r) r.Seq > afterSeq).Take(limit).ToList()
                    End If
                Else
                    list = filtered.Reverse().Take(limit).Reverse().ToList()
                End If
                Return list.Select(Function(r) WireMap.ToAlarmDto(r.Alarm, r.CameraId, r.Seq, r.AckedBy)).ToList()
            End SyncLock
        End Function

        ''' <summary>إنذارات كاميرا بعد رقم (للاستئناف عبر Last-Event-ID).</summary>
        Public Function Replay(cameraId As String, afterSeq As Long, ByRef resync As Boolean) As List(Of AlarmDto)
            SyncLock _lock
                resync = afterSeq < _evictedUpTo
                If resync Then Return New List(Of AlarmDto)()
                Return _rows.Where(Function(r) r.CameraId = cameraId AndAlso r.Seq > afterSeq).
                    Select(Function(r) WireMap.ToAlarmDto(r.Alarm, r.CameraId, r.Seq, r.AckedBy)).ToList()
            End SyncLock
        End Function

        ''' <summary>يقرّ بإنذار. يعيد كاميرته أو Nothing إن لم يوجد (أو أُخلي).</summary>
        Public Function TryAck(id As Guid, by As String) As String
            SyncLock _lock
                For Each r As Row In _rows
                    If r.Alarm.Id = id Then
                        r.Alarm.Acknowledged = True
                        r.AckedBy = by
                        Return r.CameraId
                    End If
                Next
            End SyncLock
            Return Nothing
        End Function
    End Class

End Namespace
