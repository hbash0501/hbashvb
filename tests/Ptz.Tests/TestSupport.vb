Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Vision

''' <summary>ساعة اختبار قابلة للتحكم.</summary>
Public NotInheritable Class TestClock
    Implements IClock
    Public Property Now As DateTime = New DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
    Public ReadOnly Property UtcNow As DateTime Implements IClock.UtcNow
        Get
            Return Now
        End Get
    End Property
    Public Sub Advance(seconds As Double)
        Now = Now.AddSeconds(seconds)
    End Sub
End Class

''' <summary>مولّد إطارات اصطناعية.</summary>
Public NotInheritable Class TestFrames
    Private Sub New()
    End Sub

    Public Shared ReadOnly T0 As New DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)

    Public Shared Function Scene(w As Integer, h As Integer, bg As Byte, t As DateTime, seed As Integer,
                                 ParamArray squares() As (X As Integer, Y As Integer, Size As Integer, Value As Byte)) As GrayFrame
        Dim rnd As New Random(seed)
        Dim px(w * h - 1) As Byte
        For i As Integer = 0 To px.Length - 1
            px(i) = CByte(Math.Max(0, Math.Min(255, bg + rnd.Next(-3, 4))))
        Next
        For Each s In squares
            For y As Integer = s.Y To s.Y + s.Size - 1
                For x As Integer = s.X To s.X + s.Size - 1
                    If x >= 0 AndAlso x < w AndAlso y >= 0 AndAlso y < h Then px(y * w + x) = CByte(Math.Min(255, s.Value + ((x * 31 + y * 17) Mod 30)))
                Next
            Next
        Next
        Return New GrayFrame(w, h, px, t)
    End Function

    ''' <summary>إطار بنسيج متدرّج (للمطابقة): قطعة مضيئة ذات نمط على خلفية غير منتظمة.</summary>
    Public Shared Function Textured(w As Integer, h As Integer, patchX As Integer, patchY As Integer, patchSize As Integer, t As DateTime) As GrayFrame
        Dim px(w * h - 1) As Byte
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                px(y * w + x) = CByte(60 + ((x * 7 + y * 13) Mod 17))
            Next
        Next
        For y As Integer = 0 To patchSize - 1
            For x As Integer = 0 To patchSize - 1
                Dim xx As Integer = patchX + x
                Dim yy As Integer = patchY + y
                If xx >= 0 AndAlso xx < w AndAlso yy >= 0 AndAlso yy < h Then px(yy * w + xx) = CByte(120 + ((x * 31 + y * 17) Mod 100))
            Next
        Next
        Return New GrayFrame(w, h, px, t)
    End Function
End Class

''' <summary>معالج HTTP وهمي يسجّل الطلبات ويعيد ردوداً مبرمجة.</summary>
Public NotInheritable Class FakeCameraHandler
    Inherits HttpMessageHandler

    Public NotInheritable Class Recorded
        Public Property Method As String
        Public Property Path As String
        Public Property Body As String
    End Class

    Public ReadOnly Property Requests As New List(Of Recorded)()
    Private ReadOnly _routes As New Dictionary(Of String, Func(Of Recorded, HttpResponseMessage))()
    Public Property FailFirstNGets As Integer

    Public Sub [On](method As String, path As String, responder As Func(Of Recorded, HttpResponseMessage))
        _routes(method & " " & path) = responder
    End Sub

    Public Sub OnXml(method As String, path As String, xml As String, Optional status As HttpStatusCode = HttpStatusCode.OK)
        [On](method, path, Function(r) New HttpResponseMessage(status) With {.Content = New StringContent(xml, Text.Encoding.UTF8, "application/xml")})
    End Sub

    Public Const OkStatus As String = "<ResponseStatus><statusCode>1</statusCode><statusString>OK</statusString></ResponseStatus>"

    Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
        Dim body As String = If(request.Content Is Nothing, "", Await request.Content.ReadAsStringAsync(cancellationToken))
        Dim rec As New Recorded With {.Method = request.Method.Method, .Path = request.RequestUri.AbsolutePath, .Body = body}
        SyncLock Requests
            Requests.Add(rec)
        End SyncLock
        If FailFirstNGets > 0 AndAlso request.Method = HttpMethod.Get Then
            FailFirstNGets -= 1
            Throw New HttpRequestException("simulated network failure")
        End If
        Dim responder As Func(Of Recorded, HttpResponseMessage) = Nothing
        If _routes.TryGetValue(rec.Method & " " & rec.Path, responder) Then Return responder(rec)
        Return New HttpResponseMessage(HttpStatusCode.NotFound) With {.Content = New StringContent("<ResponseStatus><statusCode>4</statusCode><statusString>Not Support</statusString><subStatusCode>notSupport</subStatusCode></ResponseStatus>")}
    End Function
End Class

Public NotInheritable Class ListAudit
    Implements Ptz.Core.Security.IAuditLog
    Public ReadOnly Property Entries As New List(Of String)()
    Public Sub Write(user As String, action As String, target As String, outcome As String, details As String) Implements Ptz.Core.Security.IAuditLog.Write
        SyncLock Entries
            Entries.Add($"{user}|{action}|{outcome}")
        End SyncLock
    End Sub
End Class

Public NotInheritable Class ScriptedFrameSource
    Implements IFrameSource
    Private ReadOnly _frames As Queue(Of GrayFrame)
    Private ReadOnly _last As GrayFrame
    Public Sub New(frames As IEnumerable(Of GrayFrame))
        _frames = New Queue(Of GrayFrame)(frames)
    End Sub
    Public Function GetFrameAsync(ct As Threading.CancellationToken) As Task(Of GrayFrame) Implements IFrameSource.GetFrameAsync
        If _frames.Count = 0 Then
            Return Task.Delay(Threading.Timeout.Infinite, ct).ContinueWith(Function(t) CType(Nothing, GrayFrame), ct)
        End If
        Return Task.FromResult(_frames.Dequeue())
    End Function
End Class
