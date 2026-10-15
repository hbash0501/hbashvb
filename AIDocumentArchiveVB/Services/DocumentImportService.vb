Imports System
Imports System.Collections.Generic
Imports System.IO

Public Class DocumentImportService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _repo As ArchiveRepository
    Private ReadOnly _ai As AiAssistantService

    Public Sub New(settings As AppSettings, repo As ArchiveRepository, ai As AiAssistantService)
        _settings = settings
        _repo = repo
        _ai = ai
    End Sub

    Public Function ImportFile(sourcePath As String, Optional runOcrForImages As Boolean = False, Optional defaultStatus As String = "قيد المراجعة") As ImportResult
        Dim result As New ImportResult With {.SourcePath = sourcePath, .Status = "فشل"}
        If String.IsNullOrWhiteSpace(sourcePath) OrElse Not File.Exists(sourcePath) Then
            result.Message = "الملف غير موجود."
            Return result
        End If

        Try
            Dim security As New DocumentSecurityService(_settings)
            Dim signature = security.ValidateFileSignature(sourcePath)
            If _settings.EnableFileSignatureValidation AndAlso Not signature.IsValid Then
                result.Message = "تم رفض الملف أمنياً: " & signature.Message
                _repo.AddAudit("رفض ملف غير مطابق", "", Path.GetFileName(sourcePath), result.Message)
                Return result
            End If

            Dim hash = FileFingerprintService.ComputeSha256(sourcePath)
            Dim duplicate = _repo.GetDocumentByHash(hash)
            If duplicate IsNot Nothing Then
                result.IsDuplicate = True
                result.DocumentId = duplicate.Id
                result.Title = duplicate.Title
                result.Status = "مكرر"
                result.Message = "تم تجاهل الملف لأنه مكرر ومحفوظ سابقاً بعنوان: " & duplicate.Title
                _repo.AddAudit("محاولة استيراد مكرر", duplicate.Id, duplicate.Title, "المصدر: " & sourcePath)
                Return result
            End If

            Dim smartOcr As SmartOcrResult = Nothing
            Dim quickText = FileTextExtractor.ExtractQuickText(sourcePath)
            Dim ext As String = Path.GetExtension(sourcePath).ToLowerInvariant()
            Dim shouldRunSmartExtraction As Boolean = runOcrForImages OrElse ext = ".pdf" OrElse Not String.IsNullOrWhiteSpace(quickText) OrElse FileFingerprintService.IsImage(sourcePath)
            If shouldRunSmartExtraction Then
                Dim smartExtractor As New SmartOcrExtractionService(_settings)
                smartOcr = smartExtractor.Extract(sourcePath)
                If smartOcr IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(smartOcr.CleanText) Then quickText = smartOcr.CleanText
            End If

            Dim ai = _ai.Analyze(quickText, Path.GetFileName(sourcePath))
            If smartOcr IsNot Nothing Then
                If Not String.IsNullOrWhiteSpace(smartOcr.SuggestedTitle) Then ai.SuggestedTitle = smartOcr.SuggestedTitle
                If Not String.IsNullOrWhiteSpace(smartOcr.ReferenceNumber) Then ai.ReferenceNumber = smartOcr.ReferenceNumber
                If Not String.IsNullOrWhiteSpace(smartOcr.SuggestedSummary) Then ai.Summary = smartOcr.SuggestedSummary
                If Not String.IsNullOrWhiteSpace(smartOcr.SuggestedTags) Then ai.Tags = smartOcr.SuggestedTags
                ai.GeneralCategory = smartOcr.SuggestedGeneralCategory
                ai.SubCategory = smartOcr.SuggestedSubCategory
                ai.Priority = smartOcr.SuggestedPriority
                ai.Confidentiality = smartOcr.SuggestedConfidentiality
                If smartOcr.QualityScore > ai.ConfidencePercent Then ai.ConfidencePercent = smartOcr.QualityScore
            End If

            Dim stored = StorageService.StoreFile(sourcePath, _settings)
            Dim doc As New DocumentRecord With {
                .Title = ai.SuggestedTitle,
                .Description = "استيراد جماعي/سريع",
                .FileName = Path.GetFileName(sourcePath),
                .OriginalPath = sourcePath,
                .StoredPath = stored,
                .FileType = Path.GetExtension(sourcePath),
                .TextContent = quickText,
                .Summary = ai.Summary,
                .GeneralCategory = ai.GeneralCategory,
                .SubCategory = ai.SubCategory,
                .Tags = ai.Tags,
                .ReferenceNumber = ai.ReferenceNumber,
                .ArchiveNumber = New ArchiveNumberService(_repo).GenerateNext(ai.GeneralCategory),
                .ExternalNumber = If(smartOcr Is Nothing, "", FirstNonEmpty(smartOcr.OutgoingNumbers, smartOcr.IncomingNumbers, smartOcr.ReferenceNumbers)),
                .SourceEntity = If(smartOcr Is Nothing, "", smartOcr.SourceEntity),
                .DestinationEntity = If(smartOcr Is Nothing, "", smartOcr.DestinationEntity),
                .DocumentDateText = If(smartOcr Is Nothing, "", FirstListValue(smartOcr.Dates)),
                .Confidentiality = ai.Confidentiality,
                .Priority = ai.Priority,
                .Status = defaultStatus,
                .EmployeeName = _settings.CurrentEmployeeName,
                .FileHashSha256 = hash,
                .FileSizeBytes = FileFingerprintService.GetFileSize(sourcePath),
                .PageCount = FileFingerprintService.EstimatePageCount(sourcePath),
                .AiConfidencePercent = ai.ConfidencePercent,
                .OcrDocumentType = If(smartOcr Is Nothing, "", smartOcr.DocumentType),
                .OcrQualityScore = If(smartOcr Is Nothing, 0, smartOcr.QualityScore),
                .OcrEngineInfo = If(smartOcr Is Nothing, "", smartOcr.EngineInfo),
                .OcrDates = If(smartOcr Is Nothing, "", JoinList(smartOcr.Dates)),
                .OcrReferenceNumbers = If(smartOcr Is Nothing, "", JoinList(smartOcr.ReferenceNumbers)),
                .OcrIncomingNumbers = If(smartOcr Is Nothing, "", JoinList(smartOcr.IncomingNumbers)),
                .OcrOutgoingNumbers = If(smartOcr Is Nothing, "", JoinList(smartOcr.OutgoingNumbers)),
                .OcrAmounts = If(smartOcr Is Nothing, "", JoinList(smartOcr.Amounts)),
                .OcrNationalIds = If(smartOcr Is Nothing, "", JoinList(smartOcr.NationalIds)),
                .OcrIqamaNumbers = If(smartOcr Is Nothing, "", JoinList(smartOcr.IqamaNumbers)),
                .OcrPassportNumbers = If(smartOcr Is Nothing, "", JoinList(smartOcr.PassportNumbers)),
                .OcrPhoneNumbers = If(smartOcr Is Nothing, "", JoinList(smartOcr.PhoneNumbers)),
                .OcrEmails = If(smartOcr Is Nothing, "", JoinList(smartOcr.Emails)),
                .OcrNames = If(smartOcr Is Nothing, "", JoinList(smartOcr.Names)),
                .OcrEntities = If(smartOcr Is Nothing, "", JoinList(smartOcr.Entities)),
                .OcrStructuredReport = If(smartOcr Is Nothing, "", smartOcr.BuildReport()),
                .FileSignatureVerified = signature.IsValid,
                .FileSignatureType = signature.DetectedType,
                .SecurityStatus = signature.Message
            }
            doc.SecurityRiskScore = security.CalculateRiskScore(doc)
            Dim riskThreshold = Math.Max(0, Math.Min(100, _settings.SensitiveDocumentRiskThresholdPercent)) / 100.0
            If _settings.EnableSensitiveDocumentEncryption AndAlso doc.SecurityRiskScore >= riskThreshold Then
                doc.ProtectedFilePath = security.ProtectFile(stored, doc.Id)
                doc.IsEncrypted = True
                doc.SecurityStatus = "تم إنشاء نسخة محمية AES-256 مع تحقق HMAC."
            End If
            _repo.SaveDocument(doc)
            _repo.AddAudit("استيراد مستند", doc.Id, doc.Title, "تم الحفظ من: " & sourcePath)

            result.Status = "تم"
            result.DocumentId = doc.Id
            result.StoredPath = stored
            result.Title = doc.Title
            result.Message = "تمت الأرشفة بنجاح."
            Return result
        Catch ex As Exception
            result.Message = ex.Message
            Return result
        End Try
    End Function

    Private Shared Function JoinList(items As List(Of String)) As String
        If items Is Nothing OrElse items.Count = 0 Then Return ""
        Return String.Join("، ", items.ToArray())
    End Function

    Private Shared Function FirstListValue(items As List(Of String)) As String
        If items Is Nothing OrElse items.Count = 0 Then Return ""
        Return items(0)
    End Function

    Private Shared Function FirstNonEmpty(ParamArray groups() As List(Of String)) As String
        For Each g In groups
            If g IsNot Nothing AndAlso g.Count > 0 Then Return g(0)
        Next
        Return ""
    End Function

End Class
