Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Diagnostics
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports AForge.Video
Imports AForge.Video.DirectShow

Partial Public Class CameraImportForm
    Private _devices As FilterInfoCollection
    Private _videoSource As VideoCaptureDevice
    Private _currentFrame As Bitmap
    Private ReadOnly _frameLock As New Object()
    Private _captureFolder As String
    Private _selectedSourcePath As String = ""

    Public ReadOnly Property SelectedSourcePath As String
        Get
            Return _selectedSourcePath
        End Get
    End Property

    Public ReadOnly Property CapturedCount As Integer
        Get
            Return lstCaptures.Items.Count
        End Get
    End Property

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub CameraImportForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        _captureFolder = Path.Combine(Path.GetTempPath(), "AIDocArchiveCamera", DateTime.Now.ToString("yyyyMMdd-HHmmss"))
        Directory.CreateDirectory(_captureFolder)
        txtSaveFolder.Text = _captureFolder
        LoadDevices()
        SetStatus("اختر الكاميرا والدقة ثم اضغط تشغيل. يمكن ضبط الخصائص من لوحة التحكم أو من صفحة إعدادات الكاميرا الأصلية.")
    End Sub

    Private Sub CameraImportForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        StopCamera()
        If picturePreview.Image IsNot Nothing Then
            Dim oldImage = picturePreview.Image
            picturePreview.Image = Nothing
            oldImage.Dispose()
        End If
        SyncLock _frameLock
            If _currentFrame IsNot Nothing Then
                _currentFrame.Dispose()
                _currentFrame = Nothing
            End If
        End SyncLock
    End Sub

    Private Sub LoadDevices()
        cboCameras.Items.Clear()
        cboResolution.Items.Clear()
        Try
            _devices = New FilterInfoCollection(FilterCategory.VideoInputDevice)
            For Each info As FilterInfo In _devices
                cboCameras.Items.Add(info.Name)
            Next
            If cboCameras.Items.Count > 0 Then
                cboCameras.SelectedIndex = 0
            Else
                SetStatus("لم يتم العثور على كاميرا. تأكد من تعريف الكاميرا وسماح ويندوز للتطبيقات باستخدامها.")
            End If
        Catch ex As Exception
            SetStatus("تعذر قراءة الكاميرات: " & ex.Message)
        End Try
    End Sub

    Private Sub cboCameras_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboCameras.SelectedIndexChanged
        LoadResolutionsForSelectedCamera()
    End Sub

    Private Sub LoadResolutionsForSelectedCamera()
        cboResolution.Items.Clear()
        If _devices Is Nothing OrElse cboCameras.SelectedIndex < 0 OrElse cboCameras.SelectedIndex >= _devices.Count Then Return
        Try
            Dim deviceInfo = _devices(cboCameras.SelectedIndex)
            Dim tempDevice As New VideoCaptureDevice(deviceInfo.MonikerString)
            For Each cap As VideoCapabilities In tempDevice.VideoCapabilities
                cboResolution.Items.Add(New VideoCapabilityItem(cap))
            Next
            If cboResolution.Items.Count > 0 Then cboResolution.SelectedIndex = 0
        Catch ex As Exception
            SetStatus("تعذر قراءة دقات الكاميرا: " & ex.Message)
        End Try
    End Sub

    Private Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        LoadDevices()
    End Sub

    Private Sub btnStart_Click(sender As Object, e As EventArgs) Handles btnStart.Click
        StartCamera()
    End Sub

    Private Sub btnStop_Click(sender As Object, e As EventArgs) Handles btnStop.Click
        StopCamera()
    End Sub

    Private Sub StartCamera()
        If _devices Is Nothing OrElse cboCameras.SelectedIndex < 0 OrElse cboCameras.SelectedIndex >= _devices.Count Then
            MessageBox.Show("اختر كاميرا أولاً.", "الكاميرا", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        StopCamera()
        Try
            Dim info = _devices(cboCameras.SelectedIndex)
            _videoSource = New VideoCaptureDevice(info.MonikerString)
            If cboResolution.SelectedItem IsNot Nothing Then
                Dim selectedCap = DirectCast(cboResolution.SelectedItem, VideoCapabilityItem).Capability
                _videoSource.VideoResolution = selectedCap
            End If
            AddHandler _videoSource.NewFrame, AddressOf OnNewFrame
            _videoSource.Start()
            btnStart.Enabled = False
            btnStop.Enabled = True
            btnCapture.Enabled = True
            btnNativeSettings.Enabled = True
            btnApplyProps.Enabled = True
            ConfigureCameraControls()
            SetStatus("تم تشغيل الكاميرا: " & info.Name)
        Catch ex As Exception
            SetStatus("تعذر تشغيل الكاميرا: " & ex.Message)
            btnStart.Enabled = True
        End Try
    End Sub

    Private Sub StopCamera()
        Try
            If _videoSource IsNot Nothing Then
                RemoveHandler _videoSource.NewFrame, AddressOf OnNewFrame
                If _videoSource.IsRunning Then
                    _videoSource.SignalToStop()
                    _videoSource.WaitForStop()
                End If
                _videoSource = Nothing
            End If
        Catch
        End Try
        btnStart.Enabled = True
        btnStop.Enabled = False
        btnCapture.Enabled = False
    End Sub

    Private Sub OnNewFrame(sender As Object, eventArgs As NewFrameEventArgs)
        Dim frameCopy As Bitmap = Nothing
        Try
            frameCopy = DirectCast(eventArgs.Frame.Clone(), Bitmap)
            SyncLock _frameLock
                If _currentFrame IsNot Nothing Then _currentFrame.Dispose()
                _currentFrame = DirectCast(frameCopy.Clone(), Bitmap)
            End SyncLock
            If Not Me.IsDisposed AndAlso picturePreview.IsHandleCreated Then
                Dim uiFrame = frameCopy
                frameCopy = Nothing
                Me.BeginInvoke(New MethodInvoker(Sub()
                                                     Dim oldImage = picturePreview.Image
                                                     picturePreview.Image = uiFrame
                                                     If oldImage IsNot Nothing Then oldImage.Dispose()
                                                 End Sub))
            End If
        Catch
            If frameCopy IsNot Nothing Then frameCopy.Dispose()
        End Try
    End Sub

    Private Sub btnCapture_Click(sender As Object, e As EventArgs) Handles btnCapture.Click
        Dim imageToSave As Bitmap = Nothing
        SyncLock _frameLock
            If _currentFrame IsNot Nothing Then imageToSave = DirectCast(_currentFrame.Clone(), Bitmap)
        End SyncLock

        If imageToSave Is Nothing Then
            MessageBox.Show("لا توجد صورة جاهزة بعد. انتظر ثانية ثم حاول مرة أخرى.", "الكاميرا", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Try
            Directory.CreateDirectory(_captureFolder)
            Dim filePath = Path.Combine(_captureFolder, "CAM-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & ".jpg")
            SaveJpeg(imageToSave, filePath, CInt(numJpegQuality.Value))
            lstCaptures.Items.Add(filePath)
            _selectedSourcePath = If(lstCaptures.Items.Count = 1, filePath, _captureFolder)
            SetStatus("تم التقاط صفحة جديدة. عدد الصفحات: " & lstCaptures.Items.Count.ToString())
        Catch ex As Exception
            MessageBox.Show(ex.Message, "حفظ لقطة الكاميرا", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            imageToSave.Dispose()
        End Try
    End Sub

    Private Sub btnRemoveCapture_Click(sender As Object, e As EventArgs) Handles btnRemoveCapture.Click
        If lstCaptures.SelectedIndex < 0 Then Return
        Dim filePath = Convert.ToString(lstCaptures.SelectedItem)
        lstCaptures.Items.RemoveAt(lstCaptures.SelectedIndex)
        Try
            If File.Exists(filePath) Then File.Delete(filePath)
        Catch
        End Try
        If lstCaptures.Items.Count = 0 Then
            _selectedSourcePath = ""
        ElseIf lstCaptures.Items.Count = 1 Then
            _selectedSourcePath = Convert.ToString(lstCaptures.Items(0))
        Else
            _selectedSourcePath = _captureFolder
        End If
        SetStatus("تم حذف اللقطة المحددة. عدد الصفحات: " & lstCaptures.Items.Count.ToString())
    End Sub

    Private Sub btnOpenFolder_Click(sender As Object, e As EventArgs) Handles btnOpenFolder.Click
        Try
            Directory.CreateDirectory(_captureFolder)
            Process.Start("explorer.exe", _captureFolder)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "فتح المجلد", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Try
    End Sub

    Private Sub btnNativeSettings_Click(sender As Object, e As EventArgs) Handles btnNativeSettings.Click
        Try
            If _videoSource IsNot Nothing Then
                _videoSource.DisplayPropertyPage(Me.Handle)
            End If
        Catch ex As Exception
            MessageBox.Show("الكاميرا لا توفر صفحة إعدادات أصلية أو تعذر فتحها." & Environment.NewLine & ex.Message, "إعدادات الكاميرا", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Try
    End Sub

    Private Sub btnCrossbar_Click(sender As Object, e As EventArgs) Handles btnCrossbar.Click
        Try
            If _videoSource IsNot Nothing AndAlso _videoSource.CheckIfCrossbarAvailable() Then
                _videoSource.DisplayCrossbarPropertyPage(Me.Handle)
            Else
                MessageBox.Show("هذه الكاميرا لا توفر إعدادات Crossbar.", "الكاميرا", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Crossbar", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Try
    End Sub

    Private Sub btnApplyProps_Click(sender As Object, e As EventArgs) Handles btnApplyProps.Click
        ApplyCurrentProperties()
    End Sub

    Private Sub btnAccept_Click(sender As Object, e As EventArgs) Handles btnAccept.Click
        If lstCaptures.Items.Count = 0 Then
            MessageBox.Show("التقط صفحة واحدة على الأقل قبل الاعتماد.", "اعتماد اللقطات", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If lstCaptures.Items.Count = 1 Then
            _selectedSourcePath = Convert.ToString(lstCaptures.Items(0))
        Else
            _selectedSourcePath = _captureFolder
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        DialogResult = DialogResult.Cancel
        Close()
    End Sub

    Private Sub ConfigureCameraControls()
        ConfigureVideoTrack(trackBrightness, DsVideoProcAmpProperty.Brightness, "السطوع")
        ConfigureVideoTrack(trackContrast, DsVideoProcAmpProperty.Contrast, "التباين")
        ConfigureVideoTrack(trackSharpness, DsVideoProcAmpProperty.Sharpness, "الحدة")
        ConfigureVideoTrack(trackWhiteBalance, DsVideoProcAmpProperty.WhiteBalance, "توازن الأبيض")
        ConfigureCameraTrack(trackExposure, DsCameraControlProperty.Exposure, "التعريض")
        ConfigureCameraTrack(trackFocus, DsCameraControlProperty.Focus, "التركيز")
        ConfigureCameraTrack(trackZoom, DsCameraControlProperty.Zoom, "التقريب")
    End Sub

    Private Sub ConfigureVideoTrack(track As TrackBar, prop As DsVideoProcAmpProperty, displayName As String)
        Dim filterObject As Object = Nothing
        Try
            filterObject = BindSelectedCameraFilter()
            Dim amp = TryCast(filterObject, IAMVideoProcAmp)
            If amp Is Nothing Then
                track.Enabled = False
                Return
            End If

            Dim minValue As Integer = 0, maxValue As Integer = 0, stepSize As Integer = 1, defaultValue As Integer = 0
            Dim flags As DsControlFlags = DsControlFlags.Manual
            Dim hr = amp.GetRange(prop, minValue, maxValue, stepSize, defaultValue, flags)
            If hr < 0 Then
                track.Enabled = False
                Return
            End If

            Dim currentValue As Integer = defaultValue
            Dim currentFlags As DsControlFlags = DsControlFlags.Manual
            Try
                If amp.Get(prop, currentValue, currentFlags) < 0 Then currentValue = defaultValue
            Catch
                currentValue = defaultValue
            End Try

            ConfigureTrack(track, minValue, maxValue, currentValue, stepSize)
        Catch
            track.Enabled = False
        Finally
            ReleaseComObject(filterObject)
        End Try
    End Sub

    Private Sub ConfigureCameraTrack(track As TrackBar, prop As DsCameraControlProperty, displayName As String)
        Dim filterObject As Object = Nothing
        Try
            filterObject = BindSelectedCameraFilter()
            Dim cameraControl = TryCast(filterObject, IAMCameraControl)
            If cameraControl Is Nothing Then
                track.Enabled = False
                Return
            End If

            Dim minValue As Integer = 0, maxValue As Integer = 0, stepSize As Integer = 1, defaultValue As Integer = 0
            Dim flags As DsControlFlags = DsControlFlags.Manual
            Dim hr = cameraControl.GetRange(prop, minValue, maxValue, stepSize, defaultValue, flags)
            If hr < 0 Then
                track.Enabled = False
                Return
            End If

            Dim currentValue As Integer = defaultValue
            Dim currentFlags As DsControlFlags = DsControlFlags.Manual
            Try
                If cameraControl.Get(prop, currentValue, currentFlags) < 0 Then currentValue = defaultValue
            Catch
                currentValue = defaultValue
            End Try

            ConfigureTrack(track, minValue, maxValue, currentValue, stepSize)
        Catch
            track.Enabled = False
        Finally
            ReleaseComObject(filterObject)
        End Try
    End Sub

    Private Sub ConfigureTrack(track As TrackBar, minValue As Integer, maxValue As Integer, currentValue As Integer, stepSize As Integer)
        If minValue = maxValue Then
            track.Enabled = False
            Return
        End If

        If maxValue < minValue Then
            Dim swapValue = minValue
            minValue = maxValue
            maxValue = swapValue
        End If

        track.Minimum = minValue
        track.Maximum = maxValue
        track.SmallChange = Math.Max(1, Math.Abs(stepSize))
        track.LargeChange = Math.Max(track.SmallChange, track.SmallChange * 5)
        If currentValue < minValue Then currentValue = minValue
        If currentValue > maxValue Then currentValue = maxValue
        track.Value = currentValue
        track.Enabled = True
    End Sub

    Private Sub ApplyCurrentProperties()
        If _videoSource Is Nothing Then Return

        ApplyVideoProperty(DsVideoProcAmpProperty.Brightness, trackBrightness.Value, DsControlFlags.Manual)
        ApplyVideoProperty(DsVideoProcAmpProperty.Contrast, trackContrast.Value, DsControlFlags.Manual)
        ApplyVideoProperty(DsVideoProcAmpProperty.Sharpness, trackSharpness.Value, DsControlFlags.Manual)

        Dim whiteBalanceFlags As DsControlFlags = DsControlFlags.Manual
        If chkAutoWhiteBalance.Checked Then whiteBalanceFlags = DsControlFlags.Auto
        ApplyVideoProperty(DsVideoProcAmpProperty.WhiteBalance, trackWhiteBalance.Value, whiteBalanceFlags)

        Dim exposureFlags As DsControlFlags = DsControlFlags.Manual
        If chkAutoExposure.Checked Then exposureFlags = DsControlFlags.Auto
        ApplyCameraProperty(DsCameraControlProperty.Exposure, trackExposure.Value, exposureFlags)

        Dim focusFlags As DsControlFlags = DsControlFlags.Manual
        If chkAutoFocus.Checked Then focusFlags = DsControlFlags.Auto
        ApplyCameraProperty(DsCameraControlProperty.Focus, trackFocus.Value, focusFlags)

        ApplyCameraProperty(DsCameraControlProperty.Zoom, trackZoom.Value, DsControlFlags.Manual)
        SetStatus("تم تطبيق إعدادات الكاميرا المتاحة. بعض الكاميرات لا تسمح بتغيير كل الخصائص إلا من صفحة إعدادات التعريف الأصلية.")
    End Sub

    Private Sub ApplyVideoProperty(prop As DsVideoProcAmpProperty, propertyValue As Integer, flags As DsControlFlags)
        Dim filterObject As Object = Nothing
        Try
            filterObject = BindSelectedCameraFilter()
            Dim amp = TryCast(filterObject, IAMVideoProcAmp)
            If amp IsNot Nothing Then amp.Set(prop, propertyValue, flags)
        Catch
        Finally
            ReleaseComObject(filterObject)
        End Try
    End Sub

    Private Sub ApplyCameraProperty(prop As DsCameraControlProperty, propertyValue As Integer, flags As DsControlFlags)
        Dim filterObject As Object = Nothing
        Try
            filterObject = BindSelectedCameraFilter()
            Dim cameraControl = TryCast(filterObject, IAMCameraControl)
            If cameraControl IsNot Nothing Then cameraControl.Set(prop, propertyValue, flags)
        Catch
        Finally
            ReleaseComObject(filterObject)
        End Try
    End Sub

    Private Function BindSelectedCameraFilter() As Object
        If _devices Is Nothing OrElse cboCameras.SelectedIndex < 0 OrElse cboCameras.SelectedIndex >= _devices.Count Then Return Nothing
        Return Marshal.BindToMoniker(_devices(cboCameras.SelectedIndex).MonikerString)
    End Function

    Private Sub ReleaseComObject(comObject As Object)
        Try
            If comObject IsNot Nothing AndAlso Marshal.IsComObject(comObject) Then Marshal.ReleaseComObject(comObject)
        Catch
        End Try
    End Sub

    Private Enum DsVideoProcAmpProperty
        Brightness = 0
        Contrast = 1
        Hue = 2
        Saturation = 3
        Sharpness = 4
        Gamma = 5
        ColorEnable = 6
        WhiteBalance = 7
        BacklightCompensation = 8
        Gain = 9
    End Enum

    Private Enum DsCameraControlProperty
        Pan = 0
        Tilt = 1
        Roll = 2
        Zoom = 3
        Exposure = 4
        Iris = 5
        Focus = 6
    End Enum

    <Flags()>
    Private Enum DsControlFlags
        Auto = 1
        Manual = 2
    End Enum

    <ComImport(), Guid("C6E13360-30AC-11D0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IAMVideoProcAmp
        Function GetRange(ByVal prop As DsVideoProcAmpProperty, ByRef minValue As Integer, ByRef maxValue As Integer, ByRef steppingDelta As Integer, ByRef defaultValue As Integer, ByRef flags As DsControlFlags) As Integer
        Function [Set](ByVal prop As DsVideoProcAmpProperty, ByVal value As Integer, ByVal flags As DsControlFlags) As Integer
        Function [Get](ByVal prop As DsVideoProcAmpProperty, ByRef value As Integer, ByRef flags As DsControlFlags) As Integer
    End Interface

    <ComImport(), Guid("C6E13370-30AC-11D0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IAMCameraControl
        Function GetRange(ByVal prop As DsCameraControlProperty, ByRef minValue As Integer, ByRef maxValue As Integer, ByRef steppingDelta As Integer, ByRef defaultValue As Integer, ByRef flags As DsControlFlags) As Integer
        Function [Set](ByVal prop As DsCameraControlProperty, ByVal value As Integer, ByVal flags As DsControlFlags) As Integer
        Function [Get](ByVal prop As DsCameraControlProperty, ByRef value As Integer, ByRef flags As DsControlFlags) As Integer
    End Interface

    Private Sub SaveJpeg(source As Bitmap, filePath As String, quality As Integer)
        Dim encoder = GetEncoder(ImageFormat.Jpeg)
        If encoder Is Nothing Then
            source.Save(filePath, ImageFormat.Jpeg)
            Return
        End If
        Dim parameters As New EncoderParameters(1)
        parameters.Param(0) = New EncoderParameter(System.Drawing.Imaging.Encoder.Quality, CLng(quality))
        source.Save(filePath, encoder, parameters)
    End Sub

    Private Function GetEncoder(format As ImageFormat) As ImageCodecInfo
        For Each codec In ImageCodecInfo.GetImageDecoders()
            If codec.FormatID = format.Guid Then Return codec
        Next
        Return Nothing
    End Function

    Private Sub SetStatus(message As String)
        lblStatus.Text = message
    End Sub

    Private Class VideoCapabilityItem
        Public ReadOnly Property Capability As VideoCapabilities
        Public Sub New(capability As VideoCapabilities)
            Me.Capability = capability
        End Sub
        Public Overrides Function ToString() As String
            If Capability Is Nothing Then Return "دقة غير معروفة"
            Return Capability.FrameSize.Width.ToString() & " × " & Capability.FrameSize.Height.ToString() & " @ " & Capability.AverageFrameRate.ToString() & "fps"
        End Function
    End Class
End Class
