Imports System
Imports System.Collections.Generic
Imports System.Text
Imports System.Text.RegularExpressions

Public Class OpenAIArchiveService
    Private ReadOnly _client As OpenAIClientService

    Public Sub New(settings As AppSettings)
        _client = New OpenAIClientService(settings)
    End Sub

    Public Function TestConnection() As String
        Return _client.TestConnection()
    End Function

    Public Function AnalyzeDocument(text As String, fileName As String, fallback As AiAnalysisResult) As AiAnalysisResult
        If fallback Is Nothing Then fallback = New AiAnalysisResult()
        If String.IsNullOrWhiteSpace(text) Then Return fallback

        Dim sample = LimitText(text, 7000)
        Dim prompt As String = BuildAnalysisPrompt(sample, fileName)
        Dim systemPrompt As String = "أنت خبير أرشفة وثائق عربي داخل برنامج مكتبي. استخرج حقولاً دقيقة ومختصرة. لا تخترع معلومات غير موجودة. إذا لم تجد الحقل اكتب غير موجود."
        Dim answer = _client.Generate(prompt, systemPrompt, 150000)
        If String.IsNullOrWhiteSpace(answer) Then Return fallback

        ApplyToAnalysisResult(fallback, answer)
        If String.IsNullOrWhiteSpace(fallback.Notes) Then
            fallback.Notes = "تم التحليل بواسطة مزود الذكاء السحابي مع قواعد احتياطية. راجع النتائج قبل الاعتماد."
        ElseIf Not fallback.Notes.Contains("الذكاء السحابي") Then
            fallback.Notes &= Environment.NewLine & "تحسين إضافي بواسطة الذكاء السحابي."
        End If
        fallback.ConfidencePercent = Math.Max(fallback.ConfidencePercent, EstimateCloudConfidence(fallback, text))
        Return fallback
    End Function

    Public Function AskDocumentQuestion(document As DocumentRecord, question As String) As String
        If document Is Nothing Then Return "اختر مستنداً أولاً."
        If String.IsNullOrWhiteSpace(question) Then Return "اكتب السؤال المطلوب."
        Dim content As String = BuildDocumentContext(document, 9000)
        Dim prompt As String = "أجب عن سؤال الموظف اعتماداً على بيانات ومحتوى المستند فقط. إذا لم تجد الجواب قل: لا يظهر ذلك في المستند." & Environment.NewLine & Environment.NewLine &
            "السؤال:" & Environment.NewLine & question.Trim() & Environment.NewLine & Environment.NewLine &
            "بيانات ومحتوى المستند:" & Environment.NewLine & content
        Return _client.Generate(prompt, "أنت مساعد أرشفة مؤسسية دقيق ومختصر وتكتب بالعربية الواضحة.", 150000)
    End Function

    Public Function BuildExecutiveCard(document As DocumentRecord) As String
        If document Is Nothing Then Return ""
        Dim prompt As String = "أنشئ بطاقة تنفيذية عربية فاخرة ومختصرة لهذا المستند تشمل: الخلاصة، نوع المستند، الأطراف، الأرقام المهمة، التواريخ، المبالغ، درجة السرية، الأولوية، النواقص، الإجراء المقترح التالي. لا تستخدم جداول طويلة." & Environment.NewLine & Environment.NewLine & BuildDocumentContext(document, 9000)
        Return _client.Generate(prompt, "أنت مستشار أرشفة وعمليات إدارية.", 150000)
    End Function

    Public Function DraftOutgoingReply(document As DocumentRecord) As String
        If document Is Nothing Then Return ""
        Dim prompt As String = "اكتب مسودة رد رسمي عربي قصير ومهذب بناءً على المستند التالي. اجعلها قابلة للتعديل، ولا تضف أسماء غير موجودة. ابدأ بـ: السلام عليكم ورحمة الله وبركاته." & Environment.NewLine & Environment.NewLine & BuildDocumentContext(document, 9000)
        Return _client.Generate(prompt, "أنت محرر خطابات رسمية عربية في جهة إدارية.", 150000)
    End Function

    Public Function ReviewOcrQuality(document As DocumentRecord) As String
        If document Is Nothing Then Return ""
        Dim prompt As String = "راجع جودة OCR لهذا المستند. اذكر المشاكل المحتملة في النص المستخرج، الحقول الناقصة، وكيف يحسن الموظف الاستخراج. أجب بنقاط عملية قصيرة." & Environment.NewLine & Environment.NewLine & BuildDocumentContext(document, 9000)
        Return _client.Generate(prompt, "أنت خبير OCR وأرشفة مستندات عربية.", 150000)
    End Function

    Public Function ExtractDeepFields(text As String) As String
        Dim sample = LimitText(text, 9000)
        If String.IsNullOrWhiteSpace(sample) Then Return ""
        Dim prompt As String = "استخرج جميع الحقول الممكنة من النص التالي لاستخدامها في الأرشفة. أجب حصراً بأسطر من نمط: الحقل: القيمة. الحقول: نوع المستند، العنوان، الملخص، التصنيف العام، التصنيف الثانوي، جهة الصادر، جهة الوارد، رقم الصادر، رقم الوارد، رقم المرجع، تاريخ المستند، كل التواريخ، المبالغ، الأسماء، الجهات، أرقام الهوية أو السجل المدني، أرقام الإقامة، أرقام الجواز، أرقام الجوال، البريد الإلكتروني، السرية، الأولوية، كلمات مفتاحية، ملاحظات جودة." & Environment.NewLine & Environment.NewLine & sample
        Return _client.Generate(prompt, "أنت محرك استخراج حقول أرشيفية عربي دقيق. لا تخترع بيانات.", 180000)
    End Function

    Private Shared Function BuildAnalysisPrompt(text As String, fileName As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("حلل هذا المستند لغرض الأرشفة الذكية. أجب فقط بأسطر قصيرة من الشكل: الحقل: القيمة")
        sb.AppendLine("الحقول المطلوبة:")
        sb.AppendLine("العنوان المقترح: ")
        sb.AppendLine("الملخص: ")
        sb.AppendLine("التصنيف العام: إداري/مالي/موارد بشرية/عقود/مراسلات/تقارير/سري/أخرى")
        sb.AppendLine("التصنيف الثانوي: ")
        sb.AppendLine("الكلمات المفتاحية: ")
        sb.AppendLine("رقم المرجع: ")
        sb.AppendLine("السرية: عادي/داخلي/سري")
        sb.AppendLine("الأولوية: منخفض/متوسط/مرتفع")
        sb.AppendLine("ملاحظات: ")
        sb.AppendLine()
        sb.AppendLine("اسم الملف: " & If(fileName, ""))
        sb.AppendLine("النص:")
        sb.AppendLine(text)
        Return sb.ToString()
    End Function

    Private Shared Function BuildDocumentContext(d As DocumentRecord, maxChars As Integer) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("العنوان: " & Safe(d.Title))
        sb.AppendLine("اسم الملف: " & Safe(d.FileName))
        sb.AppendLine("رقم الأرشيف: " & Safe(d.ArchiveNumber))
        sb.AppendLine("رقم المرجع: " & Safe(d.ReferenceNumber))
        sb.AppendLine("التصنيف: " & Safe(d.GeneralCategory) & " / " & Safe(d.SubCategory))
        sb.AppendLine("الحالة: " & Safe(d.Status))
        sb.AppendLine("الأولوية: " & Safe(d.Priority) & " | السرية: " & Safe(d.Confidentiality))
        sb.AppendLine("جهة الصادر: " & Safe(d.SourceEntity))
        sb.AppendLine("جهة الوارد: " & Safe(d.DestinationEntity))
        sb.AppendLine("تاريخ المستند: " & Safe(d.DocumentDateText))
        sb.AppendLine("وسوم: " & Safe(d.Tags))
        sb.AppendLine("ملخص محفوظ: " & Safe(d.Summary))
        sb.AppendLine("تقرير OCR: " & Safe(d.OcrStructuredReport))
        sb.AppendLine("النص المستخرج:")
        sb.AppendLine(Safe(d.TextContent))
        Return LimitText(sb.ToString(), maxChars)
    End Function

    Private Shared Sub ApplyToAnalysisResult(r As AiAnalysisResult, aiText As String)
        For Each rawLine In If(aiText, "").Split(New String() {ControlChars.CrLf, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries)
            Dim line = rawLine.Trim().Trim("-"c, "*"c, " "c)
            Dim parts = line.Split(New Char() {":"c, "："c}, 2)
            If parts.Length < 2 Then Continue For
            Dim key = NormalizeKey(parts(0))
            Dim value = CleanValue(parts(1))
            If value = "" Then Continue For

            If key.Contains("العنوان") Then r.SuggestedTitle = value
            If key.Contains("الملخص") Then r.Summary = value
            If key.Contains("التصنيفالعام") Then r.GeneralCategory = value
            If key.Contains("التصنيفالثانوي") Then r.SubCategory = value
            If key.Contains("الكلمات") OrElse key.Contains("الوسوم") Then r.Tags = value
            If key.Contains("رقمالمرجع") OrElse key.Contains("رقم") Then
                If String.IsNullOrWhiteSpace(r.ReferenceNumber) OrElse key.Contains("مرجع") Then r.ReferenceNumber = value
            End If
            If key.Contains("السرية") Then r.Confidentiality = NormalizeConfidentiality(value)
            If key.Contains("الأولوية") OrElse key.Contains("الاولوية") Then r.Priority = NormalizePriority(value)
            If key.Contains("ملاحظات") Then r.Notes = value
        Next
    End Sub

    Private Shared Function EstimateCloudConfidence(r As AiAnalysisResult, text As String) As Integer
        Dim score As Integer = 55
        If Not String.IsNullOrWhiteSpace(text) AndAlso text.Length > 500 Then score += 10
        If Not String.IsNullOrWhiteSpace(r.SuggestedTitle) Then score += 7
        If Not String.IsNullOrWhiteSpace(r.Summary) AndAlso r.Summary.Length > 40 Then score += 8
        If Not String.IsNullOrWhiteSpace(r.ReferenceNumber) Then score += 5
        If Not String.IsNullOrWhiteSpace(r.Tags) Then score += 5
        If Not String.IsNullOrWhiteSpace(r.GeneralCategory) Then score += 5
        If score > 98 Then score = 98
        Return score
    End Function

    Private Shared Function NormalizePriority(value As String) As String
        Dim v = If(value, "").Trim()
        If v.Contains("مرتفع") OrElse v.Contains("عاجل") OrElse v.Contains("هام") Then Return "مرتفع"
        If v.Contains("منخفض") Then Return "منخفض"
        Return "متوسط"
    End Function

    Private Shared Function NormalizeConfidentiality(value As String) As String
        Dim v = If(value, "").Trim()
        If v.Contains("سري") OrElse v.Contains("حساس") Then Return "سري"
        If v.Contains("داخلي") OrElse v.Contains("خاص") Then Return "داخلي"
        Return "عادي"
    End Function

    Private Shared Function NormalizeKey(value As String) As String
        Return Regex.Replace(If(value, ""), "\s+", "").Trim()
    End Function

    Private Shared Function CleanValue(value As String) As String
        Dim v = Regex.Replace(If(value, ""), "\s+", " ").Trim()
        Dim lower = v.ToLowerInvariant()
        If lower = "غير موجود" OrElse lower = "لا يوجد" OrElse lower = "n/a" OrElse lower = "none" Then Return ""
        If v.Length > 500 Then v = v.Substring(0, 500).Trim() & "..."
        Return v
    End Function

    Private Shared Function LimitText(value As String, maxChars As Integer) As String
        Dim s = If(value, "").Trim()
        If s.Length <= maxChars Then Return s
        Return s.Substring(0, maxChars) & Environment.NewLine & "... [تم اختصار النص للحفاظ على سرعة التحليل]"
    End Function

    Private Shared Function Safe(value As String) As String
        Return If(value, "").Trim()
    End Function
End Class
