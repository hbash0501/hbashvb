Imports Ptz.Core.Abstractions

Namespace Security

    ''' <summary>
    ''' تقييد محاولات الدخول عن بُعد في الذاكرة (لا يلمس UserStore فلا يمكن لمهاجم الإنترنت قفل المدير محلياً ولا
    ''' يسبب كتابة قرص لكل محاولة). (1) لكل (مستخدم،IP): بعد 3 إخفاقات خلال 10 د ينتظر المحاول min(60, 2^(n-3)) ث؛
    ''' (2) لكل مستخدم عبر كل العناوين: 20 إخفاقاً خلال 60 د ⇒ قفل دخول بعيد 30 د. الذاكرة محدودة بإخلاء الأقدم.
    ''' </summary>
    Public NotInheritable Class LoginThrottle
        Private NotInheritable Class Window
            Public ReadOnly Stamps As New List(Of DateTime)()
            Public LockedUntilUtc As DateTime = DateTime.MinValue
            Public LastTouchUtc As DateTime
        End Class

        Public Shared ReadOnly PairWindow As TimeSpan = TimeSpan.FromMinutes(10)
        Public Shared ReadOnly UserWindow As TimeSpan = TimeSpan.FromMinutes(60)
        Public Shared ReadOnly UserLockDuration As TimeSpan = TimeSpan.FromMinutes(30)
        Public Const UserFailureLimit As Integer = 20

        Private ReadOnly _clock As IClock
        Private ReadOnly _maxKeys As Integer
        Private ReadOnly _lock As New Object()
        Private ReadOnly _pairs As New Dictionary(Of String, Window)()
        Private ReadOnly _users As New Dictionary(Of String, Window)(StringComparer.OrdinalIgnoreCase)

        Public Sub New(Optional clock As IClock = Nothing, Optional maxKeys As Integer = 10000)
            _clock = If(clock, SystemClock.Instance)
            _maxKeys = maxKeys
        End Sub

        ''' <summary>اسم موحّد للمفتاح: الأسماء غير الصالحة (Unicode/طويلة) تُجمَّع في مفتاح واحد كي لا يلوّث المهاجم الذاكرة.</summary>
        Private Shared Function NormalizeUser(user As String) As String
            If String.IsNullOrEmpty(user) OrElse user.Length > 64 Then Return "~invalid"
            For Each c As Char In user
                If Not (Char.IsAsciiLetterOrDigit(c) OrElse c = "_"c OrElse c = "."c OrElse c = "-"c) Then Return "~invalid"
            Next
            Return user.ToLowerInvariant()
        End Function

        Private Shared Function PairKey(ip As String, user As String) As String
            Return NormalizeUser(user) & "|" & ip
        End Function

        Private Shared Sub Prune(w As Window, now As DateTime, span As TimeSpan)
            w.Stamps.RemoveAll(Function(t) now - t > span)
        End Sub

        ''' <summary>المدة المتبقية للانتظار قبل السماح بمحاولة جديدة لهذا الزوج، أو Nothing إن لم يوجد انتظار.</summary>
        Public Function Precheck(ip As String, userName As String) As TimeSpan?
            Dim now As DateTime = _clock.UtcNow
            SyncLock _lock
                Dim w As Window = Nothing
                If Not _pairs.TryGetValue(PairKey(ip, userName), w) Then Return Nothing
                Prune(w, now, PairWindow)
                Dim n As Integer = w.Stamps.Count
                If n < 3 Then Return Nothing
                Dim waitSeconds As Double = Math.Min(60.0, Math.Pow(2, n - 3))
                Dim until As DateTime = w.Stamps(w.Stamps.Count - 1).AddSeconds(waitSeconds)
                If until > now Then Return until - now
                Return Nothing
            End SyncLock
        End Function

        ''' <summary>
        ''' يسجّل إخفاقاً. دائماً يغذّي تراجع (مستخدم،IP). القفل على مستوى المستخدم يتغذى <b>فقط</b> من إخفاقات مرحلة TOTP
        ''' (كلمة المرور صحيحة والرمز خاطئ)، فلا يستطيع من يجهل كلمة المرور قفل مستخدم معروف. لا يُمدَّد قفل قائم.
        ''' </summary>
        Public Sub RecordFailure(ip As String, userName As String, Optional totpStage As Boolean = False)
            Dim now As DateTime = _clock.UtcNow
            SyncLock _lock
                Dim pw As Window = GetWindow(_pairs, PairKey(ip, userName), now)
                Prune(pw, now, PairWindow)
                pw.Stamps.Add(now)
                If Not totpStage Then Return
                Dim uw As Window = GetWindow(_users, NormalizeUser(userName), now)
                If uw.LockedUntilUtc > now Then Return
                Prune(uw, now, UserWindow)
                uw.Stamps.Add(now)
                If uw.Stamps.Count >= UserFailureLimit Then
                    uw.LockedUntilUtc = now + UserLockDuration
                    uw.Stamps.Clear()
                End If
            End SyncLock
        End Sub

        Public Sub RecordSuccess(ip As String, userName As String)
            SyncLock _lock
                _pairs.Remove(PairKey(ip, userName))
            End SyncLock
        End Sub

        Public Function IsRemoteLocked(userName As String) As Boolean
            Dim now As DateTime = _clock.UtcNow
            SyncLock _lock
                Dim w As Window = Nothing
                Return _users.TryGetValue(NormalizeUser(userName), w) AndAlso w.LockedUntilUtc > now
            End SyncLock
        End Function

        ''' <summary>يفك القفل البعيد يدوياً (أمر unlock في أداة الإدارة).</summary>
        Public Sub Unlock(userName As String)
            SyncLock _lock
                _users.Remove(NormalizeUser(userName))
            End SyncLock
        End Sub

        Private Function GetWindow(Of TKey)(dict As Dictionary(Of TKey, Window), key As TKey, now As DateTime) As Window
            Dim w As Window = Nothing
            If Not dict.TryGetValue(key, w) Then
                If dict.Count >= _maxKeys Then EvictOldest(dict)
                w = New Window()
                dict(key) = w
            End If
            w.LastTouchUtc = now
            Return w
        End Function

        Private Shared Sub EvictOldest(Of TKey)(dict As Dictionary(Of TKey, Window))
            Dim oldestKey As TKey = Nothing
            Dim oldest As DateTime = DateTime.MaxValue
            For Each kv In dict
                If kv.Value.LastTouchUtc < oldest Then
                    oldest = kv.Value.LastTouchUtc
                    oldestKey = kv.Key
                End If
            Next
            If oldest <> DateTime.MaxValue Then dict.Remove(oldestKey)
        End Sub
    End Class

End Namespace
