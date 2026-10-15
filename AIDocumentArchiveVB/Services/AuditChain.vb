Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Security.Cryptography
Imports System.Text

' سلسلة تدقيق مقاومة للتلاعب: كل سطر يحمل تجزئة SHA-256 تشمل تجزئة السطر السابق،
' فأي تعديل أو حذف أو إعادة ترتيب لسطر قديم يكسر السلسلة ويُكتشف عند التحقق.
Public Class AuditChainResult
    Public Property IsValid As Boolean
    Public Property Count As Integer
    Public Property FirstBrokenLine As Integer
    Public Property Message As String
    Public Property LastHash As String
End Class

Public NotInheritable Class AuditChain
    Public Const GenesisHash As String = "0000000000000000000000000000000000000000000000000000000000000000"

    Private Sub New()
    End Sub

    Private Shared Function Esc(value As String) As String
        Return If(value, "").Replace("\", "\\").Replace(vbTab, "\t").Replace(vbCr, "\r").Replace(vbLf, "\n")
    End Function

    Private Shared Function Unesc(value As String) As String
        Dim sb As New StringBuilder()
        Dim i As Integer = 0
        While i < value.Length
            Dim c As Char = value(i)
            If c = "\"c AndAlso i + 1 < value.Length Then
                i += 1
                Select Case value(i)
                    Case "t"c : sb.Append(vbTab)
                    Case "r"c : sb.Append(vbCr)
                    Case "n"c : sb.Append(vbLf)
                    Case Else : sb.Append(value(i))
                End Select
            Else
                sb.Append(c)
            End If
            i += 1
        End While
        Return sb.ToString()
    End Function

    Public Shared Function ComputeHash(sequence As Long, previousHash As String, timestampUtc As String, actor As String, action As String, detail As String) As String
        Dim payload As String = sequence.ToString(CultureInfo.InvariantCulture) & vbLf & previousHash & vbLf & timestampUtc & vbLf & Esc(actor) & vbLf & Esc(action) & vbLf & Esc(detail)
        Using sha As SHA256 = SHA256.Create()
            Dim bytes As Byte() = sha.ComputeHash(Encoding.UTF8.GetBytes(payload))
            Dim sb As New StringBuilder(64)
            For Each b In bytes
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture))
            Next
            Return sb.ToString()
        End Using
    End Function

    ' يبني سطراً جديداً يلي السطر السابق ذي التجزئة previousHash.
    Public Shared Function BuildLine(sequence As Long, previousHash As String, timestampUtc As DateTime, actor As String, action As String, detail As String) As String
        Dim ts As String = timestampUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
        Dim hash As String = ComputeHash(sequence, previousHash, ts, actor, action, detail)
        Return String.Join(vbTab, New String() {sequence.ToString(CultureInfo.InvariantCulture), ts, Esc(actor), Esc(action), Esc(detail), previousHash, hash})
    End Function

    Public Shared Function Verify(lines As IEnumerable(Of String)) As AuditChainResult
        Dim result As New AuditChainResult With {.IsValid = True, .LastHash = GenesisHash, .Message = "السلسلة سليمة."}
        Dim previous As String = GenesisHash
        Dim expected As Long = 1
        Dim lineNo As Integer = 0
        For Each line In If(lines, New List(Of String)())
            If String.IsNullOrWhiteSpace(line) Then Continue For
            lineNo += 1
            Dim parts As String() = line.Split(vbTab(0))
            Dim seq As Long
            If parts.Length <> 7 OrElse Not Long.TryParse(parts(0), NumberStyles.None, CultureInfo.InvariantCulture, seq) Then
                Return Broken(result, lineNo, "سطر مشوّه.")
            End If
            If seq <> expected Then Return Broken(result, lineNo, "تسلسل غير متوقع (حذف أو إدراج سطر).")
            If parts(5) <> previous Then Return Broken(result, lineNo, "الارتباط بالسطر السابق غير مطابق.")
            Dim recomputed As String = ComputeHash(seq, parts(5), parts(1), Unesc(parts(2)), Unesc(parts(3)), Unesc(parts(4)))
            If Not String.Equals(recomputed, parts(6), StringComparison.Ordinal) Then Return Broken(result, lineNo, "محتوى السطر لا يطابق تجزئته (تعديل).")
            previous = parts(6)
            expected += 1
            result.Count += 1
        Next
        result.LastHash = previous
        Return result
    End Function

    Private Shared Function Broken(result As AuditChainResult, lineNo As Integer, message As String) As AuditChainResult
        result.IsValid = False
        result.FirstBrokenLine = lineNo
        result.Message = "انكسرت السلسلة عند السطر " & lineNo.ToString(CultureInfo.InvariantCulture) & ": " & message
        Return result
    End Function
End Class
