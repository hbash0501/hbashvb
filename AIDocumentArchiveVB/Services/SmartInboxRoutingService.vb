Imports System
Imports System.Collections.Generic
Imports System.Data

Public Class SmartInboxRoutingService
    Private ReadOnly _repo As ArchiveRepository

    Public Sub New(repo As ArchiveRepository)
        _repo = repo
    End Sub

    Public Function GetAllDocuments() As List(Of DocumentRecord)
        Dim docs = _repo.GetDocuments()
        If docs Is Nothing Then Return New List(Of DocumentRecord)()
        Return docs
    End Function

    Public Function CountByStage(stageName As String) As Integer
        Return GetItems(stageName).Count
    End Function

    Public Function GetItems(stageName As String) As List(Of SmartInboxItem)
        Dim result As New List(Of SmartInboxItem)()
        For Each d In GetAllDocuments()
            If d Is Nothing Then Continue For
            Dim item = BuildItem(d, stageName)
            If item IsNot Nothing Then result.Add(item)
        Next
        result.Sort(Function(a As SmartInboxItem, b As SmartInboxItem) a.SortWeight.CompareTo(b.SortWeight))
        Return result
    End Function

    Public Function ToDataTable(items As List(Of SmartInboxItem)) As DataTable
        Dim table As New DataTable()
        table.Columns.Add("Id")
        table.Columns.Add("المسار")
        table.Columns.Add("الأولوية")
        table.Columns.Add("الرقم")
        table.Columns.Add("العنوان")
        table.Columns.Add("الحالة")
        table.Columns.Add("سبب الظهور")
        table.Columns.Add("الإجراء المقترح")

        If items IsNot Nothing Then
            For Each item In items
                table.Rows.Add(item.DocumentId, item.StageName, item.PriorityText, item.DisplayNumber, item.Title, item.StatusText, item.Reason, item.RecommendedAction)
            Next
        End If
        Return table
    End Function

    Public Function BuildDailyPlan() As String
        Dim lines As New List(Of String)()
        lines.Add("خطة تشغيل صندوق الوارد الذكي")
        lines.Add("التاريخ: " & DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
        lines.Add("")
        AddPlanLine(lines, "وارد جديد", "ابدأ بمعالجة الوارد الجديد وإدخاله في مسار OCR والتصنيف.")
        AddPlanLine(lines, "يحتاج OCR", "نفذ OCR أو الذكاء السحابي قبل التصنيف والاعتماد.")
        AddPlanLine(lines, "يحتاج تصنيف", "راجع التصنيف العام والفرعي واعتمد الاقتراحات.")
        AddPlanLine(lines, "يحتاج مراجعة", "افتح سير العمل وحدد اعتماد/إرجاع/استكمال.")
        AddPlanLine(lines, "عاجل أو سري", "أعط هذه المستندات أولوية قصوى وتحقق من الصلاحيات.")
        AddPlanLine(lines, "مكتمل ومؤرشف", "لا يحتاج إجراء إلا عند البحث أو إنشاء ملف معاملة.")
        Return String.Join(Environment.NewLine, lines.ToArray())
    End Function

    Private Sub AddPlanLine(lines As List(Of String), stageName As String, advice As String)
        Dim count = CountByStage(stageName)
        lines.Add("• " & stageName & ": " & count.ToString() & " مستند — " & advice)
    End Sub

    Private Function BuildItem(d As DocumentRecord, selectedStage As String) As SmartInboxItem
        Dim stage = DetectStage(d)
        If selectedStage <> "كل الصندوق" AndAlso stage <> selectedStage Then
            If selectedStage = "عاجل أو سري" AndAlso IsUrgentOrConfidential(d) Then
                stage = selectedStage
            ElseIf selectedStage = "ينتظر اعتماد" AndAlso IsWaitingApproval(d) Then
                stage = selectedStage
            Else
                Return Nothing
            End If
        End If

        Dim reason = GetReason(d, stage)
        If selectedStage = "كل الصندوق" AndAlso String.IsNullOrWhiteSpace(reason) Then Return Nothing

        Return New SmartInboxItem With {
            .DocumentId = d.Id,
            .StageName = stage,
            .PriorityText = Safe(d.Priority),
            .DisplayNumber = DisplayNumber(d),
            .Title = If(String.IsNullOrWhiteSpace(d.Title), "بدون عنوان", d.Title),
            .StatusText = Safe(d.Status),
            .Reason = reason,
            .RecommendedAction = GetAction(d, stage),
            .SortWeight = GetSortWeight(d, stage)
        }
    End Function

    Public Function DetectStage(d As DocumentRecord) As String
        If d Is Nothing Then Return "غير معروف"
        If IsUrgentOrConfidential(d) Then Return "عاجل أو سري"
        If HasNoOcr(d) Then Return "يحتاج OCR"
        If NeedsClassification(d) Then Return "يحتاج تصنيف"
        If IsWaitingApproval(d) Then Return "ينتظر اعتماد"
        If NeedsReview(d) Then Return "يحتاج مراجعة"
        If IsCompleted(d) Then Return "مكتمل ومؤرشف"
        Return "وارد جديد"
    End Function

    Private Function GetReason(d As DocumentRecord, stage As String) As String
        Select Case stage
            Case "وارد جديد"
                Return "مستند جديد يحتاج المرور على مسار الأرشفة."
            Case "يحتاج OCR"
                Return "لا يوجد نص مستخرج أو تقرير OCR ذكي."
            Case "يحتاج تصنيف"
                Return "التصنيف العام أو الفرعي أو البيانات الأساسية ناقصة."
            Case "يحتاج مراجعة"
                Return "حالة المستند تتطلب قراراً من الموظف أو المراجع."
            Case "ينتظر اعتماد"
                Return "بياناته الأساسية جاهزة وينتظر قرار سير العمل."
            Case "عاجل أو سري"
                Return "الأولوية عاجلة أو درجة السرية عالية."
            Case "مكتمل ومؤرشف"
                Return "المستند مكتمل ويمكن البحث عنه أو ربطه بمعاملة."
        End Select
        Return "يحتاج مراجعة عامة."
    End Function

    Private Function GetAction(d As DocumentRecord, stage As String) As String
        Select Case stage
            Case "وارد جديد"
                Return "افتح معالج الأرشفة أو شاشة المستند."
            Case "يحتاج OCR"
                Return "تشغيل OCR + OpenAI ثم مراجعة الحقول."
            Case "يحتاج تصنيف"
                Return "اعتماد التصنيف والوسوم والملخص."
            Case "يحتاج مراجعة", "ينتظر اعتماد"
                Return "فتح سير العمل لاعتماد أو إرجاع المستند."
            Case "عاجل أو سري"
                Return "معاينة سريعة ثم توجيه للمراجع المختص."
            Case "مكتمل ومؤرشف"
                Return "معاينة أو إضافة إلى ملف معاملة."
        End Select
        Return "معاينة المستند."
    End Function

    Private Function GetSortWeight(d As DocumentRecord, stage As String) As Integer
        Dim weight As Integer = 1000
        If IsUrgentOrConfidential(d) Then weight -= 400
        If Safe(d.Priority) = "عاجل" Then weight -= 250
        If Safe(d.Confidentiality) = "سري" Then weight -= 200
        If stage = "يحتاج OCR" Then weight -= 180
        If stage = "يحتاج تصنيف" Then weight -= 150
        If stage = "ينتظر اعتماد" Then weight -= 130
        If stage = "يحتاج مراجعة" Then weight -= 110
        If d.CreatedAt.Date = DateTime.Today Then weight -= 60
        Return weight
    End Function

    Public Function HasNoOcr(d As DocumentRecord) As Boolean
        If d Is Nothing Then Return True
        Return String.IsNullOrWhiteSpace(d.TextContent) AndAlso String.IsNullOrWhiteSpace(d.OcrStructuredReport)
    End Function

    Public Function NeedsClassification(d As DocumentRecord) As Boolean
        If d Is Nothing Then Return True
        If String.IsNullOrWhiteSpace(d.GeneralCategory) Then Return True
        If String.IsNullOrWhiteSpace(d.SubCategory) Then Return True
        If String.IsNullOrWhiteSpace(d.Title) Then Return True
        If String.IsNullOrWhiteSpace(d.ArchiveNumber) AndAlso String.IsNullOrWhiteSpace(d.ReferenceNumber) Then Return True
        Return False
    End Function

    Public Function NeedsReview(d As DocumentRecord) As Boolean
        Dim s = Safe(d.Status)
        Return s = "قيد المراجعة" OrElse s = "يحتاج استكمال" OrElse s = "جديد"
    End Function

    Public Function IsWaitingApproval(d As DocumentRecord) As Boolean
        If d Is Nothing Then Return False
        Dim ready = Not HasNoOcr(d) AndAlso Not NeedsClassification(d)
        Return ready AndAlso Safe(d.Status) = "قيد المراجعة"
    End Function

    Public Function IsUrgentOrConfidential(d As DocumentRecord) As Boolean
        If d Is Nothing Then Return False
        Return Safe(d.Priority) = "عاجل" OrElse Safe(d.Confidentiality) = "سري" OrElse Safe(d.Confidentiality) = "سري للغاية"
    End Function

    Public Function IsCompleted(d As DocumentRecord) As Boolean
        Dim s = Safe(d.Status)
        Return s = "معتمد" OrElse s = "مؤرشف" OrElse s = "مؤرشف نهائيًا" OrElse s = "مؤرشف نهائياً"
    End Function

    Private Function DisplayNumber(d As DocumentRecord) As String
        If d Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(d.ArchiveNumber) Then Return d.ArchiveNumber
        If Not String.IsNullOrWhiteSpace(d.ReferenceNumber) Then Return d.ReferenceNumber
        If Not String.IsNullOrWhiteSpace(d.ExternalNumber) Then Return d.ExternalNumber
        Return "بدون رقم"
    End Function

    Private Shared Function Safe(value As String) As String
        Return If(value, "").Trim()
    End Function
End Class
