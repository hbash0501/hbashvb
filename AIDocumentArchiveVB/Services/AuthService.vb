Imports System
Imports System.Data
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Xml.Linq

Public Class AuthService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _usersPath As String
    Private _db As XDocument

    Public Property CurrentUser As UserAccount

    Public Sub New(settings As AppSettings)
        _settings = settings
        Directory.CreateDirectory(_settings.BaseDataFolder)
        _usersPath = Path.Combine(_settings.BaseDataFolder, "users.xml")
        LoadOrCreate()
    End Sub

    Private Sub LoadOrCreate()
        If File.Exists(_usersPath) Then
            _db = XDocument.Load(_usersPath)
        Else
            _db = New XDocument(New XElement("Users"))
            SaveDb()
        End If
    End Sub

    Private Sub SaveDb()
        BasiraStore.AtomicSave(_usersPath, _db)
    End Sub

    ' لا يُنشأ أي حساب افتراضي؛ يُنشأ حساب المدير الأول بكلمة مرور يحددها المستخدم.
    Public ReadOnly Property NeedsInitialSetup As Boolean
        Get
            Return Not _db.Root.Elements("User").Any()
        End Get
    End Property

    Public Sub CreateInitialAdmin(password As String)
        If Not NeedsInitialSetup Then Throw New InvalidOperationException("تم إعداد المنصة مسبقاً.")
        Dim reason = PasswordPolicy.Validate(password, "admin")
        If reason <> "" Then Throw New ArgumentException(reason)
        Dim admin As New UserAccount With {
            .FullName = "مدير النظام",
            .UserName = "admin",
            .RoleName = "مدير النظام",
            .IsActive = True
        }
        SetPassword(admin, password)
        _db.Root.Add(ToElement(admin))
        SaveDb()
    End Sub

    Public Function Authenticate(userName As String, password As String) As Boolean
        userName = If(userName, "").Trim()
        password = If(password, "")
        Dim u = GetUsers().FirstOrDefault(Function(x) String.Equals(x.UserName, userName, StringComparison.OrdinalIgnoreCase) AndAlso x.IsActive)
        If u Is Nothing Then Return False
        If Not PasswordHashService.Verify(password, u.Salt, u.PasswordHash) Then Return False
        If Not u.PasswordHash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal) Then SetPassword(u, password)
        u.LastLoginAt = DateTime.Now
        SaveUser(u, Nothing)
        CurrentUser = u
        _settings.CurrentEmployeeName = u.FullName
        _settings.Save()
        Return True
    End Function

    Public Function GetUsers() As List(Of UserAccount)
        Return _db.Root.Elements("User").Select(Function(e) FromElement(e)).OrderBy(Function(x) x.FullName).ToList()
    End Function

    Public Function UsersToDataTable(users As List(Of UserAccount)) As DataTable
        Dim t As New DataTable()
        t.Columns.Add("Id")
        t.Columns.Add("الاسم")
        t.Columns.Add("اسم المستخدم")
        t.Columns.Add("الدور")
        t.Columns.Add("نشط")
        t.Columns.Add("آخر دخول")
        For Each u In users
            t.Rows.Add(u.Id, u.FullName, u.UserName, u.RoleName, If(u.IsActive, "نعم", "لا"), If(u.LastLoginAt = DateTime.MinValue, "-", u.LastLoginAt.ToString("yyyy/MM/dd HH:mm")))
        Next
        Return t
    End Function

    Public Function GetUserById(id As String) As UserAccount
        If String.IsNullOrWhiteSpace(id) Then Return Nothing
        Dim e = _db.Root.Elements("User").FirstOrDefault(Function(x) CStr(x.Attribute("Id")) = id)
        If e Is Nothing Then Return Nothing
        Return FromElement(e)
    End Function

    Public Sub SaveUser(user As UserAccount, Optional newPassword As String = Nothing)
        If user Is Nothing Then Return
        If String.IsNullOrWhiteSpace(user.Id) Then user.Id = Guid.NewGuid().ToString("N")
        user.UserName = If(user.UserName, "").Trim()
        user.FullName = If(user.FullName, "").Trim()
        If Not String.IsNullOrWhiteSpace(newPassword) Then SetPassword(user, newPassword)

        Dim users = _db.Root.Elements("User")
        Dim duplicated = users.Any(Function(e) CStr(e.Attribute("Id")) <> user.Id AndAlso String.Equals(ValueOf(e, "UserName"), user.UserName, StringComparison.OrdinalIgnoreCase))
        If duplicated Then Throw New InvalidOperationException("اسم المستخدم موجود مسبقاً.")

        Dim existing = _db.Root.Elements("User").FirstOrDefault(Function(e) CStr(e.Attribute("Id")) = user.Id)
        If existing IsNot Nothing Then existing.Remove()
        _db.Root.Add(ToElement(user))
        SaveDb()
    End Sub

    Public Sub DeactivateUser(id As String)
        Dim u = GetUserById(id)
        If u Is Nothing Then Return
        If String.Equals(u.UserName, "admin", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("لا يمكن تعطيل حساب admin الافتراضي.")
        u.IsActive = False
        SaveUser(u, Nothing)
    End Sub

    Public Function HasPermission(permissionName As String) As Boolean
        If CurrentUser Is Nothing Then Return False
        If CurrentUser.RoleName = "مدير النظام" Then Return True
        Select Case CurrentUser.RoleName
            Case "مدير الأرشيف"
                Return New String() {"view", "preview", "search", "import", "edit", "export", "scan", "workflow", "audit", "manage_categories", "ai", "maintenance"}.Contains(permissionName)
            Case "موظف أرشفة"
                Return New String() {"view", "preview", "search", "import", "edit", "export", "scan", "workflow", "ai"}.Contains(permissionName)
            Case "مراجع"
                Return New String() {"view", "preview", "search", "export", "audit", "workflow", "ai"}.Contains(permissionName)
            Case "قارئ"
                Return New String() {"view", "preview", "search"}.Contains(permissionName)
        End Select
        Return False
    End Function

    Public Shared Function AvailableRoles() As String()
        Return New String() {"مدير النظام", "مدير الأرشيف", "موظف أرشفة", "مراجع", "قارئ"}
    End Function

    Private Shared Sub SetPassword(user As UserAccount, password As String)
        user.Salt = CreateSalt()
        user.PasswordHash = PasswordHashService.Hash(password, user.Salt)
    End Sub

    Private Shared Function CreateSalt() As String
        Dim bytes(15) As Byte
        Using rng As New RNGCryptoServiceProvider()
            rng.GetBytes(bytes)
        End Using
        Return Convert.ToBase64String(bytes)
    End Function


    Private Shared Function ToElement(u As UserAccount) As XElement
        Return New XElement("User",
            New XAttribute("Id", u.Id),
            New XElement("FullName", If(u.FullName, "")),
            New XElement("UserName", If(u.UserName, "")),
            New XElement("PasswordHash", If(u.PasswordHash, "")),
            New XElement("Salt", If(u.Salt, "")),
            New XElement("RoleName", If(u.RoleName, "")),
            New XElement("IsActive", u.IsActive.ToString()),
            New XElement("CreatedAt", u.CreatedAt.ToString("o")),
            New XElement("LastLoginAt", u.LastLoginAt.ToString("o"))
        )
    End Function

    Private Shared Function FromElement(e As XElement) As UserAccount
        Dim u As New UserAccount()
        u.Id = CStr(e.Attribute("Id"))
        u.FullName = ValueOf(e, "FullName")
        u.UserName = ValueOf(e, "UserName")
        u.PasswordHash = ValueOf(e, "PasswordHash")
        u.Salt = ValueOf(e, "Salt")
        u.RoleName = ValueOf(e, "RoleName")
        Boolean.TryParse(ValueOf(e, "IsActive", "True"), u.IsActive)
        u.CreatedAt = BasiraDates.ParseStored(ValueOf(e, "CreatedAt"), DateTime.MinValue)
        u.LastLoginAt = BasiraDates.ParseStored(ValueOf(e, "LastLoginAt"), DateTime.MinValue)
        Return u
    End Function

    Private Shared Function ValueOf(e As XElement, name As String, Optional fallback As String = "") As String
        If e Is Nothing OrElse e.Element(name) Is Nothing Then Return fallback
        Return e.Element(name).Value
    End Function
End Class
