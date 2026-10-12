''' <summary>نصوص الواجهة في مكان واحد (جاهزة للترجمة لاحقاً).</summary>
Public NotInheritable Class UiText
    Private Sub New()
    End Sub

    Public Const AppTitle As String = "منصة التحكم بكاميرات PTZ"
    Public Const Connecting As String = "جارٍ الاتصال بالكاميرا..."
    Public Const Connected As String = "متصل"
    Public Const Disconnected As String = "غير متصل"
    Public Const NoPermission As String = "لا تملك صلاحية تنفيذ هذه العملية."
    Public Const SessionExpired As String = "انتهت الجلسة، سجّل الدخول من جديد."
    Public Const Unsupported As String = "هذه الميزة غير مدعومة على الكاميرا."
    Public Const AutoTrackOn As String = "التتبع الآلي: يعمل"
    Public Const AutoTrackOff As String = "التتبع الآلي: متوقف"
    Public Const ZoneDrawHint As String = "انقر لإضافة نقاط المنطقة، ونقرة مزدوجة للإنهاء."
    Public Const LineDrawHint As String = "انقر نقطتين لتحديد خط العبور."
    Public Const Saved As String = "تم الحفظ."
    Public Const Done As String = "تم."
    Public Const ManagedOnGateway As String = "هذا الإعداد يُدار على البوابة نفسها (أداة PtzGateway المحلية)، لا من هذا التطبيق."
    Public Const GatewayUsersCli As String = "المستخدمون وبذور TOTP تُدار على مضيف البوابة فقط: PtzGateway user ... / totp ..."
End Class
