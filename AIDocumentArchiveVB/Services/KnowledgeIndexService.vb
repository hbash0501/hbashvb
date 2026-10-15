Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

Public Class KnowledgeIndexService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _repository As ArchiveRepository
    Private ReadOnly _indexPath As String

    Public Sub New(settings As AppSettings, repository As ArchiveRepository)
        _settings = settings
        _repository = repository
        Directory.CreateDirectory(_settings.BaseDataFolder)
        _indexPath = Path.Combine(_settings.BaseDataFolder, "knowledge-index.xml")
    End Sub

    Public Function RebuildIndex() As Integer
        Dim root As New XElement("KnowledgeIndex")
        Dim count As Integer = 0
        For Each d In _repository.GetDocuments()
            root.Add(ToKnowledgeElement(d))
            count += 1
        Next
        Dim doc As New XDocument(root)
        doc.Save(_indexPath)
        _repository.AddAudit("إعادة بناء فهرس المعرفة", "", "", "عدد المستندات المفهرسة: " & count)
        Return count
    End Function

    Public Sub IndexDocument(document As DocumentRecord)
        If document Is Nothing Then Return
        Dim doc = LoadIndex()
        Dim root = doc.Root
        Dim oldItem = root.Elements("DocumentKnowledge").FirstOrDefault(Function(x) CStr(x.Attribute("DocumentId")) = document.Id)
        If oldItem IsNot Nothing Then oldItem.Remove()
        root.Add(ToKnowledgeElement(document))
        doc.Save(_indexPath)
    End Sub

    Public Function Search(query As String, Optional maxResults As Integer = 20) As List(Of KnowledgeSearchResult)
        Dim records = _repository.GetDocuments()
        Dim terms = Tokenize(query)
        If terms.Count = 0 Then
            Return records.Take(maxResults).Select(Function(d) New KnowledgeSearchResult With {.Document = d, .Score = 1, .Evidence = "آخر المستندات"}).ToList()
        End If

        Dim results As New List(Of KnowledgeSearchResult)()
        For Each d In records
            Dim body = BuildSearchText(d)
            Dim score As Integer = 0
            Dim evidence As New List(Of String)()
            For Each term In terms
                If body.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    score += 8
                    evidence.Add(term)
                Else
                    Dim fuzzy = FindRelatedEvidence(body, term)
                    If fuzzy <> "" Then
                        score += 3
                        evidence.Add(fuzzy)
                    End If
                End If
            Next
            If Regex.IsMatch(body, "\b" & Regex.Escape(query.Trim()) & "\b", RegexOptions.IgnoreCase) Then score += 20
            If score > 0 Then
                results.Add(New KnowledgeSearchResult With {.Document = d, .Score = score, .Evidence = String.Join("، ", evidence.Distinct().Take(8).ToArray())})
            End If
        Next
        Return results.OrderByDescending(Function(r) r.Score).ThenByDescending(Function(r) r.Document.CreatedAt).Take(maxResults).ToList()
    End Function

    Public Function ResultsToDataTable(results As List(Of KnowledgeSearchResult)) As DataTable
        Dim t As New DataTable()
        t.Columns.Add("Id")
        t.Columns.Add("الدرجة")
        t.Columns.Add("رقم الأرشيف")
        t.Columns.Add("العنوان")
        t.Columns.Add("الجهة")
        t.Columns.Add("التصنيف")
        t.Columns.Add("الدليل")
        t.Columns.Add("التاريخ")
        If results Is Nothing Then Return t
        For Each r In results
            Dim d = r.Document
            t.Rows.Add(d.Id, r.Score, If(String.IsNullOrWhiteSpace(d.ArchiveNumber), d.ReferenceNumber, d.ArchiveNumber), d.Title, If(String.IsNullOrWhiteSpace(d.SourceEntity), d.DestinationEntity, d.SourceEntity), d.GeneralCategory & " / " & d.SubCategory, r.Evidence, d.CreatedAt.ToString("yyyy/MM/dd"))
        Next
        Return t
    End Function

    Public Function BuildContext(results As List(Of KnowledgeSearchResult), maxChars As Integer) As String
        Dim sb As New StringBuilder()
        If results Is Nothing Then Return ""
        Dim index As Integer = 1
        For Each r In results
            Dim d = r.Document
            sb.AppendLine("[" & index & "] " & d.Title)
            sb.AppendLine("رقم الأرشيف: " & d.ArchiveNumber & " | المرجع: " & d.ReferenceNumber)
            sb.AppendLine("التصنيف: " & d.GeneralCategory & " / " & d.SubCategory)
            sb.AppendLine("الجهة الصادرة: " & d.SourceEntity & " | الجهة المستقبلة: " & d.DestinationEntity)
            sb.AppendLine("الأرقام: وارد=" & d.OcrIncomingNumbers & " | صادر=" & d.OcrOutgoingNumbers & " | مرجع=" & d.OcrReferenceNumbers)
            sb.AppendLine("تواريخ: " & d.OcrDates & " | مبالغ: " & d.OcrAmounts)
            sb.AppendLine("أسماء: " & d.OcrNames & " | جهات: " & d.OcrEntities)
            sb.AppendLine("ملخص: " & LimitText(d.Summary, 450))
            sb.AppendLine("نص مختصر: " & LimitText(d.TextContent, 900))
            sb.AppendLine("---")
            index += 1
            If sb.Length >= maxChars Then Exit For
        Next
        Return LimitText(sb.ToString(), maxChars)
    End Function

    Public Function ExportSqliteSchema(folderPath As String) As String
        Directory.CreateDirectory(folderPath)
        Dim schemaPath = Path.Combine(folderPath, "sqlite_schema_v9.sql")
        File.WriteAllText(schemaPath, BuildSqliteSchema(), New UTF8Encoding(False))
        Return schemaPath
    End Function

    Public Function ExportMigrationSql(folderPath As String) As String
        Directory.CreateDirectory(folderPath)
        Dim sqlPath = Path.Combine(folderPath, "xml_to_sqlite_migration_preview_v9.sql")
        Dim sb As New StringBuilder()
        sb.AppendLine(BuildSqliteSchema())
        sb.AppendLine()
        sb.AppendLine("BEGIN TRANSACTION;")
        For Each d In _repository.GetDocuments()
            sb.AppendLine("INSERT OR REPLACE INTO documents (id, archive_number, title, file_name, stored_path, general_category, sub_category, status, confidentiality, priority, source_entity, destination_entity, document_date_text, reference_number, summary, text_content, created_at, updated_at) VALUES (" &
                Sql(d.Id) & ", " & Sql(d.ArchiveNumber) & ", " & Sql(d.Title) & ", " & Sql(d.FileName) & ", " & Sql(d.StoredPath) & ", " & Sql(d.GeneralCategory) & ", " & Sql(d.SubCategory) & ", " & Sql(d.Status) & ", " & Sql(d.Confidentiality) & ", " & Sql(d.Priority) & ", " & Sql(d.SourceEntity) & ", " & Sql(d.DestinationEntity) & ", " & Sql(d.DocumentDateText) & ", " & Sql(d.ReferenceNumber) & ", " & Sql(d.Summary) & ", " & Sql(d.TextContent) & ", " & Sql(d.CreatedAt.ToString("o")) & ", " & Sql(d.UpdatedAt.ToString("o")) & ");")
            For Each ent In ExtractEntities(d)
                sb.AppendLine("INSERT INTO document_entities (id, document_id, entity_type, entity_value, source_name, confidence_percent, created_at) VALUES (" & Sql(ent.Id) & ", " & Sql(ent.DocumentId) & ", " & Sql(ent.EntityType) & ", " & Sql(ent.EntityValue) & ", " & Sql(ent.SourceName) & ", " & ent.ConfidencePercent & ", " & Sql(ent.CreatedAt.ToString("o")) & ");")
            Next
        Next
        sb.AppendLine("COMMIT;")
        File.WriteAllText(sqlPath, sb.ToString(), New UTF8Encoding(False))
        Return sqlPath
    End Function

    Public Function BuildSqliteSchema() As String
        Dim sb As New StringBuilder()
        sb.AppendLine("PRAGMA foreign_keys = ON;")
        sb.AppendLine("CREATE TABLE IF NOT EXISTS documents (")
        sb.AppendLine("  id TEXT PRIMARY KEY,")
        sb.AppendLine("  archive_number TEXT,")
        sb.AppendLine("  title TEXT,")
        sb.AppendLine("  file_name TEXT,")
        sb.AppendLine("  stored_path TEXT,")
        sb.AppendLine("  general_category TEXT,")
        sb.AppendLine("  sub_category TEXT,")
        sb.AppendLine("  status TEXT,")
        sb.AppendLine("  confidentiality TEXT,")
        sb.AppendLine("  priority TEXT,")
        sb.AppendLine("  source_entity TEXT,")
        sb.AppendLine("  destination_entity TEXT,")
        sb.AppendLine("  document_date_text TEXT,")
        sb.AppendLine("  reference_number TEXT,")
        sb.AppendLine("  summary TEXT,")
        sb.AppendLine("  text_content TEXT,")
        sb.AppendLine("  created_at TEXT,")
        sb.AppendLine("  updated_at TEXT")
        sb.AppendLine(");")
        sb.AppendLine("CREATE TABLE IF NOT EXISTS document_entities (")
        sb.AppendLine("  id TEXT PRIMARY KEY,")
        sb.AppendLine("  document_id TEXT NOT NULL,")
        sb.AppendLine("  entity_type TEXT NOT NULL,")
        sb.AppendLine("  entity_value TEXT NOT NULL,")
        sb.AppendLine("  source_name TEXT,")
        sb.AppendLine("  confidence_percent INTEGER,")
        sb.AppendLine("  created_at TEXT,")
        sb.AppendLine("  FOREIGN KEY(document_id) REFERENCES documents(id) ON DELETE CASCADE")
        sb.AppendLine(");")
        sb.AppendLine("CREATE TABLE IF NOT EXISTS ai_extractions (id TEXT PRIMARY KEY, document_id TEXT, raw_json TEXT, created_at TEXT);")
        sb.AppendLine("CREATE TABLE IF NOT EXISTS document_links (id TEXT PRIMARY KEY, source_document_id TEXT, target_document_id TEXT, relation_type TEXT, confidence_percent INTEGER, reason TEXT, created_at TEXT);")
        sb.AppendLine("CREATE TABLE IF NOT EXISTS audit_log (id TEXT PRIMARY KEY, action_name TEXT, document_id TEXT, employee_name TEXT, details TEXT, created_at TEXT);")
        sb.AppendLine("CREATE INDEX IF NOT EXISTS idx_documents_archive_number ON documents(archive_number);")
        sb.AppendLine("CREATE INDEX IF NOT EXISTS idx_documents_category ON documents(general_category, sub_category);")
        sb.AppendLine("CREATE INDEX IF NOT EXISTS idx_entities_type_value ON document_entities(entity_type, entity_value);")
        sb.AppendLine("CREATE VIRTUAL TABLE IF NOT EXISTS documents_fts USING fts5(title, summary, text_content, content='documents', content_rowid='rowid');")
        Return sb.ToString()
    End Function

    Private Function LoadIndex() As XDocument
        If File.Exists(_indexPath) Then
            Try
                Return XDocument.Load(_indexPath)
            Catch
            End Try
        End If
        Return New XDocument(New XElement("KnowledgeIndex"))
    End Function

    Private Function ToKnowledgeElement(d As DocumentRecord) As XElement
        Dim item As New XElement("DocumentKnowledge",
            New XAttribute("DocumentId", d.Id),
            New XElement("Title", Safe(d.Title)),
            New XElement("ArchiveNumber", Safe(d.ArchiveNumber)),
            New XElement("ReferenceNumber", Safe(d.ReferenceNumber)),
            New XElement("Category", Safe(d.GeneralCategory & " / " & d.SubCategory)),
            New XElement("Summary", Safe(d.Summary)),
            New XElement("SearchText", Safe(BuildSearchText(d))),
            New XElement("IndexedAt", DateTime.Now.ToString("o"))
        )
        Dim entities = New XElement("Entities")
        For Each ent In ExtractEntities(d)
            entities.Add(New XElement("Entity",
                New XAttribute("Type", ent.EntityType),
                New XAttribute("Confidence", ent.ConfidencePercent),
                New XAttribute("Source", ent.SourceName),
                Safe(ent.EntityValue)))
        Next
        item.Add(entities)
        Return item
    End Function

    Public Function ExtractEntities(d As DocumentRecord) As List(Of KnowledgeEntity)
        Dim list As New List(Of KnowledgeEntity)()
        AddEntities(list, d.Id, "date", d.OcrDates, "OCR/AI")
        AddEntities(list, d.Id, "incoming_number", d.OcrIncomingNumbers, "OCR/AI")
        AddEntities(list, d.Id, "outgoing_number", d.OcrOutgoingNumbers, "OCR/AI")
        AddEntities(list, d.Id, "reference_number", d.OcrReferenceNumbers & "،" & d.ReferenceNumber, "OCR/AI")
        AddEntities(list, d.Id, "amount", d.OcrAmounts, "OCR/AI")
        AddEntities(list, d.Id, "national_id", d.OcrNationalIds, "OCR/AI")
        AddEntities(list, d.Id, "iqama_number", d.OcrIqamaNumbers, "OCR/AI")
        AddEntities(list, d.Id, "passport_number", d.OcrPassportNumbers, "OCR/AI")
        AddEntities(list, d.Id, "person", d.OcrNames, "OCR/AI")
        AddEntities(list, d.Id, "organization", d.OcrEntities & "،" & d.SourceEntity & "،" & d.DestinationEntity, "OCR/AI")
        AddEntities(list, d.Id, "phone", d.OcrPhoneNumbers, "OCR/AI")
        AddEntities(list, d.Id, "email", d.OcrEmails, "OCR/AI")
        Return list
    End Function

    Private Shared Sub AddEntities(list As List(Of KnowledgeEntity), docId As String, entityType As String, rawValues As String, source As String)
        If String.IsNullOrWhiteSpace(rawValues) Then Return
        Dim parts = Regex.Split(rawValues, "[،,;\r\n]+")
        For Each p In parts
            Dim value = Regex.Replace(If(p, ""), "\s+", " ").Trim()
            If value = "" Then Continue For
            If Not list.Any(Function(e) e.EntityType = entityType AndAlso e.EntityValue = value) Then
                list.Add(New KnowledgeEntity With {.DocumentId = docId, .EntityType = entityType, .EntityValue = value, .SourceName = source, .ConfidencePercent = 80})
            End If
        Next
    End Sub

    Private Shared Function BuildSearchText(d As DocumentRecord) As String
        Dim sb As New StringBuilder()
        sb.AppendLine(d.Title)
        sb.AppendLine(d.Description)
        sb.AppendLine(d.ArchiveNumber)
        sb.AppendLine(d.ReferenceNumber)
        sb.AppendLine(d.ExternalNumber)
        sb.AppendLine(d.GeneralCategory & " " & d.SubCategory)
        sb.AppendLine(d.SourceEntity & " " & d.DestinationEntity)
        sb.AppendLine(d.Tags)
        sb.AppendLine(d.Summary)
        sb.AppendLine(d.TextContent)
        sb.AppendLine(d.OcrStructuredReport)
        sb.AppendLine(d.OcrDates & " " & d.OcrReferenceNumbers & " " & d.OcrIncomingNumbers & " " & d.OcrOutgoingNumbers)
        sb.AppendLine(d.OcrAmounts & " " & d.OcrNames & " " & d.OcrEntities)
        Return sb.ToString()
    End Function

    Private Shared Function Tokenize(query As String) As List(Of String)
        Dim q = Regex.Replace(If(query, ""), "[^\u0600-\u06FFA-Za-z0-9]+", " ").Trim()
        Return q.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries).Where(Function(t) t.Length >= 2).Distinct().Take(12).ToList()
    End Function

    Private Shared Function FindRelatedEvidence(body As String, term As String) As String
        Dim thesaurus As New Dictionary(Of String, String()) From {
            {"اجازة", New String() {"غياب", "دوام", "مباشرة", "عذر"}},
            {"إجازة", New String() {"غياب", "دوام", "مباشرة", "عذر"}},
            {"مالي", New String() {"فاتورة", "صرف", "ميزانية", "مبلغ"}},
            {"معاملة", New String() {"خطاب", "وارد", "صادر", "قيد"}},
            {"هوية", New String() {"سجل مدني", "رقم الهوية"}},
            {"مؤسسة", New String() {"شركة", "جهة", "إدارة", "وزارة"}}
        }
        If Not thesaurus.ContainsKey(term) Then Return ""
        For Each alt In thesaurus(term)
            If body.IndexOf(alt, StringComparison.OrdinalIgnoreCase) >= 0 Then Return alt
        Next
        Return ""
    End Function

    Private Shared Function LimitText(value As String, maxChars As Integer) As String
        Dim s = If(value, "").Trim()
        If s.Length <= maxChars Then Return s
        Return s.Substring(0, maxChars).Trim() & "..."
    End Function

    Private Shared Function Sql(value As String) As String
        If value Is Nothing Then Return "NULL"
        Return "'" & value.Replace("'", "''") & "'"
    End Function

    Private Shared Function Safe(value As String) As String
        If value Is Nothing Then Return ""
        Dim sb As New StringBuilder(value.Length)
        For Each ch As Char In value
            Dim code = AscW(ch)
            If ch = ControlChars.Tab OrElse ch = ControlChars.Cr OrElse ch = ControlChars.Lf OrElse code >= 32 Then sb.Append(ch)
        Next
        Return sb.ToString()
    End Function
End Class
