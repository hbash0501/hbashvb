Imports System.Xml
Imports System.Xml.Linq
Imports Ptz.Core.Models

Namespace Isapi

    ''' <summary>أدوات XML آمنة لبروتوكول ISAPI (منع DTD/كيانات خارجية، بحث بالاسم المحلي).</summary>
    Public NotInheritable Class IsapiXml
        Private Sub New()
        End Sub

        Public Shared ReadOnly Ns As XNamespace = "http://www.hikvision.com/ver20/XMLSchema"

        Public Shared Function Parse(text As String) As XDocument
            Dim settings As New XmlReaderSettings With {
                .DtdProcessing = DtdProcessing.Prohibit,
                .XmlResolver = Nothing,
                .MaxCharactersFromEntities = 0,
                .MaxCharactersInDocument = 8 * 1024 * 1024
            }
            Using sr As New IO.StringReader(text)
                Using xr As XmlReader = XmlReader.Create(sr, settings)
                    Return XDocument.Load(xr)
                End Using
            End Using
        End Function

        ''' <summary>أول عنصر بالاسم المحلي (يتجاهل مساحة الأسماء التي تختلف بين البرمجيات الثابتة).</summary>
        Public Shared Function FindLocal(root As XContainer, localName As String) As XElement
            If root Is Nothing Then Return Nothing
            For Each e As XElement In root.Descendants()
                If e.Name.LocalName = localName Then Return e
            Next
            Return Nothing
        End Function

        Public Shared Function ValueOf(root As XContainer, localName As String) As String
            Dim e As XElement = FindLocal(root, localName)
            Return If(e Is Nothing, Nothing, e.Value)
        End Function

        ''' <summary>يضبط قيمة عنصر موجود (قراءة-تعديل-كتابة). False إن لم يوجد العنصر.</summary>
        Public Shared Function SetValue(root As XContainer, localName As String, value As String) As Boolean
            Dim e As XElement = FindLocal(root, localName)
            If e Is Nothing Then Return False
            e.Value = value
            Return True
        End Function

        ''' <summary>يبني عنصر جسم طلب (مساحة أسماء + version) — الحشو عبر XElement يمنع حقن XML.</summary>
        Public Shared Function Build(name As String, ParamArray children() As Object) As XElement
            Dim root As New XElement(Ns + name, New XAttribute("version", "2.0"))
            AddChildren(root, children)
            Return root
        End Function

        Public Shared Function El(name As String, ParamArray children() As Object) As XElement
            Dim e As New XElement(Ns + name)
            AddChildren(e, children)
            Return e
        End Function

        Private Shared Sub AddChildren(parent As XElement, children() As Object)
            For Each c As Object In children
                If c Is Nothing Then Continue For
                parent.Add(c)
            Next
        End Sub

        Public Shared Function Num(name As String, value As Integer) As XElement
            Return New XElement(Ns + name, value.ToString(Globalization.CultureInfo.InvariantCulture))
        End Function

        Public Shared Function Txt(name As String, value As String) As XElement
            Return New XElement(Ns + name, value)
        End Function

        ''' <summary>هل رد الجهاز ResponseStatus ناجح؟ (statusCode=1 أو statusString=OK)</summary>
        Public Shared Function IsOkStatus(doc As XDocument) As Boolean
            Dim code As String = ValueOf(doc, "statusCode")
            Dim str As String = ValueOf(doc, "statusString")
            If code Is Nothing AndAlso str Is Nothing Then Return True ' ليس ResponseStatus
            Return code = "1" OrElse String.Equals(str, "OK", StringComparison.OrdinalIgnoreCase)
        End Function
    End Class

    ''' <summary>مسارات ISAPI المستخدمة — مركزة ليسهل تصحيحها لكل إصدار برمجي ثابت.</summary>
    Public NotInheritable Class IsapiPaths
        Private Sub New()
        End Sub

        Public Const DeviceInfo As String = "/ISAPI/System/deviceInfo"
        Public Const Reboot As String = "/ISAPI/System/reboot"
        Public Const AlertStream As String = "/ISAPI/Event/notification/alertStream"

        Public Shared Function Continuous(ch As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/continuous"
        End Function

        Public Shared Function Momentary(ch As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/momentary"
        End Function

        Public Shared Function Absolute(ch As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/absolute"
        End Function

        Public Shared Function PtzStatus(ch As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/status"
        End Function

        Public Shared Function Position3D(ch As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/position3D"
        End Function

        Public Shared Function Home(ch As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/homeposition"
        End Function

        Public Shared Function Presets(ch As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/presets"
        End Function

        Public Shared Function Preset(ch As Integer, id As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/presets/{id}"
        End Function

        Public Shared Function Patrol(ch As Integer, id As Integer, action As String) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/patrols/{id}/{action}"
        End Function

        Public Shared Function Pattern(ch As Integer, id As Integer, action As String) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/patterns/{id}/{action}"
        End Function

        Public Shared Function Focus(ch As Integer) As String
            Return $"/ISAPI/System/Video/inputs/channels/{ch}/focus"
        End Function

        Public Shared Function Iris(ch As Integer) As String
            Return $"/ISAPI/System/Video/inputs/channels/{ch}/iris"
        End Function

        Public Shared Function Aux(ch As Integer, id As Integer) As String
            Return $"/ISAPI/PTZCtrl/channels/{ch}/auxcontrols/{id}"
        End Function

        Public Shared Function Snapshot(streamChannel As Integer) As String
            Return $"/ISAPI/Streaming/channels/{streamChannel}/picture"
        End Function

        Public Shared Function Image(template As String, ch As Integer) As String
            Return template.Replace("{ch}", ch.ToString(Globalization.CultureInfo.InvariantCulture))
        End Function
    End Class

End Namespace
