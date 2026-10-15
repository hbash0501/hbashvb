Public Class LayoutWordConversionOptions
    Public Property Dpi As Integer = 300
    Public Property Language As String = "ara+eng"
    Public Property Mode As String = "Hybrid" ' Hybrid, VisualOnly, EditableOnly
    Public Property AddEditableTextLayer As Boolean = True
    Public Property KeepPageHeaders As Boolean = True
    Public Property MaxPages As Integer = 200
    Public Property FirstPage As Integer = 1
    Public Property LastPage As Integer = 0
    Public Property ForceOcr As Boolean = True
    Public Property HighlightDetectedEntities As Boolean = True
End Class
