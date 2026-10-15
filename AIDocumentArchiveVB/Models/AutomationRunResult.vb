Imports System
Imports System.Collections.Generic

Public Class AutomationRunResult
    Public Property DocumentId As String
    Public Property DocumentTitle As String
    Public Property ProfileName As String
    Public Property StartedAt As DateTime
    Public Property CompletedAt As DateTime
    Public Property Success As Boolean
    Public Property QualityBefore As Integer
    Public Property QualityAfter As Integer
    Public Property Summary As String
    Public Property Actions As List(Of AutomationAction)

    Public Sub New()
        StartedAt = DateTime.Now
        Actions = New List(Of AutomationAction)()
    End Sub
End Class
