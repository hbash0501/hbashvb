Imports System
Imports System.Diagnostics
Imports System.Text
Imports System.Threading

' ينفذ مساراً واحداً متكاملاً بدلاً من توزيع OCR والكيانات والذكاء والتوجيه على عدة شاشات.
Public Class IntelligentAutomationService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _repository As ArchiveRepository
    Private ReadOnly _queue As ProcessingJobQueueService
    Private ReadOnly _speed As SpeedEngineService
    Private ReadOnly _aiGateway As AiGatewayService
    Private ReadOnly _quality As DocumentQualityGateService
    Private ReadOnly _profiles As ProcessingProfileService
    Private ReadOnly _sqlite As EnterpriseSqliteService

    Public Sub New(settings As AppSettings, repository As ArchiveRepository, queue As ProcessingJobQueueService, speed As SpeedEngineService, aiGateway As AiGatewayService, quality As DocumentQualityGateService, profiles As ProcessingProfileService, sqlite As EnterpriseSqliteService)
        _settings = settings
        _repository = repository
        _queue = queue
        _speed = speed
        _aiGateway = aiGateway
        _quality = quality
        _profiles = profiles
        _sqlite = sqlite
    End Sub

    Public Function EnqueueDocumentAutomation(documentId As String, profileName As String) As ProcessingJob
        Dim documentRecord = _repository.GetDocumentById(documentId)
        If documentRecord Is Nothing Then Throw New Exception("لم يتم العثور على المستند المحدد.")
        Dim profile = _profiles.FindByName(profileName)

        Return _queue.Enqueue("المعالجة الذكية المتكاملة", "INTELLIGENT_AUTOMATION", documentId, documentRecord.StoredPath,
            Function(job, token)
                Return RunAutomation(documentId, profile, job, token).Summary
            End Function)
    End Function

    Public Function RunAutomation(documentId As String, profile As ProcessingProfile, job As ProcessingJob, token As CancellationToken) As AutomationRunResult
        If profile Is Nothing Then profile = _profiles.FindByName("المسار الإداري المتوازن")
        Dim result As New AutomationRunResult With {
            .DocumentId = documentId,
            .ProfileName = profile.Name
        }
        Dim documentRecord = _repository.GetDocumentById(documentId)
        If documentRecord Is Nothing Then
            result.Success = False
            result.Summary = "المستند غير موجود."
            result.CompletedAt = DateTime.Now
            Return result
        End If

        result.DocumentTitle = documentRecord.Title
        Dim beforeAssessment = _quality.Assess(documentRecord)
        result.QualityBefore = beforeAssessment.QualityScore
        _queue.Report(job, "بدأ المسار: " & profile.Name, 3)

        If profile.RunOcr AndAlso String.IsNullOrWhiteSpace(documentRecord.TextContent) Then
            ExecuteAction(result, "استخراج النص OCR", Sub()
                _speed.ExecuteQuickOcr(documentId, job, token)
            End Sub)
            documentRecord = _repository.GetDocumentById(documentId)
        End If

        token.ThrowIfCancellationRequested()
        If profile.ExtractEntities Then
            ExecuteAction(result, "استخراج الكيانات المحلية", Sub()
                _speed.ExecuteFastEntities(documentId, job, token)
            End Sub)
            documentRecord = _repository.GetDocumentById(documentId)
        End If

        token.ThrowIfCancellationRequested()
        Dim quickResult As String = ""
        If profile.RunQuickAi AndAlso ShouldRunQuickAi(documentRecord) Then
            _queue.Report(job, "تشغيل الفهم السريع مع التبديل التلقائي بين المزودات...", 68)
            ExecuteAction(result, "الفهم السريع", Sub()
                quickResult = _aiGateway.AnalyzeQuick(documentRecord, token)
                ApplyQuickAiResult(documentRecord, quickResult)
            End Sub)
        End If

        token.ThrowIfCancellationRequested()
        If profile.RunDeepAiForSensitiveDocuments AndAlso IsSensitive(documentRecord) Then
            _queue.Report(job, "المستند حساس؛ تشغيل تحليل أعمق...", 78)
            ExecuteAction(result, "تحليل المستند الحساس", Sub()
                Dim deepResult = _aiGateway.AnalyzeDeep(documentRecord, token)
                documentRecord.OcrStructuredReport = AppendSection(documentRecord.OcrStructuredReport, "تحليل حساس", deepResult)
            End Sub)
        End If

        token.ThrowIfCancellationRequested()
        If profile.GenerateReplyDraftWhenNeeded AndAlso NeedsReply(documentRecord, quickResult) Then
            _queue.Report(job, "إنشاء مسودة رد قابلة للمراجعة...", 86)
            ExecuteAction(result, "مسودة الرد", Sub()
                Dim reply = _aiGateway.GenerateReply(documentRecord, SuggestedTone(documentRecord, quickResult), token)
                documentRecord.OcrStructuredReport = AppendSection(documentRecord.OcrStructuredReport, "مسودة رد تلقائية - غير معتمدة", reply)
            End Sub)
        End If

        FastEntityExtractionService.ApplyFastEntities(documentRecord)
        Dim afterAssessment = _quality.Assess(documentRecord)
        If profile.ApplyAutomaticRouting Then ApplyRouting(documentRecord, afterAssessment, profile)
        documentRecord.UpdatedAt = DateTime.Now
        _repository.SaveDocument(documentRecord)
        If _sqlite IsNot Nothing Then _sqlite.UpsertDocument(documentRecord)

        afterAssessment = _quality.Assess(documentRecord)
        result.QualityAfter = afterAssessment.QualityScore
        result.Success = True
        result.CompletedAt = DateTime.Now
        result.Summary = BuildSummary(result, afterAssessment)
        documentRecord.OcrStructuredReport = AppendSection(documentRecord.OcrStructuredReport, "نتيجة الأتمتة والجودة", result.Summary)
        _repository.SaveDocument(documentRecord)
        If _sqlite IsNot Nothing Then _sqlite.UpsertDocument(documentRecord)
        _repository.AddAudit("معالجة ذكية متكاملة", documentRecord.Id, documentRecord.Title, result.Summary)
        _queue.Report(job, "اكتملت المعالجة. الجودة: " & result.QualityAfter.ToString() & "%", 100)
        Return result
    End Function

    Private Shared Sub ExecuteAction(runResult As AutomationRunResult, actionName As String, work As Action)
        Dim sw = Stopwatch.StartNew()
        Dim actionItem As New AutomationAction With {.ActionName = actionName, .State = "قيد التنفيذ"}
        runResult.Actions.Add(actionItem)
        Try
            work()
            sw.Stop()
            actionItem.State = "مكتمل"
            actionItem.Message = "تم بنجاح"
            actionItem.DurationMs = sw.ElapsedMilliseconds
        Catch ex As Exception
            sw.Stop()
            actionItem.State = "تعذر"
            actionItem.Message = ex.Message
            actionItem.DurationMs = sw.ElapsedMilliseconds
            Throw
        End Try
    End Sub

    Private Shared Function ShouldRunQuickAi(documentRecord As DocumentRecord) As Boolean
        If documentRecord Is Nothing Then Return False
        Return String.IsNullOrWhiteSpace(documentRecord.Summary) OrElse
               String.IsNullOrWhiteSpace(documentRecord.GeneralCategory) OrElse
               String.IsNullOrWhiteSpace(documentRecord.SourceEntity) OrElse
               documentRecord.AiConfidencePercent < 70
    End Function

    Private Shared Sub ApplyQuickAiResult(documentRecord As DocumentRecord, aiText As String)
        If documentRecord Is Nothing OrElse String.IsNullOrWhiteSpace(aiText) Then Return
        Dim value As String

        value = OpenAIClientService.ExtractJsonString(aiText, "title")
        If Not String.IsNullOrWhiteSpace(value) AndAlso String.IsNullOrWhiteSpace(documentRecord.Title) Then documentRecord.Title = value
        value = OpenAIClientService.ExtractJsonString(aiText, "sender")
        If Not String.IsNullOrWhiteSpace(value) Then documentRecord.SourceEntity = value
        value = OpenAIClientService.ExtractJsonString(aiText, "receiver")
        If Not String.IsNullOrWhiteSpace(value) Then documentRecord.DestinationEntity = value
        value = OpenAIClientService.ExtractJsonString(aiText, "priority")
        If Not String.IsNullOrWhiteSpace(value) Then documentRecord.Priority = value
        value = OpenAIClientService.ExtractJsonString(aiText, "confidentiality")
        If Not String.IsNullOrWhiteSpace(value) Then documentRecord.Confidentiality = value
        value = OpenAIClientService.ExtractJsonString(aiText, "summary")
        If Not String.IsNullOrWhiteSpace(value) Then documentRecord.Summary = value
        value = OpenAIClientService.ExtractJsonString(aiText, "document_type")
        If Not String.IsNullOrWhiteSpace(value) Then documentRecord.OcrDocumentType = value

        documentRecord.OcrStructuredReport = AppendSection(documentRecord.OcrStructuredReport, "فهم سريع منظم", aiText)
        If aiText.TrimStart().StartsWith("{") Then documentRecord.AiConfidencePercent = Math.Max(documentRecord.AiConfidencePercent, 78)
    End Sub

    Private Shared Sub ApplyRouting(documentRecord As DocumentRecord, assessment As DocumentQualityAssessment, profile As ProcessingProfile)
        If documentRecord Is Nothing OrElse assessment Is Nothing Then Return
        If assessment.IsDuplicateSuspected Then
            documentRecord.Status = "مراجعة تكرار"
        ElseIf IsSensitive(documentRecord) Then
            documentRecord.Status = "مراجعة خاصة"
        ElseIf assessment.QualityScore >= profile.MinimumQualityScore AndAlso Not assessment.NeedsHumanReview Then
            documentRecord.Status = "جاهز للاعتماد"
        ElseIf String.IsNullOrWhiteSpace(documentRecord.TextContent) Then
            documentRecord.Status = "بانتظار OCR"
        Else
            documentRecord.Status = "يحتاج مراجعة"
        End If
        documentRecord.WorkflowNote = assessment.RecommendedAction
    End Sub

    Private Shared Function NeedsReply(documentRecord As DocumentRecord, aiText As String) As Boolean
        Dim aiFlag = OpenAIClientService.ExtractJsonString(aiText, "reply_needed")
        If String.Equals(aiFlag, "true", StringComparison.OrdinalIgnoreCase) OrElse aiFlag.Contains("نعم") Then Return True
        Dim source = (If(documentRecord.TextContent, "") & " " & If(documentRecord.Summary, ""))
        Return ContainsAny(source, "يرجى", "نأمل", "التكرم", "الإفادة", "الرد", "مطلوب", "تزويدنا", "إشعارنا")
    End Function

    Private Shared Function SuggestedTone(documentRecord As DocumentRecord, aiText As String) As String
        Dim aiTone = OpenAIClientService.ExtractJsonString(aiText, "reply_tone")
        If Not String.IsNullOrWhiteSpace(aiTone) Then Return aiTone
        If ContainsAny(documentRecord.Priority, "عاجل") Then Return "رسمي مباشر ومختصر"
        If ContainsAny(documentRecord.TextContent, "شكوى", "تضرر", "اعتذار") Then Return "متعاطف ومتفهم"
        If IsSensitive(documentRecord) Then Return "رسمي دقيق وحذر"
        Return "رسمي متوازن"
    End Function

    Private Shared Function IsSensitive(documentRecord As DocumentRecord) As Boolean
        If documentRecord Is Nothing Then Return False
        Return ContainsAny(documentRecord.Confidentiality, "سري") OrElse ContainsAny(documentRecord.Priority, "عاجل")
    End Function

    Private Shared Function ContainsAny(value As String, ParamArray terms() As String) As Boolean
        Dim source = If(value, "")
        For Each term In terms
            If source.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        Next
        Return False
    End Function

    Private Shared Function BuildSummary(runResult As AutomationRunResult, assessment As DocumentQualityAssessment) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("المسار: " & runResult.ProfileName)
        sb.AppendLine("الجودة قبل المعالجة: " & runResult.QualityBefore.ToString() & "%")
        sb.AppendLine("الجودة بعد المعالجة: " & runResult.QualityAfter.ToString() & "%")
        sb.AppendLine("التصنيف: " & assessment.Grade & " | المخاطر: " & assessment.RiskLevel)
        sb.AppendLine("التوصية: " & assessment.RecommendedAction)
        sb.AppendLine("الإجراءات:")
        For Each item In runResult.Actions
            sb.AppendLine("- " & item.ActionName & ": " & item.State & " (" & item.DurationMs.ToString() & " ms)")
        Next
        Return sb.ToString().Trim()
    End Function

    Private Shared Function AppendSection(current As String, title As String, value As String) As String
        Return If(current, "") & Environment.NewLine & Environment.NewLine &
               "===== " & title & " " & DateTime.Now.ToString("yyyy/MM/dd HH:mm") & " =====" & Environment.NewLine & If(value, "")
    End Function
End Class
