Imports System
Imports System.IO
Imports System.Threading

Public Class DocumentWorkspaceOrchestrator
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _repository As ArchiveRepository
    Private ReadOnly _queue As ProcessingJobQueueService
    Private ReadOnly _speed As SpeedEngineService
    Private ReadOnly _aiGateway As AiGatewayService
    Private ReadOnly _sqlite As EnterpriseSqliteService

    Public Sub New(settings As AppSettings, repository As ArchiveRepository, queue As ProcessingJobQueueService, speed As SpeedEngineService, aiGateway As AiGatewayService, sqlite As EnterpriseSqliteService)
        _settings = settings
        _repository = repository
        _queue = queue
        _speed = speed
        _aiGateway = aiGateway
        _sqlite = sqlite
    End Sub

    Public Function ImportFast(filePath As String, Optional onImported As Action(Of ImportResult) = Nothing) As DocumentRecord
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Throw New FileNotFoundException("لم يتم العثور على الملف المحدد.", filePath)
        Dim importer As New DocumentImportService(_settings, _repository, AppServices.Ai)
        Dim result = importer.ImportFile(filePath, False, "بانتظار المعالجة")
        If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.DocumentId) Then Throw New Exception("تعذر إدراج المستند: " & If(result Is Nothing, "", result.Message))
        Dim d = _repository.GetDocumentById(result.DocumentId)
        If d Is Nothing Then Throw New Exception("تم الإدراج لكن تعذر قراءة سجل المستند.")
        d.Status = "بانتظار المعالجة"
        d.UpdatedAt = DateTime.Now
        _repository.SaveDocument(d)
        If _sqlite IsNot Nothing Then _sqlite.UpsertDocument(d)
        If _settings IsNot Nothing AndAlso _settings.EnableAutomaticProcessing AndAlso AppServices.IntelligentAutomation IsNot Nothing Then
            AppServices.IntelligentAutomation.EnqueueDocumentAutomation(d.Id, "المسار الإداري المتوازن")
        Else
            EnqueueOcrAndFastUnderstanding(d.Id)
        End If
        If onImported IsNot Nothing Then onImported(result)
        Return d
    End Function

    Public Sub EnqueueOcrAndFastUnderstanding(documentId As String)
        If String.IsNullOrWhiteSpace(documentId) Then Return
        Dim queuedDocument = _repository.GetDocumentById(documentId)
        _queue.Enqueue("معالجة: " & If(queuedDocument Is Nothing, documentId, queuedDocument.Title), "FAST_PIPELINE", documentId, "",
            Function(job, token)
                _speed.ExecuteQuickOcr(documentId, job, token, 70)
                token.ThrowIfCancellationRequested()
                Dim d = _repository.GetDocumentById(documentId)
                If d Is Nothing Then Return "المستند غير موجود."
                _queue.Report(job, "استخراج الكيانات المحلية السريعة...", 74)
                FastEntityExtractionService.ApplyFastEntities(d)
                _queue.Report(job, "الفهم والتصنيف عند الحاجة...", 82)
                Dim needsAi = String.IsNullOrWhiteSpace(d.Summary) OrElse String.IsNullOrWhiteSpace(d.GeneralCategory) OrElse d.AiConfidencePercent < 70
                If needsAi AndAlso _aiGateway IsNot Nothing Then
                    Dim ai = _aiGateway.AnalyzeQuick(d, token)
                    d.OcrStructuredReport = AppendSection(d.OcrStructuredReport, "تحليل سريع", ai)
                    d.Summary = PickSummary(ai, d.Summary)
                    d.AiConfidencePercent = Math.Max(d.AiConfidencePercent, 75)
                End If
                token.ThrowIfCancellationRequested()
                _queue.Report(job, "حفظ النتائج وتحديث فهرس الأرشيف...", 96)
                d.Status = If(String.IsNullOrWhiteSpace(d.GeneralCategory), "يحتاج تصنيف", "جاهز للمراجعة")
                d.UpdatedAt = DateTime.Now
                _repository.SaveDocument(d)
                If _sqlite IsNot Nothing Then _sqlite.UpsertDocument(d)
                _repository.AddAudit("معالجة خلفية", d.Id, d.Title, "OCR/Entities/QuickAI")
                _queue.Report(job, "اكتملت المعالجة السريعة.", 100)
                Return "اكتملت المعالجة السريعة."
            End Function)
    End Sub

    Public Function EnqueueDeepUnderstanding(documentId As String) As ProcessingJob
        Return _queue.Enqueue("فهم عميق", "DEEP_UNDERSTANDING", documentId, "",
            Function(job, token)
                Dim d = _repository.GetDocumentById(documentId)
                If d Is Nothing Then Return "المستند غير موجود."
                _queue.Report(job, "تجهيز سياق مختصر للفهم العميق...", 20)
                Dim result = _aiGateway.AnalyzeDeep(d, token)
                _queue.Report(job, "حفظ النتيجة وربطها بالمستند...", 80)
                d.OcrStructuredReport = AppendSection(d.OcrStructuredReport, "فهم عميق", result)
                d.Summary = PickSummary(result, d.Summary)
                d.UpdatedAt = DateTime.Now
                _repository.SaveDocument(d)
                If _sqlite IsNot Nothing Then _sqlite.UpsertDocument(d)
                Return "تم إنشاء فهم عميق."
            End Function)
    End Function

    Public Function EnqueueReply(documentId As String, tone As String) As ProcessingJob
        Return _queue.Enqueue("إنشاء رد ذكي", "SMART_REPLY", documentId, "",
            Function(job, token)
                Dim d = _repository.GetDocumentById(documentId)
                If d Is Nothing Then Return "المستند غير موجود."
                _queue.Report(job, "تجهيز بيانات الرد دون إرسال كامل المستند...", 25)
                Dim result = _aiGateway.GenerateReply(d, tone, token)
                _queue.Report(job, "حفظ الرد في سجل التحليل...", 85)
                d.OcrStructuredReport = AppendSection(d.OcrStructuredReport, "رد ذكي - " & tone, result)
                d.UpdatedAt = DateTime.Now
                _repository.SaveDocument(d)
                If _sqlite IsNot Nothing Then _sqlite.UpsertDocument(d)
                Return "تم إنشاء الرد الذكي وحفظه في سجل المستند."
            End Function)
    End Function

    Public Function BuildPerformanceSnapshot() As PerformanceSnapshot
        Dim snap As PerformanceSnapshot = Nothing
        If _sqlite IsNot Nothing AndAlso _sqlite.IsAvailable Then snap = _sqlite.GetStats()
        If snap Is Nothing Then snap = New PerformanceSnapshot()
        Dim docs = _repository.GetDocuments()
        snap.TotalDocuments = Math.Max(snap.TotalDocuments, docs.Count)
        snap.ActiveJobs = If(_queue Is Nothing, 0, _queue.GetActiveCount())
        Dim missingOcr As Integer = 0
        Dim missingClass As Integer = 0
        Dim waiting As Integer = 0
        Dim totalScore As Integer = 0
        For Each d In docs
            If String.IsNullOrWhiteSpace(d.TextContent) Then missingOcr += 1
            If String.IsNullOrWhiteSpace(d.GeneralCategory) OrElse String.IsNullOrWhiteSpace(d.SubCategory) Then missingClass += 1
            If Safe(d.Status).Contains("مراجعة") OrElse Safe(d.Status).Contains("انتظار") Then waiting += 1
            totalScore += ReadinessScore(d)
        Next
        snap.MissingOcr = Math.Max(snap.MissingOcr, missingOcr)
        snap.MissingClassification = Math.Max(snap.MissingClassification, missingClass)
        snap.WaitingReview = Math.Max(snap.WaitingReview, waiting)
        snap.AverageReadiness = If(docs.Count = 0, 100, CInt(totalScore / docs.Count))
        snap.LastUpdated = DateTime.Now
        Return snap
    End Function

    Public Function BuildRecommendedActions() As List(Of WorkspaceAction)
        Dim result As New List(Of WorkspaceAction)()
        For Each d In _repository.GetDocuments()
            If String.IsNullOrWhiteSpace(d.TextContent) Then
                result.Add(New WorkspaceAction With {.Title = "استخراج OCR", .Description = Safe(d.Title), .CommandKey = "ocr", .Priority = 1, .DocumentId = d.Id})
            ElseIf String.IsNullOrWhiteSpace(d.GeneralCategory) OrElse String.IsNullOrWhiteSpace(d.SubCategory) Then
                result.Add(New WorkspaceAction With {.Title = "تصنيف وفهم سريع", .Description = Safe(d.Title), .CommandKey = "quick", .Priority = 2, .DocumentId = d.Id})
            ElseIf Safe(d.Status).Contains("مراجعة") OrElse Safe(d.Status).Contains("انتظار") Then
                result.Add(New WorkspaceAction With {.Title = "مراجعة واعتماد", .Description = Safe(d.Title), .CommandKey = "open", .Priority = 3, .DocumentId = d.Id})
            End If
            If result.Count >= 30 Then Exit For
        Next
        result.Sort(Function(a, b) a.Priority.CompareTo(b.Priority))
        Return result
    End Function

    Private Shared Function ReadinessScore(d As DocumentRecord) As Integer
        Dim score = 100
        If d Is Nothing Then Return 0
        If String.IsNullOrWhiteSpace(d.TextContent) Then score -= 35
        If String.IsNullOrWhiteSpace(d.GeneralCategory) Then score -= 15
        If String.IsNullOrWhiteSpace(d.SubCategory) Then score -= 10
        If String.IsNullOrWhiteSpace(d.ArchiveNumber) Then score -= 10
        If String.IsNullOrWhiteSpace(d.Summary) Then score -= 10
        If String.IsNullOrWhiteSpace(d.SourceEntity) Then score -= 5
        If String.IsNullOrWhiteSpace(d.DestinationEntity) Then score -= 5
        Return Math.Max(0, Math.Min(100, score))
    End Function

    Private Shared Function PickSummary(aiText As String, fallback As String) As String
        If String.IsNullOrWhiteSpace(aiText) Then Return fallback
        Dim clean = aiText.Replace("{"c, " "c).Replace("}"c, " "c).Replace("""", "")
        For Each line In clean.Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim v = line.Trim()
            If v.Length > 25 Then
                If v.Length > 500 Then v = v.Substring(0, 500)
                Return v
            End If
        Next
        If clean.Length > 500 Then clean = clean.Substring(0, 500)
        Return clean
    End Function

    Private Shared Function AppendSection(current As String, title As String, value As String) As String
        Return If(current, "") & Environment.NewLine & Environment.NewLine & "===== " & title & " " & DateTime.Now.ToString("yyyy/MM/dd HH:mm") & " =====" & Environment.NewLine & If(value, "")
    End Function

    Private Shared Function Safe(value As String) As String
        Return If(value, "")
    End Function
End Class
