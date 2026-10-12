Imports Ptz.Core.Abstractions
Imports Ptz.Core.Models

Namespace Cameras

    ''' <summary>
    ''' عقد حركة PTZ على جهة الخادم: كل أمر حركة يجدّد مهلة (ttl). إن لم يصل أمر حركة/إيقاف قبل انقضائها (انهيار العميل،
    ''' موت النفق) أو أُبطلت جلسة المالك، يُصدر <see cref="Tick"/> أمر إيقاف مرة واحدة. فئة نقية بلا I/O.
    ''' </summary>
    Public NotInheritable Class PtzLease
        Private ReadOnly _clock As IClock
        Private ReadOnly _lock As New Object()
        Private _owner As Guid
        Private _deadline As DateTime = DateTime.MinValue
        Private _active As Boolean
        Private _lastVector As PtzVector
        Private _lastForwardUtc As DateTime = DateTime.MinValue
        Private _lastActivityUtc As DateTime = DateTime.MinValue

        Public Sub New(clock As IClock)
            _clock = clock
        End Sub

        Public ReadOnly Property LastActivityUtc As DateTime
            Get
                SyncLock _lock
                    Return _lastActivityUtc
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property IsActive As Boolean
            Get
                SyncLock _lock
                    Return _active
                End SyncLock
            End Get
        End Property

        ''' <summary>
        ''' يسجّل أمر حركة غير صفري ويجدّد العقد. يعيد True إن وجب تمريره للكاميرا (تغيّر المتجه، أو مرّ forwardMinMs
        ''' على آخر تمرير، أو تغيّر المالك). <paramref name="renew"/> = إعادة إرسال دورية من العميل للحفاظ على العقد:
        ''' تُتجاهل إن لم يعد المرسل مالك عقد نشط (مثلاً أوقف مشغّل آخر الكاميرا) كي لا يتغلب التجديد الآلي على إيقاف زميل.
        ''' </summary>
        Public Function OnMove(owner As Guid, v As PtzVector, ttlMs As Integer, forwardMinMs As Integer, Optional renew As Boolean = False) As Boolean
            Dim now As DateTime = _clock.UtcNow
            SyncLock _lock
                If renew AndAlso (Not _active OrElse owner <> _owner) Then Return False
                _lastActivityUtc = now
                Dim changed As Boolean = Not _active OrElse owner <> _owner OrElse v.Pan <> _lastVector.Pan OrElse v.Tilt <> _lastVector.Tilt OrElse v.Zoom <> _lastVector.Zoom
                _owner = owner
                _active = True
                _deadline = now.AddMilliseconds(ttlMs)
                If changed OrElse (now - _lastForwardUtc).TotalMilliseconds >= forwardMinMs Then
                    _lastVector = v
                    _lastForwardUtc = now
                    Return True
                End If
                Return False
            End SyncLock
        End Function

        ''' <summary>هل لهذا المالك عقد حركة نشط الآن؟ (تجديد من غير المالك يجب ألا يعلّق الكشف ولا يلمس شيئاً).</summary>
        Public Function OwnsActive(owner As Guid) As Boolean
            SyncLock _lock
                Return _active AndAlso owner = _owner
            End SyncLock
        End Function

        Public Sub OnStop(owner As Guid)
            SyncLock _lock
                _lastActivityUtc = _clock.UtcNow
                _active = False
                _lastVector = PtzVector.Zero
                _lastForwardUtc = DateTime.MinValue
            End SyncLock
        End Sub

        ''' <summary>إبطال جلسة: إن كانت المالكة للعقد يلزم إيقاف (True).</summary>
        Public Function OnSessionRevoked(owner As Guid) As Boolean
            SyncLock _lock
                If Not _active OrElse owner <> _owner Then Return False
                _active = False
                _lastVector = PtzVector.Zero
                Return True
            End SyncLock
        End Function

        ''' <summary>True مرة واحدة عند انقضاء العقد ⇒ يجب إصدار إيقاف.</summary>
        Public Function Tick() As Boolean
            Dim now As DateTime = _clock.UtcNow
            SyncLock _lock
                If _active AndAlso now >= _deadline Then
                    _active = False
                    _lastVector = PtzVector.Zero
                    _lastForwardUtc = DateTime.MinValue
                    Return True
                End If
                Return False
            End SyncLock
        End Function
    End Class

End Namespace
