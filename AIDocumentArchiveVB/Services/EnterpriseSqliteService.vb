Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.IO
Imports System.Text
Imports System.Xml.Linq
Imports System.Data.SQLite

Public Class EnterpriseSqliteService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _dbPath As String
    Private ReadOnly _connectionString As String
    Private _isAvailable As Boolean = False
    Private _lastError As String = ""

    Public ReadOnly Property IsAvailable As Boolean
        Get
            Return _isAvailable
        End Get
    End Property

    Public ReadOnly Property LastError As String
        Get
            Return _lastError
        End Get
    End Property

    Public ReadOnly Property DatabasePath As String
        Get
            Return _dbPath
        End Get
    End Property

    Public Sub New(settings As AppSettings)
        _settings = settings
        Dim baseFolder = If(settings Is Nothing OrElse String.IsNullOrWhiteSpace(settings.BaseDataFolder), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SaeedAIDocArchive"), settings.BaseDataFolder)
        Directory.CreateDirectory(baseFolder)
        _dbPath = Path.Combine(baseFolder, "archive-enterprise-v25.sqlite")
        _connectionString = "Data Source=" & _dbPath & ";Version=3;Journal Mode=WAL;Synchronous=NORMAL;Foreign Keys=True;"
    End Sub

    Public Sub Initialize()
        Try
            If Not File.Exists(_dbPath) Then
                SQLiteConnection.CreateFile(_dbPath)
            End If
            Using cn As New SQLiteConnection(_connectionString)
                cn.Open()
                ExecuteNonQuery(cn, "PRAGMA journal_mode=WAL;")
                ExecuteNonQuery(cn, "PRAGMA synchronous=NORMAL;")
                ExecuteNonQuery(cn, "PRAGMA temp_store=MEMORY;")
                ExecuteNonQuery(cn, BuildSchemaSql())
                EnsureColumn(cn, "Documents", "FileSignatureVerified", "INTEGER DEFAULT 0")
                EnsureColumn(cn, "Documents", "FileSignatureType", "TEXT")
                EnsureColumn(cn, "Documents", "SecurityRiskScore", "REAL DEFAULT 0")
                EnsureColumn(cn, "Documents", "IsEncrypted", "INTEGER DEFAULT 0")
                EnsureColumn(cn, "Documents", "ProtectedFilePath", "TEXT")
                EnsureColumn(cn, "Documents", "SecurityStatus", "TEXT")
            End Using
            _isAvailable = True
            _lastError = ""
        Catch ex As Exception
            _isAvailable = False
            _lastError = ex.Message
        End Try
    End Sub

    Public Sub MigrateFromXmlRepository(repository As ArchiveRepository)
        If Not _isAvailable OrElse repository Is Nothing Then Return
        Try
            Dim docs = repository.GetDocuments()
            Using cn As New SQLiteConnection(_connectionString)
                cn.Open()
                Using tx = cn.BeginTransaction()
                    For Each d In docs
                        UpsertDocumentInternal(cn, d)
                        UpsertTextInternal(cn, d)
                        UpsertFastEntitiesInternal(cn, d)
                    Next
                    tx.Commit()
                End Using
            End Using
        Catch ex As Exception
            _lastError = ex.Message
        End Try
    End Sub

    Public Sub UpsertDocument(record As DocumentRecord)
        If Not _isAvailable OrElse record Is Nothing Then Return
        Try
            Using cn As New SQLiteConnection(_connectionString)
                cn.Open()
                UpsertDocumentInternal(cn, record)
                UpsertTextInternal(cn, record)
                UpsertFastEntitiesInternal(cn, record)
            End Using
        Catch ex As Exception
            _lastError = ex.Message
        End Try
    End Sub

    Public Function SearchDocuments(query As String, Optional limit As Integer = 100) As DataTable
        Dim dt As New DataTable()
        If Not _isAvailable Then Return dt
        Try
            Using cn As New SQLiteConnection(_connectionString)
                cn.Open()
                Dim sql As String = "SELECT d.Id, d.ArchiveNumber, d.Title, d.Category, d.SubCategory, d.Status, d.Confidentiality, d.Priority, d.CreatedAt " &
                                    "FROM Documents d LEFT JOIN DocumentTexts t ON d.Id=t.DocumentId " &
                                    "WHERE @q='' OR d.Title LIKE @like OR d.ArchiveNumber LIKE @like OR d.Category LIKE @like OR d.SubCategory LIKE @like OR t.CleanText LIKE @like " &
                                    "ORDER BY d.UpdatedAt DESC LIMIT @limit"
                Using cmd As New SQLiteCommand(sql, cn)
                    cmd.Parameters.AddWithValue("@q", If(query, ""))
                    cmd.Parameters.AddWithValue("@like", "%" & If(query, "") & "%")
                    cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit))
                    Using da As New SQLiteDataAdapter(cmd)
                        da.Fill(dt)
                    End Using
                End Using
            End Using
        Catch ex As Exception
            _lastError = ex.Message
        End Try
        Return dt
    End Function

    Public Function GetStats() As PerformanceSnapshot
        Dim snap As New PerformanceSnapshot()
        If Not _isAvailable Then Return snap
        Try
            Using cn As New SQLiteConnection(_connectionString)
                cn.Open()
                snap.TotalDocuments = ScalarInt(cn, "SELECT COUNT(*) FROM Documents")
                snap.MissingOcr = ScalarInt(cn, "SELECT COUNT(*) FROM Documents d LEFT JOIN DocumentTexts t ON d.Id=t.DocumentId WHERE t.CleanText IS NULL OR length(trim(t.CleanText)) < 20")
                snap.MissingClassification = ScalarInt(cn, "SELECT COUNT(*) FROM Documents WHERE Category IS NULL OR trim(Category)='' OR SubCategory IS NULL OR trim(SubCategory)=''")
                snap.WaitingReview = ScalarInt(cn, "SELECT COUNT(*) FROM Documents WHERE Status LIKE '%مراجعة%' OR Status LIKE '%انتظار%'")
            End Using
        Catch ex As Exception
            _lastError = ex.Message
        End Try
        Return snap
    End Function

    Public Sub SaveAiAnalysis(documentId As String, analysisType As String, modelName As String, promptVersion As String, textHash As String, jsonResult As String)
        If Not _isAvailable Then Return
        Try
            Using cn As New SQLiteConnection(_connectionString)
                cn.Open()
                Dim sql = "INSERT INTO AiAnalyses(DocumentId,AnalysisType,ModelName,PromptVersion,TextHash,JsonResult,CreatedAt) VALUES(@d,@t,@m,@p,@h,@j,@c)"
                Using cmd As New SQLiteCommand(sql, cn)
                    cmd.Parameters.AddWithValue("@d", If(documentId, ""))
                    cmd.Parameters.AddWithValue("@t", If(analysisType, ""))
                    cmd.Parameters.AddWithValue("@m", If(modelName, ""))
                    cmd.Parameters.AddWithValue("@p", If(promptVersion, ""))
                    cmd.Parameters.AddWithValue("@h", If(textHash, ""))
                    cmd.Parameters.AddWithValue("@j", If(jsonResult, ""))
                    cmd.Parameters.AddWithValue("@c", DateTime.Now.ToString("o"))
                    cmd.ExecuteNonQuery()
                End Using
            End Using
        Catch ex As Exception
            _lastError = ex.Message
        End Try
    End Sub

    Public Function LatestAiAnalysis(documentId As String, analysisType As String, textHash As String) As String
        If Not _isAvailable Then Return ""
        Try
            Using cn As New SQLiteConnection(_connectionString)
                cn.Open()
                Dim sql = "SELECT JsonResult FROM AiAnalyses WHERE DocumentId=@d AND AnalysisType=@t AND TextHash=@h ORDER BY Id DESC LIMIT 1"
                Using cmd As New SQLiteCommand(sql, cn)
                    cmd.Parameters.AddWithValue("@d", If(documentId, ""))
                    cmd.Parameters.AddWithValue("@t", If(analysisType, ""))
                    cmd.Parameters.AddWithValue("@h", If(textHash, ""))
                    Dim v = cmd.ExecuteScalar()
                    If v IsNot Nothing AndAlso v IsNot DBNull.Value Then Return v.ToString()
                End Using
            End Using
        Catch ex As Exception
            _lastError = ex.Message
        End Try
        Return ""
    End Function

    Private Sub UpsertDocumentInternal(cn As SQLiteConnection, d As DocumentRecord)
        Dim sql As String = "INSERT OR REPLACE INTO Documents(Id,ArchiveNumber,Title,FilePath,FileHash,Category,SubCategory,Status,Confidentiality,Priority,CreatedAt,UpdatedAt,SourceEntity,DestinationEntity,ReferenceNumber,FileSignatureVerified,FileSignatureType,SecurityRiskScore,IsEncrypted,ProtectedFilePath,SecurityStatus) " &
                            "VALUES(@Id,@ArchiveNumber,@Title,@FilePath,@FileHash,@Category,@SubCategory,@Status,@Confidentiality,@Priority,@CreatedAt,@UpdatedAt,@SourceEntity,@DestinationEntity,@ReferenceNumber,@FileSignatureVerified,@FileSignatureType,@SecurityRiskScore,@IsEncrypted,@ProtectedFilePath,@SecurityStatus)"
        Using cmd As New SQLiteCommand(sql, cn)
            cmd.Parameters.AddWithValue("@Id", d.Id)
            cmd.Parameters.AddWithValue("@ArchiveNumber", Safe(d.ArchiveNumber))
            cmd.Parameters.AddWithValue("@Title", Safe(d.Title))
            cmd.Parameters.AddWithValue("@FilePath", Safe(If(Not String.IsNullOrWhiteSpace(d.StoredPath), d.StoredPath, d.OriginalPath)))
            cmd.Parameters.AddWithValue("@FileHash", Safe(d.FileHashSha256))
            cmd.Parameters.AddWithValue("@Category", Safe(d.GeneralCategory))
            cmd.Parameters.AddWithValue("@SubCategory", Safe(d.SubCategory))
            cmd.Parameters.AddWithValue("@Status", Safe(d.Status))
            cmd.Parameters.AddWithValue("@Confidentiality", Safe(d.Confidentiality))
            cmd.Parameters.AddWithValue("@Priority", Safe(d.Priority))
            cmd.Parameters.AddWithValue("@CreatedAt", d.CreatedAt.ToString("o"))
            cmd.Parameters.AddWithValue("@UpdatedAt", d.UpdatedAt.ToString("o"))
            cmd.Parameters.AddWithValue("@SourceEntity", Safe(d.SourceEntity))
            cmd.Parameters.AddWithValue("@DestinationEntity", Safe(d.DestinationEntity))
            cmd.Parameters.AddWithValue("@ReferenceNumber", Safe(d.ReferenceNumber))
            cmd.Parameters.AddWithValue("@FileSignatureVerified", If(d.FileSignatureVerified, 1, 0))
            cmd.Parameters.AddWithValue("@FileSignatureType", Safe(d.FileSignatureType))
            cmd.Parameters.AddWithValue("@SecurityRiskScore", d.SecurityRiskScore)
            cmd.Parameters.AddWithValue("@IsEncrypted", If(d.IsEncrypted, 1, 0))
            cmd.Parameters.AddWithValue("@ProtectedFilePath", Safe(d.ProtectedFilePath))
            cmd.Parameters.AddWithValue("@SecurityStatus", Safe(d.SecurityStatus))
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub UpsertTextInternal(cn As SQLiteConnection, d As DocumentRecord)
        Dim text = If(d.TextContent, "")
        Dim clean = SpeedPromptService.NormalizeText(text)
        Dim textHash = ComputeTextHash(clean)
        Dim sql = "INSERT OR REPLACE INTO DocumentTexts(DocumentId,OcrText,CleanText,TextHash,OcrEngine,OcrStatus,UpdatedAt) VALUES(@d,@o,@c,@h,@e,@s,@u)"
        Using cmd As New SQLiteCommand(sql, cn)
            cmd.Parameters.AddWithValue("@d", d.Id)
            cmd.Parameters.AddWithValue("@o", text)
            cmd.Parameters.AddWithValue("@c", clean)
            cmd.Parameters.AddWithValue("@h", textHash)
            cmd.Parameters.AddWithValue("@e", Safe(d.OcrEngineInfo))
            cmd.Parameters.AddWithValue("@s", If(String.IsNullOrWhiteSpace(clean), "MISSING", "READY"))
            cmd.Parameters.AddWithValue("@u", DateTime.Now.ToString("o"))
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub UpsertFastEntitiesInternal(cn As SQLiteConnection, d As DocumentRecord)
        ExecuteNonQuery(cn, "DELETE FROM DocumentEntities WHERE DocumentId='" & EscapeSql(d.Id) & "'")
        AddEntityList(cn, d.Id, "Date", d.OcrDates, "local")
        AddEntityList(cn, d.Id, "Reference", d.OcrReferenceNumbers, "local")
        AddEntityList(cn, d.Id, "Incoming", d.OcrIncomingNumbers, "local")
        AddEntityList(cn, d.Id, "Outgoing", d.OcrOutgoingNumbers, "local")
        AddEntityList(cn, d.Id, "Amount", d.OcrAmounts, "local")
        AddEntityList(cn, d.Id, "NationalId", d.OcrNationalIds, "local")
        AddEntityList(cn, d.Id, "Iqama", d.OcrIqamaNumbers, "local")
        AddEntityList(cn, d.Id, "Passport", d.OcrPassportNumbers, "local")
        AddEntityList(cn, d.Id, "Phone", d.OcrPhoneNumbers, "local")
        AddEntityList(cn, d.Id, "Email", d.OcrEmails, "local")
        AddEntityList(cn, d.Id, "Person", d.OcrNames, "local")
        AddEntityList(cn, d.Id, "Organization", d.OcrEntities, "local")
    End Sub

    Private Sub AddEntityList(cn As SQLiteConnection, documentId As String, entityType As String, values As String, source As String)
        If String.IsNullOrWhiteSpace(values) Then Return
        For Each raw In values.Split(New Char() {";"c, ","c, ChrW(10)}, StringSplitOptions.RemoveEmptyEntries)
            Dim v = raw.Trim()
            If v.Length = 0 Then Continue For
            Using cmd As New SQLiteCommand("INSERT INTO DocumentEntities(DocumentId,EntityType,EntityValue,Confidence,Source) VALUES(@d,@t,@v,@c,@s)", cn)
                cmd.Parameters.AddWithValue("@d", documentId)
                cmd.Parameters.AddWithValue("@t", entityType)
                cmd.Parameters.AddWithValue("@v", v)
                cmd.Parameters.AddWithValue("@c", 0.85)
                cmd.Parameters.AddWithValue("@s", source)
                cmd.ExecuteNonQuery()
            End Using
        Next
    End Sub

    Private Shared Function BuildSchemaSql() As String
        Return "CREATE TABLE IF NOT EXISTS Documents(" &
               "Id TEXT PRIMARY KEY,ArchiveNumber TEXT,Title TEXT,FilePath TEXT,FileHash TEXT,Category TEXT,SubCategory TEXT,Status TEXT,Confidentiality TEXT,Priority TEXT,CreatedAt TEXT,UpdatedAt TEXT,SourceEntity TEXT,DestinationEntity TEXT,ReferenceNumber TEXT);" &
               "CREATE INDEX IF NOT EXISTS IX_Documents_ArchiveNumber ON Documents(ArchiveNumber);" &
               "CREATE INDEX IF NOT EXISTS IX_Documents_Category ON Documents(Category,SubCategory);" &
               "CREATE TABLE IF NOT EXISTS DocumentTexts(DocumentId TEXT PRIMARY KEY,OcrText TEXT,CleanText TEXT,TextHash TEXT,OcrEngine TEXT,OcrStatus TEXT,UpdatedAt TEXT);" &
               "CREATE INDEX IF NOT EXISTS IX_DocumentTexts_TextHash ON DocumentTexts(TextHash);" &
               "CREATE TABLE IF NOT EXISTS DocumentEntities(Id INTEGER PRIMARY KEY AUTOINCREMENT,DocumentId TEXT,EntityType TEXT,EntityValue TEXT,Confidence REAL,Source TEXT);" &
               "CREATE INDEX IF NOT EXISTS IX_Entities_TypeValue ON DocumentEntities(EntityType,EntityValue);" &
               "CREATE TABLE IF NOT EXISTS AiAnalyses(Id INTEGER PRIMARY KEY AUTOINCREMENT,DocumentId TEXT,ModelName TEXT,PromptVersion TEXT,TextHash TEXT,AnalysisType TEXT,JsonResult TEXT,CreatedAt TEXT);" &
               "CREATE INDEX IF NOT EXISTS IX_AiAnalyses_Cache ON AiAnalyses(DocumentId,AnalysisType,TextHash);" &
               "CREATE TABLE IF NOT EXISTS ProcessingJobs(Id TEXT PRIMARY KEY,JobType TEXT,DocumentId TEXT,Status TEXT,Progress INTEGER,Message TEXT,StartedAt TEXT,FinishedAt TEXT,ErrorMessage TEXT);" &
               "CREATE TABLE IF NOT EXISTS AuditLog(Id INTEGER PRIMARY KEY AUTOINCREMENT,UserName TEXT,Action TEXT,DocumentId TEXT,Details TEXT,CreatedAt TEXT);"
    End Function

    Private Shared Sub EnsureColumn(cn As SQLiteConnection, tableName As String, columnName As String, columnDefinition As String)
        Using cmd As New SQLiteCommand("PRAGMA table_info(" & tableName & ")", cn)
            Using reader = cmd.ExecuteReader()
                While reader.Read()
                    If String.Equals(reader("name").ToString(), columnName, StringComparison.OrdinalIgnoreCase) Then Return
                End While
            End Using
        End Using
        ExecuteNonQuery(cn, "ALTER TABLE " & tableName & " ADD COLUMN " & columnName & " " & columnDefinition)
    End Sub

    Private Shared Sub ExecuteNonQuery(cn As SQLiteConnection, sql As String)
        Using cmd As New SQLiteCommand(sql, cn)
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Function ScalarInt(cn As SQLiteConnection, sql As String) As Integer
        Using cmd As New SQLiteCommand(sql, cn)
            Dim v = cmd.ExecuteScalar()
            If v Is Nothing OrElse v Is DBNull.Value Then Return 0
            Dim n As Integer
            If Integer.TryParse(v.ToString(), n) Then Return n
        End Using
        Return 0
    End Function

    Private Shared Function Safe(value As String) As String
        Return If(value, "")
    End Function

    Private Shared Function EscapeSql(value As String) As String
        Return If(value, "").Replace("'", "''")
    End Function

    Private Shared Function ComputeTextHash(text As String) As String
        Using sha = System.Security.Cryptography.SHA256.Create()
            Dim bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(If(text, "")))
            Dim sb As New StringBuilder(bytes.Length * 2)
            For Each b In bytes
                sb.Append(b.ToString("x2"))
            Next
            Return sb.ToString()
        End Using
    End Function
End Class
