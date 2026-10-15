Imports System
Imports System.IO

Public Class StorageService
    Public Shared Function StoreFile(sourcePath As String, settings As AppSettings) As String
        If String.IsNullOrWhiteSpace(sourcePath) OrElse Not File.Exists(sourcePath) Then
            Throw New FileNotFoundException("الملف غير موجود.", sourcePath)
        End If

        Dim yearFolder = DateTime.Now.ToString("yyyy")
        Dim monthFolder = DateTime.Now.ToString("MM")
        Dim destFolder = Path.Combine(settings.ArchiveRootFolder, yearFolder, monthFolder)
        Directory.CreateDirectory(destFolder)

        Dim ext = Path.GetExtension(sourcePath)
        Dim safeName = Guid.NewGuid().ToString("N") & ext
        Dim dest = Path.Combine(destFolder, safeName)
        File.Copy(sourcePath, dest, True)
        Return dest
    End Function
End Class
