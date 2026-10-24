Imports System.Drawing
Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class BasiraShellForm
    Inherits Form
    Private components As System.ComponentModel.IContainer
    Friend WithEvents navOverview As Button
    Friend WithEvents navArchive As Button
    Friend WithEvents navKnowledge As Button
    Friend WithEvents navWorkflow As Button
    Friend WithEvents navCollections As Button
    Friend WithEvents navProcessing As Button
    Friend WithEvents navAdmin As Button
    Friend WithEvents btnImport As Button
    Friend WithEvents btnScan As Button
    Friend WithEvents btnArchiveScan As Button
    Friend WithEvents btnArchiveImport As Button
    Friend WithEvents btnCamera As Button
    Friend WithEvents btnWizard As Button
    Friend WithEvents btnRefresh As Button
    Friend WithEvents btnViewJobs As Button
    Friend WithEvents btnDismissNotice As Button
    Friend WithEvents processingTray As NotifyIcon
    Friend WithEvents btnSaveCollection As Button
    Friend WithEvents btnEdit As Button
    Friend WithEvents btnAnalyze As Button
    Friend WithEvents btnVersions As Button
    Friend WithEvents btnPreview As Button
    Friend WithEvents btnRunOcr As Button
    Friend WithEvents btnOpenSource As Button
    Friend WithEvents btnAsk As Button
    Friend WithEvents btnCancelAsk As Button
    Friend WithEvents btnExportAnswer As Button
    Friend WithEvents btnExportCase As Button
    Friend WithEvents btnCancelJob As Button
    Friend WithEvents btnApplyCollection As Button
    Friend WithEvents btnDeleteCollection As Button
    Friend WithEvents txtSearch As TextBox
    Friend WithEvents cboCategory As ComboBox
    Friend WithEvents cboFilter As ComboBox
    Friend WithEvents txtQuestion As TextBox
    Friend WithEvents chkCloud As CheckBox
    Friend WithEvents gridArchive As DataGridView
    Friend WithEvents gridFindings As DataGridView
    Friend WithEvents gridEvidence As DataGridView
    Friend WithEvents gridJobs As DataGridView
    Friend WithEvents gridCollections As DataGridView
    Friend WithEvents gridReferences As DataGridView
    Friend WithEvents btnHealthReport As Button
    Friend referencesTab As TabPage
    Friend lblReferencesNote As Label
    Friend WithEvents searchTimer As Timer
    Friend WithEvents liveTimer As Timer
    Friend WithEvents detailTabs As TabControl
    Friend WithEvents knowledgeTabs As TabControl
    Friend WithEvents graph As BasiraGraphControl
    Friend banner As BasiraBanner
    Friend lblStatus As Label
    Friend lblUser As Label
    Friend lblDetailTitle As Label
    Friend lblDetailMeta As Label
    Friend lblAnswerMode As Label
    Friend lblOverviewNote As Label
    Friend boxText As RichTextBox
    Friend boxSummary As RichTextBox
    Friend boxEntities As RichTextBox
    Friend boxHistory As RichTextBox
    Friend boxAnswer As RichTextBox
    Friend boxLinks As RichTextBox
    Friend pages As Panel
    Friend overviewPage As Panel
    Friend archivePage As Panel
    Friend knowledgePage As Panel
    Friend workflowPage As Panel
    Friend collectionsPage As Panel
    Friend processingPage As Panel
    Friend metrics As FlowLayoutPanel
    Friend workflowColumns As TableLayoutPanel
    Friend archiveSplit As SplitContainer
    Friend processingNotice As Panel
    Friend noticeAccent As Panel
    Friend lblProcessingTitle As Label
    Friend lblProcessingMessage As Label
    Friend lblProcessingCounts As Label
    Friend lblProcessingPercent As Label
    Friend processingProgress As ProgressBar

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then components.Dispose()
        MyBase.Dispose(disposing)
    End Sub

    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
        SuspendLayout()
        AutoScaleMode = AutoScaleMode.Dpi
        AutoScaleDimensions = New SizeF(96, 96)
        Font = New Font("Segoe UI", 10.0F)
        ClientSize = New Size(1460, 930)
        MinimumSize = New Size(1120, 740)
        Text = "بصيرة | منصة الأرشفة الذكية — 26.10.3"
        StartPosition = FormStartPosition.CenterScreen
        RightToLeft = RightToLeft.Yes
        BackColor = BasiraTheme.Canvas
        KeyPreview = True
        Dim main As New Panel With {.Dock = DockStyle.Fill}
        Dim sidebar As New Panel With {.Dock = DockStyle.Right, .Width = 210, .BackColor = BasiraTheme.Ink, .Padding = New Padding(10)}
        Dim brand As New Label With {.Dock = DockStyle.Top, .Height = 128, .Text = "بصيرة" & vbCrLf & "B A S I R A" & vbCrLf & "الأرشفة الذكية", .TextAlign = ContentAlignment.MiddleCenter, .Font = New Font("Segoe UI", 18.0F, FontStyle.Bold), .ForeColor = BasiraTheme.Gold}
        lblUser = New Label With {.Dock = DockStyle.Bottom, .Height = 75, .ForeColor = Color.FromArgb(176, 194, 202), .TextAlign = ContentAlignment.MiddleCenter, .Font = New Font("Segoe UI", 9.0F)}
        Dim nav As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Padding = New Padding(2, 8, 2, 0)}
        navOverview = NavButton("◈   نبض الأرشيف")
        navArchive = NavButton("▤   المستندات")
        navKnowledge = NavButton("✦   اسأل بصيرة")
        navWorkflow = NavButton("▦   لوحة المتابعة")
        navCollections = NavButton("◇   مجموعاتي الذكية")
        navProcessing = NavButton("◉   مركز المعالجة")
        navAdmin = NavButton("⚙   الإدارة والأدوات")
        nav.Controls.AddRange(New Control() {navOverview, navArchive, navKnowledge, navWorkflow, navCollections, navProcessing, navAdmin})
        sidebar.Controls.Add(nav)
        sidebar.Controls.Add(lblUser)
        sidebar.Controls.Add(brand)
        banner = New BasiraBanner With {.Dock = DockStyle.Top, .Height = 144, .Heading = "من الأرشيف إلى رؤية أوضح."}
        lblStatus = New Label With {.Dock = DockStyle.Bottom, .Height = 32, .ForeColor = BasiraTheme.Muted, .Padding = New Padding(16, 5, 16, 0), .BackColor = Color.White, .Text = "جارٍ تحميل الأرشيف..."}
        pages = New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(18)}
        Dim entryBar = Toolbar()
        entryBar.Height = 62
        entryBar.Padding = New Padding(16, 8, 16, 8)
        entryBar.BackColor = Color.White
        btnImport = BasiraTheme.Button("+  إدراج ملفات", True)
        btnScan = BasiraTheme.Button("مسح بالسكانر")
        btnScan.BackColor = Color.FromArgb(234, 245, 240)
        btnScan.ForeColor = BasiraTheme.Teal
        btnCamera = BasiraTheme.Button("إدراج بالكاميرا")
        btnWizard = BasiraTheme.Button("معالج الإدراج")
        btnRefresh = BasiraTheme.Button("تحديث  F5")
        entryBar.Controls.AddRange(New Control() {btnImport, btnScan, btnCamera, btnWizard, btnRefresh})

        ' إشعار ثابت فوق شريط الحالة، يظهر أثناء العمل وحتى إغلاق نتيجة النهاية.
        processingNotice = New Panel With {.Dock = DockStyle.Bottom, .Height = 122, .Padding = New Padding(16, 8, 16, 8), .BackColor = Color.White, .Visible = False}
        noticeAccent = New Panel With {.Dock = DockStyle.Top, .Height = 3, .BackColor = BasiraTheme.Teal}
        Dim noticeActions As New Panel With {.Dock = DockStyle.Left, .Width = 166, .Padding = New Padding(0, 10, 8, 0)}
        btnViewJobs = BasiraTheme.Button("تفاصيل المعالجة")
        btnViewJobs.Dock = DockStyle.Top
        btnDismissNotice = BasiraTheme.Button("إخفاء النتيجة")
        btnDismissNotice.Dock = DockStyle.Bottom
        btnDismissNotice.Height = 28
        btnDismissNotice.Font = New Font("Segoe UI", 9.0F)
        noticeActions.Controls.Add(btnViewJobs)
        noticeActions.Controls.Add(btnDismissNotice)
        Dim noticeBody As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(12, 7, 2, 0)}
        lblProcessingTitle = New Label With {.Dock = DockStyle.Top, .Height = 25, .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold), .ForeColor = BasiraTheme.Ink, .AutoEllipsis = True}
        lblProcessingMessage = New Label With {.Dock = DockStyle.Top, .Height = 24, .Font = New Font("Segoe UI", 9.5F), .ForeColor = BasiraTheme.Muted, .AutoEllipsis = True}
        lblProcessingCounts = New Label With {.Dock = DockStyle.Bottom, .Height = 24, .Font = New Font("Segoe UI", 9.0F), .ForeColor = BasiraTheme.Muted, .AutoEllipsis = True}
        Dim progressRow As New Panel With {.Dock = DockStyle.Top, .Height = 20, .Padding = New Padding(0, 3, 0, 3)}
        lblProcessingPercent = New Label With {.Dock = DockStyle.Left, .Width = 56, .ForeColor = BasiraTheme.Teal, .TextAlign = ContentAlignment.MiddleCenter, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}
        processingProgress = New ProgressBar With {.Dock = DockStyle.Fill, .Minimum = 0, .Maximum = 100, .Style = ProgressBarStyle.Continuous, .MarqueeAnimationSpeed = 35}
        progressRow.Controls.Add(processingProgress)
        progressRow.Controls.Add(lblProcessingPercent)
        noticeBody.Controls.Add(progressRow)
        noticeBody.Controls.Add(lblProcessingMessage)
        noticeBody.Controls.Add(lblProcessingTitle)
        noticeBody.Controls.Add(lblProcessingCounts)
        processingNotice.Controls.Add(noticeBody)
        processingNotice.Controls.Add(noticeActions)
        processingNotice.Controls.Add(noticeAccent)
        processingTray = New NotifyIcon(components) With {.Text = "بصيرة — متابعة المعالجة", .Visible = False}
        main.Controls.Add(pages)
        main.Controls.Add(processingNotice)
        main.Controls.Add(lblStatus)
        main.Controls.Add(entryBar)
        main.Controls.Add(banner)
        Controls.Add(main)
        Controls.Add(sidebar)
        overviewPage = Page()
        archivePage = Page()
        knowledgePage = Page()
        workflowPage = Page()
        collectionsPage = Page()
        processingPage = Page()
        pages.Controls.AddRange(New Control() {overviewPage, archivePage, knowledgePage, workflowPage, collectionsPage, processingPage})

        ' نبض الأرشيف
        metrics = New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 136, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False}
        lblOverviewNote = New Label With {.Dock = DockStyle.Top, .Height = 54, .Text = "ما يحتاج انتباهك اليوم", .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold), .ForeColor = BasiraTheme.Ink, .Padding = New Padding(8, 14, 8, 0)}
        gridFindings = BasiraTheme.Grid()
        BasiraTheme.Column(gridFindings, "Kind", "الإجراء", 70)
        BasiraTheme.Column(gridFindings, "Title", "المستند", 145)
        BasiraTheme.Column(gridFindings, "Reason", "سبب التنبيه", 170)
        Dim overviewBar = Toolbar()
        btnHealthReport = BasiraTheme.Button("تقرير صحة الأرشيف", True)
        btnHealthReport.Width = 175
        Dim healthHint As New Label With {.AutoSize = True, .Text = "تقرير HTML يُطبع ويُشارك: مؤشر الصحة والتوزيعات والتنبيهات  •  Ctrl+Shift+H", .ForeColor = BasiraTheme.Muted, .Font = New Font("Segoe UI", 9.0F), .Margin = New Padding(12, 12, 8, 0)}
        overviewBar.Controls.AddRange(New Control() {btnHealthReport, healthHint})
        overviewPage.Controls.Add(gridFindings)
        overviewPage.Controls.Add(lblOverviewNote)
        overviewPage.Controls.Add(overviewBar)
        overviewPage.Controls.Add(metrics)

        ' المستندات: قائمة + تفاصيل في الصفحة نفسها
        Dim searchBar As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 54, .ColumnCount = 3, .RowCount = 1, .Padding = New Padding(0, 4, 0, 8)}
        searchBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        searchBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
        searchBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
        txtSearch = New TextBox With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 12.0F), .Margin = New Padding(5), .BorderStyle = BorderStyle.FixedSingle}
        cboCategory = New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList, .Margin = New Padding(5)}
        cboFilter = New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList, .Margin = New Padding(5)}
        searchBar.Controls.Add(txtSearch, 0, 0)
        searchBar.Controls.Add(cboCategory, 1, 0)
        searchBar.Controls.Add(cboFilter, 2, 0)
        archiveSplit = New SplitContainer With {.Dock = DockStyle.Fill, .Size = New Size(1190, 630), .SplitterWidth = 12, .BackColor = BasiraTheme.Canvas, .RightToLeft = RightToLeft.No}
        archiveSplit.Panel1.RightToLeft = RightToLeft.Yes
        archiveSplit.Panel2.RightToLeft = RightToLeft.Yes
        gridArchive = BasiraTheme.Grid()
        BasiraTheme.Column(gridArchive, "ArchiveNumber", "الأرشيف", 70)
        BasiraTheme.Column(gridArchive, "Title", "العنوان", 170)
        BasiraTheme.Column(gridArchive, "GeneralCategory", "التصنيف", 75)
        BasiraTheme.Column(gridArchive, "Status", "الحالة", 80)
        archiveSplit.Panel1.Controls.Add(gridArchive)
        Dim docHeader As New Panel With {.Dock = DockStyle.Top, .Height = 88, .BackColor = Color.White, .Padding = New Padding(12)}
        lblDetailTitle = New Label With {.Dock = DockStyle.Top, .Height = 33, .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold), .ForeColor = BasiraTheme.Ink, .AutoEllipsis = True}
        lblDetailMeta = New Label With {.Dock = DockStyle.Fill, .ForeColor = BasiraTheme.Muted, .Font = New Font("Segoe UI", 9.0F)}
        docHeader.Controls.Add(lblDetailMeta)
        docHeader.Controls.Add(lblDetailTitle)
        detailTabs = New TabControl With {.Dock = DockStyle.Fill, .RightToLeft = RightToLeft.Yes, .RightToLeftLayout = True}
        boxSummary = BasiraTheme.ReadBox()
        boxText = BasiraTheme.ReadBox()
        boxEntities = BasiraTheme.ReadBox()
        boxHistory = BasiraTheme.ReadBox()
        AddTab(detailTabs, "البطاقة", boxSummary)
        AddTab(detailTabs, "النص والدليل", boxText)
        ' الإشارات إلى خطابات وقرارات سابقة كما وردت في نص المستند، مع المستند المقصود إن كان في الأرشيف.
        Dim referencesPanel As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White}
        gridReferences = BasiraTheme.Grid()
        BasiraTheme.Column(gridReferences, "KindLabel", "النوع", 60)
        BasiraTheme.Column(gridReferences, "Cited", "المستند المشار إليه", 95)
        BasiraTheme.Column(gridReferences, "DateText", "التاريخ", 70)
        BasiraTheme.Column(gridReferences, "Gregorian", "الموافق", 70)
        BasiraTheme.Column(gridReferences, "OwnerLabel", "الجهة", 70)
        BasiraTheme.Column(gridReferences, "InArchive", "في الأرشيف", 110)
        lblReferencesNote = New Label With {.Dock = DockStyle.Top, .Height = 40, .ForeColor = BasiraTheme.Muted, .Font = New Font("Segoe UI", 9.0F), .Padding = New Padding(4, 4, 4, 0),
            .Text = "انقر مرتين على إشارة: يُفتح المستند المشار إليه إن وُجد في الأرشيف، وإلا يظهر موضعها في النص."}
        referencesPanel.Controls.Add(gridReferences)
        referencesPanel.Controls.Add(lblReferencesNote)
        AddTab(detailTabs, "الإشارات", referencesPanel)
        referencesTab = detailTabs.TabPages(detailTabs.TabPages.Count - 1)
        AddTab(detailTabs, "الكيانات", boxEntities)
        AddTab(detailTabs, "السجل والنسخ", boxHistory)
        Dim docToolbar = Toolbar()
        docToolbar.Height = 145
        docToolbar.WrapContents = True
        btnEdit = BasiraTheme.Button("تحرير البيانات والنص")
        btnAnalyze = BasiraTheme.Button("الرد والتحليل")
        btnVersions = BasiraTheme.Button("مقارنة النسخ")
        btnPreview = BasiraTheme.Button("فتح الأصل")
        btnRunOcr = BasiraTheme.Button("استخراج النص", True)
        btnSaveCollection = BasiraTheme.Button("حفظ البحث كمجموعة")
        docToolbar.Controls.AddRange(New Control() {btnEdit, btnAnalyze, btnVersions, btnPreview, btnRunOcr, btnSaveCollection})
        archiveSplit.Panel2.Controls.Add(detailTabs)
        archiveSplit.Panel2.Controls.Add(docToolbar)
        archiveSplit.Panel2.Controls.Add(docHeader)
        archivePage.Controls.Add(archiveSplit)
        archivePage.Controls.Add(searchBar)
        Dim archiveEntry = Toolbar()
        archiveEntry.BackColor = Color.White
        archiveEntry.AutoScroll = True
        btnArchiveScan = BasiraTheme.Button("مسح وإدراج وثيقة", True)
        btnArchiveImport = BasiraTheme.Button("إدراج من ملف")
        Dim archiveEntryHint As New Label With {.AutoSize = True, .Text = "إضافة مستند  •  Ctrl+Shift+S للسكانر", .ForeColor = BasiraTheme.Muted, .Font = New Font("Segoe UI", 9.0F), .Margin = New Padding(12, 12, 8, 0)}
        archiveEntry.Controls.AddRange(New Control() {btnArchiveScan, btnArchiveImport, archiveEntryHint})
        archivePage.Controls.Add(archiveEntry)

        ' اسأل بصيرة
        Dim questionBar = Toolbar()
        questionBar.Height = 96
        questionBar.WrapContents = True
        txtQuestion = New TextBox With {.Width = 460, .Font = New Font("Segoe UI", 12.0F), .Margin = New Padding(4, 6, 4, 4)}
        btnAsk = BasiraTheme.Button("ابحث عن الأدلة", True)
        btnCancelAsk = BasiraTheme.Button("إلغاء", False)
        btnCancelAsk.Enabled = False
        chkCloud = New CheckBox With {.AutoSize = True, .Text = "صياغة سحابية للمصادر غير الحساسة", .Margin = New Padding(8, 12, 8, 4), .ForeColor = BasiraTheme.Muted}
        btnExportAnswer = BasiraTheme.Button("تصدير Word")
        btnExportCase = BasiraTheme.Button("حزمة المصادر ZIP")
        questionBar.Controls.AddRange(New Control() {txtQuestion, btnAsk, btnCancelAsk, chkCloud, btnExportAnswer, btnExportCase})
        lblAnswerMode = New Label With {.Dock = DockStyle.Top, .Height = 44, .ForeColor = BasiraTheme.Teal, .Text = "كل إجابة تبدأ من مستند قابل للفتح والمراجعة", .Padding = New Padding(8, 12, 8, 0)}
        knowledgeTabs = New TabControl With {.Dock = DockStyle.Fill, .RightToLeft = RightToLeft.Yes, .RightToLeftLayout = True}
        boxAnswer = BasiraTheme.ReadBox()
        gridEvidence = BasiraTheme.Grid()
        BasiraTheme.Column(gridEvidence, "Number", "المصدر", 35)
        BasiraTheme.Column(gridEvidence, "Title", "المستند", 110)
        BasiraTheme.Column(gridEvidence, "Quote", "المقتطف الأصلي", 200)
        graph = New BasiraGraphControl With {.Dock = DockStyle.Fill}
        boxLinks = BasiraTheme.ReadBox()
        AddTab(knowledgeTabs, "الإجابة", boxAnswer)
        Dim evidencePage As New Panel With {.Dock = DockStyle.Fill}
        Dim evidenceToolbar = Toolbar()
        btnOpenSource = BasiraTheme.Button("فتح المصدر والدليل", True)
        evidenceToolbar.Controls.Add(btnOpenSource)
        evidencePage.Controls.Add(gridEvidence)
        evidencePage.Controls.Add(evidenceToolbar)
        AddTab(knowledgeTabs, "المصادر", evidencePage)
        Dim graphPage As New Panel With {.Dock = DockStyle.Fill}
        boxLinks.Dock = DockStyle.Bottom
        boxLinks.Height = 106
        graphPage.Controls.Add(graph)
        graphPage.Controls.Add(boxLinks)
        AddTab(knowledgeTabs, "خريطة العلاقات", graphPage)
        knowledgePage.Controls.Add(knowledgeTabs)
        knowledgePage.Controls.Add(lblAnswerMode)
        knowledgePage.Controls.Add(questionBar)

        workflowColumns = New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1}
        For i = 0 To 2
            workflowColumns.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.333F))
        Next
        workflowPage.Controls.Add(workflowColumns)

        gridCollections = BasiraTheme.Grid()
        BasiraTheme.Column(gridCollections, "Name", "المجموعة", 100)
        BasiraTheme.Column(gridCollections, "Query", "عبارة البحث", 100)
        BasiraTheme.Column(gridCollections, "Category", "التصنيف", 70)
        Dim collectionsToolbar = Toolbar()
        btnApplyCollection = BasiraTheme.Button("فتح المجموعة", True)
        btnDeleteCollection = BasiraTheme.Button("إزالة المجموعة")
        collectionsToolbar.Controls.AddRange(New Control() {btnApplyCollection, btnDeleteCollection})
        collectionsPage.Controls.Add(gridCollections)
        collectionsPage.Controls.Add(collectionsToolbar)

        gridJobs = BasiraTheme.Grid()
        BasiraTheme.Column(gridJobs, "Title", "المهمة", 100)
        BasiraTheme.Column(gridJobs, "State", "الحالة", 65)
        BasiraTheme.Column(gridJobs, "ProgressPercent", "التقدم %", 45)
        BasiraTheme.Column(gridJobs, "Message", "التفاصيل", 180)
        Dim jobToolbar = Toolbar()
        btnCancelJob = BasiraTheme.Button("إلغاء المهمة المحددة")
        jobToolbar.Controls.Add(btnCancelJob)
        processingPage.Controls.Add(gridJobs)
        processingPage.Controls.Add(jobToolbar)
        searchTimer = New Timer(components) With {.Interval = 280}
        liveTimer = New Timer(components) With {.Interval = 2500}
        ResumeLayout(True)
    End Sub

    Private Shared Function Page() As Panel
        Return New Panel With {.Dock = DockStyle.Fill, .Visible = False, .BackColor = BasiraTheme.Canvas}
    End Function
    Private Shared Function Toolbar() As FlowLayoutPanel
        Return New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 52, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Padding = New Padding(2, 3, 2, 4)}
    End Function
    Private Shared Function NavButton(textValue As String) As Button
        Dim b = BasiraTheme.Button(textValue)
        b.Size = New Size(180, 49)
        b.BackColor = BasiraTheme.Ink
        b.ForeColor = Color.FromArgb(203, 216, 224)
        b.FlatAppearance.BorderSize = 0
        b.TextAlign = ContentAlignment.MiddleRight
        Return b
    End Function
    Private Shared Sub AddTab(tabs As TabControl, nameValue As String, child As Control)
        Dim page As New TabPage(nameValue) With {.Padding = New Padding(12), .BackColor = Color.White}
        page.Controls.Add(child)
        tabs.TabPages.Add(page)
    End Sub
End Class
