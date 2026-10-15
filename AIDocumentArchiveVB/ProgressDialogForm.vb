Imports System
Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class ProgressDialogForm
    Private _work As Func(Of ProgressDialogForm, Object)
    Private _result As Object
    Private _error As Exception
    Private _cancelRequested As Boolean
    Private _startedAt As DateTime
    Private _useSta As Boolean

    Public ReadOnly Property IsCancelRequested As Boolean
        Get
            Return _cancelRequested
        End Get
    End Property

    Public Sub New()
        InitializeComponent()
        _startedAt = DateTime.Now
    End Sub

    Public Shared Function Run(Of T)(owner As IWin32Window, title As String, message As String, work As Func(Of ProgressDialogForm, T)) As T
        Return RunCore(owner, title, message, work, False)
    End Function

    Public Shared Function RunSta(Of T)(owner As IWin32Window, title As String, message As String, work As Func(Of ProgressDialogForm, T)) As T
        Return RunCore(owner, title, message, work, True)
    End Function

    Private Shared Function RunCore(Of T)(owner As IWin32Window, title As String, message As String, work As Func(Of ProgressDialogForm, T), useSta As Boolean) As T
        Using dlg As New ProgressDialogForm()
            dlg._useSta = useSta
            If useSta Then
                dlg.btnCancel.Visible = False
                dlg.lblHint.Text = "للإلغاء استخدم زر إلغاء في نافذة السكانر."
            End If
            dlg.lblTitle.Text = If(String.IsNullOrWhiteSpace(title), "جاري تنفيذ العملية", title)
            dlg.lblMessage.Text = If(String.IsNullOrWhiteSpace(message), "يرجى الانتظار...", message)
            dlg._work = Function(ctx As ProgressDialogForm) CType(work(ctx), Object)
            If owner Is Nothing Then
                dlg.ShowDialog()
            Else
                dlg.ShowDialog(owner)
            End If
            If dlg._error IsNot Nothing Then Throw dlg._error
            If dlg._result Is Nothing Then Return Nothing
            Return CType(dlg._result, T)
        End Using
    End Function

    Public Shared Sub Run(owner As IWin32Window, title As String, message As String, work As Action(Of ProgressDialogForm))
        Run(Of Object)(owner, title, message,
                       Function(ctx As ProgressDialogForm)
                           work(ctx)
                           Return Nothing
                       End Function)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        _startedAt = DateTime.Now
        timerElapsed.Start()
        If _useSta Then
            Dim worker As New Thread(AddressOf ExecuteWork) With {.IsBackground = True, .Name = "Basira-WIA"}
            worker.SetApartmentState(ApartmentState.STA)
            worker.Start()
        Else
            Task.Factory.StartNew(AddressOf ExecuteWork)
        End If
    End Sub

    Private Sub ExecuteWork()
        Try
            If _work IsNot Nothing Then _result = _work(Me)
        Catch ex As Exception
            _error = ex
        Finally
            SafeCloseFromWorker()
        End Try
    End Sub

    Private Sub SafeCloseFromWorker()
        Try
            If Me.IsHandleCreated AndAlso Not Me.IsDisposed Then
                Me.BeginInvoke(New MethodInvoker(Sub()
                                                     timerElapsed.Stop()
                                                     If _cancelRequested Then
                                                         Me.DialogResult = DialogResult.Cancel
                                                     ElseIf _error Is Nothing Then
                                                         Me.DialogResult = DialogResult.OK
                                                     Else
                                                         Me.DialogResult = DialogResult.Abort
                                                     End If
                                                     Me.Close()
                                                 End Sub))
            End If
        Catch
        End Try
    End Sub

    Public Sub Report(message As String, Optional percent As Integer = -1)
        Try
            If Me.IsDisposed Then Return
            If Me.InvokeRequired Then
                Me.BeginInvoke(New MethodInvoker(Sub() Report(message, percent)))
                Return
            End If
            If Not String.IsNullOrWhiteSpace(message) Then lblMessage.Text = message
            If percent >= 0 Then
                If percent > 100 Then percent = 100
                progressMain.Style = ProgressBarStyle.Continuous
                progressMain.Value = percent
                lblPercent.Text = percent.ToString() & "%"
            Else
                progressMain.Style = ProgressBarStyle.Marquee
                lblPercent.Text = "جاري التنفيذ..."
            End If
        Catch
        End Try
    End Sub

    Public Sub ReportStep(current As Integer, total As Integer, message As String)
        Dim pct As Integer = -1
        If total > 0 Then pct = CInt(Math.Max(0, Math.Min(100, (current * 100.0R) / total)))
        Report(message & If(total > 0, "  (" & current.ToString() & " / " & total.ToString() & ")", ""), pct)
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        _cancelRequested = True
        btnCancel.Enabled = False
        lblHint.Text = "تم طلب الإلغاء. ستتوقف العملية عند أول نقطة آمنة."
        lblHint.ForeColor = Color.FromArgb(150, 60, 45)
    End Sub

    Private Sub timerElapsed_Tick(sender As Object, e As EventArgs) Handles timerElapsed.Tick
        Dim elapsed = DateTime.Now - _startedAt
        If progressMain.Style = ProgressBarStyle.Marquee Then
            lblPercent.Text = "جاري التنفيذ... الوقت المنقضي: " & elapsed.ToString("mm\:ss")
        Else
            lblHint.Text = "الوقت المنقضي: " & elapsed.ToString("mm\:ss") & " — لا يزال البرنامج يعمل في الخلفية."
        End If
    End Sub
End Class
