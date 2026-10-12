Imports System.Drawing
Imports System.Windows.Forms
Imports Ptz.App.Controls
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Presentation

Namespace Views

    ''' <summary>
    ''' يربط عناصر نافذة كاميرا (اللوحات وشريط الحالة) بعقد <see cref="ICameraFormView"/> دون أن تتضمن
    ''' النافذة نفسها أي منطق. مشترك بين نافذتي الكاميرتين لتفادي تكرار كود الربط.
    ''' </summary>
    Friend NotInheritable Class CameraFormBinder
        Implements ICameraFormView

        Private ReadOnly _form As Form
        Private ReadOnly _ptz As PtzPanel
        Private ReadOnly _image As ImagePanel
        Private ReadOnly _smart As SmartPanel
        Private ReadOnly _alarms As AlarmPanel
        Private ReadOnly _video As VideoView
        Private ReadOnly _conn As ToolStripStatusLabel
        Private ReadOnly _user As ToolStripStatusLabel
        Private ReadOnly _device As ToolStripStatusLabel
        Private ReadOnly _fps As ToolStripStatusLabel
        Private ReadOnly _reconnect As ToolStripStatusLabel
        Private ReadOnly _message As ToolStripStatusLabel
        Private ReadOnly _clearTimer As Timer
        Private ReadOnly _dispatcher As IUiDispatcher

        Public Event ReconnectRequested As Action Implements ICameraFormView.ReconnectRequested

        Public Sub New(form As Form, ptz As PtzPanel, image As ImagePanel, smart As SmartPanel, alarms As AlarmPanel, video As VideoView,
                       conn As ToolStripStatusLabel, user As ToolStripStatusLabel, device As ToolStripStatusLabel,
                       fps As ToolStripStatusLabel, reconnect As ToolStripStatusLabel, message As ToolStripStatusLabel)
            _form = form
            _ptz = ptz
            _image = image
            _smart = smart
            _alarms = alarms
            _video = video
            _conn = conn
            _user = user
            _device = device
            _fps = fps
            _reconnect = reconnect
            _message = message
            _dispatcher = New WinFormsDispatcher(form)
            _clearTimer = New Timer With {.Interval = 8000}
            AddHandler _clearTimer.Tick, Sub()
                                             _clearTimer.Stop()
                                             _message.Text = ""
                                         End Sub
            AddHandler reconnect.Click, Sub() RaiseEvent ReconnectRequested()
        End Sub

        Public ReadOnly Property Dispatcher As IUiDispatcher Implements ICameraFormView.Dispatcher
            Get
                Return _dispatcher
            End Get
        End Property

        Public ReadOnly Property Ptz As IPtzPanelView Implements ICameraFormView.Ptz
            Get
                Return _ptz
            End Get
        End Property

        Public ReadOnly Property Image As IImagePanelView Implements ICameraFormView.Image
            Get
                Return _image
            End Get
        End Property

        Public ReadOnly Property Smart As ISmartPanelView Implements ICameraFormView.Smart
            Get
                Return _smart
            End Get
        End Property

        Public ReadOnly Property Alarms As IAlarmPanelView Implements ICameraFormView.Alarms
            Get
                Return _alarms
            End Get
        End Property

        Public ReadOnly Property Video As IVideoView Implements ICameraFormView.Video
            Get
                Return _video
            End Get
        End Property

        Public Sub SetTitle(text As String) Implements ICameraFormView.SetTitle
            _form.Text = text
        End Sub

        Public Sub SetConnectionState(connected As Boolean, text As String) Implements ICameraFormView.SetConnectionState
            _conn.Text = text
            _conn.ForeColor = If(connected, Theme.Ok, Theme.Danger)
            _reconnect.Visible = Not connected AndAlso text <> UiText.Connecting
        End Sub

        Public Sub ShowDeviceInfo(info As DeviceInfo) Implements ICameraFormView.ShowDeviceInfo
            _device.Text = $"{info.Model}  SN:{info.SerialNumber}  FW:{info.FirmwareVersion}"
        End Sub

        Public Sub SetUserInfo(userName As String, role As String) Implements ICameraFormView.SetUserInfo
            _user.Text = $"{userName} ({role})"
        End Sub

        Public Sub SetFrameStats(fps As Double, trackCount As Integer) Implements ICameraFormView.SetFrameStats
            _fps.Text = $"{fps:F1} إطار/ث · أجسام {trackCount}"
        End Sub

        ''' <summary>الفرض الفعلي في الطبقة الأمنية؛ هنا نعطّل ما لا يملكه المستخدم لتوضيح الواجهة فقط.</summary>
        Public Sub ApplyPermissions(permissions As Permission) Implements ICameraFormView.ApplyPermissions
            _ptz.Enabled = (permissions And (Permission.PtzControl Or Permission.PresetUse)) <> 0
            _image.Enabled = (permissions And Permission.ImageControl) <> 0
            _smart.Enabled = (permissions And Permission.SmartView) <> 0
        End Sub

        Public Sub ShowError(message As String) Implements IMessageSink.ShowError
            Show(message, Theme.Danger)
        End Sub

        Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
            Show(message, Theme.Ok)
        End Sub

        Private Sub Show(message As String, color As Color)
            _dispatcher.Post(Sub()
                                 _message.ForeColor = color
                                 _message.Text = message
                                 _clearTimer.Stop()
                                 _clearTimer.Start()
                             End Sub)
        End Sub
    End Class

End Namespace
