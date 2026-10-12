Imports System.Security.Cryptography
Imports System.Text

Namespace Security

    ''' <summary>موفّر المفتاح الرئيسي (32 بايت). التنفيذ الإنتاجي على Windows يحميه بـ DPAPI.</summary>
    Public Interface IKeyProvider
        Function GetMasterKey() As Byte()
    End Interface

    ''' <summary>مفتاح رئيسي في ملف (للاختبار/Linux). في Windows استخدم DpapiKeyProvider في التطبيق.</summary>
    Public NotInheritable Class FileKeyProvider
        Implements IKeyProvider

        Private ReadOnly _path As String
        Private _key As Byte()
        Private ReadOnly _lock As New Object()

        Public Sub New(path As String)
            _path = path
        End Sub

        Public Function GetMasterKey() As Byte() Implements IKeyProvider.GetMasterKey
            SyncLock _lock
                If _key Is Nothing Then
                    If IO.File.Exists(_path) Then
                        _key = Convert.FromBase64String(IO.File.ReadAllText(_path).Trim())
                    Else
                        _key = RandomNumberGenerator.GetBytes(32)
                        Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(_path))
                        If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
                        IO.File.WriteAllText(_path, Convert.ToBase64String(_key))
                        If Not OperatingSystem.IsWindows() Then
                            IO.File.SetUnixFileMode(_path, IO.UnixFileMode.UserRead Or IO.UnixFileMode.UserWrite)
                        End If
                    End If
                End If
                Return CType(_key.Clone(), Byte())
            End SyncLock
        End Function
    End Class

    Public NotInheritable Class KeyDerivation
        Private Sub New()
        End Sub

        ''' <summary>اشتقاق مفتاح فرعي لغرض محدد (فصل المفاتيح: audit/vault/webhook...).</summary>
        Public Shared Function Derive(master As Byte(), purpose As String) As Byte()
            Using h As New HMACSHA256(master)
                Return h.ComputeHash(Encoding.UTF8.GetBytes("ptz-platform/v1/" & purpose))
            End Using
        End Function
    End Class

    ''' <summary>
    ''' خزنة أسرار: تشفير AES-256-GCM لكلمات مرور الكاميرات قبل حفظها، مع ربط السر بسياقه
    ''' (اسم الكاميرا) كبيانات مرتبطة فلا يمكن نقل السر لإدخال آخر.
    ''' </summary>
    Public NotInheritable Class CredentialVault
        Private Const Prefix As String = "v1:"
        Private ReadOnly _key As Byte()

        Public Sub New(keyProvider As IKeyProvider)
            _key = KeyDerivation.Derive(keyProvider.GetMasterKey(), "vault")
        End Sub

        Public Function Protect(plaintext As String, context As String) As String
            Dim pt As Byte() = Encoding.UTF8.GetBytes(plaintext)
            Dim nonce As Byte() = RandomNumberGenerator.GetBytes(12)
            Dim tag(15) As Byte
            Dim ct(pt.Length - 1) As Byte
            Using aes As New AesGcm(_key, 16)
                aes.Encrypt(nonce, pt, ct, tag, Encoding.UTF8.GetBytes(context))
            End Using
            Dim blob(12 + 16 + ct.Length - 1) As Byte
            Buffer.BlockCopy(nonce, 0, blob, 0, 12)
            Buffer.BlockCopy(tag, 0, blob, 12, 16)
            Buffer.BlockCopy(ct, 0, blob, 28, ct.Length)
            Return Prefix & Convert.ToBase64String(blob)
        End Function

        ''' <summary>يفك التشفير؛ يرمي CryptographicException عند التلاعب أو سياق خاطئ.</summary>
        Public Function Unprotect(protectedText As String, context As String) As String
            If protectedText Is Nothing OrElse Not protectedText.StartsWith(Prefix, StringComparison.Ordinal) Then
                Throw New CryptographicException("صيغة السر غير معروفة.")
            End If
            Dim blob As Byte() = Convert.FromBase64String(protectedText.Substring(Prefix.Length))
            If blob.Length < 28 Then Throw New CryptographicException("السر مبتور.")
            Dim nonce(11) As Byte
            Dim tag(15) As Byte
            Dim ct(blob.Length - 29) As Byte
            Buffer.BlockCopy(blob, 0, nonce, 0, 12)
            Buffer.BlockCopy(blob, 12, tag, 0, 16)
            Buffer.BlockCopy(blob, 28, ct, 0, ct.Length)
            Dim pt(ct.Length - 1) As Byte
            Using aes As New AesGcm(_key, 16)
                aes.Decrypt(nonce, ct, tag, pt, Encoding.UTF8.GetBytes(context))
            End Using
            Return Encoding.UTF8.GetString(pt)
        End Function
    End Class

End Namespace
