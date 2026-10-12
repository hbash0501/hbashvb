Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Vision
Imports Ptz.Presentation

Namespace Controls

    ''' <summary>لوحة الذكاء: مفاتيح التشغيل، رسم المناطق، عتبات السلوك، نموذج الشذوذ.</summary>
    Public Class SmartPanel
        Implements ISmartPanelView

        Public Event MotionToggled As Action(Of Boolean) Implements ISmartPanelView.MotionToggled
        Public Event BehaviorToggled As Action(Of Boolean) Implements ISmartPanelView.BehaviorToggled
        Public Event AutoTrackToggled As Action(Of Boolean) Implements ISmartPanelView.AutoTrackToggled
        Public Event ReleaseTargetRequested As Action Implements ISmartPanelView.ReleaseTargetRequested
        Public Event ClearGeometryRequested As Action Implements ISmartPanelView.ClearGeometryRequested
        Public Event DrawModeRequested As Action(Of GeometryDrawMode) Implements ISmartPanelView.DrawModeRequested
        Public Event AnomalyLearningToggled As Action(Of Boolean) Implements ISmartPanelView.AnomalyLearningToggled
        Public Event SaveModelRequested As Action Implements ISmartPanelView.SaveModelRequested
        Public Event ApplyBehaviorOptionsRequested As Action(Of BehaviorOptions) Implements ISmartPanelView.ApplyBehaviorOptionsRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
        End Sub

        Public Sub ShowStatus(autoTrackActive As Boolean, learning As Boolean, trackCount As Integer, suspended As Boolean) Implements ISmartPanelView.ShowStatus
            lblStatus.Text = $"أجسام: {trackCount}" & If(autoTrackActive, " · يتتبع", "") & If(learning, " · تعلّم", "") & If(suspended, " · معلّق", "")
            lblStatus.ForeColor = If(autoTrackActive, Theme.Danger, Theme.Muted)
        End Sub

        Public Sub ShowHint(text As String) Implements ISmartPanelView.ShowHint
            lblHint.Text = If(text = "", "نقر أيمن على الفيديو = قفل هدف للتتبع · سحب = تكبير على منطقة", text)
        End Sub

        Private Sub chkMotion_CheckedChanged(sender As Object, e As EventArgs) Handles chkMotion.CheckedChanged
            RaiseEvent MotionToggled(chkMotion.Checked)
        End Sub

        Private Sub chkBehavior_CheckedChanged(sender As Object, e As EventArgs) Handles chkBehavior.CheckedChanged
            RaiseEvent BehaviorToggled(chkBehavior.Checked)
        End Sub

        Private Sub chkAutoTrack_CheckedChanged(sender As Object, e As EventArgs) Handles chkAutoTrack.CheckedChanged
            RaiseEvent AutoTrackToggled(chkAutoTrack.Checked)
        End Sub

        Private Sub chkLearning_CheckedChanged(sender As Object, e As EventArgs) Handles chkLearning.CheckedChanged
            RaiseEvent AnomalyLearningToggled(chkLearning.Checked)
        End Sub

        Private Sub btnRelease_Click(sender As Object, e As EventArgs) Handles btnRelease.Click
            RaiseEvent ReleaseTargetRequested()
        End Sub

        Private Sub btnZone_Click(sender As Object, e As EventArgs) Handles btnZone.Click
            RaiseEvent DrawModeRequested(GeometryDrawMode.Zone)
        End Sub

        Private Sub btnLine_Click(sender As Object, e As EventArgs) Handles btnLine.Click
            RaiseEvent DrawModeRequested(GeometryDrawMode.Line)
        End Sub

        Private Sub btnMask_Click(sender As Object, e As EventArgs) Handles btnMask.Click
            RaiseEvent DrawModeRequested(GeometryDrawMode.MaskZone)
        End Sub

        Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
            If MessageBox.Show(Me, "مسح كل المناطق والخطوط لهذه الكاميرا؟", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
                RaiseEvent ClearGeometryRequested()
            End If
        End Sub

        Private Sub btnSaveModel_Click(sender As Object, e As EventArgs) Handles btnSaveModel.Click
            RaiseEvent SaveModelRequested()
        End Sub

        Private Sub btnApplyThresholds_Click(sender As Object, e As EventArgs) Handles btnApplyThresholds.Click
            Dim o As New BehaviorOptions With {
                .LoiterSeconds = CDbl(nudLoiter.Value),
                .RunSpeed = CDbl(nudRun.Value),
                .StationarySeconds = CDbl(nudStationary.Value),
                .GatherCount = CInt(nudGather.Value),
                .EventCooldownSeconds = CDbl(nudCooldown.Value)}
            RaiseEvent ApplyBehaviorOptionsRequested(o)
        End Sub
    End Class

End Namespace
