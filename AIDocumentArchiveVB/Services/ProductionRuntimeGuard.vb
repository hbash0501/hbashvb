Imports System
Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms

Public NotInheritable Class ProductionRuntimeGuard
    Private Sub New()
    End Sub

    Public Shared Sub Install()
        AddHandler Application.ThreadException, AddressOf OnThreadException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException
    End Sub

    Private Shared Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
        HandleException(e.Exception)
    End Sub

    Private Shared Sub OnUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        If ex IsNot Nothing Then HandleException(ex)
    End Sub

    Public Shared Sub HandleException(ex As Exception)
        Try
            WriteOperationalLog(ex)
        Catch
        End Try

        Try
            MessageBox.Show("حدث خطأ غير متوقع أثناء تنفيذ العملية. تم تسجيل التفاصيل في سجل النظام، ويمكنك إعادة المحاولة أو التواصل مع مسؤول النظام." & Environment.NewLine & Environment.NewLine & "تفاصيل مختصرة: " & SafeMessage(ex),
                            "تنبيه تشغيلي", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch
        End Try
    End Sub

    Private Shared Function SafeMessage(ex As Exception) As String
        If ex Is Nothing OrElse String.IsNullOrWhiteSpace(ex.Message) Then Return "غير متوفر"
        Dim m = ex.Message.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        If m.Length > 220 Then m = m.Substring(0, 220) & "..."
        Return m
    End Function

    Private Shared Sub WriteOperationalLog(ex As Exception)
        Dim baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SaeedAIDocArchive", "Logs")
        Directory.CreateDirectory(baseFolder)
        Dim logPath = Path.Combine(baseFolder, "operational-errors.log")
        Dim sb As New StringBuilder()
        sb.AppendLine("------------------------------------------------------------")
        sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        sb.AppendLine(ex.ToString())
        File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8)
    End Sub
End Class
