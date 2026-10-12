Imports System.Security.Cryptography
Imports System.Text
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Alarms
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Cameras
Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>مسارات الذكاء: حالة المحرك، عتبات السلوك، المناطق والخطوط (ETag)، قفل الهدف، حفظ نموذج الشذوذ.</summary>
    Public NotInheritable Class SmartRoutes
        Private Sub New()
        End Sub

        Public Shared Sub Register(api As ApiRegistry, svc As GatewayServices)
            api.Map("GET", "/api/v1/cameras/{cam}/smart/state", Permission.SmartView, Function(c) GetState(c))
            api.Map("PUT", "/api/v1/cameras/{cam}/smart/state", Permission.SmartView, Function(c) PutState(c),
                    New RouteOptions With {.Mutates = True, .RateClass = RateClass.Cmd, .MaxBodyBytes = 1024})
            api.Map("GET", "/api/v1/cameras/{cam}/smart/behavior", Permission.SmartView, Function(c) GetBehavior(c))
            api.Map("PUT", "/api/v1/cameras/{cam}/smart/behavior", Permission.SmartConfigure, Function(c) PutBehavior(c),
                    New RouteOptions With {.Mutates = True, .StepUp = True, .RateClass = RateClass.Cmd})
            api.Map("GET", "/api/v1/cameras/{cam}/smart/geometry", Permission.SmartView, Function(c) GetGeometry(c))
            api.Map("PUT", "/api/v1/cameras/{cam}/smart/geometry", Permission.SmartConfigure, Function(c) PutGeometry(c),
                    New RouteOptions With {.Mutates = True, .StepUp = True, .RateClass = RateClass.Cmd, .MaxBodyBytes = 65536})
            api.Map("POST", "/api/v1/cameras/{cam}/smart/target", Permission.AutoTrack, Function(c) LockTarget(c),
                    New RouteOptions With {.Mutates = True, .RateClass = RateClass.Cmd, .MaxBodyBytes = 1024})
            api.Map("DELETE", "/api/v1/cameras/{cam}/smart/target", Permission.AutoTrack, Function(c) ReleaseTarget(c),
                    New RouteOptions With {.Mutates = True, .RateClass = RateClass.Cmd})
            api.Map("POST", "/api/v1/cameras/{cam}/smart/anomaly/save", Permission.SmartConfigure, Function(c) SaveAnomaly(c),
                    New RouteOptions With {.Mutates = True, .RateClass = RateClass.Cmd})
        End Sub

        Private Shared Function EngineOf(c As ApiCall) As Ptz.Core.Engine.SmartEngine
            If c.Camera.Engine Is Nothing Then Throw New ApiException(503, "camera_unavailable")
            Return c.Camera.Engine
        End Function

        ''' <summary>تحذير تشغيلي عند أي تغيير إعداد ذكاء (كاشف أمني لإساءة استخدام جلسة): يظهر في لوحة الإنذارات.</summary>
        Private Shared Sub NoteChange(c As ApiCall, what As String)
            c.Camera.Bus.Publish(New AlarmEvent With {.Camera = c.Camera.DisplayName, .Kind = "System", .Severity = AlarmSeverity.Warning,
                                                      .Message = $"Smart configuration changed ({what}) by {c.UserName}"})
        End Sub

        Private Shared Function GetState(c As ApiCall) As Task
            EngineOf(c)
            Return c.Ok(c.Camera.EngineState())
        End Function

        Private Shared Async Function PutState(c As ApiCall) As Task
            Dim eng = EngineOf(c)
            Dim req As EngineStateDto = Await c.ReadJsonAsync(Of EngineStateDto)().ConfigureAwait(False)
            If Not (req.MotionEnabled.HasValue OrElse req.BehaviorEnabled.HasValue OrElse req.AutoTrackEnabled.HasValue OrElse
                    req.ForceAnomalyLearning.HasValue OrElse req.AutoAnomalyLearning.HasValue) Then Throw ApiException.BadRequest()
            If req.ForceAnomalyLearning.HasValue AndAlso req.AutoAnomalyLearning.GetValueOrDefault(False) Then Throw ApiException.BadRequest("autoAnomalyLearning")
            ' كل الصلاحيات المطلوبة تُفحص قبل تطبيق أي شيء (الكل أو لا شيء)
            If req.MotionEnabled.HasValue OrElse req.BehaviorEnabled.HasValue OrElse req.ForceAnomalyLearning.HasValue OrElse req.AutoAnomalyLearning.HasValue Then
                c.Authorize(Permission.SmartConfigure, "Smart.State.Put")
            End If
            If req.AutoTrackEnabled.HasValue Then c.Authorize(Permission.AutoTrack, "Smart.State.Put")
            Dim weakens As Boolean = (req.MotionEnabled.HasValue AndAlso Not req.MotionEnabled.Value) OrElse
                                     (req.BehaviorEnabled.HasValue AndAlso Not req.BehaviorEnabled.Value) OrElse
                                     (req.ForceAnomalyLearning.HasValue AndAlso req.ForceAnomalyLearning.Value)
            If weakens Then c.RequireStepUp()

            If req.MotionEnabled.HasValue Then eng.MotionEnabled = req.MotionEnabled.Value
            If req.BehaviorEnabled.HasValue Then eng.BehaviorEnabled = req.BehaviorEnabled.Value
            If req.AutoTrackEnabled.HasValue Then eng.AutoTrackEnabled = req.AutoTrackEnabled.Value
            If req.ForceAnomalyLearning.HasValue Then
                eng.ForceAnomalyLearning = req.ForceAnomalyLearning.Value
                c.Camera.OnForceLearningChanged(req.ForceAnomalyLearning)
            ElseIf req.AutoAnomalyLearning.GetValueOrDefault(False) Then
                eng.ForceAnomalyLearning = Nothing
                c.Camera.OnForceLearningChanged(Nothing)
            End If
            c.Camera.PersistEngineState()
            c.Camera.PublishState(c.UserName)
            If weakens Then NoteChange(c, "detection weakened") Else NoteChange(c, "state")
            c.AuditWrite("Smart.State.Put", "OK", WireJson.Serialize(req))
            Await c.Ok(c.Camera.EngineState()).ConfigureAwait(False)
        End Function

        Private Shared Function GetBehavior(c As ApiCall) As Task
            Return c.Ok(WireMap.FromBehavior(EngineOf(c).SnapshotBehaviorOptions()))
        End Function

        Private Shared Async Function PutBehavior(c As ApiCall) As Task
            Dim eng = EngineOf(c)
            Dim req As BehaviorDto = Await c.ReadJsonAsync(Of BehaviorDto)().ConfigureAwait(False)
            eng.ApplyBehaviorOptions(WireMap.ToBehavior(req))
            c.Camera.PersistEngineState()
            NoteChange(c, "behavior")
            c.AuditWrite("Smart.Behavior.Put", "OK", "")
            Await c.Ok(WireMap.FromBehavior(eng.SnapshotBehaviorOptions())).ConfigureAwait(False)
        End Function

        Private Shared Function Etag(rev As String) As String
            Return """" & rev & """"
        End Function

        Private Shared Async Function GetGeometry(c As ApiCall) As Task
            EngineOf(c)
            c.Http.Response.Headers("ETag") = Etag(c.Camera.GeometryRev())
            Await c.Ok(WireMap.FromGeometry(c.Camera.Geometry)).ConfigureAwait(False)
        End Function

        Private Shared Async Function PutGeometry(c As ApiCall) As Task
            EngineOf(c)
            Dim ifMatch As String = c.Http.Request.Headers("If-Match").ToString()
            If String.IsNullOrEmpty(ifMatch) Then Throw New ApiException(428, "precondition_required")
            Dim req As GeometryDto = Await c.ReadJsonAsync(Of GeometryDto)().ConfigureAwait(False)
            Dim geometry As SmartGeometry = WireMap.ToGeometry(req)
            Dim cam As CameraHost = c.Camera
            Dim mismatch As Boolean = False
            Dim newRev As String = Nothing
            Await cam.GeometryGate.WaitAsync(c.Ct).ConfigureAwait(False)
            Try
                If ifMatch.Trim() <> Etag(cam.GeometryRev()) Then
                    mismatch = True
                Else
                    Await cam.SmartConfig.SaveGeometryAsync(geometry, c.Ct).ConfigureAwait(False)
                    cam.SetGeometry(geometry)
                    newRev = cam.GeometryRev()
                End If
            Finally
                cam.GeometryGate.Release()
            End Try
            If mismatch Then
                ' الترويسة عبر ExtraHeaders لأن كتابة الخطأ تمسح ترويسات الاستجابة الحالية
                Dim pf As New ApiException(412, "precondition_failed")
                pf.ExtraHeaders("ETag") = Etag(cam.GeometryRev())
                Throw pf
            End If
            cam.Events.PublishControl(New SseMessage("geo", Nothing, WireJson.Serialize(New GeoWire With {.Rev = newRev, .By = c.UserName})))
            NoteChange(c, "geometry")
            Dim hash As String = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WireJson.Serialize(req)))).Substring(0, 12).ToLowerInvariant()
            c.AuditWrite("Smart.Geometry.Put", "OK", $"zones={req.Zones.Count} lines={req.Lines.Count} masks={req.Masks.Count} sha={hash}")
            c.Http.Response.Headers("ETag") = Etag(newRev)
            Await c.Ok(WireMap.FromGeometry(cam.Geometry)).ConfigureAwait(False)
        End Function

        Private Shared Async Function LockTarget(c As ApiCall) As Task
            Dim eng = EngineOf(c)
            Dim t As TargetRequest = Await c.ReadJsonAsync(Of TargetRequest)().ConfigureAwait(False)
            If Double.IsNaN(t.X) OrElse Double.IsNaN(t.Y) OrElse t.X < 0 OrElse t.X > 1 OrElse t.Y < 0 OrElse t.Y > 1 Then Throw ApiException.BadRequest("x")
            eng.LockTargetAt(New NormPoint(t.X, t.Y))
            c.AuditWrite("Smart.Target.Lock", "OK", $"x={t.X:F2} y={t.Y:F2}")
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Function ReleaseTarget(c As ApiCall) As Task
            EngineOf(c).ReleaseTarget()
            c.AuditWrite("Smart.Target.Release", "OK", "")
            Return c.NoContent()
        End Function

        Private Shared Async Function SaveAnomaly(c As ApiCall) As Task
            EngineOf(c)
            Await c.Camera.SmartConfig.SaveAnomalyModelAsync(c.Ct).ConfigureAwait(False)
            NoteChange(c, "anomaly model saved")
            c.AuditWrite("Smart.Anomaly.Save", "OK", "")
            Await c.NoContent().ConfigureAwait(False)
        End Function
    End Class

End Namespace
