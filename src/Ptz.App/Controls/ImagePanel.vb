Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Models
Imports Ptz.Presentation

Namespace Controls

    ''' <summary>لوحة الصورة: كل معامل مرتبط بعنصر واجهة عبر جدول (ImageParam ↔ Control) دون منطق أعمال.</summary>
    Public Class ImagePanel
        Implements IImagePanelView

        Private ReadOnly _trackBars As New Dictionary(Of ImageParam, TrackBar)()
        Private ReadOnly _combos As New Dictionary(Of ImageParam, ComboBox)()
        Private ReadOnly _valueLabels As New Dictionary(Of TrackBar, Label)()
        Private ReadOnly _dirty As New HashSet(Of ImageParam)()
        Private _loading As Boolean

        Public Event ApplyRequested As Action(Of ImageSettings) Implements IImagePanelView.ApplyRequested
        Public Event RefreshRequested As Action Implements IImagePanelView.RefreshRequested
        Public Event FocusRequested As Action(Of LensDirection, Integer) Implements IImagePanelView.FocusRequested
        Public Event IrisRequested As Action(Of LensDirection, Integer) Implements IImagePanelView.IrisRequested
        Public Event AuxRequested As Action(Of AuxKind, Boolean) Implements IImagePanelView.AuxRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
            Map(ImageParam.Brightness, trkBrightness, lblBrightnessValue)
            Map(ImageParam.Contrast, trkContrast, lblContrastValue)
            Map(ImageParam.Saturation, trkSaturation, lblSaturationValue)
            Map(ImageParam.Sharpness, trkSharpness, lblSharpnessValue)
            Map(ImageParam.NoiseReduceLevel, trkNoise, lblNoiseValue)
            Map(ImageParam.WdrLevel, trkWdrLevel, lblWdrLevelValue)
            Map(ImageParam.IrBrightness, trkIrBrightness, lblIrBrightnessValue)
            Map(ImageParam.ExposureType, cmbExposureType)
            Map(ImageParam.IrCutMode, cmbIrCutMode)
            Map(ImageParam.WdrMode, cmbWdrMode)
            Map(ImageParam.BlcMode, cmbBlcMode)
            Map(ImageParam.WhiteBalanceStyle, cmbWhiteBalance)
            Map(ImageParam.DefogMode, cmbDefog)
            Map(ImageParam.SupplementLightMode, cmbSupplement)
            Map(ImageParam.FocusStyle, cmbFocusStyle)
        End Sub

        Private Sub Map(param As ImageParam, bar As TrackBar, valueLabel As Label)
            _trackBars(param) = bar
            _valueLabels(bar) = valueLabel
            AddHandler bar.ValueChanged, Sub()
                                             valueLabel.Text = bar.Value.ToString()
                                             If Not _loading Then _dirty.Add(param)
                                         End Sub
        End Sub

        Private Sub Map(param As ImageParam, combo As ComboBox)
            _combos(param) = combo
            ' القيم المسموحة من الكتالوج (مصدر واحد للحقيقة) لا من نص ثابت في المصمم.
            For Each v As String In ImageParamCatalog.Get(param).AllowedValues
                combo.Items.Add(v)
            Next
            AddHandler combo.SelectedIndexChanged, Sub()
                                                       If Not _loading Then _dirty.Add(param)
                                                   End Sub
        End Sub

        Public Sub SetCapabilities(features As CameraFeatures) Implements IImagePanelView.SetCapabilities
            For Each kv In _trackBars
                Dim ok As Boolean = (features And ImageParamCatalog.Get(kv.Key).Feature) <> 0
                kv.Value.Enabled = ok
            Next
            For Each kv In _combos
                kv.Value.Enabled = (features And ImageParamCatalog.Get(kv.Key).Feature) <> 0
            Next
            grpLens.Enabled = (features And CameraFeatures.Focus) <> 0
            btnWiperOn.Enabled = (features And CameraFeatures.Wiper) <> 0
            btnWiperOff.Enabled = btnWiperOn.Enabled
            btnLightOn.Enabled = (features And CameraFeatures.SupplementLight) <> 0
            btnLightOff.Enabled = btnLightOn.Enabled
        End Sub

        Public Sub ShowSettings(settings As ImageSettings) Implements IImagePanelView.ShowSettings
            _loading = True
            Try
                For Each kv In _trackBars
                    Dim v As String = settings(kv.Key)
                    Dim n As Integer
                    If v IsNot Nothing AndAlso Integer.TryParse(v, n) Then kv.Value.Value = Math.Min(kv.Value.Maximum, Math.Max(kv.Value.Minimum, n))
                Next
                For Each kv In _combos
                    Dim v As String = settings(kv.Key)
                    If v IsNot Nothing Then kv.Value.SelectedItem = v
                Next
                _dirty.Clear()
            Finally
                _loading = False
            End Try
        End Sub

        Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click
            Dim s As New ImageSettings()
            For Each p As ImageParam In _dirty
                Dim bar As TrackBar = Nothing
                Dim combo As ComboBox = Nothing
                If _trackBars.TryGetValue(p, bar) Then
                    If bar.Enabled Then s(p) = bar.Value.ToString()
                ElseIf _combos.TryGetValue(p, combo) Then
                    If combo.Enabled AndAlso combo.SelectedItem IsNot Nothing Then s(p) = CStr(combo.SelectedItem)
                End If
            Next
            If s.Count = 0 Then Return
            RaiseEvent ApplyRequested(s)
            _dirty.Clear()
        End Sub

        Private Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
            RaiseEvent RefreshRequested()
        End Sub

        ' التركيز والفتحة: حركة أثناء الضغط وإيقاف عند الإفلات (وعند فقدان التركيز/الالتقاط/تعطيل النافذة)
        Private _focusHeld As Boolean
        Private _irisHeld As Boolean

        Private Sub HoldFocus(direction As LensDirection)
            _focusHeld = True
            RaiseEvent FocusRequested(direction, 50)
        End Sub

        Private Sub ReleaseFocus()
            If Not _focusHeld Then Return
            _focusHeld = False
            RaiseEvent FocusRequested(LensDirection.Halt, 0)
        End Sub

        Private Sub HoldIris(direction As LensDirection)
            _irisHeld = True
            RaiseEvent IrisRequested(direction, 50)
        End Sub

        Private Sub ReleaseIris()
            If Not _irisHeld Then Return
            _irisHeld = False
            RaiseEvent IrisRequested(LensDirection.Halt, 0)
        End Sub

        Private Sub btnFocusNear_MouseDown(sender As Object, e As MouseEventArgs) Handles btnFocusNear.MouseDown
            HoldFocus(LensDirection.Near)
        End Sub

        Private Sub btnFocusFar_MouseDown(sender As Object, e As MouseEventArgs) Handles btnFocusFar.MouseDown
            HoldFocus(LensDirection.Far)
        End Sub

        Private Sub FocusButton_Released(sender As Object, e As EventArgs) Handles btnFocusNear.MouseUp, btnFocusFar.MouseUp, btnFocusNear.LostFocus, btnFocusFar.LostFocus, btnFocusNear.MouseCaptureChanged, btnFocusFar.MouseCaptureChanged
            ReleaseFocus()
        End Sub

        Private Sub btnIrisClose_MouseDown(sender As Object, e As MouseEventArgs) Handles btnIrisClose.MouseDown
            HoldIris(LensDirection.Near)
        End Sub

        Private Sub btnIrisOpen_MouseDown(sender As Object, e As MouseEventArgs) Handles btnIrisOpen.MouseDown
            HoldIris(LensDirection.Far)
        End Sub

        Private Sub IrisButton_Released(sender As Object, e As EventArgs) Handles btnIrisClose.MouseUp, btnIrisOpen.MouseUp, btnIrisClose.LostFocus, btnIrisOpen.LostFocus, btnIrisClose.MouseCaptureChanged, btnIrisOpen.MouseCaptureChanged
            ReleaseIris()
        End Sub

        ''' <summary>يحرّر أدوات العدسة فوراً؛ آمن للاستدعاء المتكرر.</summary>
        Public Sub ReleaseAll()
            ReleaseFocus()
            ReleaseIris()
        End Sub

        Private _hookedForm As Form

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            Dim f As Form = FindForm()
            If f IsNot Nothing AndAlso f IsNot _hookedForm Then
                _hookedForm = f
                AddHandler f.Deactivate, AddressOf OnFormDeactivated
            End If
        End Sub

        Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
            If _hookedForm IsNot Nothing Then
                RemoveHandler _hookedForm.Deactivate, AddressOf OnFormDeactivated
                _hookedForm = Nothing
            End If
            MyBase.OnHandleDestroyed(e)
        End Sub

        Private Sub OnFormDeactivated(sender As Object, e As EventArgs)
            ReleaseAll()
        End Sub

        Private Sub btnWiperOn_Click(sender As Object, e As EventArgs) Handles btnWiperOn.Click
            RaiseEvent AuxRequested(AuxKind.Wiper, True)
        End Sub

        Private Sub btnWiperOff_Click(sender As Object, e As EventArgs) Handles btnWiperOff.Click
            RaiseEvent AuxRequested(AuxKind.Wiper, False)
        End Sub

        Private Sub btnLightOn_Click(sender As Object, e As EventArgs) Handles btnLightOn.Click
            RaiseEvent AuxRequested(AuxKind.Light, True)
        End Sub

        Private Sub btnLightOff_Click(sender As Object, e As EventArgs) Handles btnLightOff.Click
            RaiseEvent AuxRequested(AuxKind.Light, False)
        End Sub
    End Class

End Namespace
