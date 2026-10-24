Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class BasiraShellForm
    Private _engine As New BasiraEngine(New List(Of DocumentRecord))
    Private _referenceIndex As New CorrespondenceIndex(New List(Of DocumentRecord))
    Private _documents As New List(Of DocumentRecord)
    Private _revision As Long = -1
    Private _refreshing As Boolean
    Private _binding As Boolean
    Private _searchGeneration As Integer
    Private _answer As BasiraAnswer
    Private _askToken As CancellationTokenSource
    Private _currentId As String = ""
    Private _importing As Boolean
    Private ReadOnly _filterKeys As String() = {"all", "review", "overdue", "week", "ocr", "sensitive"}
    Private _currentPage As Panel
    Private ReadOnly _store As BasiraStore
    Private ReadOnly _jobNotices As New BasiraJobNotifications()
    Private _lastFinishedJob As ProcessingJob
    Private _noticeDismissed As Boolean
    Private _closing As Boolean
    Private _noticeRefreshPending As Integer

    Public Sub New()
        InitializeComponent()
        _store = AppServices.Repository.VersionStore
        cboCategory.Items.Add("الكل")
        cboCategory.SelectedIndex = 0
        cboFilter.Items.AddRange(New Object() {"كل المستندات", "بانتظار المراجعة", "مواعيد متأخرة", "مستحقة خلال أسبوع", "بدون نص OCR", "مستندات حساسة"})
        cboFilter.SelectedIndex = 0
        lblUser.Text = AppServices.Auth.CurrentUser.FullName & vbCrLf & AppServices.Auth.CurrentUser.RoleName
        SetEntryEnabled(Allowed("import"))
        btnRunOcr.Enabled = Allowed("edit")
        btnExportAnswer.Enabled = Allowed("export")
        btnExportCase.Enabled = Allowed("export")
        btnHealthReport.Enabled = Allowed("export")
        btnAsk.Enabled = Allowed("search")
        navAdmin.Enabled = AppServices.Auth.CurrentUser.RoleName = "مدير النظام"
        btnCancelJob.Enabled = Allowed("edit")
        btnEdit.Enabled = Allowed("edit")
        btnAnalyze.Enabled = Allowed("ai")
        AllowDrop = Allowed("import")
        AddHandler DragEnter, AddressOf HandleDragEnter
        AddHandler DragDrop, AddressOf HandleDragDrop
        AddHandler archiveSplit.Resize, Sub() ConfigureSplit()
        AddHandler metrics.Resize, Sub() ResizeMetrics()
        AddHandler graph.DocumentClicked, AddressOf OpenDocument
        Dim tips As New ToolTip(components)
        tips.SetToolTip(txtSearch, "ابحث بالكلمات العربية أو رقم المعاملة. Ctrl+K للبحث، Ctrl+I للإدراج، Ctrl+Shift+S للسكانر.")
        tips.SetToolTip(btnScan, "اختيار السكانر والمسح ثم إدراج الوثيقة للمعالجة. Ctrl+Shift+S")
        tips.SetToolTip(btnArchiveScan, "مسح وثيقة جديدة وإدراجها مباشرة من صفحة المستندات. Ctrl+Shift+S")
        tips.SetToolTip(btnWizard, "اختيار ملف أو مجلد أو سكانر أو كاميرا. Ctrl+Shift+I")
        tips.SetToolTip(processingProgress, "تقدم مراحل المهمة الحالية، وليس تقديراً للوقت المتبقي. تظهر النتيجة بعد اكتمال الحفظ.")
        tips.SetToolTip(chkCloud, "يرسل مقتطفات المصادر غير الحساسة إلى مزودك المهيأ. النتيجة مسودة تحتاج مراجعة.")
        Dim importMenu As New ContextMenuStrip(components)
        importMenu.Items.Add("معالج الأرشفة / الماسح / الكاميرا", Nothing, Sub() OpenWizard())
        btnImport.ContextMenuStrip = importMenu
        _jobNotices.Seed(AppServices.ProcessingQueue.GetJobs())
        AddHandler AppServices.ProcessingQueue.JobChanged, AddressOf QueueJobChanged
        ShowPage(overviewPage, navOverview, "نبض الأرشيف", "المواعيد والاكتمال والتكرار، مرتبة بحسب ما يحتاج انتباهك.")
    End Sub

    Private Function Allowed(permission As String) As Boolean
        Return AppServices.Auth IsNot Nothing AndAlso AppServices.Auth.HasPermission(permission)
    End Function

    Private Async Sub ShellShown(sender As Object, e As EventArgs) Handles MyBase.Shown
        processingTray.Icon = Me.Icon
        processingTray.Visible = True
        ConfigureSplit()
        Await RefreshAsync()
        liveTimer.Start()
    End Sub

    Private Sub ConfigureSplit()
        Dim dpiScale = Me.DeviceDpi / 96.0
        If archiveSplit.ClientSize.Width < CInt(750 * dpiScale) Then Return
        archiveSplit.Panel1MinSize = CInt(300 * dpiScale)
        archiveSplit.Panel2MinSize = CInt(300 * dpiScale)
        Dim maximum = archiveSplit.Width - archiveSplit.Panel2MinSize - archiveSplit.SplitterWidth
        archiveSplit.SplitterDistance = Math.Max(archiveSplit.Panel1MinSize, Math.Min(maximum, archiveSplit.Width - CInt(390 * dpiScale)))
    End Sub

    Private Sub ShowPage(page As Panel, nav As Button, heading As String, caption As String)
        For Each p As Control In pages.Controls
            p.Visible = p Is page
        Next
        page.BringToFront()
        _currentPage = page
        For Each b In New Button() {navOverview, navArchive, navKnowledge, navWorkflow, navCollections, navProcessing}
            b.BackColor = If(b Is nav, Color.FromArgb(33, 66, 69), BasiraTheme.Ink)
            b.ForeColor = If(b Is nav, BasiraTheme.Gold, Color.FromArgb(203, 216, 224))
        Next
        banner.Heading = heading
        banner.Caption = caption
        banner.Invalidate()
        ConfigureSplit()
    End Sub

    Private Async Function RefreshAsync() As Task
        If _refreshing OrElse IsDisposed Then Return
        _refreshing = True
        Try
            Dim beforeRevision = AppServices.Repository.Revision
            Dim docs = Await Task.Run(Function() AppServices.Repository.GetDocuments())
            Dim engine = Await Task.Run(Function() New BasiraEngine(docs))
            Dim findings = Await Task.Run(Function() engine.Findings())
            Dim referenceIndex = Await Task.Run(Function() New CorrespondenceIndex(docs))
            If IsDisposed Then Return
            _documents = docs
            _engine = engine
            _referenceIndex = referenceIndex
            _revision = beforeRevision
            Dim category = CStr(cboCategory.SelectedItem)
            _binding = True
            cboCategory.Items.Clear()
            cboCategory.Items.Add("الكل")
            cboCategory.Items.AddRange(docs.Select(Function(d) d.GeneralCategory).Where(Function(c) Not String.IsNullOrWhiteSpace(c)).Distinct().OrderBy(Function(c) c).Cast(Of Object)().ToArray())
            cboCategory.SelectedIndex = Math.Max(0, cboCategory.Items.IndexOf(category))
            _binding = False
            gridFindings.DataSource = findings
            lblOverviewNote.Text = If(findings.Count = 0, "الأرشيف واضح — لا توجد تنبيهات في الفحوص الحالية", "ما يحتاج انتباهك اليوم  /  " & findings.Count.ToString() & " تنبيه")
            SetMetrics(docs, findings)
            LoadCollections()
            BuildWorkflow()
            Await SearchAsync()
            UpdateJobs()
        Catch ex As Exception
            ReportError("تعذر تحديث الأرشيف", ex)
        Finally
            _binding = False
            _refreshing = False
        End Try
    End Function

    Private Sub SetMetrics(docs As List(Of DocumentRecord), findings As List(Of BasiraFinding))
        Dim old = metrics.Controls.Cast(Of Control)().ToArray()
        metrics.Controls.Clear()
        For Each c In old
            c.Dispose()
        Next
        metrics.Controls.Add(New BasiraMetricCard With {.TitleText = "مستندات الأرشيف", .ValueText = docs.Count.ToString("N0"), .CaptionText = "بياناتك الحالية", .Accent = BasiraTheme.Teal})
        metrics.Controls.Add(New BasiraMetricCard With {.TitleText = "بانتظار المراجعة", .ValueText = docs.Where(Function(d) BasiraEngine.MatchesFilter(d, "review", DateTime.Today)).Count().ToString(), .CaptionText = "افتح لوحة المتابعة", .Accent = BasiraTheme.Gold})
        metrics.Controls.Add(New BasiraMetricCard With {.TitleText = "مواعيد متأخرة", .ValueText = docs.Where(Function(d) BasiraEngine.MatchesFilter(d, "overdue", DateTime.Today)).Count().ToString(), .CaptionText = "وفق المواعيد المسجلة", .Accent = Color.FromArgb(182, 74, 65)})
        metrics.Controls.Add(New BasiraMetricCard With {.TitleText = "نصوص قابلة للبحث", .ValueText = docs.Where(Function(d) Not String.IsNullOrWhiteSpace(d.TextContent)).Count().ToString(), .CaptionText = "افتح النص وراجع الدليل", .Accent = Color.FromArgb(94, 102, 161)})
        ResizeMetrics()
    End Sub

    Private Sub ResizeMetrics()
        For Each c As Control In metrics.Controls
            c.Width = Math.Max(125, (metrics.ClientSize.Width - 60) \ 4)
        Next
    End Sub

    Private Async Function SearchAsync() As Task
        If _binding OrElse cboFilter.SelectedIndex < 0 Then Return
        _searchGeneration += 1
        Dim generation = _searchGeneration
        Dim query = txtSearch.Text
        Dim category = CStr(cboCategory.SelectedItem)
        Dim filter = _filterKeys(cboFilter.SelectedIndex)
        Dim engine = _engine
        Dim selectedId = _currentId
        Dim hits = Await Task.Run(Function() engine.Search(query, category, filter, 500))
        If generation <> _searchGeneration OrElse IsDisposed Then Return
        gridArchive.DataSource = hits.Select(Function(h) h.Document).ToList()
        If selectedId <> "" Then SelectRow(selectedId)
        If gridArchive.Rows.Count = 0 Then ClearDetails()
        lblStatus.Text = hits.Count.ToString() & " نتيجة" & If(hits.Count = 500, " • أول 500 نتيجة؛ ضيّق البحث", "") & "   |   بحث عربي محلي   |   " & DateTime.Now.ToString("HH:mm")
    End Function

    Private Sub SearchChanged(sender As Object, e As EventArgs) Handles txtSearch.TextChanged, cboCategory.SelectedIndexChanged, cboFilter.SelectedIndexChanged
        If _binding Then Return
        searchTimer.Stop()
        searchTimer.Start()
    End Sub

    Private Async Sub SearchTick(sender As Object, e As EventArgs) Handles searchTimer.Tick
        searchTimer.Stop()
        Try
            Await SearchAsync()
        Catch ex As Exception
            ReportError("تعذر البحث", ex)
        End Try
    End Sub

    Private Sub ArchiveSelection(sender As Object, e As EventArgs) Handles gridArchive.SelectionChanged
        Dim d = TryCast(gridArchive.CurrentRow?.DataBoundItem, DocumentRecord)
        If d IsNot Nothing Then LoadDetails(d)
    End Sub

    Private Sub LoadDetails(d As DocumentRecord)
        _currentId = d.Id
        lblDetailTitle.Text = d.Title
        lblDetailMeta.Text = d.ArchiveNumber & "  |  " & d.Status & vbCrLf & d.Confidentiality & "  •  " & d.SourceEntity
        boxText.Text = If(d.TextContent, "")
        boxSummary.Text = "الملخص" & vbCrLf & If(String.IsNullOrWhiteSpace(d.Summary), "لم يُسجّل ملخص بعد.", d.Summary) & vbCrLf & vbCrLf & "الموعد: " & If(String.IsNullOrWhiteSpace(d.DueDateText), "غير محدد", d.DueDateText) & vbCrLf & "المرجع: " & d.ReferenceNumber & vbCrLf & "التصنيف: " & d.GeneralCategory & " / " & d.SubCategory & vbCrLf & "الجهة المستقبلة: " & d.DestinationEntity & vbCrLf & "آخر تحديث: " & d.UpdatedAt.ToString("yyyy/MM/dd HH:mm")
        boxEntities.Text = "أسماء: " & d.OcrNames & vbCrLf & "جهات: " & d.OcrEntities & vbCrLf & "تواريخ: " & d.OcrDates & vbCrLf & "مبالغ: " & d.OcrAmounts & vbCrLf & "مرجع: " & d.OcrReferenceNumbers & vbCrLf & "وارد: " & d.OcrIncomingNumbers & vbCrLf & "صادر: " & d.OcrOutgoingNumbers & vbCrLf & "هويات: " & d.OcrNationalIds & vbCrLf & "إقامات: " & d.OcrIqamaNumbers & vbCrLf & "جوازات: " & d.OcrPassportNumbers & vbCrLf & "هواتف: " & d.OcrPhoneNumbers & vbCrLf & "بريد: " & d.OcrEmails
        LoadHistory(d.Id)
        SetGraph(d)
        LoadReferences(d)
    End Sub

    ' يستخرج الإشارات من النص المعروض (مواضع RichTextBox نفسها) ويميّزها، ويربطها بمستندات الأرشيف.
    Private Sub LoadReferences(d As DocumentRecord)
        Dim rows As New List(Of ReferenceRow)
        Try
            For Each m In _referenceIndex.Resolve(boxText.Text, d.Id)
                Dim r = m.Reference
                Dim target = m.Documents.FirstOrDefault()
                rows.Add(New ReferenceRow With {
                    .KindLabel = r.KindLabel,
                    .Cited = If(r.DocumentType = "", "", r.DocumentType & " ") & "رقم " & r.RawNumber,
                    .DateText = If(r.DateText = "", "—", r.DateText & If(r.Date.HasValue, "", " (غير صالح)")),
                    .Gregorian = If(r.Date.HasValue AndAlso r.Calendar = "هجري", r.Date.Value.ToString("yyyy/MM/dd", Globalization.CultureInfo.InvariantCulture) & " م", ""),
                    .OwnerLabel = r.OwnerLabel,
                    .InArchive = If(target Is Nothing, "غير موجود", target.ArchiveNumber & " — " & target.Title),
                    .TargetId = If(target Is Nothing, "", target.Id),
                    .Offset = r.Offset,
                    .Length = r.Length})
                If r.Offset + r.Length <= boxText.TextLength Then
                    boxText.Select(r.Offset, r.Length)
                    boxText.SelectionBackColor = Color.FromArgb(255, 238, 189)
                End If
            Next
            boxText.Select(0, 0)
        Catch ex As Exception
            Diagnostics.Trace.WriteLine(ex.ToString())
        End Try
        gridReferences.DataSource = rows
        referencesTab.Text = If(rows.Count = 0, "الإشارات", "الإشارات (" & rows.Count.ToString() & ")")
        lblReferencesNote.Text = If(rows.Count = 0, "لا توجد في نص هذا المستند إشارات إلى خطابات أو قرارات سابقة.", "انقر مرتين على إشارة: يُفتح المستند المشار إليه إن وُجد في الأرشيف، وإلا يظهر موضعها في النص.")
    End Sub

    Private Sub ReferenceOpen(sender As Object, e As DataGridViewCellEventArgs) Handles gridReferences.CellDoubleClick
        If e.RowIndex < 0 Then Return
        Dim row = TryCast(gridReferences.Rows(e.RowIndex).DataBoundItem, ReferenceRow)
        If row Is Nothing Then Return
        If row.TargetId <> "" Then
            ' OpenDocument تمسح البحث والتصفية فيظهر المستند المقصود في القائمة ثم تحدده.
            OpenDocument(row.TargetId)
        Else
            detailTabs.SelectedIndex = 1
            boxText.Select(Math.Min(row.Offset, boxText.TextLength), Math.Min(row.Length, Math.Max(0, boxText.TextLength - row.Offset)))
            boxText.ScrollToCaret()
            boxText.Focus()
        End If
    End Sub

    Private Sub HealthReportClick(sender As Object, e As EventArgs) Handles btnHealthReport.Click
        ExportHealthReport()
    End Sub

    Private Async Sub LoadHistory(documentId As String)
        boxHistory.Text = "جارٍ تحميل سجل المستند..."
        Try
            Dim textValue = Await Task.Run(Function() BuildHistory(documentId))
            If Not IsDisposed AndAlso _currentId = documentId Then boxHistory.Text = textValue
        Catch ex As Exception
            If Not IsDisposed AndAlso _currentId = documentId Then boxHistory.Text = "تعذر قراءة السجل: " & ex.Message
        End Try
    End Sub
    Private Function BuildHistory(documentId As String) As String
        Dim history As New StringBuilder()
        For Each h In AppServices.Repository.GetWorkflowHistory(documentId).Take(15)
            history.AppendLine(h.CreatedAt.ToString("yyyy/MM/dd HH:mm") & "  " & h.EmployeeName)
            history.AppendLine(h.OldStatus & " ← " & h.NewStatus & "  " & h.Note)
        Next
        history.AppendLine(vbCrLf & "نسخ البيانات والنص قبل التعديل (حتى 25 نسخة)")
        Try
            For Each revision In _store.Revisions(documentId)
                Dim previous = revision.Element("Document")
                history.AppendLine(CStr(revision.Attribute("CapturedAt")))
                history.AppendLine(CStr(previous.Element("Title")) & " | " & CStr(previous.Element("Status")))
            Next
        Catch ex As Exception
            history.AppendLine("تعذر قراءة سجل النسخ: " & ex.Message)
        End Try
        Return history.ToString()
    End Function

    Private Sub ClearDetails()
        _currentId = ""
        lblDetailTitle.Text = "اختر مستنداً"
        lblDetailMeta.Text = ""
        boxText.Clear()
        boxSummary.Clear()
        boxEntities.Clear()
        boxHistory.Clear()
        gridReferences.DataSource = Nothing
        referencesTab.Text = "الإشارات"
    End Sub

    Private Sub SetGraph(d As DocumentRecord)
        Dim links = _engine.Links(d.Id)
        graph.SetDocument(d, links)
        boxLinks.Text = "روابط مستنتجة بالقواعد، تحتاج مراجعة. الخريطة تعرض حتى 6 روابط." & vbCrLf & String.Join(vbCrLf, links.Select(Function(l) l.Document.Title & "  •  " & l.Reason))
    End Sub

    Private Sub SelectRow(id As String)
        For Each row As DataGridViewRow In gridArchive.Rows
            Dim d = TryCast(row.DataBoundItem, DocumentRecord)
            If d IsNot Nothing AndAlso d.Id = id Then
                gridArchive.CurrentCell = row.Cells(0)
                Return
            End If
        Next
    End Sub

    Private Async Sub OpenDocument(id As String)
        Try
            _binding = True
            txtSearch.Clear()
            cboCategory.SelectedIndex = 0
            cboFilter.SelectedIndex = 0
            _binding = False
            ShowPage(archivePage, navArchive, "المستندات", "المستند ونصه وكياناته وسجله، في مساحة عمل واحدة.")
            Await SearchAsync()
            SelectRow(id)
        Catch ex As Exception
            ReportError("تعذر فتح المستند", ex)
        Finally
            _binding = False
        End Try
    End Sub

    Private Sub Navigation(sender As Object, e As EventArgs) Handles navOverview.Click, navArchive.Click, navKnowledge.Click, navWorkflow.Click, navCollections.Click, navProcessing.Click
        If sender Is navOverview Then ShowPage(overviewPage, navOverview, "نبض الأرشيف", "المواعيد والاكتمال والتكرار، مرتبة بحسب ما يحتاج انتباهك.")
        If sender Is navArchive Then ShowPage(archivePage, navArchive, "المستندات", "بحث عربي فوري، وبطاقة ونص وكيانات وسجل لكل مستند.")
        If sender Is navKnowledge Then ShowPage(knowledgePage, navKnowledge, "اسأل بصيرة", "من السؤال إلى الأدلة الأصلية؛ افتح المصدر قبل اعتماد الإجابة.")
        If sender Is navWorkflow Then ShowPage(workflowPage, navWorkflow, "لوحة المتابعة", "قيد العمل، قيد المراجعة، ومؤرشف — كل انتقال مسجّل.")
        If sender Is navCollections Then ShowPage(collectionsPage, navCollections, "مجموعاتي الذكية", "عمليات بحث محفوظة تعكس المستندات الحالية عند فتحها.")
        If sender Is navProcessing Then ShowPage(processingPage, navProcessing, "مركز المعالجة", "معالجة خلفية متسلسلة لكل مستند، وتوازٍ بين المستندات المختلفة.")
    End Sub

    Private Async Sub RefreshClick(sender As Object, e As EventArgs) Handles btnRefresh.Click
        Await RefreshAsync()
    End Sub

    Private Async Sub LiveTick(sender As Object, e As EventArgs) Handles liveTimer.Tick
        UpdateJobs()
        If AppServices.Repository.Revision <> _revision Then Await RefreshAsync()
    End Sub

    Private Sub UpdateJobs()
        Dim selected = TryCast(gridJobs.CurrentRow?.DataBoundItem, ProcessingJob)?.Id
        gridJobs.DataSource = AppServices.ProcessingQueue.GetJobs().Take(200).ToList()
        If selected IsNot Nothing Then
            For Each row As DataGridViewRow In gridJobs.Rows
                If DirectCast(row.DataBoundItem, ProcessingJob).Id = selected Then gridJobs.CurrentCell = row.Cells(0)
            Next
        End If
        UpdateCancelButton()
        UpdateProcessingNotice()
    End Sub

    Private Sub QueueJobChanged(job As ProcessingJob)
        If _closing OrElse IsDisposed OrElse Not IsHandleCreated Then Return
        Dim terminal = BasiraJobNotifications.IsTerminal(job)
        If Not terminal AndAlso Interlocked.Exchange(_noticeRefreshPending, 1) = 1 Then Return
        Try
            BeginInvoke(New MethodInvoker(Sub()
                If Not terminal Then Interlocked.Exchange(_noticeRefreshPending, 0)
                If _closing OrElse IsDisposed Then Return
                If _jobNotices.Consume(job) Then
                    _lastFinishedJob = job
                    _noticeDismissed = False
                    ShowCompletionNotification(job)
                End If
                UpdateProcessingNotice()
            End Sub))
        Catch ex As InvalidOperationException
            Interlocked.Exchange(_noticeRefreshPending, 0)
        End Try
    End Sub

    Private Sub UpdateProcessingNotice()
        If _closing Then Return
        Dim jobs = AppServices.ProcessingQueue.GetJobs()
        Dim running = jobs.Where(Function(j) j.State = "قيد التنفيذ").OrderBy(Function(j) j.StartedAt).ToList()
        Dim waiting = jobs.Where(Function(j) j.State = "منتظر").ToList()
        Dim active = running.Count + waiting.Count
        processingNotice.Visible = active > 0 OrElse (_lastFinishedJob IsNot Nothing AndAlso Not _noticeDismissed)
        If Not processingNotice.Visible Then Return
        btnDismissNotice.Visible = active = 0
        btnViewJobs.Text = If(active > 0, "متابعة " & active.ToString() & " مهمة", "عرض النتيجة")
        Dim job = If(running.Count > 0, running(0), If(waiting.Count > 0, waiting(0), _lastFinishedJob))
        If job Is Nothing Then Return
        Dim caption = JobCaption(job)
        If active > 0 Then
            noticeAccent.BackColor = BasiraTheme.Teal
            lblProcessingTitle.Text = If(running.Count = 0, "بانتظار المعالجة", "جارٍ المعالجة") & "  |  " & caption
            lblProcessingMessage.Text = If(job.Message, "تجهيز المهمة...")
            If running.Count = 0 Then
                processingProgress.Style = ProgressBarStyle.Marquee
                lblProcessingPercent.Text = "منتظر"
            Else
                processingProgress.Style = ProgressBarStyle.Continuous
                processingProgress.Value = Math.Max(0, Math.Min(99, job.ProgressPercent))
                lblProcessingPercent.Text = processingProgress.Value.ToString() & "%"
            End If
        Else
            noticeAccent.BackColor = If(job.State = "فاشلة", Color.FromArgb(182, 74, 65), If(job.State = "ملغاة", BasiraTheme.Muted, BasiraTheme.Teal))
            lblProcessingTitle.Text = BasiraJobNotifications.OutcomeTitle(job) & "  |  " & caption
            lblProcessingMessage.Text = If(job.State = "فاشلة", If(job.ErrorMessage, job.Message), If(job.ResultSummary, job.Message))
            processingProgress.Style = ProgressBarStyle.Continuous
            processingProgress.Value = If(job.State = "مكتملة", 100, Math.Max(0, Math.Min(100, job.ProgressPercent)))
            lblProcessingPercent.Text = If(job.State = "مكتملة", "100%", If(job.State = "فاشلة", "خطأ", "إلغاء"))
        End If
        lblProcessingCounts.Text = "قيد التنفيذ: " & running.Count.ToString() & "   •   في الانتظار: " & waiting.Count.ToString() & "   •   مكتملة: " & jobs.Where(Function(j) j.State = "مكتملة").Count().ToString() & "   •   تحتاج مراجعة: " & jobs.Where(Function(j) j.State = "فاشلة").Count().ToString()
        If active > 0 AndAlso _lastFinishedJob IsNot Nothing Then lblProcessingCounts.Text &= "   |   " & BasiraJobNotifications.OutcomeTitle(_lastFinishedJob) & ": " & JobCaption(_lastFinishedJob)
    End Sub

    Private Function JobCaption(job As ProcessingJob) As String
        Dim document = _documents.FirstOrDefault(Function(d) d.Id = job.DocumentId)
        If document Is Nothing Then Return If(job.Title, "مستند")
        If Not String.IsNullOrWhiteSpace(job.Title) AndAlso Not String.IsNullOrWhiteSpace(document.Title) AndAlso job.Title.IndexOf(document.Title, StringComparison.OrdinalIgnoreCase) >= 0 Then Return document.ArchiveNumber & "  |  " & job.Title
        Return document.Title & " — " & If(job.Title, "معالجة")
    End Function

    Private Sub ShowCompletionNotification(job As ProcessingJob)
        If WindowState <> FormWindowState.Minimized AndAlso Application.OpenForms.Cast(Of Form)().Any(Function(f) f.ContainsFocus) Then Return
        Try
            Dim icon = If(job.State = "فاشلة", ToolTipIcon.Warning, ToolTipIcon.Info)
            ' إشعار Windows لا يحتوي نص المستند أو عنوانه؛ التفاصيل داخل جلسة بصيرة.
            processingTray.ShowBalloonTip(6000, BasiraJobNotifications.OutcomeTitle(job), "افتح بصيرة لعرض حالة المستند والنتيجة في مركز المعالجة.", icon)
        Catch ex As Exception
            Diagnostics.Trace.WriteLine(ex.ToString())
        End Try
    End Sub

    Private Sub ViewProcessing(sender As Object, e As EventArgs) Handles btnViewJobs.Click, processingTray.BalloonTipClicked, processingTray.DoubleClick
        If Not Enabled Then Return
        If WindowState = FormWindowState.Minimized Then WindowState = FormWindowState.Normal
        Show()
        Activate()
        ShowPage(processingPage, navProcessing, "مركز المعالجة", "كل مرحلة ونتيجتها؛ العمل يستمر أثناء تنقلك بين الصفحات.")
        UpdateJobs()
        If _lastFinishedJob IsNot Nothing Then
            For Each row As DataGridViewRow In gridJobs.Rows
                If DirectCast(row.DataBoundItem, ProcessingJob).Id = _lastFinishedJob.Id Then gridJobs.CurrentCell = row.Cells(0)
            Next
        End If
    End Sub

    Private Sub DismissNotice(sender As Object, e As EventArgs) Handles btnDismissNotice.Click
        If AppServices.ProcessingQueue.GetActiveCount() > 0 Then Return
        _noticeDismissed = True
        processingNotice.Visible = False
    End Sub

    Private Sub UpdateCancelButton() Handles gridJobs.SelectionChanged
        If btnCancelJob Is Nothing Then Return
        Dim job = TryCast(gridJobs.CurrentRow?.DataBoundItem, ProcessingJob)
        btnCancelJob.Enabled = Allowed("edit") AndAlso BasiraJobNotifications.IsActive(job) AndAlso job.CanCancel
    End Sub

    Private Sub JobsFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs) Handles gridJobs.CellFormatting
        If e.RowIndex < 0 Then Return
        Dim name = gridJobs.Columns(e.ColumnIndex).DataPropertyName
        If name = "ProgressPercent" AndAlso e.Value IsNot Nothing Then
            e.Value = CInt(e.Value).ToString() & "%"
            e.FormattingApplied = True
        ElseIf name = "State" Then
            Dim state = CStr(e.Value)
            e.CellStyle.ForeColor = If(state = "فاشلة", Color.FromArgb(182, 74, 65), If(state = "منتظر", Color.FromArgb(155, 120, 52), BasiraTheme.Teal))
        End If
    End Sub

    Private Sub CancelJobClick(sender As Object, e As EventArgs) Handles btnCancelJob.Click
        If Not Allowed("edit") Then Return
        Dim job = TryCast(gridJobs.CurrentRow?.DataBoundItem, ProcessingJob)
        If job IsNot Nothing Then AppServices.ProcessingQueue.CancelJob(job.Id)
    End Sub

    Private Async Sub ImportClick(sender As Object, e As EventArgs) Handles btnImport.Click, btnArchiveImport.Click
        If Not Allowed("import") Then Return
        Using dialog As New OpenFileDialog With {.Multiselect = True, .Title = "إدراج مستندات إلى الأرشيف", .Filter = "مستندات وصور|*.pdf;*.docx;*.txt;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp|كل الملفات|*.*"}
            If dialog.ShowDialog(Me) = DialogResult.OK Then Await ImportFilesAsync(dialog.FileNames)
        End Using
    End Sub

    Private Async Function ImportFilesAsync(files As String()) As Task
        If _importing OrElse Not Allowed("import") Then Return
        _importing = True
        SetEntryEnabled(False)
        Dim errors As New List(Of String)
        Try
            For i = 0 To files.Length - 1
                lblStatus.Text = "إدراج " & (i + 1).ToString() & " من " & files.Length.ToString()
                Dim filePath = files(i)
                Try
                    Await Task.Run(Function() AppServices.Orchestrator.ImportFast(filePath))
                Catch ex As Exception
                    errors.Add(Path.GetFileName(filePath) & ": " & ex.Message)
                End Try
            Next
            Await RefreshAsync()
            lblStatus.Text = "تم إدراج " & (files.Length - errors.Count).ToString() & " مستند. المعالجة مستمرة في الخلفية."
            If errors.Count > 0 Then MessageBox.Show(Me, String.Join(vbCrLf, errors.Take(10)), "ملفات تحتاج مراجعة", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Finally
            _importing = False
            SetEntryEnabled(Allowed("import"))
        End Try
    End Function

    Private Sub HandleDragEnter(sender As Object, e As DragEventArgs)
        If Allowed("import") AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then e.Effect = DragDropEffects.Copy
    End Sub
    Private Async Sub HandleDragDrop(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then Await ImportFilesAsync(DirectCast(e.Data.GetData(DataFormats.FileDrop), String()).Where(Function(f) File.Exists(f)).ToArray())
    End Sub
    Private Async Sub OpenWizard(Optional startWithScanner As Boolean = False)
        If Not Allowed("import") OrElse _importing Then Return
        Using form As New ArchiveWizardForm(startWithScanner)
            form.ShowDialog(Me)
        End Using
        Await RefreshAsync()
    End Sub

    Private Sub ScanClick(sender As Object, e As EventArgs) Handles btnScan.Click, btnArchiveScan.Click
        OpenWizard(True)
    End Sub

    Private Sub WizardClick(sender As Object, e As EventArgs) Handles btnWizard.Click
        OpenWizard()
    End Sub

    Private Async Sub CameraClick(sender As Object, e As EventArgs) Handles btnCamera.Click
        If Not Allowed("import") OrElse _importing Then Return
        Try
            Using form As New CameraImportForm()
                If form.ShowDialog(Me) <> DialogResult.OK Then Return
                If File.Exists(form.SelectedSourcePath) Then
                    Await ImportFilesAsync(New String() {form.SelectedSourcePath})
                ElseIf Directory.Exists(form.SelectedSourcePath) Then
                    Await ImportFilesAsync(Directory.GetFiles(form.SelectedSourcePath))
                End If
            End Using
        Catch ex As Exception
            ReportError("تعذر الإدراج بالكاميرا", ex)
            MessageBox.Show(Me, ex.Message, "إدراج بالكاميرا", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub SetEntryEnabled(enabled As Boolean)
        btnImport.Enabled = enabled
        btnScan.Enabled = enabled
        btnArchiveScan.Enabled = enabled
        btnArchiveImport.Enabled = enabled
        btnCamera.Enabled = enabled
        btnWizard.Enabled = enabled
    End Sub

    Private Async Sub EditClick(sender As Object, e As EventArgs) Handles btnEdit.Click
        If _currentId = "" OrElse Not Allowed("edit") Then Return
        Using form As New BasiraDocumentEditorForm(_currentId)
            form.ShowDialog(Me)
        End Using
        Await RefreshAsync()
    End Sub

    Private Async Sub AnalyzeClick(sender As Object, e As EventArgs) Handles btnAnalyze.Click
        If _currentId = "" OrElse Not Allowed("ai") Then Return
        Using form As New DocumentWorkspaceForm(_currentId)
            form.ShowDialog(Me)
        End Using
        Await RefreshAsync()
    End Sub

    Private Sub VersionsClick(sender As Object, e As EventArgs) Handles btnVersions.Click
        If _currentId = "" OrElse Not Allowed("view") Then Return
        Try
            Using form As New BasiraRevisionViewerForm(_currentId)
                form.ShowDialog(Me)
            End Using
        Catch ex As Exception
            ReportError("تعذر قراءة النسخ", ex)
        End Try
    End Sub

    Private Sub PreviewClick(sender As Object, e As EventArgs) Handles btnPreview.Click
        If _currentId = "" OrElse Not Allowed("preview") Then Return
        Dim d = AppServices.Repository.GetDocumentById(_currentId)
        If d Is Nothing Then Return
        Dim filePath = If(File.Exists(If(d.StoredPath, "")), d.StoredPath, d.OriginalPath)
        If Not File.Exists(If(filePath, "")) Then
            MessageBox.Show(Me, "ملف المستند غير متاح في المسار المحفوظ.", "فتح الأصل")
            Return
        End If
        Try
            Diagnostics.Process.Start(New Diagnostics.ProcessStartInfo(filePath) With {.UseShellExecute = True})
        Catch ex As Exception
            ReportError("تعذر فتح الأصل", ex)
        End Try
    End Sub

    Private Sub OcrClick(sender As Object, e As EventArgs) Handles btnRunOcr.Click
        If _currentId = "" OrElse Not Allowed("edit") Then Return
        Try
            AppServices.Orchestrator.EnqueueOcrAndFastUnderstanding(_currentId)
            lblStatus.Text = "أضيف المستند إلى المعالجة. تابع تقدمه من مركز المعالجة."
        Catch ex As Exception
            ReportError("تعذر بدء المعالجة", ex)
        End Try
    End Sub

    Private Async Sub AskClick(sender As Object, e As EventArgs) Handles btnAsk.Click
        If String.IsNullOrWhiteSpace(txtQuestion.Text) OrElse Not Allowed("search") Then Return
        If chkCloud.Checked AndAlso Not Allowed("ai") Then Return
        _askToken = New CancellationTokenSource()
        Dim source = _askToken
        Dim engine = _engine
        Dim question = txtQuestion.Text.Trim()
        Dim cloud = chkCloud.Checked
        btnAsk.Enabled = False
        btnCancelAsk.Enabled = True
        lblAnswerMode.Text = "جارٍ البحث في الأدلة..."
        Try
            Dim service As New BasiraAssistantService(AppServices.ResilientAi)
            Dim result = Await Task.Run(Function() service.Ask(engine, question, cloud, source.Token), source.Token)
            If IsDisposed OrElse source.IsCancellationRequested Then Return
            _answer = result
            boxAnswer.Text = result.Text
            lblAnswerMode.Text = result.Mode & "  |  " & result.Evidence.Count.ToString() & " مصدر"
            gridEvidence.DataSource = result.Evidence
            knowledgeTabs.SelectedIndex = 0
        Catch ex As OperationCanceledException
            If Not IsDisposed Then lblAnswerMode.Text = "تم إلغاء السؤال"
        Catch ex As Exception
            ReportError("تعذر إعداد الإجابة", ex)
        Finally
            If Not IsDisposed Then
                btnAsk.Enabled = Allowed("search")
                btnCancelAsk.Enabled = False
            End If
            _askToken = Nothing
            source.Dispose()
        End Try
    End Sub
    Private Sub CancelAsk(sender As Object, e As EventArgs) Handles btnCancelAsk.Click
        If _askToken IsNot Nothing Then _askToken.Cancel()
    End Sub

    Private Async Sub OpenSource(sender As Object, e As EventArgs) Handles btnOpenSource.Click, gridEvidence.DoubleClick
        Dim evidence = TryCast(gridEvidence.CurrentRow?.DataBoundItem, BasiraEvidence)
        If evidence Is Nothing Then Return
        _binding = True
        txtSearch.Clear()
        cboCategory.SelectedIndex = 0
        cboFilter.SelectedIndex = 0
        _binding = False
        ShowPage(archivePage, navArchive, "النص والدليل", "موضع المقتطف داخل النص الأصلي المحفوظ؛ أرقام الصفحات لا تُفترض.")
        Await SearchAsync()
        SelectRow(evidence.DocumentId)
        detailTabs.SelectedIndex = 1
        Dim offset = Math.Min(evidence.Offset, boxText.TextLength)
        boxText.Select(offset, Math.Min(evidence.Quote.Length, boxText.TextLength - offset))
        boxText.SelectionBackColor = Color.FromArgb(255, 238, 189)
        boxText.ScrollToCaret()
        boxText.Focus()
    End Sub

    Private Sub ExportAnswer(sender As Object, e As EventArgs) Handles btnExportAnswer.Click
        If _answer Is Nothing OrElse Not Allowed("export") Then Return
        Using dialog As New SaveFileDialog With {.Filter = "Word|*.docx", .FileName = "Basira_Answer.docx"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Dim text = "السؤال: " & _answer.Question & vbCrLf & "نوع النتيجة: " & _answer.Mode & vbCrLf & vbCrLf & _answer.Text & vbCrLf & vbCrLf & "المصادر الأصلية:"
                For Each evidence In _answer.Evidence
                    text &= vbCrLf & "[" & evidence.Number.ToString() & "] " & evidence.Title & " | " & evidence.Reference & vbCrLf & evidence.Quote
                Next
                ReplyExportService.ExportReplyAsDocx(Nothing, text, dialog.FileName)
                AppServices.Repository.AddAudit("تصدير إجابة بصيرة", "", "", _answer.Question)
                lblStatus.Text = "تم حفظ إجابة بصيرة ومصادرها في Word."
            Catch ex As Exception
                ReportError("تعذر التصدير", ex)
            End Try
        End Using
    End Sub

    Private Async Sub ExportCase(sender As Object, e As EventArgs) Handles btnExportCase.Click
        If _answer Is Nothing OrElse Not Allowed("export") Then Return
        Using dialog As New SaveFileDialog With {.Filter = "ZIP|*.zip", .FileName = "Basira_Evidence_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".zip"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            btnExportCase.Enabled = False
            Try
                Dim result = _answer
                Dim docs = result.Evidence.Select(Function(sourceEvidence) AppServices.Repository.GetDocumentById(sourceEvidence.DocumentId)).Where(Function(d) d IsNot Nothing).ToList()
                Dim pathValue = dialog.FileName
                lblStatus.Text = "جارٍ تجهيز حزمة الأدلة..."
                Dim message = Await Task.Run(Function() BasiraCaseExportService.Export(result, docs, pathValue))
                If Not IsDisposed Then lblStatus.Text = message
            Catch ex As Exception
                ReportError("تعذر تصدير حزمة الأدلة", ex)
            Finally
                If Not IsDisposed Then btnExportCase.Enabled = Allowed("export")
            End Try
        End Using
    End Sub

    Private Sub SaveCollection(sender As Object, e As EventArgs) Handles btnSaveCollection.Click
        Dim name = Microsoft.VisualBasic.Interaction.InputBox("اسم المجموعة الذكية", "حفظ البحث", If(txtSearch.Text.Trim() = "", CStr(cboFilter.SelectedItem), txtSearch.Text.Trim()))
        If String.IsNullOrWhiteSpace(name) Then Return
        Try
            _store.SaveCollection(New BasiraCollection With {.Name = name.Trim(), .Query = txtSearch.Text, .Category = CStr(cboCategory.SelectedItem), .Filter = _filterKeys(Math.Max(0, cboFilter.SelectedIndex))})
            LoadCollections()
            lblStatus.Text = "تم حفظ المجموعة. نتائجها تتحدث عند فتحها."
        Catch ex As Exception
            ReportError("تعذر حفظ المجموعة", ex)
        End Try
    End Sub
    Private Sub LoadCollections()
        gridCollections.DataSource = _store.Collections()
    End Sub
    Private Async Sub ApplyCollection(sender As Object, e As EventArgs) Handles btnApplyCollection.Click, gridCollections.DoubleClick
        Dim collection = TryCast(gridCollections.CurrentRow?.DataBoundItem, BasiraCollection)
        If collection Is Nothing Then Return
        _binding = True
        txtSearch.Text = collection.Query
        cboCategory.SelectedIndex = Math.Max(0, cboCategory.Items.IndexOf(collection.Category))
        cboFilter.SelectedIndex = Math.Max(0, Array.IndexOf(_filterKeys, collection.Filter))
        _binding = False
        ShowPage(archivePage, navArchive, collection.Name, "مجموعة ذكية تعكس الأرشيف الحالي")
        Await SearchAsync()
    End Sub
    Private Sub DeleteCollection(sender As Object, e As EventArgs) Handles btnDeleteCollection.Click
        Dim collection = TryCast(gridCollections.CurrentRow?.DataBoundItem, BasiraCollection)
        If collection Is Nothing Then Return
        Try
            _store.DeleteCollection(collection.Id)
            LoadCollections()
        Catch ex As Exception
            ReportError("تعذر إزالة المجموعة", ex)
        End Try
    End Sub

    Private Sub BuildWorkflow()
        For Each c In workflowColumns.Controls.Cast(Of Control)().ToArray()
            workflowColumns.Controls.Remove(c)
            c.Dispose()
        Next
        Dim buckets = New String() {"قيد العمل", "للمراجعة", "مؤرشف"}
        For i = 0 To 2
            Dim index = i
            Dim list = _documents.Where(Function(d) If(BasiraEngine.IsClosed(d), 2, If(BasiraEngine.MatchesFilter(d, "review", DateTime.Today), 1, 0)) = index).ToList()
            Dim panel As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(6), .BackColor = Color.FromArgb(236, 241, 245)}
            Dim title As New Label With {.Dock = DockStyle.Top, .Height = 54, .Text = buckets(i) & "  /  " & list.Count.ToString(), .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = BasiraTheme.Ink}
            Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Padding = New Padding(8)}
            For Each d In list.Take(100)
                Dim document = d
                Dim card As New Panel With {.Width = Math.Max(220, (pages.ClientSize.Width - 95) \ 3), .Height = 150, .BackColor = Color.White, .Margin = New Padding(3, 3, 3, 9), .Padding = New Padding(10)}
                Dim body As New Label With {.Dock = DockStyle.Top, .Height = 92, .Text = d.Title & vbCrLf & d.ArchiveNumber & vbCrLf & d.SourceEntity & "  •  " & d.DueDateText, .ForeColor = BasiraTheme.Ink}
                Dim action = BasiraTheme.Button(If(i = 0, "إرسال للمراجعة", If(i = 1, "مراجعة واعتماد", "فتح المستند")), i = 1)
                action.Dock = DockStyle.Bottom
                If i = 2 Then
                    AddHandler action.Click, Sub() OpenDocument(document.Id)
                Else
                    action.Enabled = Allowed("workflow")
                    AddHandler action.Click, Sub() Transition(document.Id, If(index = 0, "قيد المراجعة", "مؤرشف"))
                End If
                AddHandler body.Click, Sub() OpenDocument(document.Id)
                body.Cursor = Cursors.Hand
                card.Controls.Add(action)
                card.Controls.Add(body)
                flow.Controls.Add(card)
            Next
            panel.Controls.Add(flow)
            panel.Controls.Add(title)
            workflowColumns.Controls.Add(panel, i, 0)
        Next
    End Sub
    Private Async Sub Transition(id As String, status As String)
        If Not Allowed("workflow") Then Return
        Try
            Dim d = AppServices.Repository.GetDocumentById(id)
            If d Is Nothing Then Return
            If AppServices.ProcessingQueue.GetJobs().Any(Function(j) j.DocumentId = id AndAlso (j.State = "منتظر" OrElse j.State = "قيد التنفيذ")) Then
                lblStatus.Text = "انتظر اكتمال معالجة المستند قبل تغيير حالته."
                Return
            End If
            If status = "مؤرشف" Then
                Dim quality = AppServices.QualityGate.Assess(d)
                If quality.QualityScore < AppServices.Settings.MinimumQualityScoreForApproval Then
                    MessageBox.Show(Me, "تحتاج جودة المستند إلى مراجعة قبل الاعتماد. الدرجة الحالية: " & quality.QualityScore.ToString(), "مراجعة المستند")
                    OpenDocument(id)
                    Return
                End If
            End If
            AppServices.Repository.ApplyWorkflow(id, status, "من لوحة متابعة بصيرة")
            AppServices.EnterpriseDb.UpsertDocument(AppServices.Repository.GetDocumentById(id))
            Await RefreshAsync()
        Catch ex As Exception
            ReportError("تعذر تغيير الحالة", ex)
        End Try
    End Sub

    Private Sub FindingsDoubleClick(sender As Object, e As EventArgs) Handles gridFindings.DoubleClick
        Dim finding = TryCast(gridFindings.CurrentRow?.DataBoundItem, BasiraFinding)
        If finding IsNot Nothing Then OpenDocument(finding.DocumentId)
    End Sub
    Private Async Sub AdminClick(sender As Object, e As EventArgs) Handles navAdmin.Click
        If AppServices.Auth.CurrentUser.RoleName <> "مدير النظام" Then Return
        Using form As New AdminCenterForm()
            form.ShowDialog(Me)
        End Using
        Await RefreshAsync()
    End Sub
    Private Sub ShellKey(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
        If e.Control AndAlso e.KeyCode = Keys.K Then
            ShowPage(archivePage, navArchive, "المستندات", "ابحث بالعنوان أو الكلمات أو رقم المعاملة.")
            txtSearch.Focus()
            txtSearch.SelectAll()
            e.SuppressKeyPress = True
        ElseIf e.Control AndAlso e.KeyCode = Keys.J Then
            ShowPage(knowledgePage, navKnowledge, "اسأل بصيرة", "ابحث عن الأدلة الأصلية، ثم راجعها قبل الاعتماد.")
            txtQuestion.Focus()
            e.SuppressKeyPress = True
        ElseIf e.Control AndAlso e.Shift AndAlso e.KeyCode = Keys.S Then
            OpenWizard(True)
            e.SuppressKeyPress = True
        ElseIf e.Control AndAlso e.Shift AndAlso e.KeyCode = Keys.I Then
            OpenWizard()
            e.SuppressKeyPress = True
        ElseIf e.Control AndAlso e.KeyCode = Keys.I Then
            ImportClick(sender, EventArgs.Empty)
            e.SuppressKeyPress = True
        ElseIf e.Control AndAlso e.Shift AndAlso e.KeyCode = Keys.H Then
            ExportHealthReport()
            e.SuppressKeyPress = True
        ElseIf e.KeyCode = Keys.F5 Then
            RefreshClick(sender, EventArgs.Empty)
            e.SuppressKeyPress = True
        End If
    End Sub
    Private Async Sub ExportHealthReport()
        If Not Allowed("export") Then
            lblStatus.Text = "لا تملك صلاحية تصدير التقارير."
            Return
        End If
        Using dialog As New SaveFileDialog With {.Filter = "HTML|*.html", .FileName = "Basira_Health_" & DateTime.Now.ToString("yyyyMMdd_HHmm") & ".html"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Dim pathValue = dialog.FileName
                Dim engine = _engine
                Dim author = If(AppServices.Auth.CurrentUser Is Nothing, "", AppServices.Auth.CurrentUser.FullName)
                lblStatus.Text = "جارٍ إعداد تقرير صحة الأرشيف..."
                Await Task.Run(Sub() ArchiveHealthReportService.Export(AppServices.Repository.GetDocuments(), engine.Findings(), author, pathValue))
                AppServices.Repository.AddAudit("تصدير تقرير صحة الأرشيف", "", "", pathValue)
                If Not IsDisposed Then lblStatus.Text = "تم حفظ تقرير صحة الأرشيف (Ctrl+Shift+H)."
            Catch ex As Exception
                ReportError("تعذر إعداد التقرير", ex)
            End Try
        End Using
    End Sub
    Private Sub ShellClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If _importing Then
            e.Cancel = True
            lblStatus.Text = "انتظر اكتمال حفظ الملفات قبل الإغلاق."
            Return
        End If
        liveTimer.Stop()
        searchTimer.Stop()
        _closing = True
        processingTray.Visible = False
        RemoveHandler AppServices.ProcessingQueue.JobChanged, AddressOf QueueJobChanged
        If _askToken IsNot Nothing Then _askToken.Cancel()
    End Sub
    Private Sub ReportError(title As String, ex As Exception)
        If IsDisposed Then Return
        lblStatus.Text = title & ": " & ex.Message
        Diagnostics.Trace.WriteLine(ex.ToString())
    End Sub
End Class

Friend Class ReferenceRow
    Public Property KindLabel As String
    Public Property Cited As String
    Public Property DateText As String
    Public Property Gregorian As String
    Public Property OwnerLabel As String
    Public Property InArchive As String
    Public Property TargetId As String
    Public Property Offset As Integer
    Public Property Length As Integer
End Class
