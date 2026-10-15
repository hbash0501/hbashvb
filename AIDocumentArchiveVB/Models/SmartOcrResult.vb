Imports System
Imports System.Collections.Generic
Imports System.Text

Public Class SmartOcrResult
    Public Property RawText As String
    Public Property CleanText As String
    Public Property SuggestedTitle As String
    Public Property SuggestedSummary As String
    Public Property SuggestedTags As String
    Public Property SuggestedGeneralCategory As String
    Public Property SuggestedSubCategory As String
    Public Property SuggestedPriority As String
    Public Property SuggestedConfidentiality As String
    Public Property DocumentType As String
    Public Property ReferenceNumber As String
    Public Property SourceEntity As String
    Public Property DestinationEntity As String
    Public Property QualityScore As Integer
    Public Property EngineInfo As String
    Public Property AiNotes As String

    Public Property Dates As List(Of String)
    Public Property ReferenceNumbers As List(Of String)
    Public Property IncomingNumbers As List(Of String)
    Public Property OutgoingNumbers As List(Of String)
    Public Property Amounts As List(Of String)
    Public Property NationalIds As List(Of String)
    Public Property IqamaNumbers As List(Of String)
    Public Property PassportNumbers As List(Of String)
    Public Property PhoneNumbers As List(Of String)
    Public Property Emails As List(Of String)
    Public Property Names As List(Of String)
    Public Property Entities As List(Of String)
    Public Property Warnings As List(Of String)

    Public Sub New()
        RawText = ""
        CleanText = ""
        SuggestedTitle = ""
        SuggestedSummary = ""
        SuggestedTags = ""
        SuggestedGeneralCategory = "إداري"
        SuggestedSubCategory = "خطابات داخلية"
        SuggestedPriority = "متوسط"
        SuggestedConfidentiality = "عادي"
        DocumentType = "غير محدد"
        ReferenceNumber = ""
        SourceEntity = ""
        DestinationEntity = ""
        EngineInfo = ""
        AiNotes = ""
        Dates = New List(Of String)()
        ReferenceNumbers = New List(Of String)()
        IncomingNumbers = New List(Of String)()
        OutgoingNumbers = New List(Of String)()
        Amounts = New List(Of String)()
        NationalIds = New List(Of String)()
        IqamaNumbers = New List(Of String)()
        PassportNumbers = New List(Of String)()
        PhoneNumbers = New List(Of String)()
        Emails = New List(Of String)()
        Names = New List(Of String)()
        Entities = New List(Of String)()
        Warnings = New List(Of String)()
    End Sub

    Public Function BuildReport() As String
        Dim sb As New StringBuilder()
        sb.AppendLine("تقرير OCR الذكي")
        sb.AppendLine("================")
        sb.AppendLine("نوع المستند: " & Safe(DocumentType))
        sb.AppendLine("العنوان المقترح: " & Safe(SuggestedTitle))
        sb.AppendLine("التصنيف: " & Safe(SuggestedGeneralCategory) & " / " & Safe(SuggestedSubCategory))
        sb.AppendLine("الأولوية: " & Safe(SuggestedPriority))
        sb.AppendLine("السرية: " & Safe(SuggestedConfidentiality))
        sb.AppendLine("درجة جودة الاستخراج: " & QualityScore.ToString() & "%")
        sb.AppendLine("المحرك: " & Safe(EngineInfo))
        sb.AppendLine()
        sb.AppendLine("الأرقام والمراجع")
        sb.AppendLine("---------------")
        sb.AppendLine("رقم مرجعي مختار: " & Safe(ReferenceNumber))
        sb.AppendLine("أرقام الصادر: " & JoinList(OutgoingNumbers))
        sb.AppendLine("أرقام الوارد: " & JoinList(IncomingNumbers))
        sb.AppendLine("كل الأرقام المرجعية: " & JoinList(ReferenceNumbers))
        sb.AppendLine()
        sb.AppendLine("التواريخ والمبالغ")
        sb.AppendLine("----------------")
        sb.AppendLine("التواريخ: " & JoinList(Dates))
        sb.AppendLine("المبالغ: " & JoinList(Amounts))
        sb.AppendLine()
        sb.AppendLine("الأطراف والجهات")
        sb.AppendLine("--------------")
        sb.AppendLine("جهة الصادر/المصدر: " & Safe(SourceEntity))
        sb.AppendLine("جهة الوارد/المستلم: " & Safe(DestinationEntity))
        sb.AppendLine("الجهات المكتشفة: " & JoinList(Entities))
        sb.AppendLine("الأسماء المحتملة: " & JoinList(Names))
        sb.AppendLine()
        sb.AppendLine("المعرفات ووسائل الاتصال")
        sb.AppendLine("----------------------")
        sb.AppendLine("أرقام السجل/الهوية: " & JoinList(NationalIds))
        sb.AppendLine("أرقام الإقامة: " & JoinList(IqamaNumbers))
        sb.AppendLine("أرقام الجوازات: " & JoinList(PassportNumbers))
        sb.AppendLine("الجوالات: " & JoinList(PhoneNumbers))
        sb.AppendLine("البريد الإلكتروني: " & JoinList(Emails))
        sb.AppendLine()
        sb.AppendLine("الملخص")
        sb.AppendLine("------")
        sb.AppendLine(Safe(SuggestedSummary))
        sb.AppendLine()
        sb.AppendLine("الوسوم")
        sb.AppendLine("------")
        sb.AppendLine(Safe(SuggestedTags))
        If Warnings.Count > 0 Then
            sb.AppendLine()
            sb.AppendLine("تنبيهات جودة")
            sb.AppendLine("------------")
            For Each w In Warnings
                sb.AppendLine("- " & w)
            Next
        End If
        If Not String.IsNullOrWhiteSpace(AiNotes) Then
            sb.AppendLine()
            sb.AppendLine("ملاحظات الذكاء الاصطناعي")
            sb.AppendLine("------------------------")
            sb.AppendLine(AiNotes)
        End If
        Return sb.ToString()
    End Function

    Private Shared Function JoinList(items As List(Of String)) As String
        If items Is Nothing OrElse items.Count = 0 Then Return ""
        Return String.Join("، ", items.ToArray())
    End Function

    Private Shared Function Safe(value As String) As String
        If value Is Nothing Then Return ""
        Return value.Trim()
    End Function
End Class
