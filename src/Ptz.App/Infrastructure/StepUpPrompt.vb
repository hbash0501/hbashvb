Imports System.Windows.Forms
Imports Ptz.App.Views
Imports Ptz.Core.Remote

Namespace Infrastructure

    ''' <summary>
    ''' يطلب من المستخدم رمز TOTP جديداً عندما تشترط البوابة تحققاً معزَّزاً لإجراء حساس. يُنشأ على خيط الواجهة
    ''' ويعرض النافذة هناك مهما كان الخيط الذي طلبه (الأوامر تُرسل من خيوط خلفية).
    ''' </summary>
    Public NotInheritable Class StepUpPrompt
        Implements IGatewayCredentialProvider

        Private ReadOnly _ui As Threading.SynchronizationContext

        Public Sub New()
            _ui = Threading.SynchronizationContext.Current
            If _ui Is Nothing Then Throw New InvalidOperationException("يجب إنشاء StepUpPrompt على خيط الواجهة.")
        End Sub

        Public Function GetStepUpCodeAsync(reason As String, ct As Threading.CancellationToken) As Task(Of String) Implements IGatewayCredentialProvider.GetStepUpCodeAsync
            Dim tcs As New TaskCompletionSource(Of String)(TaskCreationOptions.RunContinuationsAsynchronously)
            ct.Register(Sub() tcs.TrySetCanceled(ct))
            _ui.Post(Sub(state)
                         Try
                             Using dlg As New FrmStepUp(reason)
                                 Dim owner As IWin32Window = Form.ActiveForm
                                 Dim r As DialogResult = If(owner IsNot Nothing, dlg.ShowDialog(owner), dlg.ShowDialog())
                                 tcs.TrySetResult(If(r = DialogResult.OK, dlg.Code, Nothing))
                             End Using
                         Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ObjectDisposedException
                             tcs.TrySetResult(Nothing)
                         End Try
                     End Sub, Nothing)
            Return tcs.Task
        End Function
    End Class

End Namespace
