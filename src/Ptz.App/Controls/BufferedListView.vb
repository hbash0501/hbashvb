Imports System.Windows.Forms

Namespace Controls

    ''' <summary>ListView مزدوج التخزين المؤقت (بلا وميض عند التحديث السريع).</summary>
    Public Class BufferedListView
        Inherits ListView
        Public Sub New()
            DoubleBuffered = True
            View = System.Windows.Forms.View.Details
            FullRowSelect = True
            HideSelection = False
        End Sub
    End Class

End Namespace
