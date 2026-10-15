Imports System
Imports System.Linq
Imports System.Text.RegularExpressions

Public Class AiAssistantService
    Private ReadOnly _repo As ArchiveRepository
    Private ReadOnly _settings As AppSettings

    Public Sub New(repo As ArchiveRepository, Optional settings As AppSettings = Nothing)
        _repo = repo
        _settings = settings
    End Sub

    Public Function Analyze(text As String, fileName As String) As AiAnalysisResult
        Dim raw = If(text, "")
        Dim normalized = raw.ToLowerInvariant()
        Dim result As New AiAnalysisResult()

        Dim title = ExtractTitle(raw)
        If String.IsNullOrWhiteSpace(title) Then title = IO.Path.GetFileNameWithoutExtension(If(fileName, "مستند جديد"))
        result.SuggestedTitle = CleanShort(title, 85)
        result.ReferenceNumber = ExtractReference(raw)
        result.Summary = BuildSummary(raw, fileName)
        result.Tags = SuggestTags(normalized, fileName)

        Dim category = Classify(normalized, fileName)
        result.GeneralCategory = category.Item1
        result.SubCategory = category.Item2
        result.Confidentiality = DetectConfidentiality(normalized)
        result.Priority = DetectPriority(normalized)
        result.ConfidencePercent = EstimateConfidence(raw, result)
        result.Notes = "اقتراح آلي قابل للتعديل: راجع العنوان والتصنيف قبل الاعتماد النهائي."

        If _settings IsNot Nothing AndAlso _settings.EnableOpenAI AndAlso Not String.IsNullOrWhiteSpace(raw) Then
            Try
                Dim cloudAi As New OpenAIArchiveService(_settings)
                result = cloudAi.AnalyzeDocument(raw, fileName, result)
            Catch ex As Exception
                result.Notes &= Environment.NewLine & "تعذر تحسين التحليل عبر الذكاء السحابي: " & ex.Message
            End Try
        End If

        Return result
    End Function

    Private Function Classify(text As String, fileName As String) As Tuple(Of String, String)
        Dim combined = (If(text, "") & " " & If(fileName, "")).ToLowerInvariant()
        If ContainsAny(combined, "فاتورة", "fatura", "invoice", "مبلغ", "ضريبة", "سداد", "صرف", "محاسبة", "مستخلص") Then
            If ContainsAny(combined, "مستخلص") Then Return Tuple.Create("مالي", "مستخلصات")
            If ContainsAny(combined, "صرف") Then Return Tuple.Create("مالي", "أوامر صرف")
            Return Tuple.Create("مالي", "فواتير")
        End If
        If ContainsAny(combined, "عقد", "اتفاقية", "توريد", "صيانة", "contract") Then
            If ContainsAny(combined, "صيانة") Then Return Tuple.Create("عقود", "عقد صيانة")
            Return Tuple.Create("عقود", "عقد توريد")
        End If
        If ContainsAny(combined, "إجازة", "اجازة", "انتداب", "مباشرة", "موظف", "راتب", "موارد بشرية", "hr") Then
            If ContainsAny(combined, "انتداب") Then Return Tuple.Create("موارد بشرية", "انتدابات")
            If ContainsAny(combined, "مباشرة") Then Return Tuple.Create("موارد بشرية", "مباشرات")
            Return Tuple.Create("موارد بشرية", "إجازات")
        End If
        If ContainsAny(combined, "وارد", "صادر", "خطاب", "مراسلة", "إحالة", "معاملة") Then
            If ContainsAny(combined, "صادر") Then Return Tuple.Create("مراسلات", "صادر")
            Return Tuple.Create("مراسلات", "وارد")
        End If
        If ContainsAny(combined, "تقرير", "احصائية", "إنجاز", "أداء", "شهري", "ربع سنوي") Then
            If ContainsAny(combined, "أداء") Then Return Tuple.Create("تقارير", "تقرير أداء")
            Return Tuple.Create("تقارير", "تقرير شهري")
        End If
        If ContainsAny(combined, "سري", "خاص", "حساس", "confidential") Then
            Return Tuple.Create("سري", "معاملة حساسة")
        End If
        If ContainsAny(combined, "قرار", "تعميم", "محضر", "اعتماد", "إداري") Then
            If ContainsAny(combined, "قرار") Then Return Tuple.Create("إداري", "قرارات")
            If ContainsAny(combined, "تعميم") Then Return Tuple.Create("إداري", "تعاميم")
            Return Tuple.Create("إداري", "خطابات داخلية")
        End If
        Return Tuple.Create("إداري", "خطابات داخلية")
    End Function

    Private Shared Function ContainsAny(text As String, ParamArray words() As String) As Boolean
        Return words.Any(Function(w) text.Contains(w.ToLowerInvariant()))
    End Function

    Private Shared Function ExtractTitle(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return ""
        Dim lines = text.Split({ControlChars.CrLf, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(l) l.Trim()).Where(Function(l) l.Length >= 4).ToList()
        If lines.Count = 0 Then Return ""
        Return lines.FirstOrDefault(Function(l) l.Length <= 85)
    End Function

    Private Shared Function ExtractReference(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return ""
        Dim patterns = {
            "(?:رقم|برقم|المعاملة|القيد|الصادر|الوارد)\s*[:：#-]?\s*([A-Za-z0-9\-/]{3,})",
            "\b\d{4,}[/\-]\d{1,4}\b",
            "\b\d{6,}\b"
        }
        For Each p In patterns
            Dim m = Regex.Match(text, p, RegexOptions.IgnoreCase)
            If m.Success Then
                If m.Groups.Count > 1 Then Return m.Groups(1).Value
                Return m.Value
            End If
        Next
        Return ""
    End Function

    Private Shared Function BuildSummary(text As String, fileName As String) As String
        If String.IsNullOrWhiteSpace(text) Then
            Return "لم يتم استخراج نص بعد. يمكن تشغيل OCR أو كتابة وصف مختصر، وسيتم تحديث الملخص آليًا."
        End If
        Dim clean = Regex.Replace(text, "\s+", " ").Trim()
        If clean.Length <= 260 Then Return clean
        Return clean.Substring(0, 260) & "..."
    End Function

    Private Shared Function SuggestTags(text As String, fileName As String) As String
        Dim tags As New List(Of String)
        Dim pairs = New Dictionary(Of String, String) From {
            {"فاتورة", "فاتورة"}, {"invoice", "فاتورة"}, {"عقد", "عقد"}, {"صيانة", "صيانة"},
            {"تقرير", "تقرير"}, {"سري", "سري"}, {"إجازة", "إجازة"}, {"انتداب", "انتداب"},
            {"وارد", "وارد"}, {"صادر", "صادر"}, {"قرار", "قرار"}, {"تعميم", "تعميم"},
            {"مستخلص", "مستخلص"}, {"محضر", "محضر"}, {"اعتماد", "اعتماد"}
        }
        For Each kv In pairs
            If text.Contains(kv.Key.ToLowerInvariant()) AndAlso Not tags.Contains(kv.Value) Then tags.Add(kv.Value)
        Next
        If tags.Count = 0 Then tags.Add("أرشفة")
        If Not String.IsNullOrWhiteSpace(fileName) Then tags.Add(IO.Path.GetExtension(fileName).Replace(".", "").ToUpperInvariant())
        Return String.Join("، ", tags.Take(8))
    End Function

    Private Shared Function DetectConfidentiality(text As String) As String
        If ContainsAny(text, "سري للغاية", "confidential", "حساس") Then Return "سري"
        If ContainsAny(text, "خاص", "داخلي") Then Return "داخلي"
        Return "عادي"
    End Function

    Private Shared Function DetectPriority(text As String) As String
        If ContainsAny(text, "عاجل جداً", "عاجل جدا", "فوري", "هام جداً", "هام جدا") Then Return "مرتفع"
        If ContainsAny(text, "عاجل", "هام", "يرجى سرعة") Then Return "مرتفع"
        If ContainsAny(text, "للعلم", "إحاطة") Then Return "منخفض"
        Return "متوسط"
    End Function

    Private Shared Function EstimateConfidence(text As String, r As AiAnalysisResult) As Integer
        Dim score = 45
        If Not String.IsNullOrWhiteSpace(text) AndAlso text.Length > 80 Then score += 20
        If Not String.IsNullOrWhiteSpace(r.ReferenceNumber) Then score += 10
        If Not String.IsNullOrWhiteSpace(r.Tags) Then score += 10
        If r.GeneralCategory <> "إداري" Then score += 10
        If score > 95 Then score = 95
        Return score
    End Function

    Private Shared Function CleanShort(text As String, maxLen As Integer) As String
        Dim clean = Regex.Replace(If(text, ""), "\s+", " ").Trim()
        If clean.Length <= maxLen Then Return clean
        Return clean.Substring(0, maxLen) & "..."
    End Function
End Class
