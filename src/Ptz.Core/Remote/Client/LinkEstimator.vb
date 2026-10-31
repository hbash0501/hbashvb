Namespace Remote

    ''' <summary>
    ''' يقدّر إنتاجية الوصلة من أزمنة تنزيل الإطارات (بعد وصول الترويسات، فلا يلوّثها انتظار الخادم لإطار جديد).
    ''' متوسط أُسّي؛ العيّنات الصغيرة جداً تُهمل لأنها تعكس زمن الشبكة لا السعة.
    ''' </summary>
    Public NotInheritable Class LinkEstimator
        Public Const DefaultMinSampleBytes As Integer = 4096
        Private ReadOnly _minSampleBytes As Integer
        Private Const Alpha As Double = 0.3
        Private _throughput As Double
        Private _samples As Integer
        Private _failures As Integer

        Public Sub New(Optional minSampleBytes As Integer = DefaultMinSampleBytes)
            _minSampleBytes = Math.Max(1, minSampleBytes)
        End Sub

        ''' <summary>بايت/ثانية (0 = غير معروف بعد).</summary>
        Public ReadOnly Property ThroughputBytesPerSecond As Double
            Get
                Return _throughput
            End Get
        End Property

        Public ReadOnly Property Samples As Integer
            Get
                Return _samples
            End Get
        End Property

        Public ReadOnly Property ConsecutiveFailures As Integer
            Get
                Return _failures
            End Get
        End Property

        Public Sub RecordFrame(bytes As Integer, bodyMs As Double)
            _failures = 0
            If bytes < _minSampleBytes Then Return
            Dim seconds As Double = Math.Max(bodyMs, 1.0) / 1000.0
            Dim sample As Double = bytes / seconds
            _throughput = If(_samples = 0, sample, _throughput * (1 - Alpha) + sample * Alpha)
            _samples += 1
        End Sub

        Public Sub RecordFailure()
            _failures += 1
        End Sub

        Public Sub Reset()
            _throughput = 0
            _samples = 0
            _failures = 0
        End Sub
    End Class

End Namespace
