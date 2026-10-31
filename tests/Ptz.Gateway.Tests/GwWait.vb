''' <summary>مساعدات انتظار مشتركة بين اختبارات البوابة (الساعة المزيّفة لا تؤثر على الانتظار الحقيقي).</summary>
Public Module GwWait

    Public Sub WaitFor(cond As Func(Of Boolean), Optional seconds As Double = 5)
        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While Not cond() AndAlso sw.Elapsed.TotalSeconds < seconds
            Threading.Thread.Sleep(10)
        End While
    End Sub

    Public Sub WaitOnline(h As Harness)
        WaitFor(Function() h.Svc.Cameras.Find("se7c432mw").Online AndAlso h.Svc.Cameras.Find("de7232iw").Online)
    End Sub

    ''' <summary>يعيد دلاء الأوامر (5/ث، دفعة 10) بتقديم الساعة المزيّفة.</summary>
    Public Sub Refill(h As Harness)
        h.Clock.Advance(5)
    End Sub

End Module
