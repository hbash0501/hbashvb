Imports System

Public Class DocumentRecord
    Public Property Id As String
    Public Property Title As String
    Public Property Description As String
    Public Property FileName As String
    Public Property OriginalPath As String
    Public Property StoredPath As String
    Public Property FileType As String
    Public Property TextContent As String
    Public Property Summary As String
    Public Property GeneralCategory As String
    Public Property SubCategory As String
    Public Property Tags As String
    Public Property ReferenceNumber As String
    Public Property Confidentiality As String
    Public Property Priority As String
    Public Property EmployeeName As String
    Public Property Status As String
    Public Property CreatedAt As DateTime
    Public Property UpdatedAt As DateTime
    Public Property FileHashSha256 As String
    Public Property FileSizeBytes As Long
    Public Property PageCount As Integer
    Public Property Department As String
    Public Property DueDateText As String
    Public Property AiConfidencePercent As Integer
    Public Property ArchiveNumber As String
    Public Property ExternalNumber As String
    Public Property SourceEntity As String
    Public Property DestinationEntity As String
    Public Property DocumentDateText As String
    Public Property WorkflowNote As String
    Public Property LastWorkflowBy As String
    Public Property LastWorkflowAt As DateTime
    Public Property OcrDocumentType As String
    Public Property OcrQualityScore As Integer
    Public Property OcrEngineInfo As String
    Public Property OcrDates As String
    Public Property OcrReferenceNumbers As String
    Public Property OcrIncomingNumbers As String
    Public Property OcrOutgoingNumbers As String
    Public Property OcrAmounts As String
    Public Property OcrNationalIds As String
    Public Property OcrIqamaNumbers As String
    Public Property OcrPassportNumbers As String
    Public Property OcrPhoneNumbers As String
    Public Property OcrEmails As String
    Public Property OcrNames As String
    Public Property OcrEntities As String
    Public Property OcrStructuredReport As String
    Public Property FileSignatureVerified As Boolean
    Public Property FileSignatureType As String
    Public Property SecurityRiskScore As Double
    Public Property IsEncrypted As Boolean
    Public Property ProtectedFilePath As String
    Public Property SecurityStatus As String

    Public Sub New()
        Id = Guid.NewGuid().ToString("N")
        CreatedAt = DateTime.Now
        UpdatedAt = DateTime.Now
        Status = "قيد المراجعة"
        Confidentiality = "عادي"
        Priority = "متوسط"
        LastWorkflowAt = DateTime.MinValue
    End Sub
End Class
