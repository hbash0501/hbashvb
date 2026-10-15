Imports System
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Text.RegularExpressions

' بوابة ذكاء سحابي متعددة المزودات.
' الاسم بقي OpenAIClientService للتوافق مع بقية الكود القديم، لكن الخدمة الآن تدعم:
' Groq، Gemini، OpenRouter، OpenAI، وأي مزود OpenAI-Compatible.
Public Class OpenAIClientService
    Private ReadOnly _settings As AppSettings

    Public Sub New(settings As AppSettings)
        _settings = settings
    End Sub

    Public Function IsEnabled() As Boolean
        If _settings Is Nothing OrElse Not _settings.EnableOpenAI Then Return False
        If String.IsNullOrWhiteSpace(GetApiKey()) Then Return False
        Return Not String.IsNullOrWhiteSpace(GetEndpoint()) AndAlso Not String.IsNullOrWhiteSpace(GetModel())
    End Function

    Public Function ProviderDisplayName() As String
        Select Case NormalizeProvider(GetProvider())
            Case "groq"
                Return "Groq"
            Case "gemini"
                Return "Google Gemini"
            Case "openrouter"
                Return "OpenRouter"
            Case "openai"
                Return "OpenAI"
            Case Else
                Return "OpenAI-Compatible"
        End Select
    End Function

    Public Function TestConnection() As String
        If _settings Is Nothing Then Return "الإعدادات غير مهيأة."
        If String.IsNullOrWhiteSpace(GetApiKey()) Then
            Return "لم يتم ضبط مفتاح الذكاء السحابي. ضع المفتاح في الإعدادات أو في متغير البيئة: " & GetApiKeyEnvironmentName()
        End If

        Dim answer As String = Generate("أجب بكلمة واحدة فقط: جاهز", "أنت مساعد عربي مختصر داخل منصة أرشفة مستندات.", 45000)
        If String.IsNullOrWhiteSpace(answer) Then Return "لم يصل رد من مزود الذكاء السحابي. تحقق من المفتاح واسم النموذج والاتصال بالإنترنت."
        Return answer.Trim()
    End Function

    Public Function Generate(prompt As String, Optional systemPrompt As String = "", Optional timeoutMs As Integer = 120000, Optional token As CancellationToken = Nothing) As String
        token.ThrowIfCancellationRequested()
        If _settings Is Nothing Then Return ""
        If String.IsNullOrWhiteSpace(GetApiKey()) Then Return ""

        Dim endpoint As String = GetEndpoint()
        Dim model As String = GetModel()
        If String.IsNullOrWhiteSpace(endpoint) OrElse String.IsNullOrWhiteSpace(model) Then Return ""

        Dim provider = NormalizeProvider(GetProvider())
        Dim json As String
        If provider = "openai" AndAlso endpoint.EndsWith("/responses", StringComparison.OrdinalIgnoreCase) Then
            json = BuildResponsesPayload(model, prompt, systemPrompt)
        Else
            json = BuildChatCompletionsPayload(model, prompt, systemPrompt)
        End If

        Dim request = CType(WebRequest.Create(endpoint), HttpWebRequest)
        request.Method = "POST"
        request.ContentType = "application/json; charset=utf-8"
        request.Headers(HttpRequestHeader.Authorization) = "Bearer " & GetApiKey()
        request.Timeout = timeoutMs
        request.ReadWriteTimeout = timeoutMs

        If provider = "openai" Then
            If Not String.IsNullOrWhiteSpace(_settings.OpenAiOrganization) Then request.Headers("OpenAI-Organization") = _settings.OpenAiOrganization.Trim()
            If Not String.IsNullOrWhiteSpace(_settings.OpenAiProject) Then request.Headers("OpenAI-Project") = _settings.OpenAiProject.Trim()
        ElseIf provider = "openrouter" Then
            request.Headers("HTTP-Referer") = "https://local-document-archive.app"
            request.Headers("X-Title") = "AI Document Archive"
        End If

        Using cancellation = token.Register(Sub() request.Abort())
            Try
                token.ThrowIfCancellationRequested()
                Dim bytes = Encoding.UTF8.GetBytes(json)
                request.ContentLength = bytes.Length
                Using stream = request.GetRequestStream()
                    stream.Write(bytes, 0, bytes.Length)
                End Using
                Using response = CType(request.GetResponse(), HttpWebResponse)
                    Using sr As New StreamReader(response.GetResponseStream(), Encoding.UTF8)
                        Dim body = sr.ReadToEnd()
                        token.ThrowIfCancellationRequested()
                        Dim text = ExtractResponseText(body)
                        If String.IsNullOrWhiteSpace(text) Then text = body
                        Return text.Trim()
                    End Using
                End Using
            Catch ex As WebException
                token.ThrowIfCancellationRequested()
                Throw New ApplicationException(BuildFriendlyCloudAiError(ex), ex)
            End Try
        End Using
    End Function

    Public Function GetProvider() As String
        If _settings Is Nothing Then Return "Groq"
        Dim p As String = If(_settings.AiProvider, "").Trim()
        If p = "" Then p = "Groq"
        Return p
    End Function

    Public Function GetEndpoint() As String
        If _settings Is Nothing Then Return DefaultEndpoint("Groq")
        Dim endpoint As String = If(_settings.CloudAiEndpoint, "").Trim()
        If endpoint = "" Then endpoint = If(_settings.OpenAiEndpoint, "").Trim()
        If endpoint = "" Then endpoint = DefaultEndpoint(GetProvider())
        Return NormalizeEndpoint(GetProvider(), endpoint)
    End Function

    Public Function GetModel() As String
        If _settings Is Nothing Then Return DefaultModel("Groq")
        Dim model As String = If(_settings.CloudAiModel, "").Trim()
        If model = "" Then model = If(_settings.OpenAiModel, "").Trim()
        If model = "" Then model = DefaultModel(GetProvider())
        Return model
    End Function

    Public Function GetApiKeyEnvironmentName() As String
        If _settings Is Nothing Then Return "GROQ_API_KEY"
        Dim envName As String = If(_settings.CloudAiApiKeyEnvironmentName, "").Trim()
        If envName <> "" Then Return envName
        Select Case NormalizeProvider(GetProvider())
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

    Private Function GetApiKey() As String
        If _settings Is Nothing Then Return ""
        Return DocumentSecurityService.ResolveProviderApiKey(_settings, GetProvider(), GetApiKeyEnvironmentName())
    End Function

    Public Shared Function DefaultEndpoint(providerName As String) As String
        Select Case NormalizeProvider(providerName)
            Case "groq"
                Return "https://api.groq.com/openai/v1/chat/completions"
            Case "gemini"
                Return "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"
            Case "openrouter"
                Return "https://openrouter.ai/api/v1/chat/completions"
            Case "openai"
                Return "https://api.openai.com/v1/responses"
            Case Else
                Return "https://api.groq.com/openai/v1/chat/completions"
        End Select
    End Function

    Public Shared Function DefaultModel(providerName As String) As String
        Select Case NormalizeProvider(providerName)
            Case "groq"
                Return "llama-3.1-8b-instant"
            Case "gemini"
                Return "gemini-2.5-flash-lite"
            Case "openrouter"
                Return "openrouter/free"
            Case "openai"
                Return "gpt-4.1-mini"
            Case Else
                Return "llama-3.1-8b-instant"
        End Select
    End Function

    Public Shared Function NormalizeProvider(providerName As String) As String
        Dim p = If(providerName, "").Trim().ToLowerInvariant()
        If p.Contains("groq") Then Return "groq"
        If p.Contains("gemini") OrElse p.Contains("google") Then Return "gemini"
        If p.Contains("router") Then Return "openrouter"
        If p.Contains("openai") Then Return "openai"
        If p = "" Then Return "groq"
        Return "compatible"
    End Function

    Private Shared Function NormalizeEndpoint(providerName As String, endpoint As String) As String
        Dim e = If(endpoint, "").Trim()
        If e = "" Then Return DefaultEndpoint(providerName)
        If e.EndsWith("/", StringComparison.Ordinal) Then e = e.Substring(0, e.Length - 1)

        Dim provider = NormalizeProvider(providerName)
        If provider = "openai" Then
            If e.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) Then Return e & "/responses"
            If e.EndsWith("/responses", StringComparison.OrdinalIgnoreCase) Then Return e
            If e.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) Then Return e
            Return e
        End If

        If e.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) Then Return e
        If e.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) OrElse e.EndsWith("/openai", StringComparison.OrdinalIgnoreCase) Then Return e & "/chat/completions"
        Return e
    End Function

    Private Shared Function BuildResponsesPayload(model As String, prompt As String, systemPrompt As String) As String
        Dim inputText As String = If(systemPrompt, "") & Environment.NewLine & Environment.NewLine & If(prompt, "")
        Return "{" & _
            """model"":""" & JsonEscape(model) & """," & _
            """input"":""" & JsonEscape(inputText) & """," & _
            """max_output_tokens"":1800" & _
            "}"
    End Function

    Private Shared Function BuildChatCompletionsPayload(model As String, prompt As String, systemPrompt As String) As String
        Dim sys = If(String.IsNullOrWhiteSpace(systemPrompt), "أنت مساعد عربي محترف داخل منصة أرشفة مستندات.", systemPrompt)
        Return "{" & _
            """model"":""" & JsonEscape(model) & """," & _
            """messages"":" & _
            "[{""role"":""system"",""content"":""" & JsonEscape(sys) & """}," & _
            "{""role"":""user"",""content"":""" & JsonEscape(If(prompt, "")) & """}]," & _
            """temperature"":0.2," & _
            """max_tokens"":1800" & _
            "}"
    End Function

    Private Shared Function BuildFriendlyCloudAiError(ex As WebException) As String
        Dim details As String = ""
        Try
            If ex.Response IsNot Nothing Then
                Using sr As New StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)
                    details = sr.ReadToEnd()
                End Using
            End If
        Catch
        End Try
        Dim combined = (If(ex.Message, "") & " " & If(details, "")).Trim()
        If combined.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 OrElse combined.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return "مفتاح الذكاء السحابي غير صحيح أو غير مفعّل. تحقق من المفتاح واسم المزود في الإعدادات."
        End If
        If combined.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0 OrElse combined.IndexOf("rate", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return "تم تجاوز حد الاستخدام المجاني أو حد الطلبات الحالي لمزود الذكاء السحابي. جرّب لاحقاً أو بدّل المزود من الإعدادات."
        End If
        If combined.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso combined.IndexOf("not", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return "النموذج المحدد غير متاح لهذا المفتاح. غيّر اسم النموذج من الإعدادات إلى نموذج متاح في حسابك."
        End If
        If combined.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 OrElse combined.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return "تعذر الاتصال بمزود الذكاء السحابي. تحقق من الإنترنت والجدار الناري ورابط API."
        End If
        If String.IsNullOrWhiteSpace(combined) Then Return "تعذر تنفيذ طلب الذكاء السحابي."
        If combined.Length > 450 Then combined = combined.Substring(0, 450) & "..."
        Return combined
    End Function

    Public Shared Function JsonEscape(value As String) As String
        If value Is Nothing Then Return ""
        Dim q As String = """"
        Return value.Replace("\", "\\").Replace(q, "\" & q).Replace(ControlChars.CrLf, "\n").Replace(ControlChars.Lf, "\n").Replace(ControlChars.Cr, "\n").Replace(ControlChars.Tab.ToString(), "\t")
    End Function

    Public Shared Function JsonUnescape(value As String) As String
        If value Is Nothing Then Return ""
        Dim q As String = """"
        Dim s = value.Replace("\n", Environment.NewLine).Replace("\r", "").Replace("\t", ControlChars.Tab.ToString())
        s = s.Replace("\" & q, q).Replace("\\", "\")
        Return s
    End Function

    Public Shared Function ExtractJsonString(json As String, key As String) As String
        If String.IsNullOrWhiteSpace(json) Then Return ""
        Dim q As String = """"
        Dim pattern As String = q & Regex.Escape(key) & q & "\s*:\s*" & q & "((?:\\.|[^" & q & "])*)" & q
        Dim m = Regex.Match(json, pattern, RegexOptions.Singleline)
        If Not m.Success Then Return ""
        Return JsonUnescape(m.Groups(1).Value)
    End Function

    Private Shared Function ExtractResponseText(json As String) As String
        If String.IsNullOrWhiteSpace(json) Then Return ""
        Dim outputText = ExtractJsonString(json, "output_text")
        If Not String.IsNullOrWhiteSpace(outputText) Then Return outputText

        Dim messageText = ExtractFirstChatCompletionContent(json)
        If Not String.IsNullOrWhiteSpace(messageText) Then Return messageText

        Dim sb As New StringBuilder()
        Dim q As String = """"
        Dim pattern As String = q & "text" & q & "\s*:\s*" & q & "((?:\\.|[^" & q & "])*)" & q
        For Each m As Match In Regex.Matches(json, pattern, RegexOptions.Singleline)
            Dim value = JsonUnescape(m.Groups(1).Value)
            If Not String.IsNullOrWhiteSpace(value) Then sb.AppendLine(value)
        Next
        Return sb.ToString().Trim()
    End Function

    Private Shared Function ExtractFirstChatCompletionContent(json As String) As String
        If String.IsNullOrWhiteSpace(json) Then Return ""
        Dim q As String = """"
        Dim pattern As String = q & "message" & q & "\s*:\s*\{.*?" & q & "content" & q & "\s*:\s*" & q & "((?:\\.|[^" & q & "])*)" & q
        Dim m = Regex.Match(json, pattern, RegexOptions.Singleline)
        If m.Success Then Return JsonUnescape(m.Groups(1).Value)

        Dim alt As String = q & "content" & q & "\s*:\s*" & q & "((?:\\.|[^" & q & "])*)" & q
        Dim m2 = Regex.Match(json, alt, RegexOptions.Singleline)
        If m2.Success Then Return JsonUnescape(m2.Groups(1).Value)
        Return ""
    End Function
End Class
