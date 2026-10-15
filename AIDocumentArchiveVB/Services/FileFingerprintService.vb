Imports System
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text.RegularExpressions

Public Class FileFingerprintService
    Public Shared Function ComputeSha256(filePath As String) As String
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return ""
        Using sha = SHA256.Create()
            Using fs = File.OpenRead(filePath)
                Dim hash = sha.ComputeHash(fs)
                Return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()
            End Using
        End Using
    End Function

    Public Shared Function GetFileSize(filePath As String) As Long
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return 0
        Return New FileInfo(filePath).Length
    End Function

    Public Shared Function EstimatePageCount(filePath As String) As Integer
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return 0
        Dim ext = Path.GetExtension(filePath).ToLowerInvariant()
        If ext = ".jpg" OrElse ext = ".jpeg" OrElse ext = ".png" OrElse ext = ".bmp" OrElse ext = ".tif" OrElse ext = ".tiff" Then Return 1
        If ext = ".pdf" Then
            Try
                Dim bytes = File.ReadAllBytes(filePath)
                Dim text = System.Text.Encoding.ASCII.GetString(bytes)
                Dim count = Regex.Matches(text, "/Type\s*/Page\b").Count
                If count > 0 Then Return count
            Catch
            End Try
        End If
        Return 1
    End Function

    Public Shared Function IsImage(filePath As String) As Boolean
        Dim ext = Path.GetExtension(If(filePath, "")).ToLowerInvariant()
        Return ext = ".jpg" OrElse ext = ".jpeg" OrElse ext = ".png" OrElse ext = ".bmp" OrElse ext = ".tif" OrElse ext = ".tiff"
    End Function
End Class
