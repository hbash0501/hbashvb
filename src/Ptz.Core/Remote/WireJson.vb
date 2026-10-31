Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Remote

    ''' <summary>خيارات JSON الموحّدة: camelCase، تجاهل null عند الكتابة، وقراءة صارمة (يرفض الأعضاء المجهولة) على الخادم ومتسامحة على العميل.</summary>
    Public NotInheritable Class WireJson
        Private Sub New()
        End Sub

        Public Shared ReadOnly Write As JsonSerializerOptions = Build(False)
        ''' <summary>الخادم: عضو مجهول في الطلب ⇒ JsonException ⇒ 400.</summary>
        Public Shared ReadOnly ServerRead As JsonSerializerOptions = Build(True)
        ''' <summary>العميل: يتجاهل أعضاء جديدة (إضافات متوافقة لاحقاً).</summary>
        Public Shared ReadOnly ClientRead As JsonSerializerOptions = Build(False)

        Private Shared Function Build(strict As Boolean) As JsonSerializerOptions
            Dim o As New JsonSerializerOptions(JsonSerializerDefaults.Web)
            o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            o.MaxDepth = 8
            o.NumberHandling = JsonNumberHandling.Strict
            If strict Then o.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            Return o
        End Function

        Public Shared Function Serialize(Of T)(value As T) As String
            Return JsonSerializer.Serialize(value, Write)
        End Function

        Public Shared Function ToUtf8(Of T)(value As T) As Byte()
            Return JsonSerializer.SerializeToUtf8Bytes(value, Write)
        End Function
    End Class

End Namespace
