Imports Ptz.Core.Models

''' <summary>لوحة الصورة: تعريض، IR، WDR، توازن الأبيض، الإضاءة، التركيز، الممسحة...</summary>
Public NotInheritable Class ImagePresenter
    Inherits PanelPresenterBase

    Private ReadOnly _view As IImagePanelView

    Public Sub New(context As CameraContext, view As IImagePanelView)
        MyBase.New(context)
        _view = view
        AddHandler view.RefreshRequested, Sub() Refresh()
        AddHandler view.ApplyRequested, AddressOf OnApply
        AddHandler view.FocusRequested, Sub(d, s) Run(Function() Context.Controller.MoveFocusAsync(d, s, Ct))
        AddHandler view.IrisRequested, Sub(d, s) Run(Function() Context.Controller.MoveIrisAsync(d, s, Ct))
        AddHandler view.AuxRequested, Sub(k, on_) Run(Function() Context.Controller.SetAuxAsync(k, on_, Ct))
    End Sub

    Public Sub Initialize()
        _view.SetCapabilities(Context.Controller.Features)
        Refresh()
    End Sub

    Private Async Sub Run(op As Func(Of Task))
        Await GuardAsync(op)
    End Sub

    Private Async Sub Refresh()
        Dim s As ImageSettings = Nothing
        If Await GuardAsync(Async Function()
                                s = Await Context.Controller.GetImageSettingsAsync(Ct)
                            End Function) Then
            _view.ShowSettings(s)
        End If
    End Sub

    Private Async Sub OnApply(settings As ImageSettings)
        If Await GuardAsync(Function() Context.Controller.ApplyImageSettingsAsync(settings, Ct), UiText.Saved) Then
            ' تغيّر الصورة (IR/تعريض) يغيّر الإضاءة عالمياً: نعيد تعلّم الخلفية بدل إنذار كاذب.
            Context.Engine.NotifyCameraMotion(1.5)
        End If
    End Sub
End Class
