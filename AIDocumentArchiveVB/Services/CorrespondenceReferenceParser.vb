Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions

' محلل الإشارات في المراسلات الإدارية العربية: يستخرج من نص الخطاب ما يشير إليه من خطابات
' ومعاملات وقرارات سابقة («إشارة إلى خطابكم رقم … وتاريخ …»، «رداً على»، «إلحاقاً لـ»،
' «المقيد لدينا برقم»، «بناءً على قرار …»)، مع الرقم والتاريخ الهجري أو الميلادي والجهة.
' يعمل محلياً بقواعد صريحة دون أي خدمة خارجية، ويتحمل أخطاء OCR الشائعة.

Public Enum ReferenceKind
    Reference
    Reply
    FollowUp
    Registration
    LegalBasis
End Enum

Public Enum ReferenceOwner
    Unknown
    Theirs
    Ours
    ThirdParty
End Enum

Public Class CorrespondenceReference
    Public Property Kind As ReferenceKind
    Public Property Owner As ReferenceOwner
    Public Property DocumentType As String = ""
    Public Property Issuer As String = ""
    Public Property Number As String = ""
    Public Property RawNumber As String = ""
    Public Property DateText As String = ""
    Public Property CanonicalDate As String = ""
    Public Property [Date] As Nullable(Of DateTime)
    Public Property Calendar As String = ""
    Public Property Offset As Integer
    Public Property Length As Integer
    Public Property Evidence As String = ""
    Public Property Confidence As Integer

    Public ReadOnly Property KindCode As String
        Get
            Select Case Kind
                Case ReferenceKind.Reply : Return "reply"
                Case ReferenceKind.FollowUp : Return "followup"
                Case ReferenceKind.Registration : Return "registration"
                Case ReferenceKind.LegalBasis : Return "legal"
                Case Else : Return "reference"
            End Select
        End Get
    End Property

    Public ReadOnly Property KindLabel As String
        Get
            Select Case Kind
                Case ReferenceKind.Reply : Return "رد على"
                Case ReferenceKind.FollowUp : Return "إلحاق وتعقيب"
                Case ReferenceKind.Registration : Return "قيد وارد لدينا"
                Case ReferenceKind.LegalBasis : Return "سند نظامي"
                Case Else : Return "إشارة إلى"
            End Select
        End Get
    End Property

    Public ReadOnly Property OwnerCode As String
        Get
            Select Case Owner
                Case ReferenceOwner.Theirs : Return "theirs"
                Case ReferenceOwner.Ours : Return "ours"
                Case ReferenceOwner.ThirdParty : Return "third"
                Case Else : Return "unknown"
            End Select
        End Get
    End Property

    Public ReadOnly Property OwnerLabel As String
        Get
            Select Case Owner
                Case ReferenceOwner.Theirs : Return "الجهة المخاطَبة"
                Case ReferenceOwner.Ours : Return "جهتنا"
                Case ReferenceOwner.ThirdParty : Return If(Issuer = "", "جهة أخرى", Issuer)
                Case Else : Return "غير محددة"
            End Select
        End Get
    End Property

    ' الصيغة المختصرة المستخدمة في ملفات التحقق: النوع|الجهة|الرقم|التاريخ
    Public Function ToExpectedLine() As String
        Return KindCode & "|" & OwnerCode & "|" & Number & "|" & CanonicalDate
    End Function
End Class

