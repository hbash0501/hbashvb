Public Class CategoryInfo
    Public Property GeneralName As String
    Public Property SubName As String

    Public Sub New()
    End Sub

    Public Sub New(generalName As String, subName As String)
        Me.GeneralName = generalName
        Me.SubName = subName
    End Sub
End Class
