Imports System.Text.Json
Imports Ptz.Core.Models
Imports Ptz.Core.Security

Public NotInheritable Class CameraConfig
    ''' <summary>مفتاح ثابت للإدخال، مثل se7c432mw أو de7232iw.</summary>
    Public Property Key As String = ""
    Public Property DisplayName As String = ""
    Public Property ModelName As String = ""
    Public Property Host As String = ""
    Public Property Port As Integer = 443
    Public Property UseHttps As Boolean = True
    Public Property AllowInsecureHttp As Boolean
    Public Property Username As String = ""
    ''' <summary>كلمة المرور مشفّرة بـ AES-GCM (لا تُحفظ نصاً أبداً).</summary>
    Public Property PasswordProtected As String = ""
    Public Property PinnedCertSha256 As String = ""
    Public Property TimeoutSeconds As Integer = 10
End Class

''' <summary>تخزين إعدادات الكاميرات؛ كلمات المرور تمر عبر الخزنة المشفّرة.</summary>
Public NotInheritable Class CameraConfigStore
    Private ReadOnly _path As String
    Private ReadOnly _vault As CredentialVault
    Private ReadOnly _lock As New Object()
    Private ReadOnly _items As New Dictionary(Of String, CameraConfig)(StringComparer.OrdinalIgnoreCase)

    Public Sub New(path As String, vault As CredentialVault)
        _path = path
        _vault = vault
        Load()
    End Sub

    Public Function All() As List(Of CameraConfig)
        SyncLock _lock
            Return _items.Values.Select(Function(c) Copy(c)).ToList()
        End SyncLock
    End Function

    Public Function [Get](key As String) As CameraConfig
        SyncLock _lock
            Dim c As CameraConfig = Nothing
            Return If(_items.TryGetValue(key, c), Copy(c), Nothing)
        End SyncLock
    End Function

    ''' <summary>يضمن وجود إدخال افتراضي لكل طراز مدعوم.</summary>
    Public Sub EnsureDefaults(profiles As IEnumerable(Of (Key As String, Profile As CameraProfile)))
        SyncLock _lock
            For Each p In profiles
                If Not _items.ContainsKey(p.Key) Then
                    _items(p.Key) = New CameraConfig With {.Key = p.Key, .DisplayName = p.Profile.ModelName, .ModelName = p.Profile.ModelName}
                End If
            Next
            Save()
        End SyncLock
    End Sub

    ''' <summary>يحفظ الإعداد. <paramref name="newPassword"/> = Nothing يبقي كلمة المرور الحالية.</summary>
    Public Sub Update(config As CameraConfig, newPassword As String)
        Dim cfg As CameraConfig = Copy(config)
        Dim probe As New CameraConnection With {.Host = cfg.Host, .Port = cfg.Port, .UseHttps = cfg.UseHttps,
            .AllowInsecureHttp = cfg.AllowInsecureHttp, .Username = cfg.Username, .TimeoutSeconds = cfg.TimeoutSeconds}
        probe.Validate()
        SyncLock _lock
            Dim existing As CameraConfig = Nothing
            _items.TryGetValue(cfg.Key, existing)
            If newPassword IsNot Nothing Then
                cfg.PasswordProtected = If(newPassword = "", "", _vault.Protect(newPassword, "camera:" & cfg.Key))
            ElseIf existing IsNot Nothing Then
                cfg.PasswordProtected = existing.PasswordProtected
            End If
            _items(cfg.Key) = cfg
            Save()
        End SyncLock
    End Sub

    ''' <summary>يبني معاملات الاتصال (يفك تشفير كلمة المرور في الذاكرة فقط).</summary>
    Public Function BuildConnection(key As String) As CameraConnection
        Dim c As CameraConfig = [Get](key)
        If c Is Nothing Then Throw New KeyNotFoundException("الكاميرا غير معرّفة.")
        Dim pw As String = If(c.PasswordProtected = "", "", _vault.Unprotect(c.PasswordProtected, "camera:" & c.Key))
        Return New CameraConnection With {.Host = c.Host, .Port = c.Port, .UseHttps = c.UseHttps, .AllowInsecureHttp = c.AllowInsecureHttp,
            .Username = c.Username, .Password = pw, .PinnedCertSha256 = c.PinnedCertSha256, .TimeoutSeconds = c.TimeoutSeconds}
    End Function

    Private Shared Function Copy(c As CameraConfig) As CameraConfig
        Return New CameraConfig With {.Key = c.Key, .DisplayName = c.DisplayName, .ModelName = c.ModelName, .Host = c.Host, .Port = c.Port,
            .UseHttps = c.UseHttps, .AllowInsecureHttp = c.AllowInsecureHttp, .Username = c.Username,
            .PasswordProtected = c.PasswordProtected, .PinnedCertSha256 = c.PinnedCertSha256, .TimeoutSeconds = c.TimeoutSeconds}
    End Function

    Private Sub Load()
        If Not IO.File.Exists(_path) Then Return
        Dim list As List(Of CameraConfig) = JsonSerializer.Deserialize(Of List(Of CameraConfig))(IO.File.ReadAllText(_path, Text.Encoding.UTF8))
        If list Is Nothing Then Return
        For Each c As CameraConfig In list
            _items(c.Key) = c
        Next
    End Sub

    Private Sub Save()
        Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(_path))
        If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
        Dim tmp As String = _path & ".tmp"
        IO.File.WriteAllText(tmp, JsonSerializer.Serialize(_items.Values.ToList()), Text.Encoding.UTF8)
        IO.File.Move(tmp, _path, True)
    End Sub
End Class
