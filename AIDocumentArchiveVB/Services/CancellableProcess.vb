Option Strict On
Imports System
Imports System.Diagnostics
Imports System.Text
Imports System.Threading

Public NotInheritable Class CancellableProcess
    Private Sub New()
    End Sub

    Public Shared Function Run(info As ProcessStartInfo, timeoutMs As Integer, token As CancellationToken) As String
        token.ThrowIfCancellationRequested()
        info.UseShellExecute = False
        info.CreateNoWindow = True
        info.RedirectStandardOutput = True
        info.RedirectStandardError = True
        info.StandardOutputEncoding = Encoding.UTF8
        info.StandardErrorEncoding = Encoding.UTF8
        Using process = Diagnostics.Process.Start(info)
            If process Is Nothing Then Throw New InvalidOperationException("تعذر بدء أداة المعالجة.")
            Dim output = process.StandardOutput.ReadToEndAsync()
            Dim errors = process.StandardError.ReadToEndAsync()
            Dim watch = Stopwatch.StartNew()
            Try
                While Not process.WaitForExit(100)
                    token.ThrowIfCancellationRequested()
                    If watch.ElapsedMilliseconds > timeoutMs Then Throw New TimeoutException("تجاوزت أداة المعالجة الوقت المسموح.")
                End While
                token.ThrowIfCancellationRequested()
                Dim text = output.GetAwaiter().GetResult()
                Dim errorText = errors.GetAwaiter().GetResult()
                If process.ExitCode <> 0 Then Throw New ApplicationException("تعذرت المعالجة: " & errorText)
                Return text
            Finally
                If Not process.HasExited Then
                    Try
                        process.Kill()
                        process.WaitForExit(3000)
                    Catch ex As InvalidOperationException
                    End Try
                End If
            End Try
        End Using
    End Function
End Class
