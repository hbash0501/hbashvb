Imports System
Imports System.Collections.Generic

Public Class BasiraHit
    Public Property Document As DocumentRecord
    Public Property Score As Double
    Public Property Excerpt As String
    Public Property Offset As Integer
    Public Property Matches As String
End Class

Public Class BasiraFinding
    Public Property DocumentId As String
    Public Property Title As String
    Public Property Reason As String
    Public Property Kind As String
    Public Property Severity As Integer
    Public Property Due As Nullable(Of DateTime)
End Class

Public Class BasiraEvidence
    Public Property Number As Integer
    Public Property DocumentId As String
    Public Property Title As String
    Public Property Reference As String
    Public Property Quote As String
    Public Property Offset As Integer
    Public Property Sensitive As Boolean
End Class

Public Class BasiraAnswer
    Public Property Question As String
    Public Property Text As String
    Public Property Mode As String
    Public Property Evidence As New List(Of BasiraEvidence)
End Class

Public Class BasiraCollection
    Public Property Id As String = Guid.NewGuid().ToString("N")
    Public Property Name As String
    Public Property Query As String
    Public Property Category As String
    Public Property Filter As String
End Class

Public Class BasiraLink
    Public Property Document As DocumentRecord
    Public Property Reason As String
End Class
