Namespace Remote

    ''' <summary>حدث SSE كاملاً (أو تعليق/نبضة).</summary>
    Public NotInheritable Class SseFrame
        Public Property EventName As String = "message"
        Public Property Id As String
        Public Property Data As String = ""
        Public Property IsComment As Boolean
    End Class

    ''' <summary>
    ''' قارئ تدفق text/event-stream (UTF-8، الأسطر تنتهي بـ LF أو CRLF أو CR) بسقف لطول السطر والحدث
    ''' كي لا يستنزف خادمٌ معيب ذاكرة العميل. يعيد Nothing عند نهاية التدفق.
    ''' </summary>
    Public NotInheritable Class SseFrameReader
        Private ReadOnly _stream As IO.Stream
        Private ReadOnly _maxLine As Integer
        Private ReadOnly _maxEvent As Integer
        Private ReadOnly _buf(8191) As Byte
        Private _pos As Integer
        Private _len As Integer
        Private _skipLf As Boolean
        Private ReadOnly _line As New List(Of Byte)()

        Public Sub New(stream As IO.Stream, Optional maxLineBytes As Integer = 262144, Optional maxEventBytes As Integer = 524288)
            _stream = stream
            _maxLine = maxLineBytes
            _maxEvent = maxEventBytes
        End Sub

        ''' <summary>يقرأ سطراً بلا نهايته. Nothing = EOF (سطر ناقص عند الإغلاق يُهمل).</summary>
        Private Async Function ReadLineAsync(ct As Threading.CancellationToken) As Task(Of String)
            _line.Clear()
            While True
                If _pos >= _len Then
                    _len = Await _stream.ReadAsync(_buf, 0, _buf.Length, ct).ConfigureAwait(False)
                    _pos = 0
                    If _len = 0 Then Return Nothing
                End If
                While _pos < _len
                    Dim b As Byte = _buf(_pos)
                    _pos += 1
                    If _skipLf Then
                        _skipLf = False
                        If b = 10 Then Continue While
                    End If
                    If b = 10 Then Return Text.Encoding.UTF8.GetString(_line.ToArray())
                    If b = 13 Then
                        _skipLf = True
                        Return Text.Encoding.UTF8.GetString(_line.ToArray())
                    End If
                    If _line.Count >= _maxLine Then Throw New IO.InvalidDataException("سطر SSE أطول من المسموح.")
                    _line.Add(b)
                End While
            End While
            Return Nothing
        End Function

        Public Async Function ReadFrameAsync(ct As Threading.CancellationToken) As Task(Of SseFrame)
            Dim name As String = Nothing
            Dim id As String = Nothing
            Dim data As New Text.StringBuilder()
            Dim hasData As Boolean = False
            While True
                Dim line As String = Await ReadLineAsync(ct).ConfigureAwait(False)
                If line Is Nothing Then Return Nothing
                If line.Length = 0 Then
                    If hasData Then Return New SseFrame With {.EventName = If(name, "message"), .Id = id, .Data = data.ToString()}
                    name = Nothing
                    id = Nothing
                    Continue While
                End If
                If line(0) = ":"c Then
                    If Not hasData AndAlso name Is Nothing Then Return New SseFrame With {.IsComment = True, .Data = line.Substring(1).TrimStart()}
                    Continue While
                End If
                Dim colon As Integer = line.IndexOf(":"c)
                Dim field As String = If(colon < 0, line, line.Substring(0, colon))
                Dim value As String = If(colon < 0, "", line.Substring(colon + 1))
                If value.StartsWith(" ", StringComparison.Ordinal) Then value = value.Substring(1)
                Select Case field
                    Case "event" : name = value
                    Case "id" : If Not value.Contains(ChrW(0)) Then id = value
                    Case "data"
                        If data.Length + value.Length + 1 > _maxEvent Then Throw New IO.InvalidDataException("حدث SSE أكبر من المسموح.")
                        If hasData Then data.Append(ControlChars.Lf)
                        data.Append(value)
                        hasData = True
                    Case Else
                        ' retry وغيره: يتجاهله العميل (التراجع الأسّي مضبوط محلياً)
                End Select
            End While
            Return Nothing
        End Function
    End Class

End Namespace
