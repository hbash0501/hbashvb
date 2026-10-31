Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports Ptz.App.Infrastructure
Imports Ptz.Core.Engine
Imports Ptz.Core.Models
Imports Ptz.Core.Vision
Imports Ptz.Presentation

Namespace Controls

    ''' <summary>
    ''' عرض الفيديو مع التراكبات (صناديق التتبع، المسارات، المناطق، الخطوط) وتفاعل الفأرة:
    ''' سحب = تكبير على منطقة، نقر أيمن = قفل هدف للتتبع، وفي وضع الرسم نقرات لتحديد منطقة/خط.
    ''' </summary>
    Public Class VideoView
        Inherits Control
        Implements IVideoView

        Private _image As Bitmap
        Private _decoding As Integer
        Private _result As SmartFrameResult
        Private _zones As IReadOnlyList(Of RestrictedZone) = New List(Of RestrictedZone)()
        Private _lines As IReadOnlyList(Of TripLine) = New List(Of TripLine)()
        Private _masks As IReadOnlyList(Of IReadOnlyList(Of NormPoint)) = New List(Of IReadOnlyList(Of NormPoint))()
        Private _mode As GeometryDrawMode = GeometryDrawMode.None
        Private _drawPoints As New List(Of NormPoint)()
        Private _hover As Point
        Private _dragStart As Point?
        Private _dragNow As Point

        Public Event AreaSelected As Action(Of NormRect) Implements IVideoView.AreaSelected
        Public Event TargetClicked As Action(Of NormPoint) Implements IVideoView.TargetClicked
        Public Event ZoneCompleted As Action(Of IReadOnlyList(Of NormPoint), GeometryDrawMode) Implements IVideoView.ZoneCompleted
        Public Event LineCompleted As Action(Of NormPoint, NormPoint) Implements IVideoView.LineCompleted

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
            BackColor = Color.Black
            TabStop = True
        End Sub

        ' ---------- IVideoView ----------
        Public Sub ShowFrame(jpeg As Byte()) Implements IVideoView.ShowFrame
            ' فك الترميز خارج خيط الواجهة؛ نُسقط الإطار إن كان السابق ما زال قيد الفك.
            If Threading.Interlocked.CompareExchange(_decoding, 1, 0) <> 0 Then Return
            Task.Run(Sub()
                         Dim bmp As Bitmap = Nothing
                         Try
                             Using ms As New IO.MemoryStream(jpeg)
                                 Using src As New Bitmap(ms)
                                     bmp = New Bitmap(src)
                                 End Using
                             End Using
                         Catch ex As ArgumentException
                             bmp = Nothing ' JPEG تالف
                         Finally
                             Threading.Volatile.Write(_decoding, 0)
                         End Try
                         If bmp Is Nothing Then Return
                         If IsDisposed OrElse Not IsHandleCreated Then
                             bmp.Dispose()
                             Return
                         End If
                         Try
                             BeginInvoke(Sub()
                                             Dim old As Bitmap = _image
                                             _image = bmp
                                             Invalidate()
                                             old?.Dispose()
                                         End Sub)
                         Catch ex As InvalidOperationException
                             bmp.Dispose()
                         End Try
                     End Sub)
        End Sub

        Public Sub ShowOverlay(result As SmartFrameResult) Implements IVideoView.ShowOverlay
            _result = result
            Invalidate()
        End Sub

        Public Sub ShowGeometry(zones As IReadOnlyList(Of RestrictedZone), lines As IReadOnlyList(Of TripLine),
                                masks As IReadOnlyList(Of IReadOnlyList(Of NormPoint))) Implements IVideoView.ShowGeometry
            _zones = zones
            _lines = lines
            _masks = masks
            Invalidate()
        End Sub

        Public Sub SetDrawMode(mode As GeometryDrawMode) Implements IVideoView.SetDrawMode
            _mode = mode
            _drawPoints.Clear()
            Cursor = If(mode = GeometryDrawMode.None, Cursors.Default, Cursors.Cross)
            Invalidate()
        End Sub

        ' ---------- الإحداثيات ----------
        Private Function ImageRect() As RectangleF
            If _image Is Nothing Then Return New RectangleF(0, 0, Width, Height)
            Dim scale As Single = Math.Min(Width / CSng(_image.Width), Height / CSng(_image.Height))
            Dim w As Single = _image.Width * scale
            Dim h As Single = _image.Height * scale
            Return New RectangleF((Width - w) / 2.0F, (Height - h) / 2.0F, w, h)
        End Function

        Private Function ToNorm(p As Point) As NormPoint
            Dim r As RectangleF = ImageRect()
            Return New NormPoint(Geometry.Clamp((p.X - r.X) / r.Width, 0, 1), Geometry.Clamp((p.Y - r.Y) / r.Height, 0, 1))
        End Function

        Private Function ToPx(n As NormPoint) As PointF
            Dim r As RectangleF = ImageRect()
            Return New PointF(r.X + CSng(n.X) * r.Width, r.Y + CSng(n.Y) * r.Height)
        End Function

        Private Function ToPx(box As NormRect) As RectangleF
            Dim r As RectangleF = ImageRect()
            Return New RectangleF(r.X + CSng(box.X) * r.Width, r.Y + CSng(box.Y) * r.Height, CSng(box.Width) * r.Width, CSng(box.Height) * r.Height)
        End Function

        ' ---------- الرسم ----------
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(Color.Black)
            If _image Is Nothing Then
                Using b As New SolidBrush(Theme.Muted), f As New Font("Segoe UI", 12.0F)
                    g.DrawString("لا يوجد فيديو", f, b, New RectangleF(0, 0, Width, Height), New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center})
                End Using
                Return
            End If
            g.InterpolationMode = InterpolationMode.HighQualityBilinear
            g.DrawImage(_image, ImageRect())
            DrawGeometry(g)
            DrawTracks(g)
            DrawInProgress(g)
            DrawStatus(g)
        End Sub

        Private Sub DrawGeometry(g As Graphics)
            For Each m As IReadOnlyList(Of NormPoint) In _masks
                FillPoly(g, m, Color.FromArgb(70, 120, 120, 120), Color.Gray, True)
            Next
            For Each z As RestrictedZone In _zones
                FillPoly(g, z.Points, Color.FromArgb(55, Theme.Danger), Theme.Danger, False)
                Using b As New SolidBrush(Theme.Danger)
                    g.DrawString(z.Name, Theme.BaseFont, b, ToPx(z.Points(0)))
                End Using
            Next
            Using p As New Pen(Theme.Warn, 2.5F)
                For Each l As TripLine In _lines
                    g.DrawLine(p, ToPx(l.A), ToPx(l.B))
                Next
            End Using
        End Sub

        Private Sub FillPoly(g As Graphics, pts As IReadOnlyList(Of NormPoint), fill As Color, edge As Color, dashed As Boolean)
            Dim arr As PointF() = pts.Select(Function(p) ToPx(p)).ToArray()
            Using b As New SolidBrush(fill)
                g.FillPolygon(b, arr)
            End Using
            Using pen As New Pen(edge, 2.0F)
                If dashed Then pen.DashStyle = DashStyle.Dash
                g.DrawPolygon(pen, arr)
            End Using
        End Sub

        Private Sub DrawTracks(g As Graphics)
            Dim res As SmartFrameResult = _result
            If res Is Nothing Then Return
            For Each t As TrackView In res.Tracks
                Dim col As Color = If(res.AutoTrackActive, Theme.Danger, If(t.Confirmed, Theme.Ok, Theme.Warn))
                If t.Trail.Count > 1 Then
                    Using tp As New Pen(Color.FromArgb(160, col), 2.0F)
                        g.DrawLines(tp, t.Trail.Select(Function(p) ToPx(p)).ToArray())
                    End Using
                End If
                Dim rc As RectangleF = ToPx(t.Box)
                Using pen As New Pen(col, 2.0F)
                    g.DrawRectangle(pen, rc.X, rc.Y, rc.Width, rc.Height)
                End Using
                If Not res.AutoTrackActive Then
                    Using b As New SolidBrush(col)
                        g.DrawString($"#{t.Id}  {t.Speed:F2}", Theme.BaseFont, b, rc.X, Math.Max(0, rc.Y - 18))
                    End Using
                End If
            Next
        End Sub

        Private Sub DrawInProgress(g As Graphics)
            If _mode <> GeometryDrawMode.None AndAlso _drawPoints.Count > 0 Then
                Dim pts As List(Of PointF) = _drawPoints.Select(Function(p) ToPx(p)).ToList()
                pts.Add(New PointF(_hover.X, _hover.Y))
                Using pen As New Pen(Theme.Accent, 2.0F)
                    pen.DashStyle = DashStyle.Dash
                    If pts.Count > 1 Then g.DrawLines(pen, pts.ToArray())
                End Using
                Using b As New SolidBrush(Theme.Accent)
                    For Each p As PointF In pts.Take(pts.Count - 1)
                        g.FillEllipse(b, p.X - 4, p.Y - 4, 8, 8)
                    Next
                End Using
            End If
            If _dragStart.HasValue Then
                Dim rc As Rectangle = DragRect()
                Using pen As New Pen(Theme.Accent, 1.5F), b As New SolidBrush(Color.FromArgb(40, Theme.Accent))
                    g.FillRectangle(b, rc)
                    g.DrawRectangle(pen, rc)
                End Using
            End If
        End Sub

        Private Sub DrawStatus(g As Graphics)
            Dim res As SmartFrameResult = _result
            If res Is Nothing Then Return
            Dim text As String = Nothing
            If res.AutoTrackActive Then
                text = "● تتبع آلي"
            ElseIf res.Suspended Then
                text = "الكشف معلّق (الكاميرا تتحرك)"
            ElseIf res.WarmingUp Then
                text = "تهيئة الخلفية..."
            ElseIf res.IlluminationChange Then
                text = "تغيّر إضاءة — إعادة تعلّم"
            End If
            If text Is Nothing Then Return
            Using b As New SolidBrush(Color.FromArgb(190, 0, 0, 0)), t As New SolidBrush(If(res.AutoTrackActive, Theme.Danger, Theme.Warn))
                Dim sz As SizeF = g.MeasureString(text, Theme.BaseFont)
                g.FillRectangle(b, 8, 8, sz.Width + 12, sz.Height + 6)
                g.DrawString(text, Theme.BaseFont, t, 14, 11)
            End Using
        End Sub

        ' ---------- الفأرة ----------
        Private Function DragRect() As Rectangle
            Dim s As Point = _dragStart.Value
            Return New Rectangle(Math.Min(s.X, _dragNow.X), Math.Min(s.Y, _dragNow.Y), Math.Abs(s.X - _dragNow.X), Math.Abs(s.Y - _dragNow.Y))
        End Function

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            If _image Is Nothing Then Return
            If e.Button = MouseButtons.Right Then
                If _mode <> GeometryDrawMode.None Then
                    SetDrawMode(GeometryDrawMode.None)
                Else
                    RaiseEvent TargetClicked(ToNorm(e.Location))
                End If
                Return
            End If
            If e.Button <> MouseButtons.Left Then Return
            Select Case _mode
                Case GeometryDrawMode.Zone, GeometryDrawMode.MaskZone
                    _drawPoints.Add(ToNorm(e.Location))
                Case GeometryDrawMode.Line
                    _drawPoints.Add(ToNorm(e.Location))
                    If _drawPoints.Count = 2 Then
                        Dim a As NormPoint = _drawPoints(0)
                        Dim b As NormPoint = _drawPoints(1)
                        SetDrawMode(GeometryDrawMode.None)
                        RaiseEvent LineCompleted(a, b)
                    End If
                Case Else
                    _dragStart = e.Location
                    _dragNow = e.Location
                    Capture = True
            End Select
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            _hover = e.Location
            If _dragStart.HasValue Then _dragNow = e.Location
            If _dragStart.HasValue OrElse _drawPoints.Count > 0 Then Invalidate()
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            If Not _dragStart.HasValue Then Return
            _dragNow = e.Location
            Dim rc As Rectangle = DragRect()
            _dragStart = Nothing
            Capture = False
            Invalidate()
            If rc.Width > 8 AndAlso rc.Height > 8 Then
                Dim a As NormPoint = ToNorm(New Point(rc.Left, rc.Top))
                Dim b As NormPoint = ToNorm(New Point(rc.Right, rc.Bottom))
                RaiseEvent AreaSelected(New NormRect(a.X, a.Y, b.X - a.X, b.Y - a.Y))
            Else
                Dim c As NormPoint = ToNorm(e.Location)
                RaiseEvent AreaSelected(New NormRect(c.X, c.Y, 0, 0)) ' نقرة = توسيط على النقطة
            End If
        End Sub

        Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
            MyBase.OnMouseDoubleClick(e)
            If (_mode = GeometryDrawMode.Zone OrElse _mode = GeometryDrawMode.MaskZone) AndAlso e.Button = MouseButtons.Left Then
                ' النقرة المزدوجة سجّلت نقطتين متطابقتين تقريباً: نحذف المكرر.
                If _drawPoints.Count >= 2 AndAlso _drawPoints(_drawPoints.Count - 1).DistanceTo(_drawPoints(_drawPoints.Count - 2)) < 0.01 Then
                    _drawPoints.RemoveAt(_drawPoints.Count - 1)
                End If
                Dim pts As List(Of NormPoint) = _drawPoints.ToList()
                Dim mode As GeometryDrawMode = _mode
                SetDrawMode(GeometryDrawMode.None)
                RaiseEvent ZoneCompleted(pts, mode)
            End If
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            If keyData = Keys.Escape Then Return True
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Escape Then SetDrawMode(GeometryDrawMode.None)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _image?.Dispose()
                _image = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class

End Namespace
