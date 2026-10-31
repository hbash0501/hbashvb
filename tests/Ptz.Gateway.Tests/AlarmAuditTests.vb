Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Xunit

Public Class AlarmAuditTests

    Private Const Se As String = "/api/v1/cameras/se7c432mw"

    Private Shared Sub Raise(h As Harness, cam As String, kind As String, severity As AlarmSeverity, Optional message As String = "m", Optional track As Integer = 0)
        h.Svc.Cameras.Find(cam).Bus.Publish(New AlarmEvent With {.Camera = cam, .Kind = kind, .Severity = severity, .Message = message, .TrackId = track})
    End Sub

    Private Shared Function Alarms(h As Harness, token As String, Optional query As String = "") As AlarmListDto
        Dim resp As HttpResponseMessage = h.Send("GET", "/api/v1/alarms" & query, token:=token)
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode)
        Return Harness.Read(Of AlarmListDto)(resp)
    End Function

    <Fact>
    Public Sub Alarms_List_OrdersByTime_FiltersByCamera_AndPagesByCursor()
        Using h As New Harness()
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Raise(h, "se7c432mw", "Intrusion", AlarmSeverity.Critical, "a", 1)
            Raise(h, "se7c432mw", "Loitering", AlarmSeverity.Warning, "b", 2)
            Raise(h, "de7232iw", "Motion", AlarmSeverity.Info, "c", 3)
            WaitFor(Function() Alarms(h, v).Items.Count >= 3)
            Dim all As AlarmListDto = Alarms(h, v)
            Assert.Equal({"a", "b", "c"}, all.Items.Select(Function(a) a.Message).ToArray())
            Assert.True(all.Items.Zip(all.Items.Skip(1), Function(x, y) x.Seq < y.Seq).All(Function(b) b))
            Assert.Equal(all.Items.Last().Seq, all.LastSeq)
            Assert.False(all.Resync)
            Assert.Equal({"a", "b"}, Alarms(h, v, "?camera=se7c432mw").Items.Select(Function(a) a.Message).ToArray())
            Assert.Equal({"c"}, Alarms(h, v, "?afterSeq=" & all.Items(1).Seq).Items.Select(Function(a) a.Message).ToArray())
            Assert.Equal({"c"}, Alarms(h, v, "?limit=1").Items.Select(Function(a) a.Message).ToArray())
            Assert.Equal("critical", all.Items(0).Severity)
            Assert.Equal("se7c432mw", all.Items(0).CameraId)
        End Using
    End Sub

    <Fact>
    Public Sub Alarms_List_RejectsBadParameters_AndRequiresAuth()
        Using h As New Harness()
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", "/api/v1/alarms").StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("GET", "/api/v1/alarms?camera=nope", token:=v).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("GET", "/api/v1/alarms?camera=" & Uri.EscapeDataString("../x"), token:=v).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", "/api/v1/alarms?limit=0", token:=v).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", "/api/v1/alarms?limit=9999", token:=v).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", "/api/v1/alarms?afterSeq=-1", token:=v).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", "/api/v1/alarms?afterSeq=abc", token:=v).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Alarms_AfterSeq_OlderThanTheRing_AsksForResync()
        Using h As New Harness()
            WaitOnline(h)
            Dim v As String = h.Token("view")
            Raise(h, "se7c432mw", "Motion", AlarmSeverity.Critical, "first", 0)
            WaitFor(Function() Alarms(h, v).Items.Count >= 1)
            Dim firstSeq As Long = Alarms(h, v).Items.Single().Seq
            ' مؤشر حديث لا يحتاج مزامنة
            Assert.False(Alarms(h, v, "?afterSeq=" & firstSeq).Resync)
            ' نملأ الحلقة (500) فيُخلى الأول
            For i As Integer = 1 To 520
                Raise(h, "se7c432mw", "Motion", AlarmSeverity.Critical, "n" & i, i)
            Next
            WaitFor(Function() h.Svc.Alarms.EvictedUpToSeq >= firstSeq)
            Dim r As AlarmListDto = Alarms(h, v, "?afterSeq=" & (firstSeq - 1))
            Assert.True(r.Resync)
            Assert.True(r.Items.Count <= 50)
            Assert.Equal("n520", r.Items.Last().Message)
        End Using
    End Sub

    <Fact>
    Public Sub Ack_OperatorCan_ViewerCannot_UnknownIs404_AndItIsAuditedAndShared()
        Using h As New Harness()
            WaitOnline(h)
            Raise(h, "se7c432mw", "Intrusion", AlarmSeverity.Critical, "door", 7)
            Dim op As String = h.Token("alice")
            WaitFor(Function() Alarms(h, op).Items.Count >= 1)
            Dim a As AlarmDto = Alarms(h, op).Items.Single()
            Assert.False(a.Acknowledged)
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("POST", "/api/v1/alarms/" & a.Id.ToString() & "/ack", token:=h.Token("view")).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("POST", "/api/v1/alarms/" & Guid.NewGuid().ToString() & "/ack", token:=op).StatusCode)
            Assert.Equal(HttpStatusCode.NotFound, h.Send("POST", "/api/v1/alarms/not-a-guid/ack", token:=op).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", "/api/v1/alarms/" & a.Id.ToString() & "/ack", token:=op).StatusCode)
            Dim after As AlarmDto = Alarms(h, op).Items.Single()
            Assert.True(after.Acknowledged)
            Assert.Equal("alice", after.AckedBy)
            Assert.Empty(Alarms(h, op, "?unacked=true").Items)
            Assert.Contains(h.Svc.Cameras.Find("se7c432mw").Bus.Recent(10), Function(x) x.Id = a.Id AndAlso x.Acknowledged)
            Dim audit As String = String.Join(vbLf, h.AuditLines())
            Assert.Contains("Alarm.Ack", audit)
            Assert.Contains(a.Id.ToString(), audit)
        End Using
    End Sub

    <Fact>
    Public Sub Alarms_Text_IsSanitized_BeforeLeavingTheGateway()
        Using h As New Harness()
            WaitOnline(h)
            Raise(h, "se7c432mw", "Motion" & vbCrLf & "X", AlarmSeverity.Warning, "ok" & ChrW(&H202E) & "evil" & vbLf & "line2" & New String("z"c, 500), 1)
            Dim v As String = h.Token("view")
            WaitFor(Function() Alarms(h, v).Items.Count >= 1)
            Dim a As AlarmDto = Alarms(h, v).Items.Single()
            Assert.False(a.Message.Contains(ChrW(&H202E)))
            Assert.False(a.Message.Contains(ControlChars.Lf))
            Assert.False(a.Kind.Contains(ControlChars.Lf))
            Assert.True(a.Message.Length <= 200)
        End Using
    End Sub

    <Fact>
    Public Sub Audit_List_IsSupervisorOnly_Pages_Filters_AndMasksInternalAddresses()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("GET", "/api/v1/audit", token:=h.Token("alice")).StatusCode)
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", "/api/v1/audit").StatusCode)
            h.Svc.Audit.Write("alice", "Test.Mask", "se7c432mw", "Fail", "[203.0.113.9] connect to 10.0.0.9:443 failed via 192.168.1.5")
            For i As Integer = 1 To 8
                h.Svc.Audit.Write("alice", "Test.Page", "x", "OK", "i=" & i)
            Next
            Refill(h)
            Dim p1 As AuditListDto = Harness.Read(Of AuditListDto)(h.Send("GET", "/api/v1/audit?limit=5", token:=sup))
            Assert.Equal(5, p1.Items.Count)
            Assert.NotNull(p1.NextBeforeSeq)
            Assert.True(p1.Items.Zip(p1.Items.Skip(1), Function(x, y) x.Seq > y.Seq).All(Function(b) b))
            Refill(h)
            Dim p2 As AuditListDto = Harness.Read(Of AuditListDto)(h.Send("GET", "/api/v1/audit?limit=5&beforeSeq=" & p1.NextBeforeSeq.Value, token:=sup))
            Assert.All(p2.Items, Sub(e) Assert.True(e.Seq < p1.NextBeforeSeq.Value))
            Refill(h)
            Dim byAction As AuditListDto = Harness.Read(Of AuditListDto)(h.Send("GET", "/api/v1/audit?action=Test.Mask", token:=sup))
            Dim masked As AuditItemDto = byAction.Items.Single()
            Assert.StartsWith("[203.0.113.9] ", masked.Details)
            Assert.DoesNotContain("10.0.0.9", masked.Details)
            Assert.DoesNotContain("192.168.1.5", masked.Details)
            Assert.Contains("<addr>", masked.Details)
            Refill(h)
            Assert.All(Harness.Read(Of AuditListDto)(h.Send("GET", "/api/v1/audit?user=ALICE&limit=50", token:=sup)).Items, Sub(e) Assert.Equal("alice", e.User, ignoreCase:=True))
            Refill(h)
            Dim raw As String = Harness.Body(h.Send("GET", "/api/v1/audit?limit=200", token:=sup))
            Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase)
            Refill(h)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("GET", "/api/v1/audit?limit=100000", token:=sup).StatusCode)
            ' الاطلاع على السجل يُدقَّق
            Assert.Contains("Audit.View", String.Join(vbLf, h.AuditLines()))
        End Using
    End Sub

    <Fact>
    Public Sub Audit_Verify_ReportsValid_IsRateLimited_AndDetectsTampering()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("GET", "/api/v1/audit/verify", token:=h.Token("alice")).StatusCode)
            Dim ok As AuditVerifyDto = Harness.Read(Of AuditVerifyDto)(h.Send("GET", "/api/v1/audit/verify", token:=sup))
            Assert.True(ok.IsValid)
            Assert.True(ok.EntriesChecked > 0)
            Assert.False(String.IsNullOrEmpty(ok.LastHash))
            ' التحقق ثقيل: دفعة واحدة ثم انتظار
            Dim again As HttpResponseMessage = h.Send("GET", "/api/v1/audit/verify", token:=sup)
            Assert.Equal(CType(429, HttpStatusCode), again.StatusCode)
            Assert.True(again.Headers.Contains("Retry-After"))

            Dim path As String = IO.Path.Combine(h.Dir, "audit.log")
            Dim lines As List(Of String) = IO.File.ReadAllLines(path).ToList()
            Dim idx As Integer = lines.FindIndex(Function(l) l.Contains("User.Create"))
            Assert.True(idx >= 0)
            lines(idx) = lines(idx).Replace("role=", "rolf=")
            IO.File.WriteAllLines(path, lines)
            h.Clock.Advance(61)
            Dim bad As AuditVerifyDto = Harness.Read(Of AuditVerifyDto)(h.Send("GET", "/api/v1/audit/verify", token:=sup))
            Assert.False(bad.IsValid)
            Assert.True(bad.FirstBadSeq > 0)
        End Using
    End Sub
End Class
