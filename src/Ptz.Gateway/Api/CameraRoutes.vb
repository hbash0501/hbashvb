Imports Microsoft.AspNetCore.Http
Imports Ptz.Core.Models
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Cameras
Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>مسارات الكاميرا: قائمة/اتصال، PTZ، مواضع/دوريات/أنماط، عدسة، صورة، ملحقات، لقطة/إطار، إعادة تشغيل.</summary>
    Public NotInheritable Class CameraRoutes
        Private Sub New()
        End Sub

        Private Shared ReadOnly PtzRead As RouteOptions = New RouteOptions()

        Public Shared Sub Register(api As ApiRegistry, svc As GatewayServices)
            Dim mut As Func(Of RouteOptions) = Function() New RouteOptions With {.Mutates = True}
            Dim cmd As Func(Of RouteOptions) = Function() New RouteOptions With {.Mutates = True, .RateClass = RateClass.Cmd}

            api.Map("GET", "/api/v1/cameras", Permission.ViewLive, Function(c) ListCameras(c))
            api.Map("GET", "/api/v1/cameras/{cam}", Permission.ViewLive, Function(c) c.Ok(c.Camera.ToDto()))
            api.Map("POST", "/api/v1/cameras/{cam}/connect", Permission.ViewLive, Function(c) Connect(c),
                    New RouteOptions With {.Mutates = True, .RateClass = RateClass.Connect})

            ' --- PTZ ---
            api.Map("POST", "/api/v1/cameras/{cam}/ptz/move", Permission.PtzControl, Function(c) Move(c),
                    New RouteOptions With {.Mutates = True, .RateClass = RateClass.Ptz, .MaxBodyBytes = 1024})
            api.Map("POST", "/api/v1/cameras/{cam}/ptz/stop", Permission.PtzControl, Function(c) [Stop](c),
                    New RouteOptions With {.Mutates = True, .AuditExempt = True, .MaxBodyBytes = 1024})
            api.Map("GET", "/api/v1/cameras/{cam}/ptz/position", Permission.ViewLive, Function(c) GetPosition(c), New RouteOptions With {.RateClass = RateClass.Cmd})
            api.Map("PUT", "/api/v1/cameras/{cam}/ptz/position", Permission.PtzControl, Function(c) PutPosition(c), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/ptz/zoom-area", Permission.PtzControl, Function(c) ZoomArea(c), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/ptz/home", Permission.PtzControl, Function(c) GoHome(c), cmd())
            api.Map("PUT", "/api/v1/cameras/{cam}/ptz/home", Permission.PresetManage, Function(c) SetHome(c), cmd())

            ' --- مواضع/دوريات/أنماط ---
            api.Map("GET", "/api/v1/cameras/{cam}/presets", Permission.PresetUse, Function(c) GetPresets(c), New RouteOptions With {.RateClass = RateClass.Cmd})
            api.Map("PUT", "/api/v1/cameras/{cam}/presets/{n}", Permission.PresetManage, Function(c) SavePreset(c), cmd())
            api.Map("DELETE", "/api/v1/cameras/{cam}/presets/{n}", Permission.PresetManage, Function(c) DeletePreset(c), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/presets/{n}/goto", Permission.PresetUse, Function(c) GoToPreset(c), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/patrols/{n}/start", Permission.PresetUse, Function(c) Patrol(c, True), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/patrols/{n}/stop", Permission.PresetUse, Function(c) Patrol(c, False), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/patterns/{n}/run", Permission.PresetUse, Function(c) Pattern(c, "run"), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/patterns/{n}/stop", Permission.PresetUse, Function(c) Pattern(c, "stop"), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/patterns/{n}/record/start", Permission.PresetManage, Function(c) Pattern(c, "recordStart"), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/patterns/{n}/record/stop", Permission.PresetManage, Function(c) Pattern(c, "recordStop"), cmd())

            ' --- عدسة/صورة/ملحقات ---
            api.Map("POST", "/api/v1/cameras/{cam}/lens/focus", Permission.ImageControl, Function(c) Lens(c, True), cmd())
            api.Map("POST", "/api/v1/cameras/{cam}/lens/iris", Permission.ImageControl, Function(c) Lens(c, False), cmd())
            api.Map("GET", "/api/v1/cameras/{cam}/image", Permission.ViewLive, Function(c) GetImage(c), New RouteOptions With {.RateClass = RateClass.Heavy})
            api.Map("PUT", "/api/v1/cameras/{cam}/image", Permission.ImageControl, Function(c) PutImage(c), cmd())
            api.Map("PUT", "/api/v1/cameras/{cam}/aux/{kind}", Permission.ImageControl, Function(c) PutAux(c), cmd())

            ' --- الفيديو ---
            api.Map("GET", "/api/v1/cameras/{cam}/snapshot", Permission.ViewLive, Function(c) Snapshot(c))
            api.Map("GET", "/api/v1/cameras/{cam}/frame", Permission.ViewLive, Function(c) Frame(c), New RouteOptions With {.TimeoutSeconds = 12})

            ' --- إعادة التشغيل: غير مسجَّل إلا بإذن إداري صريح ---
            If svc.MaxRole = Role.Administrator Then
                api.Map("POST", "/api/v1/cameras/{cam}/device/reboot", Permission.DeviceMaintenance, Function(c) Reboot(c),
                        New RouteOptions With {.Mutates = True, .StepUp = True, .RateClass = RateClass.Cmd, .TimeoutSeconds = 30, .MaxBodyBytes = 1024})
            End If
        End Sub

        Private Shared Function ListCameras(c As ApiCall) As Task
            Dim dto As New CameraListDto()
            For Each h As CameraHost In c.Svc.Cameras.All
                dto.Items.Add(h.ToDto())
            Next
            Return c.Ok(dto)
        End Function

        Private Shared Async Function Connect(c As ApiCall) As Task
            Dim h As CameraHost = c.Camera
            If h.Raw Is Nothing OrElse Not h.Online Then Throw New ApiException(503, "camera_unavailable") With {.RetryAfterSeconds = 2}
            c.AuditWrite("Camera.Connect", "OK", "")
            Dim dev As Ptz.Core.Models.DeviceInfo = h.Device
            Dim canSeeIds As Boolean = c.User.Has(Permission.DeviceMaintenance)
            Await c.Ok(New ConnectResultDto With {
                .Device = New DeviceDto With {.DeviceName = dev.DeviceName, .Model = dev.Model,
                                              .SerialNumber = If(canSeeIds, dev.SerialNumber, ""), .FirmwareVersion = If(canSeeIds, dev.FirmwareVersion, "")},
                .Features = WireMap.FeatureNames(h.Features)}).ConfigureAwait(False)
        End Function

        ' ======================= PTZ =======================
        Private Shared Sub CheckAxis(v As Integer, field As String)
            If v < -100 OrElse v > 100 Then Throw ApiException.BadRequest(field)
        End Sub

        ''' <summary>رقم تسلسلي لكل (جلسة،كاميرا): الأقدم أو المساوي يُرفض 409 (وصل متأخراً بعد أمر أحدث).</summary>
        Private Shared Sub AcceptSeq(c As ApiCall, seq As Long?, strict As Boolean)
            If Not seq.HasValue Then Return
            If seq.Value < 1 Then Throw ApiException.BadRequest("seq")
            SyncLock c.Rec.LastSeq
                Dim last As Long = 0
                c.Rec.LastSeq.TryGetValue(c.Camera.Id, last)
                If seq.Value <= last Then
                    If strict Then Throw New ApiException(409, "stale_seq")
                    Return
                End If
                c.Rec.LastSeq(c.Camera.Id) = seq.Value
            End SyncLock
        End Sub

        Private Shared Async Function Move(c As ApiCall) As Task
            Dim req As MoveRequest = Await c.ReadJsonAsync(Of MoveRequest)().ConfigureAwait(False)
            CheckAxis(req.Pan, "pan")
            CheckAxis(req.Tilt, "tilt")
            CheckAxis(req.Zoom, "zoom")
            Dim opt As PtzOptions = c.Svc.Options.Ptz
            Dim ttl As Integer = If(req.TtlMs, opt.DefaultTtlMs)
            If ttl < opt.MinTtlMs OrElse ttl > opt.MaxTtlMs Then Throw ApiException.BadRequest("ttlMs")
            AcceptSeq(c, req.Seq, True)
            Dim v As New PtzVector(req.Pan, req.Tilt, req.Zoom)
            If v.IsZero Then
                Await DoStop(c).ConfigureAwait(False)
                Await c.NoContent().ConfigureAwait(False)
                Return
            End If
            Dim cam As CameraHost = c.Camera
            Dim ctl As SecuredCameraController = c.Controller
            ' تجديد دوري لعقد لم يعد قائماً (أوقف مشغّل آخر أو انتهى): لا أثر له، ولا يجوز أن يعلّق الكشف الذكي كل 400ms.
            If req.Renew.GetValueOrDefault(False) AndAlso Not cam.Lease.OwnsActive(c.Rec.SessionId) Then
                Await c.NoContent().ConfigureAwait(False)
                Return
            End If
            Dim gen As Integer = cam.StopGeneration
            cam.RegisterLeaseRevoke(c.Rec)
            cam.NotifyMotion(1.0)
            Await cam.PtzGate.WaitAsync(c.Ct).ConfigureAwait(False)
            Try
                If gen = cam.StopGeneration Then          ' إيقاف وصل أثناء الانتظار ⇒ هذه الحركة قديمة
                    If cam.Lease.OnMove(c.Rec.SessionId, v, ttl, opt.ForwardMinIntervalMs, req.Renew.GetValueOrDefault(False)) Then
                        Await ctl.MoveAsync(v, c.Ct).ConfigureAwait(False)
                    End If
                End If
            Finally
                cam.PtzGate.Release()
            End Try
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function DoStop(c As ApiCall) As Task
            Dim cam As CameraHost = c.Camera
            Dim ctl As SecuredCameraController = c.Controller
            cam.BumpStopGeneration()
            cam.NotifyMotion(1.0)
            Await cam.PtzGate.WaitAsync(c.Ct).ConfigureAwait(False)
            Dim failed As Boolean = False
            Try
                cam.Lease.OnStop(c.Rec.SessionId)
                Await ctl.StopAsync(c.Ct).ConfigureAwait(False)
            Catch ex As Exception When TypeOf ex Is Ptz.Core.Isapi.IsapiException OrElse TypeOf ex Is OperationCanceledException
                failed = True
                Throw
            Finally
                cam.PtzGate.Release()
                ' العقد أُلغي أعلاه فلن يطلق إيقافاً: إعادة في الخلفية (الإيقاف لا يُترك لنداء واحد، خاصة عبر السحابة أو عند إغلاق الصفحة).
                If failed Then cam.ScheduleStopRetry()
            End Try
        End Function

        Private Shared Async Function [Stop](c As ApiCall) As Task
            Dim req As StopRequest = Nothing
            If c.Http.Request.ContentLength.GetValueOrDefault(0) > 0 Then req = Await c.ReadJsonAsync(Of StopRequest)().ConfigureAwait(False)
            If req IsNot Nothing Then AcceptSeq(c, req.Seq, False)
            Await DoStop(c).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function GetPosition(c As ApiCall) As Task
            Dim p As PtzPosition
            Await c.Camera.ReadGate.WaitAsync(c.Ct).ConfigureAwait(False)
            Try
                p = Await c.Controller.GetPositionAsync(c.Ct).ConfigureAwait(False)
            Finally
                c.Camera.ReadGate.Release()
            End Try
            Await c.Ok(New PositionDto With {.Azimuth = p.Azimuth, .Elevation = p.Elevation, .Zoom = p.Zoom}).ConfigureAwait(False)
        End Function

        Private Shared Function Finite(v As Double, lo As Double, hi As Double, field As String) As Double
            If Double.IsNaN(v) OrElse Double.IsInfinity(v) OrElse v < lo OrElse v > hi Then Throw ApiException.BadRequest(field)
            Return v
        End Function

        Private Shared Async Function PutPosition(c As ApiCall) As Task
            Dim req As PositionDto = Await c.ReadJsonAsync(Of PositionDto)().ConfigureAwait(False)
            Dim pos As New PtzPosition(Finite(req.Azimuth, 0, 360, "azimuth"), Finite(req.Elevation, -90, 90, "elevation"), Finite(req.Zoom, 1, c.Camera.Profile.OpticalZoomMax, "zoom"))
            c.Camera.NotifyMotion(1.0)
            Await c.Controller.GoToPositionAsync(pos, c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function ZoomArea(c As ApiCall) As Task
            Dim r As ZoomAreaRequest = Await c.ReadJsonAsync(Of ZoomAreaRequest)().ConfigureAwait(False)
            Dim x As Double = Finite(r.X, 0, 1, "x")
            Dim y As Double = Finite(r.Y, 0, 1, "y")
            Dim w As Double = Finite(r.Width, 0.01, 1, "width")
            Dim h As Double = Finite(r.Height, 0.01, 1, "height")
            If x + w > 1.0000001 OrElse y + h > 1.0000001 Then Throw ApiException.BadRequest("width")
            c.Camera.NotifyMotion(1.5)
            Await c.Controller.ZoomToAreaAsync(New NormRect(x, y, w, h), c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function GoHome(c As ApiCall) As Task
            c.Camera.NotifyMotion(1.0)
            Await c.Controller.GoHomeAsync(c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function SetHome(c As ApiCall) As Task
            Await c.Controller.SetHomeAsync(c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        ' ======================= المواضع/الدوريات/الأنماط =======================
        Private Shared Async Function GetPresets(c As ApiCall) As Task
            Dim list As IReadOnlyList(Of PtzPreset)
            Await c.Camera.ReadGate.WaitAsync(c.Ct).ConfigureAwait(False)
            Try
                list = Await c.Controller.GetPresetsAsync(c.Ct).ConfigureAwait(False)
            Finally
                c.Camera.ReadGate.Release()
            End Try
            Dim dto As New PresetListDto()
            For Each p As PtzPreset In list
                dto.Items.Add(New PresetDto With {.Id = p.Id, .Name = AuditSanitizer.Clean(p.Name, 32)})
            Next
            Await c.Ok(dto).ConfigureAwait(False)
        End Function

        Private Shared Async Function SavePreset(c As ApiCall) As Task
            Dim n As Integer = c.RouteInt("n", c.Camera.Profile.MaxPresets)
            Dim req As PresetPutRequest = Await c.ReadJsonAsync(Of PresetPutRequest)().ConfigureAwait(False)
            Await c.Controller.SavePresetAsync(n, WireMap.ValidateName(req.Name, "name"), c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function DeletePreset(c As ApiCall) As Task
            Await c.Controller.DeletePresetAsync(c.RouteInt("n", c.Camera.Profile.MaxPresets), c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function GoToPreset(c As ApiCall) As Task
            Dim n As Integer = c.RouteInt("n", c.Camera.Profile.MaxPresets)
            c.Camera.NotifyMotion(1.0)
            Await c.Controller.GoToPresetAsync(n, c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function Patrol(c As ApiCall, start As Boolean) As Task
            Dim n As Integer = c.RouteInt("n", c.Camera.Profile.MaxPatrols)
            If start Then
                c.Camera.NotifyMotion(1.0)
                Await c.Controller.StartPatrolAsync(n, c.Ct).ConfigureAwait(False)
            Else
                Await c.Controller.StopPatrolAsync(n, c.Ct).ConfigureAwait(False)
            End If
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function Pattern(c As ApiCall, action As String) As Task
            Dim n As Integer = c.RouteInt("n", c.Camera.Profile.MaxPatterns)
            Dim ctl As SecuredCameraController = c.Controller
            Select Case action
                Case "run"
                    c.Camera.NotifyMotion(1.0)
                    Await ctl.RunPatternAsync(n, c.Ct).ConfigureAwait(False)
                Case "stop" : Await ctl.StopPatternAsync(n, c.Ct).ConfigureAwait(False)
                Case "recordStart" : Await ctl.StartPatternRecordingAsync(n, c.Ct).ConfigureAwait(False)
                Case "recordStop" : Await ctl.StopPatternRecordingAsync(n, c.Ct).ConfigureAwait(False)
            End Select
            Await c.NoContent().ConfigureAwait(False)
        End Function

        ' ======================= العدسة/الصورة =======================
        Private Shared Async Function Lens(c As ApiCall, focus As Boolean) As Task
            Dim req As LensRequest = Await c.ReadJsonAsync(Of LensRequest)().ConfigureAwait(False)
            Dim dir As LensDirection = WireMap.ParseLensDirection(req.Direction)
            ' الإيقاف (halt) لا يحتاج سرعة؛ رفضه بسبب السرعة 0 كان يترك العدسة تجري حتى نهايتها.
            If dir <> LensDirection.Halt AndAlso (req.Speed < 1 OrElse req.Speed > 100) Then Throw ApiException.BadRequest("speed")
            Dim kind As String = If(focus, "focus", "iris")
            If dir <> LensDirection.Halt Then c.Camera.ArmLensWatchdog(kind)
            If focus Then
                Await c.Controller.MoveFocusAsync(dir, req.Speed, c.Ct).ConfigureAwait(False)
            Else
                Await c.Controller.MoveIrisAsync(dir, req.Speed, c.Ct).ConfigureAwait(False)
            End If
            If dir = LensDirection.Halt Then c.Camera.DisarmLensWatchdog(kind)
            c.Camera.RegisterLensRevoke(c.Rec)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function GetImage(c As ApiCall) As Task
            Dim s As ImageSettings
            Await c.Camera.ReadGate.WaitAsync(c.Ct).ConfigureAwait(False)
            Try
                s = Await c.Controller.GetImageSettingsAsync(c.Ct).ConfigureAwait(False)
            Finally
                c.Camera.ReadGate.Release()
            End Try
            Dim dto As New ImageDto With {.Values = WireMap.FromImageSettings(s)}
            For Each spec As ImageParamSpec In ImageParamCatalog.All
                If (c.Camera.Features And spec.Feature) = spec.Feature Then dto.Supported.Add(WireMap.ImageKey(spec.Param))
            Next
            Await c.Ok(dto).ConfigureAwait(False)
        End Function

        Private Shared Async Function PutImage(c As ApiCall) As Task
            Dim req As ImagePutRequest = Await c.ReadJsonAsync(Of ImagePutRequest)().ConfigureAwait(False)
            Dim settings As ImageSettings = WireMap.ToImageSettings(req.Values)
            Await c.Controller.ApplyImageSettingsAsync(settings, c.Ct).ConfigureAwait(False)
            c.Camera.NotifyMotion(1.5)               ' تغيّر الإضاءة عالمياً ⇒ إعادة تعلّم الخلفية بلا إنذار كاذب
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared Async Function PutAux(c As ApiCall) As Task
            Dim kind As AuxKind
            Select Case c.RouteValue("kind")
                Case "wiper" : kind = AuxKind.Wiper
                Case "light" : kind = AuxKind.Light
                Case Else : Throw ApiException.NotFound()
            End Select
            Dim req As AuxRequest = Await c.ReadJsonAsync(Of AuxRequest)().ConfigureAwait(False)
            Await c.Controller.SetAuxAsync(kind, req.Enabled, c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function

        ' ======================= الفيديو =======================
        Private Shared Function ResolveTier(c As ApiCall, defaultId As String, allowOrig As Boolean) As FrameTier
            Dim id As String = If(c.Query("tier"), defaultId)
            If allowOrig AndAlso id = "orig" Then Return New FrameTier("orig", Integer.MaxValue, 100, 5)
            Dim t As FrameTier = FrameTier.TryGet(id)
            If t Is Nothing Then Throw ApiException.BadRequest("tier")
            Dim maxIdx As Integer = FrameTier.IndexOf(c.Svc.Options.Frames.MaxTier)
            If FrameTier.IndexOf(t.Id) > maxIdx Then t = FrameTier.Ladder(maxIdx)
            Return t
        End Function

        Private Shared Async Function WriteJpegAsync(c As ApiCall, bytes As Byte(), entry As FrameEntry, tier As FrameTier) As Task
            Dim h As IHeaderDictionary = c.Http.Response.Headers
            h("X-Frame-Seq") = entry.Seq.ToString(Globalization.CultureInfo.InvariantCulture)
            h("X-Frame-Time") = entry.CaptureMs.ToString(Globalization.CultureInfo.InvariantCulture)
            h("X-Frame-Tier") = tier.Id
            If entry.Width > 0 Then
                Dim w As Integer = Math.Min(entry.Width, tier.MaxWidth)
                h("X-Frame-Width") = w.ToString(Globalization.CultureInfo.InvariantCulture)
                h("X-Frame-Height") = Math.Max(1, CInt(Math.Round(entry.Height * (w / CDbl(entry.Width))))).ToString(Globalization.CultureInfo.InvariantCulture)
            End If
            c.Http.Response.StatusCode = 200
            c.Http.Response.ContentType = "image/jpeg"
            c.Http.Response.ContentLength = bytes.Length
            Await c.Http.Response.Body.WriteAsync(bytes, 0, bytes.Length, c.Ct).ConfigureAwait(False)
        End Function

        Private Shared Async Function Snapshot(c As ApiCall) As Task
            Dim tier As FrameTier = ResolveTier(c, "orig", True)
            Dim fresh As Boolean = c.Query("fresh") = "1"
            Dim cam As CameraHost = c.Camera
            If fresh Then
                Dim b As TokenBucket = c.Rec.Bucket("snapshot-fresh", 1, 2, c.Svc.Clock)
                If Not b.TryTake() Then Throw New ApiException(429, "rate_limited") With {.RetryAfterSeconds = Math.Max(1, b.RetryAfterSeconds())}
                Dim jpeg As Byte() = Await c.Controller.GetSnapshotAsync(c.Ct).ConfigureAwait(False)
                c.AuditWrite("Video.Snapshot", "OK", "fresh")
                Dim ent As New FrameEntry(cam.Frames.Seq, New DateTimeOffset(DateTime.SpecifyKind(c.Svc.Clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeMilliseconds(), jpeg, 0, 0)
                Dim outBytes As Byte() = If(tier.Id = "orig", jpeg, Await cam.Frames.GetTierAsync(ent, tier, c.Ct).ConfigureAwait(False))
                Await WriteJpegAsync(c, outBytes, ent, tier).ConfigureAwait(False)
                Return
            End If
            Dim entry As FrameEntry = cam.Frames.Latest()
            Dim age As Long? = cam.Frames.AgeMs()
            If entry Is Nothing OrElse Not age.HasValue OrElse age.Value > 5000 Then Throw New ApiException(503, "camera_unavailable") With {.RetryAfterSeconds = 2}
            Dim bytes As Byte() = Await cam.Frames.GetTierAsync(entry, tier, c.Ct).ConfigureAwait(False)
            Await WriteJpegAsync(c, bytes, entry, tier).ConfigureAwait(False)
        End Function

        Private NotInheritable Class FramePacing
            Public Inflight As Integer
            Public LastSentUtc As DateTime = DateTime.MinValue
        End Class

        Private Shared Async Function Frame(c As ApiCall) As Task
            Dim after As Long = c.QueryLong("after", 0, 0, Long.MaxValue)
            Dim waitMs As Integer = CInt(c.QueryLong("wait", 3000, 0, 5000))
            Dim tier As FrameTier = ResolveTier(c, "mid", False)
            Dim cam As CameraHost = c.Camera
            Dim state As FramePacing = CType(c.Rec.Items.GetOrAdd("fp:" & cam.Id, Function(k) New FramePacing()), FramePacing)
            If Threading.Interlocked.Increment(state.Inflight) > 2 Then
                Threading.Interlocked.Decrement(state.Inflight)
                Throw New ApiException(429, "rate_limited") With {.RetryAfterSeconds = 1}
            End If
            Try
                If after = 0 Then cam.Frames.ForceNext()
                Dim entry As FrameEntry = Await cam.Frames.WaitForNewerAsync(after, waitMs, c.Ct).ConfigureAwait(False)
                If entry Is Nothing Then
                    c.Http.Response.Headers("X-Frame-Seq") = cam.Frames.Seq.ToString(Globalization.CultureInfo.InvariantCulture)
                    c.Http.Response.Headers("X-Camera-State") = If(cam.Online, "online", "offline")
                    Await c.NoContent().ConfigureAwait(False)
                    Return
                End If
                ' تباعد حسب أقصى معدل للطبقة (تخطي الإطارات الوسيطة)
                Dim minGap As Double = 1000.0 / tier.MaxFps
                Dim since As Double = (DateTime.UtcNow - state.LastSentUtc).TotalMilliseconds
                If since < minGap Then
                    Await Task.Delay(CInt(minGap - since), c.Ct).ConfigureAwait(False)
                    entry = If(cam.Frames.Latest(), entry)
                End If
                Dim bytes As Byte() = Await cam.Frames.GetTierAsync(entry, tier, c.Ct).ConfigureAwait(False)
                state.LastSentUtc = DateTime.UtcNow
                Await WriteJpegAsync(c, bytes, entry, tier).ConfigureAwait(False)
            Finally
                Threading.Interlocked.Decrement(state.Inflight)
            End Try
        End Function

        Private Shared Async Function Reboot(c As ApiCall) As Task
            Dim req As RebootRequest = Await c.ReadJsonAsync(Of RebootRequest)().ConfigureAwait(False)
            If req.Confirm <> "REBOOT" Then Throw ApiException.BadRequest("confirm")
            Await c.Controller.RebootAsync(c.Ct).ConfigureAwait(False)
            Await c.NoContent().ConfigureAwait(False)
        End Function
    End Class

End Namespace
