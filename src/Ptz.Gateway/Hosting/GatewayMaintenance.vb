Imports Microsoft.Extensions.Hosting

Namespace Hosting

    ''' <summary>
    ''' صيانة دورية (كل 5 ث): يلتقط تغييرات أداة الإدارة في users.json وtotp.json (عملية مستقلة) فتنعكس على الجلسات القائمة،
    ''' يعالج طلبات فك القفل وإبطال الجميع (ملفات في مجلد البيانات)، يفرّغ طابور التدقيق عند التعافي، يزيل الجلسات المنتهية،
    ''' ويكتب مرساة آخر تجزئة تدقيق دورياً (لكشف بتر نهاية السجل).
    ''' </summary>
    Public NotInheritable Class GatewayMaintenance
        Inherits BackgroundService

        Private ReadOnly _svc As GatewayServices
        Private _lastAnchorUtc As DateTime = DateTime.MinValue

        Public Sub New(svc As GatewayServices)
            _svc = svc
        End Sub

        Protected Overrides Async Function ExecuteAsync(stoppingToken As Threading.CancellationToken) As Task
            While Not stoppingToken.IsCancellationRequested
                Try
                    Tick()
                Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    Diagnostics.Debug.WriteLine("maintenance tick failed: " & ex.Message)
                End Try
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    Exit While
                End Try
            End While
            WriteAnchor()
        End Function

        ''' <summary>دورة واحدة (عامة للاختبارات).</summary>
        Public Sub Tick()
            _svc.Users.ReloadIfChanged()
            _svc.Totp.ReloadIfChanged()
            ProcessRequestFiles()
            If Not _svc.AuditHealth.IsHealthy Then _svc.RemoteAudit.TryRecover()
            _svc.Sessions.PurgeExpired()
            _svc.Denied.Flush(_svc.Clock.UtcNow)
            If (_svc.Clock.UtcNow - _lastAnchorUtc).TotalMinutes >= 15 Then WriteAnchor()
        End Sub

        Private Sub ProcessRequestFiles()
            Dim dir As String = _svc.Options.DataDir
            If Not IO.Directory.Exists(dir) Then Return
            ' إبطال كل الجلسات التي أُنشئت قبل ختم revoke.epoch (أمر revoke-all)
            Dim epoch As String = IO.Path.Combine(dir, "revoke.epoch")
            If IO.File.Exists(epoch) Then
                Dim ms As Long
                If Long.TryParse(IO.File.ReadAllText(epoch).Trim(), ms) Then
                    _svc.Sessions.RevokeCreatedBefore(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime)
                End If
            End If
            ' فك قفل الدخول البعيد (الحالة في الذاكرة: تُرسل أداة الإدارة اسم المستخدم في ملف)
            For Each f As String In IO.Directory.GetFiles(dir, "unlock.*.request")
                Dim user As String = IO.Path.GetFileName(f).Substring("unlock.".Length)
                user = user.Substring(0, user.Length - ".request".Length)
                _svc.Throttle.Unlock(user)
                Try
                    IO.File.Delete(f)
                Catch ex As IO.IOException
                End Try
            Next
        End Sub

        Public Sub WriteAnchor()
            _lastAnchorUtc = _svc.Clock.UtcNow
            Dim path As String = _svc.Options.AuditAnchorFile
            If String.IsNullOrWhiteSpace(path) Then Return
            Try
                IO.File.AppendAllText(path, $"{_svc.Clock.UtcNow:o} {_svc.AuditFile.LastHash}{Environment.NewLine}")
            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Diagnostics.Debug.WriteLine("anchor write failed: " & ex.Message)
            End Try
        End Sub
    End Class

End Namespace
