Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading

' محرك محلي قابل للاختبار: BM25، تطبيع عربي، تنبيهات، أدلة بمواضعها الأصلية.
' درجة البحث ترتيب نسبي؛ ليست نسبة ثقة أو إثباتاً لصحة المحتوى.
Public Class BasiraEngine
    Private Class Entry
        Public Document As DocumentRecord
        Public Frequencies As Dictionary(Of String, Integer)
        Public Length As Integer
        Public Title As String
    End Class
    Private ReadOnly _entries As New List(Of Entry)
    Private ReadOnly _postings As New Dictionary(Of String, List(Of Entry))(StringComparer.Ordinal)
    Private ReadOnly _averageLength As Double
    Private Shared ReadOnly Stops As New HashSet(Of String)("من في على علي الى الي عن هل ما ماذا متى متي هو هي و او أو كيف اريد أريد ابحث البحث جميع التي الذي هذه هذا المستندات مستندات اعطني لي مع هل كان تم".Split(" "c), StringComparer.Ordinal)
    Private Shared ReadOnly Aliases As New Dictionary(Of String, String()) From {
        {"عقد", New String() {"عقود"}}, {"فاتوره", New String() {"فواتير"}},
        {"تعميم", New String() {"تعاميم"}}, {"خطاب", New String() {"خطابات"}},
        {"مورد", New String() {"موردين", "توريد"}}, {"اجازه", New String() {"اجازات"}}}

    Public Sub New(documents As IEnumerable(Of DocumentRecord))
        For Each d In documents
            Dim text = String.Join(" ", New String() {d.Title, d.ArchiveNumber, d.ReferenceNumber, d.Tags, d.SourceEntity, d.DestinationEntity, d.GeneralCategory, d.SubCategory, d.Summary, d.TextContent, d.OcrNames, d.OcrEntities})
            Dim words = Tokens(text).ToList()
            Dim e As New Entry With {.Document = d, .Title = Normalize(d.Title), .Length = Math.Max(1, words.Count), .Frequencies = New Dictionary(Of String, Integer)(StringComparer.Ordinal)}
            For Each word In words
                If Not e.Frequencies.ContainsKey(word) Then e.Frequencies(word) = 0
                e.Frequencies(word) += 1
            Next
            _entries.Add(e)
            For Each word In e.Frequencies.Keys
                If Not _postings.ContainsKey(word) Then _postings(word) = New List(Of Entry)
                _postings(word).Add(e)
            Next
        Next
        _averageLength = If(_entries.Count = 0, 1.0, _entries.Average(Function(e) CDbl(e.Length)))
    End Sub

    Public Shared Function Normalize(value As String) As String
        Dim sb As New StringBuilder()
        For Each ch In If(value, "").Normalize(NormalizationForm.FormKC).ToLowerInvariant()
            Dim n = AscW(ch)
            If (n >= &H64B AndAlso n <= &H65F) OrElse n = &H670 OrElse n = &H640 Then Continue For
            If n >= &H660 AndAlso n <= &H669 Then ch = ChrW(AscW("0"c) + n - &H660)
            If n >= &H6F0 AndAlso n <= &H6F9 Then ch = ChrW(AscW("0"c) + n - &H6F0)
            Select Case ch
                Case "أ"c, "إ"c, "آ"c, "ٱ"c : ch = "ا"c
                Case "ى"c : ch = "ي"c
                Case "ة"c : ch = "ه"c
            End Select
            sb.Append(ch)
        Next
        Return Regex.Replace(sb.ToString(), "\s+", " ").Trim()
    End Function

    Public Shared Function TokenKey(value As String) As String
        Dim n = Normalize(value)
        Return If(n.Length > 4 AndAlso n.StartsWith("ال", StringComparison.Ordinal), n.Substring(2), n)
    End Function

    Public Shared Function Tokens(value As String) As IEnumerable(Of String)
        Return Regex.Matches(Normalize(value), "[\p{L}\p{Nd}]+(?:[-/][\p{L}\p{Nd}]+)*").Cast(Of Match)().Select(Function(m) m.Value).Where(Function(w) w.Length > 1 AndAlso Not Stops.Contains(w)).Select(Function(w) TokenKey(w))
    End Function

    Public Function Search(query As String, Optional category As String = "", Optional filter As String = "all", Optional limit As Integer = 100, Optional token As CancellationToken = Nothing) As List(Of BasiraHit)
        Dim rawTerms = Tokens(query).Distinct().ToList()
        Dim weights As New Dictionary(Of String, Double)(StringComparer.Ordinal)
        For Each term In rawTerms
            weights(term) = 1.0
            If Aliases.ContainsKey(term) Then
                For Each other In Aliases(term)
                    If Not weights.ContainsKey(other) Then weights(other) = 0.5
                Next
            End If
        Next
        Dim candidates As New HashSet(Of Entry)
        If String.IsNullOrWhiteSpace(query) Then
            candidates.UnionWith(_entries)
        Else
            For Each term In weights.Keys
                If _postings.ContainsKey(term) Then candidates.UnionWith(_postings(term))
            Next
            ' يدعم البحث بجزء من رقم المرجع دون تخمين لغوي.
            If Regex.IsMatch(Normalize(query), "\d") Then candidates.UnionWith(_entries.Where(Function(e) Normalize(e.Document.ArchiveNumber & " " & e.Document.ReferenceNumber).Contains(Normalize(query))))
        End If
        Dim result As New List(Of BasiraHit)
        For Each e In candidates
            token.ThrowIfCancellationRequested()
            Dim d = e.Document
            If Not String.IsNullOrWhiteSpace(category) AndAlso category <> "الكل" AndAlso d.GeneralCategory <> category Then Continue For
            If Not MatchesFilter(d, filter, DateTime.Today) Then Continue For
            Dim score As Double = 0
            Dim matched As New List(Of String)
            For Each kv In weights
                If Not e.Frequencies.ContainsKey(kv.Key) Then Continue For
                Dim df = _postings(kv.Key).Count
                Dim idf = Math.Log(1.0 + (_entries.Count - df + 0.5) / (df + 0.5))
                Dim tf = CDbl(e.Frequencies(kv.Key))
                score += kv.Value * idf * (tf * 2.2) / (tf + 1.2 * (0.25 + 0.75 * e.Length / _averageLength))
                If e.Title.Contains(kv.Key) Then score += kv.Value * 2.0
                matched.Add(kv.Key)
            Next
            If Not String.IsNullOrWhiteSpace(query) AndAlso Normalize(d.ArchiveNumber & " " & d.ReferenceNumber).Contains(Normalize(query)) Then score += 12
            Dim offset As Integer
            Dim excerpt = BuildExcerpt(d.TextContent, matched, offset)
            result.Add(New BasiraHit With {.Document = d, .Score = Math.Round(score, 3), .Excerpt = excerpt, .Offset = offset, .Matches = String.Join("، ", matched)})
        Next
        Return result.OrderByDescending(Function(h) h.Score).ThenByDescending(Function(h) h.Document.UpdatedAt).Take(Math.Max(1, limit)).ToList()
    End Function

    Public Shared Function BuildExcerpt(text As String, terms As IEnumerable(Of String), ByRef offset As Integer) As String
        Dim raw = If(text, "")
        offset = 0
        If raw.Length = 0 Then Return ""
        Dim matches = Regex.Matches(raw, "[\p{L}\p{Nd}]+(?:[-/][\p{L}\p{Nd}]+)*").Cast(Of Match)()
        Dim setTerms As New HashSet(Of String)(terms, StringComparer.Ordinal)
        Dim first = matches.FirstOrDefault(Function(m) setTerms.Contains(TokenKey(m.Value)))
        If first IsNot Nothing Then offset = Math.Max(0, first.Index - 90)
        Return raw.Substring(offset, Math.Min(650, raw.Length - offset))
    End Function

    Public Shared Function TryDue(value As String, ByRef due As DateTime) As Boolean
        Dim raw = Normalize(value)
        Dim m = Regex.Match(raw, "^(\d{1,4})[/-](\d{1,2})[/-](\d{1,4})(?:\s*(هـ|ه|هجري|م))?$")
        If Not m.Success Then Return False
        Dim a = Integer.Parse(m.Groups(1).Value, CultureInfo.InvariantCulture)
        Dim month = Integer.Parse(m.Groups(2).Value, CultureInfo.InvariantCulture)
        Dim b = Integer.Parse(m.Groups(3).Value, CultureInfo.InvariantCulture)
        Dim year = If(a >= 1300, a, b)
        Dim day = If(a >= 1300, b, a)
        If year < 1300 Then Return False ' لا يفسر سنة ذات رقمين أو وصفاً غامضاً.
        Try
            If year < 1700 OrElse m.Groups(4).Value.Contains("ه") Then
                due = New UmAlQuraCalendar().ToDateTime(year, month, day, 0, 0, 0, 0)
            Else
                due = New DateTime(year, month, day)
            End If
            Return True
        Catch ex As ArgumentOutOfRangeException
            Return False
        End Try
    End Function

    Public Shared Function IsClosed(d As DocumentRecord) As Boolean
        Return New String() {"مؤرشف", "مغلقة", "مغلق", "مكتمل", "منجز"}.Contains(If(d.Status, ""))
    End Function

    Public Shared Function IsSensitive(d As DocumentRecord) As Boolean
        Return Not New String() {"", "عادي", "عام", "غير سري"}.Contains(If(d.Confidentiality, "")) OrElse d.IsEncrypted
    End Function

    Public Shared Function MatchesFilter(d As DocumentRecord, filter As String, today As DateTime) As Boolean
        Dim due As DateTime
        Select Case filter
            Case "review" : Return If(d.Status, "").Contains("مراجعة") OrElse If(d.Status, "").Contains("اعتماد")
            Case "overdue" : Return Not IsClosed(d) AndAlso TryDue(d.DueDateText, due) AndAlso due.Date < today.Date
            Case "week" : Return Not IsClosed(d) AndAlso TryDue(d.DueDateText, due) AndAlso due.Date >= today.Date AndAlso due.Date <= today.AddDays(7).Date
            Case "ocr" : Return String.IsNullOrWhiteSpace(d.TextContent)
            Case "sensitive" : Return IsSensitive(d)
            Case Else : Return True
        End Select
    End Function

    Public Function Findings(Optional today As Nullable(Of DateTime) = Nothing) As List(Of BasiraFinding)
        Dim day = If(today.HasValue, today.Value.Date, DateTime.Today)
        Dim result As New List(Of BasiraFinding)
        For Each e In _entries
            Dim d = e.Document
            Dim due As DateTime
            If Not IsClosed(d) AndAlso TryDue(d.DueDateText, due) AndAlso due.Date <= day.AddDays(7) Then
                result.Add(New BasiraFinding With {.DocumentId = d.Id, .Title = d.Title, .Kind = "موعد", .Severity = If(due.Date < day, 0, 1), .Due = due, .Reason = If(due.Date < day, "تجاوز الموعد بـ " & (day - due.Date).Days.ToString() & " يوم", "موعد مستحق " & due.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture))})
            ElseIf Not String.IsNullOrWhiteSpace(d.DueDateText) AndAlso Not TryDue(d.DueDateText, due) Then
                result.Add(New BasiraFinding With {.DocumentId = d.Id, .Title = d.Title, .Kind = "موعد غير واضح", .Severity = 2, .Reason = "راجع صيغة الموعد: " & d.DueDateText})
            End If
            If String.IsNullOrWhiteSpace(d.TextContent) Then result.Add(New BasiraFinding With {.DocumentId = d.Id, .Title = d.Title, .Kind = "OCR", .Severity = 2, .Reason = "لا يوجد نص قابل للبحث"})
            If String.IsNullOrWhiteSpace(d.GeneralCategory) Then result.Add(New BasiraFinding With {.DocumentId = d.Id, .Title = d.Title, .Kind = "تصنيف", .Severity = 3, .Reason = "التصنيف العام غير مكتمل"})
        Next
        For Each group In _entries.Where(Function(e) Not String.IsNullOrWhiteSpace(e.Document.FileHashSha256)).GroupBy(Function(e) e.Document.FileHashSha256, StringComparer.OrdinalIgnoreCase).Where(Function(g) g.Count() > 1)
            For Each e In group
                result.Add(New BasiraFinding With {.DocumentId = e.Document.Id, .Title = e.Document.Title, .Kind = "نسخة مطابقة", .Severity = 2, .Reason = "بصمة SHA-256 مطابقة لـ " & (group.Count() - 1).ToString() & " مستند؛ راجعها قبل أي حذف"})
            Next
        Next
        Return result.OrderBy(Function(f) f.Severity).ThenBy(Function(f) If(f.Due.HasValue, f.Due.Value, DateTime.MaxValue)).ThenBy(Function(f) f.Title).ToList()
    End Function

    Public Function Links(documentId As String) As List(Of BasiraLink)
        Dim source = _entries.FirstOrDefault(Function(e) e.Document.Id = documentId)
        Dim result As New List(Of BasiraLink)
        If source Is Nothing Then Return result
        Dim d = source.Document
        For Each e In _entries.Where(Function(x) x.Document.Id <> documentId)
            Dim other = e.Document
            Dim reason As String = ""
            If Not String.IsNullOrWhiteSpace(d.FileHashSha256) AndAlso String.Equals(d.FileHashSha256, other.FileHashSha256, StringComparison.OrdinalIgnoreCase) Then
                reason = "بصمة ملف متطابقة"
            ElseIf Not String.IsNullOrWhiteSpace(d.ReferenceNumber) AndAlso Normalize(d.ReferenceNumber) = Normalize(other.ReferenceNumber) Then
                reason = "مرجع مشترك: " & d.ReferenceNumber
            ElseIf Not String.IsNullOrWhiteSpace(d.SourceEntity) AndAlso Normalize(d.SourceEntity) = Normalize(other.SourceEntity) Then
                reason = "جهة مشتركة: " & d.SourceEntity
            End If
            If reason <> "" Then result.Add(New BasiraLink With {.Document = other, .Reason = reason})
        Next
        Return result.Take(12).ToList()
    End Function

    Public Function LocalAnswer(question As String, Optional token As CancellationToken = Nothing) As BasiraAnswer
        Dim result As New BasiraAnswer With {.Question = question, .Mode = "أدلة محلية"}
        Dim sb As New StringBuilder()
        For Each hit In Search(question, limit:=8, token:=token)
            If String.IsNullOrWhiteSpace(hit.Excerpt) Then Continue For
            Dim e As New BasiraEvidence With {.Number = result.Evidence.Count + 1, .DocumentId = hit.Document.Id, .Title = hit.Document.Title, .Reference = hit.Document.ArchiveNumber, .Quote = hit.Excerpt, .Offset = hit.Offset, .Sensitive = IsSensitive(hit.Document)}
            result.Evidence.Add(e)
            sb.AppendLine("[" & e.Number.ToString() & "] " & e.Title & " — " & e.Reference)
            sb.AppendLine(e.Quote)
            sb.AppendLine()
        Next
        result.Text = If(result.Evidence.Count = 0, "لم أجد نصوصاً مناسبة لهذا السؤال. جرّب اسم الجهة أو رقم المعاملة، أو استخرج نص المستند أولاً.", "مقتطفات أصلية مرتبطة بسؤالك. افتح أي مصدر للتحقق من السياق الكامل." & Environment.NewLine & Environment.NewLine & sb.ToString())
        Return result
    End Function
End Class
