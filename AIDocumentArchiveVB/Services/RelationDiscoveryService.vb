Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions

Public Class RelationDiscoveryService
    Private ReadOnly _repository As ArchiveRepository

    Public Sub New(repository As ArchiveRepository)
        _repository = repository
    End Sub

    Public Function DiscoverForDocument(documentId As String, Optional maxRelations As Integer = 25) As List(Of ArchiveRelation)
        Dim selected = _repository.GetDocumentById(documentId)
        If selected Is Nothing Then Return New List(Of ArchiveRelation)()
        Dim docs = _repository.GetDocuments().Where(Function(d) d.Id <> selected.Id).ToList()
        Dim results As New List(Of ArchiveRelation)()
        For Each target In docs
            Dim relation = ScoreRelation(selected, target)
            If relation IsNot Nothing AndAlso relation.ConfidencePercent >= 35 Then results.Add(relation)
        Next
        Return results.OrderByDescending(Function(r) r.ConfidencePercent).Take(maxRelations).ToList()
    End Function

    Public Function DiscoverForDocuments(documents As IEnumerable(Of DocumentRecord), Optional maxRelations As Integer = 40) As List(Of ArchiveRelation)
        Dim input = If(documents, New List(Of DocumentRecord)()).Where(Function(d) d IsNot Nothing).ToList()
        Dim allDocs = _repository.GetDocuments()
        Dim found As New Dictionary(Of String, ArchiveRelation)(StringComparer.OrdinalIgnoreCase)
        For Each source In input
            For Each target In allDocs
                If target Is Nothing OrElse target.Id = source.Id Then Continue For
                Dim relation = ScoreRelation(source, target)
                If relation Is Nothing OrElse relation.ConfidencePercent < 38 Then Continue For
                Dim key1 = source.Id & "|" & target.Id
                Dim key2 = target.Id & "|" & source.Id
                If Not found.ContainsKey(key1) AndAlso Not found.ContainsKey(key2) Then found(key1) = relation
            Next
        Next
        Return found.Values.OrderByDescending(Function(r) r.ConfidencePercent).Take(maxRelations).ToList()
    End Function

    Public Function RelationsToDataTable(relations As List(Of ArchiveRelation)) As DataTable
        Dim t As New DataTable()
        t.Columns.Add("المستند الأساسي")
        t.Columns.Add("المستند المرتبط")
        t.Columns.Add("نوع العلاقة")
        t.Columns.Add("الثقة")
        t.Columns.Add("سبب الربط")
        If relations Is Nothing Then Return t
        For Each r In relations
            t.Rows.Add(SafeTitle(r.SourceDocument), SafeTitle(r.TargetDocument), r.RelationType, r.ConfidencePercent & "%", r.Reason)
        Next
        Return t
    End Function

    Public Function BuildRelationsReport(relations As List(Of ArchiveRelation)) As String
        If relations Is Nothing OrElse relations.Count = 0 Then Return "لا توجد علاقات واضحة بين نتائج البحث الحالية وبقية الأرشيف."
        Dim sb As New StringBuilder()
        sb.AppendLine("شبكة العلاقات الذكية بين المستندات")
        sb.AppendLine("=================================")
        sb.AppendLine()
        Dim i As Integer = 1
        For Each r In relations
            sb.AppendLine(i & ") " & SafeTitle(r.SourceDocument) & "  ⇦⇨  " & SafeTitle(r.TargetDocument))
            sb.AppendLine("   نوع العلاقة: " & r.RelationType & " | الثقة: " & r.ConfidencePercent & "%")
            sb.AppendLine("   السبب: " & r.Reason)
            If Not String.IsNullOrWhiteSpace(r.SharedValues) Then sb.AppendLine("   القيم المشتركة: " & r.SharedValues)
            sb.AppendLine()
            i += 1
        Next
        Return sb.ToString()
    End Function

    Public Function ExportRelationsHtml(relations As List(Of ArchiveRelation), outputFolder As String) As String
        Directory.CreateDirectory(outputFolder)
        Dim htmlPath = Path.Combine(outputFolder, "Archive_Relations_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".html")
        Dim sb As New StringBuilder()
        sb.AppendLine("<!doctype html><html lang='ar' dir='rtl'><head><meta charset='utf-8'>")
        sb.AppendLine("<title>شبكة علاقات الأرشيف</title><style>body{font-family:Tahoma,Arial;background:#f6f8f7;color:#172b27;margin:32px}.card{background:#fff;border:1px solid #d9e1dd;border-radius:18px;padding:18px;margin:14px 0;box-shadow:0 8px 24px rgba(0,0,0,.06)}.score{color:#8a6a1f;font-weight:bold}.meta{color:#53645f;font-size:13px}</style></head><body>")
        sb.AppendLine("<h1>شبكة علاقات الأرشيف الذكية</h1><p class='meta'>تم الإنشاء: " & Html(DateTime.Now.ToString("yyyy/MM/dd HH:mm")) & "</p>")
        If relations IsNot Nothing Then
            For Each r In relations
                sb.AppendLine("<div class='card'><h2>" & Html(SafeTitle(r.SourceDocument)) & " ⇦⇨ " & Html(SafeTitle(r.TargetDocument)) & "</h2>")
                sb.AppendLine("<div class='score'>" & Html(r.RelationType) & " — " & r.ConfidencePercent & "%</div>")
                sb.AppendLine("<p>" & Html(r.Reason) & "</p><p class='meta'>" & Html(r.SharedValues) & "</p></div>")
            Next
        End If
        sb.AppendLine("</body></html>")
        File.WriteAllText(htmlPath, sb.ToString(), New UTF8Encoding(False))
        Return htmlPath
    End Function

    Public Function BuildSmartCaseFolder(question As String, results As List(Of KnowledgeSearchResult), relations As List(Of ArchiveRelation), outputFolder As String) As String
        Dim safeName = Regex.Replace(If(question, "ملف_معاملة"), "[^\p{L}\p{Nd}_\- ]+", " ").Trim()
        If safeName.Length > 60 Then safeName = safeName.Substring(0, 60).Trim()
        If String.IsNullOrWhiteSpace(safeName) Then safeName = "ملف_معاملة"
        Dim caseFolder = Path.Combine(outputFolder, "SmartCase_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & "_" & safeName.Replace(" ", "_"))
        Directory.CreateDirectory(caseFolder)
        Dim docsFolder = Path.Combine(caseFolder, "Documents")
        Directory.CreateDirectory(docsFolder)

        If results IsNot Nothing Then
            For Each item In results.Take(20)
                Dim d = item.Document
                If d Is Nothing OrElse String.IsNullOrWhiteSpace(d.StoredPath) OrElse Not File.Exists(d.StoredPath) Then Continue For
                Try
                    Dim fileName = CleanFileName(If(String.IsNullOrWhiteSpace(d.ArchiveNumber), d.Title, d.ArchiveNumber) & "_" & Path.GetFileName(d.StoredPath))
                    File.Copy(d.StoredPath, Path.Combine(docsFolder, fileName), True)
                Catch
                End Try
            Next
        End If

        File.WriteAllText(Path.Combine(caseFolder, "README_CASE.txt"), BuildCaseText(question, results, relations), New UTF8Encoding(False))
        File.WriteAllText(Path.Combine(caseFolder, "case_index.html"), BuildCaseHtml(question, results, relations), New UTF8Encoding(False))
        Return caseFolder
    End Function

    Private Function BuildCaseText(question As String, results As List(Of KnowledgeSearchResult), relations As List(Of ArchiveRelation)) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("ملف معاملة ذكي")
        sb.AppendLine("================")
        sb.AppendLine("موضوع/سؤال الملف: " & question)
        sb.AppendLine("وقت الإنشاء: " & DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
        sb.AppendLine()
        sb.AppendLine("المستندات الأساسية:")
        If results IsNot Nothing Then
            Dim i As Integer = 1
            For Each r In results
                Dim d = r.Document
                If d Is Nothing Then Continue For
                sb.AppendLine(i & ") " & d.Title & " | " & If(String.IsNullOrWhiteSpace(d.ArchiveNumber), d.ReferenceNumber, d.ArchiveNumber) & " | درجة: " & r.Score)
                sb.AppendLine("   الدليل: " & r.Evidence)
                i += 1
            Next
        End If
        sb.AppendLine()
        sb.AppendLine(BuildRelationsReport(relations))
        Return sb.ToString()
    End Function

    Private Function BuildCaseHtml(question As String, results As List(Of KnowledgeSearchResult), relations As List(Of ArchiveRelation)) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<!doctype html><html lang='ar' dir='rtl'><head><meta charset='utf-8'><title>ملف معاملة ذكي</title>")
        sb.AppendLine("<style>body{font-family:Tahoma,Arial;background:#f7f9f8;color:#10201d;margin:30px}.hero{background:#173a36;color:white;border-radius:22px;padding:24px}.gold{color:#d6b05c}.card{background:white;border-radius:18px;padding:16px;margin:14px 0;border:1px solid #dfe8e4;box-shadow:0 8px 22px rgba(0,0,0,.05)}.small{color:#63736e;font-size:13px}</style></head><body>")
        sb.AppendLine("<div class='hero'><h1>ملف معاملة ذكي</h1><div class='gold'>" & Html(question) & "</div><p>تم الإنشاء: " & Html(DateTime.Now.ToString("yyyy/MM/dd HH:mm")) & "</p></div>")
        sb.AppendLine("<h2>المستندات الأساسية</h2>")
        If results IsNot Nothing Then
            For Each r In results
                Dim d = r.Document
                If d Is Nothing Then Continue For
                sb.AppendLine("<div class='card'><h3>" & Html(d.Title) & "</h3><div class='small'>" & Html(If(String.IsNullOrWhiteSpace(d.ArchiveNumber), d.ReferenceNumber, d.ArchiveNumber)) & " — درجة: " & r.Score & "</div><p>" & Html(r.Evidence) & "</p></div>")
            Next
        End If
        sb.AppendLine("<h2>العلاقات المكتشفة</h2>")
        If relations IsNot Nothing Then
            For Each rel In relations.Take(30)
                sb.AppendLine("<div class='card'><b>" & Html(SafeTitle(rel.SourceDocument)) & " ⇦⇨ " & Html(SafeTitle(rel.TargetDocument)) & "</b><br><span class='gold'>" & Html(rel.RelationType) & " — " & rel.ConfidencePercent & "%</span><p>" & Html(rel.Reason) & "</p></div>")
            Next
        End If
        sb.AppendLine("</body></html>")
        Return sb.ToString()
    End Function

    Private Function ScoreRelation(source As DocumentRecord, target As DocumentRecord) As ArchiveRelation
        Dim score As Integer = 0
        Dim reasons As New List(Of String)()
        Dim sharedValues As New List(Of String)()

        AddScoreForSharedValues(source.OcrNationalIds, target.OcrNationalIds, 45, "تطابق رقم هوية/سجل مدني", score, reasons, sharedValues)
        AddScoreForSharedValues(source.OcrIqamaNumbers, target.OcrIqamaNumbers, 45, "تطابق رقم إقامة", score, reasons, sharedValues)
        AddScoreForSharedValues(source.OcrPassportNumbers, target.OcrPassportNumbers, 45, "تطابق رقم جواز", score, reasons, sharedValues)
        AddScoreForSharedValues(source.OcrIncomingNumbers & "،" & source.OcrOutgoingNumbers & "،" & source.OcrReferenceNumbers & "،" & source.ReferenceNumber, target.OcrIncomingNumbers & "،" & target.OcrOutgoingNumbers & "،" & target.OcrReferenceNumbers & "،" & target.ReferenceNumber, 35, "اشتراك في أرقام وارد/صادر/مرجع", score, reasons, sharedValues)
        AddScoreForSharedValues(source.OcrNames, target.OcrNames, 22, "اشتراك في أسماء أشخاص", score, reasons, sharedValues)
        AddScoreForSharedValues(source.OcrEntities & "،" & source.SourceEntity & "،" & source.DestinationEntity, target.OcrEntities & "،" & target.SourceEntity & "،" & target.DestinationEntity, 18, "اشتراك في جهات", score, reasons, sharedValues)

        If Not String.IsNullOrWhiteSpace(source.GeneralCategory) AndAlso source.GeneralCategory = target.GeneralCategory Then score += 8
        If Not String.IsNullOrWhiteSpace(source.SubCategory) AndAlso source.SubCategory = target.SubCategory Then score += 8

        Dim semanticScore = TextSimilarityScore(BuildText(source), BuildText(target))
        If semanticScore >= 30 Then
            score += Math.Min(28, semanticScore)
            reasons.Add("تشابه دلالي/موضوعي في النص بنسبة تقريبية " & semanticScore & "%")
        End If

        If score > 100 Then score = 100
        If score < 25 Then Return Nothing

        Dim r As New ArchiveRelation()
        r.SourceDocument = source
        r.TargetDocument = target
        r.ConfidencePercent = score
        r.SharedValues = String.Join("، ", sharedValues.Distinct().Take(12).ToArray())
        r.Reason = If(reasons.Count = 0, "تشابه عام في بيانات المستند.", String.Join("، ", reasons.Distinct().ToArray()))
        r.RelationType = GuessRelationType(source, target, reasons, sharedValues)
        Return r
    End Function

    Private Shared Sub AddScoreForSharedValues(a As String, b As String, weight As Integer, reason As String, ByRef score As Integer, reasons As List(Of String), sharedValues As List(Of String))
        Dim left = SplitValues(a)
        Dim right = SplitValues(b)
        If left.Count = 0 OrElse right.Count = 0 Then Return
        Dim overlap = left.Intersect(right, StringComparer.OrdinalIgnoreCase).ToList()
        If overlap.Count = 0 Then Return
        score += Math.Min(weight, 10 + overlap.Count * 10)
        reasons.Add(reason)
        sharedValues.AddRange(overlap)
    End Sub

    Private Shared Function SplitValues(raw As String) As List(Of String)
        Dim list As New List(Of String)()
        If String.IsNullOrWhiteSpace(raw) Then Return list
        For Each p In Regex.Split(raw, "[،,;\r\n|]+")
            Dim v = Regex.Replace(If(p, ""), "\s+", " ").Trim()
            If v.Length >= 3 AndAlso Not list.Any(Function(x) String.Equals(x, v, StringComparison.OrdinalIgnoreCase)) Then list.Add(v)
        Next
        Return list
    End Function

    Private Shared Function TextSimilarityScore(a As String, b As String) As Integer
        Dim ta = Tokens(a)
        Dim tb = Tokens(b)
        If ta.Count = 0 OrElse tb.Count = 0 Then Return 0
        Dim intersection = ta.Intersect(tb, StringComparer.OrdinalIgnoreCase).Count()
        Dim union = ta.Union(tb, StringComparer.OrdinalIgnoreCase).Count()
        If union = 0 Then Return 0
        Return CInt(Math.Round((intersection / CDbl(union)) * 100.0R))
    End Function

    Private Shared Function Tokens(text As String) As List(Of String)
        Dim list As New List(Of String)()
        If String.IsNullOrWhiteSpace(text) Then Return list
        For Each m As Match In Regex.Matches(text.ToLowerInvariant(), "[\p{L}\p{Nd}]{3,}")
            Dim v = m.Value.Trim()
            If IsStopWord(v) Then Continue For
            If Not list.Contains(v) Then list.Add(v)
            If list.Count >= 300 Then Exit For
        Next
        Return list
    End Function

    Private Shared Function IsStopWord(token As String) As Boolean
        Select Case token
            Case "هذا", "هذه", "ذلك", "التي", "الذي", "على", "إلى", "الى", "من", "عن", "في", "مع", "كان", "تم", "غير", "وقد", "وذلك", "كما", "the", "and", "for", "with"
                Return True
        End Select
        Return False
    End Function

    Private Shared Function BuildText(d As DocumentRecord) As String
        If d Is Nothing Then Return ""
        Return String.Join(" ", New String() {d.Title, d.Summary, d.Tags, d.TextContent, d.OcrStructuredReport, d.SourceEntity, d.DestinationEntity, d.OcrNames, d.OcrEntities})
    End Function

    Private Shared Function GuessRelationType(source As DocumentRecord, target As DocumentRecord, reasons As List(Of String), sharedValues As List(Of String)) As String
        Dim combined = String.Join(" ", reasons).ToLowerInvariant()
        If combined.Contains("وارد") OrElse combined.Contains("صادر") OrElse combined.Contains("مرجع") Then Return "سلسلة مراسلات / وارد وصادر"
        If combined.Contains("هوية") OrElse combined.Contains("إقامة") OrElse combined.Contains("جواز") Then Return "ملف شخص / هوية مشتركة"
        If combined.Contains("جهات") Then Return "جهة مشتركة"
        If combined.Contains("أسماء") Then Return "شخص أو موضوع مشترك"
        Return "تشابه موضوعي"
    End Function

    Private Shared Function SafeTitle(d As DocumentRecord) As String
        If d Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(d.Title) Then Return d.Title
        If Not String.IsNullOrWhiteSpace(d.ArchiveNumber) Then Return d.ArchiveNumber
        Return d.FileName
    End Function

    Private Shared Function Html(value As String) As String
        If value Is Nothing Then Return ""
        Return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;")
    End Function

    Private Shared Function CleanFileName(value As String) As String
        Dim invalid = Path.GetInvalidFileNameChars()
        Dim sb As New StringBuilder()
        For Each ch In value
            If invalid.Contains(ch) Then
                sb.Append("_"c)
            Else
                sb.Append(ch)
            End If
        Next
        Dim s = sb.ToString().Trim()
        If s.Length > 120 Then s = s.Substring(0, 120)
        If String.IsNullOrWhiteSpace(s) Then s = "document"
        Return s
    End Function
End Class
