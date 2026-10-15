Imports System

Public Class PerformanceAiService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _cache As ProcessingCacheService
    Private Const PromptVersion As String = "lean-prompts-1"

    Public Sub New(settings As AppSettings, cache As ProcessingCacheService)
        _settings = settings
        _cache = cache
    End Sub

    Public Function GenerateCached(prompt As String, systemPrompt As String, purpose As String, Optional timeoutMs As Integer = 60000) As String
        Dim model As String = If(_settings Is Nothing OrElse String.IsNullOrWhiteSpace(_settings.CloudAiModel), If(_settings Is Nothing OrElse String.IsNullOrWhiteSpace(_settings.OpenAiModel), "llama-3.1-8b-instant", _settings.OpenAiModel), _settings.CloudAiModel)
        Dim textHash As String = _cache.ComputeTextHash(prompt & "|" & systemPrompt)
        Dim cached As String = _cache.GetCachedAi(PromptVersion, model, textHash, purpose)
        If Not String.IsNullOrWhiteSpace(cached) Then Return cached

        Dim client As New OpenAIClientService(_settings)
        If Not client.IsEnabled() Then Return ""
        Dim answer As String = client.Generate(prompt, systemPrompt, timeoutMs)
        If Not String.IsNullOrWhiteSpace(answer) Then _cache.SetCachedAi(PromptVersion, model, textHash, purpose, answer)
        Return answer
    End Function

    Public Function BuildFastReply(documentRecord As DocumentRecord, tone As String, useOpenAI As Boolean) As String
        If documentRecord Is Nothing Then Return "اختر مستنداً أولاً."
        Dim wantedTone As String = If(String.IsNullOrWhiteSpace(tone), "رسمي متوازن", tone)
        If useOpenAI Then
            Dim context As String = SpeedPromptService.BuildLeanDocumentContext(documentRecord, 4200)
            Dim prompt As String = "اكتب مسودة رد عربي رسمي مختصرة وعملية على المستند التالي. النبرة المطلوبة: " & wantedTone & "." & Environment.NewLine &
                                   "لا تطل. اجعل الرد جاهزاً للتعديل، مع إجراء واضح في النهاية." & Environment.NewLine & Environment.NewLine & context
            Dim answer As String = GenerateCached(prompt, "أنت خبير خطابات إدارية. اكتب ردوداً عربية واضحة وسريعة ومناسبة للعمل الحكومي والمؤسسي.", "reply:" & wantedTone, 65000)
            If Not String.IsNullOrWhiteSpace(answer) Then Return answer.Trim()
        End If

        Return "سعادة/ .................................. حفظه الله" & Environment.NewLine &
               "السلام عليكم ورحمة الله وبركاته، وبعد:" & Environment.NewLine & Environment.NewLine &
               "إشارة إلى ما ورد في خطابكم بشأن: " & If(String.IsNullOrWhiteSpace(documentRecord.Title), documentRecord.FileName, documentRecord.Title) & "." & Environment.NewLine &
               "نفيدكم بأنه جرى الاطلاع على مضمون الخطاب، وسيتم اتخاذ الإجراء النظامي المناسب وفق البيانات المتاحة، مع استكمال ما يلزم عند الحاجة." & Environment.NewLine & Environment.NewLine &
               "وتفضلوا بقبول فائق الاحترام والتقدير."
    End Function

    Public Function BuildFastDeepUnderstanding(documentRecord As DocumentRecord, Optional deepMode As Boolean = False) As String
        If documentRecord Is Nothing Then Return "اختر مستنداً أولاً."
        Dim maxChars As Integer = If(deepMode, 9000, 3800)
        Dim context As String = SpeedPromptService.BuildLeanDocumentContext(documentRecord, maxChars)
        Dim prompt As String = "حلل المستند التالي بسرعة وبشكل عملي في هذه العناوين فقط: الخلاصة، ماذا يريد المرسل، الإجراء المطلوب، درجة الاستعجال، نبرة الرد المناسبة، المخاطر، الخطوة التالية." & Environment.NewLine & context
        Dim timeout As Integer = If(deepMode, 120000, 60000)
        Dim answer = GenerateCached(prompt, "أنت محلل مراسلات إدارية. كن مختصراً وعملياً.", "deep:" & deepMode.ToString(), timeout)
        If Not String.IsNullOrWhiteSpace(answer) Then Return answer.Trim()
        Return "تحليل محلي سريع:" & Environment.NewLine &
               "الخلاصة: " & If(String.IsNullOrWhiteSpace(documentRecord.Summary), "يحتاج مراجعة مختصرة من الموظف.", documentRecord.Summary) & Environment.NewLine &
               "الإجراء المقترح: مراجعة الكيانات المستخرجة، التحقق من رقم الخطاب والتاريخ، ثم اتخاذ الرد المناسب."
    End Function
End Class
