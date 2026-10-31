Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>سجلّ كل مجموعات المسارات.</summary>
    Public NotInheritable Class Routes
        Private Sub New()
        End Sub

        Public Shared Sub RegisterAll(api As ApiRegistry, svc As GatewayServices)
            AuthRoutes.Register(api, svc)
            SystemRoutes.Register(api, svc)
            CameraRoutes.Register(api, svc)
            SmartRoutes.Register(api, svc)
            AlarmAuditRoutes.Register(api, svc)
            StreamRoutes.Register(api, svc)
            WebRoutes.Register(api, svc)
        End Sub
    End Class

End Namespace
