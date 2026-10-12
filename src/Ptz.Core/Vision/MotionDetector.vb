Imports Ptz.Core.Models

Namespace Vision

    Public NotInheritable Class MotionDetectorOptions
        ''' <summary>عرض المعالجة الداخلي بالبكسل (الإطار يُصغَّر إليه).</summary>
        Public Property ProcessWidth As Integer = 160
        ''' <summary>أصغر مساحة للكتلة كنسبة من الإطار.</summary>
        Public Property MinAreaFraction As Double = 0.0008
        ''' <summary>أقصى مساحة (نسبة) لتجاهل الكتل الضخمة الناتجة عن تغيّر المشهد.</summary>
        Public Property MaxAreaFraction As Double = 0.6
        ''' <summary>دمج الكتل المتجاورة إن كانت الفجوة أقل من هذه القيمة (مُطبَّعة).</summary>
        Public Property MergeGap As Double = 0.03
        Public Property Subtractor As New BackgroundSubtractorOptions()
    End Class

    Public NotInheritable Class Detection
        Public ReadOnly Property Box As NormRect
        Public ReadOnly Property AreaPixels As Integer

        Public Sub New(box As NormRect, areaPixels As Integer)
            Me.Box = box
            Me.AreaPixels = areaPixels
        End Sub
    End Class

    Public NotInheritable Class MotionResult
        Public ReadOnly Property Detections As IReadOnlyList(Of Detection)
        Public ReadOnly Property ForegroundRatio As Double
        Public ReadOnly Property IlluminationChange As Boolean
        Public ReadOnly Property Suspended As Boolean
        Public ReadOnly Property WarmingUp As Boolean

        Public Sub New(detections As IReadOnlyList(Of Detection), ratio As Double, illumination As Boolean, suspended As Boolean, warming As Boolean)
            Me.Detections = detections
            ForegroundRatio = ratio
            IlluminationChange = illumination
            Me.Suspended = suspended
            WarmingUp = warming
        End Sub

        Public Shared ReadOnly Property Empty As New MotionResult(New List(Of Detection)(), 0.0, False, False, False)
    End Class

    ''' <summary>
    ''' كاشف الحركة: فصل خلفية → فلترة مورفولوجية → كتل متصلة → دمج → ترشيح بالمساحة والمناطق المستثناة.
    ''' يُعلَّق أثناء حركة الكاميرا (Suspend) لتفادي الإنذارات الكاذبة.
    ''' </summary>
    Public NotInheritable Class MotionDetector
        Private ReadOnly _opt As MotionDetectorOptions
        Private ReadOnly _sub As BackgroundSubtractor
        Private ReadOnly _exclusions As New List(Of IReadOnlyList(Of NormPoint))()
        Private _exclusionMask As Boolean()
        Private _exclusionW As Integer
        Private _exclusionH As Integer
        Private _suspendedUntil As DateTime = DateTime.MinValue

        Public Sub New(Optional options As MotionDetectorOptions = Nothing)
            _opt = If(options, New MotionDetectorOptions())
            _sub = New BackgroundSubtractor(_opt.Subtractor)
        End Sub

        ''' <summary>مناطق تُهمَل فيها الحركة (خصوصية/أشجار/شاشات).</summary>
        Public Sub SetExclusionZones(zones As IEnumerable(Of IReadOnlyList(Of NormPoint)))
            _exclusions.Clear()
            If zones IsNot Nothing Then _exclusions.AddRange(zones)
            _exclusionMask = Nothing
        End Sub

        ''' <summary>تعليق الكشف حتى الوقت المحدد وإعادة تعلّم الخلفية بعده.</summary>
        Public Sub SuspendUntil(untilUtc As DateTime)
            If untilUtc > _suspendedUntil Then _suspendedUntil = untilUtc
            _sub.Reset()
        End Sub

        ''' <summary>يحدّد وقت الاستئناف مباشرةً (حتى لو كان التعليق السابق أبعد) ويعيد تعلّم الخلفية.</summary>
        Public Sub ResumeAt(untilUtc As DateTime)
            _suspendedUntil = untilUtc
            _sub.Reset()
        End Sub

        Public Sub ResetBackground()
            _sub.Reset()
        End Sub

        Public Function Process(frame As GrayFrame) As MotionResult
            If frame Is Nothing Then Throw New ArgumentNullException(NameOf(frame))
            If frame.TimestampUtc < _suspendedUntil Then
                Return New MotionResult(New List(Of Detection)(), 0.0, False, True, False)
            End If

            Dim small As GrayFrame = frame.DownscaleToWidth(_opt.ProcessWidth)
            Dim sr As SubtractionResult = _sub.Process(small)
            If sr.WarmingUp OrElse sr.IlluminationChange Then
                Return New MotionResult(New List(Of Detection)(), sr.ForegroundRatio, sr.IlluminationChange, False, sr.WarmingUp)
            End If

            Dim w As Integer = small.Width
            Dim h As Integer = small.Height
            Dim cleaned As Byte() = MaskOps.Open3x3(sr.Mask, w, h)
            ApplyExclusions(cleaned, w, h)
            Dim blobs As List(Of Detection) = MaskOps.ExtractBlobs(cleaned, w, h, _opt.MinAreaFraction, _opt.MaxAreaFraction)
            blobs = MaskOps.MergeNearby(blobs, _opt.MergeGap)
            Return New MotionResult(blobs, sr.ForegroundRatio, False, False, False)
        End Function

        Private Sub ApplyExclusions(mask As Byte(), w As Integer, h As Integer)
            If _exclusions.Count = 0 Then Return
            If _exclusionMask Is Nothing OrElse _exclusionW <> w OrElse _exclusionH <> h Then
                _exclusionW = w
                _exclusionH = h
                ReDim _exclusionMask(w * h - 1)
                For y As Integer = 0 To h - 1
                    For x As Integer = 0 To w - 1
                        Dim p As New NormPoint((x + 0.5) / w, (y + 0.5) / h)
                        For Each z As IReadOnlyList(Of NormPoint) In _exclusions
                            If Geometry.PointInPolygon(p, z) Then
                                _exclusionMask(y * w + x) = True
                                Exit For
                            End If
                        Next
                    Next
                Next
            End If
            For i As Integer = 0 To mask.Length - 1
                If _exclusionMask(i) Then mask(i) = 0
            Next
        End Sub
    End Class

    ''' <summary>عمليات مورفولوجية واستخراج الكتل على أقنعة ثنائية (0/255).</summary>
    Public NotInheritable Class MaskOps
        Private Sub New()
        End Sub

        ''' <summary>فتح مورفولوجي 3×3 (تآكل ثم تمدد) لإزالة الضجيج المنفرد.</summary>
        Public Shared Function Open3x3(mask As Byte(), w As Integer, h As Integer) As Byte()
            Return Dilate3x3(Erode3x3(mask, w, h), w, h)
        End Function

        Public Shared Function Erode3x3(mask As Byte(), w As Integer, h As Integer) As Byte()
            Dim dst(mask.Length - 1) As Byte
            For y As Integer = 1 To h - 2
                For x As Integer = 1 To w - 2
                    Dim i As Integer = y * w + x
                    If mask(i) = 0 Then Continue For
                    Dim keep As Boolean = True
                    For dy As Integer = -1 To 1
                        For dx As Integer = -1 To 1
                            If mask(i + dy * w + dx) = 0 Then
                                keep = False
                                Exit For
                            End If
                        Next
                        If Not keep Then Exit For
                    Next
                    If keep Then dst(i) = 255
                Next
            Next
            Return dst
        End Function

        Public Shared Function Dilate3x3(mask As Byte(), w As Integer, h As Integer) As Byte()
            Dim dst(mask.Length - 1) As Byte
            For y As Integer = 0 To h - 1
                For x As Integer = 0 To w - 1
                    If mask(y * w + x) = 0 Then Continue For
                    For dy As Integer = -1 To 1
                        Dim yy As Integer = y + dy
                        If yy < 0 OrElse yy >= h Then Continue For
                        For dx As Integer = -1 To 1
                            Dim xx As Integer = x + dx
                            If xx < 0 OrElse xx >= w Then Continue For
                            dst(yy * w + xx) = 255
                        Next
                    Next
                Next
            Next
            Return dst
        End Function

        ''' <summary>كتل متصلة (8-اتصال) بمسح عمقي غير تعاودي.</summary>
        Public Shared Function ExtractBlobs(mask As Byte(), w As Integer, h As Integer, minAreaFraction As Double, maxAreaFraction As Double) As List(Of Detection)
            Dim result As New List(Of Detection)()
            Dim visited(mask.Length - 1) As Boolean
            Dim stack As New Stack(Of Integer)()
            Dim total As Integer = w * h
            Dim minArea As Integer = Math.Max(2, CInt(Math.Ceiling(minAreaFraction * total)))
            Dim maxArea As Integer = CInt(maxAreaFraction * total)

            For start As Integer = 0 To mask.Length - 1
                If mask(start) = 0 OrElse visited(start) Then Continue For
                Dim area As Integer = 0
                Dim minX As Integer = Integer.MaxValue, minY As Integer = Integer.MaxValue
                Dim maxX As Integer = -1, maxY As Integer = -1
                stack.Push(start)
                visited(start) = True
                While stack.Count > 0
                    Dim idx As Integer = stack.Pop()
                    Dim x As Integer = idx Mod w
                    Dim y As Integer = idx \ w
                    area += 1
                    If x < minX Then minX = x
                    If x > maxX Then maxX = x
                    If y < minY Then minY = y
                    If y > maxY Then maxY = y
                    For dy As Integer = -1 To 1
                        Dim yy As Integer = y + dy
                        If yy < 0 OrElse yy >= h Then Continue For
                        For dx As Integer = -1 To 1
                            Dim xx As Integer = x + dx
                            If xx < 0 OrElse xx >= w Then Continue For
                            Dim ni As Integer = yy * w + xx
                            If mask(ni) <> 0 AndAlso Not visited(ni) Then
                                visited(ni) = True
                                stack.Push(ni)
                            End If
                        Next
                    Next
                End While
                If area >= minArea AndAlso area <= maxArea Then
                    result.Add(New Detection(New NormRect(minX / CDbl(w), minY / CDbl(h), (maxX - minX + 1) / CDbl(w), (maxY - minY + 1) / CDbl(h)), area))
                End If
            Next
            Return result
        End Function

        ''' <summary>دمج الكتل التي تفصلها فجوة أصغر من العتبة (تكرار حتى الثبات).</summary>
        Public Shared Function MergeNearby(items As List(Of Detection), gap As Double) As List(Of Detection)
            Dim list As New List(Of Detection)(items)
            Dim merged As Boolean = True
            While merged
                merged = False
                For i As Integer = 0 To list.Count - 1
                    For j As Integer = i + 1 To list.Count - 1
                        If list(i).Box.GapTo(list(j).Box) <= gap Then
                            list(i) = New Detection(list(i).Box.Union(list(j).Box), list(i).AreaPixels + list(j).AreaPixels)
                            list.RemoveAt(j)
                            merged = True
                            Exit For
                        End If
                    Next
                    If merged Then Exit For
                Next
            End While
            Return list
        End Function
    End Class

End Namespace
