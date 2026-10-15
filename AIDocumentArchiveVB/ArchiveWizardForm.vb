Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Windows.Forms

Partial Public Class ArchiveWizardForm
    Private _sourcePath As String = ""
    Private _lastDocumentId As String = ""
    Private _lastFolderResults As New List(Of ImportResult)()
    Private ReadOnly _startWithScanner As Boolean

    Public Sub New()
        Me.New(False)
    End Sub

    Public Sub New(startWithScanner As Boolean)
        InitializeComponent()
        _startWithScanner = startWithScanner
    End Sub

    Private Sub ArchiveWizardForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        txtResult.Text = "ابدأ باختيار ملف، أو تصوير بالكاميرا، أو مسح ضوئي عبر WIA، أو استيراد مجلد. بعد ذلك اضغط: إدراج ومعالجة."
    End Sub

    Private Sub ArchiveWizardForm_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
        If _startWithScanner Then BeginInvoke(New MethodInvoker(Sub() btnScan.PerformClick()))
    End Sub

    Private Sub btnChooseFile_Click(sender As Object, e As EventArgs) Handles btnChooseFile.Click
        Using ofd As New OpenFileDialog()
            ofd.Title = "اختر مستنداً للأرشفة"
            ofd.Filter = "كل الملفات المدعومة|*.pdf;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.txt;*.csv;*.docx;*.xlsx|PDF|*.pdf|صور|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp|Office/Text|*.txt;*.csv;*.docx;*.xlsx|كل الملفات|*.*"
            If ofd.ShowDialog(Me) = DialogResult.OK Then
                SetSource(ofd.FileName)
            End If
        End Using
    End Sub


    Private Sub btnCamera_Click(sender As Object, e As EventArgs) Handles btnCamera.Click
        Try
            Using f As New CameraImportForm()
                If f.ShowDialog(Me) = DialogResult.OK Then
                    Dim capturedSource = f.SelectedSourcePath
                    If String.IsNullOrWhiteSpace(capturedSource) Then Return
                    If File.Exists(capturedSource) Then
                        SetSource(capturedSource)
                        txtResult.Text = "تم التقاط مستند بالكاميرا بنجاح." & Environment.NewLine & capturedSource & Environment.NewLine & Environment.NewLine & "اضغط إدراج ومعالجة لتشغيل استخراج النص والكيانات في الخلفية وإدخال المستند في الأرشيف."
                    ElseIf Directory.Exists(capturedSource) Then
                        _sourcePath = capturedSource
                        _lastDocumentId = ""
                        btnOpenEditor.Enabled = False
                        btnOcrReport.Enabled = False
                        txtSourcePath.Text = capturedSource
                        txtResult.Text = "تم التقاط عدة صفحات بالكاميرا." & Environment.NewLine & capturedSource & Environment.NewLine & Environment.NewLine & "عدد الصفحات: " & f.CapturedCount.ToString() & Environment.NewLine & "اضغط إدراج ومعالجة لاستيراد الصفحات المصورة كدفعة واحدة."
                    End If
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "إدراج بالكاميرا", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub btnScan_Click(sender As Object, e As EventArgs) Handles btnScan.Click
        Try
            Dim tempFolder = Path.Combine(Path.GetTempPath(), "AIDocArchiveScans")
            Dim scannedPath As String = ProgressDialogForm.RunSta(Of String)(Me,
                "المسح الضوئي",
                "جاري التواصل مع الماسح الضوئي. قد تظهر نافذة تعريف الماسح حسب الجهاز.",
                Function(ctx As ProgressDialogForm)
                    ctx.Report("جاري تشغيل خدمة WIA للماسح...", -1)
                    Dim scanner As New WiaScanService()
                    Return scanner.ScanToImage(tempFolder)
                End Function)
            If String.IsNullOrWhiteSpace(scannedPath) Then
                txtResult.Text = "أُلغي المسح؛ لم تتم إضافة وثيقة جديدة."
                Return
            End If
            SetSource(scannedPath)
            txtResult.Text = "تم المسح الضوئي بنجاح." & Environment.NewLine & scannedPath & Environment.NewLine & Environment.NewLine & "اضغط إدراج ومعالجة لإدخال المستند في الأرشيف."
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "المسح بالسكانر", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub btnChooseFolder_Click(sender As Object, e As EventArgs) Handles btnChooseFolder.Click
        Using fbd As New FolderBrowserDialog()
            fbd.Description = "اختر مجلد المستندات المراد استيراده"
            If fbd.ShowDialog(Me) = DialogResult.OK Then
                _sourcePath = fbd.SelectedPath
                _lastDocumentId = ""
                btnOpenEditor.Enabled = False
                btnOcrReport.Enabled = False
                txtSourcePath.Text = _sourcePath
                txtResult.Text = "تم اختيار مجلد." & Environment.NewLine & _sourcePath & Environment.NewLine & Environment.NewLine & "سيتم استيراد الملفات المدعومة فقط عند الضغط على إدراج ومعالجة."
            End If
        End Using
    End Sub

    Private Sub SetSource(filePath As String)
        _sourcePath = filePath
        txtSourcePath.Text = filePath
        _lastDocumentId = ""
        btnOpenEditor.Enabled = False
        btnOcrReport.Enabled = False
        If File.Exists(filePath) Then
            Dim info As New FileInfo(filePath)
            txtResult.Text = "تم اختيار الملف:" & Environment.NewLine & filePath & Environment.NewLine & Environment.NewLine &
                             "الحجم: " & FormatBytes(info.Length) & Environment.NewLine &
                             "النوع: " & info.Extension & Environment.NewLine & Environment.NewLine &
                             "الخطوة التالية: اضغط إدراج ومعالجة لتشغيل استخراج النص والتصنيف الذكي."
        End If
    End Sub

    Private Sub btnAnalyzeSave_Click(sender As Object, e As EventArgs) Handles btnAnalyzeSave.Click
        If String.IsNullOrWhiteSpace(_sourcePath) Then
            MessageBox.Show("اختر ملفاً أو مجلداً أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If File.Exists(_sourcePath) Then
            ImportSingleFile(_sourcePath)
        ElseIf Directory.Exists(_sourcePath) Then
            ImportFolder(_sourcePath)
        Else
            MessageBox.Show("المصدر المحدد غير موجود.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Sub ImportSingleFile(filePath As String)
        btnAnalyzeSave.Enabled = False
        Try
            Dim result As ImportResult = ProgressDialogForm.Run(Of ImportResult)(Me,
                "إدراج المستند",
                "حفظ الأصل في الأرشيف ثم إرسال المستند إلى المعالجة الخلفية.",
                Function(ctx As ProgressDialogForm)
                    ctx.Report("جاري تهيئة محرك الاستيراد...", 5)
                    ctx.Report("حفظ الملف: " & Path.GetFileName(filePath), 25)
                    Dim importResult = QueueImport(filePath)
                    ctx.Report("حُفظ الأصل وأُضيف إلى طابور المعالجة.", 100)
                    Return importResult
                End Function)
            _lastDocumentId = result.DocumentId
            txtResult.Text = BuildSingleResultText(result)
            btnOpenEditor.Enabled = Not String.IsNullOrWhiteSpace(_lastDocumentId)
            btnOcrReport.Enabled = btnOpenEditor.Enabled
            If btnOpenEditor.Enabled Then
                AppServices.Repository.AddAudit("إدخال عبر معالج الأرشفة", _lastDocumentId, result.Title, "المصدر: " & filePath)
            End If
        Catch ex As Exception
            MessageBox.Show("تعذر إدراج ومعالجة المستند:" & Environment.NewLine & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnAnalyzeSave.Enabled = True
        End Try
    End Sub

    Private Sub ImportFolder(folderPath As String)
        btnAnalyzeSave.Enabled = False
        _lastFolderResults.Clear()
        Try
            Dim results As List(Of ImportResult) = ProgressDialogForm.Run(Of List(Of ImportResult))(Me,
                "استيراد مجلد مستندات",
                "حفظ الملفات المدعومة وإرسالها إلى المعالجة الخلفية.",
                Function(ctx As ProgressDialogForm)
                    Dim supportedFiles As New List(Of String)()
                    For Each candidatePath In Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                        If IsSupportedFile(candidatePath) Then supportedFiles.Add(candidatePath)
                    Next
                    Dim output As New List(Of ImportResult)()
                    Dim index As Integer = 0
                    For Each filePath In supportedFiles
                        If ctx.IsCancelRequested Then Exit For
                        index += 1
                        ctx.ReportStep(index, supportedFiles.Count, "جاري استيراد: " & Path.GetFileName(filePath))
                        Try
                            output.Add(QueueImport(filePath))
                        Catch ex As Exception
                            output.Add(New ImportResult With {.SourcePath = filePath, .Title = Path.GetFileName(filePath), .Status = "فشل", .Message = ex.Message})
                        End Try
                    Next
                    Return output
                End Function)
            _lastFolderResults = results
            For Each r In _lastFolderResults
                If String.IsNullOrWhiteSpace(_lastDocumentId) AndAlso Not String.IsNullOrWhiteSpace(r.DocumentId) Then _lastDocumentId = r.DocumentId
            Next
            txtResult.Text = BuildFolderResultText(folderPath, _lastFolderResults)
            btnOpenEditor.Enabled = Not String.IsNullOrWhiteSpace(_lastDocumentId)
            btnOcrReport.Enabled = btnOpenEditor.Enabled
            AppServices.Repository.AddAudit("استيراد مجلد عبر معالج الأرشفة", "", "", folderPath & " — عدد النتائج: " & _lastFolderResults.Count.ToString())
        Catch ex As Exception
            MessageBox.Show("تعذر استيراد المجلد:" & Environment.NewLine & ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnAnalyzeSave.Enabled = True
        End Try
    End Sub

    Private Function QueueImport(filePath As String) As ImportResult
        Dim result As ImportResult = Nothing
        AppServices.Orchestrator.ImportFast(filePath, Sub(imported) result = imported)
        result.Status = "بانتظار المعالجة"
        result.Message = "حُفظ الأصل. تُستخرج النصوص والكيانات في الخلفية؛ تابع إشعار التقدم ثم راجع النتائج عند الاكتمال."
        Return result
    End Function

    Private Function BuildSingleResultText(result As ImportResult) As String
        If result Is Nothing Then Return "لم يرجع المعالج نتيجة."
        Dim doc As DocumentRecord = Nothing
        If Not String.IsNullOrWhiteSpace(result.DocumentId) Then doc = AppServices.Repository.GetDocumentById(result.DocumentId)
        Dim sb As New StringBuilder()
        sb.AppendLine("تم الإدراج — المعالجة في الخلفية")
        sb.AppendLine("الحالة: " & Safe(result.Status))
        sb.AppendLine("الرسالة: " & Safe(result.Message))
        sb.AppendLine("العنوان: " & Safe(result.Title))
        If doc IsNot Nothing Then
            sb.AppendLine("رقم الأرشيف: " & Safe(doc.ArchiveNumber))
            sb.AppendLine("التصنيف: " & Safe(doc.GeneralCategory) & " / " & Safe(doc.SubCategory))
            sb.AppendLine("الجهة الصادرة: " & Safe(doc.SourceEntity))
            sb.AppendLine("الجهة المستقبلة: " & Safe(doc.DestinationEntity))
            sb.AppendLine("الأولوية/السرية: " & Safe(doc.Priority) & " / " & Safe(doc.Confidentiality))
            sb.AppendLine("التوجيه: " & New SmartInboxRoutingService(AppServices.Repository).DetectStage(doc))
            sb.AppendLine("")
            sb.AppendLine("يمكنك إغلاق المعالج. تابع إشعار التقدم في بصيرة؛ بعد اكتمال المعالجة افتح المستند لمراجعة البيانات والنص.")
        End If
        Return sb.ToString()
    End Function

    Private Function BuildFolderResultText(folderPath As String, results As List(Of ImportResult)) As String
        Dim ok As Integer = 0
        Dim duplicateCount As Integer = 0
        Dim failed As Integer = 0
        For Each r In results
            If r.IsDuplicate Then duplicateCount += 1
            If Not String.IsNullOrWhiteSpace(r.DocumentId) AndAlso Safe(r.Status) <> "فشل" Then ok += 1
            If Safe(r.Status) = "فشل" Then failed += 1
        Next
        Dim sb As New StringBuilder()
        sb.AppendLine("نتيجة استيراد المجلد")
        sb.AppendLine(folderPath)
        sb.AppendLine("")
        sb.AppendLine("أُدرج للمعالجة: " & ok.ToString())
        sb.AppendLine("مكرر: " & duplicateCount.ToString())
        sb.AppendLine("فشل: " & failed.ToString())
        sb.AppendLine("إجمالي الملفات المدعومة: " & results.Count.ToString())
        sb.AppendLine("")
        For Each r In results
            sb.AppendLine("- " & Safe(r.Status) & " | " & Safe(r.Title) & " | " & Safe(r.Message))
        Next
        Return sb.ToString()
    End Function

    Private Function IsSupportedFile(filePath As String) As Boolean
        Dim ext = Path.GetExtension(filePath).ToLowerInvariant()
        Select Case ext
            Case ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".txt", ".csv", ".docx", ".xlsx"
                Return True
        End Select
        Return False
    End Function

    Private Sub btnOpenEditor_Click(sender As Object, e As EventArgs) Handles btnOpenEditor.Click
        If String.IsNullOrWhiteSpace(_lastDocumentId) Then Return
        Using f As New DocumentWorkspaceForm(_lastDocumentId)
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnOcrReport_Click(sender As Object, e As EventArgs) Handles btnOcrReport.Click
        If String.IsNullOrWhiteSpace(_lastDocumentId) Then Return
        Using f As New DocumentWorkspaceForm(_lastDocumentId)
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnDone_Click(sender As Object, e As EventArgs) Handles btnDone.Click
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Shared Function Safe(value As String) As String
        Return If(value, "").Trim()
    End Function

    Private Shared Function FormatBytes(size As Long) As String
        If size <= 0 Then Return "غير معروف"
        If size < 1024 Then Return size.ToString() & " B"
        If size < 1024 * 1024 Then Return Math.Round(size / 1024.0R, 1).ToString() & " KB"
        Return Math.Round(size / 1024.0R / 1024.0R, 1).ToString() & " MB"
    End Function
End Class
