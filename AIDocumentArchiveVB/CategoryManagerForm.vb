
Imports System
Imports System.Data
Imports System.Windows.Forms

Partial Public Class CategoryManagerForm
    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub CategoryManagerForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LoadGrid()
        UiThemeService.StyleDataGrid(dgvCategories, False)
    End Sub

    Private Sub LoadGrid()
        Dim t As New DataTable()
        t.Columns.Add("التصنيف العام")
        t.Columns.Add("التصنيف الثانوي")
        For Each c In AppServices.Repository.GetAllCategories()
            t.Rows.Add(c.GeneralName, c.SubName)
        Next
        dgvCategories.DataSource = t
    End Sub

    Private Sub btnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
        If String.IsNullOrWhiteSpace(txtGeneral.Text) Then
            MessageBox.Show("اكتب التصنيف العام أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        AppServices.Repository.AddCategory(txtGeneral.Text, txtSub.Text)
        txtGeneral.Clear()
        txtSub.Clear()
        LoadGrid()
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub
End Class
