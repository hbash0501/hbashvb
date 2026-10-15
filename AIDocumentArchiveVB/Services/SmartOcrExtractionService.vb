Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions

Public Class SmartOcrExtractionService
    Private ReadOnly _settings As AppSettings

    Public Sub New(settings As AppSettings)
        _settings = settings
    End Sub

    Public Function Extract(filePath As String) As SmartOcrResult
        Dim result As New SmartOcrResult()
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then
            result.Warnings.Add("الملف غير موجود أو المسار غير صحيح.")
            Return result
        End If

        Dim ext As String = Path.GetExtension(filePath).ToLowerInvariant()
        Dim quickText As String = FileTextExtractor.ExtractQuickText(filePath)
        Dim ocrText As String = ""
        Dim engines As New List(Of String)()

        If ext = ".pdf" Then
            Try
                Dim pdfService As New PdfOcrService(_settings)
                Dim pdfOptions = PdfOcrOptions.FromSettings(_settings)
                pdfOptions.ForceOcr = False
                Dim pdfResult = pdfService.Extract(filePath, pdfOptions)
                quickText = MergeTexts(quickText, pdfResult.DirectText)
                ocrText = MergeTexts(ocrText, pdfResult.OcrText)
                engines.Add(pdfResult.EngineSummary())
                If pdfResult.Warnings IsNot Nothing Then
                    For Each warning In pdfResult.Warnings
                        If Not String.IsNullOrWhiteSpace(warning) Then result.Warnings.Add(warning)
                    Next
                End If
                If pdfResult.RenderedPageCount > 0 Then result.Warnings.Add("تم OCR لعدد " & pdfResult.RenderedPageCount.ToString() & " صفحة من PDF بدقة " & pdfResult.Dpi.ToString() & " DPI.")
            Catch ex As Exception
                result.Warnings.Add("تعذر تشغيل PDF OCR: " & ex.Message)
            End Try
        Else
            If Not String.IsNullOrWhiteSpace(quickText) Then
                engines.Add("استخراج نص مباشر من الملف")
            End If

            Dim needsOcr As Boolean = FileFingerprintService.IsImage(filePath) OrElse String.IsNullOrWhiteSpace(quickText)
            If needsOcr Then
                Try
                    Dim ocr As New OcrService(_settings)
                    ocrText = ocr.ExtractTextProfessional(filePath)
                    If Not String.IsNullOrWhiteSpace(ocrText) Then engines.Add("Tesseract OCR متعدد المحاولات")
                Catch ex As Exception
                    result.Warnings.Add("تعذر تشغيل OCR: " & ex.Message)
                End Try
            End If
        End If

        Dim combined As String = MergeTexts(quickText, ocrText)
        result.RawText = combined
        result.CleanText = CleanOcrText(combined)
        result.EngineInfo = If(engines.Count = 0, "لم يتم تفعيل محرك OCR أو لم يستخرج نصاً", String.Join(" + ", engines.ToArray()))

        ExtractRuleBasedFields(result, filePath)

        If _settings IsNot Nothing AndAlso _settings.EnableOpenAI Then
            Try
                EnrichWithLocalAi(result)
            Catch ex As Exception
                result.Warnings.Add("لم يتم استخدام OpenAI السحابي: " & ex.Message)
            End Try
        End If

        FinalizeResult(result, filePath)
        Return result
    End Function

    Private Shared Function MergeTexts(a As String, b As String) As String
        a = If(a, "").Trim()
        b = If(b, "").Trim()
        If a = "" Then Return b
        If b = "" Then Return a
        If a.Contains(b) OrElse b.Contains(a) Then
            If a.Length >= b.Length Then Return a Else Return b
        End If
        Return a & Environment.NewLine & Environment.NewLine & b
    End Function

    Private Shared Function CleanOcrText(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return ""
        Dim s As String = text
        s = NormalizeArabicDigits(s)
        s = s.Replace("ـ", "")
        s = Regex.Replace(s, "[\u200E\u200F\u202A-\u202E]", "")
        s = Regex.Replace(s, "[ \t]+", " ")
        s = Regex.Replace(s, "\r?\n\s*\r?\n+", Environment.NewLine)
        s = Regex.Replace(s, "(?m)^\s+|\s+$", "")
        Return s.Trim()
    End Function

    Private Shared Function NormalizeArabicDigits(text As String) As String
        If String.IsNullOrEmpty(text) Then Return ""
        Dim sb As New StringBuilder(text.Length)
        For Each ch As Char In text
            Select Case ch
                Case "٠"c, "۰"c : sb.Append("0"c)
                Case "١"c, "۱"c : sb.Append("1"c)
                Case "٢"c, "۲"c : sb.Append("2"c)
                Case "٣"c, "۳"c : sb.Append("3"c)
                Case "٤"c, "۴"c : sb.Append("4"c)
                Case "٥"c, "۵"c : sb.Append("5"c)
                Case "٦"c, "۶"c : sb.Append("6"c)
                Case "٧"c, "۷"c : sb.Append("7"c)
                Case "٨"c, "۸"c : sb.Append("8"c)
                Case "٩"c, "۹"c : sb.Append("9"c)
                Case Else : sb.Append(ch)
            End Select
        Next
        Return sb.ToString()
    End Function

    Private Sub ExtractRuleBasedFields(result As SmartOcrResult, filePath As String)
        Dim text As String = If(result.CleanText, "")
        Dim lower As String = text.ToLowerInvariant()

        AddMatches(result.Dates, text, "(?<!\d)(?:[12][0-9]{3}|14[0-9]{2})[\/\-.](?:0?[1-9]|1[0-2])[\/\-.](?:0?[1-9]|[12][0-9]|3[01])(?!\d)")
        AddMatches(result.Dates, text, "(?<!\d)(?:0?[1-9]|[12][0-9]|3[01])[\/\-.](?:0?[1-9]|1[0-2])[\/\-.](?:[12][0-9]{3}|14[0-9]{2})(?!\d)")
        AddMatches(result.Dates, text, "(?<!\d)(?:0?[1-9]|[12][0-9]|3[01])\s+(?:محرم|صفر|ربيع\s+الأول|ربيع\s+الثاني|جمادى\s+الأولى|جمادى\s+الثانية|رجب|شعبان|رمضان|شوال|ذو\s+القعدة|ذو\s+الحجة|يناير|فبراير|مارس|أبريل|ابريل|مايو|يونيو|يوليو|أغسطس|اغسطس|سبتمبر|أكتوبر|اكتوبر|نوفمبر|ديسمبر)\s+(?:[12][0-9]{3}|14[0-9]{2})(?!\d)")

        AddGroupMatches(result.IncomingNumbers, text, "(?:رقم\s*)?(?:الوارد|وارد|قيد\s*الوارد)\s*[:：#\-]??\s*([A-Za-z0-9\-/]{2,25})")
        AddGroupMatches(result.OutgoingNumbers, text, "(?:رقم\s*)?(?:الصادر|صادر|قيد\s*الصادر)\s*[:：#\-]??\s*([A-Za-z0-9\-/]{2,25})")
        AddGroupMatches(result.ReferenceNumbers, text, "(?:رقم|برقم|المعاملة|القيد|المرجع|خطاب\s*رقم|تعميم\s*رقم|فاتورة\s*رقم|عقد\s*رقم)\s*[:：#\-]??\s*([A-Za-z0-9\-/]{3,30})")
        AddMatches(result.ReferenceNumbers, text, "(?<!\d)[A-Za-z]{1,6}[\-/][0-9]{2,8}(?:[\-/][0-9]{1,6})?(?!\d)")
        AddMatches(result.ReferenceNumbers, text, "(?<!\d)[0-9]{3,8}[\-/][0-9]{1,6}(?:[\-/][0-9]{1,6})?(?!\d)")

        AddGroupMatches(result.Amounts, text, "(?:مبلغ|بقيمة|إجمالي|اجمالي|المجموع|صافي|ضريبة|فاتورة|مستحقات|ريال|SAR)\s*[:：]?\s*([0-9][0-9,]*(?:\.[0-9]{1,2})?)\s*(?:ريال|ر\.س|SAR)?")
        AddMatches(result.Amounts, text, "(?<!\d)[0-9][0-9,]{2,}(?:\.[0-9]{1,2})?\s*(?:ريال|ر\.س|SAR)(?!\w)")

        AddGroupMatches(result.NationalIds, text, "(?:السجل\s*المدني|رقم\s*الهوية|الهوية\s*الوطنية|هوية)\s*[:：#\-]?\s*([12][0-9]{9})")
        AddMatches(result.NationalIds, text, "(?<!\d)[12][0-9]{9}(?!\d)")
        AddGroupMatches(result.IqamaNumbers, text, "(?:الإقامة|الاقامة|رقم\s*الإقامة|رقم\s*الاقامة)\s*[:：#\-]?\s*([2-9][0-9]{9})")
        AddGroupMatches(result.PassportNumbers, text, "(?:الجواز|جواز\s*السفر|passport)\s*[:：#\-]?\s*([A-Za-z0-9]{5,15})")

        AddMatches(result.PhoneNumbers, text, "(?<!\d)(?:\+966|00966|966|0)?5[0-9]{8}(?!\d)")
        AddMatches(result.Emails, text, "[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")

        AddGroupMatches(result.Names, text, "(?:الاسم|اسم\s*الموظف|اسم\s*المواطن|المكرم|الأستاذ|الاستاذ|سعادة)\s*[:：]?\s*([\u0600-\u06FF]+(?:\s+[\u0600-\u06FF]+){1,5})")
        ExtractEntities(result, text)

        result.DocumentType = DetectDocumentType(lower)
        Dim ruleAi As New AiAssistantService(Nothing)
        Dim ai As AiAnalysisResult = ruleAi.Analyze(text, Path.GetFileName(filePath))
        result.SuggestedGeneralCategory = ai.GeneralCategory
        result.SuggestedSubCategory = ai.SubCategory
        result.SuggestedPriority = ai.Priority
        result.SuggestedConfidentiality = ai.Confidentiality
        result.SuggestedSummary = ai.Summary
        result.SuggestedTags = BuildSmartTags(ai.Tags, result)
        result.SuggestedTitle = BuildSuggestedTitle(text, filePath, result.DocumentType)
    End Sub

    Private Shared Sub ExtractEntities(result As SmartOcrResult, text As String)
        If String.IsNullOrWhiteSpace(text) Then Return
        Dim lines = text.Split(New String() {ControlChars.CrLf, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries)
        For Each rawLine In lines
            Dim line = Regex.Replace(rawLine, "\s+", " ").Trim()
            If line.Length < 4 OrElse line.Length > 120 Then Continue For

            Dim source = MatchFirstGroup(line, "(?:من|المرسل|الجهة\s*الصادرة|صادر\s*من|مصدر\s*المعاملة)\s*[:：\-]?\s*(.+)$")
            If source <> "" AndAlso result.SourceEntity = "" Then result.SourceEntity = CleanEntity(source)

            Dim dest = MatchFirstGroup(line, "(?:إلى|الى|المستلم|المرسل\s*إليه|المرسل\s*اليه|جهة\s*الوارد|وارد\s*إلى|وارد\s*الى)\s*[:：\-]?\s*(.+)$")
            If dest <> "" AndAlso result.DestinationEntity = "" Then result.DestinationEntity = CleanEntity(dest)

            If Regex.IsMatch(line, "(وزارة|إدارة|ادارة|أمانة|امانة|بلدية|مدرسة|ابتدائية|متوسطة|ثانوية|جامعة|شركة|مؤسسة|مكتب|قسم|وحدة|مستشفى|بنك|مصرف|هيئة|محافظة|إمارة|امارة)") Then
                AddUnique(result.Entities, CleanEntity(line), 12)
            End If
        Next

        If result.SourceEntity = "" Then
            For Each e In result.Entities
                If e.Contains("وزارة") OrElse e.Contains("إدارة") OrElse e.Contains("ادارة") OrElse e.Contains("شركة") OrElse e.Contains("مدرسة") Then
                    result.SourceEntity = e
                    Exit For
                End If
            Next
        End If
    End Sub

    Private Shared Function CleanEntity(value As String) As String
        Dim s = Regex.Replace(If(value, ""), "\s+", " ").Trim()
        s = Regex.Replace(s, "[\.:：\-]+$", "").Trim()
        If s.Length > 90 Then s = s.Substring(0, 90).Trim()
        Return s
    End Function

    Private Shared Function MatchFirstGroup(text As String, pattern As String) As String
        Dim m = Regex.Match(text, pattern, RegexOptions.IgnoreCase)
        If m.Success AndAlso m.Groups.Count > 1 Then Return m.Groups(1).Value.Trim()
        Return ""
    End Function

    Private Shared Function DetectDocumentType(lower As String) As String
        If ContainsAny(lower, "فاتورة", "invoice", "ضريبة", "إجمالي", "اجمالي") Then Return "فاتورة / مستند مالي"
        If ContainsAny(lower, "عقد", "اتفاقية", "طرف أول", "طرف ثاني") Then Return "عقد / اتفاقية"
        If ContainsAny(lower, "تعميم") Then Return "تعميم"
        If ContainsAny(lower, "قرار") Then Return "قرار إداري"
        If ContainsAny(lower, "محضر") Then Return "محضر"
        If ContainsAny(lower, "تقرير") Then Return "تقرير"
        If ContainsAny(lower, "خطاب", "سعادة", "المكرم", "نفيدكم", "إشارة إلى", "اشارة الى") Then Return "خطاب / مراسلة"
        If ContainsAny(lower, "هوية", "جواز", "إقامة", "اقامة") Then Return "مستند هوية"
        Return "مستند عام"
    End Function

    Private Shared Function ContainsAny(text As String, ParamArray words() As String) As Boolean
        If text Is Nothing Then Return False
        For Each w In words
            If text.Contains(w.ToLowerInvariant()) Then Return True
        Next
        Return False
    End Function

    Private Shared Function BuildSuggestedTitle(text As String, filePath As String, documentType As String) As String
        Dim lines = If(text, "").Split(New String() {ControlChars.CrLf, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries)
        For Each rawLine In lines
            Dim line = Regex.Replace(rawLine, "\s+", " ").Trim()
            If line.Length >= 8 AndAlso line.Length <= 90 Then
                If Not Regex.IsMatch(line, "^[0-9\-/\s]+$") Then Return line
            End If
        Next
        Dim name = Path.GetFileNameWithoutExtension(If(filePath, "مستند"))
        If String.IsNullOrWhiteSpace(documentType) OrElse documentType = "مستند عام" Then Return name
        Return documentType & " - " & name
    End Function

    Private Shared Function BuildSmartTags(aiTags As String, r As SmartOcrResult) As String
        Dim tags As New List(Of String)()
        AddTokenTags(tags, aiTags)
        AddUnique(tags, r.DocumentType, 12)
        If r.Amounts.Count > 0 Then AddUnique(tags, "مبلغ", 12)
        If r.Dates.Count > 0 Then AddUnique(tags, "تاريخ", 12)
        If r.NationalIds.Count > 0 OrElse r.IqamaNumbers.Count > 0 OrElse r.PassportNumbers.Count > 0 Then AddUnique(tags, "هوية", 12)
        If r.IncomingNumbers.Count > 0 Then AddUnique(tags, "وارد", 12)
        If r.OutgoingNumbers.Count > 0 Then AddUnique(tags, "صادر", 12)
        Return String.Join("، ", tags.Where(Function(x) Not String.IsNullOrWhiteSpace(x)).Take(10).ToArray())
    End Function

    Private Shared Sub AddTokenTags(tags As List(Of String), text As String)
        If String.IsNullOrWhiteSpace(text) Then Return
        For Each p In text.Split(New Char() {"،"c, ","c, ";"c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            AddUnique(tags, p.Trim(), 12)
        Next
    End Sub

    Private Sub EnrichWithLocalAi(result As SmartOcrResult)
        If String.IsNullOrWhiteSpace(result.CleanText) Then Return
        Dim sample As String = result.CleanText
        If sample.Length > 4500 Then sample = sample.Substring(0, 4500)

        Dim svc As New OpenAIArchiveService(_settings)
        Dim aiText = svc.ExtractDeepFields(sample)
        If String.IsNullOrWhiteSpace(aiText) Then Return
        result.AiNotes = aiText.Trim()
        result.EngineInfo &= " + OpenAI"
        ApplyAiLines(result, aiText)
    End Sub

    Private Function CallOpenAI(prompt As String) As String
        Dim client As New OpenAIClientService(_settings)
        If client Is Nothing OrElse Not client.IsEnabled() Then Return ""
        Return client.Generate(prompt, "أنت مساعد استخراج حقول أرشيفية عربي دقيق.", 90000)
    End Function

    Private Shared Sub ApplyAiLines(result As SmartOcrResult, aiText As String)
        For Each rawLine In aiText.Split(New String() {ControlChars.CrLf, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries)
            Dim line = rawLine.Trim().Trim("-"c, "*"c, " "c)
            Dim parts = line.Split(New Char() {":"c, "："c}, 2)
            If parts.Length < 2 Then Continue For
            Dim key = NormalizeKey(parts(0))
            Dim value = parts(1).Trim()
            If value = "" OrElse value = "غير موجود" OrElse value = "لا يوجد" Then Continue For

            If key.Contains("نوعالمستند") Then result.DocumentType = value
            If key.Contains("العنوان") Then result.SuggestedTitle = value
            If key.Contains("الجهةالصادرة") OrElse key.Contains("المصدر") Then result.SourceEntity = value
            If key.Contains("الجهةالمستقبلة") OrElse key.Contains("جهةالوارد") OrElse key.Contains("المستلم") Then result.DestinationEntity = value
            If key.Contains("رقمالصادر") Then AddUnique(result.OutgoingNumbers, value, 8)
            If key.Contains("رقمالوارد") Then AddUnique(result.IncomingNumbers, value, 8)
            If key.Contains("رقمالمرجع") Then AddUnique(result.ReferenceNumbers, value, 12)
            If key.Contains("تاريخ") Then AddTokenTags(result.Dates, value)
            If key.Contains("مبالغ") OrElse key.Contains("المبالغ") Then AddTokenTags(result.Amounts, value)
            If key.Contains("الأسماء") OrElse key.Contains("الاسماء") Then AddTokenTags(result.Names, value)
            If key.Contains("الجهات") Then AddTokenTags(result.Entities, value)
            If key.Contains("الهويات") OrElse key.Contains("السجلات") Then AddTokenTags(result.NationalIds, value)
            If key.Contains("الإقامات") OrElse key.Contains("الاقامات") Then AddTokenTags(result.IqamaNumbers, value)
            If key.Contains("الجوازات") Then AddTokenTags(result.PassportNumbers, value)
            If key.Contains("الجوال") OrElse key.Contains("الهاتف") Then AddTokenTags(result.PhoneNumbers, value)
            If key.Contains("البريد") Then AddTokenTags(result.Emails, value)
            If key.Contains("السرية") Then result.SuggestedConfidentiality = value
            If key.Contains("الأولوية") OrElse key.Contains("الاولوية") Then result.SuggestedPriority = value
        Next
    End Sub

    Private Shared Function NormalizeKey(value As String) As String
        Return Regex.Replace(If(value, ""), "\s+", "").Trim()
    End Function

    Private Shared Function JsonEscape(value As String) As String
        If value Is Nothing Then Return ""
        Dim q As String = """"
        Return value.Replace("\", "\\").Replace(q, "\" & q).Replace(ControlChars.CrLf, "\n").Replace(ControlChars.Lf, "\n").Replace(ControlChars.Cr, "\n")
    End Function

    Private Shared Function ExtractJsonString(json As String, key As String) As String
        If String.IsNullOrWhiteSpace(json) Then Return ""
        Dim q As String = """"
        Dim pattern As String = q & Regex.Escape(key) & q & "\s*:\s*" & q & "((?:\\.|[^" & q & "])*)" & q
        Dim m = Regex.Match(json, pattern)
        If Not m.Success Then Return ""
        Return JsonUnescape(m.Groups(1).Value)
    End Function

    Private Shared Function JsonUnescape(value As String) As String
        If value Is Nothing Then Return ""
        Dim q As String = """"
        Dim s = value.Replace("\n", Environment.NewLine).Replace("\r", "").Replace("\t", ControlChars.Tab)
        s = s.Replace("\" & q, q).Replace("\\", "\")
        Return s
    End Function

    Private Shared Sub FinalizeResult(result As SmartOcrResult, filePath As String)
        If result.ReferenceNumber = "" Then
            If result.OutgoingNumbers.Count > 0 Then result.ReferenceNumber = result.OutgoingNumbers(0)
            If result.ReferenceNumber = "" AndAlso result.IncomingNumbers.Count > 0 Then result.ReferenceNumber = result.IncomingNumbers(0)
            If result.ReferenceNumber = "" AndAlso result.ReferenceNumbers.Count > 0 Then result.ReferenceNumber = result.ReferenceNumbers(0)
        End If
        If String.IsNullOrWhiteSpace(result.SuggestedTitle) Then result.SuggestedTitle = Path.GetFileNameWithoutExtension(filePath)
        If String.IsNullOrWhiteSpace(result.SuggestedSummary) Then result.SuggestedSummary = MakeSummary(result.CleanText)
        If String.IsNullOrWhiteSpace(result.SuggestedTags) Then result.SuggestedTags = "OCR، أرشفة، " & result.DocumentType
        result.QualityScore = EstimateQuality(result)
        If String.IsNullOrWhiteSpace(result.CleanText) Then result.Warnings.Add("لم يتم استخراج نص. قد يكون الملف PDF مصور ويحتاج تحويل الصفحات إلى صور أو تثبيت Tesseract/Poppler.")
        If result.QualityScore < 45 Then result.Warnings.Add("جودة الاستخراج منخفضة؛ راجع الصورة أو الدقة أو لغة OCR.")
    End Sub

    Private Shared Function MakeSummary(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return ""
        Dim clean = Regex.Replace(text, "\s+", " ").Trim()
        If clean.Length <= 300 Then Return clean
        Return clean.Substring(0, 300) & "..."
    End Function

    Private Shared Function EstimateQuality(r As SmartOcrResult) As Integer
        Dim score As Integer = 20
        If Not String.IsNullOrWhiteSpace(r.CleanText) Then score += Math.Min(30, r.CleanText.Length \ 40)
        If r.Dates.Count > 0 Then score += 8
        If r.ReferenceNumbers.Count > 0 OrElse r.IncomingNumbers.Count > 0 OrElse r.OutgoingNumbers.Count > 0 Then score += 12
        If r.Entities.Count > 0 OrElse r.SourceEntity <> "" OrElse r.DestinationEntity <> "" Then score += 10
        If r.Names.Count > 0 Then score += 5
        If r.Amounts.Count > 0 Then score += 5
        If r.NationalIds.Count > 0 OrElse r.IqamaNumbers.Count > 0 OrElse r.PassportNumbers.Count > 0 Then score += 5
        If r.PhoneNumbers.Count > 0 OrElse r.Emails.Count > 0 Then score += 3
        If score > 98 Then score = 98
        Return score
    End Function

    Private Shared Sub AddMatches(list As List(Of String), text As String, pattern As String)
        If list Is Nothing OrElse String.IsNullOrWhiteSpace(text) Then Return
        For Each m As Match In Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
            AddUnique(list, m.Value.Trim(), 25)
        Next
    End Sub

    Private Shared Sub AddGroupMatches(list As List(Of String), text As String, pattern As String)
        If list Is Nothing OrElse String.IsNullOrWhiteSpace(text) Then Return
        For Each m As Match In Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
            If m.Success AndAlso m.Groups.Count > 1 Then AddUnique(list, m.Groups(1).Value.Trim(), 25)
        Next
    End Sub

    Private Shared Sub AddUnique(list As List(Of String), value As String, maxItems As Integer)
        If list Is Nothing OrElse String.IsNullOrWhiteSpace(value) Then Return
        Dim clean = Regex.Replace(value, "\s+", " ").Trim().Trim("،"c, ","c, ";"c, "."c, ":"c, "："c)
        If clean = "" OrElse clean.Length > 120 Then Return
        For Each item In list
            If String.Equals(item, clean, StringComparison.OrdinalIgnoreCase) Then Return
        Next
        If list.Count < maxItems Then list.Add(clean)
    End Sub
End Class
