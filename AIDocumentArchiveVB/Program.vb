Imports System
Imports System.Windows.Forms

Friend Module Program
    <STAThread()>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        ProductionRuntimeGuard.Install()
        AppServices.Initialize()
        Try
        Using login As New LoginForm()
            If login.ShowDialog() = DialogResult.OK Then
                Application.Run(New BasiraShellForm())
            End If
        End Using
        Finally
            If AppServices.ProcessingQueue IsNot Nothing Then AppServices.ProcessingQueue.Dispose()
        End Try
    End Sub
End Module
