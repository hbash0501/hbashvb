Imports System.Collections.Concurrent
Imports System.Security.Cryptography
Imports System.Text
Imports Ptz.Core.Abstractions

Namespace Security

    Public NotInheritable Class TokenHasher
        Private Sub New()
        End Sub

        ''' <summary>الصيغة الوحيدة المقبولة: ptzs_ + 43 محرف base64url.</summary>
        Public Shared Function IsWellFormed(token As String) As Boolean
            If token Is Nothing OrElse token.Length <> 48 OrElse Not token.StartsWith("ptzs_", StringComparison.Ordinal) Then Return False
            For i As Integer = 5 To token.Length - 1
                Dim c As Char = token(i)
                If Not (Char.IsAsciiLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c) Then Return False
            Next
            Return True
        End Function

        Public Shared Function Hash(token As String) As String
            Return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)))
        End Function

        Public Shared Function NewToken() As String
            Return "ptzs_" & Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd("="c).Replace("+"c, "-"c).Replace("/"c, "_"c)
        End Function
    End Class

    ''' <summary>جلسة بعيدة واحدة على البوابة. لا تُحفظ في أي مكان (ذاكرة فقط) ولا يُخزَّن الرمز نفسه بل تجزئته.</summary>
    Public NotInheritable Class SessionRecord
        Private ReadOnly _revoked As New Threading.CancellationTokenSource()
        Private ReadOnly _lock As New Object()
        Private _lastActivity As DateTime
        Private _stepUpUntil As DateTime = DateTime.MinValue

        Public ReadOnly Property SessionId As Guid = Guid.NewGuid()
        Public ReadOnly Property SessionRef As String
        Public ReadOnly Property UserName As String
        Public ReadOnly Property Role As Role
        ''' <summary>بصمة الأمان وقت الدخول (دور/كلمة مرور/تعطيل/TOTP) للمقارنة في كل طلب.</summary>
        Public ReadOnly Property Stamp As String
        Public ReadOnly Property UserSession As UserSession
        Public ReadOnly Property CreatedUtc As DateTime
        Public ReadOnly Property AbsoluteExpiryUtc As DateTime
        Public ReadOnly Property ClientIp As String
        Public ReadOnly Property ClientName As String
        ''' <summary>تخزين خاص بالجلسة (مثل المتحكمات المؤمَّنة المخبّأة لكل كاميرا).</summary>
        Public ReadOnly Property Items As New ConcurrentDictionary(Of String, Object)()
        Public ReadOnly Property Buckets As New ConcurrentDictionary(Of String, TokenBucket)()
        Public ReadOnly Property LastSeq As New ConcurrentDictionary(Of String, Long)()
        Public StreamCount As Integer

        Friend Sub New(userName As String, role As Role, stamp As String, ip As String, client As String, now As DateTime, absolute As TimeSpan)
            Me.UserName = userName
            Me.Role = role
            Me.Stamp = stamp
            ClientIp = ip
            ClientName = AuditSanitizer.Clean(client, 64)
            CreatedUtc = now
            AbsoluteExpiryUtc = now + absolute
            _lastActivity = now
            SessionRef = Convert.ToHexString(SHA256.HashData(SessionId.ToByteArray())).Substring(0, 8).ToLowerInvariant()
            UserSession = New UserSession(userName, role, now, TimeSpan.FromDays(3650))
        End Sub

        Public ReadOnly Property LastActivityUtc As DateTime
            Get
                SyncLock _lock
                    Return _lastActivity
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property StepUpUntilUtc As DateTime
            Get
                SyncLock _lock
                    Return _stepUpUntil
                End SyncLock
            End Get
        End Property

        Public Sub Touch(now As DateTime)
            SyncLock _lock
                If now > _lastActivity Then _lastActivity = now
            End SyncLock
        End Sub

        Public Sub GrantStepUp(untilUtc As DateTime)
            SyncLock _lock
                _stepUpUntil = untilUtc
            End SyncLock
        End Sub

        ''' <summary>يُلغى عند الإبطال: تنتهي به البثوث المفتوحة ويوقف عقد PTZ.</summary>
        Public ReadOnly Property RevokedToken As Threading.CancellationToken
            Get
                Return _revoked.Token
            End Get
        End Property

        Public ReadOnly Property IsRevoked As Boolean
            Get
                Return _revoked.IsCancellationRequested
            End Get
        End Property

        Friend Sub MarkRevoked()
            Try
                _revoked.Cancel()
            Catch ex As AggregateException
                ' معالج إلغاء فشل: الجلسة ما زالت مُبطلة.
            End Try
        End Sub

        Public Function Bucket(name As String, ratePerSecond As Double, burst As Double, clock As IClock) As TokenBucket
            Return Buckets.GetOrAdd(name, Function(k) New TokenBucket(ratePerSecond, burst, clock))
        End Function
    End Class

    Public Enum SessionAuthResult
        Ok
        Unknown
        Expired
    End Enum

    ''' <summary>سجل الجلسات البعيدة في الذاكرة. إعادة تشغيل البوابة تُبطل كل الجلسات (مفتاح إيقاف طبيعي).</summary>
    Public NotInheritable Class SessionRegistry
        Private ReadOnly _byHash As New ConcurrentDictionary(Of String, SessionRecord)()
        Private ReadOnly _clock As IClock
        Private ReadOnly _idle As TimeSpan
        Private ReadOnly _absolute As TimeSpan
        Private ReadOnly _maxPerUser As Integer
        Private ReadOnly _createLock As New Object()

        Public Sub New(clock As IClock, idle As TimeSpan, absolute As TimeSpan, maxPerUser As Integer)
            _clock = clock
            _idle = idle
            _absolute = absolute
            _maxPerUser = maxPerUser
        End Sub

        Public ReadOnly Property Count As Integer
            Get
                Return _byHash.Count
            End Get
        End Property

        ''' <summary>ينشئ جلسة جديدة (رمز جديد دائماً ⇒ لا تثبيت جلسة). يُبطل الأقدم عند تجاوز الحد لكل مستخدم.</summary>
        Public Function Create(userName As String, role As Role, stamp As String, ip As String, client As String, ByRef token As String) As SessionRecord
            Dim rec As New SessionRecord(userName, role, stamp, ip, client, _clock.UtcNow, _absolute)
            token = TokenHasher.NewToken()
            SyncLock _createLock
                Dim mine As List(Of SessionRecord) = _byHash.Values.Where(Function(r) String.Equals(r.UserName, userName, StringComparison.OrdinalIgnoreCase) AndAlso Not r.IsRevoked).
                    OrderBy(Function(r) r.CreatedUtc).ToList()
                While mine.Count >= _maxPerUser
                    Revoke(mine(0))
                    mine.RemoveAt(0)
                End While
                _byHash(TokenHasher.Hash(token)) = rec
            End SyncLock
            Return rec
        End Function

        ''' <summary>يتحقق من الرمز ويعيد الجلسة أو سبب الفشل. لا يجدّد النشاط (التجديد صراحةً بـ Touch).</summary>
        Public Function TryAuthenticate(token As String, ByRef result As SessionAuthResult) As SessionRecord
            If Not TokenHasher.IsWellFormed(token) Then
                result = SessionAuthResult.Unknown
                Return Nothing
            End If
            Dim rec As SessionRecord = Nothing
            If Not _byHash.TryGetValue(TokenHasher.Hash(token), rec) Then
                result = SessionAuthResult.Unknown
                Return Nothing
            End If
            Dim now As DateTime = _clock.UtcNow
            If rec.IsRevoked OrElse now - rec.LastActivityUtc > _idle OrElse now >= rec.AbsoluteExpiryUtc Then
                Revoke(rec)
                result = SessionAuthResult.Expired
                Return Nothing
            End If
            result = SessionAuthResult.Ok
            Return rec
        End Function

        Public Sub Revoke(rec As SessionRecord)
            If rec Is Nothing Then Return
            rec.MarkRevoked()
            For Each kv In _byHash
                If kv.Value Is rec Then
                    Dim removed As SessionRecord = Nothing
                    _byHash.TryRemove(kv.Key, removed)
                End If
            Next
        End Sub

        Public Sub RevokeUser(userName As String)
            For Each r As SessionRecord In _byHash.Values.Where(Function(x) String.Equals(x.UserName, userName, StringComparison.OrdinalIgnoreCase)).ToList()
                Revoke(r)
            Next
        End Sub

        Public Sub RevokeAll()
            For Each r As SessionRecord In _byHash.Values.ToList()
                Revoke(r)
            Next
        End Sub

        Public Sub RevokeCreatedBefore(utc As DateTime)
            For Each r As SessionRecord In _byHash.Values.Where(Function(x) x.CreatedUtc <= utc).ToList()
                Revoke(r)
            Next
        End Sub

        Public Function ForUser(userName As String) As List(Of SessionRecord)
            Return _byHash.Values.Where(Function(r) String.Equals(r.UserName, userName, StringComparison.OrdinalIgnoreCase) AndAlso Not r.IsRevoked).
                OrderBy(Function(r) r.CreatedUtc).ToList()
        End Function

        ''' <summary>إبطال الجلسات المنتهية (استدعاء دوري).</summary>
        Public Function PurgeExpired() As Integer
            Dim now As DateTime = _clock.UtcNow
            Dim n As Integer = 0
            For Each r As SessionRecord In _byHash.Values.ToList()
                If r.IsRevoked OrElse now - r.LastActivityUtc > _idle OrElse now >= r.AbsoluteExpiryUtc Then
                    Revoke(r)
                    n += 1
                End If
            Next
            Return n
        End Function
    End Class

End Namespace
