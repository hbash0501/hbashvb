Option Strict On
Imports System
Imports System.Globalization

Public NotInheritable Class BasiraDates
    Private Sub New()
    End Sub
    Public Shared Function ParseStored(value As String, fallback As DateTime) As DateTime
        Dim result As DateTime
        If DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, result) Then Return result
        If DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, result) Then Return result
        Return fallback
    End Function
End Class
