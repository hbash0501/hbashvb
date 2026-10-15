Imports System

Public Class UserAccount
    Public Property Id As String
    Public Property FullName As String
    Public Property UserName As String
    Public Property PasswordHash As String
    Public Property Salt As String
    Public Property RoleName As String
    Public Property IsActive As Boolean
    Public Property CreatedAt As DateTime
    Public Property LastLoginAt As DateTime

    Public Sub New()
        Id = Guid.NewGuid().ToString("N")
        IsActive = True
        RoleName = "موظف أرشفة"
        CreatedAt = DateTime.Now
    End Sub
End Class
