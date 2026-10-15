Imports System
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Public Class AiGatewayService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _cache As ProcessingCacheService
    Private ReadOnly _sqlite As EnterpriseSqliteService
    Private ReadOnly _openai As OpenAIClientService
    Private ReadOnly _resilience As MultiAiResilienceService

    Public Sub New(settings As AppSettings, cache As ProcessingCacheService, sqlite As EnterpriseSqliteService)
        Me.New(settings, cache, sqlite, New MultiAiResilienceService(settings))
    End Sub

    Public Sub New(settings As AppSettings, cache As ProcessingCacheService, sqlite As EnterpriseSqliteService, resilience As MultiAiResilienceService)
        _settings = settings
        _cache = cache
        _sqlite = sqlite
        _openai = New OpenAIClientService(settings)
        _resilience = If(resilience, New MultiAiResilienceService(settings))
    End Sub

    Public Function IsReady() As Boolean
        Return _resilience IsNot Nothing AndAlso _resilience.HasAnyConfiguredProvider()
    End Function

    Public Async Function AnalyzeQuickAsync(documentRecord As DocumentRecord, token As CancellationToken) As Task(Of String)
        Return Await Task.Run(Function() AnalyzeQuick(documentRecord, token), token)
    End Function

    Public Async Function AnalyzeDeepAsync(documentRecord As DocumentRecord, token As CancellationToken) As Task(Of String)
        Return Await Task.Run(Function() AnalyzeDeep(documentRecord, token), token)
    End Function

    Public Async Function GenerateReplyAsync(documentRecord As DocumentRecord, tone As String, token As CancellationToken) As Task(Of String)
        Return Await Task.Run(Function() GenerateReply(documentRecord, tone, token), token)
    End Function

    Public Function AnalyzeQuick(documentRecord As DocumentRecord, token As CancellationToken) As String
        If documentRecord Is Nothing Then Return ""
        Dim compact = BuildCompactContext(documentRecord, 4200)
        Return GenerateCached(documentRecord.Id, "quick-understanding-v25", compact, BuildQuickPrompt(compact), 45000, token)
    End Function

    Public Function AnalyzeDeep(documentRecord As DocumentRecord, token As CancellationToken) As String
        If documentRecord Is Nothing Then Return ""
        Dim compact = BuildCompactContext(documentRecord, 9000)
        Return GenerateCached(documentRecord.Id, "deep-understanding-v25", compact, BuildDeepPrompt(compact), 90000, token)
    End Function

    Public Function GenerateReply(documentRecord As DocumentRecord, tone As String, token As CancellationToken) As String
        If documentRecord Is Nothing Then Return ""
        Dim compact = BuildCompactContext(documentRecord, 5000)
        Dim prompt = BuildReplyPrompt(compact, tone)
        Return GenerateCached(documentRecord.Id, "reply-" & NormalizeKey(tone) & "-v25", compact, prompt, 70000, token)
    End Function

    Public Function GenerateCached(documentId As String, analysisType As String, sourceText As String, prompt As String, timeoutMs As Integer, token As CancellationToken) As String
        If token.IsCancellationRequested Then token.ThrowIfCancellationRequested()
        Dim model = If(_settings Is Nothing OrElse String.IsNullOrWhiteSpace(_settings.CloudAiModel), If(_settings Is Nothing OrElse String.IsNullOrWhiteSpace(_settings.OpenAiModel), "llama-3.1-8b-instant", _settings.OpenAiModel.Trim()), _settings.CloudAiModel.Trim())
        Dim textHash = If(_cache Is Nothing, ComputeTextHash(sourceText), _cache.ComputeTextHash(sourceText))

        If _sqlite IsNot Nothing AndAlso _sqlite.IsAvailable Then
            Dim fromDb = _sqlite.LatestAiAnalysis(documentId, analysisType, textHash)
            If Not String.IsNullOrWhiteSpace(fromDb) Then Return fromDb
        End If

        If _cache IsNot Nothing Then
            Dim cached = _cache.GetCachedAi("v26", model, textHash, analysisType)
            If Not String.IsNullOrWhiteSpace(cached) Then Return cached
        End If

        If _resilience Is Nothing OrElse Not _resilience.HasAnyConfiguredProvider() Then
            Return LocalFallback(analysisType, sourceText)
        End If

        Dim result As String = ""
        Dim response = _resilience.GenerateWithFailover(prompt, "أنت خبير أرشفة ومخاطبات عربية. أعد نتائج عملية مختصرة ومنظمة. عند طلب JSON أعد JSON فقط.", timeoutMs, token)
        If response IsNot Nothing AndAlso response.Success Then
            result = response.Content
            model = response.ModelName
        Else
            Dim details = If(response Is Nothing, "", response.ErrorMessage)
            result = LocalFallback(analysisType, sourceText)
            If Not String.IsNullOrWhiteSpace(details) Then result &= Environment.NewLine & "تعذر الذكاء السحابي؛ استُخدم التحليل المحلي: " & details
        End If

        If _cache IsNot Nothing Then _cache.SetCachedAi("v26", model, textHash, analysisType, result)
        If _sqlite IsNot Nothing AndAlso _sqlite.IsAvailable Then _sqlite.SaveAiAnalysis(documentId, analysisType, model, "v26", textHash, result)
        Return result
    End Function

    Public Function BuildCompactContext(d As DocumentRecord, maxChars As Integer) As String
        If d Is Nothing Then Return ""
        Dim sb As New StringBuilder()
        sb.AppendLine("بيانات الأرشفة:")
        sb.AppendLine("العنوان: " & Safe(d.Title))
        sb.AppendLine("رقم الأرشيف: " & Safe(d.ArchiveNumber))
        sb.AppendLine("التصنيف: " & Safe(d.GeneralCategory) & " / " & Safe(d.SubCategory))
        sb.AppendLine("الجهة الصادرة: " & Safe(d.SourceEntity))
        sb.AppendLine("الجهة الواردة: " & Safe(d.DestinationEntity))
        sb.AppendLine("الأولوية: " & Safe(d.Priority) & " | السرية: " & Safe(d.Confidentiality))
        sb.AppendLine("التواريخ: " & Safe(d.OcrDates))
        sb.AppendLine("الأرقام المرجعية: " & Safe(d.OcrReferenceNumbers) & " " & Safe(d.OcrIncomingNumbers) & " " & Safe(d.OcrOutgoingNumbers))
        sb.AppendLine("المبالغ: " & Safe(d.OcrAmounts))
        sb.AppendLine("الأسماء: " & Safe(d.OcrNames))
        sb.AppendLine("الجهات: " & Safe(d.OcrEntities))
        sb.AppendLine("--- النص المختصر ---")
        Dim clean = SpeedPromptService.NormalizeText(Safe(d.TextContent))
        If clean.Length > maxChars Then
            Dim head = clean.Substring(0, Math.Min(clean.Length, CInt(maxChars * 0.65)))
            Dim tailLen = Math.Min(clean.Length - head.Length, CInt(maxChars * 0.25))
            Dim tail = If(tailLen > 0, clean.Substring(clean.Length - tailLen), "")
            clean = head & Environment.NewLine & "... [تم اختصار منتصف النص حفاظاً على السرعة] ..." & Environment.NewLine & tail
        End If
        sb.AppendLine(clean)
        Return sb.ToString()
    End Function

    Private Shared Function BuildQuickPrompt(compact As String) As String
        Return "حلل المستند الإداري التالي بسرعة وأعد JSON فقط بالمفاتيح: document_type,title,sender,receiver,priority,confidentiality,required_action,summary,reply_needed,reply_tone. " &
               "لا تشرح خارج JSON." & Environment.NewLine & compact
    End Function

    Private Shared Function BuildDeepPrompt(compact As String) As String
        Return "قدّم فهماً عميقاً للمستند التالي. أعد النتيجة في أقسام واضحة: المعنى، المقصود الحقيقي، المخاطر، النقاط الحساسة، ما يحتاج رد، نبرة الرد المناسبة، توصية للموظف، ملخص تنفيذي. " &
               "كن دقيقاً وعملياً ولا تطل بلا حاجة." & Environment.NewLine & compact
    End Function

    Private Shared Function BuildReplyPrompt(compact As String, tone As String) As String
        Return "اكتب مسودة رد عربي رسمي على الخطاب التالي. النبرة المطلوبة: " & Safe(tone) & ". " &
               "يجب أن يكون الرد منسقاً ومناسباً للمخاطبات الرسمية، مع تجنب الجزم بما لا يثبت من النص. " &
               "ابدأ بالبسملة إن كانت مناسبة ثم صياغة خطاب قابلة للتصدير إلى Word." & Environment.NewLine & compact
    End Function

    Private Shared Function LocalFallback(analysisType As String, sourceText As String) As String
        Dim preview = If(sourceText, "")
        If preview.Length > 1200 Then preview = preview.Substring(0, 1200)
        If analysisType.StartsWith("reply", StringComparison.OrdinalIgnoreCase) Then
            Return "سعادة/ الجهة المختصة حفظكم الله" & Environment.NewLine & Environment.NewLine &
                   "إشارةً إلى ما ورد في الخطاب محل المعالجة، نأمل التكرم بالاطلاع واتخاذ ما يلزم وفق الأنظمة والتعليمات، مع تزويدنا بما يستجد حيال الموضوع عند الحاجة." & Environment.NewLine & Environment.NewLine &
                   "وتقبلوا خالص التحية والتقدير."
        End If
        Return "تحليل محلي سريع:" & Environment.NewLine &
               "- تم تجهيز ملخص أولي دون استخدام خدمة الذكاء السحابي." & Environment.NewLine &
               "- يوصى بمراجعة الجهة الصادرة والواردة والأرقام المرجعية قبل الاعتماد." & Environment.NewLine &
               "- مقتطف: " & preview
    End Function

    Private Shared Function NormalizeKey(value As String) As String
        Dim v = If(value, "auto").Trim()
        If v = "" Then v = "auto"
        For Each c In IO.Path.GetInvalidFileNameChars()
            v = v.Replace(c, "_"c)
        Next
        Return v.Replace(" ", "_")
    End Function

    Private Shared Function Safe(value As String) As String
        Return If(value, "")
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
