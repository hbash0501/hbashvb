Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Cameras
Imports Ptz.Core.Engine
Imports Ptz.Core.Isapi
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Ptz.Core.Vision

''' <summary>
''' المقدِّم الرئيسي لنافذة كاميرا: يبني المتحكم المؤمَّن والمحرك الذكي واللوحات ويدير دورة الحياة.
''' كل طراز كاميرا يرث هذه الفئة ويحدد ملفه التعريفي وضبطه الخاص.
''' </summary>
Public MustInherit Class CameraSessionPresenter
    Implements IDisposable

    Private ReadOnly _view As ICameraFormView
    Private ReadOnly _deps As CameraSessionDeps
    Private _cts As Threading.CancellationTokenSource
    Private _controller As SecuredCameraController
    Private _engine As SmartEngine
    Private _source As SnapshotFrameSource
    Private _ptz As PtzPresenter
    Private _image As ImagePresenter
    Private _smart As SmartPresenter
    Private _alarms As AlarmPresenter
    Private _video As VideoPresenter
    Private _alertTask As Task
    Private _positionTask As Task
    Private _frames As Integer
    Private _fpsWindowStart As DateTime = DateTime.UtcNow
    Private _started As Boolean

    Protected Sub New(view As ICameraFormView, deps As CameraSessionDeps)
        _view = view
        _deps = deps
        AddHandler view.ReconnectRequested, AddressOf OnReconnect
    End Sub

    ''' <summary>الملف التعريفي للطراز.</summary>
    Protected MustOverride ReadOnly Property Profile As CameraProfile

    ''' <summary>ضبط محرك الذكاء الخاص بالطراز (مثل معدل الإطارات).</summary>
    Protected Overridable Sub ConfigureEngine(options As SmartEngineOptions)
    End Sub

    Public ReadOnly Property Controller As SecuredCameraController
        Get
            Return _controller
        End Get
    End Property

    Public ReadOnly Property Engine As SmartEngine
        Get
            Return _engine
        End Get
    End Property

    Public Async Function StartAsync() As Task
        If _started Then Return
        _started = True
        _cts = New Threading.CancellationTokenSource()
        Dim ct As Threading.CancellationToken = _cts.Token
        Dim failed As Boolean = False
        Try
            _view.SetTitle(Profile.DisplayName & If(_deps.CameraName <> "", " — " & _deps.CameraName, ""))
            _view.SetUserInfo(_deps.Session.UserName, _deps.Session.Role.ToString())
            _view.ApplyPermissions(_deps.Session.Permissions)
            _view.SetConnectionState(False, UiText.Connecting)

            Dim inner As ICameraController = CameraControllerFactory.Create(Profile, _deps.Connection, _deps.HttpHandler)
            _controller = New SecuredCameraController(inner, _deps.Session, _deps.Access, _deps.Audit, _deps.Clock)

            Dim options As New SmartEngineOptions With {.CameraName = _deps.CameraName}
            ConfigureEngine(options)
            _source = New SnapshotFrameSource(_controller, _deps.Decoder, _deps.Clock)
            Dim recorder As New TrajectoryRecorder(IO.Path.Combine(_deps.DataDirectory, "trajectories"), _deps.CameraName)
            _engine = New SmartEngine(options, _source, _controller.AsAutoTrackMover(), _deps.Bus, recorder, New HeatMap(), _deps.Clock)

            Dim ctx As New CameraContext(_deps, _controller, _engine, _view, ct)
            _ptz = New PtzPresenter(ctx, _view.Ptz)
            _image = New ImagePresenter(ctx, _view.Image)
            _video = New VideoPresenter(ctx, _view.Video)
            _smart = New SmartPresenter(ctx, _view.Smart, _view.Video)
            _alarms = New AlarmPresenter(ctx, _view.Alarms)

            Dim info As DeviceInfo = Await _controller.ConnectAsync(ct)
            _view.ShowDeviceInfo(info)
            _view.SetConnectionState(True, UiText.Connected)
            _ptz.Initialize()
            _image.Initialize()
            _smart.Initialize()

            AddHandler _source.FrameCaptured, AddressOf OnJpeg
            AddHandler _engine.FrameProcessed, AddressOf OnResult
            AddHandler _engine.Faulted, AddressOf OnFault
            _engine.Start()
            _positionTask = Task.Run(Function() PositionLoopAsync(ct))
            If (_controller.Features And CameraFeatures.AlertStream) <> 0 Then _alertTask = Task.Run(Function() AlertLoopAsync(ct))
        Catch ex As OperationCanceledException
            _started = False
        Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is AccessDeniedException OrElse
                                  TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException
            _view.SetConnectionState(False, UiText.Disconnected)
            _view.ShowError(If(TypeOf ex Is AccessDeniedException, UiText.NoPermission, ex.Message))
            failed = True
        End Try
        If failed Then
            _started = False
            Await TeardownAsync()
        End If
    End Function

    Public Async Function StopAsync() As Task
        Await TeardownAsync()
        _started = False
        _view.SetConnectionState(False, UiText.Disconnected)
    End Function

    Private Async Function TeardownAsync() As Task
        If _cts IsNot Nothing Then _cts.Cancel()
        If _engine IsNot Nothing Then
            RemoveHandler _engine.FrameProcessed, AddressOf OnResult
            RemoveHandler _engine.Faulted, AddressOf OnFault
            Await _engine.StopAsync()
        End If
        If _source IsNot Nothing Then RemoveHandler _source.FrameCaptured, AddressOf OnJpeg
        Try
            If _positionTask IsNot Nothing Then Await _positionTask
            If _alertTask IsNot Nothing Then Await _alertTask
        Catch ex As OperationCanceledException
        End Try
        _positionTask = Nothing
        _alertTask = Nothing
        _alarms?.Dispose()
        _alarms = Nothing
        _controller?.Dispose()
        _controller = Nothing
        _engine = Nothing
        _cts?.Dispose()
        _cts = Nothing
    End Function

    Private Async Sub OnReconnect()
        Await StopAsync()
        Await StartAsync()
    End Sub

    ' ---- أحداث الخلفية (تُمرَّر لخيط الواجهة عبر Dispatcher داخل المقدِّمات) ----
    Private Sub OnJpeg(jpeg As Byte())
        _video?.OnJpeg(jpeg)
    End Sub

    Private Sub OnResult(result As SmartFrameResult)
        _video?.OnResult(result)
        _smart?.OnFrame(result)
        _frames += 1
        Dim now As DateTime = DateTime.UtcNow
        Dim secs As Double = (now - _fpsWindowStart).TotalSeconds
        If secs >= 2.0 Then
            Dim fps As Double = _frames / secs
            Dim tracks As Integer = result.Tracks.Count
            _frames = 0
            _fpsWindowStart = now
            _deps.Dispatcher.Post(Sub() _view.SetFrameStats(fps, tracks))
        End If
    End Sub

    Private Sub OnFault(ex As Exception, count As Integer)
        Dim ade As AccessDeniedException = TryCast(ex, AccessDeniedException)
        _deps.Dispatcher.Post(
            Sub()
                If ade IsNot Nothing AndAlso ade.IsSessionExpired Then
                    _view.SetConnectionState(False, UiText.SessionExpired)
                Else
                    _view.SetConnectionState(False, $"{UiText.Disconnected} ({count}) — {ex.Message}")
                End If
            End Sub)
    End Sub

    Private Async Function PositionLoopAsync(ct As Threading.CancellationToken) As Task
        Try
            While Not ct.IsCancellationRequested
                Await Task.Delay(TimeSpan.FromSeconds(2), ct)
                If _ptz IsNot Nothing Then Await _ptz.RefreshPositionAsync()
            End While
        Catch ex As OperationCanceledException
        End Try
    End Function

    ''' <summary>يستورد أحداث الكاميرا الذكية الأصلية (VMD/خط/اقتحام...) إلى ناقل الإنذارات.</summary>
    Private Async Function AlertLoopAsync(ct As Threading.CancellationToken) As Task
        Using client As New IsapiClient(_deps.Connection, _deps.HttpHandler)
            Dim stream As New AlertStreamClient(client)
            Await stream.RunAsync(
                Sub(ev)
                    _deps.Bus.Publish(New AlarmEvent With {
                        .Camera = _deps.CameraName,
                        .Kind = "Device:" & ev.EventType,
                        .Severity = AlarmSeverity.Warning,
                        .Message = $"حدث من الكاميرا: {ev.EventType} {ev.Description}".Trim()})
                End Sub, Nothing, ct)
        End Using
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _cts IsNot Nothing Then
            Try
                TeardownAsync().GetAwaiter().GetResult()
            Catch ex As Exception When TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is ObjectDisposedException
            End Try
        End If
    End Sub
End Class

''' <summary>مقدِّم الكاميرا DS-2SE7C432MW-AEB (4MP).</summary>
Public NotInheritable Class Ds2Se7c432mwPresenter
    Inherits CameraSessionPresenter

    Public Sub New(view As ICameraFormView, deps As CameraSessionDeps)
        MyBase.New(view, deps)
    End Sub

    Protected Overrides ReadOnly Property Profile As CameraProfile
        Get
            Return CameraProfiles.Ds2Se7c432mwAeb
        End Get
    End Property

    Protected Overrides Sub ConfigureEngine(options As SmartEngineOptions)
        ' لقطات 4MP أكبر حجماً: معدل أقل يحمي الشبكة والمعالج؛ ضجيج أقل (DarkFighter) يسمح بعتبة أدق.
        options.TargetFps = 4.0
        options.Motion.Subtractor.ThresholdSigma = 2.8F
    End Sub
End Class

''' <summary>مقدِّم الكاميرا DS-2DE7232IW-AE (2MP).</summary>
Public NotInheritable Class Ds2De7232iwPresenter
    Inherits CameraSessionPresenter

    Public Sub New(view As ICameraFormView, deps As CameraSessionDeps)
        MyBase.New(view, deps)
    End Sub

    Protected Overrides ReadOnly Property Profile As CameraProfile
        Get
            Return CameraProfiles.Ds2De7232iwAe
        End Get
    End Property

    Protected Overrides Sub ConfigureEngine(options As SmartEngineOptions)
        options.TargetFps = 5.0
        options.Motion.Subtractor.ThresholdSigma = 3.0F
    End Sub
End Class
