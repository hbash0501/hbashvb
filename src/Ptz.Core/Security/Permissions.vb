Namespace Security

    ''' <summary>صلاحيات دقيقة على مستوى الوظيفة.</summary>
    <Flags>
    Public Enum Permission
        None = 0
        ViewLive = 1 << 0
        PtzControl = 1 << 1
        PresetUse = 1 << 2
        PresetManage = 1 << 3
        ImageControl = 1 << 4
        SmartView = 1 << 5
        SmartConfigure = 1 << 6
        AutoTrack = 1 << 7
        AckAlarms = 1 << 8
        DeviceMaintenance = 1 << 9
        ManageConnections = 1 << 10
        ManageUsers = 1 << 11
        ViewAudit = 1 << 12
        All = (1 << 13) - 1
    End Enum

    ''' <summary>مستويات المستخدمين (متدرجة).</summary>
    Public Enum Role
        Viewer = 1
        [Operator] = 2
        Supervisor = 3
        Administrator = 4
    End Enum

    Public NotInheritable Class RolePermissions
        Private Sub New()
        End Sub

        Public Shared Function [For](r As Role) As Permission
            Dim viewer As Permission = Permission.ViewLive Or Permission.SmartView
            Dim op As Permission = viewer Or Permission.PtzControl Or Permission.PresetUse Or Permission.AutoTrack Or Permission.AckAlarms
            Dim sup As Permission = op Or Permission.PresetManage Or Permission.ImageControl Or Permission.SmartConfigure Or Permission.ViewAudit
            Select Case r
                Case Role.Viewer : Return viewer
                Case Role.Operator : Return op
                Case Role.Supervisor : Return sup
                Case Role.Administrator : Return Permission.All
                Case Else : Return Permission.None
            End Select
        End Function
    End Class

    Public Class AccessDeniedException
        Inherits Exception
        Public ReadOnly Property Required As Permission
        ''' <summary>true إذا كان السبب انتهاء الجلسة (لا نقص الصلاحية).</summary>
        Public ReadOnly Property IsSessionExpired As Boolean

        Public Sub New(required As Permission, message As String, Optional isSessionExpired As Boolean = False)
            MyBase.New(message)
            Me.Required = required
            Me.IsSessionExpired = isSessionExpired
        End Sub
    End Class

    Public Class AuthenticationException
        Inherits Exception
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    ''' <summary>جلسة مستخدم موثّقة، تنتهي بعد خمول المدة المحددة.</summary>
    Public NotInheritable Class UserSession
        Private _lastActivityUtc As DateTime

        Public ReadOnly Property UserName As String
        Public ReadOnly Property Role As Role
        Public ReadOnly Property Permissions As Permission
        Public ReadOnly Property SessionId As Guid = Guid.NewGuid()
        Public ReadOnly Property IdleTimeout As TimeSpan

        Public Sub New(userName As String, role As Role, nowUtc As DateTime, Optional idleTimeout As TimeSpan = Nothing)
            Me.UserName = userName
            Me.Role = role
            Permissions = RolePermissions.For(role)
            Me.IdleTimeout = If(idleTimeout = TimeSpan.Zero, TimeSpan.FromMinutes(15), idleTimeout)
            _lastActivityUtc = nowUtc
        End Sub

        Public Function IsExpired(nowUtc As DateTime) As Boolean
            Return nowUtc - _lastActivityUtc > IdleTimeout
        End Function

        Public Sub Touch(nowUtc As DateTime)
            _lastActivityUtc = nowUtc
        End Sub

        Public Function Has(p As Permission) As Boolean
            Return (Permissions And p) = p
        End Function
    End Class

    ''' <summary>نقطة فرض الصلاحيات المركزية.</summary>
    Public NotInheritable Class AccessControl
        Private ReadOnly _clock As Abstractions.IClock

        Public Sub New(Optional clock As Abstractions.IClock = Nothing)
            _clock = If(clock, Abstractions.SystemClock.Instance)
        End Sub

        ''' <summary>
        ''' يرمي AccessDeniedException إن انتهت الجلسة أو لم تتوفر الصلاحية.
        ''' touch = False للعمليات الخلفية (استطلاع الفيديو) كي لا تمنع انتهاء الجلسة بالخمول.
        ''' </summary>
        Public Sub Demand(session As UserSession, required As Permission, Optional touch As Boolean = True)
            If session Is Nothing Then Throw New AccessDeniedException(required, "لا توجد جلسة مصادقة.")
            Dim now As DateTime = _clock.UtcNow
            If session.IsExpired(now) Then Throw New AccessDeniedException(required, "انتهت الجلسة بسبب الخمول.", True)
            If Not session.Has(required) Then
                Throw New AccessDeniedException(required, $"الصلاحية {required} غير متاحة للدور {session.Role}.")
            End If
            If touch Then session.Touch(now)
        End Sub
    End Class

End Namespace
