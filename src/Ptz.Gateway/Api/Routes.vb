Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>سجلّ كل مجموعات المسارات.</summary>
    Public NotInheritable Class Routes
        Private Sub New()
        End Sub

        Public Shared Sub RegisterAll(api As ApiRegistry, svc As GatewayServices)
            AuthRoutes.Register(api, svc)
            SystemRoutes.Register(api, svc)
        End Sub
    End Class

End Namespace
