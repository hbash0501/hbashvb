Imports System.Drawing
Imports System.Windows.Forms

Namespace Infrastructure

    ''' <summary>الثيم الداكن الموحّد: ألوان وخطوط، وتطبيق متكرر على شجرة العناصر.</summary>
    Public NotInheritable Class Theme
        Private Sub New()
        End Sub

        Public Shared ReadOnly Back As Color = Color.FromArgb(20, 24, 29)
        Public Shared ReadOnly Panel As Color = Color.FromArgb(28, 34, 42)
        Public Shared ReadOnly Surface As Color = Color.FromArgb(37, 45, 55)
        Public Shared ReadOnly Border As Color = Color.FromArgb(58, 69, 83)
        Public Shared ReadOnly Accent As Color = Color.FromArgb(47, 156, 244)
        Public Shared ReadOnly Text As Color = Color.FromArgb(230, 234, 240)
        Public Shared ReadOnly Muted As Color = Color.FromArgb(139, 150, 165)
        Public Shared ReadOnly Danger As Color = Color.FromArgb(229, 72, 77)
        Public Shared ReadOnly Warn As Color = Color.FromArgb(245, 165, 36)
        Public Shared ReadOnly Ok As Color = Color.FromArgb(48, 164, 108)

        Public Shared ReadOnly BaseFont As New Font("Segoe UI", 9.5F)
        Public Shared ReadOnly TitleFont As New Font("Segoe UI Semibold", 11.5F)

        Public Shared Sub Apply(root As Control)
            root.BackColor = Back
            root.ForeColor = Text
            If TypeOf root Is Form Then root.Font = BaseFont
            For Each c As Control In root.Controls
                ApplyOne(c)
            Next
        End Sub

        Private Shared Sub ApplyOne(c As Control)
            Dim btn As Button = TryCast(c, Button)
            If btn IsNot Nothing Then
                btn.FlatStyle = FlatStyle.Flat
                btn.FlatAppearance.BorderColor = Border
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 58, 70)
                btn.FlatAppearance.MouseDownBackColor = Accent
                btn.BackColor = Surface
                btn.ForeColor = Text
                btn.UseVisualStyleBackColor = False
                btn.Cursor = Cursors.Hand
            ElseIf TypeOf c Is TextBox OrElse TypeOf c Is NumericUpDown OrElse TypeOf c Is ComboBox OrElse TypeOf c Is ListBox OrElse TypeOf c Is ListView Then
                c.BackColor = Surface
                c.ForeColor = Text
                Dim tb As TextBox = TryCast(c, TextBox)
                If tb IsNot Nothing Then tb.BorderStyle = BorderStyle.FixedSingle
                Dim cmb As ComboBox = TryCast(c, ComboBox)
                If cmb IsNot Nothing Then
                    cmb.FlatStyle = FlatStyle.Flat
                    cmb.DropDownStyle = ComboBoxStyle.DropDownList
                End If
            ElseIf TypeOf c Is GroupBox Then
                c.ForeColor = Accent
                c.BackColor = Panel
            ElseIf TypeOf c Is TabPage Then
                c.BackColor = Back
            ElseIf TypeOf c Is TrackBar OrElse TypeOf c Is SplitContainer Then
                c.BackColor = Panel
            ElseIf TypeOf c Is StatusStrip Then
                c.BackColor = Panel
                c.ForeColor = Text
            ElseIf TypeOf c Is CheckBox OrElse TypeOf c Is RadioButton Then
                c.ForeColor = Text
            ElseIf TypeOf c Is Label Then
                ' التسمية التي وسمها المصمم بـ Tag="muted" تبقى خافتة
                c.ForeColor = If(TypeOf c.Tag Is String AndAlso CStr(c.Tag) = "muted", Muted, Text)
            End If
            Dim sc As SplitContainer = TryCast(c, SplitContainer)
            If sc IsNot Nothing Then
                sc.Panel1.BackColor = Back
                sc.Panel2.BackColor = Back
                For Each k As Control In sc.Panel1.Controls
                    ApplyOne(k)
                Next
                For Each k As Control In sc.Panel2.Controls
                    ApplyOne(k)
                Next
                Return
            End If
            If TypeOf c Is UserControl Then Return ' اللوحات تطبّق الثيم على نفسها
            For Each k As Control In c.Controls
                ApplyOne(k)
            Next
        End Sub
    End Class

End Namespace
