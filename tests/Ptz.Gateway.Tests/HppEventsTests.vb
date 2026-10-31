Imports System.Text.Json
Imports Ptz.Core.Models
Imports Ptz.Gateway.Cloud
Imports Xunit

''' <summary>إنذارات الكاميرا عبر Hik-Partner Pro (دليل §3.6 والملحق A.4): المحلّل، والمضخة على خادم وهمي يعيد الدفعات غير المؤكَّدة.</summary>
Public Class HppEventsTests

    Private Const Se As String = "SE7SERIAL"
    Private Const De As String = "DE7SERIAL"

    Private NotInheritable Class Target
        Implements IHppEventTarget
        Public ReadOnly Property Received As New List(Of HppDeviceEvent)()
        Public ReadOnly Property CloudSerial As String Implements IHppEventTarget.CloudSerial
        Public Property Throws As Boolean

        Public Sub New(serial As String)
            CloudSerial = serial
        End Sub

        Public Sub OnCloudEvent(e As HppDeviceEvent) Implements IHppEventTarget.OnCloudEvent
            SyncLock Received
                Received.Add(e)
            End SyncLock
            If Throws Then Throw New InvalidOperationException("boom")
        End Sub

        Public Function Types() As String()
            SyncLock Received
                Return Received.Select(Function(x) x.EventType).ToArray()
            End SyncLock
        End Function
    End Class

    Private Shared Function Item(json As String) As JsonElement
        Using doc As JsonDocument = JsonDocument.Parse(json)
            Return doc.RootElement.Clone()
        End Using
    End Function

    ' ===================== المحلّل =====================
    <Fact>
    Public Sub Parse_XmlAlarmDataString_Vmd()
        Dim ev As HppDeviceEvent = HppEventParser.Parse(Item(FakeHppCloud.XmlItem(Se, "VMD")))
        Assert.Equal(Se, ev.Serial)
        Assert.Equal("VMD", ev.EventType)
        Assert.Equal(1, ev.ChannelId)
        Assert.Equal("XML", ev.Format)
        Assert.False(ev.Position.HasValue)
    End Sub

    <Fact>
    Public Sub Parse_Intrusion_WithTargetRectAndDetectionTarget()
        Dim inner As String = "<DetectionRegionList><DetectionRegionEntry><regionID>1</regionID><detectionTarget>human</detectionTarget>" &
                              "<TargetRect><X>0.2</X><Y>0.4</Y><width>0.2</width><height>0.2</height></TargetRect></DetectionRegionEntry></DetectionRegionList>"
        Dim ev As HppDeviceEvent = HppEventParser.Parse(Item(FakeHppCloud.XmlItem(Se, "fielddetection", inner)))
        Assert.Equal("fielddetection", ev.EventType)
        Assert.Equal("human", ev.Target)
        Assert.True(ev.Position.HasValue)
        Assert.Equal(0.3, ev.Position.Value.X, 3)
        Assert.Equal(0.5, ev.Position.Value.Y, 3)
    End Sub

    <Fact>
    Public Sub Parse_JsonAlarmDataObject_AndStringifiedJson()
        Dim a As HppDeviceEvent = HppEventParser.Parse(Item(FakeHppCloud.JsonItem(Se, "deviceoffline")))
        Assert.Equal("deviceoffline", a.EventType)
        Dim stringified As String = "{""formatType"":""JSON"",""deviceSerial"":""" & Se & """,""alarmData"":" & JsonSerializer.Serialize("{""eventType"":""deviceonline""}") & "}"
        Assert.Equal("deviceonline", HppEventParser.Parse(Item(stringified)).EventType)
    End Sub

    <Fact>
    Public Sub Parse_SerialFromAlarmData_WhenTheItemOmitsIt()
        Dim json As String = "{""formatType"":""JSON"",""alarmData"":{""deviceSerial"":""" & Se & """,""eventType"":""VMD""}}"
        Assert.Equal(Se, HppEventParser.Parse(Item(json)).Serial)
    End Sub

    Public Shared Iterator Function HostileItems() As IEnumerable(Of Object())
        ' DTD/XXE (المحلّل يمنع DTD)
        Yield {"{""deviceSerial"":""SE7SERIAL"",""alarmData"":" & JsonSerializer.Serialize("<?xml version=""1.0""?><!DOCTYPE x [<!ENTITY e SYSTEM ""file:///etc/passwd"">]><EventNotificationAlert><eventType>&e;</eventType></EventNotificationAlert>") & "}"}
        ' جذر غير متوقع
        Yield {"{""deviceSerial"":""SE7SERIAL"",""alarmData"":" & JsonSerializer.Serialize("<html><eventType>VMD</eventType></html>") & "}"}
        ' بلا نوع
        Yield {"{""deviceSerial"":""SE7SERIAL"",""alarmData"":{""eventType"":""""}}"}
        ' رقم تسلسلي غير صالح (ترويسة/حقن)
        Yield {"{""deviceSerial"":""SE7 SERIAL"",""alarmData"":{""eventType"":""VMD""}}"}
        ' ضخم
        Yield {"{""deviceSerial"":""SE7SERIAL"",""alarmData"":" & JsonSerializer.Serialize(New String("x"c, 70000)) & "}"}
        ' alarmData رقم
        Yield {"{""deviceSerial"":""SE7SERIAL"",""alarmData"":5}"}
        ' ليس كائناً
        Yield {"[1,2]"}
    End Function

    <Theory>
    <MemberData(NameOf(HostileItems))>
    Public Sub Parse_HostileOrUnusableItems_AreIgnored(json As String)
        Assert.Null(HppEventParser.Parse(Item(json)))
    End Sub

    <Fact>
    Public Sub Parse_EventTypeIsSanitized_AndOutOfRangeRectIsDropped()
        Dim inner As String = "<TargetRect><X>2</X><Y>0.4</Y><width>0.2</width><height>0.2</height></TargetRect>"
        Dim ev As HppDeviceEvent = HppEventParser.Parse(Item(FakeHppCloud.XmlItem(Se, "VMD&lt;script&gt;alert(1)&lt;/script&gt;", inner)))
        Assert.Equal("VMDscriptalert1script", ev.EventType)
        Assert.False(ev.Position.HasValue)
    End Sub

    ' ===================== المضخة =====================
    Private NotInheritable Class PumpRig
        Implements IDisposable
        Public ReadOnly Property Clock As New TestClock()
        Public ReadOnly Property Fake As FakeHppCloud
        Public ReadOnly Property Cloud As HppCloud
        Public ReadOnly Property SeTgt As New Target(Se)
        Public ReadOnly Property DeTgt As New Target(De)
        Public ReadOnly Property Pump As HppEventPump
        Public ReadOnly Property Calls As New List(Of HppCallEventArgs)()

        Public Sub New()
            Fake = New FakeHppCloud(Clock)
            Fake.AddDevice(Se)
            Fake.AddDevice(De)
            Cloud = New HppCloud(New HppOptions With {.Enabled = True}, FakeHppCloud.AppKey, FakeHppCloud.SecretKey, Clock, Fake)
            AddHandler Cloud.CallCompleted, Sub(s, e)
                                                SyncLock Calls
                                                    Calls.Add(e)
                                                End SyncLock
                                            End Sub
            Pump = New HppEventPump(Cloud, {SeTgt, DeTgt})
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Pump.Dispose()
            Cloud.Dispose()
        End Sub
    End Class

    Private Shared Sub WithFastPump(action As Action)
        Dim minPoll As TimeSpan = HppEventPump.MinPollInterval
        Dim busy As TimeSpan = HppEventPump.BusyBackoff
        HppEventPump.MinPollInterval = TimeSpan.FromMilliseconds(200)
        HppEventPump.BusyBackoff = TimeSpan.FromMilliseconds(400)
        Try
            action()
        Finally
            HppEventPump.MinPollInterval = minPoll
            HppEventPump.BusyBackoff = busy
        End Try
    End Sub

    <Fact>
    Public Sub Pump_SubscribesWithAListOfOnlyTheConfiguredSerials()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.Fake.AddDevice("OTHER0001")
                             r.Pump.Start()
                             GwWait.WaitFor(Function() r.Pump.IsSubscribed)
                             Dim body As String = r.Fake.Subscriptions.Single()
                             Using doc As JsonDocument = JsonDocument.Parse(body)
                                 Assert.Equal(1, doc.RootElement.GetProperty("subType").GetInt32())
                                 Assert.Equal("list", doc.RootElement.GetProperty("subMode").GetString())        ' لا "all"
                                 Assert.Equal({De, Se}, doc.RootElement.GetProperty("deviceSerialList").EnumerateArray().Select(Function(x) x.GetString()).ToArray())
                             End Using
                             Assert.Empty(r.Fake.Violations)      ' Content-Type: application/json على نداءات mq
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_DeliversToTheRightCamera_IgnoresOtherDevices_AndConfirmsTheBatch()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.Pump.Start()
                             Dim id As String = r.Fake.EnqueueBatch(FakeHppCloud.XmlItem(Se, "VMD"), FakeHppCloud.JsonItem(De, "deviceoffline"), FakeHppCloud.XmlItem("OTHER0001", "VMD"))
                             GwWait.WaitFor(Function() r.Fake.ConfirmedBatches.Contains(id))
                             Assert.Equal({"VMD"}, r.SeTgt.Types())
                             Assert.Equal({"deviceoffline"}, r.DeTgt.Types())
                             Assert.Contains(id, r.Fake.ConfirmedBatches)
                             Assert.Equal(0, r.Fake.PendingBatches())
                             Threading.Thread.Sleep(600)                   ' لا إعادة بعد التأكيد
                             Assert.Single(r.SeTgt.Received)
                             Assert.Equal(2, r.Pump.EventsDelivered)
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_UnconfirmedBatch_IsRedelivered_AtLeastOnce()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.Fake.OffsetCode = "LAP000000"
                             r.Pump.Start()
                             r.Fake.EnqueueBatch(FakeHppCloud.XmlItem(Se, "linedetection"))
                             GwWait.WaitFor(Function() r.SeTgt.Received.Count >= 2)
                             Assert.True(r.SeTgt.Received.Count >= 2, "الدفعة غير المؤكَّدة تُعاد")
                             r.Fake.OffsetCode = Nothing
                             GwWait.WaitFor(Function() r.Fake.PendingBatches() = 0)
                             Assert.Equal(0, r.Fake.PendingBatches())
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_ConcurrentPullRejection_BacksOff_NoHotLoop()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.Fake.MessagesCode = "LAP068001"
                             r.Pump.Start()
                             Threading.Thread.Sleep(1500)
                             Assert.InRange(r.Fake.MessagesCalls, 1, 5)          ' حجب 400 ms بعد كل رفض
                             Assert.Contains(r.Calls, Function(c) c.Serial = "mq" AndAlso c.Code = "LAP068001")
                             r.Fake.MessagesCode = Nothing
                             Dim id As String = r.Fake.EnqueueBatch(FakeHppCloud.XmlItem(Se, "VMD"))
                             GwWait.WaitFor(Function() r.Fake.ConfirmedBatches.Contains(id))
                             Assert.Equal({"VMD"}, r.SeTgt.Types())
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_ImmediateEmptyReplies_RespectTheMinimumInterval()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.Fake.PollHoldMs = 0              ' سحابة لا تحجب: الحلقة يجب ألا تصبح ساخنة
                             r.Pump.Start()
                             Threading.Thread.Sleep(1100)
                             Assert.InRange(r.Fake.MessagesCalls, 1, 8)
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_RejectedToken_IsRefreshedAndPollingContinues()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.Pump.Start()
                             GwWait.WaitFor(Function() r.Fake.MessagesCalls >= 1)
                             r.Fake.ExpireCurrentToken()
                             Dim id As String = r.Fake.EnqueueBatch(FakeHppCloud.XmlItem(Se, "shelteralarm"))
                             GwWait.WaitFor(Function() r.Fake.ConfirmedBatches.Contains(id))
                             Assert.Equal({"shelteralarm"}, r.SeTgt.Types())
                             Assert.True(r.Fake.TokenCalls >= 2)
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_SubscribeFailure_IsRetried()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.Fake.SubscribeCode = "LAP000000"
                             r.Pump.Start()
                             Threading.Thread.Sleep(500)
                             Assert.False(r.Pump.IsSubscribed)
                             Assert.Equal(0, r.Fake.MessagesCalls)
                             r.Fake.SubscribeCode = Nothing
                             GwWait.WaitFor(Function() r.Pump.IsSubscribed, 10)
                             Assert.True(r.Pump.IsSubscribed)
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_AFailingTarget_DoesNotStopDeliveryToOthers()
        WithFastPump(Sub()
                         Using r As New PumpRig()
                             r.SeTgt.Throws = True
                             r.Pump.Start()
                             Dim id As String = r.Fake.EnqueueBatch(FakeHppCloud.XmlItem(Se, "VMD"), FakeHppCloud.XmlItem(De, "VMD"))
                             GwWait.WaitFor(Function() r.Fake.ConfirmedBatches.Contains(id))
                             Assert.Equal({"VMD"}, r.DeTgt.Types())
                         End Using
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_StopsPromptly_EvenDuringALongPoll()
        WithFastPump(Sub()
                         Dim r As New PumpRig()
                         r.Fake.PollHoldMs = 20000
                         r.Pump.Start()
                         GwWait.WaitFor(Function() r.Fake.MessagesCalls >= 1)
                         Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                         r.Pump.StopAsync().GetAwaiter().GetResult()
                         Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), sw.Elapsed.ToString())
                         r.Cloud.Dispose()
                     End Sub)
    End Sub

    <Fact>
    Public Sub Pump_WithNoTargets_NeverStarts()
        Dim clock As New TestClock()
        Dim fake As New FakeHppCloud(clock)
        Using cloud As New HppCloud(New HppOptions(), FakeHppCloud.AppKey, FakeHppCloud.SecretKey, clock, fake)
            Using pump As New HppEventPump(cloud, Array.Empty(Of IHppEventTarget)())
                pump.Start()
                Threading.Thread.Sleep(300)
                Assert.Empty(fake.Requests)
            End Using
        End Using
    End Sub

    <Fact>
    Public Sub GuideSerialFormat_Check()
        Assert.True(CameraTransports.LooksLikeGuideSerial("Q01728482"))
        Assert.False(CameraTransports.LooksLikeGuideSerial("DS-2DE7232IW-AE20210101AAWRF12345678"))
        Assert.False(CameraTransports.LooksLikeGuideSerial("ABCDEFGHIJ"))
    End Sub
End Class
