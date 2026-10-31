Imports System.Text
Imports System.Xml.Linq
Imports Ptz.Core.Models

Namespace Isapi

    ''' <summary>محلّل بث الأحداث الذكية من الكاميرا (multipart): يلتقط كتل EventNotificationAlert.</summary>
    Public NotInheritable Class AlertStreamParser
        Private Const StartTag As String = "<EventNotificationAlert"
        Private Const EndTag As String = "</EventNotificationAlert>"
        Private Const MaxBuffer As Integer = 1024 * 1024
        Private ReadOnly _buf As New StringBuilder()

        Public Function Feed(chunk As String) As List(Of DeviceEvent)
            Dim result As New List(Of DeviceEvent)()
            _buf.Append(chunk)
            If _buf.Length > MaxBuffer Then _buf.Clear() ' حماية من بث تالف/خبيث
            While True
                Dim text As String = _buf.ToString()
                Dim s As Integer = text.IndexOf(StartTag, StringComparison.Ordinal)
                If s < 0 Then
                    If text.Length > StartTag.Length Then _buf.Remove(0, text.Length - StartTag.Length)
                    Exit While
                End If
                Dim e As Integer = text.IndexOf(EndTag, s, StringComparison.Ordinal)
                If e < 0 Then
                    If s > 0 Then _buf.Remove(0, s)
                    Exit While
                End If
                Dim xml As String = text.Substring(s, e + EndTag.Length - s)
                _buf.Remove(0, e + EndTag.Length)
                Dim ev As DeviceEvent = ParseEvent(xml)
                If ev IsNot Nothing Then result.Add(ev)
            End While
            Return result
        End Function

        Public Shared Function ParseEvent(xml As String) As DeviceEvent
            Try
                Dim doc As XDocument = IsapiXml.Parse(xml)
                Dim type As String = If(IsapiXml.ValueOf(doc, "eventType"), "")
                Dim state As String = If(IsapiXml.ValueOf(doc, "eventState"), "")
                ' نبضة الحياة (heartbeat) تأتي كـ videoloss غير نشط.
                If type.Equals("videoloss", StringComparison.OrdinalIgnoreCase) AndAlso state.Equals("inactive", StringComparison.OrdinalIgnoreCase) Then Return Nothing
                Dim ch As Integer
                Integer.TryParse(IsapiXml.ValueOf(doc, "channelID"), Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, ch)
                Dim t As DateTime
                If Not DateTime.TryParse(IsapiXml.ValueOf(doc, "dateTime"), Globalization.CultureInfo.InvariantCulture,
                                         Globalization.DateTimeStyles.AdjustToUniversal Or Globalization.DateTimeStyles.AssumeUniversal, t) Then
                    t = DateTime.UtcNow
                End If
                Return New DeviceEvent With {.EventType = type, .ChannelId = ch, .State = state, .TimeUtc = t,
                                             .Description = If(IsapiXml.ValueOf(doc, "eventDescription"), "")}
            Catch ex As Xml.XmlException
                Return Nothing
            End Try
        End Function
    End Class

    ''' <summary>يستمع لأحداث الكاميرا الذكية مع إعادة اتصال أسّية عند الانقطاع.</summary>
    Public NotInheritable Class AlertStreamClient
        Private ReadOnly _client As IsapiClient

        Public Sub New(client As IsapiClient)
            _client = client
        End Sub

        Public Async Function RunAsync(onEvent As Action(Of DeviceEvent), onError As Action(Of Exception), ct As Threading.CancellationToken) As Task
            Dim backoff As Integer = 1
            While Not ct.IsCancellationRequested
                Try
                    Using stream As IO.Stream = Await _client.OpenStreamAsync(IsapiPaths.AlertStream, ct).ConfigureAwait(False)
                        backoff = 1
                        Dim parser As New AlertStreamParser()
                        Dim decoder As Decoder = Encoding.UTF8.GetDecoder()
                        Dim bytes(4095) As Byte
                        Dim chars(4095) As Char
                        While Not ct.IsCancellationRequested
                            Dim n As Integer = Await stream.ReadAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(False)
                            If n = 0 Then Exit While
                            Dim c As Integer = decoder.GetChars(bytes, 0, n, chars, 0)
                            For Each ev As DeviceEvent In parser.Feed(New String(chars, 0, c))
                                onEvent(ev)
                            Next
                        End While
                    End Using
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                    Exit While
                Catch ex As Exception When TypeOf ex Is IsapiException OrElse TypeOf ex Is IO.IOException OrElse TypeOf ex Is Net.Http.HttpRequestException
                    If onError IsNot Nothing Then onError(ex)
                End Try
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(backoff), ct).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    Exit While
                End Try
                backoff = Math.Min(30, backoff * 2)
            End While
        End Function
    End Class

End Namespace
