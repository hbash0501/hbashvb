Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports Ptz.Core.Abstractions
Imports Ptz.Core.Vision

Namespace Infrastructure

    ''' <summary>فك ترميز JPEG إلى تدرج رمادي بـ GDI+ (Windows).</summary>
    Public NotInheritable Class GdiFrameDecoder
        Implements IFrameDecoder

        Public Function DecodeToGray(jpeg As Byte()) As GrayFrame Implements IFrameDecoder.DecodeToGray
            Using ms As New IO.MemoryStream(jpeg)
                Using bmp As New Bitmap(ms)
                    Dim w As Integer = bmp.Width
                    Dim h As Integer = bmp.Height
                    ' نصغّر أثناء الفك لتقليل الذاكرة: التحليل يعمل على عرض ~160-320 فقط.
                    Dim target As Integer = Math.Max(1, w \ 4)
                    Using small As New Bitmap(w \ 4, h \ 4, PixelFormat.Format32bppArgb)
                        Using g As Graphics = Graphics.FromImage(small)
                            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBilinear
                            g.DrawImage(bmp, 0, 0, small.Width, small.Height)
                        End Using
                        Dim rect As New Rectangle(0, 0, small.Width, small.Height)
                        Dim data As BitmapData = small.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                        Try
                            Dim buffer(data.Stride * small.Height - 1) As Byte
                            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length)
                            Return GrayFrame.FromBgra32(small.Width, small.Height, data.Stride, buffer, DateTime.UtcNow)
                        Finally
                            small.UnlockBits(data)
                        End Try
                    End Using
                End Using
            End Using
        End Function
    End Class

End Namespace
