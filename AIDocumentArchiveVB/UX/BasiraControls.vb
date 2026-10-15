Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public NotInheritable Class BasiraTheme
    Public Shared ReadOnly Ink As Color = Color.FromArgb(21, 35, 47)
    Public Shared ReadOnly Muted As Color = Color.FromArgb(108, 122, 132)
    Public Shared ReadOnly Canvas As Color = Color.FromArgb(245, 247, 250)
    Public Shared ReadOnly Teal As Color = Color.FromArgb(12, 120, 109)
    Public Shared ReadOnly Gold As Color = Color.FromArgb(211, 177, 110)
    Public Shared ReadOnly Line As Color = Color.FromArgb(228, 234, 239)

    Public Shared Function Button(text As String, Optional primary As Boolean = False) As Button
        Dim b As New Button With {.Text = text, .AutoSize = False, .Width = 145, .Height = 38, .Margin = New Padding(4), .FlatStyle = FlatStyle.Flat, .Cursor = Cursors.Hand, .BackColor = If(primary, Teal, Color.White), .ForeColor = If(primary, Color.White, Ink), .Font = New Font("Segoe UI", 10.0F)}
        b.FlatAppearance.BorderSize = If(primary, 0, 1)
        b.FlatAppearance.BorderColor = Line
        Return b
    End Function

    Public Shared Function Grid() As DataGridView
        Dim g As New DataGridView With {.Dock = DockStyle.Fill, .BackgroundColor = Color.White, .BorderStyle = BorderStyle.None, .ReadOnly = True, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .AllowUserToResizeRows = False, .RowHeadersVisible = False, .SelectionMode = DataGridViewSelectionMode.FullRowSelect, .MultiSelect = False, .AutoGenerateColumns = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, .EnableHeadersVisualStyles = False, .ColumnHeadersHeight = 44, .RightToLeft = RightToLeft.Yes}
        g.DefaultCellStyle.Font = New Font("Segoe UI", 10.0F)
        g.DefaultCellStyle.ForeColor = Ink
        g.DefaultCellStyle.BackColor = Color.White
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(227, 244, 239)
        g.DefaultCellStyle.SelectionForeColor = Teal
        g.DefaultCellStyle.Padding = New Padding(7)
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 242, 246)
        g.ColumnHeadersDefaultCellStyle.ForeColor = Muted
        g.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
        g.RowTemplate.Height = 52
        g.GridColor = Line
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(251, 252, 253)
        Return g
    End Function

    Public Shared Sub Column(grid As DataGridView, key As String, title As String, Optional weight As Single = 100.0F)
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = key, .DataPropertyName = key, .HeaderText = title, .FillWeight = weight})
    End Sub

    Public Shared Function ReadBox() As RichTextBox
        Return New RichTextBox With {.Dock = DockStyle.Fill, .BorderStyle = BorderStyle.None, .ReadOnly = True, .DetectUrls = False, .BackColor = Color.White, .ForeColor = Ink, .Font = New Font("Segoe UI", 11.0F), .RightToLeft = RightToLeft.Yes}
    End Function
End Class

Public Class BasiraBanner
    Inherits Control
    Public Property Heading As String = "صباح المعرفة، ووضوح القرار."
    Public Property Caption As String = "أرشيف واحد. أدلة مترابطة. عمل أكثر وضوحاً."
    Public Sub New()
        DoubleBuffered = True
        ResizeRedraw = True
        BackColor = BasiraTheme.Ink
    End Sub
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Using brush As New LinearGradientBrush(ClientRectangle, Color.FromArgb(21, 35, 47), Color.FromArgb(17, 69, 68), 15.0F)
            e.Graphics.FillRectangle(brush, ClientRectangle)
        End Using
        For i = 1 To 4
            Using pen As New Pen(Color.FromArgb(22, 183, 225, 211), 1.0F)
                e.Graphics.DrawEllipse(pen, 15 - i * 16, 14 - i * 16, 160 + i * 32, 160 + i * 32)
            End Using
        Next
        Using format As New StringFormat With {.Alignment = StringAlignment.Far, .FormatFlags = StringFormatFlags.DirectionRightToLeft}, titleFont As New Font("Segoe UI", 25.0F, FontStyle.Bold), subFont As New Font("Segoe UI", 10.5F), white As New SolidBrush(Color.White), gold As New SolidBrush(BasiraTheme.Gold)
            ' Near مع DirectionRightToLeft يضع النص بمحاذاة اليمين.
            format.Alignment = StringAlignment.Near
            e.Graphics.DrawString("BASIRA  /  بصيرة", subFont, gold, New RectangleF(28, 18, Width - 56, 26), format)
            e.Graphics.DrawString(Heading, titleFont, white, New RectangleF(28, 48, Width - 56, 58), format)
            e.Graphics.DrawString(Caption, subFont, white, New RectangleF(28, 111, Width - 56, 28), format)
        End Using
    End Sub
