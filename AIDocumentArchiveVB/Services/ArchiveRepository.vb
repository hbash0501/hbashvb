Imports System
Imports System.Data
Imports System.IO
Imports System.Linq
Imports System.Xml
Imports System.Xml.Linq

Public Class ArchiveRepository
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _dbPath As String
    Private _db As XDocument
    Private ReadOnly _syncRoot As New Object()
    Private _revision As Long
    Public ReadOnly VersionStore As BasiraStore
    Public ReadOnly Property Revision As Long
        Get
            Return System.Threading.Interlocked.Read(_revision)
        End Get
    End Property

    Public Sub New(settings As AppSettings)
        _settings = settings
        VersionStore = New BasiraStore(settings.BaseDataFolder)
        Directory.CreateDirectory(_settings.BaseDataFolder)
        Directory.CreateDirectory(_settings.ArchiveRootFolder)
        _dbPath = Path.Combine(_settings.BaseDataFolder, "archive-db.xml")
        LoadOrCreate()
    End Sub

    Private Sub LoadOrCreate()
        If File.Exists(_dbPath) Then
            Try
                _db = XDocument.Load(_dbPath, LoadOptions.PreserveWhitespace)
                EnsureDatabaseShape()
            Catch ex As XmlException
                If TryLoadBackupDatabase() Then Return
                BackupCorruptedDatabase(ex)
                Throw New InvalidDataException("تعذر فتح قاعدة الأرشيف أو نسختها الاحتياطية. احتُفظ بالملفات الأصلية للمراجعة.", ex)
            Catch ex As IOException
                If TryLoadBackupDatabase() Then Return
                BackupCorruptedDatabase(ex)
                Throw New IOException("تعذر الوصول إلى قاعدة الأرشيف. لم تُنشأ قاعدة فارغة بدلاً منها.", ex)
            End Try
        Else
            CreateEmptyDatabase()
            SaveDb()
        End If
    End Sub

    Private Sub CreateEmptyDatabase()
        _db = New XDocument(New XElement("Archive",
            New XElement("Documents"),
            New XElement("Categories"),
            New XElement("AuditLog"),
            New XElement("WorkflowHistory")))
    End Sub

    Private Sub EnsureDatabaseShape()
        If _db Is Nothing OrElse _db.Root Is Nothing OrElse _db.Root.Name <> "Archive" Then
            Throw New InvalidDataException("بنية ملف الأرشيف غير صحيحة. لم يجر استبدالها بقاعدة فارغة.")
        End If
        If _db.Root.Element("Documents") Is Nothing Then _db.Root.Add(New XElement("Documents"))
        If _db.Root.Element("Categories") Is Nothing Then _db.Root.Add(New XElement("Categories"))
        If _db.Root.Element("AuditLog") Is Nothing Then _db.Root.Add(New XElement("AuditLog"))
        If _db.Root.Element("WorkflowHistory") Is Nothing Then _db.Root.Add(New XElement("WorkflowHistory"))
    End Sub


    Private Function TryLoadBackupDatabase() As Boolean
        Try
            Dim backupPath As String = _dbPath & ".bak"
            If Not File.Exists(backupPath) Then Return False
            _db = XDocument.Load(backupPath, LoadOptions.PreserveWhitespace)
            EnsureDatabaseShape()
            Try
                File.Copy(backupPath, _dbPath, True)
            Catch
            End Try
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Sub BackupCorruptedDatabase(ex As Exception)
        Try
            Directory.CreateDirectory(_settings.BaseDataFolder)
            If File.Exists(_dbPath) Then
                Dim stamp As String = DateTime.Now.ToString("yyyyMMdd-HHmmss")
                Dim backupPath As String = Path.Combine(_settings.BaseDataFolder, "archive-db.corrupted-" & stamp & ".xml")
                File.Copy(_dbPath, backupPath, True)
            End If
        Catch
        End Try
    End Sub

    Private Sub SaveDb()
        EnsureDatabaseShape()
        BasiraStore.AtomicSave(_dbPath, _db)
        System.Threading.Interlocked.Increment(_revision)
    End Sub

    Public Sub EnsureSeedData()
        SyncLock _syncRoot
            Dim cats = _db.Root.Element("Categories")
            If cats IsNot Nothing AndAlso cats.Elements("Category").Any() Then Return

            AddCategory("إداري", "تعاميم")
            AddCategory("إداري", "خطابات داخلية")
            AddCategory("إداري", "قرارات")
            AddCategory("مالي", "فواتير")
            AddCategory("مالي", "أوامر صرف")
            AddCategory("مالي", "مستخلصات")
            AddCategory("موارد بشرية", "إجازات")
            AddCategory("موارد بشرية", "انتدابات")
            AddCategory("موارد بشرية", "مباشرات")
            AddCategory("عقود", "عقد توريد")
            AddCategory("عقود", "عقد صيانة")
            AddCategory("مراسلات", "وارد")
            AddCategory("مراسلات", "صادر")
            AddCategory("تقارير", "تقرير شهري")
            AddCategory("تقارير", "تقرير أداء")
            AddCategory("سري", "معاملة حساسة")
            SaveDb()
        End SyncLock
    End Sub

    Public Function GetGeneralCategories() As List(Of String)
        SyncLock _syncRoot
            Return _db.Root.Element("Categories").Elements("Category").
                Select(Function(e) CStr(e.Attribute("General"))).
                Where(Function(s) Not String.IsNullOrWhiteSpace(s)).Distinct().OrderBy(Function(s) s).ToList()
        End SyncLock
    End Function

    Public Function GetSubCategories(generalName As String) As List(Of String)
        SyncLock _syncRoot
            Return _db.Root.Element("Categories").Elements("Category").
                Where(Function(e) CStr(e.Attribute("General")) = generalName).
                Select(Function(e) CStr(e.Attribute("Sub"))).
                Where(Function(s) Not String.IsNullOrWhiteSpace(s)).Distinct().OrderBy(Function(s) s).ToList()
        End SyncLock
    End Function

    Public Function GetAllCategories() As List(Of CategoryInfo)
        SyncLock _syncRoot
            Return _db.Root.Element("Categories").Elements("Category").
                Select(Function(e) New CategoryInfo(CStr(e.Attribute("General")), CStr(e.Attribute("Sub")))).
                OrderBy(Function(c) c.GeneralName).ThenBy(Function(c) c.SubName).ToList()
        End SyncLock
    End Function

    Public Sub AddCategory(generalName As String, subName As String)
        SyncLock _syncRoot
            generalName = If(generalName, "").Trim()
            subName = If(subName, "").Trim()
            If generalName = "" Then Return
            If subName = "" Then subName = "عام"

            Dim cats = _db.Root.Element("Categories")
            Dim exists = cats.Elements("Category").Any(Function(e) CStr(e.Attribute("General")) = generalName AndAlso CStr(e.Attribute("Sub")) = subName)
            If Not exists Then
                cats.Add(New XElement("Category", New XAttribute("General", generalName), New XAttribute("Sub", subName)))
                SaveDb()
            End If
        End SyncLock
    End Sub

    Public Sub SaveDocument(record As DocumentRecord)
        SyncLock _syncRoot
            If String.IsNullOrWhiteSpace(record.Id) Then record.Id = Guid.NewGuid().ToString("N")
            record.UpdatedAt = DateTime.Now
            If record.CreatedAt = DateTime.MinValue Then record.CreatedAt = DateTime.Now

            Dim docs = _db.Root.Element("Documents")
            Dim existing = docs.Elements("Document").FirstOrDefault(Function(e) CStr(e.Attribute("Id")) = record.Id)
            Dim wasUpdate = existing IsNot Nothing
            If existing IsNot Nothing Then
                VersionStore.CapturePrevious(record.Id, existing)
                existing.Remove()
            End If
            docs.Add(ToElement(record))
            AddCategory(record.GeneralCategory, record.SubCategory)
            SaveDb()
            AddAudit(If(wasUpdate, "تعديل مستند", "حفظ مستند"), record.Id, record.Title, "التصنيف: " & record.GeneralCategory & " / " & record.SubCategory)
        End SyncLock
    End Sub

    Public Sub UpdateDocument(record As DocumentRecord)
        SyncLock _syncRoot
            If record Is Nothing Then Return
            SaveDocument(record)
        End SyncLock
    End Sub

    Public Function GetDocumentById(id As String) As DocumentRecord
        SyncLock _syncRoot
            Dim e = _db.Root.Element("Documents").Elements("Document").FirstOrDefault(Function(x) CStr(x.Attribute("Id")) = id)
            If e Is Nothing Then Return Nothing
            Return FromElement(e)
        End SyncLock
    End Function

    Public Function GetDocuments(Optional search As String = "", Optional generalCategory As String = "", Optional status As String = "") As List(Of DocumentRecord)
        SyncLock _syncRoot
            Dim q = _db.Root.Element("Documents").Elements("Document").Select(Function(e) FromElement(e)).AsEnumerable()
            If Not String.IsNullOrWhiteSpace(search) Then
                Dim s = search.Trim().ToLowerInvariant()
                q = q.Where(Function(d) Safe(d.Title).ToLowerInvariant().Contains(s) OrElse Safe(d.Tags).ToLowerInvariant().Contains(s) OrElse Safe(d.TextContent).ToLowerInvariant().Contains(s) OrElse Safe(d.ReferenceNumber).ToLowerInvariant().Contains(s) OrElse Safe(d.ArchiveNumber).ToLowerInvariant().Contains(s) OrElse Safe(d.OcrStructuredReport).ToLowerInvariant().Contains(s) OrElse Safe(d.OcrEntities).ToLowerInvariant().Contains(s) OrElse Safe(d.OcrNames).ToLowerInvariant().Contains(s))
            End If
            If Not String.IsNullOrWhiteSpace(generalCategory) AndAlso generalCategory <> "الكل" Then
                q = q.Where(Function(d) d.GeneralCategory = generalCategory)
            End If
            If Not String.IsNullOrWhiteSpace(status) AndAlso status <> "الكل" Then
                q = q.Where(Function(d) d.Status = status)
            End If
            Return q.OrderByDescending(Function(d) d.CreatedAt).ToList()
        End SyncLock
    End Function



    Public Function AdvancedSearch(criteria As DocumentSearchCriteria) As List(Of DocumentRecord)
        If criteria Is Nothing Then Return GetDocuments()
        Dim q = GetDocuments().AsEnumerable()

        If Not String.IsNullOrWhiteSpace(criteria.Keyword) Then
            Dim s = criteria.Keyword.Trim().ToLowerInvariant()
            q = q.Where(Function(d) Safe(d.Title).ToLowerInvariant().Contains(s) OrElse Safe(d.Description).ToLowerInvariant().Contains(s) OrElse Safe(d.Summary).ToLowerInvariant().Contains(s) OrElse Safe(d.TextContent).ToLowerInvariant().Contains(s) OrElse Safe(d.FileName).ToLowerInvariant().Contains(s) OrElse Safe(d.ArchiveNumber).ToLowerInvariant().Contains(s) OrElse Safe(d.SourceEntity).ToLowerInvariant().Contains(s) OrElse Safe(d.DestinationEntity).ToLowerInvariant().Contains(s) OrElse Safe(d.OcrStructuredReport).ToLowerInvariant().Contains(s) OrElse Safe(d.OcrReferenceNumbers).ToLowerInvariant().Contains(s) OrElse Safe(d.OcrNationalIds).ToLowerInvariant().Contains(s))
        End If
        If Not String.IsNullOrWhiteSpace(criteria.GeneralCategory) AndAlso criteria.GeneralCategory <> "الكل" Then
            q = q.Where(Function(d) d.GeneralCategory = criteria.GeneralCategory)
        End If
        If Not String.IsNullOrWhiteSpace(criteria.SubCategory) AndAlso criteria.SubCategory <> "الكل" Then
            q = q.Where(Function(d) d.SubCategory = criteria.SubCategory)
        End If
        If Not String.IsNullOrWhiteSpace(criteria.Status) AndAlso criteria.Status <> "الكل" Then
            q = q.Where(Function(d) d.Status = criteria.Status)
        End If
        If Not String.IsNullOrWhiteSpace(criteria.Confidentiality) AndAlso criteria.Confidentiality <> "الكل" Then
            q = q.Where(Function(d) d.Confidentiality = criteria.Confidentiality)
        End If
        If Not String.IsNullOrWhiteSpace(criteria.Priority) AndAlso criteria.Priority <> "الكل" Then
            q = q.Where(Function(d) d.Priority = criteria.Priority)
        End If
        If Not String.IsNullOrWhiteSpace(criteria.EmployeeName) Then
            Dim e = criteria.EmployeeName.Trim().ToLowerInvariant()
            q = q.Where(Function(d) Safe(d.EmployeeName).ToLowerInvariant().Contains(e))
        End If
        If Not String.IsNullOrWhiteSpace(criteria.ReferenceNumber) Then
            Dim r = criteria.ReferenceNumber.Trim().ToLowerInvariant()
            q = q.Where(Function(d) Safe(d.ReferenceNumber).ToLowerInvariant().Contains(r))
        End If
        If Not String.IsNullOrWhiteSpace(criteria.Tags) Then
            Dim tags = criteria.Tags.Trim().ToLowerInvariant()
            q = q.Where(Function(d) Safe(d.Tags).ToLowerInvariant().Contains(tags))
        End If
        If criteria.HasDateRange Then
            Dim fromDate = criteria.DateFrom.Date
            Dim toDate = criteria.DateTo.Date
            q = q.Where(Function(d) d.CreatedAt.Date >= fromDate AndAlso d.CreatedAt.Date <= toDate)
        End If

        Return q.OrderByDescending(Function(d) d.CreatedAt).ToList()
    End Function

    Public Function GetDocumentByHash(hash As String) As DocumentRecord
        SyncLock _syncRoot
            If String.IsNullOrWhiteSpace(hash) Then Return Nothing
            Dim e = _db.Root.Element("Documents").Elements("Document").FirstOrDefault(Function(x) V(x, "FileHashSha256") = hash)
            If e Is Nothing Then Return Nothing
            Return FromElement(e)
        End SyncLock
    End Function

    Public Sub DeleteDocument(id As String, Optional deletePhysicalFile As Boolean = False)
        SyncLock _syncRoot
            Dim docs = _db.Root.Element("Documents")
            Dim e = docs.Elements("Document").FirstOrDefault(Function(x) CStr(x.Attribute("Id")) = id)
            If e Is Nothing Then Return
            Dim title = V(e, "Title")
            Dim storedFilePath = V(e, "StoredPath")
            e.Remove()
            SaveDb()
            AddAudit("حذف مستند", id, title, "حذف السجل" & If(deletePhysicalFile, " مع الملف", " دون حذف الملف"))
            If deletePhysicalFile AndAlso Not String.IsNullOrWhiteSpace(storedFilePath) AndAlso File.Exists(storedFilePath) Then
                Try
                    File.Delete(storedFilePath)
                Catch
                End Try
            End If
        End SyncLock
    End Sub

    Public Sub AddAudit(actionName As String, documentId As String, documentTitle As String, details As String)
        SyncLock _syncRoot
            Dim logs = _db.Root.Element("AuditLog")
            If logs Is Nothing Then
                logs = New XElement("AuditLog")
                _db.Root.Add(logs)
            End If
            Dim item As New AuditLogEntry With {
                .ActionName = actionName,
                .DocumentId = If(documentId, ""),
                .DocumentTitle = If(documentTitle, ""),
                .EmployeeName = _settings.CurrentEmployeeName,
                .Details = If(details, "")
            }
            logs.Add(New XElement("Log",
                New XAttribute("Id", item.Id),
                New XElement("ActionName", item.ActionName),
                New XElement("DocumentId", item.DocumentId),
                New XElement("DocumentTitle", item.DocumentTitle),
                New XElement("EmployeeName", item.EmployeeName),
                New XElement("Details", item.Details),
                New XElement("CreatedAt", item.CreatedAt.ToString("o"))
            ))
            SaveDb()
        End SyncLock
    End Sub

    Public Function GetAuditLog(Optional maxItems As Integer = 500) As List(Of AuditLogEntry)
        SyncLock _syncRoot
            Dim logs = _db.Root.Element("AuditLog")
            If logs Is Nothing Then Return New List(Of AuditLogEntry)()
            Return logs.Elements("Log").Select(Function(e) New AuditLogEntry With {
                .Id = CStr(e.Attribute("Id")),
                .ActionName = V(e, "ActionName"),
                .DocumentId = V(e, "DocumentId"),
                .DocumentTitle = V(e, "DocumentTitle"),
                .EmployeeName = V(e, "EmployeeName"),
                .Details = V(e, "Details"),
                .CreatedAt = DateValueOf(V(e, "CreatedAt"), DateTime.Now)
            }).OrderByDescending(Function(x) x.CreatedAt).Take(maxItems).ToList()
        End SyncLock
    End Function

    Public Function AuditToDataTable(records As List(Of AuditLogEntry)) As DataTable
        Dim t As New DataTable()
        t.Columns.Add("الوقت")
        t.Columns.Add("الإجراء")
        t.Columns.Add("المستند")
        t.Columns.Add("الموظف")
        t.Columns.Add("التفاصيل")
        For Each r In records
            t.Rows.Add(r.CreatedAt.ToString("yyyy/MM/dd HH:mm"), r.ActionName, r.DocumentTitle, r.EmployeeName, r.Details)
        Next
        Return t
    End Function

    Public Function ToDataTable(records As List(Of DocumentRecord)) As DataTable
        Dim t As New DataTable()
        t.Columns.Add("Id")
        t.Columns.Add("رقم الأرشيف")
        t.Columns.Add("العنوان")
        t.Columns.Add("التصنيف العام")
        t.Columns.Add("التصنيف الثانوي")
        t.Columns.Add("الأولوية")
        t.Columns.Add("السرية")
        t.Columns.Add("الحالة")
        t.Columns.Add("رقم المرجع")
        t.Columns.Add("الصفحات")
        t.Columns.Add("ثقة AI")
        t.Columns.Add("تاريخ الإضافة")
        For Each r In records
            t.Rows.Add(r.Id, If(String.IsNullOrWhiteSpace(r.ArchiveNumber), r.ReferenceNumber, r.ArchiveNumber), r.Title, r.GeneralCategory, r.SubCategory, r.Priority, r.Confidentiality, r.Status, r.ReferenceNumber, r.PageCount, r.AiConfidencePercent & "%", r.CreatedAt.ToString("yyyy/MM/dd HH:mm"))
        Next
        Return t
    End Function

    Public Function CountAll() As Integer
        SyncLock _syncRoot
            Return _db.Root.Element("Documents").Elements("Document").Count()
        End SyncLock
    End Function

    Public Function CountByStatus(status As String) As Integer
        Return GetDocuments("", "", status).Count
    End Function

    Public Function CountToday() As Integer
        Return GetDocuments().Where(Function(d) d.CreatedAt.Date = Date.Today).Count()
    End Function

    Public Function CountConfidential() As Integer
        Return GetDocuments().Where(Function(d) d.Confidentiality = "سري").Count()
    End Function


    Public Function CountByArchivePrefix(prefix As String, yearText As String) As Integer
        Dim pattern = If(prefix, "") & "-" & If(yearText, "") & "-"
        Return GetDocuments().Where(Function(d) Safe(d.ArchiveNumber).StartsWith(pattern, StringComparison.OrdinalIgnoreCase)).Count()
    End Function

    Public Sub ApplyWorkflow(documentId As String, newStatus As String, note As String)
        SyncLock _syncRoot
            Dim docs = _db.Root.Element("Documents")
            Dim e = docs.Elements("Document").FirstOrDefault(Function(x) CStr(x.Attribute("Id")) = documentId)
            If e Is Nothing Then Return
            VersionStore.CapturePrevious(documentId, e)
            Dim oldStatus = V(e, "Status")
            SetElementValue(e, "Status", If(newStatus, ""))
            SetElementValue(e, "WorkflowNote", If(note, ""))
            SetElementValue(e, "LastWorkflowBy", _settings.CurrentEmployeeName)
            SetElementValue(e, "LastWorkflowAt", DateTime.Now.ToString("o"))
            SetElementValue(e, "UpdatedAt", DateTime.Now.ToString("o"))

            Dim history = _db.Root.Element("WorkflowHistory")
            If history Is Nothing Then
                history = New XElement("WorkflowHistory")
                _db.Root.Add(history)
            End If
            Dim h As New WorkflowHistoryEntry With {
                .DocumentId = documentId,
                .OldStatus = oldStatus,
                .NewStatus = If(newStatus, ""),
                .Note = If(note, ""),
                .EmployeeName = _settings.CurrentEmployeeName
            }
            history.Add(New XElement("Workflow",
                New XAttribute("Id", h.Id),
                New XElement("DocumentId", h.DocumentId),
                New XElement("OldStatus", h.OldStatus),
                New XElement("NewStatus", h.NewStatus),
                New XElement("Note", h.Note),
                New XElement("EmployeeName", h.EmployeeName),
                New XElement("CreatedAt", h.CreatedAt.ToString("o"))
            ))
            SaveDb()
            AddAudit("تغيير حالة المستند", documentId, V(e, "Title"), oldStatus & " ← " & newStatus & If(String.IsNullOrWhiteSpace(note), "", " | " & note))
        End SyncLock
    End Sub

    Public Function GetWorkflowHistory(documentId As String) As List(Of WorkflowHistoryEntry)
        SyncLock _syncRoot
            Dim history = _db.Root.Element("WorkflowHistory")
            If history Is Nothing Then Return New List(Of WorkflowHistoryEntry)()
            Return history.Elements("Workflow").Where(Function(e) V(e, "DocumentId") = documentId).Select(Function(e) New WorkflowHistoryEntry With {
                .Id = CStr(e.Attribute("Id")),
                .DocumentId = V(e, "DocumentId"),
                .OldStatus = V(e, "OldStatus"),
                .NewStatus = V(e, "NewStatus"),
                .Note = V(e, "Note"),
                .EmployeeName = V(e, "EmployeeName"),
                .CreatedAt = DateValueOf(V(e, "CreatedAt"), DateTime.Now)
            }).OrderByDescending(Function(x) x.CreatedAt).ToList()
        End SyncLock
    End Function

    Private Shared Sub SetElementValue(parent As XElement, name As String, value As String)
        Dim child = parent.Element(name)
        If child Is Nothing Then
            parent.Add(New XElement(name, If(value, "")))
        Else
            child.Value = If(value, "")
        End If
    End Sub

    Private Shared Function Safe(v As String) As String
        Return CleanXmlText(If(v, ""))
    End Function

    Private Shared Function CleanXmlText(value As String) As String
        If String.IsNullOrEmpty(value) Then Return ""
        Dim buffer As New System.Text.StringBuilder(value.Length)
        For Each ch As Char In value
            Dim code As Integer = AscW(ch)
            If ch = ControlChars.Tab OrElse ch = ControlChars.Lf OrElse ch = ControlChars.Cr OrElse code >= 32 Then
                buffer.Append(ch)
            End If
        Next
        Return buffer.ToString()
    End Function

    Private Shared Function ToElement(d As DocumentRecord) As XElement
        Return New XElement("Document",
            New XAttribute("Id", d.Id),
            New XElement("Title", Safe(d.Title)),
            New XElement("Description", Safe(d.Description)),
            New XElement("FileName", Safe(d.FileName)),
            New XElement("OriginalPath", Safe(d.OriginalPath)),
            New XElement("StoredPath", Safe(d.StoredPath)),
            New XElement("FileType", Safe(d.FileType)),
            New XElement("TextContent", Safe(d.TextContent)),
            New XElement("Summary", Safe(d.Summary)),
            New XElement("GeneralCategory", Safe(d.GeneralCategory)),
            New XElement("SubCategory", Safe(d.SubCategory)),
            New XElement("Tags", Safe(d.Tags)),
            New XElement("ReferenceNumber", Safe(d.ReferenceNumber)),
            New XElement("Confidentiality", Safe(d.Confidentiality)),
            New XElement("Priority", Safe(d.Priority)),
            New XElement("EmployeeName", Safe(d.EmployeeName)),
            New XElement("Status", Safe(d.Status)),
            New XElement("CreatedAt", d.CreatedAt.ToString("o")),
            New XElement("UpdatedAt", d.UpdatedAt.ToString("o")),
            New XElement("FileHashSha256", Safe(d.FileHashSha256)),
            New XElement("FileSizeBytes", d.FileSizeBytes.ToString()),
            New XElement("PageCount", d.PageCount.ToString()),
            New XElement("Department", Safe(d.Department)),
            New XElement("DueDateText", Safe(d.DueDateText)),
            New XElement("AiConfidencePercent", d.AiConfidencePercent.ToString()),
            New XElement("ArchiveNumber", Safe(d.ArchiveNumber)),
            New XElement("ExternalNumber", Safe(d.ExternalNumber)),
            New XElement("SourceEntity", Safe(d.SourceEntity)),
            New XElement("DestinationEntity", Safe(d.DestinationEntity)),
            New XElement("DocumentDateText", Safe(d.DocumentDateText)),
            New XElement("WorkflowNote", Safe(d.WorkflowNote)),
            New XElement("LastWorkflowBy", Safe(d.LastWorkflowBy)),
            New XElement("LastWorkflowAt", If(d.LastWorkflowAt = DateTime.MinValue, "", d.LastWorkflowAt.ToString("o"))),
            New XElement("OcrDocumentType", Safe(d.OcrDocumentType)),
            New XElement("OcrQualityScore", d.OcrQualityScore.ToString()),
            New XElement("OcrEngineInfo", Safe(d.OcrEngineInfo)),
            New XElement("OcrDates", Safe(d.OcrDates)),
            New XElement("OcrReferenceNumbers", Safe(d.OcrReferenceNumbers)),
            New XElement("OcrIncomingNumbers", Safe(d.OcrIncomingNumbers)),
            New XElement("OcrOutgoingNumbers", Safe(d.OcrOutgoingNumbers)),
            New XElement("OcrAmounts", Safe(d.OcrAmounts)),
            New XElement("OcrNationalIds", Safe(d.OcrNationalIds)),
            New XElement("OcrIqamaNumbers", Safe(d.OcrIqamaNumbers)),
            New XElement("OcrPassportNumbers", Safe(d.OcrPassportNumbers)),
            New XElement("OcrPhoneNumbers", Safe(d.OcrPhoneNumbers)),
            New XElement("OcrEmails", Safe(d.OcrEmails)),
            New XElement("OcrNames", Safe(d.OcrNames)),
            New XElement("OcrEntities", Safe(d.OcrEntities)),
            New XElement("OcrStructuredReport", Safe(d.OcrStructuredReport)),
            New XElement("FileSignatureVerified", d.FileSignatureVerified.ToString()),
            New XElement("FileSignatureType", Safe(d.FileSignatureType)),
            New XElement("SecurityRiskScore", d.SecurityRiskScore.ToString(Globalization.CultureInfo.InvariantCulture)),
            New XElement("IsEncrypted", d.IsEncrypted.ToString()),
            New XElement("ProtectedFilePath", Safe(d.ProtectedFilePath)),
            New XElement("SecurityStatus", Safe(d.SecurityStatus))
        )
    End Function

    Private Shared Function FromElement(e As XElement) As DocumentRecord
        Dim d As New DocumentRecord()
        d.Id = CStr(e.Attribute("Id"))
        d.Title = V(e, "Title")
        d.Description = V(e, "Description")
        d.FileName = V(e, "FileName")
        d.OriginalPath = V(e, "OriginalPath")
        d.StoredPath = V(e, "StoredPath")
        d.FileType = V(e, "FileType")
        d.TextContent = V(e, "TextContent")
        d.Summary = V(e, "Summary")
        d.GeneralCategory = V(e, "GeneralCategory")
        d.SubCategory = V(e, "SubCategory")
        d.Tags = V(e, "Tags")
        d.ReferenceNumber = V(e, "ReferenceNumber")
        d.Confidentiality = V(e, "Confidentiality")
        d.Priority = V(e, "Priority")
        d.EmployeeName = V(e, "EmployeeName")
        d.Status = V(e, "Status")
        d.CreatedAt = DateValueOf(V(e, "CreatedAt"), DateTime.Now)
        d.UpdatedAt = DateValueOf(V(e, "UpdatedAt"), d.CreatedAt)
        d.FileHashSha256 = V(e, "FileHashSha256")
        Long.TryParse(V(e, "FileSizeBytes"), d.FileSizeBytes)
        Integer.TryParse(V(e, "PageCount"), d.PageCount)
        d.Department = V(e, "Department")
        d.DueDateText = V(e, "DueDateText")
        Integer.TryParse(V(e, "AiConfidencePercent"), d.AiConfidencePercent)
        d.ArchiveNumber = V(e, "ArchiveNumber")
        d.ExternalNumber = V(e, "ExternalNumber")
        d.SourceEntity = V(e, "SourceEntity")
        d.DestinationEntity = V(e, "DestinationEntity")
        d.DocumentDateText = V(e, "DocumentDateText")
        d.WorkflowNote = V(e, "WorkflowNote")
        d.LastWorkflowBy = V(e, "LastWorkflowBy")
        d.LastWorkflowAt = DateValueOf(V(e, "LastWorkflowAt"), DateTime.MinValue)
        d.OcrDocumentType = V(e, "OcrDocumentType")
        Integer.TryParse(V(e, "OcrQualityScore"), d.OcrQualityScore)
        d.OcrEngineInfo = V(e, "OcrEngineInfo")
        d.OcrDates = V(e, "OcrDates")
        d.OcrReferenceNumbers = V(e, "OcrReferenceNumbers")
        d.OcrIncomingNumbers = V(e, "OcrIncomingNumbers")
        d.OcrOutgoingNumbers = V(e, "OcrOutgoingNumbers")
        d.OcrAmounts = V(e, "OcrAmounts")
        d.OcrNationalIds = V(e, "OcrNationalIds")
        d.OcrIqamaNumbers = V(e, "OcrIqamaNumbers")
        d.OcrPassportNumbers = V(e, "OcrPassportNumbers")
        d.OcrPhoneNumbers = V(e, "OcrPhoneNumbers")
        d.OcrEmails = V(e, "OcrEmails")
        d.OcrNames = V(e, "OcrNames")
        d.OcrEntities = V(e, "OcrEntities")
        d.OcrStructuredReport = V(e, "OcrStructuredReport")
        Boolean.TryParse(V(e, "FileSignatureVerified"), d.FileSignatureVerified)
        d.FileSignatureType = V(e, "FileSignatureType")
        Double.TryParse(V(e, "SecurityRiskScore"), Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, d.SecurityRiskScore)
        Boolean.TryParse(V(e, "IsEncrypted"), d.IsEncrypted)
        d.ProtectedFilePath = V(e, "ProtectedFilePath")
        d.SecurityStatus = V(e, "SecurityStatus")
        Return d
    End Function

    Private Shared Function V(e As XElement, name As String) As String
        If e.Element(name) Is Nothing Then Return ""
        Return e.Element(name).Value
    End Function

    Private Shared Function DateValueOf(value As String, fallback As DateTime) As DateTime
        Return BasiraDates.ParseStored(value, fallback)
    End Function
End Class
