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
        If AppServices.Auth.NeedsInitialSetup Then
            Using setup As New FirstPasswordForm()
                setup.Text = "بصيرة | إعداد حساب المدير"
                If setup.ShowDialog() <> DialogResult.OK Then Return
                AppServices.Auth.CreateInitialAdmin(setup.PasswordText)
            End Using
        End If
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