End Class

Public Class BasiraMetricCard
    Inherits Control
    Public Property TitleText As String
    Public Property ValueText As String
    Public Property CaptionText As String
    Public Property Accent As Color = BasiraTheme.Teal
    Public Sub New()
        DoubleBuffered = True
        ResizeRedraw = True
        BackColor = Color.White
        Size = New Size(230, 118)
        Margin = New Padding(7)
    End Sub
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Using stripe As New SolidBrush(Accent), title As New Font("Segoe UI", 10.0F), value As New Font("Segoe UI", 27.0F, FontStyle.Bold), caption As New Font("Segoe UI", 8.5F), ink As New SolidBrush(BasiraTheme.Ink), muted As New SolidBrush(BasiraTheme.Muted), format As New StringFormat With {.FormatFlags = StringFormatFlags.DirectionRightToLeft, .Alignment = StringAlignment.Near}
            e.Graphics.FillRectangle(stripe, Width - 4, 0, 4, Height)
            e.Graphics.DrawString(TitleText, title, muted, New RectangleF(12, 12, Width - 28, 23), format)
            e.Graphics.DrawString(ValueText, value, ink, New RectangleF(12, 35, Width - 28, 50), format)
            e.Graphics.DrawString(CaptionText, caption, muted, New RectangleF(12, 88, Width - 28, 25), format)
        End Using
    End Sub
End Class

Public Class BasiraGraphControl
    Inherits Control
    Private _center As DocumentRecord
    Private _links As New List(Of BasiraLink)
    Private ReadOnly _targets As New List(Of Tuple(Of RectangleF, String))
    Public Event DocumentClicked(id As String)
    Public Sub New()
        DoubleBuffered = True
        ResizeRedraw = True
        BackColor = Color.White
    End Sub
    Public Sub SetDocument(document As DocumentRecord, links As List(Of BasiraLink))
        _center = document
        _links = links
        Invalidate()
    End Sub
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        _targets.Clear()
        If _center Is Nothing Then Return
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim center As New PointF(Width / 2.0F, Height / 2.0F)
        Dim count = Math.Min(6, _links.Count)
        For i = 0 To count - 1
            Dim angle = -Math.PI / 2 + i * Math.PI * 2 / Math.Max(1, count)
            Dim target As New PointF(center.X + CSng(Math.Cos(angle)) * Math.Max(0.0F, Width / 2.0F - 130), center.Y + CSng(Math.Sin(angle)) * Math.Max(0.0F, Height / 2.0F - 55))
            Using pen As New Pen(Color.FromArgb(167, 198, 191), 1.6F)
                e.Graphics.DrawLine(pen, center, target)
            End Using
            Dim bounds As New RectangleF(target.X - 115, target.Y - 34, 230, 68)
            DrawNode(e.Graphics, bounds, _links(i).Document.Title, _links(i).Reason, False)
            _targets.Add(Tuple.Create(bounds, _links(i).Document.Id))
        Next
        DrawNode(e.Graphics, New RectangleF(center.X - 115, center.Y - 40, 230, 80), _center.Title, "المستند المحدد", True)
        If count = 0 Then
            Using brush As New SolidBrush(BasiraTheme.Muted)
                e.Graphics.DrawString("لا توجد علاقة بالبصمة أو المرجع أو الجهة. أضف بيانات المستند لتظهر روابطه.", Font, brush, New RectangleF(20, 20, Width - 40, 70))
            End Using
        End If
    End Sub
    Private Shared Sub DrawNode(g As Graphics, r As RectangleF, title As String, note As String, selected As Boolean)
        Using background As New SolidBrush(If(selected, BasiraTheme.Teal, Color.FromArgb(239, 246, 244))), foreground As New SolidBrush(If(selected, Color.White, BasiraTheme.Ink)), titleFont As New Font("Segoe UI", 9.0F, FontStyle.Bold), subFont As New Font("Segoe UI", 8.0F), format As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center, .FormatFlags = StringFormatFlags.DirectionRightToLeft, .Trimming = StringTrimming.EllipsisCharacter}
            g.FillRectangle(background, r)
            g.DrawString(title, titleFont, foreground, New RectangleF(r.X + 6, r.Y + 4, r.Width - 12, r.Height / 2), format)
            g.DrawString(note, subFont, foreground, New RectangleF(r.X + 6, r.Y + r.Height / 2, r.Width - 12, r.Height / 2 - 4), format)
        End Using
    End Sub
    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        For Each t In _targets
            If t.Item1.Contains(e.Location) Then RaiseEvent DocumentClicked(t.Item2)
        Next
    End Sub
End Class
