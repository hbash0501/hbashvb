Imports System
Imports System.Data
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text

Public Class ArchiveMaintenanceService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _repo As ArchiveRepository

    Public Sub New(settings As AppSettings, repo As ArchiveRepository)
        _settings = settings
        _repo = repo
    End Sub

    Public Function BuildDiagnostics() As DataTable
        Dim table As New DataTable()
        table.Columns.Add("الفحص")
        table.Columns.Add("النتيجة")
        table.Columns.Add("التفاصيل")

        AddRow(table, "مجلد البيانات", If(Directory.Exists(_settings.BaseDataFolder), "سليم", "غير موجود"), _settings.BaseDataFolder)
        AddRow(table, "مجلد الأرشيف", If(Directory.Exists(_settings.ArchiveRootFolder), "سليم", "غير موجود"), _settings.ArchiveRootFolder)

        Dim dbFile = Path.Combine(_settings.BaseDataFolder, "archive-db.xml")
        AddRow(table, "قاعدة XML", If(File.Exists(dbFile), "موجودة", "غير موجودة"), If(File.Exists(dbFile), FormatBytes(New FileInfo(dbFile).Length), dbFile))
        AddRow(table, "نسخة احتياطية XML", If(File.Exists(dbFile & ".bak"), "موجودة", "غير موجودة"), dbFile & ".bak")

        Dim docs = _repo.GetDocuments()
        AddRow(table, "عدد المستندات", "معلومة", docs.Count.ToString())
        Dim missing = docs.Where(Function(d) Not String.IsNullOrWhiteSpace(d.StoredPath) AndAlso Not File.Exists(d.StoredPath)).Count()
        AddRow(table, "ملفات مفقودة", If(missing = 0, "سليم", "يحتاج مراجعة"), missing.ToString())
        Dim emptyText = docs.Where(Function(d) String.IsNullOrWhiteSpace(d.TextContent)).Count()
        AddRow(table, "مستندات بلا نص OCR", If(emptyText = 0, "ممتاز", "قابل للتحسين"), emptyText.ToString())
        Dim duplicateHashes = docs.Where(Function(d) Not String.IsNullOrWhiteSpace(d.FileHashSha256)).GroupBy(Function(d) d.FileHashSha256).Where(Function(g) g.Count() > 1).Count()
        AddRow(table, "مجموعات تكرار بالبصمة", If(duplicateHashes = 0, "سليم", "تكرارات موجودة"), duplicateHashes.ToString())
        Dim confidential = docs.Where(Function(d) d.Confidentiality = "سري").Count()
        AddRow(table, "مستندات سرية", "معلومة", confidential.ToString())
        Dim pending = docs.Where(Function(d) d.Status = "قيد المراجعة" OrElse d.Status = "يحتاج استكمال").Count()
        AddRow(table, "معاملات تحتاج إجراء", If(pending = 0, "ممتاز", "تحتاج متابعة"), pending.ToString())
        Return table
    End Function

    Public Function CreateBackupPackage(destinationZipPath As String) As String
        If String.IsNullOrWhiteSpace(destinationZipPath) Then Throw New ArgumentException("مسار النسخة الاحتياطية غير صحيح.")
        Dim folderPath = Path.GetDirectoryName(destinationZipPath)
        If Not String.IsNullOrWhiteSpace(folderPath) Then Directory.CreateDirectory(folderPath)
        If File.Exists(destinationZipPath) Then File.Delete(destinationZipPath)

        Using archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create)
            AddFileIfExists(archive, Path.Combine(_settings.BaseDataFolder, "archive-db.xml"), "Data/archive-db.xml")
            AddFileIfExists(archive, Path.Combine(_settings.BaseDataFolder, "archive-db.xml.bak"), "Data/archive-db.xml.bak")
            AddFileIfExists(archive, Path.Combine(_settings.BaseDataFolder, "users.xml"), "Data/users.xml")
            AddFileIfExists(archive, AppSettings.SettingsFilePath(), "Data/settings.xml")
            If Directory.Exists(_settings.ArchiveRootFolder) Then
                For Each sourceFilePath In Directory.GetFiles(_settings.ArchiveRootFolder, "*.*", SearchOption.AllDirectories)
                    Dim rel = sourceFilePath.Substring(_settings.ArchiveRootFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    archive.CreateEntryFromFile(sourceFilePath, "ArchiveFiles/" & rel.Replace("\", "/"), CompressionLevel.Optimal)
                Next
            End If
            Dim manifest = BuildManifestText()
            Dim entry = archive.CreateEntry("README_BACKUP_AR.txt", CompressionLevel.Optimal)
            Using sw As New StreamWriter(entry.Open(), Encoding.UTF8)
                sw.Write(manifest)
            End Using
        End Using
        _repo.AddAudit("إنشاء نسخة احتياطية", "", "", destinationZipPath)
        Return destinationZipPath
    End Function

    Public Function ExportInventoryCsv(destinationCsvPath As String) As String
        Dim docs = _repo.GetDocuments()
        Using sw As New StreamWriter(destinationCsvPath, False, New UTF8Encoding(True))
            sw.WriteLine("ArchiveNumber,Title,GeneralCategory,SubCategory,Status,Confidentiality,Priority,FileName,StoredPath,CreatedAt")
            For Each d In docs
                sw.WriteLine(String.Join(",", New String() {Csv(d.ArchiveNumber), Csv(d.Title), Csv(d.GeneralCategory), Csv(d.SubCategory), Csv(d.Status), Csv(d.Confidentiality), Csv(d.Priority), Csv(d.FileName), Csv(d.StoredPath), Csv(d.CreatedAt.ToString("yyyy-MM-dd HH:mm"))}))
            Next
        End Using
        _repo.AddAudit("تصدير فهرس الأرشيف", "", "", destinationCsvPath)
        Return destinationCsvPath
    End Function

    Public Function RebuildTextIndex() As String
        Dim outputPath = Path.Combine(_settings.BaseDataFolder, "search-index.tsv")
        Using sw As New StreamWriter(outputPath, False, New UTF8Encoding(True))
            sw.WriteLine("Id" & ControlChars.Tab & "ArchiveNumber" & ControlChars.Tab & "Title" & ControlChars.Tab & "Category" & ControlChars.Tab & "Tags" & ControlChars.Tab & "TextPreview")
            For Each d In _repo.GetDocuments()
                Dim preview = NormalizeLine(If(d.TextContent, ""))
                If preview.Length > 800 Then preview = preview.Substring(0, 800)
                sw.WriteLine(String.Join(ControlChars.Tab, New String() {d.Id, CleanTsv(d.ArchiveNumber), CleanTsv(d.Title), CleanTsv(d.GeneralCategory & " / " & d.SubCategory), CleanTsv(d.Tags), CleanTsv(preview)}))
            Next
        End Using
        _repo.AddAudit("إعادة بناء فهرس نصي", "", "", outputPath)
        Return outputPath
    End Function

    Private Function BuildManifestText() As String
        Dim sb As New StringBuilder()
        sb.AppendLine("نسخة احتياطية لمنصة الأرشفة الذكية")
        sb.AppendLine("تاريخ الإنشاء: " & DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
        sb.AppendLine("مجلد البيانات: " & _settings.BaseDataFolder)
        sb.AppendLine("مجلد الأرشيف: " & _settings.ArchiveRootFolder)
        sb.AppendLine("عدد المستندات: " & _repo.CountAll().ToString())
        sb.AppendLine("")
        sb.AppendLine("تحتوي النسخة على قاعدة البيانات، المستخدمين، الإعدادات، وملفات الأرشيف إن وجدت.")
        Return sb.ToString()
    End Function

    Private Shared Sub AddFileIfExists(archive As ZipArchive, sourceFilePath As String, entryName As String)
        If File.Exists(sourceFilePath) Then archive.CreateEntryFromFile(sourceFilePath, entryName, CompressionLevel.Optimal)
    End Sub

    Private Shared Sub AddRow(table As DataTable, checkName As String, resultText As String, details As String)
        table.Rows.Add(checkName, resultText, details)
    End Sub

    Private Shared Function FormatBytes(size As Long) As String
        If size < 1024 Then Return size.ToString() & " B"
        If size < 1024 * 1024 Then Return Math.Round(size / 1024.0R, 1).ToString() & " KB"
        Return Math.Round(size / 1024.0R / 1024.0R, 1).ToString() & " MB"
    End Function

    Private Shared Function Csv(value As String) As String
        Dim v = If(value, "").Replace(Chr(34), Chr(34) & Chr(34))
        Return """" & v & """"
    End Function

    Private Shared Function CleanTsv(value As String) As String
        Return NormalizeLine(value).Replace(ControlChars.Tab, " ")
    End Function

    Private Shared Function NormalizeLine(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return ""
        Return value.Replace(ControlChars.Cr, " ").Replace(ControlChars.Lf, " ").Trim()
    End Function
End Class
