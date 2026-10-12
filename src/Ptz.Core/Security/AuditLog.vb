Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json

Namespace Security

    Public NotInheritable Class AuditEntry
        Public Property Seq As Long
        Public Property TimeUtc As DateTime
        Public Property User As String = ""
        Public Property Action As String = ""
        Public Property Target As String = ""
        ''' <summary>OK / DENIED / FAIL</summary>
        Public Property Outcome As String = "OK"
        Public Property Details As String = ""
        Public Property PrevHash As String = ""
        Public Property Hash As String = ""
    End Class

    Public NotInheritable Class AuditVerifyResult
        Public ReadOnly Property IsValid As Boolean
        Public ReadOnly Property EntriesChecked As Long
        ''' <summary>رقم أول سجل معطوب (0 إن كانت السلسلة سليمة).</summary>
        Public ReadOnly Property FirstBadSeq As Long

        Public Sub New(isValid As Boolean, entriesChecked As Long, firstBadSeq As Long)
            Me.IsValid = isValid
            Me.EntriesChecked = entriesChecked
            Me.FirstBadSeq = firstBadSeq
        End Sub
    End Class

    Public Interface IAuditLog
        Sub Write(user As String, action As String, target As String, outcome As String, details As String)
    End Interface

    ''' <summary>سجل لا يفعل شيئاً (للاختبارات أو التعطيل الصريح).</summary>
    Public NotInheritable Class NullAuditLog
        Implements IAuditLog
        Public Shared ReadOnly Property Instance As New NullAuditLog()
        Public Sub Write(user As String, action As String, target As String, outcome As String, details As String) Implements IAuditLog.Write
        End Sub
    End Class

    ''' <summary>
    ''' سجل تدقيق إلحاقي بصيغة JSONL، كل سجل مرتبط بسابقه بـ HMAC-SHA256 (سلسلة تجزئة)
    ''' فيكشف التعديل أو الحذف من وسط السجل. آمن للاستخدام من عدة خيوط.
    ''' </summary>
    Public NotInheritable Class FileAuditLog
        Implements IAuditLog

        Private ReadOnly _path As String
        Private ReadOnly _key As Byte()
        Private ReadOnly _clock As Abstractions.IClock
        Private ReadOnly _lock As New Object()
        Private _lastHash As String = ""
        Private _seq As Long = 0

        Public Sub New(path As String, hmacKey As Byte(), Optional clock As Abstractions.IClock = Nothing)
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("مسار السجل مطلوب.", NameOf(path))
            If hmacKey Is Nothing OrElse hmacKey.Length < 16 Then Throw New ArgumentException("مفتاح HMAC قصير.", NameOf(hmacKey))
            _path = path
            _key = CType(hmacKey.Clone(), Byte())
            _clock = If(clock, Abstractions.SystemClock.Instance)
            Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(path))
            If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
            LoadTail()
        End Sub

        ''' <summary>آخر تجزئة — يُنصح بحفظها خارج الجهاز (Anchor) لكشف بتر نهاية السجل.</summary>
        Public ReadOnly Property LastHash As String
            Get
                SyncLock _lock
                    Return _lastHash
                End SyncLock
            End Get
        End Property

        Private Sub LoadTail()
            If Not IO.File.Exists(_path) Then Return
            For Each line As String In IO.File.ReadLines(_path, Encoding.UTF8)
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim e As AuditEntry = JsonSerializer.Deserialize(Of AuditEntry)(line)
                If e IsNot Nothing Then
                    _seq = e.Seq
                    _lastHash = e.Hash
                End If
            Next
        End Sub

        Private Function Compute(e As AuditEntry) As String
            Dim canonical As String = JsonSerializer.Serialize(New String() {
                e.Seq.ToString(Globalization.CultureInfo.InvariantCulture),
                e.TimeUtc.ToString("o", Globalization.CultureInfo.InvariantCulture),
                e.User, e.Action, e.Target, e.Outcome, e.Details, e.PrevHash})
            Using h As New HMACSHA256(_key)
                Return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            End Using
        End Function

        Public Sub Write(user As String, action As String, target As String, outcome As String, details As String) Implements IAuditLog.Write
            SyncLock _lock
                Dim e As New AuditEntry With {
                    .Seq = _seq + 1,
                    .TimeUtc = _clock.UtcNow,
                    .User = If(user, ""),
                    .Action = If(action, ""),
                    .Target = If(target, ""),
                    .Outcome = If(outcome, ""),
                    .Details = Truncate(If(details, ""), 1000),
                    .PrevHash = _lastHash
                }
                e.Hash = Compute(e)
                IO.File.AppendAllText(_path, JsonSerializer.Serialize(e) & Environment.NewLine, Encoding.UTF8)
                _seq = e.Seq
                _lastHash = e.Hash
            End SyncLock
        End Sub

        Private Shared Function Truncate(s As String, max As Integer) As String
            Return If(s.Length <= max, s, s.Substring(0, max))
        End Function

        ''' <summary>يتحقق من سلامة السلسلة كاملة.</summary>
        Public Function Verify() As AuditVerifyResult
            SyncLock _lock
                If Not IO.File.Exists(_path) Then Return New AuditVerifyResult(True, 0, 0)
                Dim prev As String = ""
                Dim expectedSeq As Long = 1
                Dim count As Long = 0
                For Each line As String In IO.File.ReadLines(_path, Encoding.UTF8)
                    If String.IsNullOrWhiteSpace(line) Then Continue For
                    Dim e As AuditEntry
                    Try
                        e = JsonSerializer.Deserialize(Of AuditEntry)(line)
                    Catch ex As JsonException
                        Return New AuditVerifyResult(False, count, expectedSeq)
                    End Try
                    If e Is Nothing OrElse e.Seq <> expectedSeq OrElse e.PrevHash <> prev OrElse
                       Not CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Compute(e)), Encoding.ASCII.GetBytes(If(e.Hash, ""))) Then
                        Return New AuditVerifyResult(False, count, expectedSeq)
                    End If
                    prev = e.Hash
                    expectedSeq += 1
                    count += 1
                Next
                Return New AuditVerifyResult(True, count, 0)
            End SyncLock
        End Function

        ''' <summary>آخر <paramref name="count"/> سجلات (الأحدث أولاً) للعرض.</summary>
        Public Function ReadRecent(count As Integer) As List(Of AuditEntry)
            Dim all As New List(Of AuditEntry)()
            SyncLock _lock
                If IO.File.Exists(_path) Then
                    For Each line As String In IO.File.ReadLines(_path, Encoding.UTF8)
                        If String.IsNullOrWhiteSpace(line) Then Continue For
                        Dim e As AuditEntry = JsonSerializer.Deserialize(Of AuditEntry)(line)
                        If e IsNot Nothing Then all.Add(e)
                    Next
                End If
            End SyncLock
            all.Reverse()
            If all.Count > count Then all.RemoveRange(count, all.Count - count)
            Return all
        End Function
    End Class

End Namespace
