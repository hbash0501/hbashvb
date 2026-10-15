Imports System

Public Class PerformanceSnapshot
    Public Property TotalDocuments As Integer
    Public Property MissingOcr As Integer
    Public Property MissingClassification As Integer
    Public Property WaitingReview As Integer
    Public Property ActiveJobs As Integer
    Public Property CacheTextCount As Integer
    Public Property CacheAiCount As Integer
    Public Property AverageReadiness As Integer
    Public Property LastUpdated As DateTime = DateTime.Now
End Class
