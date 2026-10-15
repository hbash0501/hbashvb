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
    Sub Main()
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
