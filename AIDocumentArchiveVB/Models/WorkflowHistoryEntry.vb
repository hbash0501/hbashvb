Imports System

Public Class WorkflowHistoryEntry
    Public Property Id As String
    Public Property DocumentId As String
    Public Property OldStatus As String
    Public Property NewStatus As String
    Public Property Note As String
    Public Property EmployeeName As String
    Public Property CreatedAt As DateTime

    Public Sub New()
        Id = Guid.NewGuid().ToString("N")
        CreatedAt = DateTime.Now
    End Sub
End Class
