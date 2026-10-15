Imports System
Imports System.Windows.Forms

Public Class EmployeeGuideForm
    Public Sub New()
        InitializeComponent()
        txtGuide.Text = BuildGuideText()
    End Sub

    Private Shared Function BuildGuideText() As String
        Return "دليل تشغيل النسخة النهائية للموظفين" & Environment.NewLine &
               "======================================" & Environment.NewLine & Environment.NewLine &
               "المسار اليومي المختصر:" & Environment.NewLine &
               "1) من الشاشة الرئيسية اختر (إدراج مستند جديد) أو (إدراج سريع)." & Environment.NewLine &
               "2) يتم تسجيل المستند فوراً ولا تنتظر OCR أو الذكاء السحابي." & Environment.NewLine &
               "3) افتح (مركز المعالجة والأداء) لمتابعة OCR والذكاء الاصطناعي في الخلفية." & Environment.NewLine &
               "4) افتح (مركز المستند الموحد) للمراجعة: النص، الكيانات، الفهم، الردود، التصدير." & Environment.NewLine &
               "5) عند الخطابات التي تحتاج رداً: اختر نبرة الرد ثم صدّر إلى Word." & Environment.NewLine & Environment.NewLine &
               "متى أستخدم كل زر؟" & Environment.NewLine &
               "- صندوق العمل: يعرض الأولويات اليومية." & Environment.NewLine &
               "- مركز المستند: المكان الوحيد لمراجعة مستند محدد." & Environment.NewLine &
               "- مركز المعالجة: متابعة المهام البطيئة دون ظن أن البرنامج متوقف." & Environment.NewLine &
               "- اسأل الأرشيف: البحث المعرفي والعلاقات بين المستندات." & Environment.NewLine &
               "- تحويل PDF/صورة إلى Word: عند الحاجة إلى إخراج Word قريب من شكل الأصل." & Environment.NewLine & Environment.NewLine &
               "ملاحظات مهمة:" & Environment.NewLine &
               "- OpenAI اختياري، وإذا تعطل OpenAI ستعمل المنصة بالتحليل المحلي السريع." & Environment.NewLine &
               "- اسم النموذج السحابي يجب أن يطابق نتيجة الأمر: قائمة النماذج المتاحة في حساب OpenAI" & Environment.NewLine &
               "- PDF النصي يعالج بسرعة أكبر من PDF المصور." & Environment.NewLine &
               "- لا تكرر نفس العملية إذا كانت مكتملة؛ النتائج محفوظة في الكاش." & Environment.NewLine & Environment.NewLine &
               "حساب التجربة الافتراضي:" & Environment.NewLine &
               "اسم المستخدم: admin" & Environment.NewLine &
               "كلمة المرور: admin123"
    End Function

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub
End Class
