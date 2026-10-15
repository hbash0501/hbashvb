Imports System
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Xml.Linq

Public Class ProcessingCacheService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _cacheFolder As String

    Public Sub New(settings As AppSettings)
        _settings = settings
        Dim baseFolder As String = If(settings Is Nothing OrElse String.IsNullOrWhiteSpace(settings.BaseDataFolder), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SaeedAIDocArchive"), settings.BaseDataFolder)
        _cacheFolder = Path.Combine(baseFolder, "SpeedCache")
        Directory.CreateDirectory(_cacheFolder)
        Directory.CreateDirectory(Path.Combine(_cacheFolder, "text"))
        Directory.CreateDirectory(Path.Combine(_cacheFolder, "ai"))
    End Sub

    Public Function ComputeFileHash(filePath As String) As String
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return ""
        Using sha = SHA256.Create()
            Using fs = File.OpenRead(filePath)
                Return ToHex(sha.ComputeHash(fs))
            End Using
        End Using
    End Function

    Public Function ComputeTextHash(text As String) As String
        If text Is Nothing Then text = ""
        Using sha = SHA256.Create()
            Return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
        End Using
    End Function

    Public Function GetCachedText(fileHash As String) As String
        If String.IsNullOrWhiteSpace(fileHash) Then Return ""
        Dim p = Path.Combine(_cacheFolder, "text", CleanKey(fileHash) & ".txt")
        If File.Exists(p) Then Return File.ReadAllText(p, Encoding.UTF8)
        Return ""
    End Function

    Public Sub SetCachedText(fileHash As String, text As String)
        If String.IsNullOrWhiteSpace(fileHash) Then Return
        Dim p = Path.Combine(_cacheFolder, "text", CleanKey(fileHash) & ".txt")
        File.WriteAllText(p, If(text, ""), Encoding.UTF8)
    End Sub

    Public Function GetCachedAi(promptVersion As String, modelName As String, textHash As String, purpose As String) As String
        Dim key = BuildAiKey(promptVersion, modelName, textHash, purpose)
        If key = "" Then Return ""
        Dim p = Path.Combine(_cacheFolder, "ai", key & ".txt")
        If File.Exists(p) Then Return File.ReadAllText(p, Encoding.UTF8)
        Return ""
    End Function

    Public Sub SetCachedAi(promptVersion As String, modelName As String, textHash As String, purpose As String, value As String)
        Dim key = BuildAiKey(promptVersion, modelName, textHash, purpose)
        If key = "" Then Return
        Dim p = Path.Combine(_cacheFolder, "ai", key & ".txt")
        File.WriteAllText(p, If(value, ""), Encoding.UTF8)
    End Sub

    Public Function CacheReport() As String
        Dim textCount As Integer = SafeCount(Path.Combine(_cacheFolder, "text"), "*.txt")
        Dim aiCount As Integer = SafeCount(Path.Combine(_cacheFolder, "ai"), "*.txt")
        Return "كاش النصوص: " & textCount.ToString() & " ملف" & Environment.NewLine &
               "كاش الذكاء الاصطناعي: " & aiCount.ToString() & " نتيجة" & Environment.NewLine &
               "المجلد: " & _cacheFolder
    End Function

    Private Function BuildAiKey(promptVersion As String, modelName As String, textHash As String, purpose As String) As String
        If String.IsNullOrWhiteSpace(textHash) Then Return ""
        Dim raw = If(promptVersion, "v24") & "|" & If(modelName, "model") & "|" & If(textHash, "") & "|" & If(purpose, "")
        Return ComputeTextHash(raw)
    End Function

    Private Shared Function ToHex(bytes As Byte()) As String
        Dim sb As New StringBuilder(bytes.Length * 2)
        For Each b As Byte In bytes
            sb.Append(b.ToString("x2"))
        Next
        Return sb.ToString()
    End Function

    Private Shared Function CleanKey(value As String) As String
        If value Is Nothing Then Return ""
        Dim v = value.Trim()
        For Each c In Path.GetInvalidFileNameChars()
            v = v.Replace(c, "_"c)
        Next
        Return v
    End Function

    Private Shared Function SafeCount(folder As String, pattern As String) As Integer
        Try
            If Not Directory.Exists(folder) Then Return 0
            Return Directory.GetFiles(folder, pattern).Length
        Catch
            Return 0
        End Try
    End Function
End Class
