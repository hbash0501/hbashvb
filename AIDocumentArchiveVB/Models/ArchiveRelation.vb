Imports System

Public Class ArchiveRelation
    Public Property Id As String
    Public Property SourceDocument As DocumentRecord
    Public Property TargetDocument As DocumentRecord
    Public Property RelationType As String
    Public Property ConfidencePercent As Integer
    Public Property Reason As String
    Public Property SharedValues As String

    Public Sub New()
        Id = Guid.NewGuid().ToString("N")
        RelationType = "تشابه محتوى"
        ConfidencePercent = 0
        Reason = ""
        SharedValues = ""
    End Sub
End Class
