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
        ''' <summary>أسطر غير مقروءة تُجوهلت (شظايا كتابة مبتورة). لا تُبطل السلسلة لكنها تُعرض للمراجعة.</summary>
        Public ReadOnly Property SkippedLines As Long

        Public Sub New(isValid As Boolean, entriesChecked As Long, firstBadSeq As Long, Optional skippedLines As Long = 0)
            Me.IsValid = isValid
            Me.EntriesChecked = entriesChecked
            Me.FirstBadSeq = firstBadSeq
            Me.SkippedLines = skippedLines
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
        Private _needsLeadingNewline As Boolean

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

        ''' <summary>
        ''' يستعيد آخر رقم وتجزئة صالحين. الأسطر التالفة (قطع كهرباء/امتلاء قرص تترك سطراً ناقصاً) تُتجاوز ولا توقف الإقلاع؛
        ''' سلامة السلسلة تحرسها فحوص Seq/PrevHash (حذف/تعديل سجل مُثبَّت يُكشف عند أول سجل لاحق)، لا قابلية قراءة كل سطر.
        ''' </summary>
        Private Sub LoadTail()
            If Not IO.File.Exists(_path) Then Return
            Dim info As New IO.FileInfo(_path)
            If info.Length = 0 Then Return
            ' آخر بايت ليس سطراً جديداً ⇒ الكتابة السابقة بُترت: نعزل الشظية في سطرها عند أول إلحاق
            Using fs As New IO.FileStream(_path, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite)
                fs.Seek(-1, IO.SeekOrigin.End)
                _needsLeadingNewline = (fs.ReadByte() <> 10)
            End Using
            Dim tail As List(Of AuditEntry) = ReadRecent(64)
            If tail.Count > 0 Then
                _seq = tail(0).Seq
                _lastHash = tail(0).Hash
                Return
            End If
            ' لا سجل صالح في الذيل (سجل ضخم كله تالف في نهايته): مسح كامل مرة واحدة
            For Each line As String In IO.File.ReadLines(_path, Encoding.UTF8)
                Dim e As AuditEntry = TryParse(line)
                If e IsNot Nothing Then
                    _seq = e.Seq
                    _lastHash = e.Hash
                End If
            Next
        End Sub

        Private Shared Function TryParse(line As String) As AuditEntry
            If String.IsNullOrWhiteSpace(line) Then Return Nothing
            Try
                Dim e As AuditEntry = JsonSerializer.Deserialize(Of AuditEntry)(line)
                If e Is Nothing OrElse e.Seq < 1 OrElse String.IsNullOrEmpty(e.Hash) Then Return Nothing
                Return e
            Catch ex As JsonException
                Return Nothing
            End Try
        End Function

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
                    .User = AuditSanitizer.Clean(user, 64),
                    .Action = AuditSanitizer.Clean(action, 64),
                    .Target = AuditSanitizer.Clean(target, 128),
                    .Outcome = AuditSanitizer.Clean(outcome, 16),
                    .Details = AuditSanitizer.Clean(details, 1000),
                    .PrevHash = _lastHash
                }
                e.Hash = Compute(e)
                Dim line As String = JsonSerializer.Serialize(e) & Environment.NewLine
                Try
                    IO.File.AppendAllText(_path, If(_needsLeadingNewline, Environment.NewLine, "") & line, Encoding.UTF8)
                    _needsLeadingNewline = False
                Catch ex As IO.IOException
                    _needsLeadingNewline = True      ' قد يكون أُلحق جزء من السطر: السطر التالي يبدأ بفاصل
                    Throw
                End Try
                _seq = e.Seq
                _lastHash = e.Hash
            End SyncLock
        End Sub

        Private Shared Function Truncate(s As String, max As Integer) As String
            Return If(s.Length <= max, s, s.Substring(0, max))
        End Function

        ''' <summary>
        ''' يتحقق من سلامة السلسلة كاملة بقراءة متدفقة (بلا حدّ 2GB ولا حجز للكتّاب؛ السجل ملحَق فقط). الأسطر غير المقروءة (شظايا
        ''' كتابة مبتورة) تُحصى في <see cref="AuditVerifyResult.SkippedLines"/> ولا تُبطل السلسلة؛ أما حذف/تعديل/إعادة ترتيب سجل
        ''' فيُكشف دائماً عبر Seq وPrevHash والـ HMAC في أول سجل صالح يليه.
        ''' </summary>
        Public Function Verify() As AuditVerifyResult
            If Not IO.File.Exists(_path) Then Return New AuditVerifyResult(True, 0, 0)
            ' الطول يُلتقط تحت قفل الكتابة فيقع على حدّ سطر؛ نقرأ حتى هذا الحدّ فقط. القراءة حتى نهاية ملف ينمو قد تقسم سطراً
            ' قيد الكتابة إلى نصفين تالفين فيضيع سجل صالح ويبدو السجل معطوباً.
            Dim limit As Long
            SyncLock _lock
                limit = New IO.FileInfo(_path).Length
            End SyncLock
            Dim prev As String = ""
            Dim expectedSeq As Long = 1
            Dim count As Long = 0
            Dim skipped As Long = 0
            Using fs As New IO.FileStream(_path, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite, 65536, IO.FileOptions.SequentialScan)
                Using sr As New IO.StreamReader(New PrefixStream(fs, limit), Encoding.UTF8)
                    Dim line As String = sr.ReadLine()
                    While line IsNot Nothing
                        If Not String.IsNullOrWhiteSpace(line) Then
                            Dim e As AuditEntry = TryParse(line)
                            If e Is Nothing Then
                                skipped += 1
                            Else
                                If e.Seq <> expectedSeq OrElse e.PrevHash <> prev OrElse
                                   Not CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Compute(e)), Encoding.ASCII.GetBytes(e.Hash)) Then
                                    Return New AuditVerifyResult(False, count, expectedSeq, skipped)
                                End If
                                prev = e.Hash
                                expectedSeq += 1
                                count += 1
                            End If
                        End If
                        line = sr.ReadLine()
                    End While
                End Using
            End Using
            Return New AuditVerifyResult(True, count, 0, skipped)
        End Function

        ''' <summary>
        ''' آخر <paramref name="count"/> سجلات صالحة (الأحدث أولاً) بقراءة عكسية من ذيل الملف خارج قفل الكتابة: كلفتها تتناسب مع
        ''' العدد المطلوب لا مع حجم السجل (فيض السجل لا يعطّل التدقيق ولا يستهلك الذاكرة).
        ''' </summary>
        Public Function ReadRecent(count As Integer) As List(Of AuditEntry)
            Dim result As New List(Of AuditEntry)()
            If count < 1 OrElse Not IO.File.Exists(_path) Then Return result
            Using fs As New IO.FileStream(_path, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite)
                Dim pos As Long = fs.Length
                Dim start As Long = 0
                Dim newlines As Integer = 0
                Dim buf(65535) As Byte
                Dim found As Boolean = False
                While pos > 0 AndAlso Not found
                    Dim n As Integer = CInt(Math.Min(buf.Length, pos))
                    pos -= n
                    fs.Seek(pos, IO.SeekOrigin.Begin)
                    Dim read As Integer = 0
                    While read < n
                        Dim k As Integer = fs.Read(buf, read, n - read)
                        If k = 0 Then Exit While
                        read += k
                    End While
                    For i As Integer = read - 1 To 0 Step -1
                        If buf(i) = 10 Then
                            newlines += 1
                            If newlines > count * 2 + 1 Then          ' هامش للأسطر التالفة
                                start = pos + i + 1
                                found = True
                                Exit For
                            End If
                        End If
                    Next
                End While
                fs.Seek(start, IO.SeekOrigin.Begin)
                Using sr As New IO.StreamReader(fs, Encoding.UTF8, False, 65536, True)
                    Dim line As String = sr.ReadLine()
                    While line IsNot Nothing
                        Dim e As AuditEntry = TryParse(line)
                        If e IsNot Nothing Then result.Add(e)
                        line = sr.ReadLine()
                    End While
                End Using
            End Using
            result.Reverse()
            If result.Count > count Then result.RemoveRange(count, result.Count - count)
            Return result
        End Function
    End Class

    ''' <summary>يعرض أول <c>limit</c> بايت فقط من مجرى (قراءة آمنة لملف ملحَق فقط يُكتب أثناء القراءة).</summary>
    Friend NotInheritable Class PrefixStream
        Inherits IO.Stream

        Private ReadOnly _inner As IO.Stream
        Private _remaining As Long

        Public Sub New(inner As IO.Stream, limit As Long)
            _inner = inner
            _remaining = limit
        End Sub

        Public Overrides ReadOnly Property CanRead As Boolean = True
        Public Overrides ReadOnly Property CanSeek As Boolean = False
        Public Overrides ReadOnly Property CanWrite As Boolean = False
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException()
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property

        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            If _remaining <= 0 Then Return 0
            Dim n As Integer = _inner.Read(buffer, offset, CInt(Math.Min(count, _remaining)))
            _remaining -= n
            Return n
        End Function

        Public Overrides Sub Flush()
        End Sub

        Public Overrides Function Seek(offset As Long, origin As IO.SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function

        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
    End Class

End Namespace
