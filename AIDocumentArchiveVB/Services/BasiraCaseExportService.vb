Option Strict On
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Xml.Linq

Public NotInheritable Class BasiraCaseExportService
    Private Sub New()
    End Sub
    Public Shared Function Export(answer As BasiraAnswer, documents As List(Of DocumentRecord), destination As String) As String
        If answer Is Nothing Then Throw New ArgumentNullException(NameOf(answer))
        Dim folder = Path.Combine(Path.GetTempPath(), "basira-case-" & Guid.NewGuid().ToString("N"))
        Dim temporaryZip = destination & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Directory.CreateDirectory(Path.Combine(folder, "Sources"))
        Dim manifest As New XElement("EvidencePackage", New XAttribute("CreatedAt", DateTime.Now.ToString("o")), New XElement("Question", answer.Question), New XElement("Mode", answer.Mode))
        Dim report As New StringBuilder(answer.Text)
        Dim copied As Integer = 0
        Try
            For Each e In answer.Evidence
                Dim d = documents.FirstOrDefault(Function(x) x.Id = e.DocumentId)
                Dim item As New XElement("Source", New XAttribute("Number", e.Number), New XAttribute("DocumentId", e.DocumentId), New XElement("Title", e.Title), New XElement("Reference", e.Reference), New XElement("ExcerptOffset", e.Offset), New XElement("Excerpt", e.Quote))
                report.AppendLine(vbCrLf & "[" & e.Number.ToString() & "] " & e.Title & " | " & e.Reference)
                report.AppendLine(e.Quote)
                Dim filePath As String = ""
                If d IsNot Nothing Then filePath = If(d.IsEncrypted, d.ProtectedFilePath, d.StoredPath)
                If Not String.IsNullOrWhiteSpace(filePath) AndAlso File.Exists(filePath) Then
                    Dim nameValue = Path.GetFileName(filePath)
                    For Each c In Path.GetInvalidFileNameChars()
                        nameValue = nameValue.Replace(c, "_"c)
                    Next
                    Dim relative = "Sources/" & e.Number.ToString("000") & "-" & nameValue
                    Dim target = Path.Combine(folder, relative.Replace("/"c, Path.DirectorySeparatorChar))
                    File.Copy(filePath, target)
                    Using stream = File.OpenRead(target), sha = SHA256.Create()
                        item.Add(New XElement("ExportedSHA256", BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()))
                    End Using
                    item.Add(New XElement("File", relative), New XElement("FileStatus", If(d IsNot Nothing AndAlso d.IsEncrypted, "EncryptedOriginal", "Copied")))
                    copied += 1
                Else
                    item.Add(New XElement("FileStatus", "Unavailable"))
                End If
                manifest.Add(item)
            Next
            Dim manifestDocument As New XDocument(manifest)
            manifestDocument.Save(Path.Combine(folder, "evidence.xml"))
            ReplyExportService.ExportReplyAsDocx(Nothing, report.ToString(), Path.Combine(folder, "answer.docx"))
            File.WriteAllText(Path.Combine(folder, "READ_ME_AR.txt"), "حزمة أدلة بصيرة" & vbCrLf & "answer.docx: الإجابة مع المقتطفات." & vbCrLf & "evidence.xml: أرقام المصادر ومواضع المقتطفات وبصمات الملفات المنسوخة." & vbCrLf & "Sources: الملفات المتاحة. الملفات المشفرة تظل مشفرة. تحقق من FileStatus؛ Unavailable يعني عدم توفر الأصل." & vbCrLf & "المقتطفات والنسخ قد لا تعكس تعديلات أحدث طرأت بعد وقت السؤال.", New UTF8Encoding(False))
            ZipFile.CreateFromDirectory(folder, temporaryZip, CompressionLevel.Optimal, False)
            If File.Exists(destination) Then
                File.Replace(temporaryZip, destination, Nothing)
            Else
                File.Move(temporaryZip, destination)
            End If
            Return "تم حفظ الإجابة والأدلة؛ ملفات أصلية متاحة: " & copied.ToString() & " من " & answer.Evidence.Count.ToString()
        Finally
            If Directory.Exists(folder) Then Directory.Delete(folder, True)
            If File.Exists(temporaryZip) Then File.Delete(temporaryZip)
        End Try
    End Function
End Class
