Imports Ptz.Core.Alarms
Imports Ptz.Core.Security
Imports Ptz.Presentation

Namespace Infrastructure

    ''' <summary>جذر التركيب (Composition Root): ينشئ الخدمات المشتركة مرة واحدة.</summary>
    Public NotInheritable Class AppServices
        Public ReadOnly Property DataDirectory As String
        Public ReadOnly Property Audit As FileAuditLog
        Public ReadOnly Property Users As UserStore
        Public ReadOnly Property Vault As CredentialVault
        Public ReadOnly Property Cameras As CameraConfigStore
        Public ReadOnly Property Bus As AlarmBus
        Public ReadOnly Property Access As AccessControl
        Public ReadOnly Property Decoder As GdiFrameDecoder

        Public Sub New(Optional dataDirectory As String = Nothing)
            Me.DataDirectory = If(dataDirectory, IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PtzPlatform"))
            IO.Directory.CreateDirectory(Me.DataDirectory)
            Dim keys As New DpapiKeyProvider(IO.Path.Combine(Me.DataDirectory, "master.key"))
            Dim master As Byte() = keys.GetMasterKey()
            Audit = New FileAuditLog(IO.Path.Combine(Me.DataDirectory, "audit.jsonl"), KeyDerivation.Derive(master, "audit"))
            Users = New UserStore(IO.Path.Combine(Me.DataDirectory, "users.json"), Audit)
            Vault = New CredentialVault(keys)
            Cameras = New CameraConfigStore(IO.Path.Combine(Me.DataDirectory, "cameras.json"), Vault)
            Bus = New AlarmBus()
            Bus.AddSink(New FileAlarmSink(IO.Path.Combine(Me.DataDirectory, "alarms.jsonl")))
            Access = New AccessControl()
            Decoder = New GdiFrameDecoder()
        End Sub
    End Class

End Namespace
