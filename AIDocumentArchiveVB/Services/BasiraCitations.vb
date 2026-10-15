Option Strict On
Imports System
Imports System.Linq
Imports System.Text.RegularExpressions

Public NotInheritable Class BasiraCitations
    Private Sub New()
    End Sub
    Public Shared Function Valid(text As String, allowed As Integer()) As Boolean
        Dim matches = Regex.Matches(If(text, ""), "\[(\d+)\]").Cast(Of Match)().ToList()
        If matches.Count = 0 Then Return False
        For Each m In matches
            Dim number As Integer
            If Not Integer.TryParse(m.Groups(1).Value, number) OrElse Not allowed.Contains(number) Then Return False
        Next
        Return True
    End Function
End Class
