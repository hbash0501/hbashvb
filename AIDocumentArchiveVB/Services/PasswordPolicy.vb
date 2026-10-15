Option Strict On
Imports System
Imports System.Linq

' سياسة كلمات المرور المعتمدة لحسابات المنصة.
Public NotInheritable Class PasswordPolicy
    Public Const MinimumLength As Integer = 10

    Private Shared ReadOnly Common As String() = {"admin123", "password", "password1", "1234567890", "0123456789", "qwertyuiop", "12345678910", "abcdefghij"}

    Private Sub New()
    End Sub

    ' يعيد نص سبب الرفض، أو سلسلة فارغة إذا كانت كلمة المرور مقبولة.
    Public Shared Function Validate(password As String, Optional userName As String = "") As String
        Dim value As String = If(password, "")
        If value.Length < MinimumLength Then Return "كلمة المرور يجب ألا تقل عن " & MinimumLength.ToString() & " أحرف."
        If value.Distinct().Count() < 4 Then Return "كلمة المرور متكررة الأحرف؛ استخدم أحرفاً متنوعة."
        If Common.Contains(value.ToLowerInvariant()) Then Return "كلمة المرور شائعة ويسهل تخمينها."
        If Not String.IsNullOrWhiteSpace(userName) AndAlso value.ToLowerInvariant().Contains(userName.Trim().ToLowerInvariant()) Then Return "لا تضمّن اسم المستخدم داخل كلمة المرور."
        Return ""
    End Function
End Class
