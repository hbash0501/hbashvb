Imports Ptz.Core.Abstractions
Imports Ptz.Core.Cameras
Imports Ptz.Core.Models
Imports Ptz.Core.Security
Imports Xunit

''' <summary>صمود سجل التدقيق: شظايا الكتابة المبتورة، كشف العبث رغم التسامح معها، القراءة المحدودة الكلفة، وخنق تدقيق الإيقاف.</summary>
Public Class AuditResilienceTests

    Private Shared ReadOnly Key As Byte() = Enumerable.Repeat(CByte(7), 32).ToArray()

    Private Shared Function TempFile() As String
        Dim d As String = IO.Path.Combine(IO.Path.GetTempPath(), "ptz-audit-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(d)
        Return IO.Path.Combine(d, "audit.log")
    End Function

    Private Shared Function Fill(path As String, n As Integer) As FileAuditLog
        Dim log As New FileAuditLog(path, Key)
        For i As Integer = 1 To n
            log.Write("u", "Act", "t", "OK", "i=" & i)
        Next
        Return log
    End Function

    <Fact>
    Public Sub TornLastLine_DoesNotStopStartup_IsIsolatedOnItsOwnLine_AndTheChainStaysValid()
        Dim path As String = TempFile()
        Fill(path, 5)
        IO.File.AppendAllText(path, "{""Seq"":6,""TimeUtc"":""2026-01-01T00:00:00Z"",""User"":""u"",""Act")         ' كتابة مبتورة بلا سطر جديد
        Dim reopened As FileAuditLog = Nothing
        Assert.Null(Record.Exception(Sub() reopened = New FileAuditLog(path, Key)))
        reopened.Write("u", "After", "t", "OK", "later")
        reopened.Write("u", "After2", "t", "OK", "later2")
        Dim r As AuditVerifyResult = reopened.Verify()
        Assert.True(r.IsValid)
        Assert.Equal(7L, r.EntriesChecked)
        Assert.Equal(1L, r.SkippedLines)
        Assert.Equal("After2", reopened.ReadRecent(1).Single().Action)
        ' إعادة فتح ثانية لا تتعثر أيضاً
        Assert.Equal(7L, New FileAuditLog(path, Key).Verify().EntriesChecked)
    End Sub

    <Fact>
    Public Sub GarbageTail_WithNoValidEntryNearIt_StillRecoversTheLastRealEntry()
        Dim path As String = TempFile()
        Fill(path, 3)
        IO.File.AppendAllText(path, String.Concat(Enumerable.Repeat(ChrW(0) & ChrW(0) & vbLf, 200)))
        Dim reopened As New FileAuditLog(path, Key)
        reopened.Write("u", "Next", "t", "OK", "")
        Dim r As AuditVerifyResult = reopened.Verify()
        Assert.True(r.IsValid)
        Assert.Equal(4L, r.EntriesChecked)
    End Sub

    <Fact>
    Public Sub Tolerance_DoesNotHideTampering_DeletedModifiedOrReplacedEntriesAreStillCaught()
        For Each mutation As Integer In {0, 1, 2}
            Dim path As String = TempFile()
            Fill(path, 6)
            Dim lines As List(Of String) = IO.File.ReadAllLines(path).ToList()
            Select Case mutation
                Case 0 : lines.RemoveAt(2)                                      ' حذف السجل 3
                Case 1 : lines(2) = lines(2).Replace("i=3", "i=9")               ' تعديل
                Case 2 : lines(2) = "garbage that replaced a real entry"        ' استبدال بنص تالف لإخفاء الدليل
            End Select
            IO.File.WriteAllLines(path, lines)
            Dim r As AuditVerifyResult = New FileAuditLog(path, Key).Verify()
            Assert.False(r.IsValid, "mutation " & mutation)
            Assert.Equal(3L, r.FirstBadSeq)
        Next
    End Sub

    <Fact>
    Public Sub ReadRecent_IsNewestFirst_BoundedByCount_AndSkipsGarbage()
        Dim path As String = TempFile()
        Dim log As FileAuditLog = Fill(path, 3000)
        Dim recent As List(Of AuditEntry) = log.ReadRecent(5)
        Assert.Equal({3000L, 2999L, 2998L, 2997L, 2996L}, recent.Select(Function(e) e.Seq).ToArray())
        Assert.Equal(3000, log.ReadRecent(100000).Count)
        Assert.Empty(log.ReadRecent(0))
        IO.File.AppendAllText(path, "junk-1" & vbLf & "junk-2" & vbLf)
        Assert.Equal({3000L, 2999L}, New FileAuditLog(path, Key).ReadRecent(2).Select(Function(e) e.Seq).ToArray())
        Assert.Equal(3000L, New FileAuditLog(path, Key).Verify().EntriesChecked)
    End Sub

    <Fact>
    Public Sub ReadRecent_AndVerify_DoNotBlockWriters()
        Dim path As String = TempFile()
        Dim log As FileAuditLog = Fill(path, 2000)
        Dim stop_ As Integer = 0
        Dim writer As Task = Task.Run(Sub()
                                          While Threading.Volatile.Read(stop_) = 0
                                              log.Write("w", "Concurrent", "t", "OK", "")
                                              Threading.Thread.Sleep(1)
                                          End While
                                      End Sub)
        For i As Integer = 1 To 15
            Assert.True(log.Verify().IsValid, "Verify أثناء كتابة متزامنة (محاولة " & i & ")")
            Assert.Equal(10, log.ReadRecent(10).Count)
        Next
        Threading.Volatile.Write(stop_, 1)
        writer.Wait()
        Assert.True(log.Verify().IsValid)
    End Sub

    <Fact>
    Public Sub StopAudit_IsThrottledToOnePerSecond_ButEveryStopIsStillExecuted()
        Dim h As New FakeCameraHandler()
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/continuous", FakeCameraHandler.OkStatus)
        h.OnXml("PUT", "/ISAPI/PTZCtrl/channels/1/momentary", FakeCameraHandler.OkStatus)
        Dim clock As New TestClock()
        Dim audit As New ListAudit()
        Dim inner As ICameraController = CameraControllerFactory.Create(CameraProfiles.Ds2Se7c432mwAeb,
            New CameraConnection With {.Host = "10.0.0.1", .Username = "u", .Password = "p"}, h)
        Using sec As New SecuredCameraController(inner, New UserSession("op", Role.Operator, clock.UtcNow), New AccessControl(clock), audit, clock)
            For i As Integer = 1 To 50
                sec.StopAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            Next
            Assert.Equal(1, audit.Entries.Where(Function(e) e.Contains("Ptz.Stop")).Count())
            clock.Advance(1.5)
            sec.StopAsync(Threading.CancellationToken.None).GetAwaiter().GetResult()
            Assert.Equal(2, audit.Entries.Where(Function(e) e.Contains("Ptz.Stop")).Count())
        End Using
        SyncLock h.Requests
            Assert.Equal(51, h.Requests.Where(Function(r) r.Method = "PUT" AndAlso r.Path.EndsWith("/continuous")).Count())
        End SyncLock
    End Sub
End Class
