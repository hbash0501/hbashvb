Imports System

Public Class PdfOcrOptions
    Public Property ForceOcr As Boolean = False
    Public Property Dpi As Integer = 300
    Public Property FirstPage As Integer = 1
    Public Property LastPage As Integer = 0
    Public Property MaxPages As Integer = 100
    Public Property KeepPageHeaders As Boolean = True

    Public Shared Function FromSettings(settings As AppSettings) As PdfOcrOptions
        Dim options As New PdfOcrOptions()
        If settings IsNot Nothing Then
            options.Dpi = Math.Max(150, Math.Min(600, settings.PdfOcrDpi))
            options.MaxPages = Math.Max(1, Math.Min(500, settings.PdfOcrMaxPages))
        End If
        Return options
    End Function
End Class
