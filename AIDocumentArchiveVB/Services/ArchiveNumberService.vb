Imports System
Imports System.Collections.Generic

Public Class ArchiveNumberService
    Private ReadOnly _repo As ArchiveRepository

    Public Sub New(repo As ArchiveRepository)
        _repo = repo
    End Sub

    Public Function GenerateNext(generalCategory As String) As String
        Dim prefix = GetPrefix(generalCategory)
        Dim yearText = DateTime.Now.Year.ToString()
        Dim serial = _repo.CountByArchivePrefix(prefix, yearText) + 1
        Return prefix & "-" & yearText & "-" & serial.ToString("000000")
    End Function

    Public Shared Function GetPrefix(generalCategory As String) As String
        Dim g = If(generalCategory, "").Trim()
        Select Case g
            Case "إداري" : Return "ADM"
            Case "مالي" : Return "FIN"
            Case "موارد بشرية" : Return "HR"
            Case "عقود" : Return "CON"
            Case "مراسلات" : Return "COR"
            Case "تقارير" : Return "REP"
            Case "سري" : Return "SEC"
            Case Else : Return "DOC"
        End Select
    End Function
End Class
