Imports System

Public Class KnowledgeEntity
    Public Property Id As String
    Public Property DocumentId As String
    Public Property EntityType As String
    Public Property EntityValue As String
    Public Property SourceName As String
    Public Property ConfidencePercent As Integer
    Public Property CreatedAt As DateTime

    Public Sub New()
        Id = Guid.NewGuid().ToString("N")
        CreatedAt = DateTime.Now
        SourceName = "قواعد محلية"
        ConfidencePercent = 70
    End Sub
End Class
