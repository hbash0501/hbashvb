Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks

' طبقة الأمان المؤسسية: مفاتيح API مشفرة بحساب ويندوز، فحص البصمة الرقمية،
' وتشفير نسخة محمية من المستندات الحساسة باستخدام AES-256-CBC مع HMAC-SHA256.
Public Class DocumentSecurityService
    Private ReadOnly _settings As AppSettings
    Private Shared ReadOnly HeaderBytes As Byte() = Encoding.ASCII.GetBytes("AIDOCSEC1")

    Public Sub New(settings As AppSettings)
        _settings = If(settings, AppSettings.DefaultSettings())
        Directory.CreateDirectory(SecurityFolder())
        Directory.CreateDirectory(KeyVaultFolder())
        Directory.CreateDirectory(ProtectedDocumentsFolder())
    End Sub

    Public Function SecurityFolder() As String
        Dim baseFolder = If(String.IsNullOrWhiteSpace(_settings.BaseDataFolder), AppSettings.DefaultSettings().BaseDataFolder, _settings.BaseDataFolder)
        Return System.IO.Path.Combine(baseFolder, "Security")
    End Function

    Public Function KeyVaultFolder() As String
        Return System.IO.Path.Combine(SecurityFolder(), "ApiKeys")
    End Function

    Public Function ProtectedDocumentsFolder() As String
        Return System.IO.Path.Combine(SecurityFolder(), "ProtectedDocuments")
    End Function

    Public Sub SaveProviderApiKey(providerName As String, apiKey As String)
        Dim provider = NormalizeProvider(providerName)
        If String.IsNullOrWhiteSpace(apiKey) Then Return
        Dim plain = Encoding.UTF8.GetBytes(apiKey.Trim())
        Dim protectedBytes = ProtectedData.Protect(plain, Entropy(provider), DataProtectionScope.CurrentUser)
        WriteAllBytesAtomic(KeyPath(provider), protectedBytes)
        Array.Clear(plain, 0, plain.Length)
    End Sub

    Public Function GetProviderApiKey(providerName As String) As String
        Dim provider = NormalizeProvider(providerName)
        Dim path = KeyPath(provider)
        If Not File.Exists(path) Then Return ""
        Try
            Dim protectedBytes = File.ReadAllBytes(path)
            Dim plain = ProtectedData.Unprotect(protectedBytes, Entropy(provider), DataProtectionScope.CurrentUser)
            Dim value = Encoding.UTF8.GetString(plain)
            Array.Clear(plain, 0, plain.Length)
            Return value
        Catch
            Return ""
        End Try
    End Function

    Public Function HasProviderApiKey(providerName As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(GetProviderApiKey(providerName))
    End Function

    Public Sub DeleteProviderApiKey(providerName As String)
        Dim path = KeyPath(NormalizeProvider(providerName))
        If File.Exists(path) Then File.Delete(path)
    End Sub

    Public Sub MigrateLegacyPlainTextKeys(settings As AppSettings)
        If settings Is Nothing Then Return
        Dim legacy = If(settings.CloudAiApiKey, "").Trim()
        If legacy = "" Then legacy = If(settings.OpenAiApiKey, "").Trim()
        If legacy <> "" Then
            SaveProviderApiKey(settings.AiProvider, legacy)
            settings.CloudAiApiKey = ""
            settings.OpenAiApiKey = ""
            settings.Save()
        End If
    End Sub

    Public Shared Function ResolveProviderApiKey(settings As AppSettings, providerName As String, environmentName As String) As String
        If settings Is Nothing Then Return ""
        Dim direct = If(settings.CloudAiApiKey, "").Trim()
        If direct = "" Then direct = If(settings.OpenAiApiKey, "").Trim()
        If direct <> "" Then Return direct

        Try
            Dim security As New DocumentSecurityService(settings)
            Dim secureValue = security.GetProviderApiKey(providerName)
            If secureValue <> "" Then Return secureValue
        Catch
        End Try

        Dim envValue = If(Environment.GetEnvironmentVariable(environmentName), "").Trim()
        Return envValue
    End Function

    Public Function ValidateFileSignature(filePath As String) As FileSignatureValidationResult
        Dim result As New FileSignatureValidationResult With {
            .FilePath = filePath,
            .Extension = Path.GetExtension(If(filePath, "")).ToLowerInvariant(),
            .DetectedType = "غير معروف",
            .IsValid = False,
            .Message = "تعذر التحقق من الملف."
        }
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then
            result.Message = "الملف غير موجود."
            Return result
        End If

        Try
            Dim ext = result.Extension
            Dim buffer = ReadPrefix(filePath, 4096)

            If ext = ".pdf" Then Return MatchResult(result, StartsWith(buffer, New Byte() {&H25, &H50, &H44, &H46}), "PDF", "بصمة PDF")
            If ext = ".png" Then Return MatchResult(result, StartsWith(buffer, New Byte() {&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}), "PNG", "بصمة PNG")
            If ext = ".jpg" OrElse ext = ".jpeg" Then Return MatchResult(result, StartsWith(buffer, New Byte() {&HFF, &HD8, &HFF}), "JPEG", "بصمة JPEG")
            If ext = ".bmp" Then Return MatchResult(result, StartsWith(buffer, Encoding.ASCII.GetBytes("BM")), "BMP", "بصمة BMP")
            If ext = ".tif" OrElse ext = ".tiff" Then
                Dim validTiff = StartsWith(buffer, New Byte() {&H49, &H49, &H2A, &H0}) OrElse StartsWith(buffer, New Byte() {&H4D, &H4D, &H0, &H2A})
                Return MatchResult(result, validTiff, "TIFF", "بصمة TIFF")
            End If
            If ext = ".docx" OrElse ext = ".xlsx" OrElse ext = ".pptx" Then
                Dim validOffice = IsOpenXmlPackage(filePath)
                Return MatchResult(result, validOffice, "Office Open XML", "حاوية Office Open XML")
            End If
            If ext = ".doc" OrElse ext = ".xls" OrElse ext = ".ppt" Then
                Return MatchResult(result, StartsWith(buffer, New Byte() {&HD0, &HCF, &H11, &HE0, &HA1, &HB1, &H1A, &HE1}), "Microsoft Compound File", "بصمة Office القديمة")
            End If
            If ext = ".zip" Then Return MatchResult(result, StartsWith(buffer, New Byte() {&H50, &H4B}), "ZIP", "بصمة ZIP")
            If ext = ".rtf" Then Return MatchResult(result, Encoding.ASCII.GetString(buffer).TrimStart().StartsWith("{\rtf", StringComparison.OrdinalIgnoreCase), "RTF", "بصمة RTF")
            If IsTextExtension(ext) Then
                Dim textValid = Not ContainsNullByte(buffer)
                Return MatchResult(result, textValid, "Text", "ملف نصي")
            End If

            result.Message = "نوع الملف غير مدعوم في سياسة الاستيراد الآمن: " & ext
            Return result
        Catch ex As Exception
            result.Message = "فشل فحص بصمة الملف: " & ex.Message
            Return result
        End Try
    End Function

    Public Function CalculateRiskScore(documentRecord As DocumentRecord) As Double
        If documentRecord Is Nothing Then Return 0.0
        Dim score As Double = 0.1
        Dim confidentiality = If(documentRecord.Confidentiality, "")
        Dim priority = If(documentRecord.Priority, "")
        If confidentiality.Contains("سري للغاية") Then
            score += 0.55
        ElseIf confidentiality.Contains("سري") Then
            score += 0.4
        End If
        If priority.Contains("عاجل") Then score += 0.12
        If HasText(documentRecord.OcrNationalIds) OrElse HasText(documentRecord.OcrIqamaNumbers) OrElse HasText(documentRecord.OcrPassportNumbers) Then score += 0.2
        If HasText(documentRecord.OcrAmounts) Then score += 0.08
        If ContainsAny(documentRecord.GeneralCategory, "موارد بشرية", "مالي", "عقود", "سري") Then score += 0.08
        If Not documentRecord.FileSignatureVerified Then score += 0.1
        Return Math.Max(0.0, Math.Min(1.0, score))
    End Function

    Public Function ProtectFile(inputFilePath As String, documentId As String) As String
        Return ProtectFileAsync(inputFilePath, documentId, CancellationToken.None).GetAwaiter().GetResult()
    End Function

    Public Async Function ProtectFileAsync(inputFilePath As String, documentId As String, token As CancellationToken) As Task(Of String)
        If String.IsNullOrWhiteSpace(inputFilePath) OrElse Not File.Exists(inputFilePath) Then Throw New FileNotFoundException("الملف المراد حمايته غير موجود.", inputFilePath)
        Dim outputPath = System.IO.Path.Combine(ProtectedDocumentsFolder(), If(String.IsNullOrWhiteSpace(documentId), Guid.NewGuid().ToString("N"), documentId) & ".aidoc")
        Dim tempPath = outputPath & ".tmp"
        Dim salt(15) As Byte
        Dim iv(15) As Byte
        Dim master As Byte() = Nothing
        Dim encKey As Byte() = Nothing
        Dim macKey As Byte() = Nothing
        Try
            Using rng = RandomNumberGenerator.Create()
                rng.GetBytes(salt)
                rng.GetBytes(iv)
            End Using
            master = GetOrCreateMasterKey()
            encKey = DeriveKey(master, salt, "ENC")
            macKey = DeriveKey(master, salt, "MAC")

            Using output As New FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, True)
                Await output.WriteAsync(HeaderBytes, 0, HeaderBytes.Length, token)
                Await output.WriteAsync(salt, 0, salt.Length, token)
                Await output.WriteAsync(iv, 0, iv.Length, token)
                Using aes = System.Security.Cryptography.Aes.Create()
                    aes.KeySize = 256
                    aes.BlockSize = 128
                    aes.Mode = CipherMode.CBC
                    aes.Padding = PaddingMode.PKCS7
                    aes.Key = encKey
                    aes.IV = iv
                    Using crypto As New CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, True)
                        Using input As New FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, True)
                            Await CopyStreamAsync(input, crypto, token)
                        End Using
                        crypto.FlushFinalBlock()
                    End Using
                End Using
                Await output.FlushAsync(token)
            End Using

            Dim mac = ComputeFileHmac(tempPath, macKey)
            Using appendStream As New FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None)
                appendStream.Write(mac, 0, mac.Length)
            End Using
            If File.Exists(outputPath) Then File.Delete(outputPath)
            File.Move(tempPath, outputPath)
            Return outputPath
        Catch
            If File.Exists(tempPath) Then
                Try
                    File.Delete(tempPath)
                Catch
                End Try
            End If
            Throw
        Finally
            ClearBytes(master, encKey, macKey)
        End Try
    End Function

    Public Async Function RestoreProtectedFileAsync(protectedFilePath As String, outputFilePath As String, token As CancellationToken) As Task
        If Not File.Exists(protectedFilePath) Then Throw New FileNotFoundException("النسخة المحمية غير موجودة.", protectedFilePath)
        Dim totalLength = New FileInfo(protectedFilePath).Length
        Dim prefixLength = HeaderBytes.Length + 16 + 16
        If totalLength <= prefixLength + 32 Then Throw New InvalidDataException("ملف الحماية غير مكتمل.")

        Dim master As Byte() = Nothing
        Dim encKey As Byte() = Nothing
        Dim macKey As Byte() = Nothing
        Try
            master = GetOrCreateMasterKey()
            Dim salt(15) As Byte
            Dim iv(15) As Byte
            Dim storedMac(31) As Byte
            Using fs As New FileStream(protectedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Dim header(HeaderBytes.Length - 1) As Byte
                ReadExact(fs, header)
                If Not FixedEquals(header, HeaderBytes) Then Throw New InvalidDataException("ترويسة ملف الحماية غير صحيحة.")
                ReadExact(fs, salt)
                ReadExact(fs, iv)
                fs.Position = totalLength - 32
                ReadExact(fs, storedMac)
            End Using

            encKey = DeriveKey(master, salt, "ENC")
            macKey = DeriveKey(master, salt, "MAC")
            Dim expectedMac = ComputeFileHmac(protectedFilePath, macKey, totalLength - 32)
            If Not FixedEquals(storedMac, expectedMac) Then Throw New CryptographicException("فشل التحقق من سلامة الملف المحمي؛ قد يكون تالفاً أو معدلاً.")

            Dim cipherLength = totalLength - prefixLength - 32
            Using input As New FileStream(protectedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, True)
                input.Position = prefixLength
                Using bounded As New LimitedReadStream(input, cipherLength)
                    Using aes = System.Security.Cryptography.Aes.Create()
                        aes.KeySize = 256
                        aes.BlockSize = 128
                        aes.Mode = CipherMode.CBC
                        aes.Padding = PaddingMode.PKCS7
                        aes.Key = encKey
                        aes.IV = iv
                        Using crypto As New CryptoStream(bounded, aes.CreateDecryptor(), CryptoStreamMode.Read, True)
                            Using output As New FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, True)
                                Await CopyStreamAsync(crypto, output, token)
                                Await output.FlushAsync(token)
                            End Using
                        End Using
                    End Using
                End Using
            End Using
        Catch
            If File.Exists(outputFilePath) Then
                Try
                    File.Delete(outputFilePath)
                Catch
                End Try
            End If
            Throw
        Finally
            ClearBytes(master, encKey, macKey)
        End Try
    End Function

    Private Function GetOrCreateMasterKey() As Byte()
        Dim path = System.IO.Path.Combine(SecurityFolder(), "document-master.key")
        If File.Exists(path) Then
            Dim protectedBytes = File.ReadAllBytes(path)
            Return ProtectedData.Unprotect(protectedBytes, Entropy("DOCUMENT_MASTER"), DataProtectionScope.CurrentUser)
        End If
        Dim master(31) As Byte
        Using rng = RandomNumberGenerator.Create()
            rng.GetBytes(master)
        End Using
        Dim encrypted = ProtectedData.Protect(master, Entropy("DOCUMENT_MASTER"), DataProtectionScope.CurrentUser)
        WriteAllBytesAtomic(path, encrypted)
        Return master
    End Function

    Private Function KeyPath(provider As String) As String
        Return System.IO.Path.Combine(KeyVaultFolder(), provider & ".key")
    End Function

    Private Shared Function NormalizeProvider(providerName As String) As String
        Dim p = If(providerName, "AI").Trim().ToLowerInvariant()
        For Each bad In Path.GetInvalidFileNameChars()
            p = p.Replace(bad, "_"c)
        Next
        Return If(p = "", "ai", p)
    End Function

    Private Shared Function Entropy(scopeName As String) As Byte()
        Return Encoding.UTF8.GetBytes("AIDocumentArchiveVB|" & If(scopeName, "") & "|SECURITY-V1")
    End Function

    Private Shared Function DeriveKey(master As Byte(), salt As Byte(), purpose As String) As Byte()
        Using h As New HMACSHA256(master)
            Return h.ComputeHash(Combine(salt, Encoding.UTF8.GetBytes(purpose)))
        End Using
    End Function

    Private Shared Function ComputeFileHmac(path As String, key As Byte(), Optional bytesToRead As Long = -1) As Byte()
        Using h As New HMACSHA256(key)
            Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                Dim remaining = If(bytesToRead < 0, fs.Length, bytesToRead)
                Dim buffer(81919) As Byte
                While remaining > 0
                    Dim wanted = CInt(Math.Min(buffer.Length, remaining))
                    Dim read = fs.Read(buffer, 0, wanted)
                    If read <= 0 Then Exit While
                    remaining -= read
                    h.TransformBlock(buffer, 0, read, Nothing, 0)
                End While
                h.TransformFinalBlock(New Byte() {}, 0, 0)
                Return h.Hash
            End Using
        End Using
    End Function

    Private Shared Async Function CopyStreamAsync(input As Stream, output As Stream, token As CancellationToken) As Task
        Dim buffer(81919) As Byte
        Do
            token.ThrowIfCancellationRequested()
            Dim read = Await input.ReadAsync(buffer, 0, buffer.Length, token)
            If read <= 0 Then Exit Do
            Await output.WriteAsync(buffer, 0, read, token)
        Loop
    End Function

    Private Shared Async Function CopyLimitedStreamAsync(input As Stream, output As Stream, maxBytes As Long, token As CancellationToken) As Task
        Dim buffer(81919) As Byte
        Dim remaining = maxBytes
        While remaining > 0
            token.ThrowIfCancellationRequested()
            Dim wanted = CInt(Math.Min(buffer.Length, remaining))
            Dim read = Await input.ReadAsync(buffer, 0, wanted, token)
            If read <= 0 Then Exit While
            Await output.WriteAsync(buffer, 0, read, token)
            remaining -= read
        End While
    End Function

    Private Shared Function IsOpenXmlPackage(filePath As String) As Boolean
        Try
            Using archive = ZipFile.OpenRead(filePath)
                For Each entry In archive.Entries
                    If String.Equals(entry.FullName, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase) Then Return True
                Next
            End Using
        Catch
        End Try
        Return False
    End Function

    Private Shared Function MatchResult(result As FileSignatureValidationResult, valid As Boolean, detectedType As String, description As String) As FileSignatureValidationResult
        result.IsValid = valid
        result.DetectedType = detectedType
        result.Message = If(valid, "تم التحقق من " & description & " بنجاح.", "امتداد الملف لا يطابق " & description & ".")
        Return result
    End Function

    Private Shared Function ReadPrefix(path As String, maxBytes As Integer) As Byte()
        Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            Dim length = CInt(Math.Min(maxBytes, fs.Length))
            If length <= 0 Then Return New Byte() {}
            Dim buffer(length - 1) As Byte
            Dim offset As Integer = 0
            While offset < length
                Dim read = fs.Read(buffer, offset, length - offset)
                If read <= 0 Then Exit While
                offset += read
            End While
            If offset = length Then Return buffer
            Dim trimmed(Math.Max(0, offset - 1)) As Byte
            If offset > 0 Then System.Buffer.BlockCopy(buffer, 0, trimmed, 0, offset)
            Return trimmed
        End Using
    End Function

    Private Shared Function StartsWith(buffer As Byte(), prefix As Byte()) As Boolean
        If buffer Is Nothing OrElse prefix Is Nothing OrElse buffer.Length < prefix.Length Then Return False
        For i As Integer = 0 To prefix.Length - 1
            If buffer(i) <> prefix(i) Then Return False
        Next
        Return True
    End Function

    Private Shared Function ContainsNullByte(buffer As Byte()) As Boolean
        If buffer Is Nothing Then Return False
        For Each b In buffer
            If b = 0 Then Return True
        Next
        Return False
    End Function

    Private Shared Function IsTextExtension(ext As String) As Boolean
        Return ext = ".txt" OrElse ext = ".csv" OrElse ext = ".xml" OrElse ext = ".json" OrElse ext = ".md" OrElse ext = ".html" OrElse ext = ".htm"
    End Function

    Private Shared Function HasText(value As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(value)
    End Function

    Private Shared Function ContainsAny(value As String, ParamArray values() As String) As Boolean
        Dim source = If(value, "")
        For Each item In values
            If source.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        Next
        Return False
    End Function

    Private Shared Function Combine(a As Byte(), b As Byte()) As Byte()
        Dim result(a.Length + b.Length - 1) As Byte
        System.Buffer.BlockCopy(a, 0, result, 0, a.Length)
        System.Buffer.BlockCopy(b, 0, result, a.Length, b.Length)
        Return result
    End Function

    Private Shared Function FixedEquals(a As Byte(), b As Byte()) As Boolean
        If a Is Nothing OrElse b Is Nothing OrElse a.Length <> b.Length Then Return False
        Dim diff As Integer = 0
        For i As Integer = 0 To a.Length - 1
            diff = diff Or (a(i) Xor b(i))
        Next
        Return diff = 0
    End Function

    Private Shared Sub ReadExact(stream As Stream, buffer As Byte())
        Dim offset = 0
        While offset < buffer.Length
            Dim read = stream.Read(buffer, offset, buffer.Length - offset)
            If read <= 0 Then Throw New EndOfStreamException()
            offset += read
        End While
    End Sub

    Private Shared Sub WriteAllBytesAtomic(path As String, bytes As Byte())
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path))
        Dim temp = path & ".tmp"
        File.WriteAllBytes(temp, bytes)
        If File.Exists(path) Then File.Delete(path)
        File.Move(temp, path)
    End Sub


    Private NotInheritable Class LimitedReadStream
        Inherits Stream

        Private ReadOnly _inner As Stream
        Private _remaining As Long

        Public Sub New(inner As Stream, length As Long)
            _inner = inner
            _remaining = Math.Max(0, length)
        End Sub

        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return _inner.CanRead
            End Get
        End Property

        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Return _remaining
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

        Public Overrides Sub Flush()
        End Sub

        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            If _remaining <= 0 Then Return 0
            Dim wanted = CInt(Math.Min(count, _remaining))
            Dim bytesRead = _inner.Read(buffer, offset, wanted)
            _remaining -= bytesRead
            Return bytesRead
        End Function

        Public Overrides Async Function ReadAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)
            If _remaining <= 0 Then Return 0
            Dim wanted = CInt(Math.Min(count, _remaining))
            Dim bytesRead = Await _inner.ReadAsync(buffer, offset, wanted, cancellationToken)
            _remaining -= bytesRead
            Return bytesRead
        End Function

        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function

        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            ' لا نغلق التدفق الأصلي؛ مالكه مسؤول عنه.
            MyBase.Dispose(disposing)
        End Sub
    End Class

    Private Shared Sub ClearBytes(firstBuffer As Byte(), secondBuffer As Byte(), thirdBuffer As Byte())
        ClearByteArray(firstBuffer)
        ClearByteArray(secondBuffer)
        ClearByteArray(thirdBuffer)
    End Sub

    Private Shared Sub ClearByteArray(buffer As Byte())
        If buffer Is Nothing OrElse buffer.Length = 0 Then Return
        Array.Clear(buffer, 0, buffer.Length)
    End Sub
End Class
