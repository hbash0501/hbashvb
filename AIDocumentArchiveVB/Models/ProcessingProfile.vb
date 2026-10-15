Public Class ProcessingProfile
    Public Property Name As String
    Public Property Description As String
    Public Property RunOcr As Boolean
    Public Property ExtractEntities As Boolean
    Public Property RunQuickAi As Boolean
    Public Property RunDeepAiForSensitiveDocuments As Boolean
    Public Property GenerateReplyDraftWhenNeeded As Boolean
    Public Property ApplyAutomaticRouting As Boolean
    Public Property MinimumQualityScore As Integer

    Public Overrides Function ToString() As String
        Return If(Name, "")
    End Function
End Class
