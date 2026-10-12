Imports System.Buffers.Binary
Imports System.Security.Cryptography
Imports System.Text

Namespace Security

    ''' <summary>
    ''' كلمة مرور لمرة واحدة زمنية (RFC 6238، HMAC-SHA1، 6 أرقام، 30 ث). التحقق بزمن ثابت على كل الخطوات
    ''' المرشحة وبحماية من إعادة الاستخدام (الخطوة المقبولة يجب أن تكون أكبر من آخر خطوة مستهلكة).
    ''' </summary>
    Public NotInheritable Class Totp
        Private Sub New()
        End Sub

        Public Const StepSeconds As Integer = 30

        Public Shared Function NewSecret() As Byte()
            Return RandomNumberGenerator.GetBytes(20)
        End Function

        Public Shared Function StepOf(unixSeconds As Long) As Long
            Return unixSeconds \ StepSeconds
        End Function

        Public Shared Function Compute(secret As Byte(), stepNo As Long, Optional digits As Integer = 6) As String
            Dim msg(7) As Byte
            BinaryPrimitives.WriteInt64BigEndian(msg, stepNo)
            Dim h As Byte() = HMACSHA1.HashData(secret, msg)
            Dim off As Integer = h(h.Length - 1) And &HF
            Dim bin As Integer = ((h(off) And &H7F) << 24) Or (CInt(h(off + 1)) << 16) Or (CInt(h(off + 2)) << 8) Or CInt(h(off + 3))
            Dim modulus As Long = CLng(Math.Pow(10, digits))
            Return (bin Mod modulus).ToString(New String("0"c, digits), Globalization.CultureInfo.InvariantCulture)
        End Function

        ''' <summary>
        ''' يعيد الخطوة المطابقة (لتسجيلها ضد الإعادة) أو -1. الرمز يجب أن يكون 6 أرقام ASCII بالضبط.
        ''' كل الخطوات الثلاث (t-1..t+1) تُحسب وتُقارن دائماً بلا خروج مبكر.
        ''' </summary>
        Public Shared Function Verify(secret As Byte(), code As String, unixSeconds As Long, lastAcceptedStep As Long) As Long
            If code Is Nothing OrElse code.Length <> 6 Then Return -1
            For Each c As Char In code
                If c < "0"c OrElse c > "9"c Then Return -1
            Next
            Dim cur As Long = StepOf(unixSeconds)
            Dim matched As Long = -1
            Dim given As Byte() = Encoding.ASCII.GetBytes(code)
            For s As Long = cur - 1 To cur + 1
                Dim expected As Byte() = Encoding.ASCII.GetBytes(Compute(secret, s))
                Dim eq As Boolean = CryptographicOperations.FixedTimeEquals(given, expected)
                If eq AndAlso s > lastAcceptedStep Then matched = s
            Next
            Return matched
        End Function

        Public Shared Function Base32(data As Byte()) As String
            Const alphabet As String = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
            Dim sb As New StringBuilder()
            Dim buffer As Integer = 0
            Dim bits As Integer = 0
            For Each b As Byte In data
                buffer = (buffer << 8) Or b
                bits += 8
                While bits >= 5
                    sb.Append(alphabet((buffer >> (bits - 5)) And 31))
                    bits -= 5
                End While
                buffer = buffer And ((1 << bits) - 1)
            Next
            If bits > 0 Then sb.Append(alphabet((buffer << (5 - bits)) And 31))
            Return sb.ToString()
        End Function

        Public Shared Function FromBase32(text As String) As Byte()
            Const alphabet As String = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
            Dim bytes As New List(Of Byte)()
            Dim buffer As Integer = 0
            Dim bits As Integer = 0
            For Each c As Char In text.Trim().TrimEnd("="c).ToUpperInvariant()
                Dim v As Integer = alphabet.IndexOf(c)
                If v < 0 Then Throw New FormatException("Base32 غير صالح.")
                buffer = (buffer << 5) Or v
                bits += 5
                If bits >= 8 Then
                    bytes.Add(CByte((buffer >> (bits - 8)) And &HFF))
                    bits -= 8
                    buffer = buffer And ((1 << bits) - 1)
                End If
            Next
            Return bytes.ToArray()
        End Function

        Public Shared Function ProvisioningUri(issuer As String, user As String, secret As Byte()) As String
            Return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user)}?secret={Base32(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30"
        End Function
    End Class

End Namespace
