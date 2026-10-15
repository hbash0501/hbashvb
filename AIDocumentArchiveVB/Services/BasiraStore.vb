Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Xml.Linq

Public Class BasiraStore
    Private ReadOnly _folder As String
    Private ReadOnly _gate As New Object()

    Public Sub New(baseFolder As String)
        _folder = Path.Combine(baseFolder, "basira")
        Directory.CreateDirectory(_folder)
    End Sub

    Public Shared Sub AtomicSave(pathValue As String, document As XDocument)
        Directory.CreateDirectory(Path.GetDirectoryName(pathValue))
        Dim temporary = pathValue & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            document.Save(temporary)
            If File.Exists(pathValue) Then
                File.Replace(temporary, pathValue, pathValue & ".bak", True)
            Else
                File.Move(temporary, pathValue)
            End If
        Finally
            If File.Exists(temporary) Then File.Delete(temporary)
        End Try
    End Sub

    Private Function LoadCollections() As XDocument
        Dim filePath = Path.Combine(_folder, "collections.xml")
        If Not File.Exists(filePath) Then Return New XDocument(New XElement("Collections"))
        Return XDocument.Load(filePath) ' لا يستبدل ملفاً تالفاً بقائمة فارغة.
    End Function

    Public Function Collections() As List(Of BasiraCollection)
        SyncLock _gate
            Return LoadCollections().Root.Elements("Collection").Select(Function(e) New BasiraCollection With {.Id = CStr(e.Attribute("Id")), .Name = CStr(e.Element("Name")), .Query = CStr(e.Element("Query")), .Category = CStr(e.Element("Category")), .Filter = CStr(e.Element("Filter"))}).ToList()
        End SyncLock
    End Function

    Public Sub SaveCollection(collection As BasiraCollection)
        If collection Is Nothing OrElse String.IsNullOrWhiteSpace(collection.Name) Then Throw New ArgumentException("أدخل اسماً للمجموعة.")
        SyncLock _gate
            Dim doc = LoadCollections()
            Dim previous = doc.Root.Elements("Collection").FirstOrDefault(Function(e) CStr(e.Attribute("Id")) = collection.Id)
            If previous IsNot Nothing Then previous.Remove()
            doc.Root.Add(New XElement("Collection", New XAttribute("Id", collection.Id), New XElement("Name", collection.Name), New XElement("Query", If(collection.Query, "")), New XElement("Category", If(collection.Category, "")), New XElement("Filter", If(collection.Filter, "all"))))
            AtomicSave(Path.Combine(_folder, "collections.xml"), doc)
        End SyncLock
    End Sub

    Public Sub DeleteCollection(id As String)
        SyncLock _gate
            Dim doc = LoadCollections()
            Dim existing = doc.Root.Elements("Collection").FirstOrDefault(Function(e) CStr(e.Attribute("Id")) = id)
            If existing Is Nothing Then Return
            existing.Remove()
            AtomicSave(Path.Combine(_folder, "collections.xml"), doc)
        End SyncLock
    End Sub

    Private Function RevisionPath(id As String) As String
        Dim name As String
        Using hash = SHA256.Create()
            name = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "")
        End Using
        Return Path.Combine(_folder, "revisions", name & ".xml")
    End Function

    ' حفظ السجل السابق قبل التعديل. الملف الأصلي مستقل؛ هذه نسخ بيانات ونصوص.
    Public Sub CapturePrevious(id As String, previous As XElement)
        If previous Is Nothing Then Return
        SyncLock _gate
            Dim pathValue = RevisionPath(id)
            Dim doc = If(File.Exists(pathValue), XDocument.Load(pathValue), New XDocument(New XElement("Revisions")))
            Dim clone As New XElement(previous)
            If clone.Element("UpdatedAt") IsNot Nothing Then clone.Element("UpdatedAt").Remove()
            Dim latest = doc.Root.Elements("Revision").LastOrDefault()
            If latest IsNot Nothing Then
                Dim old As New XElement(latest.Element("Document"))
                If old.Element("UpdatedAt") IsNot Nothing Then old.Element("UpdatedAt").Remove()
                If XNode.DeepEquals(old, clone) Then Return
            End If
            doc.Root.Add(New XElement("Revision", New XAttribute("Id", Guid.NewGuid().ToString("N")), New XAttribute("CapturedAt", DateTime.Now.ToString("o")), New XElement(previous)))
            While doc.Root.Elements("Revision").Count() > 25
                doc.Root.Elements("Revision").First().Remove()
            End While
            AtomicSave(pathValue, doc)
        End SyncLock
    End Sub

    Public Function Revisions(id As String) As List(Of XElement)
        SyncLock _gate
            Dim pathValue = RevisionPath(id)
            If Not File.Exists(pathValue) Then Return New List(Of XElement)
            Return XDocument.Load(pathValue).Root.Elements("Revision").Reverse().Select(Function(e) New XElement(e)).ToList()
        End SyncLock
    End Function
End Class
