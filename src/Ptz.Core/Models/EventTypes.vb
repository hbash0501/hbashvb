Namespace Models

    Public Enum AlarmSeverity
        Info = 0
        Warning = 1
        Critical = 2
    End Enum

    ''' <summary>إنذار صادر عن أحد وحدات التحليل الذكي أو الكاميرا أو النظام.</summary>
    Public NotInheritable Class AlarmEvent
        Public Property Id As Guid = Guid.NewGuid()
        Public Property TimeUtc As DateTime = DateTime.UtcNow
        Public Property Camera As String = ""
        ''' <summary>نوع الحدث، مثل Motion/Intrusion/LineCrossing/Loitering/Running/Anomaly/Gathering/DeviceEvent/System.</summary>
        Public Property Kind As String = ""
        Public Property Severity As AlarmSeverity = AlarmSeverity.Info
        Public Property Message As String = ""
        Public Property TrackId As Integer = 0
        Public Property Position As NormPoint
        Public Property Acknowledged As Boolean = False

        ''' <summary>مفتاح إزالة التكرار (نوع + مسار).</summary>
        Public ReadOnly Property DedupeKey As String
            Get
                Return $"{Camera}|{Kind}|{TrackId}"
            End Get
        End Property
    End Class

    ''' <summary>حدث ذكي وارد من الكاميرا نفسها عبر alertStream.</summary>
    Public NotInheritable Class DeviceEvent
        Public Property EventType As String = ""
        Public Property ChannelId As Integer
        Public Property TimeUtc As DateTime
        Public Property State As String = ""
        Public Property Description As String = ""
    End Class

End Namespace
