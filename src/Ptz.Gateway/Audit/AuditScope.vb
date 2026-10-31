Imports System.Threading

Namespace Audit

    ''' <summary>سياق الطلب الحالي للتدقيق (IP حقيقي، مرجع الجلسة، معرّف الطلب) عبر AsyncLocal.</summary>
    Public NotInheritable Class AuditScopeData
        Public ReadOnly Property Ip As String
        Public ReadOnly Property SessionRef As String
        Public ReadOnly Property RequestId As String
        Public ReadOnly Property UserName As String

        Public Sub New(ip As String, sessionRef As String, requestId As String, userName As String)
            Me.Ip = ip
            Me.SessionRef = sessionRef
            Me.RequestId = requestId
            Me.UserName = userName
        End Sub
    End Class

    Public NotInheritable Class AuditScope
        Private Sub New()
        End Sub

        Private Shared ReadOnly _current As New AsyncLocal(Of AuditScopeData)()

        Public Shared Property Current As AuditScopeData
            Get
                Return _current.Value
            End Get
            Set(value As AuditScopeData)
                _current.Value = value
            End Set
        End Property
    End Class

End Namespace
