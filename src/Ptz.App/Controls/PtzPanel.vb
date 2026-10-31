Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Models
Imports Ptz.Presentation

Namespace Controls

    ''' <summary>لوحة PTZ: عصا تحكم، تكبير، مواضع مسبقة، دوريات، أنماط، موضع مطلق. لا تحتوي منطق أعمال.</summary>
    Public Class PtzPanel
        Implements IPtzPanelView

        Private _joyX As Double
        Private _joyY As Double
        Private _zoomDir As Integer
        Private _active As Boolean
        Private WithEvents _repeat As New Timer With {.Interval = 250}

        Public Event MoveRequested As Action(Of PtzVector) Implements IPtzPanelView.MoveRequested
        Public Event StopRequested As Action Implements IPtzPanelView.StopRequested
        Public Event GoHomeRequested As Action Implements IPtzPanelView.GoHomeRequested
        Public Event SetHomeRequested As Action Implements IPtzPanelView.SetHomeRequested
        Public Event GoToPositionRequested As Action(Of PtzPosition) Implements IPtzPanelView.GoToPositionRequested
        Public Event RefreshPresetsRequested As Action Implements IPtzPanelView.RefreshPresetsRequested
        Public Event PresetGoRequested As Action(Of Integer) Implements IPtzPanelView.PresetGoRequested
        Public Event PresetSaveRequested As Action(Of Integer, String) Implements IPtzPanelView.PresetSaveRequested
        Public Event PresetDeleteRequested As Action(Of Integer) Implements IPtzPanelView.PresetDeleteRequested
        Public Event PatrolRequested As Action(Of Integer, Boolean) Implements IPtzPanelView.PatrolRequested
        Public Event PatternRecordRequested As Action(Of Integer, Boolean) Implements IPtzPanelView.PatternRecordRequested
        Public Event PatternRunRequested As Action(Of Integer, Boolean) Implements IPtzPanelView.PatternRunRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
        End Sub

        ' ---------- IPtzPanelView ----------
        Public Sub SetCapabilities(features As CameraFeatures, profile As CameraProfile) Implements IPtzPanelView.SetCapabilities
            nudPresetId.Maximum = profile.MaxPresets
            nudPatrol.Maximum = profile.MaxPatrols
            nudPattern.Maximum = profile.MaxPatterns
            nudZm.Maximum = profile.OpticalZoomMax
            grpPresets.Enabled = (features And CameraFeatures.Presets) <> 0
            grpTours.Enabled = (features And (CameraFeatures.Patrol Or CameraFeatures.Pattern)) <> 0
            btnHome.Enabled = (features And CameraFeatures.HomePosition) <> 0
            btnSetHome.Enabled = btnHome.Enabled
            grpMove.Enabled = (features And CameraFeatures.Ptz) <> 0
            grpAbs.Enabled = grpMove.Enabled
        End Sub

        Private NotInheritable Class PresetItem
            Public ReadOnly Property Preset As PtzPreset
            Public ReadOnly Property Label As String
            Public Sub New(p As PtzPreset)
                Preset = p
                Label = $"{p.Id,3}  {p.Name}"
            End Sub

            Public Overrides Function ToString() As String
                Return Label
            End Function
        End Class

        Public Sub ShowPresets(presets As IReadOnlyList(Of PtzPreset)) Implements IPtzPanelView.ShowPresets
            lstPresets.BeginUpdate()
            lstPresets.Items.Clear()
            For Each p As PtzPreset In presets.OrderBy(Function(x) x.Id)
                lstPresets.Items.Add(New PresetItem(p))
            Next
            lstPresets.EndUpdate()
        End Sub

        Public Sub ShowPosition(position As PtzPosition) Implements IPtzPanelView.ShowPosition
            lblPosition.Text = $"الموضع: سمت {position.Azimuth:F1}°  ارتفاع {position.Elevation:F1}°  تكبير {position.Zoom:F1}×"
        End Sub

        ' ---------- الحركة ----------
        Private Function CurrentVector() As PtzVector
            Dim speed As Integer = trkSpeed.Value
            Return New PtzVector(CInt(Math.Round(_joyX * speed)), CInt(Math.Round(-_joyY * speed)), _zoomDir * speed)
        End Function

        Private Sub Push()
            Dim v As PtzVector = CurrentVector()
            If Not v.IsZero Then
                _active = True
                _repeat.Start()
                RaiseEvent MoveRequested(v)
            ElseIf _active Then
                _active = False
                _repeat.Stop()
                RaiseEvent StopRequested()
            End If
        End Sub

        Private Sub joystick_VectorChanged(x As Double, y As Double) Handles joystick.VectorChanged
            _joyX = x
            _joyY = y
            Push()
        End Sub

        Private Sub joystick_Released() Handles joystick.Released
            _joyX = 0
            _joyY = 0
            Push()
        End Sub

        Private Sub _repeat_Tick(sender As Object, e As EventArgs) Handles _repeat.Tick
            If _active Then RaiseEvent MoveRequested(CurrentVector())
        End Sub

        Private Sub ZoomDown(dir As Integer)
            _zoomDir = dir
            Push()
        End Sub

        Private Sub ZoomUp()
            _zoomDir = 0
            Push()
        End Sub

        Private Sub btnZoomIn_MouseDown(sender As Object, e As MouseEventArgs) Handles btnZoomIn.MouseDown
            ZoomDown(1)
        End Sub

        Private Sub btnZoomIn_MouseUp(sender As Object, e As MouseEventArgs) Handles btnZoomIn.MouseUp
            ZoomUp()
        End Sub

        Private Sub btnZoomOut_MouseDown(sender As Object, e As MouseEventArgs) Handles btnZoomOut.MouseDown
            ZoomDown(-1)
        End Sub

        Private Sub btnZoomOut_MouseUp(sender As Object, e As MouseEventArgs) Handles btnZoomOut.MouseUp
            ZoomUp()
        End Sub

        ''' <summary>فقدان التركيز/الالتقاط أثناء ضغط زر التكبير (Alt-Tab، نافذة حوار) لا يصل معه MouseUp: نوقف الحركة بدل أن تستمر مع تجديد العقد.</summary>
        Private Sub ZoomButton_LostHold(sender As Object, e As EventArgs) Handles btnZoomIn.LostFocus, btnZoomOut.LostFocus, btnZoomIn.MouseCaptureChanged, btnZoomOut.MouseCaptureChanged
            If _zoomDir <> 0 Then ZoomUp()
        End Sub

        ''' <summary>يحرّر كل أدوات الحركة (عصا + تكبير) فوراً؛ آمن للاستدعاء المتكرر.</summary>
        Public Sub ReleaseAll()
            _joyX = 0
            _joyY = 0
            _zoomDir = 0
            Push()
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

        Private Sub trkSpeed_ValueChanged(sender As Object, e As EventArgs) Handles trkSpeed.ValueChanged
            lblSpeedValue.Text = trkSpeed.Value.ToString()
        End Sub

        Private Sub btnHome_Click(sender As Object, e As EventArgs) Handles btnHome.Click
            RaiseEvent GoHomeRequested()
        End Sub

        Private Sub btnSetHome_Click(sender As Object, e As EventArgs) Handles btnSetHome.Click
            If MessageBox.Show(Me, "تعيين الموضع الحالي كموضع رئيسي؟", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                RaiseEvent SetHomeRequested()
            End If
        End Sub

        ' ---------- المواضع المسبقة ----------
        Private Sub lstPresets_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstPresets.SelectedIndexChanged
            Dim item As PresetItem = TryCast(lstPresets.SelectedItem, PresetItem)
            If item Is Nothing Then Return
            nudPresetId.Value = Math.Min(nudPresetId.Maximum, Math.Max(nudPresetId.Minimum, item.Preset.Id))
            txtPresetName.Text = item.Preset.Name
        End Sub

        Private Sub lstPresets_DoubleClick(sender As Object, e As EventArgs) Handles lstPresets.DoubleClick
            btnPresetGo_Click(sender, e)
        End Sub

        Private Sub btnPresetGo_Click(sender As Object, e As EventArgs) Handles btnPresetGo.Click
            RaiseEvent PresetGoRequested(CInt(nudPresetId.Value))
        End Sub

        Private Sub btnPresetSave_Click(sender As Object, e As EventArgs) Handles btnPresetSave.Click
            RaiseEvent PresetSaveRequested(CInt(nudPresetId.Value), txtPresetName.Text)
        End Sub

        Private Sub btnPresetDelete_Click(sender As Object, e As EventArgs) Handles btnPresetDelete.Click
            If MessageBox.Show(Me, $"حذف الموضع المسبق رقم {nudPresetId.Value}؟", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
                RaiseEvent PresetDeleteRequested(CInt(nudPresetId.Value))
            End If
        End Sub

        ' ---------- الدوريات والأنماط ----------
        Private Sub btnPatrolStart_Click(sender As Object, e As EventArgs) Handles btnPatrolStart.Click
            RaiseEvent PatrolRequested(CInt(nudPatrol.Value), True)
        End Sub

        Private Sub btnPatrolStop_Click(sender As Object, e As EventArgs) Handles btnPatrolStop.Click
            RaiseEvent PatrolRequested(CInt(nudPatrol.Value), False)
        End Sub

        Private Sub btnPatternRec_Click(sender As Object, e As EventArgs) Handles btnPatternRec.Click
            RaiseEvent PatternRecordRequested(CInt(nudPattern.Value), True)
        End Sub

        Private Sub btnPatternRecStop_Click(sender As Object, e As EventArgs) Handles btnPatternRecStop.Click
            RaiseEvent PatternRecordRequested(CInt(nudPattern.Value), False)
        End Sub

        Private Sub btnPatternRun_Click(sender As Object, e As EventArgs) Handles btnPatternRun.Click
            RaiseEvent PatternRunRequested(CInt(nudPattern.Value), True)
        End Sub

        Private Sub btnPatternStop_Click(sender As Object, e As EventArgs) Handles btnPatternStop.Click
            RaiseEvent PatternRunRequested(CInt(nudPattern.Value), False)
        End Sub

        Private Sub btnGoAbs_Click(sender As Object, e As EventArgs) Handles btnGoAbs.Click
            RaiseEvent GoToPositionRequested(New PtzPosition(CDbl(nudAz.Value), CDbl(nudEl.Value), CDbl(nudZm.Value)))
        End Sub

        Protected Overrides Sub OnVisibleChanged(e As EventArgs)
            MyBase.OnVisibleChanged(e)
            ' إخفاء اللوحة أثناء الضغط يجب ألا يترك الكاميرا تتحرك.
            If Not Visible AndAlso _active Then
                _joyX = 0
                _joyY = 0
                _zoomDir = 0
                Push()
            End If
        End Sub
    End Class

End Namespace
