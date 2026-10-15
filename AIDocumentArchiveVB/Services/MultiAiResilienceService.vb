Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks

' طبقة موحدة للذكاء السحابي مع التبديل التلقائي بين المزودات.
' لا تعرف بقية المنصة تفاصيل Groq/Gemini/OpenRouter؛ جميع الطلبات تمر من هنا.
Public Class MultiAiResilienceService
    Private ReadOnly _settings As AppSettings
    Private ReadOnly _blockedUntil As New Dictionary(Of String, DateTime)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _failureCounts As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _syncRoot As New Object()

    Public Sub New(settings As AppSettings)
        _settings = settings
    End Sub

    Public Function HasAnyConfiguredProvider() As Boolean
        For Each profile In BuildProviderProfiles()
            Dim client As New OpenAIClientService(BuildSettingsFor(profile))
            If client.IsEnabled() Then Return True
        Next
        Return False
    End Function

    Public Function GenerateWithFailover(prompt As String, systemPrompt As String, timeoutMs As Integer, token As CancellationToken) As AiProviderResponse
        Dim lastError As String = "لم يتم العثور على مزود ذكاء سحابي مهيأ."

        For Each profile In BuildProviderProfiles()
            token.ThrowIfCancellationRequested()
            If IsTemporarilyBlocked(profile.ProviderName) Then Continue For

            Dim providerSettings = BuildSettingsFor(profile)
            Dim client As New OpenAIClientService(providerSettings)
            If Not client.IsEnabled() Then
                lastError = "لا يوجد مفتاح مهيأ للمزود " & profile.ProviderName & "."
                Continue For
            End If

            Dim sw = Stopwatch.StartNew()
            Try
                Dim result = client.Generate(prompt, systemPrompt, timeoutMs, token)
                sw.Stop()
                If Not String.IsNullOrWhiteSpace(result) Then
                    RecordSuccess(profile.ProviderName)
                    Return New AiProviderResponse With {
                        .Success = True,
                        .ProviderName = profile.ProviderName,
                        .ModelName = profile.ModelName,
                        .Content = result,
                        .DurationMs = sw.ElapsedMilliseconds
                    }
                End If
                lastError = "أعاد المزود " & profile.ProviderName & " نتيجة فارغة."
                RecordFailure(profile.ProviderName)
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                sw.Stop()
                lastError = ex.Message
                RecordFailure(profile.ProviderName)
            End Try
        Next

        Return New AiProviderResponse With {
            .Success = False,
            .ProviderName = "LocalRules",
            .ModelName = "Local",
            .Content = "",
            .ErrorMessage = lastError,
            .DurationMs = 0
        }
    End Function

    Public Async Function TestAllProvidersAsync(token As CancellationToken) As Task(Of List(Of AiProviderHealthStatus))
        Return Await Task.Run(Function() TestAllProviders(token), token)
    End Function

    Public Function TestAllProviders(token As CancellationToken) As List(Of AiProviderHealthStatus)
        Dim results As New List(Of AiProviderHealthStatus)()
        For Each profile In BuildProviderProfiles()
            token.ThrowIfCancellationRequested()
            Dim providerSettings = BuildSettingsFor(profile)
            Dim client As New OpenAIClientService(providerSettings)
            Dim item As New AiProviderHealthStatus With {
                .ProviderName = profile.ProviderName,
                .ModelName = profile.ModelName,
                .IsConfigured = client.IsEnabled(),
                .CheckedAt = DateTime.Now
            }

            If Not item.IsConfigured Then
                item.IsAvailable = False
                item.StatusText = "غير مهيأ - المفتاح غير موجود"
                results.Add(item)
                Continue For
            End If

            Dim sw = Stopwatch.StartNew()
            Try
                Dim answer = client.Generate("أجب بكلمة واحدة فقط: جاهز", "اختبار اتصال مختصر.", Math.Max(10000, GetTimeoutMs(25)))
                sw.Stop()
                item.ResponseMs = sw.ElapsedMilliseconds
                item.IsAvailable = Not String.IsNullOrWhiteSpace(answer)
                item.StatusText = If(item.IsAvailable, "متاح", "لم يصل رد")
                If item.IsAvailable Then RecordSuccess(profile.ProviderName) Else RecordFailure(profile.ProviderName)
            Catch ex As Exception
                sw.Stop()
                item.ResponseMs = sw.ElapsedMilliseconds
                item.IsAvailable = False
                item.StatusText = FriendlyShortMessage(ex.Message)
                RecordFailure(profile.ProviderName)
            End Try
            results.Add(item)
        Next
        Return results
    End Function

    Public Function BuildProviderProfiles() As List(Of AiProviderProfile)
        Dim orderedNames As New List(Of String)()
        AddUnique(orderedNames, If(_settings Is Nothing, "Groq", _settings.AiProvider))

        Dim failoverEnabled = _settings Is Nothing OrElse _settings.EnableAiFailover
        If failoverEnabled Then
            Dim priorityText = If(_settings Is Nothing, "Groq;Gemini;OpenRouter", _settings.AiProviderPriority)
            If String.IsNullOrWhiteSpace(priorityText) Then priorityText = "Groq;Gemini;OpenRouter"
            For Each name In priorityText.Split(New Char() {";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
                AddUnique(orderedNames, name)
            Next
            AddUnique(orderedNames, "Groq")
            AddUnique(orderedNames, "Gemini")
            AddUnique(orderedNames, "OpenRouter")
        End If

        Dim result As New List(Of AiProviderProfile)()
        Dim priority As Integer = 1
        For Each providerName In orderedNames
            Dim normalized = DisplayProviderName(providerName)
            Dim endpoint = OpenAIClientService.DefaultEndpoint(normalized)
            Dim model = OpenAIClientService.DefaultModel(normalized)
            Dim envName = DefaultEnvironmentName(normalized)

            If _settings IsNot Nothing AndAlso String.Equals(OpenAIClientService.NormalizeProvider(_settings.AiProvider), OpenAIClientService.NormalizeProvider(normalized), StringComparison.OrdinalIgnoreCase) Then
                If Not String.IsNullOrWhiteSpace(_settings.CloudAiEndpoint) Then endpoint = _settings.CloudAiEndpoint.Trim()
                If Not String.IsNullOrWhiteSpace(_settings.CloudAiModel) Then model = _settings.CloudAiModel.Trim()
                If Not String.IsNullOrWhiteSpace(_settings.CloudAiApiKeyEnvironmentName) Then envName = _settings.CloudAiApiKeyEnvironmentName.Trim()
            End If

            result.Add(New AiProviderProfile With {
                .ProviderName = normalized,
                .Endpoint = endpoint,
                .ModelName = model,
                .ApiKeyEnvironmentName = envName,
                .Priority = priority
            })
            priority += 1
        Next
        Return result
    End Function

    Private Function BuildSettingsFor(profile As AiProviderProfile) As AppSettings
        Dim source As AppSettings = _settings
        If source Is Nothing Then source = AppSettings.DefaultSettings()
        Dim directKey As String = ""
        If String.Equals(OpenAIClientService.NormalizeProvider(source.AiProvider), OpenAIClientService.NormalizeProvider(profile.ProviderName), StringComparison.OrdinalIgnoreCase) Then
            directKey = If(source.CloudAiApiKey, "")
        End If

        Return New AppSettings With {
            .BaseDataFolder = source.BaseDataFolder,
            .ArchiveRootFolder = source.ArchiveRootFolder,
            .TesseractExePath = source.TesseractExePath,
            .OcrLanguage = source.OcrLanguage,
            .CurrentEmployeeName = source.CurrentEmployeeName,
            .EnableRuleBasedAi = source.EnableRuleBasedAi,
            .EnableOpenAI = source.EnableOpenAI,
            .AiProvider = profile.ProviderName,
            .CloudAiEndpoint = profile.Endpoint,
            .CloudAiModel = profile.ModelName,
            .CloudAiApiKey = directKey,
            .CloudAiApiKeyEnvironmentName = profile.ApiKeyEnvironmentName,
            .OpenAiEndpoint = profile.Endpoint,
            .OpenAiModel = profile.ModelName,
            .OpenAiApiKey = directKey,
            .OpenAiOrganization = source.OpenAiOrganization,
            .OpenAiProject = source.OpenAiProject,
            .AiRequestTimeoutSeconds = source.AiRequestTimeoutSeconds
        }
    End Function

    Private Function IsTemporarilyBlocked(providerName As String) As Boolean
        SyncLock _syncRoot
            Dim untilValue As DateTime = DateTime.MinValue
            If Not _blockedUntil.TryGetValue(providerName, untilValue) Then Return False
            If untilValue <= DateTime.Now Then
                _blockedUntil.Remove(providerName)
                _failureCounts.Remove(providerName)
                Return False
            End If
            Return True
        End SyncLock
    End Function

    Private Sub RecordSuccess(providerName As String)
        SyncLock _syncRoot
            _failureCounts(providerName) = 0
            If _blockedUntil.ContainsKey(providerName) Then _blockedUntil.Remove(providerName)
        End SyncLock
    End Sub

    Private Sub RecordFailure(providerName As String)
        SyncLock _syncRoot
            Dim count As Integer = 0
            _failureCounts.TryGetValue(providerName, count)
            count += 1
            _failureCounts(providerName) = count
            If count >= 2 Then
                Dim minutes = If(_settings Is Nothing, 10, Math.Max(1, _settings.ProviderFailureCooldownMinutes))
                _blockedUntil(providerName) = DateTime.Now.AddMinutes(minutes)
            End If
        End SyncLock
    End Sub

    Private Function GetTimeoutMs(defaultSeconds As Integer) As Integer
        Dim seconds = If(_settings Is Nothing, defaultSeconds, _settings.AiRequestTimeoutSeconds)
        If seconds <= 0 Then seconds = defaultSeconds
        Return Math.Max(10000, seconds * 1000)
    End Function

    Private Shared Sub AddUnique(list As List(Of String), value As String)
        Dim normalized = DisplayProviderName(value)
        For Each current In list
            If String.Equals(OpenAIClientService.NormalizeProvider(current), OpenAIClientService.NormalizeProvider(normalized), StringComparison.OrdinalIgnoreCase) Then Return
        Next
        list.Add(normalized)
    End Sub

    Private Shared Function DisplayProviderName(value As String) As String
        Select Case OpenAIClientService.NormalizeProvider(value)
            Case "groq"
                Return "Groq"
            Case "gemini"
                Return "Gemini"
            Case "openrouter"
                Return "OpenRouter"
            Case "openai"
                Return "OpenAI"
            Case Else
                Return If(String.IsNullOrWhiteSpace(value), "Groq", value.Trim())
        End Select
    End Function

    Private Shared Function DefaultEnvironmentName(providerName As String) As String
        Select Case OpenAIClientService.NormalizeProvider(providerName)
            Case "groq"
                Return "GROQ_API_KEY"
            Case "gemini"
                Return "GEMINI_API_KEY"
            Case "openrouter"
                Return "OPENROUTER_API_KEY"
            Case "openai"
                Return "OPENAI_API_KEY"
            Case Else
                Return "AI_API_KEY"
        End Select
    End Function

    Private Shared Function FriendlyShortMessage(value As String) As String
        Dim text = If(value, "تعذر الاتصال")
        text = text.Replace(Environment.NewLine, " ").Trim()
        If text.Length > 160 Then text = text.Substring(0, 160) & "..."
        Return text
    End Function
End Class
