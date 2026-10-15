Option Strict On
Imports System
Imports System.Security.Cryptography
Imports System.Text

Public NotInheritable Class PasswordHashService
    Private Sub New()
    End Sub
    Public Shared Function Hash(password As String, salt As String) As String
        Using derive As New Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 210000, HashAlgorithmName.SHA256)
            Return "PBKDF2-SHA256$210000$" & Convert.ToBase64String(derive.GetBytes(32))
        End Using
    End Function
    Public Shared Function Verify(password As String, salt As String, stored As String) As Boolean
        Try
            Dim computed As Byte()
            Dim expected As Byte()
            If stored.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal) Then
                Dim parts = stored.Split("$"c)
                If parts.Length <> 3 Then Return False
                Dim iterations As Integer
                If Not Integer.TryParse(parts(1), iterations) OrElse iterations < 100000 OrElse iterations > 1000000 Then Return False
                Using derive As New Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), iterations, HashAlgorithmName.SHA256)
                    computed = derive.GetBytes(32)
                End Using
                expected = Convert.FromBase64String(parts(2))
            Else
                Using sha = SHA256.Create()
                    computed = sha.ComputeHash(Encoding.UTF8.GetBytes(password & "|" & salt))
                End Using
                expected = Convert.FromBase64String(stored)
            End If
            If expected.Length <> computed.Length Then Return False
            Dim difference As Integer = 0
            For i = 0 To expected.Length - 1
                difference = difference Or (CInt(expected(i)) Xor CInt(computed(i)))
            Next
            Return difference = 0
        Catch ex As FormatException
            Return False
        Catch ex As ArgumentException
            Return False
        End Try
    End Function
End Class
