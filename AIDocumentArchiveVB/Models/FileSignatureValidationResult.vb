Public Class FileSignatureValidationResult
    Public Property FilePath As String
    Public Property Extension As String
    Public Property DetectedType As String
    Public Property IsValid As Boolean
    Public Property Message As String

    Public Overrides Function ToString() As String
        Return If(IsValid, "صالح", "غير صالح") & " | " & If(DetectedType, "غير معروف") & " | " & If(Message, "")
    End Function
End Class
