Imports System.Security.Cryptography
Imports Ptz.Core.Security

Namespace Infrastructure

    ''' <summary>
    ''' المفتاح الرئيسي محميّ بـ Windows DPAPI (نطاق المستخدم الحالي): لا يمكن فكه من مستخدم آخر
    ''' أو جهاز آخر. ينشأ عشوائياً عند أول تشغيل.
    ''' </summary>
    Public NotInheritable Class DpapiKeyProvider
        Implements IKeyProvider

        Private ReadOnly _path As String
        Private _key As Byte()
        Private ReadOnly _lock As New Object()
        Private Shared ReadOnly Entropy As Byte() = Text.Encoding.UTF8.GetBytes("PtzPlatform.MasterKey.v1")

        Public Sub New(path As String)
            _path = path
        End Sub

        Public Function GetMasterKey() As Byte() Implements IKeyProvider.GetMasterKey
            SyncLock _lock
                If _key Is Nothing Then
                    If IO.File.Exists(_path) Then
                        _key = ProtectedData.Unprotect(IO.File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser)
                    Else
                        _key = RandomNumberGenerator.GetBytes(32)
                        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(_path))
                        IO.File.WriteAllBytes(_path, ProtectedData.Protect(_key, Entropy, DataProtectionScope.CurrentUser))
                    End If
                End If
                Return CType(_key.Clone(), Byte())
            End SyncLock
        End Function
    End Class

End Namespace
