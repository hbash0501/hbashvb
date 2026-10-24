Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions

Public Class ReferenceMatch
    Public Property Reference As CorrespondenceReference
    Public Property Documents As New List(Of DocumentRecord)
End Class

' فهرس أرقام المستندات (الأرشيف، المرجع، الرقم الخارجي، الصادر والوارد) لربط كل إشارة
' بالمستند المقصود في الأرشيف إن وُجد. لا تُستخدم الأرقام المذكورة داخل النص لأنها قد تكون هي نفسها إشارات.
Public NotInheritable Class CorrespondenceIndex
    Private ReadOnly _exact As New Dictionary(Of String, List(Of DocumentRecord))(StringComparer.Ordinal)
    Private ReadOnly _digits As New Dictionary(Of String, List(Of DocumentRecord))(StringComparer.Ordinal)

    Public Sub New(documents As IEnumerable(Of DocumentRecord))
        For Each d In If(documents, Enumerable.Empty(Of DocumentRecord)())
            For Each raw In NumbersOf(d)
                Dim key As String = CorrespondenceReferenceParser.NumberKey(raw)
                If key = "" Then Continue For
                Add(_exact, key, d)
                Dim digits As String = DigitsKey(key)
                If digits.Length >= 4 Then Add(_digits, digits, d)
            Next
        Next
    End Sub

    Private Shared Sub Add(map As Dictionary(Of String, List(Of DocumentRecord)), key As String, d As DocumentRecord)
        Dim list As List(Of DocumentRecord) = Nothing
        If Not map.TryGetValue(key, list) Then
            list = New List(Of DocumentRecord)
            map(key) = list
        End If
        If Not list.Contains(d) Then list.Add(d)
    End Sub

    Private Shared Function DigitsKey(key As String) As String
        Return New String(key.Where(Function(c) Char.IsLetterOrDigit(c)).ToArray())
    End Function

    Public Shared Function NumbersOf(d As DocumentRecord) As IEnumerable(Of String)
        Dim values As New List(Of String) From {d.ArchiveNumber, d.ReferenceNumber, d.ExternalNumber}
        For Each multi In New String() {d.OcrOutgoingNumbers, d.OcrIncomingNumbers}
            If Not String.IsNullOrWhiteSpace(multi) Then values.AddRange(Regex.Split(multi, "[،,;|\r\n]+"))
        Next
        Return values.Where(Function(v) Not String.IsNullOrWhiteSpace(v) AndAlso v.Any(Function(c) Char.IsDigit(c))).Select(Function(v) v.Trim())
    End Function

    ' المستندات التي يطابق أحد أرقامها رقم الإشارة؛ المطابقة التامة أولاً ثم المتسامحة.
    Public Function Find(reference As CorrespondenceReference, Optional excludeId As String = "") As List(Of DocumentRecord)
        If reference Is Nothing OrElse reference.Number = "" Then Return New List(Of DocumentRecord)
        Dim found As List(Of DocumentRecord) = Nothing
        If Not _exact.TryGetValue(reference.Number, found) Then
            Dim digits As String = DigitsKey(reference.Number)
            If digits.Length < 4 OrElse Not _digits.TryGetValue(digits, found) Then Return New List(Of DocumentRecord)
        End If
        Return found.Where(Function(d) d.Id <> excludeId).Take(5).ToList()
    End Function

    Public Function Resolve(text As String, Optional excludeId As String = "") As List(Of ReferenceMatch)
        Return CorrespondenceReferenceParser.Parse(text).Select(Function(r) New ReferenceMatch With {.Reference = r, .Documents = Find(r, excludeId)}).ToList()
    End Function
End Class
