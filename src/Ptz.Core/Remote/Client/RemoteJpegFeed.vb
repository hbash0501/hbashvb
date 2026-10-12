Imports System.Net.Http
Imports Ptz.Core.Abstractions

Namespace Remote

    ''' <summary>
    ''' مصدر لقطات JPEG بعيد: استعلام طويل (long-poll) على /frame مع اختيار تكيّفي للجودة حسب إنتاجية الوصلة.
    ''' إطار واحد في الرحلة دائماً (لا تراكم) فتتدرّج الجودة بدل أن يتأخر العرض على الشبكات الضعيفة.
    ''' </summary>
    Public NotInheritable Class RemoteJpegFeed
        Implements IJpegFeed

        Private ReadOnly _client As GatewayClient
        Private ReadOnly _cameraId As String
        Private ReadOnly _selector As TierSelector
        Private ReadOnly _link As LinkEstimator
        Private _cts As Threading.CancellationTokenSource
        Private _run As Task
        Private _after As Long

        Public Event FrameCaptured As Action(Of Byte()) Implements IJpegFeed.FrameCaptured
        ''' <summary>تعذّر جلب إطار (الوسيط الأول: السبب، والثاني: عدد الإخفاقات المتتالية).</summary>
        Public Event Failed As Action(Of Exception, Integer)

        Public Sub New(client As GatewayClient, cameraId As String, selector As TierSelector, Optional link As LinkEstimator = Nothing)
            _client = client
            _cameraId = cameraId
            _selector = selector
            _link = If(link, New LinkEstimator())
        End Sub

        Public ReadOnly Property Tiers As TierSelector
            Get
                Return _selector
            End Get
        End Property

        Public ReadOnly Property Link As LinkEstimator
            Get
                Return _link
            End Get
        End Property

        Public Sub Start(ct As Threading.CancellationToken)
            If _run IsNot Nothing Then Return
            _cts = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
            Dim token As Threading.CancellationToken = _cts.Token
            _run = Task.Run(Function() LoopAsync(token))
        End Sub

        Public Async Function StopAsync() As Task
            Dim cts As Threading.CancellationTokenSource = _cts
            Dim run As Task = _run
            _cts = Nothing
            _run = Nothing
            If cts Is Nothing Then Return
            cts.Cancel()
            Try
                If run IsNot Nothing Then Await run.ConfigureAwait(False)
            Catch ex As OperationCanceledException
            End Try
            cts.Dispose()
        End Function

        Private Async Function LoopAsync(ct As Threading.CancellationToken) As Task
            Dim failures As Integer = 0
            While Not ct.IsCancellationRequested
                Dim tier As String = _selector.Current
                Dim delaySeconds As Double = 0
                Dim ended As Boolean = False
                Try
                    Dim res = Await _client.TryRequestAsync(HttpMethod.Get, $"/api/v1/cameras/{_cameraId}/frame?tier={tier}&after={_after}&wait=3000", Nothing, ct, timeoutSeconds:=12).ConfigureAwait(False)
                    If res.Error Is Nothing Then
                        failures = 0
                        HandleResponse(res.Response, tier)
                    Else
                        failures += 1
                        _link.RecordFailure()
                        _selector.OnFailure()
                        RaiseEvent Failed(res.Error, failures)
                        If res.Error.IsSessionEnded Then
                            ended = True
                        ElseIf res.Error.Status = 429 Then
                            delaySeconds = Math.Max(1, If(res.Error.RetryAfterSeconds, 1))
                        ElseIf res.Error.Status = 404 Then
                            delaySeconds = 5
                        Else
                            delaySeconds = Backoff(failures)
                        End If
                    End If
                Catch ex As OperationCanceledException
                    Exit While
                Catch ex As GatewayUnreachableException
                    If ct.IsCancellationRequested Then Exit While
                    failures += 1
                    _link.RecordFailure()
                    _selector.OnFailure()
                    RaiseEvent Failed(ex, failures)
                    delaySeconds = Backoff(failures)
                End Try
                If ended Then Exit While
                If delaySeconds > 0 Then
                    Try
                        Await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct).ConfigureAwait(False)
                    Catch ex As OperationCanceledException
                        Exit While
                    End Try
                End If
            End While
        End Function

        Private Shared Function Backoff(failures As Integer) As Double
            Return Math.Min(10.0, 0.5 * Math.Pow(2, Math.Min(failures, 5)))
        End Function

        Private Sub HandleResponse(r As GatewayResponse, tier As String)
            Dim seq As Long
            Dim hdr As String = r.Header("X-Frame-Seq")
            Dim hasSeq As Boolean = hdr IsNot Nothing AndAlso Long.TryParse(hdr, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, seq)
            If r.Status = 204 Then
                ' لا إطار جديد. إن رجع عدّاد الخادم للوراء (إعادة تشغيل) نبدأ من الصفر كي لا ننتظر للأبد.
                If hasSeq AndAlso seq < _after Then _after = 0
                Return
            End If
            If r.Body.Length < 4 OrElse r.Body(0) <> &HFF OrElse r.Body(1) <> &HD8 Then Return
            If hasSeq Then
                If seq = _after Then Return
                _after = seq
            End If
            Dim served As String = r.Header("X-Frame-Tier")
            If served IsNot Nothing AndAlso served <> tier Then _selector.LimitTo(served)
            _link.RecordFrame(r.Body.Length, r.BodyMs)
            _selector.OnFrame(tier, r.Body.Length, _link)
            RaiseEvent FrameCaptured(r.Body)
        End Sub
    End Class

End Namespace
