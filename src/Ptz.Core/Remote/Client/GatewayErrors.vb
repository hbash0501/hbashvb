Imports Ptz.Core.Isapi
Imports Ptz.Core.Security

Namespace Remote

    ''' <summary>
    ''' خطأ منظَّم من البوابة (غلاف الأخطاء). يُترجم إلى استثناءات النطاق نفسها التي تعالجها المقدِّمات محلياً
    ''' (AccessDenied / NotSupported / Isapi ...) كي لا تعرف الواجهة فرقاً بين الوضعين.
    ''' </summary>
    Public NotInheritable Class GatewayApiException
        Inherits Exception

        Public ReadOnly Property Status As Integer
        Public ReadOnly Property Code As String
        Public ReadOnly Property Field As String
        Public ReadOnly Property Required As String
        Public ReadOnly Property RetryAfterSeconds As Integer?
        Public ReadOnly Property IsapiStatus As Integer?
        Public ReadOnly Property IsapiSubStatus As String
        Public ReadOnly Property RequestId As String

        Public Sub New(status As Integer, body As ErrorBody)
            MyBase.New(UiMessage(If(body?.Code, ""), If(body?.Field, "")))
            Me.Status = status
            Code = If(body?.Code, "")
            Field = body?.Field
            Required = body?.Required
            RetryAfterSeconds = body?.RetryAfterSeconds
            IsapiStatus = body?.IsapiStatus
            IsapiSubStatus = body?.IsapiSubStatus
            RequestId = body?.RequestId
        End Sub

        Public Shared Function UiMessage(code As String, field As String) As String
            ' اسم الحقل يأتي من الخادم: لا يُعرض إلا إن كان معرّفاً بسيطاً (لا نصاً حراً يُحقن في الواجهة)
            Dim safeField As Boolean = Not String.IsNullOrEmpty(field) AndAlso System.Text.RegularExpressions.Regex.IsMatch(field, "^[A-Za-z0-9._-]{1,40}\z")
            Dim tail As String = If(safeField, " (" & field & ")", "")
            Select Case code
                Case "unauthorized", "session_expired" : Return "انتهت الجلسة؛ سجّل الدخول من جديد."
                Case "invalid_credentials" : Return "بيانات الدخول غير صحيحة أو الحساب مقفل مؤقتاً."
                Case "forbidden" : Return "لا تملك صلاحية هذا الإجراء."
                Case "step_up_required" : Return "هذا الإجراء يتطلب تحققاً إضافياً."
                Case "invalid_totp" : Return "رمز التحقق غير صحيح."
                Case "https_required" : Return "البوابة تشترط اتصالاً مشفّراً."
                Case "bad_request" : Return "طلب غير صالح" & tail & "."
                Case "not_found" : Return "العنصر غير موجود."
                Case "stale_seq" : Return "تم تجاوز الأمر بأمر أحدث."
                Case "conflict" : Return "تعارض مع حالة الجهاز الحالية."
                Case "precondition_failed" : Return "تغيّر الإعداد من مشغّل آخر؛ أُعيد التحميل."
                Case "precondition_required" : Return "الطلب يفتقر إلى شرط التزامن."
                Case "payload_too_large" : Return "الطلب أكبر من المسموح."
                Case "rate_limited" : Return "طلبات كثيرة؛ حاول بعد قليل."
                Case "too_many_streams" : Return "عدد البثوث المفتوحة بلغ الحد."
                Case "unsupported" : Return "هذه الميزة غير مدعومة في هذه الكاميرا."
                Case "camera_unavailable" : Return "الكاميرا غير متاحة حالياً."
                Case "camera_error" : Return "رفضت الكاميرا الأمر."
                Case "busy" : Return "البوابة مشغولة؛ حاول بعد قليل."
                Case "audit_unavailable" : Return "سجل التدقيق غير متاح؛ رُفضت العملية احترازياً."
                Case "timeout" : Return "انتهت مهلة الطلب."
                Case Else : Return "خطأ في البوابة."
            End Select
        End Function

        Public ReadOnly Property IsSessionEnded As Boolean
            Get
                Return Status = 401 AndAlso Code <> "invalid_credentials"
            End Get
        End Property

        ''' <summary>الاستثناء الذي تفهمه المقدِّمات (يحفظ الفئة، لا يكشف تفاصيل داخلية).</summary>
        Public Function ToDomainException() As Exception
            Select Case Status
                Case 401
                    If Code = "invalid_credentials" Then Return New AccessDeniedException(Permission.None, Message)
                    Return New AccessDeniedException(Permission.None, Message, isSessionExpired:=True)
                Case 403
                    Dim req As Permission = Permission.None
                    If Not String.IsNullOrEmpty(Required) Then
                        Try
                            req = WireMap.ParsePermission(Required)
                        Catch ex As ArgumentException
                            req = Permission.None
                        End Try
                    End If
                    Return New AccessDeniedException(req, Message)
                Case 400, 413, 415 : Return New ArgumentException(Message)
                Case 404 : Return New InvalidOperationException(Message)
                Case 409, 412, 428 : Return New InvalidOperationException(Message)
                Case 429 : Return New InvalidOperationException(Message & If(RetryAfterSeconds.HasValue, $" ({RetryAfterSeconds} ث)", ""))
                Case 501 : Return New NotSupportedException(Message)
                Case 502 : Return New IsapiException(If(IsapiStatus, 502), If(IsapiSubStatus, ""), Message)
                Case 503, 504 : Return New IsapiException(0, "", Message)
                Case Else : Return New IsapiException(Status, "", Message)
            End Select
        End Function
    End Class

End Namespace
