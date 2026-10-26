Imports System
Imports System.Collections.Generic
Imports System.Collections.Concurrent
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports System.Xml.Linq

Module CoreTests
    Private count As Integer
    Private Sub Check(value As Boolean, name As String)
        If Not value Then Throw New Exception("FAIL: " & name)
        count += 1
        Console.WriteLine("PASS " & name)
    End Sub
    Private Sub WaitUntil(predicate As Func(Of Boolean), name As String)
        Dim watch = Stopwatch.StartNew()
        While Not predicate()
            If watch.ElapsedMilliseconds > 5000 Then Throw New Exception("TIMEOUT: " & name)
            Thread.Sleep(15)
        End While
    End Sub
    Sub Main(args As String())
        If args IsNot Nothing AndAlso args.Contains("--references") Then
            Dim idx = Array.IndexOf(args, "--references")
            Dim dir = If(idx + 1 < args.Length, args(idx + 1), Path.Combine(AppContext.BaseDirectory, "Corpus", "references"))
            EvaluateReferences(dir, True)
            Return
        End If
        If args IsNot Nothing AndAlso args.Contains("--bench") Then
            Benchmark()
            Return
        End If
        Dim folder = Path.Combine(Path.GetTempPath(), "basira-tests-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(folder)
        Try
            Check(BasiraEngine.Normalize("إِدَارَة الـمُخَاطَبات ١٢٣") = "اداره المخاطبات 123", "Arabic letters, diacritics, tatweel and digits")
            Dim d1 As New DocumentRecord With {.Id = "A", .Title = "عقد الصيانة", .TextContent = "بداية المستند " & New String("س"c, 850) & " تسديد الفواتير الخاصة بعقد الصيانة رقم 12345", .ArchiveNumber = "ARC-2026-001", .ReferenceNumber = "12345", .SourceEntity = "الإدارة المالية", .GeneralCategory = "عقود", .DueDateText = "2026/09/30", .FileHashSha256 = "hash1", .Status = "قيد المراجعة"}
            Dim d2 As New DocumentRecord With {.Id = "B", .Title = "تعميم عام", .TextContent = "خطاب تعميم للموظفين بشأن إجازة سنوية", .GeneralCategory = "إداري", .SourceEntity = "الادارة المالية", .DueDateText = "2026/10/05", .FileHashSha256 = "hash1"}
            Dim d3 As New DocumentRecord With {.Id = "C", .Title = "مستند سري", .TextContent = "عقد آخر سري للمورد", .GeneralCategory = "عقود", .Confidentiality = "سري", .DueDateText = "2026/09/01", .Status = "مؤرشف"}
            Dim d4 As New DocumentRecord With {.Id = "D", .Title = "بدون نص", .TextContent = "", .DueDateText = "03/10/26"}
            Dim engine As New BasiraEngine(New DocumentRecord() {d1, d2, d3, d4})
            Check(engine.Search("الصِّيَانَة").First().Document.Id = "A", "Arabic normalized ranking")
            Check(engine.Search("فاتورة").Any(Function(h) h.Document.Id = "A"), "Bounded synonym matching")
            Dim hit = engine.Search("الفواتير").First()
            Check(hit.Offset > 0 AndAlso d1.TextContent.Substring(hit.Offset, hit.Excerpt.Length) = hit.Excerpt, "Evidence offset maps to original text")
            Check(engine.Search("123").First().Document.Id = "A", "Partial reference matching")
            Check(engine.Search("", "عقود").Count = 2, "Category filter")
            Check(engine.Search("", filter:="ocr").Count = 1, "Missing text filter")
            Check(engine.Search("اختراع غيرموجود").Count = 0, "No irrelevant fallback search hits")
            Dim due As DateTime
            Check(BasiraEngine.TryDue("2026/10/03", due) AndAlso due = New DateTime(2026, 10, 3), "ISO date")
            Check(BasiraEngine.TryDue("٣/١٠/٢٠٢٦", due) AndAlso due = New DateTime(2026, 10, 3), "Arabic day-first date")
            Check(BasiraEngine.TryDue("1448/04/22هـ", due) AndAlso due = New UmAlQuraCalendar().ToDateTime(1448, 4, 22, 0, 0, 0, 0), "Explicit Hijri calendar")
            Check(Not BasiraEngine.TryDue("31/02/2026", due) AndAlso Not BasiraEngine.TryDue("03/10/26", due), "Reject invalid and ambiguous dates")
            Dim findings = engine.Findings(New DateTime(2026, 10, 3))
            Check(findings.Where(Function(f) f.Severity = 0).Count() = 1, "Closed records do not trigger overdue alerts")
            Check(findings.Where(Function(f) f.Kind = "نسخة مطابقة").Count() = 2, "Exact duplicate fingerprints")
            Check(findings.Any(Function(f) f.Kind = "موعد غير واضح" AndAlso f.DocumentId = "D"), "Malformed deadline remains visible")
            Check(engine.Links("A").Any(Function(l) l.Document.Id = "B"), "Shared reference or entity links")
            Dim answer = engine.LocalAnswer("صيانة")
            Check(answer.Evidence.Count = 1 AndAlso answer.Text.Contains(d1.TextContent.Substring(answer.Evidence(0).Offset, answer.Evidence(0).Quote.Length)), "Local answer uses source excerpts")
            Check(BasiraCitations.Valid("معلومة [1]", New Integer() {1}), "Accepted known source citation")
            Check(Not BasiraCitations.Valid("معلومة [2]", New Integer() {1}) AndAlso Not BasiraCitations.Valid("بدون إحالة", New Integer() {1}) AndAlso Not BasiraCitations.Valid("[999999999999999]", New Integer() {1}), "Reject missing or invented citations")
            Check(BasiraEngine.IsSensitive(d3) AndAlso Not BasiraEngine.IsSensitive(d1), "Sensitive source classification")
            Dim store As New BasiraStore(folder)
            Dim collection As New BasiraCollection With {.Name = "العقود", .Query = "صيانة", .Category = "عقود", .Filter = "all"}
            store.SaveCollection(collection)
            Check(New BasiraStore(folder).Collections().Single().Query = "صيانة", "Collection persists across reopen")
            collection.Query = "المورد"
            store.SaveCollection(collection)
            Check(store.Collections().Count = 1 AndAlso store.Collections()(0).Query = "المورد", "Collection identity survives replacement")
            Dim previous As New XElement("Document", New XAttribute("Id", "A"), New XElement("Title", "قديم"), New XElement("TextContent", "النص السابق"))
            store.CapturePrevious("A", previous)
            store.CapturePrevious("A", previous)
            Check(store.Revisions("A").Count = 1, "Deduplicate identical revision snapshots")
            For i = 1 To 30
                previous.SetElementValue("TextContent", "نص " & i.ToString())
                store.CapturePrevious("A", previous)
            Next
            Check(store.Revisions("A").Count = 25 AndAlso CStr(store.Revisions("A")(0).Element("Document").Element("TextContent")) = "نص 30", "Bounded revision history with latest content")
            Dim originalCulture = CultureInfo.CurrentCulture
            Try
                CultureInfo.CurrentCulture = New CultureInfo("ar-SA")
                Dim stamp = New DateTime(2026, 10, 3, 8, 15, 12, DateTimeKind.Utc)
                Check(BasiraDates.ParseStored(stamp.ToString("o", CultureInfo.InvariantCulture), DateTime.MinValue) = stamp, "Stored ISO date round-trip under Arabic Hijri locale")
                Check(BasiraDates.ParseStored("invalid-date", DateTime.MinValue) = DateTime.MinValue, "Invalid stored date keeps explicit fallback")
            Finally
                CultureInfo.CurrentCulture = originalCulture
            End Try
            Dim salt = Convert.ToBase64String(Enumerable.Range(1, 16).Select(Function(i) CByte(i)).ToArray())
            Dim hash = PasswordHashService.Hash("secret-password", salt)
            Check(PasswordHashService.Verify("secret-password", salt, hash) AndAlso Not PasswordHashService.Verify("wrong", salt, hash), "PBKDF2 password verification")
            Dim legacy As String
            Using sha = SHA256.Create()
                legacy = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes("old-password|" & salt)))
            End Using
            Check(PasswordHashService.Verify("old-password", salt, legacy), "Legacy password migration compatibility")
            Dim docx = Path.Combine(folder, "answer.docx")
            ReplyExportService.ExportReplyAsDocx(d1, "السؤال < & >" & vbCrLf & "دليل [1]", docx)
            Using zip = ZipFile.OpenRead(docx)
                Using reader As New StreamReader(zip.GetEntry("word/document.xml").Open())
                    Dim xml = XDocument.Parse(reader.ReadToEnd())
                    Check(xml.ToString().Contains("دليل [1]") AndAlso zip.GetEntry("[Content_Types].xml") IsNot Nothing, "DOCX export XML and citations")
                End Using
            End Using
            Dim sourceFile = Path.Combine(folder, "sample.txt")
            File.WriteAllText(sourceFile, d1.TextContent)
            d1.StoredPath = sourceFile
            Dim bundle = Path.Combine(folder, "case.zip")
            BasiraCaseExportService.Export(answer, New List(Of DocumentRecord) From {d1}, bundle)
            Using zip = ZipFile.OpenRead(bundle)
                Check(zip.GetEntry("answer.docx") IsNot Nothing AndAlso zip.GetEntry("Sources/001-sample.txt") IsNot Nothing, "Evidence ZIP includes reply and original file")
                Using reader As New StreamReader(zip.GetEntry("evidence.xml").Open())
                    Dim manifest = XDocument.Parse(reader.ReadToEnd())
                    Check(manifest.Root.Element("Source").Element("ExportedSHA256").Value.Length = 64, "Evidence package records copied file fingerprint")
                End Using
            End Using
            TestQueue(folder)
            TestHealthReport()
            TestPasswordPolicy()
            TestAuditChain()
            TestServerAuth(folder)
            TestReferenceParser()
            TestCorrespondenceIndex()
            TestDeadlines()
            TestProcesses()
            TestJobNotifications()
            Console.WriteLine("TOTAL " & count.ToString() & " checks passed")
        Finally
            Directory.Delete(folder, True)
        End Try
    End Sub
    Private Sub TestQueue(folder As String)
        Using queue As New ProcessingJobQueueService(folder), gate As New ManualResetEventSlim(False)
            Dim snapshots As New ConcurrentQueue(Of ProcessingJob)
            AddHandler queue.JobChanged, Sub(job) snapshots.Enqueue(job)
            queue.Start(2)
            Dim started As New ManualResetEventSlim(False)
            Dim sequence As New List(Of Integer)
            Dim independent As Boolean
            Dim first = queue.Enqueue("first", "test", "same", "", Function(j, t)
                queue.Report(j, "initial stage", 35)
                started.Set()
                gate.Wait(t)
                SyncLock sequence
                    sequence.Add(1)
                End SyncLock
                Return "first"
            End Function)
            started.Wait(1000)
            Dim jobView = queue.GetJobs().First(Function(j) j.Id = first.Id)
            jobView.Message = "changed by observer"
            Check(first.Message = "initial stage", "Job list observers cannot mutate a running task")
            Dim second = queue.Enqueue("second", "test", "same", "", Function(j, t)
                SyncLock sequence
                    sequence.Add(2)
                End SyncLock
                Return "second"
            End Function)
            Dim other = queue.Enqueue("other", "test", "different", "", Function(j, t)
                independent = True
                Return "other"
            End Function)
            WaitUntil(Function() independent, "independent document progress")
            Check(second.State = "منتظر", "Same document waits while another document runs")
            gate.Set()
            WaitUntil(Function() second.State = "مكتملة", "ordered document jobs")
            Check(sequence.SequenceEqual(New Integer() {1, 2}), "FIFO execution for each document")
            Dim reported = snapshots.First(Function(j) j.Id = first.Id AndAlso j.Message = "initial stage")
            Check(reported.State = "قيد التنفيذ" AndAlso reported.ProgressPercent = 35 AndAlso reported.CanCancel, "Progress event snapshot remains stable after job completion")
            gate.Reset()
            started.Reset()
            Dim blocker = queue.Enqueue("block", "test", "same", "", Function(j, t)
                started.Set()
                gate.Wait(t)
                Return "block"
            End Function)
            started.Wait(1000)
            Dim executed As Boolean
            Dim canceled = queue.Enqueue("canceled", "test", "same", "", Function(j, t)
                executed = True
                Return "bad"
            End Function)
            queue.CancelJob(canceled.Id)
            gate.Set()
            WaitUntil(Function() canceled.State = "ملغاة", "pending cancellation")
            Check(Not executed, "Canceled pending job has no side effects")
            Dim failure = queue.Enqueue("failure", "test", "fail", "", Function(j, t)
                Throw New InvalidOperationException("expected failure")
                Return ""
            End Function)
            WaitUntil(Function() failure.State = "فاشلة", "job error state")
            Check(failure.ErrorMessage.Contains("expected failure"), "Errors remain visible")
            WaitUntil(Function() queue.GetActiveCount() = 0, "queue drain")
            Check(XDocument.Load(Path.Combine(folder, "processing-jobs-v24.xml")).Root.Elements().Count() >= 6, "Job history remains parseable after concurrent writes")
        End Using
    End Sub
    Private Sub TestJobNotifications()
        Dim notices As New BasiraJobNotifications()
        Dim job As New ProcessingJob With {.Id = "progress", .State = "قيد التنفيذ", .ProgressPercent = 100}
        Check(Not notices.Consume(job) AndAlso BasiraJobNotifications.IsActive(job), "100 percent stage is not a completion notification")
        job.State = "مكتملة"
        Check(notices.Consume(job) AndAlso Not notices.Consume(job), "A completed job generates one notification")
        Dim failure As New ProcessingJob With {.Id = "error", .State = "فاشلة", .ErrorMessage = "test error"}
        Check(notices.Consume(failure) AndAlso BasiraJobNotifications.OutcomeTitle(failure) = "تعذرت المعالجة", "A failed job generates an error notification")
        Dim canceled As New ProcessingJob With {.Id = "cancel", .State = "ملغاة", .ProgressPercent = 25}
        Check(notices.Consume(canceled) AndAlso BasiraJobNotifications.OutcomeTitle(canceled) = "أُلغيت المعالجة", "Canceled processing is distinct from completed processing")
        Dim past As New ProcessingJob With {.Id = "past", .State = "مكتملة"}
        Dim pending As New ProcessingJob With {.Id = "pending", .State = "منتظر"}
        notices.Seed(New ProcessingJob() {past, pending})
        pending.State = "مكتملة"
        Check(Not notices.Consume(past) AndAlso notices.Consume(pending), "Startup suppresses old completions and retains active jobs")
        Dim original As New ProcessingJob With {.Id = "snapshot", .State = "قيد التنفيذ", .ProgressPercent = 40, .Message = "OCR", .CanCancel = True}
        Dim snapshot = original.Snapshot()
        original.State = "مكتملة"
        original.ProgressPercent = 100
        original.Message = "done"
        original.CanCancel = False
        Check(snapshot.State = "قيد التنفيذ" AndAlso snapshot.ProgressPercent = 40 AndAlso snapshot.Message = "OCR" AndAlso snapshot.CanCancel, "Snapshot isolates all displayed progress values")
        Check(Not notices.Consume(Nothing) AndAlso Not notices.Consume(New ProcessingJob With {.Id = "", .State = "مكتملة"}), "Missing notification identity is ignored")
        Dim concurrentJob As New ProcessingJob With {.Id = "concurrent", .State = "مكتملة"}
        Dim deliveries As Integer
        System.Threading.Tasks.Parallel.For(0, 40, Sub(index)
            If notices.Consume(concurrentJob) Then Interlocked.Increment(deliveries)
        End Sub)
        Check(deliveries = 1, "Concurrent duplicate terminal events generate one notification")
    End Sub
    Private Sub TestHealthReport()
        Dim docs As New List(Of DocumentRecord) From {
            New DocumentRecord With {.Title = "عقد <صيانة>", .Status = "قيد المراجعة", .TextContent = "نص"},
            New DocumentRecord With {.Title = "فاتورة", .Status = "مؤرشف"}}
        Dim engine As New BasiraEngine(docs)
        Dim html = ArchiveHealthReportService.Build(docs, engine.Findings(), "اختبار", New DateTime(2026, 10, 1))
        Check(html.Contains("dir='rtl'") AndAlso html.Contains("تقرير صحة الأرشيف"), "Health report is RTL HTML")
        Check(Not html.Contains("<صيانة>") AndAlso html.Contains("&lt;صيانة&gt;"), "Health report encodes document titles")
        Check(ArchiveHealthReportService.Build(Nothing, Nothing, "", DateTime.Today).Contains("0%"), "Health report handles empty archive")
    End Sub

    Private Sub TestPasswordPolicy()
        Check(PasswordPolicy.Validate("short1") <> "", "Password policy rejects short passwords")
        Check(PasswordPolicy.Validate("admin123") <> "" AndAlso PasswordPolicy.Validate("aaaaaaaaaaaa") <> "", "Password policy rejects common and repeated passwords")
        Check(PasswordPolicy.Validate("My-admin-Secret-9", "admin") <> "", "Password policy rejects embedded user name")
        Check(PasswordPolicy.Validate("Tr0ub4dor&3x!") = "", "Password policy accepts a strong password")
    End Sub

    ' قياس أداء المحرك على أرشيف اصطناعي: dotnet run --project Tests -c Release -- --bench
    Private Sub Benchmark()
        Dim words = "عقد صيانة أنظمة تجهيزات فاتورة مالية تعميم إجازات موارد بشرية تدريب موظفين تقرير أداء محضر تسليم مراجعة اعتماد مشروع توريد مناقصة ميزانية شكوى طلب قرار لجنة اجتماع خطة".Split(" "c)
        Dim cats = New String() {"عقود", "مالي", "إداري", "تقارير", "مراسلات", "موارد بشرية"}
        Dim rnd As New Random(42)
        For Each size In New Integer() {1000, 10000, 50000}
            Dim docs As New List(Of DocumentRecord)
            For i = 1 To size
                Dim sb As New StringBuilder()
                For w = 1 To 120
                    sb.Append(words(rnd.Next(words.Length))).Append(" "c)
                Next
                docs.Add(New DocumentRecord With {.Id = i.ToString(), .Title = words(rnd.Next(words.Length)) & " " & words(rnd.Next(words.Length)) & " " & i.ToString(), .GeneralCategory = cats(rnd.Next(cats.Length)), .Status = If(i Mod 5 = 0, "مؤرشف", "قيد المراجعة"), .TextContent = sb.ToString(), .FileHashSha256 = Guid.NewGuid().ToString("N"), .DueDateText = If(i Mod 7 = 0, "2026/10/" & (1 + i Mod 28).ToString("00"), "")})
            Next
            Dim sw = Stopwatch.StartNew()
            Dim engine As New BasiraEngine(docs)
            Dim build = sw.ElapsedMilliseconds
            sw.Restart()
            Dim hits = 0
            For q = 1 To 20
                hits += engine.Search(words(rnd.Next(words.Length)) & " " & words(rnd.Next(words.Length)), "", "all", 100).Count
            Next
            Dim search = sw.ElapsedMilliseconds / 20.0R
            sw.Restart()
            Dim findings = engine.Findings(New DateTime(2026, 10, 15)).Count
            Dim find = sw.ElapsedMilliseconds
            sw.Restart()
            Dim answer = engine.LocalAnswer("عقد صيانة")
            Dim ans = sw.ElapsedMilliseconds
            Console.WriteLine("N=" & size.ToString() & " | build=" & build.ToString() & "ms | search(avg of 20)=" & search.ToString("0.0", CultureInfo.InvariantCulture) & "ms | findings=" & find.ToString() & "ms (" & findings.ToString() & ") | answer=" & ans.ToString() & "ms")
        Next
        Dim corpusText = String.Join(vbLf, Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "Corpus")).SelectMany(Function(d) Directory.GetFiles(d, "*.txt")).Select(Function(f) IO.File.ReadAllText(f, Encoding.UTF8)))
        Dim filler = String.Join(" ", Enumerable.Repeat("نفيدكم بأنه تمت دراسة الموضوع من قبل الإدارة المختصة وتقرر ما يلي", 40))
        Dim big As New StringBuilder()
        While big.Length < 1000000
            big.Append(corpusText).Append(vbLf).Append(filler).Append(vbLf)
        End While
        Dim swp = Stopwatch.StartNew()
        Dim found = CorrespondenceReferenceParser.Parse(big.ToString()).Count
        Console.WriteLine("Reference parser: " & (big.Length \ 1024).ToString() & " KB in " & swp.ElapsedMilliseconds.ToString() & "ms (" & found.ToString() & " unique references)")
        Dim letters = Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "Corpus")).SelectMany(Function(d) Directory.GetFiles(d, "*.txt")).Select(Function(f) IO.File.ReadAllText(f, Encoding.UTF8) & vbLf & filler & vbLf & filler).ToList()
        CorrespondenceReferenceParser.Parse(letters(0))
        swp.Restart()
        Dim parsed = 0
        For rep = 1 To 200
            For Each l In letters
                CorrespondenceReferenceParser.Parse(l)
                parsed += 1
            Next
        Next
        Console.WriteLine("Reference parser: " & parsed.ToString() & " letters (~" & (letters.Average(Function(l) l.Length) / 1024).ToString("0.0", CultureInfo.InvariantCulture) & " KB each) in " & swp.ElapsedMilliseconds.ToString() & "ms = " & (swp.Elapsed.TotalMilliseconds * 1000 / parsed).ToString("0", CultureInfo.InvariantCulture) & " µs/letter")
    End Sub

    Private Sub TestAuditChain()
        Dim lines As New List(Of String)
        Dim prev = AuditChain.GenesisHash
        For i = 1 To 5
            Dim line = AuditChain.BuildLine(i, prev, New DateTime(2026, 10, 1, 8, i, 0, DateTimeKind.Utc), "مستخدم" & vbTab & "1", "بحث", "سطر" & vbLf & "ثانٍ \ " & i.ToString())
            lines.Add(line)
            prev = line.Split(ChrW(9))(6)
        Next
        Dim ok = AuditChain.Verify(lines)
        Check(ok.IsValid AndAlso ok.Count = 5 AndAlso ok.LastHash = prev, "Audit chain verifies untouched log")
        Dim edited = New List(Of String)(lines)
        edited(2) = edited(2).Replace("بحث", "حذف")
        Dim r1 = AuditChain.Verify(edited)
        Check(Not r1.IsValid AndAlso r1.FirstBrokenLine = 3, "Audit chain detects edited entry")
        Dim removed = New List(Of String)(lines)
        removed.RemoveAt(1)
        Dim r2 = AuditChain.Verify(removed)
        Check(Not r2.IsValid AndAlso r2.FirstBrokenLine = 2, "Audit chain detects removed entry")
        Dim swapped = New List(Of String)(lines)
        Dim tmp = swapped(3) : swapped(3) = swapped(4) : swapped(4) = tmp
        Check(Not AuditChain.Verify(swapped).IsValid, "Audit chain detects reordering")
        Check(AuditChain.Verify(New List(Of String)).IsValid, "Audit chain accepts empty log")
    End Sub

    Private Sub TestServerAuth(folder As String)
        Dim data = Path.Combine(folder, "auth")
        Dim settings As New AppSettings With {.BaseDataFolder = data, .ArchiveRootFolder = Path.Combine(data, "files"), .CurrentEmployeeName = "اختبار"}
        Dim auth As New AuthService(settings)
        Check(auth.NeedsInitialSetup, "Fresh store requires initial setup")
        auth.CreateInitialAdmin("Tr0ub4dor&3x!")
        Check(Not auth.NeedsInitialSetup, "Initial admin created once")
        Check(auth.VerifyCredentials("ADMIN", "Tr0ub4dor&3x!") IsNot Nothing, "VerifyCredentials accepts correct password")
        Check(auth.VerifyCredentials("admin", "wrong-password") Is Nothing AndAlso auth.VerifyCredentials("nobody", "x") Is Nothing, "VerifyCredentials rejects bad credentials")
        Check(auth.CurrentUser Is Nothing, "VerifyCredentials has no session side effect")
        Check(AuthService.RoleHasPermission("قارئ", "search") AndAlso Not AuthService.RoleHasPermission("قارئ", "export") AndAlso AuthService.RoleHasPermission("مراجع", "audit"), "Role permissions")
    End Sub

    ' يقيس دقة محلل الإشارات على مجلد خطابات: لكل NAME.txt ملف NAME.expected بأسطر النوع|الجهة|الرقم|التاريخ.
    Private Function EvaluateReferences(folder As String, verbose As Boolean) As Tuple(Of Integer, Integer, Integer, Integer)
        Dim tp = 0, predicted = 0, expected = 0, cases = 0
        For Each file In Directory.GetFiles(folder, "*.txt").OrderBy(Function(f) f, StringComparer.Ordinal)
            cases += 1
            Dim refs = CorrespondenceReferenceParser.Parse(IO.File.ReadAllText(file, Encoding.UTF8))
            Dim got = refs.Select(Function(r) r.ToExpectedLine()).ToList()
            Dim expPath = Path.ChangeExtension(file, ".expected")
            Dim exp = If(IO.File.Exists(expPath), IO.File.ReadAllLines(expPath, Encoding.UTF8).Where(Function(l) l.Trim() <> "").Select(Function(l) l.Trim()).ToList(), Nothing)
            predicted += got.Count
            If exp IsNot Nothing Then
                expected += exp.Count
                Dim hits = got.Where(Function(g) exp.Contains(g)).Count()
                tp += hits
                Dim ok = hits = exp.Count AndAlso hits = got.Count
                If verbose OrElse Not ok Then
                    Console.WriteLine(If(ok, "OK   ", "DIFF ") & Path.GetFileName(file))
                    For Each m In exp.Where(Function(e) Not got.Contains(e))
                        Console.WriteLine("       فائت:    " & m)
                    Next
                    For Each g In got.Where(Function(x) Not exp.Contains(x))
                        Console.WriteLine("       زائد:    " & g)
                    Next
                End If
            ElseIf verbose Then
                Console.WriteLine("---- " & Path.GetFileName(file))
                For Each r In refs
                    Console.WriteLine("       " & r.ToExpectedLine() & "  | " & r.KindLabel & " | " & r.OwnerLabel & " | ثقة " & r.Confidence.ToString() & " | " & r.Evidence)
                Next
            End If
        Next
        If expected > 0 Then
            Dim precision = If(predicted = 0, 1.0R, tp / CDbl(predicted))
            Dim recall = tp / CDbl(expected)
            Console.WriteLine("Reference corpus: " & cases.ToString() & " cases, expected " & expected.ToString() & ", predicted " & predicted.ToString() &
                              ", precision " & precision.ToString("0.000", CultureInfo.InvariantCulture) & ", recall " & recall.ToString("0.000", CultureInfo.InvariantCulture))
        End If
        Return Tuple.Create(tp, predicted, expected, cases)
    End Function

    Private Sub TestReferenceParser()
        Dim canonical As String = ""
        Dim value As DateTime
        Check(CorrespondenceReferenceParser.TryParseDate("1447/01/01هـ", canonical, value) AndAlso value = New DateTime(2025, 6, 26) AndAlso canonical = "H1447-01-01", "Hijri 1 Muharram 1447 is 26 June 2025 (Umm al-Qura)")
        Check(CorrespondenceReferenceParser.TryParseDate("1 رمضان 1446", canonical, value) AndAlso value = New DateTime(2025, 3, 1), "Textual Hijri month resolves")
        Check(CorrespondenceReferenceParser.TryParseDate("٣ سبتمبر ٢٠٢٥م", canonical, value) AndAlso canonical = "G2025-09-03", "Textual Gregorian month with Eastern digits")
        Check(Not CorrespondenceReferenceParser.TryParseDate("1447/13/40هـ", canonical, value) AndAlso Not CorrespondenceReferenceParser.TryParseDate("12/3/47", canonical, value), "Reject invalid and two-digit-year dates")
        Check(CorrespondenceReferenceParser.NumberKey(" ٠٠٤٥ / ص ") = "45/ص" AndAlso CorrespondenceReferenceParser.NumberKey("hr-2025-0114") = "HR/2025/114", "Reference number canonical form")
        Check(CorrespondenceReferenceParser.NumbersMatch("4567/12", "٤٥٦٧-١٢") AndAlso CorrespondenceReferenceParser.NumbersMatch("4567 / 12", "456712") AndAlso Not CorrespondenceReferenceParser.NumbersMatch("12", "120"), "Tolerant reference number matching")
        Dim letter = "السلام عليكم" & vbLf & "إشـارةً إلى خطابكم رقم ٤٥٦٧ وتاريخ ١٤٤٧/٣/١٢هـ بشأن التقرير."
        Dim refs = CorrespondenceReferenceParser.Parse(letter)
        Check(refs.Count = 1 AndAlso refs(0).Evidence.StartsWith("إشـارةً إلى خطابكم", StringComparison.Ordinal) AndAlso letter.Substring(refs(0).Offset, refs(0).Length).EndsWith("١٤٤٧/٣/١٢هـ", StringComparison.Ordinal), "Reference evidence maps to original text")
        Check(refs(0).RawNumber = "٤٥٦٧" AndAlso refs(0).Number = "4567" AndAlso refs(0).Confidence = 100, "Reference keeps raw number and full confidence")
        Check(CorrespondenceReferenceParser.Parse("").Count = 0 AndAlso CorrespondenceReferenceParser.Parse(New String("ا"c, 5000)).Count = 0, "Reference parser handles empty and degenerate input")
        For Each corpusDir In Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "Corpus")).OrderBy(Function(d) d, StringComparer.Ordinal)
            Dim score = EvaluateReferences(corpusDir, False)
            Check(score.Item3 > 0 AndAlso score.Item1 = score.Item3 AndAlso score.Item1 = score.Item2, "Reference corpus '" & Path.GetFileName(corpusDir) & "' fully extracted")
        Next
    End Sub

    Private Sub TestCorrespondenceIndex()
        Dim original As New DocumentRecord With {.Id = "A", .Title = "خطاب الوزارة", .ArchiveNumber = "ARC-2026-001", .ExternalNumber = "4567/12"}
        Dim ours As New DocumentRecord With {.Id = "B", .Title = "خطابنا السابق", .ArchiveNumber = "ARC-2026-002", .OcrOutgoingNumbers = "1120، 1121"}
        Dim reply As New DocumentRecord With {.Id = "C", .Title = "الرد", .ArchiveNumber = "ARC-2026-003", .OcrReferenceNumbers = "4567/12", .TextContent = "إشارة إلى خطابكم رقم ٤٥٦٧-١٢ وتاريخ ١٤٤٧/٣/١٢هـ، وإلحاقاً لخطابنا رقم 1121، ورداً على خطابكم رقم 999."}
        Dim index As New CorrespondenceIndex(New List(Of DocumentRecord) From {original, ours, reply})
        Dim matches = index.Resolve(reply.TextContent, reply.Id)
        Check(matches.Count = 3, "Index resolves every reference in the text")
        Check(matches(0).Documents.Count = 1 AndAlso matches(0).Documents(0).Id = "A", "Reference links to the cited document by external number")
        Check(matches(1).Documents.Count = 1 AndAlso matches(1).Documents(0).Id = "B", "Reference links through multi-valued outgoing numbers")
        Check(matches(2).Documents.Count = 0, "Unknown reference number has no link")
        Check(index.Resolve(original.TextContent, original.Id).Count = 0 AndAlso index.Find(New CorrespondenceReference With {.Number = "4567/12"}, "A").Count = 0, "Index excludes the document itself and numbers only cited in text")
    End Sub

    Private Sub TestDeadlines()
        Dim baseDate As New DateTime(2026, 10, 1) ' خميس
        Dim d1 = DeadlineExtractor.Extract("نأمل الإفادة خلال (15) يوماً من تاريخه.", baseDate)
        Check(d1.Count = 1 AndAlso d1(0).Days = 15 AndAlso d1(0).Due.GetValueOrDefault() = New DateTime(2026, 10, 16) AndAlso d1(0).Evidence.StartsWith("خلال", StringComparison.Ordinal), "Relative deadline in days")
        Dim d2 = DeadlineExtractor.Extract("على أن يتم ذلك خلال عشرة أيام عمل.", baseDate)
        Check(d2.Count = 1 AndAlso d2(0).BusinessDays AndAlso d2(0).Due.GetValueOrDefault() = New DateTime(2026, 10, 15), "Business-day deadline skips Friday and Saturday")
        Dim d3 = DeadlineExtractor.Extract("نأمل الرد خلال أسبوعين.", baseDate)
        Check(d3.Count = 1 AndAlso d3(0).Due.GetValueOrDefault() = New DateTime(2026, 10, 15), "Deadline period in weeks")
        Dim d4 = DeadlineExtractor.Extract("ونأمل تزويدنا بها في موعد أقصاه ١٤٤٨/٥/١هـ.", Nothing)
        Check(d4.Count = 1 AndAlso Not d4(0).IsRelative AndAlso d4(0).Due.GetValueOrDefault() = New UmAlQuraCalendar().ToDateTime(1448, 5, 1, 0, 0, 0, 0), "Absolute Hijri deadline")
        Dim d5 = DeadlineExtractor.Extract("خلال 15 يوماً من تاريخه", Nothing)
        Check(d5.Count = 1 AndAlso Not d5(0).Due.HasValue, "Relative deadline without document date keeps days only")
        Check(DeadlineExtractor.Extract("تم ذلك خلال اجتماع اللجنة وخلال 2025 بلغ العدد 15.", baseDate).Count = 0, "No deadline from unrelated 'during' phrases")
        Dim d6 = DeadlineExtractor.Extract("نأمل تزويدنا بالبيانات خلال (15) leg, من تاريخه، مع بيان الأسباب.", baseDate)
        Check(d6.Count = 1 AndAlso d6(0).Days = 15 AndAlso d6(0).Due.GetValueOrDefault() = New DateTime(2026, 10, 16), "OCR-garbled unit before 'from its date' read as days")
        Check(DeadlineExtractor.Extract("خلال (15) يوماً من تاريخه", baseDate).Count = 1, "No double count when unit is readable")
        Check(DeadlineExtractor.EarliestDue("خلال 30 يوماً، أو في موعد أقصاه 2026/10/05م", baseDate).GetValueOrDefault() = New DateTime(2026, 10, 5), "Earliest due date wins")
        Dim d7 = DeadlineExtractor.Extract("نأمل تزويدنا بالبيان في موعد أقصاه عشرة أيام من تاريخه.", baseDate)
        Check(d7.Count = 1 AndAlso d7(0).Days = 10 AndAlso d7(0).Due.GetValueOrDefault() = New DateTime(2026, 10, 11), "'At most' with a period of days is relative")
        Dim d8 = DeadlineExtractor.Extract("وذلك في موعد أقصاه أسبوعان من تاريخه.", baseDate)
        Check(d8.Count = 1 AndAlso d8(0).Due.GetValueOrDefault() = New DateTime(2026, 10, 15), "'At most' with weeks is relative")
        Dim d9 = DeadlineExtractor.Extract("في موعد أقصاه يوم الأحد 1448/5/1هـ", baseDate)
        Check(d9.Count = 1 AndAlso Not d9(0).IsRelative, "'At most' before a date stays absolute (no one-day reading)")
    End Sub

    Private Sub TestProcesses()
        If OperatingSystem.IsWindows() Then
            Console.WriteLine("SKIP Linux process fixtures; validate OCR on Windows")
            Return
        End If
        Dim command As New ProcessStartInfo("python3", "-c ""import sys;sys.stdout.write('x'*300000);sys.stderr.write('y'*300000)""")
        Check(CancellableProcess.Run(command, 5000, CancellationToken.None).Length = 300000, "Drain stdout and stderr concurrently")
        Dim canceled As Boolean
        Using token As New CancellationTokenSource(100)
            Try
                CancellableProcess.Run(New ProcessStartInfo("python3", "-c ""import time;time.sleep(10)"""), 15000, token.Token)
            Catch ex As OperationCanceledException
                canceled = True
            End Try
        End Using
        Check(canceled, "Cancellation interrupts external process")
        Dim timedOut As Boolean
        Try
            CancellableProcess.Run(New ProcessStartInfo("python3", "-c ""import time;time.sleep(10)"""), 100, CancellationToken.None)
        Catch ex As TimeoutException
            timedOut = True
        End Try
        Check(timedOut, "External process enforces timeout")
    End Sub
End Module
