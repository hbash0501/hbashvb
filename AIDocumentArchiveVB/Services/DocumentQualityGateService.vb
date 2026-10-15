Imports System
Imports System.Collections.Generic

' يقيس جاهزية المستند للاعتماد ويمنع تمرير مستند ناقص دون تنبيه.
Public Class DocumentQualityGateService
    Private ReadOnly _repository As ArchiveRepository
    Private ReadOnly _settings As AppSettings

    Public Sub New(repository As ArchiveRepository, settings As AppSettings)
        _repository = repository
        _settings = settings
    End Sub

    Public Function Assess(documentRecord As DocumentRecord) As DocumentQualityAssessment
        If documentRecord Is Nothing Then
            Return New DocumentQualityAssessment With {
                .QualityScore = 0,
                .Grade = "غير متاح",
                .RiskLevel = "مرتفع",
                .MissingFields = "المستند غير موجود",
                .RecommendedAction = "اختر مستنداً صالحاً.",
                .NeedsHumanReview = True
            }
        End If

        Dim score As Integer = 100
        Dim missing As New List(Of String)()

        DeductIfMissing(documentRecord.Title, "العنوان", 5, score, missing)
        DeductIfMissing(documentRecord.ArchiveNumber, "رقم الأرشيف", 8, score, missing)
        DeductIfMissing(documentRecord.TextContent, "النص المستخرج", 28, score, missing)
        DeductIfMissing(documentRecord.GeneralCategory, "التصنيف العام", 12, score, missing)
        DeductIfMissing(documentRecord.SubCategory, "التصنيف الثانوي", 7, score, missing)
        DeductIfMissing(documentRecord.SourceEntity, "جهة الصادر", 6, score, missing)
        DeductIfMissing(documentRecord.DestinationEntity, "جهة الوارد", 6, score, missing)
        DeductIfMissing(documentRecord.DocumentDateText, "تاريخ المستند", 4, score, missing)
        DeductIfMissing(FirstNotEmpty(documentRecord.ReferenceNumber, documentRecord.ExternalNumber, documentRecord.OcrReferenceNumbers), "الرقم المرجعي", 5, score, missing)
        DeductIfMissing(documentRecord.Summary, "الملخص", 8, score, missing)
        DeductIfMissing(FirstNotEmpty(documentRecord.OcrEntities, documentRecord.OcrNames, documentRecord.OcrDates), "الكيانات المستخرجة", 5, score, missing)

        If _settings IsNot Nothing AndAlso _settings.EnableFileSignatureValidation AndAlso Not documentRecord.FileSignatureVerified Then
            score -= 12
            missing.Add("بصمة الملف غير متحققة")
        End If
        If documentRecord.SecurityRiskScore >= 0.7 AndAlso _settings IsNot Nothing AndAlso _settings.EnableSensitiveDocumentEncryption AndAlso Not documentRecord.IsEncrypted Then
            score -= 5
            missing.Add("المستند الحساس بلا نسخة محمية")
        End If

        If documentRecord.AiConfidencePercent > 0 AndAlso documentRecord.AiConfidencePercent < 55 Then
            score -= 5
            missing.Add("ثقة التحليل منخفضة")
        End If

        Dim duplicateSuspected As Boolean = False
        If _repository IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(documentRecord.FileHashSha256) Then
            Dim sameHash = _repository.GetDocumentByHash(documentRecord.FileHashSha256)
            duplicateSuspected = sameHash IsNot Nothing AndAlso Not String.Equals(sameHash.Id, documentRecord.Id, StringComparison.OrdinalIgnoreCase)
            If duplicateSuspected Then score -= 8
        End If

        score = Math.Max(0, Math.Min(100, score))
        Dim minimum = If(_settings Is Nothing, 80, Math.Max(50, Math.Min(100, _settings.MinimumQualityScoreForApproval)))
        Dim sensitive = ContainsAny(documentRecord.Confidentiality, "سري", "سري للغاية") OrElse ContainsAny(documentRecord.Priority, "عاجل", "عاجل جداً")
        Dim needsReview = score < minimum OrElse sensitive OrElse duplicateSuspected

        Dim recommendation As String
        If duplicateSuspected Then
            recommendation = "تحقق من التكرار قبل الاعتماد أو الحفظ النهائي."
        ElseIf String.IsNullOrWhiteSpace(documentRecord.TextContent) Then
            recommendation = "ابدأ OCR ثم أعد تقييم جودة المستند."
        ElseIf missing.Contains("التصنيف العام") OrElse missing.Contains("التصنيف الثانوي") Then
            recommendation = "شغّل المعالجة الذكية لتعبئة التصنيف والملخص ثم راجع النتائج."
        ElseIf needsReview Then
            recommendation = "تحتاج البيانات إلى مراجعة بشرية قبل الاعتماد."
        Else
            recommendation = "المستند جاهز للاعتماد وفق بوابة الجودة."
        End If

        Return New DocumentQualityAssessment With {
            .DocumentId = documentRecord.Id,
            .Title = documentRecord.Title,
            .QualityScore = score,
            .Grade = GradeOf(score),
            .RiskLevel = RiskOf(score, sensitive, duplicateSuspected),
            .MissingFields = If(missing.Count = 0, "لا توجد نواقص جوهرية", String.Join("، ", missing.ToArray())),
            .RecommendedAction = recommendation,
            .NeedsHumanReview = needsReview,
            .IsDuplicateSuspected = duplicateSuspected
        }
    End Function

    Public Function AssessAll(Optional search As String = "") As List(Of DocumentQualityAssessment)
        Dim result As New List(Of DocumentQualityAssessment)()
        If _repository Is Nothing Then Return result
        For Each documentRecord In _repository.GetDocuments(search)
            result.Add(Assess(documentRecord))
        Next
        result.Sort(Function(a, b) a.QualityScore.CompareTo(b.QualityScore))
        Return result
    End Function

    Private Shared Sub DeductIfMissing(value As String, label As String, points As Integer, ByRef score As Integer, missing As List(Of String))
        If String.IsNullOrWhiteSpace(value) Then
            score -= points
            missing.Add(label)
        End If
    End Sub

    Private Shared Function FirstNotEmpty(ParamArray values() As String) As String
        If values Is Nothing Then Return ""
        For Each value In values
            If Not String.IsNullOrWhiteSpace(value) Then Return value
        Next
        Return ""
    End Function

    Private Shared Function ContainsAny(value As String, ParamArray terms() As String) As Boolean
        Dim source = If(value, "")
        For Each term In terms
            If source.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        Next
        Return False
    End Function

    Private Shared Function GradeOf(score As Integer) As String
        If score >= 90 Then Return "ممتاز"
        If score >= 80 Then Return "جيد جداً"
        If score >= 70 Then Return "جيد"
        If score >= 55 Then Return "يحتاج استكمال"
        Return "غير جاهز"
    End Function

    Private Shared Function RiskOf(score As Integer, sensitive As Boolean, duplicateSuspected As Boolean) As String
        If duplicateSuspected OrElse score < 55 Then Return "مرتفع"
        If sensitive OrElse score < 80 Then Return "متوسط"
        Return "منخفض"
    End Function
End Class
