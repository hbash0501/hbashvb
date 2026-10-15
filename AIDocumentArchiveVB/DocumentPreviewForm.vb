Imports System
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Partial Public Class DocumentPreviewForm
    Private ReadOnly _documentId As String
    Private _record As DocumentRecord

    Public Sub New(documentId As String)
        InitializeComponent()
        _documentId = documentId
    End Sub

    Private Sub DocumentPreviewForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LoadDocument()
    End Sub

    Private Sub LoadDocument()
        _record = AppServices.Repository.GetDocumentById(_documentId)
        If _record Is Nothing Then
            MessageBox.Show("تعذر العثور على المستند.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Me.Close()
            Return
        End If

        lblTitle.Text = _record.Title
        lblSub.Text = _record.GeneralCategory & " / " & _record.SubCategory & " — " & _record.Status
        txtExtracted.Text = If(String.IsNullOrWhiteSpace(_record.TextContent), "لا يوجد نص مستخرج بعد.", _record.TextContent)
        txtMeta.Text = BuildMetaText(_record)
        ShowPreview(_record.StoredPath)
        AppServices.Repository.AddAudit("معاينة مستند", _record.Id, _record.Title, "فتح شاشة المعاينة")
    End Sub

    Private Sub ShowPreview(filePath As String)
        picPreview.Visible = False
        previewBrowser.Visible = False
        lblPreviewHint.Visible = True
        lblPreviewHint.BringToFront()

        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then
            lblPreviewHint.Text = "لا يوجد ملف محفوظ لهذا السجل."
            Return
        End If

        Dim ext = Path.GetExtension(filePath).ToLowerInvariant()
        If New String() {".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"}.Contains(ext) Then
            Try
                If picPreview.Image IsNot Nothing Then picPreview.Image.Dispose()
                Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    Using tempImg = Image.FromStream(fs)
                        picPreview.Image = New Bitmap(tempImg)
                    End Using
                End Using
                lblPreviewHint.Visible = False
                picPreview.Visible = True
                picPreview.BringToFront()
            Catch ex As Exception
                lblPreviewHint.Text = "تعذر عرض الصورة: " & ex.Message
            End Try
        ElseIf New String() {".pdf", ".html", ".htm", ".txt"}.Contains(ext) Then
            Try
                lblPreviewHint.Visible = False
                previewBrowser.Visible = True
                previewBrowser.BringToFront()
                previewBrowser.Navigate(filePath)
            Catch ex As Exception
                lblPreviewHint.Text = "تعذر عرض الملف داخل النافذة. استخدم زر فتح الملف."
            End Try
        Else
            lblPreviewHint.Text = "هذا النوع لا يدعم المعاينة الداخلية حالياً. يمكنك فتحه بالبرنامج الافتراضي من زر فتح الملف." & Environment.NewLine & Path.GetFileName(filePath)
        End If
    End Sub

    Private Shared Function BuildMetaText(d As DocumentRecord) As String
        Return "العنوان: " & d.Title & Environment.NewLine &
               "رقم المرجع: " & d.ReferenceNumber & Environment.NewLine &
               "التصنيف: " & d.GeneralCategory & " / " & d.SubCategory & Environment.NewLine &
               "الأولوية: " & d.Priority & Environment.NewLine &
               "السرية: " & d.Confidentiality & Environment.NewLine &
               "الحالة: " & d.Status & Environment.NewLine &
               "الموظف: " & d.EmployeeName & Environment.NewLine &
               "الوسوم: " & d.Tags & Environment.NewLine &
               "تاريخ الإضافة: " & d.CreatedAt.ToString("yyyy/MM/dd HH:mm") & Environment.NewLine &
               "آخر تحديث: " & d.UpdatedAt.ToString("yyyy/MM/dd HH:mm") & Environment.NewLine &
               "الملف: " & d.FileName & Environment.NewLine &
               "المسار: " & d.StoredPath & Environment.NewLine & Environment.NewLine &
               "الملخص:" & Environment.NewLine & d.Summary
    End Function

    Private Sub btnOpenExternal_Click(sender As Object, e As EventArgs) Handles btnOpenExternal.Click
        If _record Is Nothing OrElse String.IsNullOrWhiteSpace(_record.StoredPath) OrElse Not File.Exists(_record.StoredPath) Then Return
        Process.Start(New ProcessStartInfo(_record.StoredPath) With {.UseShellExecute = True})
    End Sub

    Private Sub btnOpenFolder_Click(sender As Object, e As EventArgs) Handles btnOpenFolder.Click
        If _record Is Nothing OrElse String.IsNullOrWhiteSpace(_record.StoredPath) OrElse Not File.Exists(_record.StoredPath) Then Return
        Process.Start("explorer.exe", "/select,""" & _record.StoredPath & """")
    End Sub
End Class
