Imports System
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Xml.Linq

Partial Public Class ProcessingJobQueueService
    Implements IDisposable
    Private ReadOnly _baseFolder As String
    Private ReadOnly _jobs As New ConcurrentDictionary(Of String, ProcessingJob)()
    Private ReadOnly _workItems As New ConcurrentQueue(Of QueueWorkItem)()
    Private ReadOnly _lastDocumentJobs As New Dictionary(Of String, ProcessingJob)(StringComparer.Ordinal)
    Private ReadOnly _signal As New AutoResetEvent(False)
    Private ReadOnly _cts As New CancellationTokenSource()
    Private ReadOnly _jobTokens As New ConcurrentDictionary(Of String, CancellationTokenSource)()
    Private ReadOnly _enqueueGate As New Object()
    Private ReadOnly _persistGate As New Object()
    Private ReadOnly _workers As New List(Of Task)()
    Private _workerStarted As Boolean
    Private _disposed As Boolean
    Public Event JobChanged(job As ProcessingJob)
    Private Class QueueWorkItem
        Public Job As ProcessingJob
        Public Previous As ProcessingJob
        Public Work As Func(Of ProcessingJob, CancellationToken, Object)
    End Class

    Public Sub New(baseFolder As String)
        _baseFolder = baseFolder
    End Sub

    Public Sub Start(Optional maxWorkers As Integer = 1)
        SyncLock _enqueueGate
            If _disposed Then Throw New ObjectDisposedException(NameOf(ProcessingJobQueueService))
            If _workerStarted Then Return
            _workerStarted = True
            For i = 1 To Math.Max(1, Math.Min(3, maxWorkers))
                _workers.Add(Task.Factory.StartNew(AddressOf WorkerLoop, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            Next
        End SyncLock
    End Sub

    Public Function Enqueue(title As String, operationKind As String, documentId As String, filePath As String, work As Func(Of ProcessingJob, CancellationToken, Object)) As ProcessingJob
        Dim job As New ProcessingJob With {.Title = title, .OperationKind = operationKind, .DocumentId = If(documentId, ""), .FilePath = If(filePath, ""), .Message = "بانتظار بدء المعالجة"}
        SyncLock _enqueueGate
            If _disposed Then Throw New ObjectDisposedException(NameOf(ProcessingJobQueueService))
            If Not _workerStarted Then Start(1)
            Dim previous As ProcessingJob = Nothing
            If job.DocumentId <> "" Then
                _lastDocumentJobs.TryGetValue(job.DocumentId, previous)
                _lastDocumentJobs(job.DocumentId) = job
            End If
            _jobs(job.Id) = job
            _jobTokens(job.Id) = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token)
            _workItems.Enqueue(New QueueWorkItem With {.Job = job, .Previous = previous, .Work = work})
        End SyncLock
        Notify(job)
        PersistLightweightHistory()
        _signal.Set()
        Return job
    End Function

    Public Function GetJobs() As List(Of ProcessingJob)
        Return _jobs.Values.Select(Function(j) j.Snapshot()).OrderByDescending(Function(j) j.CreatedAt).ToList()
    End Function

    Public Function GetActiveCount() As Integer
        Return _jobs.Values.Where(Function(j) Active(j)).Count()
    End Function

    Private Shared Function Active(job As ProcessingJob) As Boolean
        Return job.State = "منتظر" OrElse job.State = "قيد التنفيذ"
    End Function

    Public Sub CancelJob(jobId As String)
        Dim job As ProcessingJob = Nothing
        If Not _jobs.TryGetValue(jobId, job) OrElse Not Active(job) Then Return
        job.RequestedCancel = True
        Dim source As CancellationTokenSource = Nothing
        If _jobTokens.TryGetValue(jobId, source) Then
            Try
                source.Cancel()
            Catch ex As ObjectDisposedException
            End Try
        End If
        job.Message = "جارٍ إلغاء المعالجة"
        Notify(job)
    End Sub

    Public Sub Report(job As ProcessingJob, message As String, Optional percent As Integer = -1)
        If job Is Nothing Then Return
        If message IsNot Nothing Then job.Message = message
        If percent >= 0 Then job.ProgressPercent = Math.Max(0, Math.Min(100, percent))
        Notify(job)
    End Sub

    Private Sub WorkerLoop()
        Do While Not _cts.IsCancellationRequested
            Dim item As QueueWorkItem = Nothing
            If Not _workItems.TryDequeue(item) Then
                _signal.WaitOne(100)
                Continue Do
            End If
            ' ترتيب FIFO لكل مستند، مع السماح بتوازي المستندات المختلفة.
            If item.Previous IsNot Nothing AndAlso Active(item.Previous) Then
                _workItems.Enqueue(item)
                _signal.WaitOne(25)
                Continue Do
            End If
            Dim job = item.Job
            Try
                Dim source As CancellationTokenSource = Nothing
                If Not _jobTokens.TryGetValue(job.Id, source) Then Throw New OperationCanceledException()
                Dim token = source.Token
                token.ThrowIfCancellationRequested() ' لا تنفذ عملاً ألغي أثناء الانتظار.
                job.State = "قيد التنفيذ"
                job.StartedAt = DateTime.Now
                Notify(job)
                If item.Work IsNot Nothing Then
                    Dim result = item.Work(job, token)
                    If result IsNot Nothing Then job.ResultSummary = result.ToString()
                End If
                token.ThrowIfCancellationRequested()
                job.ProgressPercent = 100
                job.Message = "اكتملت المعالجة"
                job.State = "مكتملة"
            Catch ex As OperationCanceledException
                job.Message = "تم الإلغاء"
                job.State = "ملغاة"
            Catch ex As Exception
                job.ErrorMessage = ex.Message
                job.Message = "تعذرت المعالجة: " & ex.Message
                job.State = "فاشلة"
            Finally
                job.CanCancel = False
                job.CompletedAt = DateTime.Now
                If job.StartedAt <> DateTime.MinValue Then job.DurationMs = CLng((job.CompletedAt - job.StartedAt).TotalMilliseconds)
                Notify(job)
                Dim source As CancellationTokenSource = Nothing
                If _jobTokens.TryRemove(job.Id, source) Then source.Dispose()
                PersistLightweightHistory()
            End Try
        Loop
    End Sub

    Private Sub Notify(job As ProcessingJob)
        Try
            RaiseEvent JobChanged(job.Snapshot())
        Catch ex As Exception
            System.Diagnostics.Trace.WriteLine(ex.ToString())
        End Try
    End Sub

    Private Function HistoryPath() As String
        Directory.CreateDirectory(_baseFolder)
        Return Path.Combine(_baseFolder, "processing-jobs-v24.xml")
    End Function

    Private Sub PersistLightweightHistory()
        SyncLock _persistGate
            Try
                Dim root As New XElement("Jobs")
                For Each job In GetJobs().Take(200)
                    root.Add(New XElement("Job", New XAttribute("Id", job.Id), New XElement("Title", If(job.Title, "")), New XElement("OperationKind", If(job.OperationKind, "")), New XElement("DocumentId", job.DocumentId), New XElement("State", job.State), New XElement("Progress", job.ProgressPercent), New XElement("Message", If(job.Message, "")), New XElement("Error", If(job.ErrorMessage, "")), New XElement("CreatedAt", job.CreatedAt.ToString("o")), New XElement("DurationMs", job.DurationMs)))
                Next
                BasiraStore.AtomicSave(HistoryPath(), New XDocument(root))
            Catch ex As Exception
                System.Diagnostics.Trace.WriteLine(ex.ToString())
            End Try
        End SyncLock
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        SyncLock _enqueueGate
            If _disposed Then Return
            _disposed = True
            _cts.Cancel()
            _signal.Set()
        End SyncLock
        Try
            Task.WaitAll(_workers.ToArray(), 1500)
        Catch ex As AggregateException
        End Try
        For Each job In _jobs.Values.Where(Function(j) Active(j))
            job.State = "توقفت عند إغلاق البرنامج"
            job.Message = "أعد تشغيل المهمة من مركز المعالجة"
        Next
        PersistLightweightHistory()
        ' لا تحرر أحداث الانتظار إذا بقي مزود قديم في اتصال متزامن.
        If _workers.All(Function(t) t.IsCompleted) Then
            _signal.Dispose()
            _cts.Dispose()
        End If
    End Sub
End Class
