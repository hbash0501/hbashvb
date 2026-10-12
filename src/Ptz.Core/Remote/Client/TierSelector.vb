Namespace Remote

    ''' <summary>
    ''' يختار طبقة جودة الفيديو تلقائياً حسب إنتاجية الوصلة: ينزل فوراً عند الإخفاق/البطء، ويصعد خطوة واحدة فقط بعد
    ''' عدة إطارات سريعة متتالية (تباطؤ الصعود يمنع التذبذب). يحترم حدّ الخادم المعلن (hello.tiers) وحدّ المستخدم.
    ''' </summary>
    Public NotInheritable Class TierSelector
        Public Const DefaultUpgradeAfterGoodFrames As Integer = 6
        ''' <summary>هامش أمان: يلزم تدفق أكبر بـ 1.6× من حاجة الطبقة كي نعدّها مستدامة.</summary>
        Public Const Headroom As Double = 1.6

        Private ReadOnly _available As List(Of Integer)
        Private ReadOnly _observed(FrameLadder.Entries.Count - 1) As Double
        Private _current As Integer
        Private _good As Integer
        Private _manual As Integer = -1
        Private _maxIndex As Integer
        Private ReadOnly _upgradeAfter As Integer

        ''' <param name="availableIds">الطبقات التي يعلنها الخادم (الترتيب تصاعدي).</param>
        Public Sub New(availableIds As IEnumerable(Of String), Optional initialId As String = "low", Optional userMaxId As String = Nothing,
                       Optional upgradeAfterGoodFrames As Integer = DefaultUpgradeAfterGoodFrames)
            _upgradeAfter = Math.Max(1, upgradeAfterGoodFrames)
            _available = New List(Of Integer)()
            For Each id As String In availableIds
                Dim i As Integer = FrameLadder.IndexOf(id)
                If i >= 0 AndAlso Not _available.Contains(i) Then _available.Add(i)
            Next
            If _available.Count = 0 Then _available.Add(FrameLadder.IndexOf("low"))
            _available.Sort()
            _maxIndex = _available.Last()
            If userMaxId IsNot Nothing Then
                Dim u As Integer = FrameLadder.IndexOf(userMaxId)
                If u >= 0 Then _maxIndex = Math.Min(_maxIndex, u)
            End If
            _current = Clamp(FrameLadder.IndexOf(initialId))
        End Sub

        Private Function Clamp(index As Integer) As Integer
            Dim best As Integer = _available.First()
            For Each a As Integer In _available
                If a <= _maxIndex AndAlso a <= Math.Max(index, _available.First()) Then best = a
            Next
            Return best
        End Function

        Public ReadOnly Property Current As String
            Get
                Return FrameLadder.Entries(If(_manual >= 0, _manual, _current)).Id
            End Get
        End Property

        Public ReadOnly Property IsAuto As Boolean
            Get
                Return _manual < 0
            End Get
        End Property

        ''' <summary>الخادم أعلن طبقاته (hello): نقصر الاختيار عليها.</summary>
        Public Sub SetAvailable(availableIds As IEnumerable(Of String))
            Dim list As New List(Of Integer)()
            For Each id As String In availableIds
                Dim i As Integer = FrameLadder.IndexOf(id)
                If i >= 0 AndAlso Not list.Contains(i) Then list.Add(i)
            Next
            If list.Count = 0 Then Return
            list.Sort()
            _available.Clear()
            _available.AddRange(list)
            _maxIndex = Math.Min(_maxIndex, list.Last())
            _current = Clamp(_current)
            If _manual >= 0 Then _manual = Clamp(_manual)
        End Sub

        ''' <summary>الخادم قصّ الطلب إلى طبقة أدنى (سقفه): لا نحاول ما فوقها بعد الآن.</summary>
        Public Sub LimitTo(id As String)
            Dim i As Integer = FrameLadder.IndexOf(id)
            If i < 0 OrElse i >= _maxIndex Then Return
            _maxIndex = i
            _current = Clamp(_current)
            If _manual >= 0 Then _manual = Clamp(_manual)
        End Sub

        ''' <summary>تثبيت طبقة يدوياً (Nothing = تلقائي). الطبقة غير المتاحة تُقصّ إلى أقرب متاحة.</summary>
        Public Sub SetManual(id As String)
            If id Is Nothing Then
                _manual = -1
                _good = 0
                Return
            End If
            Dim i As Integer = FrameLadder.IndexOf(id)
            If i < 0 Then Throw New ArgumentException("طبقة غير معروفة.")
            _manual = Clamp(i)
        End Sub

        Private Function ExpectedBytes(index As Integer) As Double
            Return If(_observed(index) > 0, _observed(index), FrameLadder.Entries(index).TypicalBytes)
        End Function

        ''' <summary>الطبقة الأعلى التي تتحمّلها الإنتاجية بهامش أمان (أو الأدنى المتاحة).</summary>
        Public Function Sustainable(throughputBytesPerSecond As Double) As Integer
            Dim best As Integer = _available.First()
            For Each a As Integer In _available
                If a > _maxIndex Then Exit For
                Dim need As Double = ExpectedBytes(a) * FrameLadder.Entries(a).MaxFps * Headroom
                If need <= throughputBytesPerSecond Then best = a
            Next
            Return best
        End Function

        ''' <summary>يُستدعى بعد كل إطار ناجح: يتعلّم الحجم الفعلي ويحدّث الطبقة المختارة (تلقائياً فقط).</summary>
        Public Sub OnFrame(tierId As String, bytes As Integer, link As LinkEstimator)
            Dim idx As Integer = FrameLadder.IndexOf(tierId)
            If idx >= 0 AndAlso bytes > 0 Then _observed(idx) = If(_observed(idx) = 0, bytes, _observed(idx) * 0.7 + bytes * 0.3)
            If _manual >= 0 OrElse link.Samples = 0 Then Return
            Dim target As Integer = Sustainable(link.ThroughputBytesPerSecond)
            If target < _current Then
                _current = target                      ' النزول فوري
                _good = 0
            ElseIf target > _current Then
                _good += 1
                If _good >= _upgradeAfter Then
                    _current = NextUp(_current)
                    _good = 0
                End If
            Else
                _good = 0
            End If
        End Sub

        Private Function NextUp(index As Integer) As Integer
            For Each a As Integer In _available
                If a > index AndAlso a <= _maxIndex Then Return a
            Next
            Return index
        End Function

        ''' <summary>إخفاق/مهلة ⇒ نزول طبقة واحدة.</summary>
        Public Sub OnFailure()
            _good = 0
            If _manual >= 0 Then Return
            For i As Integer = _available.Count - 1 To 0 Step -1
                If _available(i) < _current Then
                    _current = _available(i)
                    Return
                End If
            Next
        End Sub
    End Class

End Namespace
