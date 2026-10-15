Imports System
Imports System.Windows.Forms

Partial Public Class UserManagerForm
    Private _editingId As String

    Public Sub New()
        InitializeComponent()
        UiThemeService.StyleDataGrid(dgvUsers, False)
    End Sub

    Private Sub UserManagerForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        cboRole.Items.AddRange(AuthService.AvailableRoles())
        cboRole.SelectedIndex = 1
        RefreshUsers()
    End Sub

    Private Sub RefreshUsers()
        dgvUsers.DataSource = AppServices.Auth.UsersToDataTable(AppServices.Auth.GetUsers())
        If dgvUsers.Columns.Contains("Id") Then dgvUsers.Columns("Id").Visible = False
        ClearEditor(False)
    End Sub

    Private Sub ClearEditor(Optional clearSelection As Boolean = True)
        _editingId = ""
        txtFullName.Text = ""
        txtUserName.Text = ""
        txtPassword.Text = ""
        If cboRole.Items.Count > 0 Then cboRole.SelectedIndex = 1
        chkActive.Checked = True
        If clearSelection Then dgvUsers.ClearSelection()
    End Sub

    Private Sub dgvUsers_SelectionChanged(sender As Object, e As EventArgs) Handles dgvUsers.SelectionChanged
        If dgvUsers.CurrentRow Is Nothing OrElse dgvUsers.CurrentRow.Cells("Id") Is Nothing Then Return
        Dim id = Convert.ToString(dgvUsers.CurrentRow.Cells("Id").Value)
        Dim u = AppServices.Auth.GetUserById(id)
        If u Is Nothing Then Return
        _editingId = u.Id
        txtFullName.Text = u.FullName
        txtUserName.Text = u.UserName
        txtPassword.Text = ""
        SelectRole(u.RoleName)
        chkActive.Checked = u.IsActive
    End Sub

    Private Sub SelectRole(roleName As String)
        For i = 0 To cboRole.Items.Count - 1
            If cboRole.Items(i).ToString() = roleName Then
                cboRole.SelectedIndex = i
                Return
            End If
        Next
    End Sub

    Private Sub btnNew_Click(sender As Object, e As EventArgs) Handles btnNew.Click
        ClearEditor()
        txtFullName.Focus()
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        If String.IsNullOrWhiteSpace(txtFullName.Text) OrElse String.IsNullOrWhiteSpace(txtUserName.Text) Then
            MessageBox.Show("أدخل اسم الموظف واسم المستخدم.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If String.IsNullOrWhiteSpace(_editingId) AndAlso String.IsNullOrWhiteSpace(txtPassword.Text) Then
            MessageBox.Show("أدخل كلمة مرور للحساب الجديد.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            Dim u As UserAccount = If(String.IsNullOrWhiteSpace(_editingId), New UserAccount(), AppServices.Auth.GetUserById(_editingId))
            If u Is Nothing Then u = New UserAccount()
            u.FullName = txtFullName.Text.Trim()
            u.UserName = txtUserName.Text.Trim()
            u.RoleName = If(cboRole.SelectedItem, "موظف أرشفة").ToString()
            u.IsActive = chkActive.Checked
            AppServices.Auth.SaveUser(u, If(String.IsNullOrWhiteSpace(txtPassword.Text), Nothing, txtPassword.Text))
            AppServices.Repository.AddAudit("حفظ مستخدم", "", "", u.UserName & " - " & u.RoleName)
            RefreshUsers()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر حفظ المستخدم", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnDeactivate_Click(sender As Object, e As EventArgs) Handles btnDeactivate.Click
        If String.IsNullOrWhiteSpace(_editingId) Then Return
        If MessageBox.Show("هل تريد تعطيل هذا الحساب؟", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        Try
            AppServices.Auth.DeactivateUser(_editingId)
            AppServices.Repository.AddAudit("تعطيل مستخدم", "", "", txtUserName.Text)
            RefreshUsers()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "تعذر التعطيل", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


End Class
