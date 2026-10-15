Imports System

Public Class ProcessingJob
    Public Property Id As String
    Public Property Title As String
    Public Property OperationKind As String
    Public Property DocumentId As String
    Public Property FilePath As String
    Public Property State As String
    Public Property ProgressPercent As Integer
    Public Property Message As String
    Public Property ErrorMessage As String
    Public Property CreatedAt As DateTime
    Public Property StartedAt As DateTime
    Public Property CompletedAt As DateTime
    Public Property DurationMs As Long
    Public Property ResultSummary As String
    Public Property CanCancel As Boolean
    Public Property RequestedCancel As Boolean

    Public Sub New()
        Id = Guid.NewGuid().ToString("N")
        CreatedAt = DateTime.Now
        State = "منتظر"
        ProgressPercent = 0
        CanCancel = True
    End Sub

    Public Function Snapshot() As ProcessingJob
        Return New ProcessingJob With {.Id = Id, .Title = Title, .OperationKind = OperationKind, .DocumentId = DocumentId, .FilePath = FilePath, .State = State, .ProgressPercent = ProgressPercent, .Message = Message, .ErrorMessage = ErrorMessage, .CreatedAt = CreatedAt, .StartedAt = StartedAt, .CompletedAt = CompletedAt, .DurationMs = DurationMs, .ResultSummary = ResultSummary, .CanCancel = CanCancel, .RequestedCancel = RequestedCancel}
    End Function
End Class
