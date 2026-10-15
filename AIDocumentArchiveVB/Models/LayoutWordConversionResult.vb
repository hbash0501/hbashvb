Imports System.Collections.Generic

Public Class LayoutWordConversionResult
    Public Property OutputPath As String = ""
    Public Property PageCount As Integer
    Public Property ExtractedText As String = ""
    Public Property Warnings As New List(Of String)()
    Public Property DurationSeconds As Double
End Class
