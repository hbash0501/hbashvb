Imports System

Public Class AiProviderProfile
    Public Property ProviderName As String
    Public Property Endpoint As String
    Public Property ModelName As String
    Public Property ApiKeyEnvironmentName As String
    Public Property Priority As Integer

    Public Overrides Function ToString() As String
        Return If(ProviderName, "") & " - " & If(ModelName, "")
    End Function
End Class
