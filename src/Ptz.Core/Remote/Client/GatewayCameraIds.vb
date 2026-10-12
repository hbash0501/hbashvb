Imports Ptz.Core.Models

Namespace Remote

    ''' <summary>معرّفات الكاميرتين كما تعرفها البوابة (ثابتة في عقد API).</summary>
    Public NotInheritable Class GatewayCameraIds
        Private Sub New()
        End Sub

        Public Const Se7c432mw As String = "se7c432mw"
        Public Const De7232iw As String = "de7232iw"

        Public Shared Function ForProfile(profile As CameraProfile) As String
            If profile Is CameraProfiles.Ds2Se7c432mwAeb Then Return Se7c432mw
            If profile Is CameraProfiles.Ds2De7232iwAe Then Return De7232iw
            Throw New NotSupportedException($"الطراز {profile.ModelName} غير مدعوم عبر البوابة.")
        End Function
    End Class

End Namespace
