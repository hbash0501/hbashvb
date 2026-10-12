Imports Ptz.Core.Abstractions

Namespace Security

    ''' <summary>دلو رموز لتحديد المعدل لكل جلسة (معدل إعادة التعبئة بالثانية + سعة اندفاع). آمن عبر الخيوط.</summary>
    Public NotInheritable Class TokenBucket
        Private ReadOnly _rate As Double
        Private ReadOnly _burst As Double
        Private ReadOnly _clock As IClock
        Private ReadOnly _lock As New Object()
        Private _tokens As Double
        Private _last As DateTime

        Public Sub New(ratePerSecond As Double, burst As Double, Optional clock As IClock = Nothing)
            If ratePerSecond <= 0 OrElse burst < 1 Then Throw New ArgumentOutOfRangeException(NameOf(ratePerSecond))
            _rate = ratePerSecond
            _burst = burst
            _clock = If(clock, SystemClock.Instance)
            _tokens = burst
            _last = _clock.UtcNow
        End Sub

        Public Function TryTake(Optional cost As Double = 1.0) As Boolean
            SyncLock _lock
                Refill()
                If _tokens >= cost Then
                    _tokens -= cost
                    Return True
                End If
                Return False
            End SyncLock
        End Function

        ''' <summary>الثواني حتى يتوفر رمز واحد (للترويسة Retry-After).</summary>
        Public Function RetryAfterSeconds() As Integer
            SyncLock _lock
                Refill()
                If _tokens >= 1.0 Then Return 0
                Return CInt(Math.Ceiling((1.0 - _tokens) / _rate))
            End SyncLock
        End Function

        Private Sub Refill()
            Dim now As DateTime = _clock.UtcNow
            Dim dt As Double = (now - _last).TotalSeconds
            If dt > 0 Then
                _tokens = Math.Min(_burst, _tokens + dt * _rate)
                _last = now
            End If
        End Sub
    End Class

End Namespace
