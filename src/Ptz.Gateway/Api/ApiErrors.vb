Imports System.Text.Json
Imports Ptz.Core.Isapi
Imports Ptz.Core.Remote
Imports Ptz.Core.Security

Namespace Api

    ''' <summary>خطأ API بحالة HTTP ورمز ثابت. الرسالة نص ثابت لكل رمز ولا تعكس المدخلات أبداً.</summary>
    Public Class ApiException
        Inherits Exception

        Public ReadOnly Property Status As Integer
        Public ReadOnly Property Code As String
        Public Property Field As String
        Public Property Required As String
        Public Property RetryAfterSeconds As Integer?
        Public Property IsapiStatus As Integer?
        Public Property IsapiSubStatus As String
        Public Property ExtraHeaders As New Dictionary(Of String, String)()

        Public Sub New(status As Integer, code As String)
            MyBase.New(MessageFor(code))
            Me.Status = status
            Me.Code = code
        End Sub

        Public Shared Function BadRequest(Optional field As String = Nothing) As ApiException
            Return New ApiException(400, "bad_request") With {.Field = field}
        End Function

        Public Shared Function NotFound() As ApiException
            Return New ApiException(404, "not_found")
        End Function

        Public Shared Function Unauthorized(Optional code As String = "unauthorized") As ApiException
            Return New ApiException(401, code)
        End Function

        Public Shared Function MessageFor(code As String) As String
            Select Case code
                Case "bad_request" : Return "invalid request"
                Case "unauthorized" : Return "authentication required"
                Case "session_expired" : Return "session expired"
                Case "invalid_credentials" : Return "invalid credentials"
                Case "forbidden" : Return "permission required"
                Case "step_up_required" : Return "step-up authentication required"
                Case "invalid_totp" : Return "invalid code"
                Case "https_required" : Return "https required"
                Case "not_found" : Return "not found"
                Case "method_not_allowed" : Return "method not allowed"
                Case "stale_seq" : Return "superseded"
                Case "conflict" : Return "conflict"
                Case "precondition_failed" : Return "precondition failed"
                Case "precondition_required" : Return "precondition required"
                Case "payload_too_large" : Return "payload too large"
                Case "unsupported_media_type" : Return "content type must be application/json"
                Case "rate_limited" : Return "too many requests"
                Case "too_many_streams" : Return "too many streams"
                Case "internal" : Return "internal error"
                Case "unsupported" : Return "not supported by this camera"
                Case "camera_error" : Return "camera rejected the command"
                Case "camera_unavailable" : Return "camera unavailable"
                Case "busy" : Return "server busy"
                Case "audit_unavailable" : Return "audit log unavailable"
                Case "timeout" : Return "timed out"
                Case Else : Return "error"
            End Select
        End Function
    End Class

    ''' <summary>تحويل الاستثناءات إلى استجابة خطأ موحّدة (لا تسريب لنص الاستثناء أبداً).</summary>
    Public NotInheritable Class ErrorMap
        Private Sub New()
        End Sub

        Public Shared Function ToApiException(ex As Exception, timedOut As Boolean) As ApiException
            Dim ae As ApiException = TryCast(ex, ApiException)
            If ae IsNot Nothing Then Return ae
            Dim wv As WireValidationException = TryCast(ex, WireValidationException)
            If wv IsNot Nothing Then Return ApiException.BadRequest(wv.FieldName)
            Dim ad As AccessDeniedException = TryCast(ex, AccessDeniedException)
            If ad IsNot Nothing Then
                If ad.IsSessionExpired Then Return ApiException.Unauthorized("session_expired")
                Return New ApiException(403, "forbidden") With {.Required = WireMap.CamelName(ad.Required.ToString())}
            End If
            If TypeOf ex Is OperationCanceledException Then
                If timedOut Then Return New ApiException(504, "timeout")
                Return Nothing
            End If
            If TypeOf ex Is JsonException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is FormatException Then Return ApiException.BadRequest()
            If TypeOf ex Is InvalidOperationException Then Return New ApiException(409, "conflict")
            If TypeOf ex Is NotSupportedException Then Return New ApiException(501, "unsupported")
            Dim ie As IsapiException = TryCast(ex, IsapiException)
            If ie IsNot Nothing Then
                If ie.HttpStatus = 0 OrElse ie.HttpStatus = 503 Then Return New ApiException(503, "camera_unavailable") With {.RetryAfterSeconds = 2}
                Dim sub_ As String = AuditSanitizer.Clean(ie.SubStatus, 32)
                Return New ApiException(502, "camera_error") With {.IsapiStatus = ie.HttpStatus, .IsapiSubStatus = If(sub_ = "", Nothing, sub_)}
            End If
            Return New ApiException(500, "internal")
        End Function

        Public Shared Function ToEnvelope(ae As ApiException, requestId As String) As ErrorEnvelope
            Return New ErrorEnvelope With {.Error = New ErrorBody With {
                .Code = ae.Code, .Message = ae.Message, .RequestId = requestId, .Required = ae.Required, .Field = ae.Field,
                .RetryAfterSeconds = ae.RetryAfterSeconds, .IsapiStatus = ae.IsapiStatus, .IsapiSubStatus = ae.IsapiSubStatus}}
        End Function
    End Class

End Namespace