Public NotInheritable Class CorrespondenceReferenceParser
    Private Sub New()
    End Sub

    ' أنواع المستندات بعد التطبيع (ة→ه، وتصير ت قبل الضمير: برقيتكم، مذكرتكم، رسالتكم).
    Private Const Docs As String = "خطاب|كتاب|برقي[هت]|مذكر[هت]|رسال[هت]|طلب|معامل[هت]|مكاتب[هت]|فاكس|بريد|استفسار|شكوي|شكوا|مطالب[هت]|افاد[هت]|تقرير|محضر|قرار|امر|تعميم|نظام|لائح[هت]|مرسوم|توجيه|موافق[هت]"
    Private Const DocCore As String = "(?<pre>لل|[لب]?(?:ال)?)(?<doc>" & Docs & ")(?<own>كم|نا)?(?!\p{L})"
    Private Const DocPlain As String = "(?:لل|[لب]?(?:ال)?)(?:" & Docs & ")(?:كم|نا)?(?!\p{L})"

    Private Shared ReadOnly LegalDocs As New HashSet(Of String)(New String() {"قرار", "امر", "تعميم", "نظام", "لائحه", "لائحت", "مرسوم", "توجيه", "موافقه", "موافقت"}, StringComparer.Ordinal)

    Private Shared ReadOnly TriggerRx As New Regex(
        "(?<!\p{L})[وف]?(?:" &
        "(?<ref>(?:ب?ال)?اشاره\s*(?:الي\s+|ل(?=\p{L}))|نشير\s+(?:الي\s+|ل(?=\p{L}))|بالاطلاع\s+علي\s+)|" &
        "(?<reply>(?:ردا|جوابا)\s+(?:علي\s+|ل(?=\p{L})))|" &
        "(?<follow>الحاقا\s*[لب](?=\p{L})|الحاقا\s+(?:الي|علي)\s+|(?:تعقيبا|عطفا|بالعطف|المعطوف|تاكيدا|استكمالا)\s+(?:علي\s+|ل(?=\p{L})))|" &
        "(?<legal>بناء\s+علي\s+|استنادا\s+(?:الي\s+|ل(?=\p{L}))|بموجب\s+|وفقا\s+(?:ل(?=\p{L})|لما\s+))|" &
        "(?<reg>(?:المقيد[هت]?|المسجل[هت]?|الوارد[هت]?)\s+(?:الينا|لدينا|لدي)\s+(?:(?!برقم)\S+\s+)?(?=برقم)|(?:المقيد[هت]?|المسجل[هت]?)\s+(?=برقم))" &
        ")", RegexOptions.Compiled)

    ' مسار احتياطي بلا كلمة تمهيد: «خطابكم رقم» و«خطابنا رقم» و«خطاب سعادتكم رقم» إشارة في ذاتها أياً كان ما يسبقها.
    Private Shared ReadOnly OwnedDocRx As New Regex("(?<!\p{L})(?:لل|[لب]?(?:ال)?)(?:" & Docs & ")(?:(?:كم|نا)(?!\p{L})|\s+(?:سعادت|معالي|سمو|فضيلت|سيادت)كم(?!\p{L}))", RegexOptions.Compiled)

    Private Shared ReadOnly StopRx As New Regex("\.(?=\s|$)|[؟!؛]|\n[ \t]*\n", RegexOptions.Compiled)

    Private Shared ReadOnly ContinuationRx As New Regex("(?<=[\s،,])و\s?(?=" & DocPlain & ")", RegexOptions.Compiled)

    ' حتى أربع كلمات تمهيدية قبل نوع المستند («إشارة إلى ما ورد بخطاب …») دون تجاوز كلمة «رقم».
    Private Shared ReadOnly FirstDocRx As New Regex("^\s*(?:(?!ب?رقم|ذي\s|ذو\s|المرقم)\S+\s+){0,4}?" & DocCore, RegexOptions.Compiled)
    Private Shared ReadOnly ContinuationDocRx As New Regex("^\s*و\s?" & DocCore, RegexOptions.Compiled)

    Private Shared ReadOnly NumberRx As New Regex(
        "(?<!\p{L})(?:(?:(?:ذي|ذو|ذات)\s+)?(?:ال)?رقم(?:ه)?|برقم|المرقم)\s*[:：#(\[]?\s*" &
        "(?<num>(?:[0-9](?: [0-9]){2,}(?![0-9])|[A-Za-z0-9]+|\p{L}{1,3}(?!\p{L}))(?:\s*[/\\\-]\s*(?:[A-Za-z0-9]+|\p{L}{1,3}(?!\p{L})))*)", RegexOptions.Compiled)

    Private Shared ReadOnly Months As New Dictionary(Of String, Tuple(Of String, Integer))(StringComparer.Ordinal)
    Friend Shared ReadOnly DateBody As String
    Private Shared ReadOnly DateRx As Regex
    Private Shared ReadOnly BareDateRx As Regex

    Private Shared ReadOnly IssuerStops As New HashSet(Of String)(New String() {
        "ذي", "ذو", "ذات", "رقم", "رقمه", "برقم", "المرقم", "المؤرخ", "المؤرخه", "المؤرخت", "وتاريخ", "وتاريخه", "بتاريخ", "والتاريخ", "التاريخ", "في",
        "الصادر", "الصادره", "الوارد", "الوارده", "المرسل", "المرسله", "المتضمن", "المتضمنه", "بشان", "بخصوص", "حول", "المشار", "المرفق", "المرفقه",
        "الي", "علي", "عن", "من", "و"}, StringComparer.Ordinal)

    Private Shared ReadOnly FromIssuerRx As New Regex("(?<!\p{L})(?:الوارد|الصادر|المرسل|المقدم)[هت]?\s+(?:الينا\s+|لنا\s+)?(?:من|عن)\s*(?<who>كم|نا)?(?!\p{L})", RegexOptions.Compiled)

    Private Shared ReadOnly HonorificRx As New Regex("^(?:سعادت|معالي|سمو|فضيلت|سيادت)(?:كم|ه)$", RegexOptions.Compiled)

    Shared Sub New()
        Dim hijri As String() = {"محرم", "صفر", "ربيع الاول", "ربيع الاخر|ربيع الثاني", "جمادي الاولي|جمادي الاول", "جمادي الاخره|جمادي الاخر|جمادي الثانيه|جمادي الثاني",
                                 "رجب", "شعبان", "رمضان", "شوال", "ذو القعده|ذي القعده", "ذو الحجه|ذي الحجه"}
        Dim gregorian As String() = {"يناير", "فبراير", "مارس", "ابريل", "مايو", "يونيو", "يوليو", "اغسطس", "سبتمبر", "اكتوبر", "نوفمبر", "ديسمبر"}
        For i = 0 To 11
            For Each name In hijri(i).Split("|"c)
                Months(name) = Tuple.Create("H", i + 1)
            Next
            Months(gregorian(i)) = Tuple.Create("G", i + 1)
        Next
        Dim names = String.Join("|", Months.Keys.OrderByDescending(Function(k) k.Length).Select(Function(k) k.Replace(" ", "\s+")))
        DateBody = "(?:(?<a>[0-9]{1,4})\s*[/\-.]\s*(?<b>[0-9]{1,2})\s*[/\-.]\s*(?<c>[0-9]{1,4})|(?<d>[0-9]{1,2})\s+(?<mon>" & names & ")\s*(?:سنه\s+|عام\s+)?(?<y>[0-9]{3,4}))" &
                   "(?:\s*(?<suf>هجريه|هجري|ه|ميلاديه|ميلادي|م)(?!\p{L}))?"
        DateRx = New Regex("(?<!\p{L})(?:وتاريخه|وتاريخها|وتاريخ|بتاريخ|والتاريخ|التاريخ|تاريخ|و?المؤرخ[هت]?(?:\s+في)?|في)\s*[:：(\[]?\s*" & DateBody, RegexOptions.Compiled)
        BareDateRx = New Regex("^\s*" & DateBody & "\s*$", RegexOptions.Compiled)
    End Sub

    ' تطبيع يحافظ على خريطة المواضع إلى النص الأصلي: حذف التشكيل والتطويل، توحيد الألف والياء والتاء المربوطة، وتحويل الأرقام الهندية.
    Friend Shared Function NormalizeWithMap(text As String, map As List(Of Integer)) As String
        Dim sb As New StringBuilder(text.Length)
        For i = 0 To text.Length - 1
            Dim ch As Char = text(i)
            Dim n As Integer = AscW(ch)
            If (n >= &H64B AndAlso n <= &H65F) OrElse n = &H670 OrElse n = &H640 Then Continue For
            If n >= &H660 AndAlso n <= &H669 Then
                ch = ChrW(48 + n - &H660)
            ElseIf n >= &H6F0 AndAlso n <= &H6F9 Then
                ch = ChrW(48 + n - &H6F0)
            Else
                Select Case ch
                    Case "أ"c, "إ"c, "آ"c, "ٱ"c : ch = "ا"c
                    Case "ى"c : ch = "ي"c
                    Case "ة"c : ch = "ه"c
                    Case ChrW(13) : ch = " "c
                End Select
            End If
            sb.Append(ch)
            map.Add(i)
        Next
        Return sb.ToString()
    End Function

    ' نهاية المقطع في النص الأصلي، ممتدة فوق التطويل والتشكيل الذي حذفه التطبيع («هـ» لا «ه»).
    Private Shared Function OriginalEnd(text As String, map As List(Of Integer), normIndexInclusive As Integer) As Integer
        Dim e As Integer = map(normIndexInclusive) + 1
        While e < text.Length
            Dim n As Integer = AscW(text(e))
            If Not ((n >= &H64B AndAlso n <= &H65F) OrElse n = &H670 OrElse n = &H640) Then Exit While
            e += 1
        End While
        Return e
    End Function

    Public Shared Function Parse(text As String) As List(Of CorrespondenceReference)
        Dim result As New List(Of CorrespondenceReference)
        If String.IsNullOrWhiteSpace(text) Then Return result
        Dim map As New List(Of Integer)(text.Length)
        Dim norm As String = NormalizeWithMap(text, map)
        Dim triggers As List(Of Match) = TriggerRx.Matches(norm).Cast(Of Match)().ToList()
        Dim stops As List(Of Integer) = StopRx.Matches(norm).Cast(Of Match)().Select(Function(m) m.Index).ToList()
        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)
        Dim covered As New List(Of Tuple(Of Integer, Integer))

        ' المرحلة الأولى: الإشارات المسبوقة بعبارة تمهيد صريحة.
        For t = 0 To triggers.Count - 1
            Dim trig As Match = triggers(t)
            Dim nextAnchor As Integer = If(t + 1 < triggers.Count, triggers(t + 1).Index, norm.Length)
            Dim segStart As Integer = trig.Index + trig.Length
            Dim segEnd As Integer = SegmentEnd(segStart, nextAnchor, norm.Length, stops)
            covered.Add(Tuple.Create(trig.Index, segEnd))
            ParseSegment(text, norm, map, trig.Index, segStart, segEnd, KindOf(trig), trig.Groups("legal").Success, seen, result)
        Next

        ' المرحلة الثانية: مستند مملوك («خطابكم رقم …») خارج ما غطته المرحلة الأولى.
        For Each dm As Match In OwnedDocRx.Matches(norm)
            Dim pos As Integer = dm.Index
            If covered.Any(Function(c) pos >= c.Item1 AndAlso pos < c.Item2) Then Continue For
            Dim nextTrigger As Integer = norm.Length
            For Each tr In triggers
                If tr.Index > pos Then
                    nextTrigger = tr.Index
                    Exit For
                End If
            Next
            Dim segEnd As Integer = SegmentEnd(pos, nextTrigger, norm.Length, stops)
            covered.Add(Tuple.Create(pos, segEnd))
            ParseSegment(text, norm, map, pos, pos, segEnd, ReferenceKind.Reference, False, seen, result)
        Next
        Return result.OrderBy(Function(x) x.Offset).ToList()
    End Function

    Private Shared Function SegmentEnd(segStart As Integer, nextAnchor As Integer, length As Integer, stops As List(Of Integer)) As Integer
        Dim segEnd As Integer = Math.Min(Math.Min(length, segStart + 320), nextAnchor)
        For Each s In stops
            If s >= segStart Then
                segEnd = Math.Min(segEnd, s)
                Exit For
            End If
        Next
        Return segEnd
    End Function

    Private Shared Sub ParseSegment(text As String, norm As String, map As List(Of Integer), anchor As Integer, segStart As Integer, segEnd As Integer,
                                    kind As ReferenceKind, legalTrigger As Boolean, seen As HashSet(Of String), result As List(Of CorrespondenceReference))
        If segEnd <= segStart Then Return
        Dim segment As String = norm.Substring(segStart, segEnd - segStart)
        Dim cuts As New List(Of Integer) From {0}
        If kind <> ReferenceKind.Registration Then
            For Each c As Match In ContinuationRx.Matches(segment)
                If c.Index > 0 Then cuts.Add(c.Index)
            Next
        End If
        For i = 0 To cuts.Count - 1
            Dim itemStart As Integer = cuts(i)
            Dim itemEnd As Integer = If(i + 1 < cuts.Count, cuts(i + 1), segment.Length)
            Dim endInItem As Integer
            Dim r = ParseItem(text, norm, map, segStart + itemStart, segment.Substring(itemStart, itemEnd - itemStart), kind, legalTrigger, i = 0, endInItem)
            If r Is Nothing Then Continue For
            If Not seen.Add(r.KindCode & "|" & r.Number) Then Continue For
            Dim normFrom As Integer = If(i = 0, anchor, segStart + itemStart)
            Dim normTo As Integer = segStart + itemStart + endInItem
            ' تجاوز المسافات وواو العطف في بداية الدليل.
            While normFrom < normTo AndAlso (Char.IsWhiteSpace(norm(normFrom)) OrElse (i > 0 AndAlso normFrom = segStart + itemStart AndAlso norm(normFrom) = "و"c))
                normFrom += 1
            End While
            r.Offset = map(normFrom)
            r.Length = OriginalEnd(text, map, normTo - 1) - r.Offset
            r.Evidence = Regex.Replace(text.Substring(r.Offset, r.Length), "\s+", " ").Trim()
            result.Add(r)
        Next
    End Sub

    Private Shared Function KindOf(m As Match) As ReferenceKind
        If m.Groups("reply").Success Then Return ReferenceKind.Reply
        If m.Groups("follow").Success Then Return ReferenceKind.FollowUp
        If m.Groups("legal").Success Then Return ReferenceKind.LegalBasis
        If m.Groups("reg").Success Then Return ReferenceKind.Registration
        Return ReferenceKind.Reference
    End Function

    Private Shared Function ParseItem(original As String, norm As String, map As List(Of Integer), itemAbs As Integer, item As String,
                                      kind As ReferenceKind, legalTrigger As Boolean, first As Boolean, ByRef endInItem As Integer) As CorrespondenceReference
        Dim r As New CorrespondenceReference With {.Kind = kind, .Confidence = 50}
        Dim afterDoc As Integer = 0

        If kind = ReferenceKind.Registration Then
            r.Owner = ReferenceOwner.Ours
            r.Confidence += 15
        Else
            Dim dm As Match = If(first, FirstDocRx, ContinuationDocRx).Match(item)
            If Not dm.Success Then Return Nothing
            Dim doc As String = dm.Groups("doc").Value
            r.DocumentType = doc
            r.Confidence += 15
            If legalTrigger Then r.Kind = If(LegalDocs.Contains(doc), ReferenceKind.LegalBasis, ReferenceKind.Reference)
            afterDoc = dm.Index + dm.Length
            Dim own As String = dm.Groups("own").Value
            If own = "كم" Then
                r.Owner = ReferenceOwner.Theirs
            ElseIf own = "نا" Then
                r.Owner = ReferenceOwner.Ours
            Else
                DetectIssuer(original, map, itemAbs, item, afterDoc, r)
            End If
        End If

        Dim number As Match = Nothing
        For Each m As Match In NumberRx.Matches(item, afterDoc)
            If m.Groups("num").Value.Any(Function(c) Char.IsDigit(c)) Then
                number = m
                Exit For
            End If
        Next
        If number Is Nothing Then Return Nothing
        Dim numGroup As Group = number.Groups("num")
        r.RawNumber = original.Substring(map(itemAbs + numGroup.Index), OriginalEnd(original, map, itemAbs + numGroup.Index + numGroup.Length - 1) - map(itemAbs + numGroup.Index))
        r.Number = NumberKey(numGroup.Value)
        endInItem = numGroup.Index + numGroup.Length

        Dim dateMatch As Match = DateRx.Match(item, afterDoc)
        If dateMatch.Success Then
            Dim body As Group = If(dateMatch.Groups("mon").Success, dateMatch.Groups("d"), dateMatch.Groups("a"))
            Dim dateStart As Integer = body.Index
            Dim dateEnd As Integer = dateMatch.Index + dateMatch.Length
            r.DateText = original.Substring(map(itemAbs + dateStart), OriginalEnd(original, map, itemAbs + dateEnd - 1) - map(itemAbs + dateStart)).Trim()
            Dim canonical As String = ""
            Dim value As DateTime
            Dim calendar As String = ""
            If Resolve(dateMatch, canonical, value, calendar) Then
                r.CanonicalDate = canonical
                r.Date = value
                r.Calendar = calendar
                r.Confidence += 30
            End If
            endInItem = Math.Max(endInItem, dateEnd)
        End If
        If r.Owner = ReferenceOwner.Unknown Then
            Dim fm As Match = FromIssuerRx.Match(item, afterDoc)
            If fm.Success Then
                If fm.Groups("who").Value = "كم" Then
                    r.Owner = ReferenceOwner.Theirs
                ElseIf fm.Groups("who").Value = "نا" Then
                    r.Owner = ReferenceOwner.Ours
                Else
                    DetectIssuer(original, map, itemAbs, item, fm.Index + fm.Length, r)
                End If
            End If
        End If
        If r.Owner <> ReferenceOwner.Unknown Then r.Confidence += 5
        r.Confidence = Math.Min(100, r.Confidence)
        Return r
    End Function

    ' الكلمات بين نوع المستند والرقم: لقب مخاطَب (سعادتكم) أو اسم جهة ثالثة (وزارة المالية).
    Private Shared Sub DetectIssuer(original As String, map As List(Of Integer), itemAbs As Integer, item As String, from As Integer, r As CorrespondenceReference)
        Dim words As New List(Of Match)
        For Each w As Match In Regex.Matches(item.Substring(from), "\S+")
            Dim token As String = w.Value.Trim("،"c, ","c, "("c, ")"c, ":"c)
            If token = "" OrElse token.Any(Function(c) Char.IsDigit(c)) OrElse IssuerStops.Contains(token) OrElse token.StartsWith("رقم", StringComparison.Ordinal) OrElse token.StartsWith("وتاريخ", StringComparison.Ordinal) Then Exit For
            words.Add(w)
            If words.Count = 6 OrElse w.Value.EndsWith("،", StringComparison.Ordinal) Then Exit For
        Next
        If words.Count = 0 Then
            r.Owner = ReferenceOwner.Unknown
        ElseIf words.Any(Function(w) HonorificRx.IsMatch(w.Value)) Then
            r.Owner = ReferenceOwner.Theirs
        Else
            r.Owner = ReferenceOwner.ThirdParty
            Dim s As Integer = map(itemAbs + from + words(0).Index)
            Dim lastW As Match = words(words.Count - 1)
            Dim e As Integer = map(itemAbs + from + lastW.Index + lastW.Length - 1) + 1
            r.Issuer = original.Substring(s, e - s).Trim().TrimEnd("،"c, ","c)
        End If
    End Sub

    Friend Shared Function Resolve(m As Match, ByRef canonical As String, ByRef value As DateTime, ByRef calendar As String) As Boolean
        Dim y, mo, d As Integer
        Dim cal As String
        Dim suf As String = m.Groups("suf").Value
        If m.Groups("mon").Success Then
            Dim info = Months(Regex.Replace(m.Groups("mon").Value, "\s+", " "))
            cal = info.Item1
            mo = info.Item2
            d = Integer.Parse(m.Groups("d").Value, CultureInfo.InvariantCulture)
            y = Integer.Parse(m.Groups("y").Value, CultureInfo.InvariantCulture)
        Else
            Dim a As Integer = Integer.Parse(m.Groups("a").Value, CultureInfo.InvariantCulture)
            Dim b As Integer = Integer.Parse(m.Groups("b").Value, CultureInfo.InvariantCulture)
            Dim c As Integer = Integer.Parse(m.Groups("c").Value, CultureInfo.InvariantCulture)
            If a >= 100 Then
                y = a : mo = b : d = c
            ElseIf c >= 100 Then
                d = a : mo = b : y = c
            Else
                Return False ' سنة من رقمين غامضة.
            End If
            If suf.StartsWith("ه", StringComparison.Ordinal) Then
                cal = "H"
            ElseIf suf.StartsWith("م", StringComparison.Ordinal) Then
                cal = "G"
            ElseIf y >= 1300 AndAlso y <= 1600 Then
                cal = "H"
            ElseIf y >= 1800 AndAlso y <= 2200 Then
                cal = "G"
            Else
                Return False
            End If
        End If
        Try
            value = If(cal = "H", New UmAlQuraCalendar().ToDateTime(y, mo, d, 0, 0, 0, 0), New DateTime(y, mo, d))
        Catch ex As ArgumentOutOfRangeException
            Return False
        End Try
        canonical = cal & y.ToString("0000", CultureInfo.InvariantCulture) & "-" & mo.ToString("00", CultureInfo.InvariantCulture) & "-" & d.ToString("00", CultureInfo.InvariantCulture)
        calendar = If(cal = "H", "هجري", "ميلادي")
        Return True
    End Function

    ' يفسر تاريخاً منفرداً («1447/03/12هـ»، «12 ربيع الأول 1447»، «2025-09-14م»).
    Public Shared Function TryParseDate(text As String, ByRef canonical As String, ByRef value As DateTime) As Boolean
        Dim map As New List(Of Integer)
        Dim m As Match = BareDateRx.Match(NormalizeWithMap(If(text, ""), map))
        Dim calendar As String = ""
        Return m.Success AndAlso Resolve(m, canonical, value, calendar)
    End Function

    ' صيغة موحدة لرقم المعاملة: أرقام لاتينية، فاصل «/»، دون أصفار بادئة، وحروف لاتينية كبيرة.
    Public Shared Function NumberKey(raw As String) As String
        Dim map As New List(Of Integer)
        Dim n As String = NormalizeWithMap(If(raw, ""), map)
        Dim parts As New List(Of String)
        For Each p In Regex.Split(n, "\s*[/\\\-.]\s*")
            Dim s As String = p.Trim().Trim("("c, ")"c, "["c, "]"c, "#"c, ":"c).Trim()
            If s = "" Then Continue For
            If s.All(Function(c) (c >= "0"c AndAlso c <= "9"c) OrElse c = " "c) Then
                s = s.Replace(" ", "")
                s = s.TrimStart("0"c)
                If s = "" Then s = "0"
            Else
                s = s.ToUpperInvariant()
            End If
            parts.Add(s)
        Next
        Return String.Join("/", parts)
    End Function

    ' مطابقة متسامحة لأرقام المعاملات (اختلاف الفواصل أو الأصفار البادئة أو الأرقام الهندية).
    Public Shared Function NumbersMatch(a As String, b As String) As Boolean
        Dim ka As String = NumberKey(a)
        Dim kb As String = NumberKey(b)
        If ka = "" OrElse kb = "" Then Return False
        If ka = kb Then Return True
        Dim da As String = New String(ka.Where(Function(c) Char.IsLetterOrDigit(c)).ToArray())
        Dim db As String = New String(kb.Where(Function(c) Char.IsLetterOrDigit(c)).ToArray())
        Return da.Length >= 4 AndAlso da = db
    End Function
End Class
