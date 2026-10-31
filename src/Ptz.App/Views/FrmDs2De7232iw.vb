Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Presentation

Namespace Views

    ''' <summary>نافذة التحكم بالكاميرا DS-2DE7232IW-AE. التصميم في ملف Designer والربط في CameraFormBinder والمنطق في المقدِّم.</summary>
    Public Class FrmDs2De7232iw
        Private ReadOnly _binder As CameraFormBinder
        Private ReadOnly _presenter As Ds2De7232iwPresenter

        Public Sub New(deps As CameraSessionDeps)
            InitializeComponent()
            Theme.Apply(Me)
            deps.Dispatcher = New WinFormsDispatcher(Me)
            _binder = New CameraFormBinder(Me, ptzPanel, imagePanel, smartPanel, alarmPanel, videoView,
                                           lblConn, lblUser, lblDevice, lblFps, lblReconnect, lblMessage)
            _presenter = New Ds2De7232iwPresenter(_binder, deps)
        End Sub

        Private Async Sub FrmDs2De7232iw_Shown(sender As Object, e As EventArgs) Handles Me.Shown
            Await _presenter.StartAsync()
        End Sub

        Private Sub FrmDs2De7232iw_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
            _presenter.Dispose()
        End Sub
    End Class

End Namespace
