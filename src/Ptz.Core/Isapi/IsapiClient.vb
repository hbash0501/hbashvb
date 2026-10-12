Imports System.Net
Imports System.Net.Http
Imports System.Net.Security
Imports System.Security.Authentication
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports System.Xml.Linq
Imports Ptz.Core.Models

Namespace Isapi

    ''' <summary>خطأ من بروتوكول ISAPI. لا يحتوي أبداً على بيانات اعتماد.</summary>
    Public Class IsapiException
        Inherits Exception

        Public ReadOnly Property HttpStatus As Integer
        Public ReadOnly Property SubStatus As String

        Public Sub New(httpStatus As Integer, subStatus As String, message As String)
            MyBase.New(message)
            Me.HttpStatus = httpStatus
            Me.SubStatus = subStatus
        End Sub
    End Class

    ''' <summary>
    ''' عميل ISAPI فوق HTTPS: مصادقة Digest، TLS 1.2+، تثبيت الشهادة (Pinning)،
    ''' منع إعادة التوجيه، سقف لحجم الاستجابة، مهلة لكل طلب، وإعادة محاولة لطلبات GET فقط.
    ''' </summary>
    Public NotInheritable Class IsapiClient
        Implements IDisposable

        Public Const MaxResponseBytes As Integer = 8 * 1024 * 1024
        Private Const GetRetries As Integer = 2

        Private ReadOnly _http As HttpClient
        Private ReadOnly _timeout As TimeSpan

        ''' <param name="handler">للاختبارات: معالج بديل. في الإنتاج يُنشأ معالج آمن تلقائياً.</param>
        Public Sub New(connection As CameraConnection, Optional handler As HttpMessageHandler = Nothing)
            If connection Is Nothing Then Throw New ArgumentNullException(NameOf(connection))
            connection.Validate()
            _timeout = TimeSpan.FromSeconds(connection.TimeoutSeconds)
            _http = New HttpClient(If(handler, CreateSecureHandler(connection)), True) With {
                .BaseAddress = connection.BaseUri,
                .Timeout = Threading.Timeout.InfiniteTimeSpan,
                .MaxResponseContentBufferSize = MaxResponseBytes
            }
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/xml, image/jpeg, */*")
        End Sub

        Public Shared Function CreateSecureHandler(c As CameraConnection) As HttpMessageHandler
            Dim cache As New CredentialCache()
            Dim cred As New NetworkCredential(c.Username, c.Password)
            cache.Add(c.BaseUri, "Digest", cred)
            If c.UseHttps Then cache.Add(c.BaseUri, "Basic", cred) ' Basic فقط داخل قناة مشفّرة
            Dim pinned As String = c.PinnedCertSha256
            Dim h As New SocketsHttpHandler With {
                .Credentials = cache,
                .PreAuthenticate = False,
                .AllowAutoRedirect = False,
                .UseCookies = False,
                .ConnectTimeout = TimeSpan.FromSeconds(c.TimeoutSeconds)
            }
            h.SslOptions = New SslClientAuthenticationOptions With {
                .EnabledSslProtocols = SslProtocols.Tls12 Or SslProtocols.Tls13,
                .RemoteCertificateValidationCallback =
                    Function(sender, cert, chain, errors) IsCertificateAcceptable(pinned, cert, errors)
            }
            Return h
        End Function

        ''' <summary>
        ''' سياسة الشهادة: إن وُجدت بصمة مثبّتة يُقبل التطابق التام فقط (حتى للشهادات الذاتية التوقيع)،
        ''' وإلا يجب أن تكون السلسلة موثوقة بلا أخطاء.
        ''' </summary>
        Public Shared Function IsCertificateAcceptable(pinnedSha256Hex As String, cert As X509Certificate, errors As SslPolicyErrors) As Boolean
            If cert Is Nothing Then Return False
            If Not String.IsNullOrWhiteSpace(pinnedSha256Hex) Then
                Dim actual As Byte() = SHA256.HashData(cert.GetRawCertData())
                Dim wanted As String = pinnedSha256Hex.Replace(":", "").Replace(" ", "").Trim()
                Dim wantedBytes As Byte()
                Try
                    wantedBytes = Convert.FromHexString(wanted)
                Catch ex As FormatException
                    Return False
                End Try
                Return CryptographicOperations.FixedTimeEquals(actual, wantedBytes)
            End If
            Return errors = SslPolicyErrors.None
        End Function

        ''' <summary>بصمة SHA-256 لشهادة (hex كبير) — تُستخدم عند أول اتصال لتثبيتها بعد موافقة المسؤول.</summary>
        Public Shared Function Fingerprint(cert As X509Certificate) As String
            Return Convert.ToHexString(SHA256.HashData(cert.GetRawCertData()))
        End Function

        Private Shared Sub ValidatePath(path As String)
            If String.IsNullOrEmpty(path) OrElse Not path.StartsWith("/ISAPI/", StringComparison.Ordinal) Then
                Throw New ArgumentException("مسار ISAPI غير صالح.", NameOf(path))
            End If
            If path.Contains("..") OrElse path.Contains("//") OrElse path.IndexOfAny({"\"c, "@"c, ChrW(0), vbCr(0), vbLf(0)}) >= 0 Then
                Throw New ArgumentException("مسار ISAPI غير صالح.", NameOf(path))
            End If
        End Sub

        Private Async Function SendCoreAsync(method As HttpMethod, path As String, body As XElement, ct As Threading.CancellationToken) As Task(Of Byte())
            ValidatePath(path)
            Dim attempts As Integer = If(method = HttpMethod.Get, GetRetries + 1, 1)
            Dim lastError As Exception = Nothing
            For attempt As Integer = 0 To attempts - 1
                If attempt > 0 Then Await Task.Delay(TimeSpan.FromMilliseconds(200 * (1 << (attempt - 1))), ct).ConfigureAwait(False)
                Using cts As Threading.CancellationTokenSource = Threading.CancellationTokenSource.CreateLinkedTokenSource(ct)
                    cts.CancelAfter(_timeout)
                    Using req As New HttpRequestMessage(method, path.TrimStart("/"c))
                        If body IsNot Nothing Then
                            req.Content = New StringContent(New XDocument(New XDeclaration("1.0", "UTF-8", Nothing), body).ToString(), Encoding.UTF8, "application/xml")
                        End If
                        Try
                            Using resp As HttpResponseMessage = Await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(False)
                                Dim data As Byte() = Await resp.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(False)
                                If resp.IsSuccessStatusCode Then Return data
                                Throw BuildException(CInt(resp.StatusCode), data)
                            End Using
                        Catch ex As HttpRequestException
                            lastError = ex
                        Catch ex As OperationCanceledException When Not ct.IsCancellationRequested
                            lastError = New TimeoutException("انتهت مهلة الاتصال بالكاميرا.")
                        End Try
                    End Using
                End Using
            Next
            Throw New IsapiException(0, "", "تعذّر الاتصال بالكاميرا: " & If(lastError?.Message, "خطأ غير معروف"))
        End Function

        Private Shared Function BuildException(status As Integer, data As Byte()) As IsapiException
            Dim sub_ As String = ""
            Dim detail As String = ""
            Try
                Dim doc As XDocument = IsapiXml.Parse(Encoding.UTF8.GetString(data))
                sub_ = If(IsapiXml.ValueOf(doc, "subStatusCode"), "")
                detail = If(IsapiXml.ValueOf(doc, "statusString"), "")
            Catch ex As Exception When TypeOf ex Is Xml.XmlException OrElse TypeOf ex Is ArgumentException
                ' الرد ليس XML — نتجاهل التفاصيل.
            End Try
            Dim msg As String
            Select Case status
                Case 401 : msg = "فشلت المصادقة مع الكاميرا (401)."
                Case 403 : msg = "رفضت الكاميرا العملية (403)."
                Case 404 : msg = "الميزة أو المسار غير مدعوم على هذه الكاميرا (404)."
                Case Else : msg = $"خطأ من الكاميرا (HTTP {status})."
            End Select
            If detail <> "" Then msg &= " " & detail
            If sub_ <> "" Then msg &= $" [{sub_}]"
            Return New IsapiException(status, sub_, msg)
        End Function

        Public Async Function GetXmlAsync(path As String, ct As Threading.CancellationToken) As Task(Of XDocument)
            Dim data As Byte() = Await SendCoreAsync(HttpMethod.Get, path, Nothing, ct).ConfigureAwait(False)
            Return IsapiXml.Parse(Encoding.UTF8.GetString(data))
        End Function

        Public Function GetBytesAsync(path As String, ct As Threading.CancellationToken) As Task(Of Byte())
            Return SendCoreAsync(HttpMethod.Get, path, Nothing, ct)
        End Function

        ''' <summary>PUT مع جسم XML اختياري؛ يتحقق من ResponseStatus إن وُجد.</summary>
        Public Async Function PutAsync(path As String, body As XElement, ct As Threading.CancellationToken) As Task
            Dim data As Byte() = Await SendCoreAsync(HttpMethod.Put, path, body, ct).ConfigureAwait(False)
            CheckStatus(data)
        End Function

        Public Async Function PostAsync(path As String, body As XElement, ct As Threading.CancellationToken) As Task
            Dim data As Byte() = Await SendCoreAsync(HttpMethod.Post, path, body, ct).ConfigureAwait(False)
            CheckStatus(data)
        End Function

        Public Async Function DeleteAsync(path As String, ct As Threading.CancellationToken) As Task
            Dim data As Byte() = Await SendCoreAsync(HttpMethod.Delete, path, Nothing, ct).ConfigureAwait(False)
            CheckStatus(data)
        End Function

        Private Shared Sub CheckStatus(data As Byte())
            If data Is Nothing OrElse data.Length = 0 Then Return
            Dim doc As XDocument
            Try
                doc = IsapiXml.Parse(Encoding.UTF8.GetString(data))
            Catch ex As Xml.XmlException
                Return
            End Try
            If Not IsapiXml.IsOkStatus(doc) Then
                Throw New IsapiException(200, If(IsapiXml.ValueOf(doc, "subStatusCode"), ""),
                                         "رفضت الكاميرا الأمر: " & If(IsapiXml.ValueOf(doc, "statusString"), "غير معروف"))
            End If
        End Sub

        ''' <summary>يفتح بثاً طويلاً (alertStream). المسؤولية على المستدعي في إغلاقه.</summary>
        Public Async Function OpenStreamAsync(path As String, ct As Threading.CancellationToken) As Task(Of IO.Stream)
            ValidatePath(path)
            Dim resp As HttpResponseMessage = Await _http.GetAsync(path.TrimStart("/"c), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(False)
            If Not resp.IsSuccessStatusCode Then
                Dim st As Integer = CInt(resp.StatusCode)
                resp.Dispose()
                Throw BuildException(st, Array.Empty(Of Byte)())
            End If
            Return Await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(False)
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _http.Dispose()
        End Sub
    End Class

End Namespace
