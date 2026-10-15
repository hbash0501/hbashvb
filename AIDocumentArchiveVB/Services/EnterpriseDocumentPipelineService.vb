Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

' واجهة تطبيقية موحدة تجمع فحص الملف، OCR، الكيانات، الذكاء، الجودة، والحماية.
Public Class EnterpriseDocumentPipelineService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _repository As ArchiveRepository
    Private ReadOnly _security As DocumentSecurityService
    Private ReadOnly _automation As IntelligentAutomationService
    Private ReadOnly _profiles As ProcessingProfileService
    Private ReadOnly _quality As DocumentQualityGateService
    Private ReadOnly _queue As ProcessingJobQueueService
    Private ReadOnly _sqlite As EnterpriseSqliteService

    Public Sub New(settings As AppSettings, repository As ArchiveRepository, security As DocumentSecurityService, automation As IntelligentAutomationService, profiles As ProcessingProfileService, quality As DocumentQualityGateService, queue As ProcessingJobQueueService, sqlite As EnterpriseSqliteService)
        _settings = settings
        _repository = repository
        _security = security
        _automation = automation
        _profiles = profiles
        _quality = quality
        _queue = queue
        _sqlite = sqlite
    End Sub

    Public Function Enqueue(documentId As String, Optional profileName As String = "المسار الإداري المتوازن") As ProcessingJob
        Dim documentRecord = _repository.GetDocumentById(documentId)
        If documentRecord Is Nothing Then Throw New Exception("لم يتم العثور على المستند.")
        Return _queue.Enqueue("المعالجة المؤسسية الآمنة", "ENTERPRISE_SECURE_PIPELINE", documentId, ResolvePath(documentRecord),
            Function(job, token)
                Return ProcessCore(documentId, profileName, job, token, Nothing).Message
            End Function)
    End Function

    Public Async Function ProcessDocumentAsync(documentId As String, profileName As String, progress As IProgress(Of EnterpriseProcessingProgress), cancellationToken As CancellationToken) As Task(Of EnterpriseProcessingResult)
        ReportProgress(Nothing, progress, "التهيئة", "تهيئة خط المعالجة المؤسسية...", 0)
        Return Await Task.Run(Function()
                                  Dim transientJob As New ProcessingJob With {
                                      .DocumentId = documentId,
                                      .Title = "المعالجة المؤسسية",
                                      .OperationKind = "ENTERPRISE_SECURE_PIPELINE"
                                  }
                                  Return ProcessCore(documentId, profileName, transientJob, cancellationToken, progress)
                              End Function, cancellationToken)
    End Function

    Private Function ProcessCore(documentId As String, profileName As String, job As ProcessingJob, token As CancellationToken, progress As IProgress(Of EnterpriseProcessingProgress)) As EnterpriseProcessingResult
        Dim result As New EnterpriseProcessingResult With {.DocumentId = documentId}
        Dim documentRecord = _repository.GetDocumentById(documentId)
        If documentRecord Is Nothing Then Throw New Exception("المستند غير موجود.")
        result.DocumentTitle = documentRecord.Title
        Dim path = ResolvePath(documentRecord)
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Throw New FileNotFoundException("ملف المستند غير موجود.", path)

        ReportProgress(job, progress, "الأمان", "فحص بصمة الملف ونوعه الحقيقي...", 5)
        token.ThrowIfCancellationRequested()
        Dim signature = _security.ValidateFileSignature(path)
        documentRecord.FileSignatureVerified = signature.IsValid
        documentRecord.FileSignatureType = signature.DetectedType
        documentRecord.SecurityStatus = signature.Message
        If _settings.EnableFileSignatureValidation AndAlso Not signature.IsValid Then
            _repository.SaveDocument(documentRecord)
            Throw New InvalidDataException(signature.Message)
        End If
        result.SignatureVerified = signature.IsValid
        result.SignatureType = signature.DetectedType

        ReportProgress(job, progress, "المعالجة", "تشغيل OCR والكيانات والفهم وفق المسار المختار...", 15)
        token.ThrowIfCancellationRequested()
        Dim profile = _profiles.FindByName(profileName)
        Dim automationResult = _automation.RunAutomation(documentId, profile, job, token)
        documentRecord = _repository.GetDocumentById(documentId)

        ReportProgress(job, progress, "المخاطر", "تقييم الحساسية والمخاطر الأمنية...", 90)
        token.ThrowIfCancellationRequested()
        documentRecord.SecurityRiskScore = _security.CalculateRiskScore(documentRecord)
        result.RiskScore = documentRecord.SecurityRiskScore

        Dim threshold = Math.Max(0, Math.Min(100, _settings.SensitiveDocumentRiskThresholdPercent)) / 100.0
        If _settings.EnableSensitiveDocumentEncryption AndAlso documentRecord.SecurityRiskScore >= threshold Then
            ReportProgress(job, progress, "الحماية", "إنشاء نسخة محمية موثقة للمستند الحساس...", 94)
            documentRecord.ProtectedFilePath = _security.ProtectFileAsync(path, documentRecord.Id, token).GetAwaiter().GetResult()
            documentRecord.IsEncrypted = True
            documentRecord.SecurityStatus = "تم إنشاء نسخة محمية AES-256 مع تحقق HMAC."
            result.ProtectedCopyCreated = True
            result.ProtectedFilePath = documentRecord.ProtectedFilePath
        ElseIf documentRecord.SecurityRiskScore >= threshold Then
            documentRecord.SecurityStatus = "مستند حساس؛ يوصى بتفعيل إنشاء النسخة المحمية."
        Else
            documentRecord.SecurityStatus = If(signature.IsValid, "تم التحقق من الملف؛ مستوى المخاطر ضمن الحد.", signature.Message)
        End If

        documentRecord.UpdatedAt = DateTime.Now
        _repository.SaveDocument(documentRecord)
        If _sqlite IsNot Nothing Then _sqlite.UpsertDocument(documentRecord)
        Dim assessment = _quality.Assess(documentRecord)
        result.QualityScore = assessment.QualityScore
        result.EngineSummary = If(automationResult Is Nothing, "", automationResult.Summary)
        result.Success = True
        result.CompletedAt = DateTime.Now
        result.Message = "اكتملت المعالجة المؤسسية. الجودة: " & result.QualityScore.ToString() & "%، المخاطر: " & CInt(result.RiskScore * 100).ToString() & "%"
        _repository.AddAudit("المعالجة المؤسسية الآمنة", documentRecord.Id, documentRecord.Title, result.Message & " | التوقيع: " & signature.DetectedType)
        ReportProgress(job, progress, "مكتمل", result.Message, 100)
        Return result
    End Function

    Private Sub ReportProgress(job As ProcessingJob, progress As IProgress(Of EnterpriseProcessingProgress), stageName As String, message As String, percentage As Integer)
        If job IsNot Nothing Then _queue.Report(job, message, percentage)
        If progress IsNot Nothing Then
            progress.Report(New EnterpriseProcessingProgress With {
                .StageName = stageName,
                .StatusMessage = message,
                .Percentage = percentage
            })
        End If
    End Sub

    Private Shared Function ResolvePath(d As DocumentRecord) As String
        If d Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(d.StoredPath) AndAlso File.Exists(d.StoredPath) Then Return d.StoredPath
        If Not String.IsNullOrWhiteSpace(d.OriginalPath) AndAlso File.Exists(d.OriginalPath) Then Return d.OriginalPath
        Return If(Not String.IsNullOrWhiteSpace(d.StoredPath), d.StoredPath, d.OriginalPath)
    End Function
End Class
