Imports System.Windows.Forms
Imports Ptz.Presentation

Namespace Infrastructure

    ''' <summary>يمرّر العمل إلى خيط الواجهة. آمن بعد إغلاق النافذة (يتجاهل).</summary>
    Public NotInheritable Class WinFormsDispatcher
        Implements IUiDispatcher

        Private ReadOnly _control As Control

        Public Sub New(control As Control)
            _control = control
        End Sub

        Public Sub Post(action As Action) Implements IUiDispatcher.Post
            Try
                If _control.IsDisposed OrElse Not _control.IsHandleCreated Then Return
                If _control.InvokeRequired Then
                    _control.BeginInvoke(action)
                Else
                    action()
                End If
            Catch ex As ObjectDisposedException
            Catch ex As InvalidOperationException
                ' النافذة أُغلقت أثناء الإرسال.
            End Try
        End Sub
    End Class

End Namespace
