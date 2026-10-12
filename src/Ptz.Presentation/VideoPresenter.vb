Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Security

''' <summary>لوحة الفيديو: عرض اللقطات + التراكبات، وتحويل نقرات المستخدم إلى أوامر (تكبير على منطقة).</summary>
Public NotInheritable Class VideoPresenter
    Inherits PanelPresenterBase

    Private ReadOnly _view As IVideoView

    Public Sub New(context As CameraContext, view As IVideoView)
        MyBase.New(context)
        _view = view
        AddHandler view.AreaSelected, AddressOf OnAreaSelected
    End Sub

    Private Async Sub OnAreaSelected(area As NormRect)
        Context.Engine.NotifyCameraMotion(1.5)
        Await GuardAsync(Function() Context.Controller.ZoomToAreaAsync(area, Ct))
    End Sub

    Public Sub OnJpeg(jpeg As Byte())
        Context.Ui.Post(Sub() _view.ShowFrame(jpeg))
    End Sub

    Public Sub OnResult(result As SmartFrameResult)
        Context.Ui.Post(Sub() _view.ShowOverlay(result))
    End Sub
End Class
