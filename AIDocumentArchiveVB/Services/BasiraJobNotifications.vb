Option Strict On
Imports System
Imports System.Collections.Generic

Public NotInheritable Class BasiraJobNotifications
    Private ReadOnly _seen As New HashSet(Of String)(StringComparer.Ordinal)
    Private ReadOnly _gate As New Object()

    Public Shared Function IsActive(job As ProcessingJob) As Boolean
        Return job IsNot Nothing AndAlso (job.State = "منتظر" OrElse job.State = "قيد التنفيذ")
    End Function

    Public Shared Function IsTerminal(job As ProcessingJob) As Boolean
        Return job IsNot Nothing AndAlso (job.State = "مكتملة" OrElse job.State = "فاشلة" OrElse job.State = "ملغاة")
    End Function

    Public Sub Seed(jobs As IEnumerable(Of ProcessingJob))
        If jobs Is Nothing Then Return
        SyncLock _gate
            For Each job In jobs
                If IsTerminal(job) AndAlso Not String.IsNullOrWhiteSpace(job.Id) Then _seen.Add(job.Id)
            Next
        End SyncLock
    End Sub

    Public Function Consume(job As ProcessingJob) As Boolean
        If Not IsTerminal(job) OrElse String.IsNullOrWhiteSpace(job.Id) Then Return False
        SyncLock _gate
            Return _seen.Add(job.Id)
        End SyncLock
    End Function

    Public Shared Function OutcomeTitle(job As ProcessingJob) As String
        If job Is Nothing Then Return "حالة المعالجة"
        Select Case job.State
            Case "مكتملة" : Return "اكتملت المعالجة"
            Case "فاشلة" : Return "تعذرت المعالجة"
            Case "ملغاة" : Return "أُلغيت المعالجة"
            Case Else : Return "المعالجة مستمرة"
        End Select
    End Function
End Class
