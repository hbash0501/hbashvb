Imports System.Drawing
Imports System.Windows.Forms

Public NotInheritable Class UiThemeService
    Private Sub New()
    End Sub

    Public Shared ReadOnly Property DarkGreen As Color
        Get
            Return Color.FromArgb(13, 49, 46)
        End Get
    End Property

    Public Shared ReadOnly Property Green As Color
        Get
            Return Color.FromArgb(29, 91, 80)
        End Get
    End Property

    Public Shared ReadOnly Property Gold As Color
        Get
            Return Color.FromArgb(198, 159, 76)
        End Get
    End Property

    Public Shared ReadOnly Property Paper As Color
        Get
            Return Color.FromArgb(248, 250, 248)
        End Get
    End Property

    Public Shared ReadOnly Property LineColor As Color
        Get
            Return Color.FromArgb(224, 230, 226)
        End Get
    End Property

    Public Shared Function Accent(name As String) As Color
        Select Case If(name, "").ToLowerInvariant()
            Case "gold"
                Return Gold
            Case "blue"
                Return Color.FromArgb(60, 117, 170)
            Case "purple"
                Return Color.FromArgb(112, 79, 160)
            Case "red"
                Return Color.FromArgb(170, 78, 75)
            Case "gray"
                Return Color.FromArgb(98, 111, 108)
            Case Else
                Return Green
        End Select
    End Function

    Public Shared Sub StylePrimaryButton(button As Button, accentName As String)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderSize = 0
        button.BackColor = Accent(accentName)
        button.ForeColor = Color.White
        button.Font = New Font("Tahoma", 9.0F, FontStyle.Bold)
        button.Height = 38
        button.Cursor = Cursors.Hand
        button.TextAlign = ContentAlignment.MiddleCenter
    End Sub

    Public Shared Sub StyleLightButton(button As Button)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = LineColor
        button.FlatAppearance.BorderSize = 1
        button.BackColor = Color.White
        button.ForeColor = Color.FromArgb(36, 48, 45)
        button.Font = New Font("Tahoma", 9.0F, FontStyle.Regular)
        button.Height = 36
        button.Cursor = Cursors.Hand
    End Sub

    Public Shared Sub StyleTextBox(textBox As TextBox)
        textBox.BorderStyle = BorderStyle.FixedSingle
        textBox.Font = New Font("Tahoma", 10.0F, FontStyle.Regular)
        textBox.BackColor = Color.White
        textBox.ForeColor = Color.FromArgb(35, 44, 41)
    End Sub

    Public Shared Sub StyleDataGrid(grid As DataGridView, Optional withSelectionStyle As Boolean = True)
        If grid Is Nothing Then Return
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(23, 58, 54)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Tahoma", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.Font = New Font("Tahoma", 9.0F)
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 249)
        If withSelectionStyle Then
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 176, 92)
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 32, 30)
        End If
    End Sub

    Public Shared Function BuildCard(title As String, valueText As String, note As String, accentName As String) As Panel
        Dim p As New Panel()
        p.Width = 210
        p.Height = 122
        p.Margin = New Padding(8)
        p.Padding = New Padding(14)
        p.BackColor = Color.White
        p.BorderStyle = BorderStyle.FixedSingle

        Dim stripe As New Panel()
        stripe.Dock = DockStyle.Right
        stripe.Width = 7
        stripe.BackColor = Accent(accentName)
        p.Controls.Add(stripe)

        Dim lblTitle As New Label()
        lblTitle.Text = title
        lblTitle.AutoSize = False
        lblTitle.Dock = DockStyle.Top
        lblTitle.Height = 28
        lblTitle.Font = New Font("Tahoma", 9.0F, FontStyle.Bold)
        lblTitle.ForeColor = Color.FromArgb(54, 67, 63)
        p.Controls.Add(lblTitle)

        Dim lblValue As New Label()
        lblValue.Text = valueText
        lblValue.AutoSize = False
        lblValue.Dock = DockStyle.Top
        lblValue.Height = 38
        lblValue.Font = New Font("Tahoma", 18.0F, FontStyle.Bold)
        lblValue.ForeColor = Accent(accentName)
        p.Controls.Add(lblValue)

        Dim lblNote As New Label()
        lblNote.Text = note
        lblNote.AutoSize = False
        lblNote.Dock = DockStyle.Fill
        lblNote.Font = New Font("Tahoma", 8.0F, FontStyle.Regular)
        lblNote.ForeColor = Color.FromArgb(92, 104, 100)
        p.Controls.Add(lblNote)

        Return p
    End Function
End Class
