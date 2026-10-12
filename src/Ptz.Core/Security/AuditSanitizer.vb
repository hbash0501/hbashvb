Imports System.Text
Imports System.Text.RegularExpressions

Namespace Security

    ''' <summary>
    ''' ينظّف نصاً قبل كتابته في سجل التدقيق أو الاستجابات: يحذف محارف التحكم (C0/C1) وفواصل الأسطر Unicode
    ''' وموجّهات الاتجاه الثنائي (تزوير العرض) وتسلسلات ANSI، ثم يقصّ الطول.
    ''' </summary>
    Public NotInheritable Class AuditSanitizer
        Private Sub New()
        End Sub

        Private Shared ReadOnly Ansi As New Regex("\u001B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled)

        Public Shared Function Clean(text As String, maxLength As Integer) As String
            If String.IsNullOrEmpty(text) Then Return ""
            Dim s As String = Ansi.Replace(text, "")
            Dim sb As New StringBuilder(Math.Min(s.Length, maxLength))
            For Each c As Char In s
                If sb.Length >= maxLength Then Exit For
                If Char.IsControl(c) Then Continue For            ' C0 و C1 (يشمل \r \n \t ESC)
                Select Case AscW(c)
                    Case &H2028, &H2029, &H200E, &H200F, &H202A To &H202E, &H2066 To &H2069, &HFEFF
                        Continue For
                End Select
                sb.Append(c)
            Next
            Return sb.ToString()
        End Function
    End Class

End Namespace
