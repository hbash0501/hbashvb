Public Class KnowledgeSearchResult
    Public Property Document As DocumentRecord
    Public Property Score As Integer
    Public Property Evidence As String

    Public ReadOnly Property DocumentId As String
        Get
            If Document Is Nothing Then Return ""
            Return Document.Id
        End Get
    End Property

    Public ReadOnly Property Title As String
        Get
            If Document Is Nothing Then Return ""
            Return Document.Title
        End Get
    End Property
End Class
