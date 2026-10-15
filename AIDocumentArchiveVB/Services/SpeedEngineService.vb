Imports System
Imports System.IO
Imports System.Threading

Public Class SpeedEngineService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _repository As ArchiveRepository
    Private ReadOnly _queue As ProcessingJobQueueService
    Private ReadOnly _cache As ProcessingCacheService
    Private ReadOnly _ai As PerformanceAiService

    Public Sub New(settings As AppSettings, repository As ArchiveRepository, queue As ProcessingJobQueueService, cache As ProcessingCacheService)
        _settings = settings
        _repository = repository
        _queue = queue
        _cache = cache
        _ai = New PerformanceAiService(settings, cache)
    End Sub

    Public Function ExecuteQuickOcr(documentId As String, job As ProcessingJob, token As CancellationToken, Optional progressLimit As Integer = 100) As String
        Return RunQuickOcr(documentId, job, token, progressLimit)
    End Function

    Public Function ExecuteFastEntities(documentId As String, job As ProcessingJob, token As CancellationToken) As String
        token.ThrowIfCancellationRequested()
        Dim documentRecord = _repository.GetDocumentById(documentId)
        If documentRecord Is Nothing Then Return "المستند غير موجود."
        If job IsNot Nothing Then _queue.Report(job, "استخراج الكيانات المحلية السريعة...", 55)
        FastEntityExtractionService.ApplyFastEntities(documentRecord)
        _repository.SaveDocument(documentRecord)
        If job IsNot Nothing Then _queue.Report(job, "تم حفظ الكيانات المستخرجة.", 65)
        Return "تم استخراج الكيانات المحلية."
    End Function

    Public Function EnqueueQuickOcr(documentId As String) As ProcessingJob
        Dim d = _repository.GetDocumentById(documentId)
        If d Is Nothing Then Throw New Exception("لم يتم العثور على المستند.")
        Dim filePath As String = ResolvePath(d)
        Return _queue.Enqueue("استخراج نص سريع", "OCR_FAST", d.Id, filePath,
            Function(job, token)
                Return RunQuickOcr(d.Id, job, token)
            End Function)
    End Function

    Public Function EnqueueFastEntities(documentId As String) As ProcessingJob
        Dim d = _repository.GetDocumentById(documentId)
        If d Is Nothing Then Throw New Exception("لم يتم العثور على المستند.")
        Return _queue.Enqueue("استخراج كيانات سريع", "ENTITIES_FAST", d.Id, ResolvePath(d),
            Function(job, token)
                Dim doc = _repository.GetDocumentById(d.Id)
                If doc Is Nothing Then Return "المستند غير موجود."
                _queue.Report(job, "استخراج التواريخ والأرقام والجهات محلياً بدون اتصال سحابي...", 35)
                FastEntityExtractionService.ApplyFastEntities(doc)
                _repository.SaveDocument(doc)
                _repository.AddAudit("استخراج كيانات سريع", doc.Id, doc.Title, "تم بدون انتظار الذكاء السحابي")
                _queue.Report(job, "تم حفظ الكيانات المستخرجة.", 100)
                Return "تم استخراج الكيانات خلال مسار سريع."
            End Function)
    End Function

    Public Function EnqueueDeepUnderstanding(documentId As String, Optional deepMode As Boolean = False) As ProcessingJob
        Dim d = _repository.GetDocumentById(documentId)
        If d Is Nothing Then Throw New Exception("لم يتم العثور على المستند.")
        Return _queue.Enqueue(If(deepMode, "فهم عميق", "فهم سريع"), "AI_UNDERSTANDING", d.Id, ResolvePath(d),
            Function(job, token)
                Dim doc = _repository.GetDocumentById(d.Id)
                If doc Is Nothing Then Return "المستند غير موجود."
                _queue.Report(job, "تجهيز نص مختصر للنموذج بدلاً من كامل المستند...", 20)
                Dim result As String = _ai.BuildFastDeepUnderstanding(doc, deepMode)
                If job.RequestedCancel Then Return "تم الإلغاء."
                _queue.Report(job, "حفظ نتيجة الفهم في الملخص وسجل OCR...", 80)
                doc.Summary = FirstUsefulLine(result, doc.Summary)
                doc.OcrStructuredReport = AppendSection(doc.OcrStructuredReport, "فهم سريع", result)
                doc.UpdatedAt = DateTime.Now
                _repository.SaveDocument(doc)
                _repository.AddAudit("فهم سريع", doc.Id, doc.Title, "DeepMode=" & deepMode.ToString())
                Return "تم إنشاء فهم سريع/عميق مع كاش."
            End Function)
    End Function

    Public Function BuildReplyFast(documentRecord As DocumentRecord, tone As String, useOpenAI As Boolean) As String
        Return _ai.BuildFastReply(documentRecord, tone, useOpenAI)
    End Function

    Private Function RunQuickOcr(documentId As String, job As ProcessingJob, token As CancellationToken, Optional progressLimit As Integer = 100) As String
        token.ThrowIfCancellationRequested()
        Dim d = _repository.GetDocumentById(documentId)
        If d Is Nothing Then Return "المستند غير موجود."
        Dim filePath = ResolvePath(d)
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Throw New FileNotFoundException("الملف الأصلي غير موجود.", filePath)

        ReportOcr(job, "حساب بصمة الملف وفحص الكاش...", 10, progressLimit)
        Dim fileHash = If(String.IsNullOrWhiteSpace(d.FileHashSha256), _cache.ComputeFileHash(filePath), d.FileHashSha256)
        Dim cachedText = _cache.GetCachedText(fileHash)
        If Not String.IsNullOrWhiteSpace(cachedText) Then
            d.TextContent = cachedText
            d.FileHashSha256 = fileHash
            FastEntityExtractionService.ApplyFastEntities(d)
            _repository.SaveDocument(d)
            _repository.AddAudit("OCR من الكاش", d.Id, d.Title, "تم تحميل النص فوراً من SpeedCache")
            ReportOcr(job, "تمت القراءة من الكاش بدون إعادة OCR.", 100, progressLimit)
            Return "استخدمت النتيجة المخزنة."
        End If

        ReportOcr(job, "محاولة استخراج نص مباشر قبل OCR...", 25, progressLimit)
        Dim ext = Path.GetExtension(filePath).ToLowerInvariant()
        Dim text As String = ""
        If ext = ".pdf" Then
            Dim pdf As New PdfOcrService(_settings)
            Dim opt = PdfOcrOptions.FromSettings(_settings)
            opt.ForceOcr = False
            Dim res = pdf.Extract(filePath, opt, token, Sub(message, percent) ReportOcr(job, message, 25 + CInt(percent * 0.5), progressLimit))
            d.OcrEngineInfo = res.EngineSummary()
            d.OcrStructuredReport = AppendSection(d.OcrStructuredReport, "تقرير PDF OCR", res.BuildReport())
            d.PageCount = Math.Max(d.PageCount, res.RenderedPageCount)
            text = res.BestText()
        Else
            text = FileTextExtractor.ExtractQuickText(filePath)
            If String.IsNullOrWhiteSpace(text) Then
                ReportOcr(job, "استخراج نص الصورة باستخدام Tesseract...", 55, progressLimit)
                Dim ocr As New OcrService(_settings, token)
                text = ocr.ExtractTextProfessional(filePath)
            End If
        End If

        token.ThrowIfCancellationRequested()
        If String.IsNullOrWhiteSpace(text) Then Throw New InvalidOperationException("لم يستخرج نص. راجع إعدادات OCR وPDF أو جودة المستند.")
        ReportOcr(job, "تنظيف النص واستخراج الكيانات السريعة...", 80, progressLimit)
        text = SpeedPromptService.NormalizeText(text)
        d.TextContent = text
        d.FileHashSha256 = fileHash
        _cache.SetCachedText(fileHash, text)
        FastEntityExtractionService.ApplyFastEntities(d)
        _repository.SaveDocument(d)
        _repository.AddAudit("OCR سريع", d.Id, d.Title, "تم الحفظ مع كاش")
        ReportOcr(job, "اكتمل استخراج النص.", 100, progressLimit)
        Return "تم استخراج النص وعدد الأحرف: " & If(text Is Nothing, 0, text.Length).ToString()
    End Function

    Private Sub ReportOcr(job As ProcessingJob, message As String, percent As Integer, limit As Integer)
        _queue.Report(job, message, CInt(Math.Max(0, Math.Min(100, percent)) * Math.Max(1, Math.Min(100, limit)) / 100.0))
    End Sub

    Private Function ResolvePath(d As DocumentRecord) As String
        If d Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(d.StoredPath) AndAlso File.Exists(d.StoredPath) Then Return d.StoredPath
        If Not String.IsNullOrWhiteSpace(d.OriginalPath) AndAlso File.Exists(d.OriginalPath) Then Return d.OriginalPath
        Return If(d.StoredPath, d.OriginalPath)
    End Function

    Private Shared Function FirstUsefulLine(text As String, fallback As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return fallback
        For Each line In text.Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim v = line.Trim()
            If v.Length > 20 Then
                If v.Length > 400 Then v = v.Substring(0, 400)
                Return v
            End If
        Next
        Return If(text.Length > 400, text.Substring(0, 400), text)
    End Function

    Private Shared Function AppendSection(current As String, title As String, value As String) As String
        Dim sep = Environment.NewLine & Environment.NewLine & "===== " & title & " " & DateTime.Now.ToString("yyyy/MM/dd HH:mm") & " =====" & Environment.NewLine
        Return If(current, "") & sep & If(value, "")
    End Function
End Class
