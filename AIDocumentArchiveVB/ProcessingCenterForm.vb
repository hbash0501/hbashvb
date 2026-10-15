Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Drawing
Imports System.Text
Imports System.Windows.Forms

Partial Public Class ProcessingCenterForm
    Public Sub New()
        InitializeComponent()
        ApplyTheme()
    End Sub

    Private Sub ProcessingCenterForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If AppServices.ProcessingQueue IsNot Nothing Then
            AddHandler AppServices.ProcessingQueue.JobChanged, AddressOf Queue_JobChanged
            AppServices.ProcessingQueue.Start(1)
        End If
        timerRefresh.Start()
        RefreshJobs()
        RenderDetails()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        Try
            If AppServices.ProcessingQueue IsNot Nothing Then RemoveHandler AppServices.ProcessingQueue.JobChanged, AddressOf Queue_JobChanged
        Catch
        End Try
        MyBase.OnFormClosed(e)
    End Sub

    Private Sub ApplyTheme()
        Try
            UiThemeService.StylePrimaryButton(btnRunMissingOcr, "gold")
            UiThemeService.StylePrimaryButton(btnFastUnderstand, "purple")
            UiThemeService.StyleLightButton(btnRefresh)
            UiThemeService.StyleLightButton(btnCancelSelected)
            UiThemeService.StyleLightButton(btnExtractEntities)
            UiThemeService.StyleLightButton(btnOpenWorkspace)
        Catch
        End Try
        UiThemeService.StyleDataGrid(dgvJobs)
    End Sub

    Private Sub Queue_JobChanged(job As ProcessingJob)
        Try
            If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
            Me.BeginInvoke(New MethodInvoker(Sub()
                                                 RefreshJobs()
                                                 RenderDetails()
                                             End Sub))
        Catch
        End Try
    End Sub

    Private Sub RefreshJobs()
        Dim table As New DataTable()
        table.Columns.Add("Id")
        table.Columns.Add("المهمة")
        table.Columns.Add("النوع")
        table.Columns.Add("الحالة")
        table.Columns.Add("التقدم")
        table.Columns.Add("الرسالة")
        table.Columns.Add("المدة")
        Dim active As Integer = 0
        Dim failed As Integer = 0
        If AppServices.ProcessingQueue IsNot Nothing Then
            For Each job In AppServices.ProcessingQueue.GetJobs()
                If job.State = "منتظر" OrElse job.State = "قيد التنفيذ" Then active += 1
                If job.State = "فاشلة" Then failed += 1
                table.Rows.Add(job.Id, job.Title, job.OperationKind, job.State, job.ProgressPercent.ToString() & "%", job.Message, FormatDuration(job.DurationMs))
            Next
        End If
        dgvJobs.DataSource = table
        If dgvJobs.Columns.Contains("Id") Then dgvJobs.Columns("Id").Visible = False
        lblStats.Text = "نشطة: " & active.ToString() & " | فاشلة: " & failed.ToString() & " | آخر تحديث: " & DateTime.Now.ToString("HH:mm:ss")
    End Sub

    Private Sub RenderDetails()
        Dim sb As New StringBuilder()
        sb.AppendLine("مبادئ المعالجة:")
        sb.AppendLine("- لا يتم إيقاف الواجهة أثناء OCR أو الذكاء السحابي.")
        sb.AppendLine("- يتم استخدام كاش حسب بصمة الملف والنص حتى لا تتكرر المعالجة.")
        sb.AppendLine("- استخراج الكيانات الأساسية يتم محلياً وبسرعة قبل أي AI.")
        sb.AppendLine("- الذكاء السحابي يحصل على مقاطع مختصرة مهمة فقط بدلاً من كامل المستند.")
        sb.AppendLine()
        If AppServices.Cache IsNot Nothing Then sb.AppendLine(AppServices.Cache.CacheReport())
        sb.AppendLine()
        sb.AppendLine("تنبيه عملي: زر OCR للناقص يضيف أول 20 مستنداً بلا نص إلى الطابور. يمكن تشغيل الباقي لاحقاً حتى لا يثقل الجهاز.")
        txtDetails.Text = sb.ToString()
    End Sub

    Private Function SelectedJobId() As String
        If dgvJobs.CurrentRow Is Nothing OrElse dgvJobs.CurrentRow.Cells("Id") Is Nothing Then Return ""
        Return Convert.ToString(dgvJobs.CurrentRow.Cells("Id").Value)
    End Function

    Private Function SelectedDocumentIdFromWorkspace() As String
        Return ""
    End Function

    Private Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        RefreshJobs()
        RenderDetails()
    End Sub

    Private Sub btnCancelSelected_Click(sender As Object, e As EventArgs) Handles btnCancelSelected.Click
        Dim id = SelectedJobId()
        If String.IsNullOrWhiteSpace(id) Then Return
        AppServices.ProcessingQueue.CancelJob(id)
        RefreshJobs()
    End Sub

    Private Sub btnRunMissingOcr_Click(sender As Object, e As EventArgs) Handles btnRunMissingOcr.Click
        Try
            Dim count As Integer = 0
            For Each d In AppServices.Repository.GetDocuments()
                If String.IsNullOrWhiteSpace(d.TextContent) Then
                    AppServices.Speed.EnqueueQuickOcr(d.Id)
                    count += 1
                    If count >= 20 Then Exit For
                End If
            Next
            MessageBox.Show("تمت إضافة " & count.ToString() & " مهمة OCR إلى الطابور.", "مركز المعالجة", MessageBoxButtons.OK, MessageBoxIcon.Information)
            RefreshJobs()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnExtractEntities_Click(sender As Object, e As EventArgs) Handles btnExtractEntities.Click
        Dim id = AskForDocumentId()
        If String.IsNullOrWhiteSpace(id) Then Return
        AppServices.Speed.EnqueueFastEntities(id)
        RefreshJobs()
    End Sub

    Private Sub btnFastUnderstand_Click(sender As Object, e As EventArgs) Handles btnFastUnderstand.Click
        Dim id = AskForDocumentId()
        If String.IsNullOrWhiteSpace(id) Then Return
        AppServices.Speed.EnqueueDeepUnderstanding(id, False)
        RefreshJobs()
    End Sub

    Private Function AskForDocumentId() As String
        Dim input As String = InputBox("أدخل رقم معرف المستند Id. يمكنك نسخه من الأرشيف الكامل أو مركز عمل المستندات.", "تحديد مستند")
        Return input.Trim()
    End Function

    Private Sub btnOpenWorkspace_Click(sender As Object, e As EventArgs) Handles btnOpenWorkspace.Click
        Dim id = AskForDocumentId()
        Using f As New DocumentWorkspaceForm(id)
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub timerRefresh_Tick(sender As Object, e As EventArgs) Handles timerRefresh.Tick
        RefreshJobs()
    End Sub

    Private Shared Function FormatDuration(ms As Long) As String
        If ms <= 0 Then Return "-"
        Dim ts = TimeSpan.FromMilliseconds(ms)
        Return ts.ToString("mm\:ss")
    End Function
End Class
