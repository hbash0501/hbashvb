Imports System

Public Class EnterpriseProcessingResult
    Public Property DocumentId As String
    Public Property DocumentTitle As String
    Public Property Success As Boolean
    Public Property SignatureVerified As Boolean
    Public Property SignatureType As String
    Public Property RiskScore As Double
    Public Property ProtectedCopyCreated As Boolean
    Public Property ProtectedFilePath As String
    Public Property QualityScore As Integer
    Public Property EngineSummary As String
    Public Property Message As String
    Public Property CompletedAt As DateTime
End Class
