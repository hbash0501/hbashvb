Imports System.Threading
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Remote
Imports Ptz.Core.Vision

Namespace Cameras

    ''' <summary>طبقة جودة للبث البعيد.</summary>
    Public NotInheritable Class FrameTier
        Public ReadOnly Property Id As String
        Public ReadOnly Property MaxWidth As Integer
        Public ReadOnly Property Quality As Integer
        Public ReadOnly Property MaxFps As Double

        Public Sub New(id As String, maxWidth As Integer, quality As Integer, maxFps As Double)
            Me.Id = id
            Me.MaxWidth = maxWidth
            Me.Quality = quality
            Me.MaxFps = maxFps
        End Sub

        Public Shared ReadOnly Ladder As IReadOnlyList(Of FrameTier) =
            FrameLadder.Entries.Select(Function(e) New FrameTier(e.Id, e.MaxWidth, e.Quality, e.MaxFps)).ToList()

        Public Shared Function TryGet(id As String) As FrameTier
            Return Ladder.FirstOrDefault(Function(t) String.Equals(t.Id, id, StringComparison.Ordinal))
        End Function

        Public Shared Function IndexOf(id As String) As Integer
            For i As Integer = 0 To Ladder.Count - 1
                If Ladder(i).Id = id Then Return i
            Next
            Return Ladder.Count - 1
        End Function
    End Class

    ''' <summary>إطار منشور: JPEG الأصلي + رقمه وزمن التقاطه.</summary>
    Public NotInheritable Class FrameEntry
        Public ReadOnly Property Seq As Long
        Public ReadOnly Property CaptureMs As Long
        Public ReadOnly Property Jpeg As Byte()
        Public ReadOnly Property Width As Integer
        Public ReadOnly Property Height As Integer
        Friend ReadOnly Cache As New Dictionary(Of String, Byte())()

        Public Sub New(seq As Long, captureMs As Long, jpeg As Byte(), width As Integer, height As Integer)
            Me.Seq = seq
            Me.CaptureMs = captureMs
            Me.Jpeg = jpeg
            Me.Width = width
            Me.Height = height
        End Sub
    End Class

    ''' <summary>
    ''' مركز اللقطات لكاميرا واحدة: لقطة واحدة من الكاميرا لكل دورة تحليل مهما بلغ عدد المشاهدين. يحتفظ بآخر إطار منشور فقط،
    ''' ويحجب الإطارات الساكنة (تقليل النطاق الترددي) ويحوّل لطبقات جودة عند الطلب مع تخزين مؤقت لكل (إطار، طبقة).
    ''' </summary>
    Public NotInheritable Class FrameHub
        Private ReadOnly _opt As FrameOptions
        Private ReadOnly _transcoder As IJpegTranscoder
        Private ReadOnly _clock As IClock
        Private ReadOnly _recentPtz As Func(Of Boolean)
        Private ReadOnly _lock As New Object()
        Private ReadOnly _transcodeGate As New SemaphoreSlim(2, 2)
        Private _entry As FrameEntry
        Private _seq As Long
        Private _lastGray As Byte()
        Private _lastPublishUtc As DateTime = DateTime.MinValue
        Private _hadTracks As Boolean
        Private _force As Boolean = True
        Private _waiter As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)

        Public Sub New(options As FrameOptions, transcoder As IJpegTranscoder, clock As IClock, recentPtzActivity As Func(Of Boolean))
            _opt = options
            _transcoder = transcoder
            _clock = clock
            _recentPtz = recentPtzActivity
        End Sub

        Public ReadOnly Property Seq As Long
            Get
                SyncLock _lock
                    Return _seq
                End SyncLock
            End Get
        End Property

        Public Function Latest() As FrameEntry
            SyncLock _lock
                Return _entry
            End SyncLock
        End Function

        ''' <summary>عمر آخر إطار منشور بالميلي ثانية (Nothing إن لم يُنشر شيء).</summary>
        Public Function AgeMs() As Long?
            SyncLock _lock
                If _entry Is Nothing Then Return Nothing
                Return CLng((_clock.UtcNow - _lastPublishUtc).TotalMilliseconds)
            End SyncLock
        End Function

        Public Sub NotifyResult(hasTracks As Boolean)
            SyncLock _lock
                _hadTracks = hasTracks
            End SyncLock
        End Sub

        ''' <summary>يفرض نشر الإطار التالي (مشترك جديد، تغيّر إعدادات الصورة، بعد توقف PTZ).</summary>
        Public Sub ForceNext()
            SyncLock _lock
                _force = True
            End SyncLock
        End Sub

        ''' <summary>يُستدعى من فاكّ الترميز لكل إطار؛ يقرر النشر.</summary>
        Public Sub Offer(jpeg As Byte(), gray As GrayFrame)
            Dim now As DateTime = _clock.UtcNow
            Dim publish As Boolean
            Dim tcs As TaskCompletionSource(Of Boolean) = Nothing
            SyncLock _lock
                publish = Not _opt.GatingEnabled OrElse _force OrElse _hadTracks OrElse _entry Is Nothing OrElse
                          (now - _lastPublishUtc).TotalSeconds >= _opt.IdleRefreshSeconds OrElse _recentPtz() OrElse
                          MeanAbsDiff(_lastGray, gray.Pixels) >= _opt.ChangeThreshold
                If Not publish Then Return
                _force = False
                _seq += 1
                Dim w As Integer = 0
                Dim h As Integer = 0
                JpegInfo.TryGetSize(jpeg, w, h)
                _entry = New FrameEntry(_seq, New DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeMilliseconds(), jpeg, w, h)
                _lastGray = gray.Pixels
                _lastPublishUtc = now
                tcs = _waiter
                _waiter = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            End SyncLock
            tcs.TrySetResult(True)
        End Sub

        Private Shared Function MeanAbsDiff(a As Byte(), b As Byte()) As Double
            If a Is Nothing OrElse b Is Nothing OrElse a.Length <> b.Length Then Return 255.0
            Dim sum As Long = 0
            Dim n As Integer = 0
            For i As Integer = 0 To a.Length - 1 Step 4
                sum += Math.Abs(CInt(a(i)) - CInt(b(i)))
                n += 1
            Next
            Return If(n = 0, 0.0, sum / CDbl(n))
        End Function

        ''' <summary>ينتظر إطاراً أحدث من <paramref name="after"/>؛ يعيد Nothing عند انقضاء المهلة.</summary>
        Public Async Function WaitForNewerAsync(after As Long, waitMs As Integer, ct As CancellationToken) As Task(Of FrameEntry)
            Dim deadline As DateTime = DateTime.UtcNow.AddMilliseconds(waitMs)
            While True
                Dim waiter As Task
                SyncLock _lock
                    If _entry IsNot Nothing AndAlso _entry.Seq > after Then Return _entry
                    waiter = _waiter.Task
                End SyncLock
                Dim remain As Integer = CInt((deadline - DateTime.UtcNow).TotalMilliseconds)
                If remain <= 0 Then Return Nothing
                Dim delay As Task = Task.Delay(remain, ct)
                Await Task.WhenAny(waiter, delay).ConfigureAwait(False)
                ct.ThrowIfCancellationRequested()
            End While
            Return Nothing
        End Function

        ''' <summary>JPEG الإطار بطبقة جودة؛ يُحوَّل مرة واحدة لكل (إطار، طبقة) ويُخزَّن لذلك الإطار فقط.</summary>
        Public Async Function GetTierAsync(entry As FrameEntry, tier As FrameTier, ct As CancellationToken) As Task(Of Byte())
            If tier.Id = "orig" Then Return entry.Jpeg
            SyncLock _lock
                Dim cached As Byte() = Nothing
                If entry.Cache.TryGetValue(tier.Id, cached) Then Return cached
            End SyncLock
            Await _transcodeGate.WaitAsync(ct).ConfigureAwait(False)
            Dim bytes As Byte()
            Try
                bytes = Await Task.Run(Function() _transcoder.Resize(entry.Jpeg, tier.MaxWidth, tier.Quality), ct).ConfigureAwait(False)
            Finally
                _transcodeGate.Release()
            End Try
            SyncLock _lock
                entry.Cache(tier.Id) = bytes
            End SyncLock
            Return bytes
        End Function
    End Class

    ''' <summary>قراءة أبعاد JPEG من ترويسة SOF دون فك الصورة.</summary>
    Public NotInheritable Class JpegInfo
        Private Sub New()
        End Sub

        Public Shared Function TryGetSize(jpeg As Byte(), ByRef width As Integer, ByRef height As Integer) As Boolean
            If jpeg Is Nothing OrElse jpeg.Length < 4 OrElse jpeg(0) <> &HFF OrElse jpeg(1) <> &HD8 Then Return False
            Dim i As Integer = 2
            While i + 9 < jpeg.Length
                If jpeg(i) <> &HFF Then
                    i += 1
                    Continue While
                End If
                Dim marker As Integer = jpeg(i + 1)
                If marker = &HFF Then
                    i += 1
                    Continue While
                End If
                If marker = &HD8 OrElse (marker >= &HD0 AndAlso marker <= &HD7) OrElse marker = &H1 Then
                    i += 2
                    Continue While
                End If
                Dim segLen As Integer = (CInt(jpeg(i + 2)) << 8) Or jpeg(i + 3)
                If (marker >= &HC0 AndAlso marker <= &HCF) AndAlso marker <> &HC4 AndAlso marker <> &HC8 AndAlso marker <> &HCC Then
                    height = (CInt(jpeg(i + 5)) << 8) Or jpeg(i + 6)
                    width = (CInt(jpeg(i + 7)) << 8) Or jpeg(i + 8)
                    Return width > 0 AndAlso height > 0
                End If
                i += 2 + segLen
            End While
            Return False
        End Function
    End Class

End Namespace
