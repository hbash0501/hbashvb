Imports Ptz.Core.Models

Namespace Engine

    ''' <summary>إعدادات المحرك الافتراضية لكل طراز (مشتركة بين التطبيق المحلي والبوابة كي يتطابق السلوك).</summary>
    Public NotInheritable Class EnginePresets
        Private Sub New()
        End Sub

        Public Shared Sub Apply(profile As CameraProfile, options As SmartEngineOptions)
            If profile Is CameraProfiles.Ds2Se7c432mwAeb Then
                ' لقطات 4MP أكبر حجماً: معدل أقل؛ ضجيج أقل (DarkFighter) يسمح بعتبة أدق.
                options.TargetFps = 4.0
                options.Motion.Subtractor.ThresholdSigma = 2.8F
            ElseIf profile Is CameraProfiles.Ds2De7232iwAe Then
                options.TargetFps = 5.0
                options.Motion.Subtractor.ThresholdSigma = 3.0F
            End If
        End Sub
    End Class

End Namespace
