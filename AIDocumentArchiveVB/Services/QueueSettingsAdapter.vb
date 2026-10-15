Partial Public Class ProcessingJobQueueService
    Public Sub New(settings As AppSettings)
        Me.New(settings.BaseDataFolder)
    End Sub
End Class
