Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.App.Views
Imports Ptz.Core.Security
Imports Ptz.Presentation

''' <summary>نقطة الدخول وجذر التركيب: دخول (محلي أو عبر بوابة) ← محور ← نوافذ الكاميرات.</summary>
Public Module Program

    ''' <summary>نتيجة شاشة الدخول: جلسة محلية، أو اتصال ببوابة، أو إنهاء التطبيق، أو العودة لشاشة الدخول.</summary>
    Private NotInheritable Class SignInResult
        Public Property Local As UserSession
        Public Property Gateway As GatewayHubContext
        Public Property Quit As Boolean
    End Class

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
            Dim result As SignInResult = SignIn(services)
            If result.Quit Then Exit Do
            Dim again As Boolean
            If result.Gateway IsNot Nothing Then
                again = RunGatewayHub(services, result.Gateway)
            ElseIf result.Local IsNot Nothing Then
                again = RunHub(services, result.Local)
            Else
                again = True                      ' أُلغي الدخول البعيد: نعود إلى شاشة الدخول
            End If
            If Not again Then Exit Do
        Loop
    End Sub

    Private Function SignIn(services As AppServices) As SignInResult
        Dim local As UserSession = Nothing
        Dim outcome As DialogResult
        Using login As New FrmLogin()
            Dim presenter As New LoginPresenter(login, services.Users)
            AddHandler presenter.SessionEstablished, Sub(s) local = s
            outcome = login.ShowDialog()
        End Using
        Select Case outcome
            Case DialogResult.OK : Return New SignInResult With {.Local = local}
            Case DialogResult.Retry : Return SignInRemote(services)
            Case Else : Return New SignInResult With {.Quit = True}
        End Select
    End Function

    Private Function SignInRemote(services As AppServices) As SignInResult
        Dim context As GatewayHubContext = Nothing
        Dim ok As Boolean
        Using frm As New FrmGatewayLogin()
            Dim presenter As New GatewayLoginPresenter(frm, New GatewaySettingsStore(services.DataDirectory), New StepUpPrompt())
            AddHandler presenter.Connected, Sub(c) context = c
            ok = (frm.ShowDialog() = DialogResult.OK)
        End Using
        Return If(ok AndAlso context IsNot Nothing, New SignInResult With {.Gateway = context}, New SignInResult())
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

    ''' <summary>المحور في وضع البوابة: الجلسة تنتهي بالخمول محلياً أو بإبطالها من الخادم، وكلاهما يعيدان لشاشة الدخول.</summary>
    Private Function RunGatewayHub(services As AppServices, gateway As GatewayHubContext) As Boolean
        Dim logoutRequested As Boolean
        Using hub As New FrmHub()
            Dim nav As New ShellNavigator(services, hub, gateway.Client)
            Using presenter As New HubPresenter(hub, gateway, services.Access, services.Audit, services.Bus,
                                                services.Decoder, hub.Dispatcher, nav, services.DataDirectory)
                Using idle As New Timer With {.Interval = 15000}
                    AddHandler idle.Tick, Sub()
                                              If gateway.Session.IsExpired(DateTime.UtcNow) Then
                                                  services.Audit.Write(gateway.Session.UserName, "Auth.SessionExpired", "", "OK", "gateway")
                                                  nav.Logout()
                                              End If
                                          End Sub
                    idle.Start()
                    Application.Run(hub)
                End Using
            End Using
            logoutRequested = nav.LogoutRequested
        End Using
        gateway.Client.Dispose()
        Return logoutRequested
    End Function

    Private Sub ReportFatal(ex As Exception)
        Try
            MessageBox.Show("حدث خطأ غير متوقع: " & If(ex?.Message, "غير معروف"), "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch
            ' لا شيء إضافي يمكن فعله.
        End Try
    End Sub

End Module
