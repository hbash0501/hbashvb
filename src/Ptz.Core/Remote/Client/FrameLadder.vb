Namespace Remote

    ''' <summary>سلّم جودة الإطارات المشترك بين البوابة والعميل (مصدر واحد للحقيقة).</summary>
    Public NotInheritable Class FrameLadderEntry
        Public ReadOnly Property Id As String
        Public ReadOnly Property MaxWidth As Integer
        Public ReadOnly Property Quality As Integer
        Public ReadOnly Property MaxFps As Double
        ''' <summary>حجم JPEG التقريبي لإطار 16:9 (بايت) يُستعمل قبل أن يتعلم المقدِّر الحجم الفعلي.</summary>
        Public ReadOnly Property TypicalBytes As Integer

        Public Sub New(id As String, maxWidth As Integer, quality As Integer, maxFps As Double, typicalBytes As Integer)
            Me.Id = id
            Me.MaxWidth = maxWidth
            Me.Quality = quality
            Me.MaxFps = maxFps
            Me.TypicalBytes = typicalBytes
        End Sub
    End Class

    Public NotInheritable Class FrameLadder
        Private Sub New()
        End Sub

        Public Shared ReadOnly Entries As IReadOnlyList(Of FrameLadderEntry) = New List(Of FrameLadderEntry) From {
            New FrameLadderEntry("tiny", 320, 40, 2, 9000),
            New FrameLadderEntry("low", 480, 50, 3, 20000),
            New FrameLadderEntry("mid", 640, 60, 4, 38000),
            New FrameLadderEntry("high", 960, 65, 5, 85000),
            New FrameLadderEntry("hd", 1600, 75, 5, 210000)}

        Public Shared Function IndexOf(id As String) As Integer
            For i As Integer = 0 To Entries.Count - 1
                If Entries(i).Id = id Then Return i
            Next
            Return -1
        End Function
    End Class

End Namespace
