Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports Ptz.App.Infrastructure

Namespace Controls

    ''' <summary>
    ''' عصا تحكم دائرية مرسومة يدوياً. أثناء الضغط تُطلق <c>VectorChanged</c> بقيم -1..1 للمحورين،
    ''' وعند الإفلات <c>Released</c>. دعم لوحة المفاتيح (الأسهم) عند التركيز.
    ''' </summary>
    Public Class JoystickControl
        Inherits Control

        Private _dragging As Boolean
        Private _vx As Double
        Private _vy As Double
        Private _keyX As Integer
        Private _keyY As Integer

        Public Event VectorChanged As Action(Of Double, Double)
        Public Event Released As Action

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
            TabStop = True
            Size = New Size(190, 190)
        End Sub

        Private ReadOnly Property Center As PointF
            Get
                Return New PointF(Width / 2.0F, Height / 2.0F)
            End Get
        End Property

        Private ReadOnly Property Radius As Single
            Get
                Return Math.Min(Width, Height) / 2.0F - 6.0F
            End Get
        End Property

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(BackColor)
            Dim c As PointF = Center
            Dim r As Single = Radius
            Using b As New SolidBrush(Theme.Surface)
                g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2)
            End Using
            Using p As New Pen(If(Focused, Theme.Accent, Theme.Border), 2)
                g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2)
            End Using
            Using p As New Pen(Theme.Border, 1)
                g.DrawLine(p, c.X - r, c.Y, c.X + r, c.Y)
                g.DrawLine(p, c.X, c.Y - r, c.X, c.Y + r)
            End Using
            ' أسهم الاتجاهات
            Using f As New Font("Segoe UI Symbol", 11.0F), b As New SolidBrush(Theme.Muted)
                Dim sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                g.DrawString(ChrW(&H25B2), f, b, c.X, c.Y - r + 14, sf)
                g.DrawString(ChrW(&H25BC), f, b, c.X, c.Y + r - 14, sf)
                g.DrawString(ChrW(&H25C0), f, b, c.X - r + 14, c.Y, sf)
                g.DrawString(ChrW(&H25B6), f, b, c.X + r - 14, c.Y, sf)
            End Using
            Dim kx As Single = c.X + CSng(_vx) * r * 0.7F
            Dim ky As Single = c.Y + CSng(_vy) * r * 0.7F
            Using b As New SolidBrush(If(_dragging OrElse _keyX <> 0 OrElse _keyY <> 0, Theme.Accent, Color.FromArgb(90, 104, 122)))
                g.FillEllipse(b, kx - 18, ky - 18, 36, 36)
            End Using
        End Sub

        Private Sub UpdateFromMouse(pt As Point)
            Dim c As PointF = Center
            Dim r As Double = Radius * 0.7
            Dim dx As Double = (pt.X - c.X) / r
            Dim dy As Double = (pt.Y - c.Y) / r
            Dim len As Double = Math.Sqrt(dx * dx + dy * dy)
            If len > 1 Then
                dx /= len
                dy /= len
            End If
            If Math.Abs(dx) < 0.12 Then dx = 0
            If Math.Abs(dy) < 0.12 Then dy = 0
            SetVector(dx, dy)
        End Sub

        Private Sub SetVector(x As Double, y As Double)
            _vx = x
            _vy = y
            Invalidate()
            RaiseEvent VectorChanged(x, y)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            Focus()
            _dragging = True
            Capture = True
            UpdateFromMouse(e.Location)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If _dragging Then UpdateFromMouse(e.Location)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            If Not _dragging Then Return
            _dragging = False
            Capture = False
            Release()
        End Sub

        Protected Overrides Sub OnMouseCaptureChanged(e As EventArgs)
            MyBase.OnMouseCaptureChanged(e)
            ' فُقد الالتقاط أثناء السحب (نافذة حوار، Alt-Tab) دون MouseUp ⇒ نحرّر وإلا بقيت الحركة مستمرة
            If _dragging AndAlso Not Capture Then
                _dragging = False
                Release()
            End If
        End Sub

        Private Sub Release()
            _vx = 0
            _vy = 0
            Invalidate()
            RaiseEvent Released()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            If _dragging Then
                _dragging = False
                Capture = False
                Release()
            End If
            If _keyX <> 0 OrElse _keyY <> 0 Then
                _keyX = 0
                _keyY = 0
                Release()
            End If
            Invalidate()
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Select Case keyData And Keys.KeyCode
                Case Keys.Left, Keys.Right, Keys.Up, Keys.Down : Return True
            End Select
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Select Case e.KeyCode
                Case Keys.Left : _keyX = -1
                Case Keys.Right : _keyX = 1
                Case Keys.Up : _keyY = -1
                Case Keys.Down : _keyY = 1
                Case Else : Return
            End Select
            e.Handled = True
            SetVector(_keyX, _keyY)
        End Sub

        Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
            MyBase.OnKeyUp(e)
            Select Case e.KeyCode
                Case Keys.Left, Keys.Right : _keyX = 0
                Case Keys.Up, Keys.Down : _keyY = 0
                Case Else : Return
            End Select
            e.Handled = True
            If _keyX = 0 AndAlso _keyY = 0 Then Release() Else SetVector(_keyX, _keyY)
        End Sub
    End Class

End Namespace
