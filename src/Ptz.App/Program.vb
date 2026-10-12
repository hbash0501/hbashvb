Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.App.Views
Imports Ptz.Core.Security
Imports Ptz.Presentation

''' <summary>نقطة الدخول وجذر التركيب: دخول ← محور ← نوافذ الكاميرات.</summary>
Public Module Program

    <STAThread>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        AddHandler Application.ThreadException, Sub(s, e) ReportFatal(e.Exception)
        AddHandler AppDomain.CurrentDomain.UnhandledException, Sub(s, e) ReportFatal(TryCast(e.ExceptionObject, Exception))

        Dim services As AppServices
        Try
            services = New AppServices()
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                                  TypeOf ex Is Security.Cryptography.CryptographicException
            MessageBox.Show("تعذّر تهيئة مخزن البيانات الآمن: " & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        Do
            Dim session As UserSession = SignIn(services)
            If session Is Nothing Then Exit Do
            If Not RunHub(services, session) Then Exit Do
        Loop
    End Sub

    Private Function SignIn(services As AppServices) As UserSession
        Dim result As UserSession = Nothing
        Using login As New FrmLogin()
            Dim presenter As New LoginPresenter(login, services.Users)
            AddHandler presenter.SessionEstablished, Sub(s) result = s
            If login.ShowDialog() <> DialogResult.OK Then Return Nothing
        End Using
        Return result
    End Function

    ''' <summary>يعيد true إذا طُلب تسجيل الخروج (للعودة لشاشة الدخول)، وfalse لإنهاء التطبيق.</summary>
    Private Function RunHub(services As AppServices, session As UserSession) As Boolean
        Using hub As New FrmHub()
            Dim nav As New ShellNavigator(services, hub)
            Dim presenter As New HubPresenter(hub, session, services.Cameras, services.Access, services.Audit, services.Bus,
                                              services.Decoder, hub.Dispatcher, nav, services.DataDirectory)
            ' انتهاء الجلسة بالخمول → عودة لشاشة الدخول
            Using idle As New Timer With {.Interval = 15000}
                AddHandler idle.Tick, Sub()
                                          If session.IsExpired(DateTime.UtcNow) Then
                                              services.Audit.Write(session.UserName, "Auth.SessionExpired", "", "OK", "")
                                              nav.Logout()
                                          End If
                                      End Sub
                idle.Start()
                Application.Run(hub)
            End Using
            Return nav.LogoutRequested
        End Using
    End Function

    Private Sub ReportFatal(ex As Exception)
        Try
            MessageBox.Show("حدث خطأ غير متوقع: " & If(ex?.Message, "غير معروف"), "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch
            ' لا شيء إضافي يمكن فعله.
        End Try
    End Sub

End Module
