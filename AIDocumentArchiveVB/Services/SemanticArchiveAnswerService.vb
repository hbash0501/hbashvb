Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Linq
Imports System.Text

Public Class SemanticArchiveAnswerService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _knowledge As KnowledgeIndexService
    Private ReadOnly _relations As RelationDiscoveryService
    Private ReadOnly _openai As OpenAIClientService

    Public Sub New(settings As AppSettings, repository As ArchiveRepository)
        _settings = settings
        _knowledge = New KnowledgeIndexService(settings, repository)
        _relations = New RelationDiscoveryService(repository)
        _openai = New OpenAIClientService(settings)
    End Sub

    Public Function RebuildIndex() As Integer
        Return _knowledge.RebuildIndex()
    End Function

    Public Function Search(question As String, Optional maxResults As Integer = 12) As List(Of KnowledgeSearchResult)
        Return _knowledge.Search(question, maxResults)
    End Function

    Public Function Ask(question As String, Optional maxResults As Integer = 10, Optional includeRelations As Boolean = True) As ArchiveAnswerResult
        Dim answer As New ArchiveAnswerResult()
        answer.Question = If(question, "").Trim()
        If String.IsNullOrWhiteSpace(answer.Question) Then
            answer.Answer = "اكتب سؤالاً أو موضوعاً للبحث في الأرشيف."
            Return answer
        End If

        answer.Evidence = _knowledge.Search(answer.Question, maxResults)
        If includeRelations Then
            answer.Relations = _relations.DiscoverForDocuments(answer.Evidence.Select(Function(r) r.Document), 20)
        End If

        If answer.Evidence.Count = 0 Then
            answer.Answer = "لم أجد مستندات مطابقة في فهرس المعرفة. جرّب إعادة بناء الفهرس أو استخدام كلمات أخرى."
            Return answer
        End If

        Dim localContext = BuildContext(answer.Evidence, answer.Relations)
        If _openai IsNot Nothing AndAlso _openai.IsEnabled() Then
            Dim prompt As New StringBuilder()
            prompt.AppendLine("أنت مساعد معرفة أرشيفية يعمل داخل منصة أرشفة إدارية.")
            prompt.AppendLine("أجب اعتماداً على سياق الأرشيف فقط، ولا تخترع بيانات غير موجودة.")
            prompt.AppendLine("المطلوب في الإجابة:")
            prompt.AppendLine("1- خلاصة تنفيذية قصيرة.")
            prompt.AppendLine("2- المستندات ذات العلاقة مع أرقامها أو عناوينها.")
            prompt.AppendLine("3- الأسماء/الجهات/الأرقام/التواريخ/المبالغ المهمة إن وجدت.")
            prompt.AppendLine("4- العلاقات المكتشفة بين المستندات.")
            prompt.AppendLine("5- توصية عملية للموظف أو المدير.")
            prompt.AppendLine()
            prompt.AppendLine("سؤال المستخدم:")
            prompt.AppendLine(answer.Question)
            prompt.AppendLine()
            prompt.AppendLine("سياق الأرشيف:")
            prompt.AppendLine(localContext)
            answer.Answer = _openai.Generate(prompt.ToString(), "أنت مساعد عربي رسمي ودقيق للأرشفة المؤسسية. التزم بالسياق المعروض فقط.", 240000)
            If String.IsNullOrWhiteSpace(answer.Answer) Then answer.Answer = BuildLocalAnswer(answer)
        Else
            answer.Answer = BuildLocalAnswer(answer)
        End If

        Return answer
    End Function

    Public Function EvidenceToDataTable(result As ArchiveAnswerResult) As DataTable
        If result Is Nothing Then Return _knowledge.ResultsToDataTable(New List(Of KnowledgeSearchResult)())
        Return _knowledge.ResultsToDataTable(result.Evidence)
    End Function

    Public Function RelationsToDataTable(result As ArchiveAnswerResult) As DataTable
        If result Is Nothing Then Return _relations.RelationsToDataTable(New List(Of ArchiveRelation)())
        Return _relations.RelationsToDataTable(result.Relations)
    End Function

    Public Function BuildCaseFolder(question As String, result As ArchiveAnswerResult, outputFolder As String) As String
        If result Is Nothing Then result = Ask(question, 10, True)
        Return _relations.BuildSmartCaseFolder(question, result.Evidence, result.Relations, outputFolder)
    End Function

    Public Function ExportRelationsHtml(result As ArchiveAnswerResult, outputFolder As String) As String
        If result Is Nothing Then Return ""
        Return _relations.ExportRelationsHtml(result.Relations, outputFolder)
    End Function

    Private Function BuildContext(results As List(Of KnowledgeSearchResult), relations As List(Of ArchiveRelation)) As String
        Dim sb As New StringBuilder()
        sb.AppendLine(_knowledge.BuildContext(results, 15000))
        sb.AppendLine()
        sb.AppendLine("علاقات مكتشفة بين المستندات:")
        If relations Is Nothing OrElse relations.Count = 0 Then
            sb.AppendLine("لا توجد علاقات قوية مكتشفة.")
        Else
            Dim i As Integer = 1
            For Each r In relations.Take(20)
                sb.AppendLine(i & ") " & SafeTitle(r.SourceDocument) & " ⇦⇨ " & SafeTitle(r.TargetDocument) & " | " & r.RelationType & " | " & r.ConfidencePercent & "%")
                sb.AppendLine("   " & r.Reason & " | " & r.SharedValues)
                i += 1
            Next
        End If
        Return LimitText(sb.ToString(), 23000)
    End Function

    Private Function BuildLocalAnswer(result As ArchiveAnswerResult) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("الذكاء السحابي غير مفعّل أو لم يُرجع إجابة، وهذه بطاقة معرفة محلية من نتائج الأرشيف:")
        sb.AppendLine()
        sb.AppendLine("السؤال/الموضوع: " & result.Question)
        sb.AppendLine("عدد المستندات المطابقة: " & result.Evidence.Count)
        sb.AppendLine()
        sb.AppendLine("أقرب المستندات:")
        Dim i As Integer = 1
        For Each r In result.Evidence.Take(10)
            Dim d = r.Document
            If d Is Nothing Then Continue For
            sb.AppendLine(i & ") " & d.Title & " | " & If(String.IsNullOrWhiteSpace(d.ArchiveNumber), d.ReferenceNumber, d.ArchiveNumber) & " | الدرجة: " & r.Score)
            sb.AppendLine("   الدليل: " & r.Evidence)
            If Not String.IsNullOrWhiteSpace(d.SourceEntity & d.DestinationEntity) Then sb.AppendLine("   الجهة: " & d.SourceEntity & " ← " & d.DestinationEntity)
            If Not String.IsNullOrWhiteSpace(d.OcrAmounts) Then sb.AppendLine("   مبالغ: " & d.OcrAmounts)
            If Not String.IsNullOrWhiteSpace(d.OcrDates) Then sb.AppendLine("   تواريخ: " & d.OcrDates)
            i += 1
        Next
        sb.AppendLine()
        If result.Relations IsNot Nothing AndAlso result.Relations.Count > 0 Then
            sb.AppendLine("علاقات مهمة:")
            For Each rel In result.Relations.Take(10)
                sb.AppendLine("- " & SafeTitle(rel.SourceDocument) & " ⇦⇨ " & SafeTitle(rel.TargetDocument) & " | " & rel.RelationType & " | " & rel.ConfidencePercent & "%")
            Next
        End If
        Return sb.ToString()
    End Function

    Private Shared Function SafeTitle(d As DocumentRecord) As String
        If d Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(d.Title) Then Return d.Title
        If Not String.IsNullOrWhiteSpace(d.ArchiveNumber) Then Return d.ArchiveNumber
        Return d.FileName
    End Function

    Private Shared Function LimitText(value As String, maxChars As Integer) As String
        If value Is Nothing Then Return ""
        If value.Length <= maxChars Then Return value
        Return value.Substring(0, maxChars) & Environment.NewLine & "..."
    End Function
End Class
