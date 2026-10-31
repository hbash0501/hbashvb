Imports System.Text.Json
Imports Ptz.Core.Remote

Namespace Cameras

    ''' <summary>يحفظ حالة تبديلات المحرك وعتبات السلوك لكل كاميرا ({key}-engine.json) لتبقى بعد إعادة تشغيل البوابة.</summary>
    Public NotInheritable Class EngineStateStore
        Public NotInheritable Class Model
            Public Property MotionEnabled As Boolean = True
            Public Property BehaviorEnabled As Boolean = True
            Public Property AutoTrackEnabled As Boolean
            Public Property Behavior As BehaviorDto
        End Class

        Private ReadOnly _path As String
        Private ReadOnly _lock As New Object()

        Public Sub New(dataDir As String, cameraId As String)
            _path = IO.Path.Combine(dataDir, cameraId & "-engine.json")
        End Sub

        Public Function Load() As Model
            SyncLock _lock
                If Not IO.File.Exists(_path) Then Return New Model()
                Try
                    Return If(JsonSerializer.Deserialize(Of Model)(IO.File.ReadAllText(_path), WireJson.ClientRead), New Model())
                Catch ex As JsonException
                    Return New Model()
                End Try
            End SyncLock
        End Function

        Public Sub Save(m As Model)
            SyncLock _lock
                Dim dir As String = IO.Path.GetDirectoryName(IO.Path.GetFullPath(_path))
                If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
                Dim tmp As String = _path & ".tmp"
                IO.File.WriteAllText(tmp, JsonSerializer.Serialize(m, WireJson.Write))
                IO.File.Move(tmp, _path, True)
            End SyncLock
        End Sub
    End Class

End Namespace
