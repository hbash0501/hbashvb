Imports System.Drawing
Imports System.Windows.Forms
Imports Ptz.App.Infrastructure

Namespace Controls

    ''' <summary>TabControl بمظهر داكن (رسم يدوي للتبويبات).</summary>
    Public Class ThemedTabControl
        Inherits TabControl

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
            DrawMode = TabDrawMode.OwnerDrawFixed
            SizeMode = TabSizeMode.Fixed
            ItemSize = New Size(110, 30)
            Padding = New Point(10, 4)
        End Sub

        Protected Overrides Sub OnDrawItem(e As DrawItemEventArgs)
            Dim selected As Boolean = (e.Index = SelectedIndex)
            Using b As New SolidBrush(If(selected, Theme.Surface, Theme.Panel))
                e.Graphics.FillRectangle(b, e.Bounds)
            End Using
            If selected Then
                Using p As New Pen(Theme.Accent, 3)
                    e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 2, e.Bounds.Right, e.Bounds.Bottom - 2)
                End Using
            End If
            Dim sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
            Using b As New SolidBrush(If(selected, Theme.Text, Theme.Muted))
                e.Graphics.DrawString(TabPages(e.Index).Text, Font, b, e.Bounds, sf)
            End Using
        End Sub

        Protected Overrides Sub OnPaintBackground(pevent As PaintEventArgs)
            pevent.Graphics.Clear(Theme.Panel)
        End Sub
    End Class

End Namespace
