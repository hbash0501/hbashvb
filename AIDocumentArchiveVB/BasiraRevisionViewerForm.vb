Imports System
Imports System.Linq
Imports System.Windows.Forms
Imports System.Xml.Linq

Partial Public Class BasiraRevisionViewerForm
    Private _document As DocumentRecord
    Public Sub New(documentId As String)
        InitializeComponent()
        _document = AppServices.Repository.GetDocumentById(documentId)
        If _document Is Nothing Then Throw New InvalidOperationException("لم يتم العثور على المستند.")
        currentText.Text = "العنوان: " & _document.Title & vbCrLf & "الحالة: " & _document.Status & vbCrLf & "الملخص: " & _document.Summary & vbCrLf & vbCrLf & _document.TextContent
        For Each revision In AppServices.Repository.VersionStore.Revisions(documentId)
            Dim d = revision.Element("Document")
            Dim index = gridVersions.Rows.Add(CStr(revision.Attribute("CapturedAt")), CStr(d.Element("Title")), CStr(d.Element("Status")))
            gridVersions.Rows(index).Tag = revision
        Next
        lblDifference.Text = If(gridVersions.Rows.Count = 0, "تظهر النسخ هنا بعد أول تعديل للبيانات أو النص.", "اختر نسخة سابقة لقراءتها بجانب النسخة الحالية.")
    End Sub
    Private Sub VersionSelection(sender As Object, e As EventArgs) Handles gridVersions.SelectionChanged
        Dim revision = TryCast(gridVersions.CurrentRow?.Tag, XElement)
        If revision Is Nothing OrElse _document Is Nothing Then Return
        Dim d = revision.Element("Document")
        Dim oldText = If(CStr(d.Element("TextContent")), "")
        previousText.Text = "العنوان: " & CStr(d.Element("Title")) & vbCrLf & "الحالة: " & CStr(d.Element("Status")) & vbCrLf & "الملخص: " & CStr(d.Element("Summary")) & vbCrLf & vbCrLf & oldText
        Dim before = oldText.Replace(vbCrLf, vbLf).Split(ChrW(10)).Where(Function(s) s.Trim() <> "").ToArray()
        Dim after = If(_document.TextContent, "").Replace(vbCrLf, vbLf).Split(ChrW(10)).Where(Function(s) s.Trim() <> "").ToArray()
        lblDifference.Text = "أسطر ظهرت في النص الحالي: " & after.Except(before).Count().ToString() & "   |   أسطر موجودة في السابق فقط: " & before.Except(after).Count().ToString() & "   •   مقارنة نصية بالأسطر الفريدة"
    End Sub
End Class
