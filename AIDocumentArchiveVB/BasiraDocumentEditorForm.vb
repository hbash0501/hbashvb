Imports System
Imports System.Linq
Imports System.Windows.Forms
Partial Public Class BasiraDocumentEditorForm
    Private ReadOnly _id As String
    Private ReadOnly _loadedAt As DateTime
    Public Sub New(documentId As String)
        InitializeComponent()
        _id = documentId
        Dim d = AppServices.Repository.GetDocumentById(documentId)
        If d Is Nothing Then Throw New InvalidOperationException("لم يتم العثور على المستند.")
        _loadedAt = d.UpdatedAt
        txtTitle.Text = If(d.Title, "")
        txtGeneralCategory.Text = If(d.GeneralCategory, "")
        txtSubCategory.Text = If(d.SubCategory, "")
        txtReferenceNumber.Text = If(d.ReferenceNumber, "")
        txtSourceEntity.Text = If(d.SourceEntity, "")
        txtDestinationEntity.Text = If(d.DestinationEntity, "")
        txtDueDateText.Text = If(d.DueDateText, "")
        txtTags.Text = If(d.Tags, "")
        txtConfidentiality.Text = If(d.Confidentiality, "")
        txtPriority.Text = If(d.Priority, "")
        txtDocumentText.Text = If(d.TextContent, "")
    End Sub
    Private Sub SaveClick(sender As Object, e As EventArgs) Handles btnSave.Click
        If Not AppServices.Auth.HasPermission("edit") Then Return
        If String.IsNullOrWhiteSpace(txtTitle.Text) Then
            lblNote.Text = "العنوان مطلوب."
            Return
        End If
        Dim due As DateTime
        If Not String.IsNullOrWhiteSpace(txtDueDateText.Text) AndAlso Not BasiraEngine.TryDue(txtDueDateText.Text, due) Then
            lblNote.Text = "استخدم تاريخاً كاملاً مثل 2026/10/03 أو 1448/04/22هـ."
            Return
        End If
        If Not New String() {"عادي", "عام", "غير سري", "سري", "سري للغاية"}.Contains(txtConfidentiality.Text.Trim()) Then
            lblNote.Text = "السرية: عادي، عام، غير سري، سري، أو سري للغاية."
            Return
        End If
        Try
            If AppServices.ProcessingQueue.GetJobs().Any(Function(j) j.DocumentId = _id AndAlso (j.State = "منتظر" OrElse j.State = "قيد التنفيذ")) Then
                lblNote.Text = "انتظر انتهاء معالجة هذا المستند قبل حفظ التعديلات."
                Return
            End If
            Dim d = AppServices.Repository.GetDocumentById(_id)
            If d Is Nothing OrElse d.UpdatedAt <> _loadedAt Then
                lblNote.Text = "تغيّر المستند أثناء التحرير. أعد فتحه لمراجعة أحدث نسخة قبل الحفظ."
                Return
            End If
            d.Title = txtTitle.Text.Trim()
            d.GeneralCategory = txtGeneralCategory.Text.Trim()
            d.SubCategory = txtSubCategory.Text.Trim()
            d.ReferenceNumber = txtReferenceNumber.Text.Trim()
            d.SourceEntity = txtSourceEntity.Text.Trim()
            d.DestinationEntity = txtDestinationEntity.Text.Trim()
            d.DueDateText = txtDueDateText.Text.Trim()
            d.Tags = txtTags.Text.Trim()
            d.Confidentiality = txtConfidentiality.Text.Trim()
            d.Priority = txtPriority.Text.Trim()
            d.TextContent = txtDocumentText.Text
            FastEntityExtractionService.ApplyFastEntities(d)
            AppServices.Repository.SaveDocument(d)
            AppServices.EnterpriseDb.UpsertDocument(d)
            AppServices.Repository.AddAudit("تحرير بيانات ونص بصيرة", d.Id, d.Title, "حفظ يدوي مع نسخة سابقة")
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            lblNote.Text = "تعذر حفظ التعديلات: " & ex.Message
        End Try
    End Sub
End Class
