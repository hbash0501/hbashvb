Imports System.Text.RegularExpressions
Imports Ptz.Core.Remote
Imports Ptz.Core.Security
Imports Ptz.Gateway.Cameras
Imports Ptz.Gateway.Hosting

Namespace Api

    ''' <summary>الإنذارات (قائمة/إقرار) وسجل التدقيق (عرض/تحقق). لا حقول تجزئة في الاستجابات.</summary>
    Public NotInheritable Class AlarmAuditRoutes
        Private Sub New()
        End Sub

        Public Shared Sub Register(api As ApiRegistry, svc As GatewayServices)
            api.Map("GET", "/api/v1/alarms", Permission.SmartView, Function(c) ListAlarms(c))
            api.Map("POST", "/api/v1/alarms/{id}/ack", Permission.AckAlarms, Function(c) Ack(c),
                    New RouteOptions With {.Mutates = True, .RateClass = RateClass.Cmd})
            api.Map("GET", "/api/v1/audit", Permission.ViewAudit, Function(c) ListAudit(c), New RouteOptions With {.RateClass = RateClass.AuditList})
            api.Map("GET", "/api/v1/audit/verify", Permission.ViewAudit, Function(c) Verify(c), New RouteOptions With {.RateClass = RateClass.Verify, .TimeoutSeconds = 60})
        End Sub

        Private Shared Function ListAlarms(c As ApiCall) As Task
            Dim hasAfter As Boolean = c.Query("afterSeq") IsNot Nothing
            Dim after As Long = c.QueryLong("afterSeq", 0, 0, Long.MaxValue)
            Dim limit As Integer = CInt(c.QueryLong("limit", 100, 1, 200))
            Dim camId As String = c.Query("camera")
            If camId IsNot Nothing Then
                If Not Regex.IsMatch(camId, "^[a-z0-9-]{1,32}\z") OrElse c.Svc.Cameras.Find(camId) Is Nothing Then Throw ApiException.NotFound()
            End If
            Dim unacked As Boolean = String.Equals(c.Query("unacked"), "true", StringComparison.OrdinalIgnoreCase)
            Dim resync As Boolean
            Dim items As List(Of AlarmDto) = c.Svc.Alarms.Query(after, hasAfter, limit, camId, unacked, resync)
            Return c.Ok(New AlarmListDto With {.Items = items, .LastSeq = c.Svc.Alarms.LastSeq, .Resync = resync})
        End Function

        Private Shared Async Function Ack(c As ApiCall) As Task
            Dim id As Guid
            If Not Guid.TryParse(c.RouteValue("id"), id) Then Throw ApiException.NotFound()
            Dim camId As String = c.Svc.Alarms.TryAck(id, c.UserName)
            If camId Is Nothing Then Throw ApiException.NotFound()
            Dim host As CameraHost = c.Svc.Cameras.Find(camId)
            If host IsNot Nothing Then
                host.Bus.Acknowledge(id)
                host.Events.PublishControl(New SseMessage("ack", Nothing, WireJson.Serialize(New AckWire With {.Id = id, .By = c.UserName})))
            End If
            c.Svc.Audit.Write(c.UserName, "Alarm.Ack", camId, "OK", id.ToString())
            Await c.NoContent().ConfigureAwait(False)
        End Function

        Private Shared ReadOnly AddressRegex As New Regex("\b\d{1,3}(\.\d{1,3}){3}(:\d+)?\b", RegexOptions.Compiled)

        ''' <summary>إخفاء عناوين الشبكة الداخلية من تفاصيل الأخطاء (بعد بادئة السياق التي تحوي IP العميل فقط).</summary>
        Public Shared Function MaskDetails(details As String) As String
            Dim d As String = If(details, "")
            Dim idx As Integer = d.IndexOf("] ", StringComparison.Ordinal)
            If d.StartsWith("[", StringComparison.Ordinal) AndAlso idx > 0 Then
                Return d.Substring(0, idx + 2) & AddressRegex.Replace(d.Substring(idx + 2), "<addr>")
            End If
            Return AddressRegex.Replace(d, "<addr>")
        End Function

        Private Shared Async Function ListAudit(c As ApiCall) As Task
            Dim limit As Integer = CInt(c.QueryLong("limit", 100, 1, 500))
            Dim before As Long = c.QueryLong("beforeSeq", Long.MaxValue, 1, Long.MaxValue)
            Dim userFilter As String = c.Query("user")
            Dim actionFilter As String = c.Query("action")
            Dim all As List(Of AuditEntry) = Await Task.Run(Function() c.Svc.AuditFile.ReadRecent(20000), c.Ct).ConfigureAwait(False)
            Dim q As IEnumerable(Of AuditEntry) = all.Where(Function(e) e.Seq < before)
            If userFilter IsNot Nothing Then q = q.Where(Function(e) String.Equals(e.User, userFilter, StringComparison.OrdinalIgnoreCase))
            If actionFilter IsNot Nothing Then q = q.Where(Function(e) String.Equals(e.Action, actionFilter, StringComparison.Ordinal))
            Dim page As List(Of AuditEntry) = q.Take(limit).ToList()
            Dim dto As New AuditListDto()
            For Each e As AuditEntry In page
                dto.Items.Add(New AuditItemDto With {.Seq = e.Seq, .TimeUtc = e.TimeUtc, .User = e.User, .Action = e.Action, .Target = e.Target, .Outcome = e.Outcome, .Details = MaskDetails(e.Details)})
            Next
            If page.Count = limit Then dto.NextBeforeSeq = page(page.Count - 1).Seq
            c.AuditWrite("Audit.View", "OK", $"n={dto.Items.Count}")
            Await c.Ok(dto).ConfigureAwait(False)
        End Function

        Private Shared Async Function Verify(c As ApiCall) As Task
            Dim cached As Object = Nothing
            Dim now As DateTime = c.Svc.Clock.UtcNow
            If c.Svc.Cache.TryGetValue("audit-verify", cached) Then
                Dim t As Tuple(Of DateTime, AuditVerifyDto) = CType(cached, Tuple(Of DateTime, AuditVerifyDto))
                If now - t.Item1 < TimeSpan.FromSeconds(60) Then
                    Await c.Ok(t.Item2).ConfigureAwait(False)
                    Return
                End If
            End If
            Dim r As AuditVerifyResult = Await Task.Run(Function() c.Svc.AuditFile.Verify(), c.Ct).ConfigureAwait(False)
            Dim dto As New AuditVerifyDto With {.IsValid = r.IsValid, .EntriesChecked = r.EntriesChecked, .FirstBadSeq = r.FirstBadSeq, .SkippedLines = r.SkippedLines, .LastHash = c.Svc.AuditFile.LastHash}
            c.Svc.Cache("audit-verify") = Tuple.Create(now, dto)
            Await c.Ok(dto).ConfigureAwait(False)
        End Function
    End Class

End Namespace
