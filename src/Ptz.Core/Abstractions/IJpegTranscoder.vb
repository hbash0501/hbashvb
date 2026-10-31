Namespace Abstractions

    ''' <summary>يصغّر صورة JPEG ويعيد ترميزها (لطبقات الجودة في البث البعيد). التنفيذ متعدد المنصات في البوابة.</summary>
    Public Interface IJpegTranscoder
        ''' <summary>يعيد JPEG بعرض لا يتجاوز <paramref name="maxWidth"/> وجودة 1..100. لا يكبّر الصورة أبداً.</summary>
        Function Resize(jpeg As Byte(), maxWidth As Integer, quality As Integer) As Byte()
    End Interface

End Namespace
