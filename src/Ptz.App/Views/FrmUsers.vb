Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Security
Imports Ptz.Presentation

Namespace Views

    Public Class FrmUsers
        Implements IUserAdminView

        Public Event CreateRequested As Action(Of String, String, Role) Implements IUserAdminView.CreateRequested
        Public Event SetRoleRequested As Action(Of String, Role) Implements IUserAdminView.SetRoleRequested
        Public Event SetDisabledRequested As Action(Of String, Boolean) Implements IUserAdminView.SetDisabledRequested
        Public Event ChangePasswordRequested As Action(Of String, String) Implements IUserAdminView.ChangePasswordRequested

        Public Sub New()
            InitializeComponent()
            Theme.Apply(Me)
            For Each r As Role In [Enum].GetValues(GetType(Role))
                cmbNewRole.Items.Add(r)
                cmbRole.Items.Add(r)
            Next
            cmbNewRole.SelectedItem = Role.Viewer
        End Sub

        Private ReadOnly Property SelectedUser As String
            Get
                Return If(lvUsers.SelectedItems.Count = 0, Nothing, lvUsers.SelectedItems(0).Text)
            End Get
        End Property

        Public Sub ShowUsers(users As IReadOnlyList(Of UserRecord)) Implements IUserAdminView.ShowUsers
            lvUsers.BeginUpdate()
            lvUsers.Items.Clear()
            For Each u As UserRecord In users
                Dim item As New ListViewItem(u.UserName)
                item.SubItems.Add(u.Role.ToString())
                item.SubItems.Add(If(u.Disabled, "معطّل", If(u.LockedUntilUtc > DateTime.UtcNow, "مقفل مؤقتاً", "فعّال")))
                lvUsers.Items.Add(item)
            Next
            lvUsers.EndUpdate()
        End Sub

        Public Sub ShowError(message As String) Implements IMessageSink.ShowError
            MessageBox.Show(Me, message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Sub

        Public Sub ShowInfo(message As String) Implements IMessageSink.ShowInfo
            lblSel.Text = message
        End Sub

        Private Sub lvUsers_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lvUsers.SelectedIndexChanged
            Dim name As String = SelectedUser
            lblSel.Text = If(name, "—")
            If name IsNot Nothing Then cmbRole.SelectedItem = [Enum].Parse(GetType(Role), lvUsers.SelectedItems(0).SubItems(1).Text)
        End Sub

        Private Sub btnCreate_Click(sender As Object, e As EventArgs) Handles btnCreate.Click
            If cmbNewRole.SelectedItem Is Nothing Then Return
            RaiseEvent CreateRequested(txtNewUser.Text.Trim(), txtNewPass.Text, CType(cmbNewRole.SelectedItem, Role))
            txtNewPass.Clear()
        End Sub

        Private Sub btnSetRole_Click(sender As Object, e As EventArgs) Handles btnSetRole.Click
            If SelectedUser Is Nothing OrElse cmbRole.SelectedItem Is Nothing Then Return
            RaiseEvent SetRoleRequested(SelectedUser, CType(cmbRole.SelectedItem, Role))
        End Sub

        Private Sub btnToggle_Click(sender As Object, e As EventArgs) Handles btnToggle.Click
            If SelectedUser Is Nothing Then Return
            Dim disabled As Boolean = lvUsers.SelectedItems(0).SubItems(2).Text = "معطّل"
            RaiseEvent SetDisabledRequested(SelectedUser, Not disabled)
        End Sub

        Private Sub btnReset_Click(sender As Object, e As EventArgs) Handles btnReset.Click
            If SelectedUser Is Nothing Then Return
            RaiseEvent ChangePasswordRequested(SelectedUser, txtResetPass.Text)
            txtResetPass.Clear()
        End Sub

        Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
            Close()
        End Sub
    End Class

End Namespace
