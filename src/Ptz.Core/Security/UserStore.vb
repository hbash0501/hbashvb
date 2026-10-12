Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json

Namespace Security

    Public NotInheritable Class UserRecord
        Public Property UserName As String = ""
        Public Property Role As Role = Role.Viewer
        Public Property Salt As String = ""
        Public Property Hash As String = ""
        Public Property Iterations As Integer
        Public Property FailedCount As Integer
        Public Property LockedUntilUtc As DateTime
        Public Property Disabled As Boolean
        Public Property CreatedUtc As DateTime
    End Class

    ''' <summary>
    ''' مخزن المستخدمين: PBKDF2-SHA256 بملح فريد، سياسة كلمات مرور، قفل بعد محاولات فاشلة،
    ''' مقارنة بزمن ثابت، حماية آخر مدير، وكتابة ذرّية للملف. لا توجد كلمات مرور افتراضية.
    ''' </summary>
    Public NotInheritable Class UserStore
        Public Const Pbkdf2Iterations As Integer = 210000
        Public Const MaxFailedAttempts As Integer = 5
        Public Shared ReadOnly LockDuration As TimeSpan = TimeSpan.FromMinutes(15)

        Private ReadOnly _path As String
        Private ReadOnly _audit As IAuditLog
        Private ReadOnly _clock As Abstractions.IClock
        Private ReadOnly _lock As New Object()
        Private ReadOnly _users As New Dictionary(Of String, UserRecord)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _iterations As Integer

        Public Sub New(path As String, Optional audit As IAuditLog = Nothing, Optional clock As Abstractions.IClock = Nothing,
                       Optional iterations As Integer = Pbkdf2Iterations)
            _path = path
            _audit = If(audit, NullAuditLog.Instance)
            _clock = If(clock, Abstractions.SystemClock.Instance)
            _iterations = iterations
            Load()
        End Sub

        Public ReadOnly Property HasAnyUser As Boolean
            Get
                SyncLock _lock
                    Return _users.Count > 0
                End SyncLock
            End Get
        End Property

        Public Function ListUsers() As List(Of UserRecord)
            SyncLock _lock
                Return _users.Values.Select(Function(u) Clone(u)).OrderBy(Function(u) u.UserName, StringComparer.OrdinalIgnoreCase).ToList()
            End SyncLock
        End Function

        ''' <summary>يرجع سبب الرفض أو Nothing إن كانت كلمة المرور مقبولة.</summary>
        Public Shared Function ValidatePasswordPolicy(userName As String, password As String) As String
            If password Is Nothing OrElse password.Length < 12 Then Return "كلمة المرور يجب ألا تقل عن 12 حرفاً."
            If password.Length > 128 Then Return "كلمة المرور أطول من المسموح (128)."
            If Not password.Any(AddressOf Char.IsUpper) Then Return "يلزم حرف كبير واحد على الأقل."
            If Not password.Any(AddressOf Char.IsLower) Then Return "يلزم حرف صغير واحد على الأقل."
            If Not password.Any(AddressOf Char.IsDigit) Then Return "يلزم رقم واحد على الأقل."
            If Not password.Any(Function(c) Not Char.IsLetterOrDigit(c)) Then Return "يلزم رمز خاص واحد على الأقل."
            If Not String.IsNullOrEmpty(userName) AndAlso password.IndexOf(userName, StringComparison.OrdinalIgnoreCase) >= 0 Then
                Return "كلمة المرور لا يجوز أن تحوي اسم المستخدم."
            End If
            Return Nothing
        End Function

        Private Shared Function ValidateUserName(userName As String) As String
            If String.IsNullOrWhiteSpace(userName) OrElse userName.Length < 3 OrElse userName.Length > 32 Then Return "اسم المستخدم من 3 إلى 32 حرفاً."
            If Not userName.All(Function(c) Char.IsLetterOrDigit(c) OrElse c = "_"c OrElse c = "."c OrElse c = "-"c) Then
                Return "اسم المستخدم يقبل حروفاً وأرقاماً و . _ - فقط."
            End If
            Return Nothing
        End Function

        Public Sub CreateUser(actor As String, userName As String, password As String, role As Role)
            Dim err As String = If(ValidateUserName(userName), ValidatePasswordPolicy(userName, password))
            If err IsNot Nothing Then Throw New ArgumentException(err)
            SyncLock _lock
                If _users.ContainsKey(userName) Then Throw New InvalidOperationException("المستخدم موجود مسبقاً.")
                Dim rec As New UserRecord With {.UserName = userName, .Role = role, .CreatedUtc = _clock.UtcNow}
                SetPassword(rec, password)
                _users(userName) = rec
                Save()
            End SyncLock
            _audit.Write(actor, "User.Create", userName, "OK", $"role={role}")
        End Sub

        Public Sub ChangePassword(actor As String, userName As String, newPassword As String)
            Dim err As String = ValidatePasswordPolicy(userName, newPassword)
            If err IsNot Nothing Then Throw New ArgumentException(err)
            SyncLock _lock
                Dim rec As UserRecord = Find(userName)
                SetPassword(rec, newPassword)
                rec.FailedCount = 0
                rec.LockedUntilUtc = DateTime.MinValue
                Save()
            End SyncLock
            _audit.Write(actor, "User.ChangePassword", userName, "OK", "")
        End Sub

        Public Sub SetRole(actor As String, userName As String, role As Role)
            SyncLock _lock
                Dim rec As UserRecord = Find(userName)
                If rec.Role = Role.Administrator AndAlso role <> Role.Administrator AndAlso ActiveAdminCount() <= 1 AndAlso Not rec.Disabled Then
                    Throw New InvalidOperationException("لا يمكن خفض صلاحية آخر مدير.")
                End If
                rec.Role = role
                Save()
            End SyncLock
            _audit.Write(actor, "User.SetRole", userName, "OK", $"role={role}")
        End Sub

        Public Sub SetDisabled(actor As String, userName As String, disabled As Boolean)
            SyncLock _lock
                Dim rec As UserRecord = Find(userName)
                If disabled AndAlso rec.Role = Role.Administrator AndAlso Not rec.Disabled AndAlso ActiveAdminCount() <= 1 Then
                    Throw New InvalidOperationException("لا يمكن تعطيل آخر مدير.")
                End If
                rec.Disabled = disabled
                Save()
            End SyncLock
            _audit.Write(actor, If(disabled, "User.Disable", "User.Enable"), userName, "OK", "")
        End Sub

        ''' <summary>مصادقة. رسالة الفشل موحّدة لمنع تعداد أسماء المستخدمين.</summary>
        Public Function Authenticate(userName As String, password As String) As UserSession
            Const generic As String = "اسم المستخدم أو كلمة المرور غير صحيحة، أو الحساب مقفل."
            Dim now As DateTime = _clock.UtcNow
            Dim rec As UserRecord = Nothing
            Dim salt As Byte()
            Dim expected As Byte()
            Dim iterations As Integer
            SyncLock _lock
                If userName IsNot Nothing Then _users.TryGetValue(userName, rec)
                If rec IsNot Nothing Then
                    salt = Convert.FromBase64String(rec.Salt)
                    expected = Convert.FromBase64String(rec.Hash)
                    iterations = rec.Iterations
                Else
                    salt = New Byte(15) {}
                    expected = New Byte(31) {}
                    iterations = _iterations
                End If
            End SyncLock

            ' نحسب التجزئة دائماً (حتى للمستخدم غير الموجود) لتقليل تسرّب التوقيت.
            Dim actual As Byte() = Hash(If(password, ""), salt, iterations)
            Dim match As Boolean = CryptographicOperations.FixedTimeEquals(actual, expected)

            SyncLock _lock
                If rec Is Nothing OrElse rec.Disabled OrElse rec.LockedUntilUtc > now Then
                    _audit.Write(If(userName, ""), "Auth.Login", "", "DENIED", "unknown/disabled/locked")
                    Throw New AuthenticationException(generic)
                End If
                If Not match Then
                    rec.FailedCount += 1
                    If rec.FailedCount >= MaxFailedAttempts Then
                        rec.LockedUntilUtc = now + LockDuration
                        rec.FailedCount = 0
                        _audit.Write(userName, "Auth.Lockout", "", "DENIED", $"locked for {LockDuration.TotalMinutes:F0} min")
                    End If
                    Save()
                    _audit.Write(userName, "Auth.Login", "", "DENIED", "bad credentials")
                    Throw New AuthenticationException(generic)
                End If
                rec.FailedCount = 0
                Save()
                _audit.Write(userName, "Auth.Login", "", "OK", $"role={rec.Role}")
                Return New UserSession(rec.UserName, rec.Role, now)
            End SyncLock
        End Function

        ' ---------- داخلي ----------
        Private Function Find(userName As String) As UserRecord
            Dim rec As UserRecord = Nothing
            If userName Is Nothing OrElse Not _users.TryGetValue(userName, rec) Then Throw New KeyNotFoundException("المستخدم غير موجود.")
            Return rec
        End Function

        Private Function ActiveAdminCount() As Integer
            Return _users.Values.Where(Function(u) u.Role = Role.Administrator AndAlso Not u.Disabled).Count()
        End Function

        Private Sub SetPassword(rec As UserRecord, password As String)
            Dim salt As Byte() = RandomNumberGenerator.GetBytes(16)
            rec.Iterations = _iterations
            rec.Salt = Convert.ToBase64String(salt)
            rec.Hash = Convert.ToBase64String(Hash(password, salt, _iterations))
        End Sub

        Private Shared Function Hash(password As String, salt As Byte(), iterations As Integer) As Byte()
            Return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32)
        End Function

        Private Shared Function Clone(u As UserRecord) As UserRecord
            Return New UserRecord With {.UserName = u.UserName, .Role = u.Role, .Disabled = u.Disabled, .CreatedUtc = u.CreatedUtc,
                                        .LockedUntilUtc = u.LockedUntilUtc, .FailedCount = u.FailedCount}
        End Function

        Private Sub Load()
            If Not IO.File.Exists(_path) Then Return
            Dim list As List(Of UserRecord) = JsonSerializer.Deserialize(Of List(Of UserRecord))(IO.File.ReadAllText(_path, Encoding.UTF8))
            If list Is Nothing Then Return
            For Each u As UserRecord In list
                _users(u.UserName) = u
            Next
        End Sub

        Private Sub Save()
            Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(_path))
            If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
            Dim tmp As String = _path & ".tmp"
            IO.File.WriteAllText(tmp, JsonSerializer.Serialize(_users.Values.ToList()), Encoding.UTF8)
            If Not OperatingSystem.IsWindows() Then IO.File.SetUnixFileMode(tmp, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite)
            IO.File.Move(tmp, _path, True)
        End Sub
    End Class

End Namespace
