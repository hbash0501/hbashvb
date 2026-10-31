Namespace Vision

    ''' <summary>إطار بتدرج رمادي 8-بت (صف تلو صف). غير قابل للتعديل بعد الإنشاء.</summary>
    Public NotInheritable Class GrayFrame
        Public ReadOnly Property Width As Integer
        Public ReadOnly Property Height As Integer
        Public ReadOnly Property Pixels As Byte()
        Public ReadOnly Property TimestampUtc As DateTime

        Public Sub New(width As Integer, height As Integer, pixels As Byte(), timestampUtc As DateTime)
            If width <= 0 OrElse height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(width))
            If pixels Is Nothing OrElse pixels.Length <> width * height Then
                Throw New ArgumentException("حجم بيانات الإطار لا يطابق الأبعاد.", NameOf(pixels))
            End If
            Me.Width = width
            Me.Height = height
            Me.Pixels = pixels
            Me.TimestampUtc = timestampUtc
        End Sub

        Public Function GetPixel(x As Integer, y As Integer) As Byte
            Return Pixels(y * Width + x)
        End Function

        ''' <summary>تصغير بمتوسط الصندوق بمعامل صحيح (≥1).</summary>
        Public Function Downscale(factor As Integer) As GrayFrame
            If factor < 1 Then Throw New ArgumentOutOfRangeException(NameOf(factor))
            If factor = 1 Then Return Me
            Dim nw As Integer = Width \ factor
            Dim nh As Integer = Height \ factor
            If nw < 1 OrElse nh < 1 Then Throw New ArgumentOutOfRangeException(NameOf(factor), "معامل التصغير أكبر من الإطار.")
            Dim dst(nw * nh - 1) As Byte
            Dim area As Integer = factor * factor
            For y As Integer = 0 To nh - 1
                For x As Integer = 0 To nw - 1
                    Dim sum As Integer = 0
                    For dy As Integer = 0 To factor - 1
                        Dim rowStart As Integer = (y * factor + dy) * Width + x * factor
                        For dx As Integer = 0 To factor - 1
                            sum += Pixels(rowStart + dx)
                        Next
                    Next
                    dst(y * nw + x) = CByte(sum \ area)
                Next
            Next
            Return New GrayFrame(nw, nh, dst, TimestampUtc)
        End Function

        ''' <summary>تصغير بحيث لا يتجاوز العرض <paramref name="maxWidth"/> (بمعامل صحيح).</summary>
        Public Function DownscaleToWidth(maxWidth As Integer) As GrayFrame
            If maxWidth < 1 Then Throw New ArgumentOutOfRangeException(NameOf(maxWidth))
            Dim f As Integer = Math.Max(1, Width \ maxWidth)
            Return Downscale(f)
        End Function

        ''' <summary>تحويل مخزن BGRA32 (بترتيب System.Drawing) إلى تدرج رمادي بمعادلة اللمعان القياسية.</summary>
        Public Shared Function FromBgra32(width As Integer, height As Integer, stride As Integer, buffer As Byte(), timestampUtc As DateTime) As GrayFrame
            If buffer Is Nothing Then Throw New ArgumentNullException(NameOf(buffer))
            If stride < width * 4 OrElse buffer.Length < stride * height Then Throw New ArgumentException("مخزن الصورة أصغر من المتوقع.")
            Dim dst(width * height - 1) As Byte
            For y As Integer = 0 To height - 1
                Dim s As Integer = y * stride
                Dim d As Integer = y * width
                For x As Integer = 0 To width - 1
                    Dim b As Integer = buffer(s)
                    Dim g As Integer = buffer(s + 1)
                    Dim r As Integer = buffer(s + 2)
                    dst(d + x) = CByte((77 * r + 150 * g + 29 * b) >> 8)
                    s += 4
                Next
            Next
            Return New GrayFrame(width, height, dst, timestampUtc)
        End Function
    End Class

End Namespace
