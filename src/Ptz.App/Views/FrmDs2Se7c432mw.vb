Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Presentation

Namespace Views

    ''' <summary>نافذة التحكم بالكاميرا DS-2SE7C432MW-AEB. التصميم في ملف Designer والربط في CameraFormBinder والمنطق في المقدِّم.</summary>
    Public Class FrmDs2Se7c432mw
        Private ReadOnly _binder As CameraFormBinder
        Private ReadOnly _presenter As Ds2Se7c432mwPresenter

        Public Sub New(deps As CameraSessionDeps)
            InitializeComponent()
            Theme.Apply(Me)
            deps.Dispatcher = New WinFormsDispatcher(Me)
            _binder = New CameraFormBinder(Me, ptzPanel, imagePanel, smartPanel, alarmPanel, videoView,
                                           lblConn, lblUser, lblDevice, lblFps, lblReconnect, lblMessage)
            _presenter = New Ds2Se7c432mwPresenter(_binder, deps)
        End Sub

        Private Async Sub FrmDs2Se7c432mw_Shown(sender As Object, e As EventArgs) Handles Me.Shown
            Await _presenter.StartAsync()
        End Sub

        Private Sub FrmDs2Se7c432mw_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
            _presenter.Dispose()
        End Sub
    End Class

End Namespace
