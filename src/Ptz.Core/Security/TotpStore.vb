Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports Ptz.Core.Abstractions

Namespace Security

    ''' <summary>
    ''' مخزن أسرار TOTP: ملف أسرار (totp.json، تكتبه أداة الإدارة فقط) وملف حالة (totp-state.json، تكتبه الخدمة فقط)
    ''' كي لا تتزاحم عمليتان على ملف واحد. الأسرار مشفّرة بالخزنة ومرتبطة باسم المستخدم. الاستهلاك ذرّي تحت قفل واحد
    ''' فلا ينجح طلبان متوازيان بالرمز نفسه.
    ''' </summary>
    Public NotInheritable Class TotpStore
        Private NotInheritable Class SecretDto
            Public Property UserName As String = ""
            Public Property SecretProtected As String = ""
        End Class

        Private NotInheritable Class StateDto
            Public Property UserName As String = ""
            Public Property LastStep As Long
        End Class

        Private Shared ReadOnly DummySecret As Byte() = Encoding.ASCII.GetBytes("ptz-gateway-dummy-secret-0123456789")

        Private ReadOnly _secretsPath As String
        Private ReadOnly _statePath As String
        Private ReadOnly _vault As CredentialVault
        Private ReadOnly _clock As IClock
        Private ReadOnly _lock As New Object()
        Private ReadOnly _secrets As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _lastStep As New Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase)
        Private _stamp As String = ""

        Public Sub New(secretsPath As String, statePath As String, vault As CredentialVault, Optional clock As IClock = Nothing)
            _secretsPath = secretsPath
            _statePath = statePath
            _vault = vault
            _clock = If(clock, SystemClock.Instance)
            LoadSecrets()
            LoadState()
        End Sub

        Public Function IsEnrolled(userName As String) As Boolean
            SyncLock _lock
                Return userName IsNot Nothing AndAlso _secrets.ContainsKey(userName)
            End SyncLock
        End Function

        Public Function AnyEnrolled() As Boolean
            SyncLock _lock
                Return _secrets.Count > 0
            End SyncLock
        End Function

        ''' <summary>بصمة السر المخزّن (تتغير عند التسجيل/إعادة التعيين) لإبطال الجلسات القديمة. "" لغير المسجَّل.</summary>
        Public Function StampOf(userName As String) As String
            SyncLock _lock
                Dim p As String = Nothing
                If userName Is Nothing OrElse Not _secrets.TryGetValue(userName, p) Then Return ""
                Return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p)))
            End SyncLock
        End Function

        ''' <summary>تسجيل (أو إعادة تسجيل) مستخدم: يولّد سراً جديداً ويحفظه ويعيده مرة واحدة للعرض. أداة الإدارة فقط.</summary>
        Public Function Enroll(userName As String) As Byte()
            Dim secret As Byte() = Totp.NewSecret()
            SyncLock _lock
                _secrets(userName) = _vault.Protect(Totp.Base32(secret), "totp:" & userName.ToLowerInvariant())
                _lastStep.Remove(userName)
                SaveSecrets()
            End SyncLock
            Return secret
        End Function

        Public Sub Reset(userName As String)
            SyncLock _lock
                _secrets.Remove(userName)
                _lastStep.Remove(userName)
                SaveSecrets()
            End SyncLock
        End Sub

        ''' <summary>يتحقق من الرمز ويستهلكه ذرّياً. False لغير المسجَّل أو الرمز الخاطئ أو المستهلك.</summary>
        Public Function TryConsume(userName As String, code As String, Optional nowUtc As DateTime? = Nothing) As Boolean
            Dim now As DateTime = If(nowUtc, _clock.UtcNow)
            Dim unix As Long = New DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds()
            SyncLock _lock
                Dim protectedSecret As String = Nothing
                If userName Is Nothing OrElse Not _secrets.TryGetValue(userName, protectedSecret) Then
                    Totp.Verify(DummySecret, code, unix, -1) ' تكافؤ زمني
                    Return False
                End If
                Dim secret As Byte()
                Try
                    secret = Totp.FromBase32(_vault.Unprotect(protectedSecret, "totp:" & userName.ToLowerInvariant()))
                Catch ex As Exception When TypeOf ex Is CryptographicException OrElse TypeOf ex Is FormatException
                    Return False
                End Try
                Dim last As Long = -1
                _lastStep.TryGetValue(userName, last)
                Dim matched As Long = Totp.Verify(secret, code, unix, last)
                If matched < 0 Then Return False
                _lastStep(userName) = matched
                SaveState()
                Return True
            End SyncLock
        End Function

        ''' <summary>تحقق وهمي بسر ثابت دون استهلاك (لتكافؤ الزمن حين لا يوجد مستخدم/تسجيل).</summary>
        Public Sub VerifyDummy(code As String, Optional nowUtc As DateTime? = Nothing)
            Dim now As DateTime = If(nowUtc, _clock.UtcNow)
            Totp.Verify(DummySecret, code, New DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds(), -1)
        End Sub

        ''' <summary>يلتقط تغييرات أداة الإدارة (عملية مستقلة) بمقارنة بصمة الملف.</summary>
        Public Function ReloadIfChanged() As Boolean
            SyncLock _lock
                If FileStamp(_secretsPath) = _stamp Then Return False
                LoadSecrets()
                Return True
            End SyncLock
        End Function

        Private Shared Function FileStamp(path As String) As String
            If Not IO.File.Exists(path) Then Return ""
            Dim fi As New IO.FileInfo(path)
            Return fi.LastWriteTimeUtc.Ticks.ToString(Globalization.CultureInfo.InvariantCulture) & ":" & fi.Length.ToString(Globalization.CultureInfo.InvariantCulture)
        End Function

        Private Sub LoadSecrets()
            _secrets.Clear()
            _stamp = FileStamp(_secretsPath)
            If Not IO.File.Exists(_secretsPath) Then Return
            Dim list As List(Of SecretDto) = JsonSerializer.Deserialize(Of List(Of SecretDto))(IO.File.ReadAllText(_secretsPath, Encoding.UTF8))
            If list Is Nothing Then Return
            For Each d As SecretDto In list
                _secrets(d.UserName) = d.SecretProtected
            Next
        End Sub

        Private Sub LoadState()
            _lastStep.Clear()
            If Not IO.File.Exists(_statePath) Then Return
            Dim list As List(Of StateDto) = JsonSerializer.Deserialize(Of List(Of StateDto))(IO.File.ReadAllText(_statePath, Encoding.UTF8))
            If list Is Nothing Then Return
            For Each d As StateDto In list
                _lastStep(d.UserName) = d.LastStep
            Next
        End Sub

        Private Sub SaveSecrets()
            Dim dto As List(Of SecretDto) = _secrets.Select(Function(kv) New SecretDto With {.UserName = kv.Key, .SecretProtected = kv.Value}).ToList()
            AtomicWrite(_secretsPath, JsonSerializer.Serialize(dto))
            _stamp = FileStamp(_secretsPath)
        End Sub

        Private Sub SaveState()
            Dim dto As List(Of StateDto) = _lastStep.Select(Function(kv) New StateDto With {.UserName = kv.Key, .LastStep = kv.Value}).ToList()
            AtomicWrite(_statePath, JsonSerializer.Serialize(dto))
        End Sub

        Private Shared Sub AtomicWrite(path As String, content As String)
            Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(path))
            If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
            Dim tmp As String = path & ".tmp"
            IO.File.WriteAllText(tmp, content, Encoding.UTF8)
            If Not OperatingSystem.IsWindows() Then IO.File.SetUnixFileMode(tmp, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite)
            IO.File.Move(tmp, path, True)
        End Sub
    End Class

End Namespace
