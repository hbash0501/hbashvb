Imports System

Public Class AuditLogEntry
    Public Property Id As String
    Public Property ActionName As String
    Public Property DocumentId As String
    Public Property DocumentTitle As String
    Public Property EmployeeName As String
    Public Property Details As String
    Public Property CreatedAt As DateTime

    Public Sub New()
        Id = Guid.NewGuid().ToString("N")
        CreatedAt = DateTime.Now
    End Sub
End Class
