Imports System.Collections.Generic

Public Class ArchiveAnswerResult
    Public Property Question As String
    Public Property Answer As String
    Public Property Evidence As List(Of KnowledgeSearchResult)
    Public Property Relations As List(Of ArchiveRelation)
    Public Property CreatedAt As DateTime

    Public Sub New()
        Evidence = New List(Of KnowledgeSearchResult)()
        Relations = New List(Of ArchiveRelation)()
        CreatedAt = DateTime.Now
    End Sub
End Class
