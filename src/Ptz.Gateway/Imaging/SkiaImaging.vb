Imports Ptz.Core.Abstractions
Imports Ptz.Core.Vision
Imports SkiaSharp

Namespace Imaging

    ''' <summary>فك JPEG إلى تدرج رمادي بتصغير أثناء الفك (SkiaSharp، متعدد المنصات).</summary>
    Public NotInheritable Class SkiaFrameDecoder
        Implements IFrameDecoder

        Private ReadOnly _clock As IClock
        Private ReadOnly _targetWidth As Integer

        Public Sub New(Optional clock As IClock = Nothing, Optional targetWidth As Integer = 320)
            _clock = If(clock, SystemClock.Instance)
            _targetWidth = targetWidth
        End Sub

        Public Function DecodeToGray(jpeg As Byte()) As GrayFrame Implements IFrameDecoder.DecodeToGray
            Using codec As SKCodec = SKCodec.Create(New SKMemoryStream(jpeg))
                If codec Is Nothing Then Throw New ArgumentException("JPEG غير صالح.")
                ' تصغير الفك بنسبة 1/2 أو 1/4 أو 1/8 فقط (يدعمها JPEG مباشرة)
                Dim scale As Single = 1.0F
                For Each cand As Single In {0.125F, 0.25F, 0.5F}
                    If codec.Info.Width * cand >= _targetWidth Then
                        scale = cand
                        Exit For
                    End If
                Next
                Dim dims As SKSizeI = codec.GetScaledDimensions(scale)
                Dim info As New SKImageInfo(dims.Width, dims.Height, SKColorType.Bgra8888, SKAlphaType.Opaque)
                Using bmp As New SKBitmap(info)
                    Dim res As SKCodecResult = codec.GetPixels(info, bmp.GetPixels())
                    If res <> SKCodecResult.Success AndAlso res <> SKCodecResult.IncompleteInput Then Throw New ArgumentException("تعذّر فك JPEG.")
                    Dim raw As Byte() = bmp.Bytes
                    Return GrayFrame.FromBgra32(bmp.Width, bmp.Height, bmp.RowBytes, raw, _clock.UtcNow)
                End Using
            End Using
        End Function
    End Class

    ''' <summary>تصغير وإعادة ترميز JPEG لطبقات الجودة (لا تكبير أبداً؛ إن كان العرض أصغر يعاد الأصل).</summary>
    Public NotInheritable Class SkiaJpegTranscoder
        Implements IJpegTranscoder

        Public Function Resize(jpeg As Byte(), maxWidth As Integer, quality As Integer) As Byte() Implements IJpegTranscoder.Resize
            Using src As SKBitmap = SKBitmap.Decode(jpeg)
                If src Is Nothing Then Throw New ArgumentException("JPEG غير صالح.")
                If src.Width <= maxWidth Then Return jpeg
                Dim nh As Integer = Math.Max(1, CInt(Math.Round(src.Height * (maxWidth / CDbl(src.Width)))))
                Using dst As SKBitmap = src.Resize(New SKImageInfo(maxWidth, nh), SKFilterQuality.Medium)
                    Using img As SKImage = SKImage.FromBitmap(dst)
                        Using data As SKData = img.Encode(SKEncodedImageFormat.Jpeg, Math.Max(1, Math.Min(100, quality)))
                            Return data.ToArray()
                        End Using
                    End Using
                End Using
            End Using
        End Function
    End Class

End Namespace
