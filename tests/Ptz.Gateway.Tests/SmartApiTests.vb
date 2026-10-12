Imports System.Net
Imports System.Net.Http
Imports Ptz.Core.Remote
Imports Xunit

Public Class SmartApiTests

    Private Const Se As String = "/api/v1/cameras/se7c432mw"

    Private Shared Function State(h As Harness, token As String) As EngineStateDto
        Return Harness.Read(Of EngineStateDto)(h.Send("GET", Se & "/smart/state", token:=token))
    End Function

    Private Shared Function Geo(points As Integer, Optional name As String = "Gate") As GeometryDto
        Dim z As New ZoneDto With {.Name = name, .Severity = "critical"}
        Dim all As (Double, Double)() = {(0.1, 0.1), (0.5, 0.1), (0.5, 0.5), (0.1, 0.5)}
        For i As Integer = 0 To points - 1
            z.Points.Add(New PointDto With {.X = all(i Mod 4).Item1, .Y = all(i Mod 4).Item2})
        Next
        Dim g As New GeometryDto()
        g.Zones.Add(z)
        g.Lines.Add(New LineDto With {.Name = "Door", .Direction = "both", .A = New PointDto With {.X = 0.2, .Y = 0.9}, .B = New PointDto With {.X = 0.8, .Y = 0.9}})
        Return g
    End Function

    <Fact>
    Public Sub State_Get_IsVisibleToViewer_AndRequiresAuth()
        Using h As New Harness()
            WaitOnline(h)
            Assert.Equal(HttpStatusCode.Unauthorized, h.Send("GET", Se & "/smart/state").StatusCode)
            Dim s As EngineStateDto = State(h, h.Token("view"))
            Assert.True(s.MotionEnabled.HasValue)
            Assert.True(s.BehaviorEnabled.HasValue)
            Assert.True(s.AutoTrackEnabled.HasValue)
        End Using
    End Sub

    <Fact>
    Public Sub State_Put_OperatorTogglesAutoTrack_ButNotDetectors_AndIsAllOrNothing()
        Using h As New Harness()
            WaitOnline(h)
            Dim op As String = h.Token("alice")
            Dim before As EngineStateDto = State(h, op)
            Dim flipped As Boolean = Not before.AutoTrackEnabled.Value
            Dim ok As HttpResponseMessage = h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.AutoTrackEnabled = flipped}, op)
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode)
            Assert.Equal(flipped, Harness.Read(Of EngineStateDto)(ok).AutoTrackEnabled)
            Assert.Equal(flipped, h.Svc.Cameras.Find("se7c432mw").Engine.AutoTrackEnabled)
            ' صلاحية ناقصة لجزء من الطلب ⇒ لا يُطبَّق شيء
            Refill(h)
            Dim denied As HttpResponseMessage = h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.AutoTrackEnabled = Not flipped, .MotionEnabled = Not before.MotionEnabled.Value}, op)
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode)
            Assert.Equal(flipped, h.Svc.Cameras.Find("se7c432mw").Engine.AutoTrackEnabled)
            Assert.Equal(before.MotionEnabled, h.Svc.Cameras.Find("se7c432mw").Engine.MotionEnabled)
            ' جسم فارغ
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/smart/state", New EngineStateDto(), op).StatusCode)
            ' المشاهد لا يكتب
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.AutoTrackEnabled = True}, h.Token("view")).StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub State_Weakening_NeedsFreshStepUp_AndRaisesWarningAlarm()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            h.Clock.Advance(6 * 60)                     ' انتهت نافذة التحقق المعزَّز (5 د)، والجلسة ما زالت حيّة
            Dim r1 As HttpResponseMessage = h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.MotionEnabled = False}, sup)
            Assert.Equal(HttpStatusCode.Forbidden, r1.StatusCode)
            Assert.Equal("step_up_required", Harness.ErrorCode(r1))
            Assert.True(h.Svc.Cameras.Find("se7c432mw").Engine.MotionEnabled)
            Assert.Equal(HttpStatusCode.OK, h.Send("POST", "/api/v1/auth/stepup", New StepUpRequest With {.Totp = h.NextCode("sup")}, sup).StatusCode)
            Refill(h)
            Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.MotionEnabled = False}, sup).StatusCode)
            Assert.False(h.Svc.Cameras.Find("se7c432mw").Engine.MotionEnabled)
            ' تمكين الكشف لا يحتاج تحققاً معزَّزاً
            h.Clock.Advance(6 * 60)
            Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.MotionEnabled = True}, sup).StatusCode)
            ' يظهر تحذير تشغيلي في سجل الإنذارات
            Dim alarms As AlarmListDto = Harness.Read(Of AlarmListDto)(h.Send("GET", "/api/v1/alarms?camera=se7c432mw", token:=sup))
            Assert.Contains(alarms.Items, Function(a) a.Kind = "System" AndAlso a.Message.Contains("detection weakened") AndAlso a.Message.Contains("sup"))
            Assert.Contains("Smart.State.Put", String.Join(vbLf, h.AuditLines()))
        End Using
    End Sub

    <Fact>
    Public Sub State_AnomalyLearning_ForceAndAuto_AreMutuallyExclusive()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Dim both As HttpResponseMessage = h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.ForceAnomalyLearning = True, .AutoAnomalyLearning = True}, sup)
            Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode)
            Assert.Equal("autoAnomalyLearning", Harness.Read(Of ErrorEnvelope)(both).Error.Field)
            Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.ForceAnomalyLearning = True}, sup).StatusCode)
            Assert.Equal(True, h.Svc.Cameras.Find("se7c432mw").Engine.ForceAnomalyLearning)
            Refill(h)
            Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.AutoAnomalyLearning = True}, sup).StatusCode)
            Assert.Null(h.Svc.Cameras.Find("se7c432mw").Engine.ForceAnomalyLearning)
        End Using
    End Sub

    <Fact>
    Public Sub State_Persists_AcrossGatewayRestart()
        Dim dir As String
        Using h As New Harness()
            WaitOnline(h)
            dir = h.Dir
            Dim sup As String = h.Token("sup")
            Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/state", New EngineStateDto With {.BehaviorEnabled = False}, sup).StatusCode)
            Dim store As New Ptz.Gateway.Cameras.EngineStateStore(h.Options.DataDir, "se7c432mw")
            Assert.False(store.Load().BehaviorEnabled)
        End Using
    End Sub

    <Fact>
    Public Sub Behavior_GetPut_ValidatesRanges_NeedsSupervisor_AndStepUp()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Dim cur As BehaviorDto = Harness.Read(Of BehaviorDto)(h.Send("GET", Se & "/smart/behavior", token:=sup))
            Assert.True(cur.LoiterSeconds > 0)
            cur.LoiterSeconds = 12.5
            Assert.Equal(HttpStatusCode.OK, h.Send("PUT", Se & "/smart/behavior", cur, sup).StatusCode)
            Assert.Equal(12.5, h.Svc.Cameras.Find("se7c432mw").Engine.SnapshotBehaviorOptions().LoiterSeconds)
            ' خارج النطاق
            Refill(h)
            cur.LoiterSeconds = 0
            Dim bad As HttpResponseMessage = h.Send("PUT", Se & "/smart/behavior", cur, sup)
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode)
            Assert.Equal("loiterSeconds", Harness.Read(Of ErrorEnvelope)(bad).Error.Field)
            Assert.Equal(12.5, h.Svc.Cameras.Find("se7c432mw").Engine.SnapshotBehaviorOptions().LoiterSeconds)
            ' المشغّل لا يضبط
            cur.LoiterSeconds = 5
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("PUT", Se & "/smart/behavior", cur, h.Token("alice")).StatusCode)
            ' تحقق معزَّز منتهٍ
            h.Clock.Advance(6 * 60)
            Dim noStep As HttpResponseMessage = h.Send("PUT", Se & "/smart/behavior", cur, sup)
            Assert.Equal("step_up_required", Harness.ErrorCode(noStep))
        End Using
    End Sub

    <Fact>
    Public Sub Geometry_ETag_PreconditionAndConcurrency()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Dim g0 As HttpResponseMessage = h.Send("GET", Se & "/smart/geometry", token:=sup)
            Assert.Equal(HttpStatusCode.OK, g0.StatusCode)
            Dim etag0 As String = g0.Headers.ETag.Tag
            Assert.StartsWith("""", etag0)
            ' بلا If-Match ⇒ 428
            Assert.Equal(CType(428, HttpStatusCode), h.Send("PUT", Se & "/smart/geometry", Geo(4), sup).StatusCode)
            ' If-Match قديم ⇒ 412 مع ETag الحالي
            Dim stale As HttpResponseMessage = h.Send("PUT", Se & "/smart/geometry", Geo(4), sup, headers:=New Dictionary(Of String, String) From {{"If-Match", """deadbeef"""}})
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode)
            Assert.Equal(etag0, stale.Headers.ETag.Tag)
            ' صحيح
            Dim ok As HttpResponseMessage = h.Send("PUT", Se & "/smart/geometry", Geo(4), sup, headers:=New Dictionary(Of String, String) From {{"If-Match", etag0}})
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode)
            Dim etag1 As String = ok.Headers.ETag.Tag
            Assert.NotEqual(etag0, etag1)
            ' إعادة نفس الـ ETag القديم الآن تفشل (كاتب متزامن)
            Refill(h)
            Assert.Equal(HttpStatusCode.PreconditionFailed, h.Send("PUT", Se & "/smart/geometry", Geo(4, "Other"), sup, headers:=New Dictionary(Of String, String) From {{"If-Match", etag0}}).StatusCode)
            Dim g1 As GeometryDto = Harness.Read(Of GeometryDto)(h.Send("GET", Se & "/smart/geometry", token:=sup))
            Assert.Equal("Gate", g1.Zones.Single().Name)
            Assert.Equal("Door", g1.Lines.Single().Name)
            Assert.Contains("Smart.Geometry.Put", String.Join(vbLf, h.AuditLines()))
        End Using
    End Sub

    <Fact>
    Public Sub Geometry_Put_RejectsInvalidShapes_AndNonSupervisors()
        Using h As New Harness()
            WaitOnline(h)
            Dim sup As String = h.Token("sup")
            Dim etag As String = h.Send("GET", Se & "/smart/geometry", token:=sup).Headers.ETag.Tag
            Dim hdr As New Dictionary(Of String, String) From {{"If-Match", etag}}
            ' مضلع بنقطتين فقط
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/smart/geometry", Geo(2), sup, headers:=hdr).StatusCode)
            ' إحداثيات خارج [0,1]
            Dim off As GeometryDto = Geo(4)
            off.Zones(0).Points(0).X = 1.5
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/smart/geometry", off, sup, headers:=hdr).StatusCode)
            ' خطورة واتجاه مجهولان
            Dim sev As GeometryDto = Geo(4)
            sev.Zones(0).Severity = "apocalyptic"
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/smart/geometry", sev, sup, headers:=hdr).StatusCode)
            Dim dir As GeometryDto = Geo(4)
            dir.Lines(0).Direction = "sideways"
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/smart/geometry", dir, sup, headers:=hdr).StatusCode)
            ' اسم يحوي محارف تحكم
            Dim nm As GeometryDto = Geo(4, "x" & ChrW(&H202E) & "y")
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/smart/geometry", nm, sup, headers:=hdr).StatusCode)
            ' حقل مجهول مرفوض (قراءة صارمة)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("PUT", Se & "/smart/geometry", "{""zones"":[],""lines"":[],""masks"":[],""evil"":1}", sup, headers:=hdr).StatusCode)
            ' مشغّل
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("PUT", Se & "/smart/geometry", Geo(4), h.Token("alice"), headers:=hdr).StatusCode)
            ' لم يتغيّر شيء
            Assert.Equal(etag, h.Send("GET", Se & "/smart/geometry", token:=sup).Headers.ETag.Tag)
        End Using
    End Sub

    <Fact>
    Public Sub Target_LockAndRelease_AreOperatorActions_AndValidated()
        Using h As New Harness()
            WaitOnline(h)
            Dim op As String = h.Token("alice")
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/smart/target", New TargetRequest With {.X = 0.5, .Y = 0.5}, op).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/smart/target", New TargetRequest With {.X = 1.5, .Y = 0.5}, op).StatusCode)
            Assert.Equal(HttpStatusCode.BadRequest, h.Send("POST", Se & "/smart/target", "{""x"":""0.5"",""y"":0.5}", op).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("DELETE", Se & "/smart/target", token:=op).StatusCode)
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("POST", Se & "/smart/target", New TargetRequest With {.X = 0.5, .Y = 0.5}, h.Token("view")).StatusCode)
            Dim audit As String = String.Join(vbLf, h.AuditLines())
            Assert.Contains("Smart.Target.Lock", audit)
            Assert.Contains("Smart.Target.Release", audit)
        End Using
    End Sub

    <Fact>
    Public Sub Anomaly_Save_IsSupervisorOnly()
        Using h As New Harness()
            WaitOnline(h)
            Assert.Equal(HttpStatusCode.Forbidden, h.Send("POST", Se & "/smart/anomaly/save", token:=h.Token("alice")).StatusCode)
            Assert.Equal(HttpStatusCode.NoContent, h.Send("POST", Se & "/smart/anomaly/save", token:=h.Token("sup")).StatusCode)
        End Using
    End Sub
End Class
