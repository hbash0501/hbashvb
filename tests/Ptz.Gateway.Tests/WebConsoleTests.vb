Imports System.Net
Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports Xunit

Public Class WebConsoleTests

    Private Shared Function Get_(h As Harness, path As String) As HttpResponseMessage
        Return h.Send("GET", path)
    End Function

    <Fact>
    Public Sub Console_IsServedAnonymously_WithAStrictCsp_AndNoCaching()
        Using h As New Harness()
            Dim page As HttpResponseMessage = Get_(h, "/")
            Assert.Equal(HttpStatusCode.OK, page.StatusCode)
            Assert.StartsWith("text/html", page.Content.Headers.ContentType.ToString())
            Dim csp As String = String.Join(";", page.Headers.GetValues("Content-Security-Policy"))
            Assert.Contains("default-src 'none'", csp)
            Assert.Contains("script-src 'self'", csp)
            Assert.DoesNotContain("unsafe-inline", csp)
            Assert.DoesNotContain("unsafe-eval", csp)
            Assert.Contains("frame-ancestors 'none'", csp)
            Assert.Equal("no-store", page.Headers.CacheControl.ToString())
            Assert.Equal("nosniff", page.Headers.GetValues("X-Content-Type-Options").Single())
            Assert.Equal(HttpStatusCode.OK, Get_(h, "/app.js").StatusCode)
            Assert.StartsWith("application/javascript", Get_(h, "/app.js").Content.Headers.ContentType.ToString())
            Assert.StartsWith("text/css", Get_(h, "/app.css").Content.Headers.ContentType.ToString())
        End Using
    End Sub

    <Fact>
    Public Sub Console_OnlyExposesItsThreeFiles()
        Using h As New Harness()
            For Each p As String In {"/index.html", "/app.js.map", "/web/app.js", "/favicon.ico", "/Web/index.html"}
                Dim code As HttpStatusCode = Get_(h, p).StatusCode
                Assert.True(code = HttpStatusCode.NotFound OrElse code = HttpStatusCode.BadRequest OrElse code = HttpStatusCode.MethodNotAllowed, p & " => " & CInt(code))
            Next
            Assert.NotEqual(HttpStatusCode.OK, h.Send("POST", "/").StatusCode)
        End Using
    End Sub

    <Fact>
    Public Sub Console_PageHasNoInlineScriptStyleOrHandlers_AndLoadsOnlyItsOwnAssets()
        Using h As New Harness()
            Dim html As String = Harness.Body(Get_(h, "/"))
            Assert.Contains("lang=""ar""", html)
            Assert.Contains("dir=""rtl""", html)
            For Each m As Match In Regex.Matches(html, "<script\b[^>]*>", RegexOptions.IgnoreCase)
                Assert.Contains("src=", m.Value)
            Next
            Assert.DoesNotMatch(New Regex("<script\b[^>]*>\s*[^<\s]", RegexOptions.IgnoreCase), html)       ' لا شيفرة داخل الوسم
            Assert.DoesNotMatch(New Regex("\sstyle\s*=", RegexOptions.IgnoreCase), html)
            Assert.DoesNotMatch(New Regex("<style\b", RegexOptions.IgnoreCase), html)
            Assert.DoesNotMatch(New Regex("\son[a-z]+\s*=", RegexOptions.IgnoreCase), html)
            Assert.DoesNotMatch(New Regex("javascript:", RegexOptions.IgnoreCase), html)
            For Each m As Match In Regex.Matches(html, "(?:src|href)\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)
                Dim u As String = m.Groups(1).Value
                Assert.True(u.StartsWith("/", StringComparison.Ordinal) AndAlso Not u.StartsWith("//", StringComparison.Ordinal), "مورد خارجي: " & u)
            Next
        End Using
    End Sub

    <Fact>
    Public Sub Console_Script_AvoidsDangerousSinks_AndNeverPersistsTheToken()
        Using h As New Harness()
            Dim js As String = Harness.Body(Get_(h, "/app.js"))
            For Each sink As String In {".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write", "eval(", "new Function", "setTimeout(""", "setTimeout('", "setInterval(""", "setInterval('", "location.hash", "postMessage("}
                Assert.False(js.Contains(sink), "منفذ خطر في app.js: " & sink)
            Next
            Assert.DoesNotMatch(New Regex("(localStorage|sessionStorage)\.setItem\([^)]*token", RegexOptions.IgnoreCase), js)
            Assert.DoesNotMatch(New Regex("document\.cookie", RegexOptions.IgnoreCase), js)
            Assert.DoesNotMatch(New Regex("[?&](access_)?token=", RegexOptions.IgnoreCase), js)             ' لا رمز في الروابط
            Assert.Contains("Authorization", js)
        End Using
    End Sub

    <Fact>
    Public Sub Console_TalksToTheApisThatExist()
        ' كل نداء api('METHOD','/path') أو cp(c,'/suffix') في السكربت يجب أن يطابق مساراً مسجَّلاً فعلاً.
        Using h As New Harness()
            Dim js As String = Harness.Body(Get_(h, "/app.js"))
            Dim routes = h.Registry.Routes.Where(Function(r) r.Pattern.StartsWith("/api/v1", StringComparison.Ordinal)).Select(Function(r) (r.Method, New Regex("^/api/v1" & Regex.Replace(Regex.Escape(r.Pattern.Substring("/api/v1".Length)), "\\\{[a-z]+\}", "[^/]+") & "$"))).ToList()
            Dim checked As Integer = 0
            For Each m As Match In Regex.Matches(js, "api\('(GET|POST|PUT|DELETE)',\s*'(/[A-Za-z0-9_/\-]*)(\?[^']*)?'")
                Dim path As String = "/api/v1" & m.Groups(2).Value
                If path.EndsWith("/", StringComparison.Ordinal) Then Continue For
                Assert.True(routes.Any(Function(r) r.Method = m.Groups(1).Value AndAlso r.Item2.IsMatch(path)), "نداء غير مسجَّل: " & m.Groups(1).Value & " " & path)
                checked += 1
            Next
            For Each m As Match In Regex.Matches(js, "\bcp\(\w+,\s*'(/[A-Za-z0-9_/\-]*)(\?[^']*)?'")
                Dim path As String = "/api/v1/cameras/x" & m.Groups(1).Value
                If path.EndsWith("/", StringComparison.Ordinal) Then Continue For          ' يُكمل ديناميكياً (مثل /lens/ + kind)
                Assert.True(routes.Any(Function(r) r.Item2.IsMatch(path)), "مسار كاميرا غير مسجَّل: " & path)
                checked += 1
            Next
            Assert.True(checked >= 25, "عدد النداءات المفحوصة قليل جداً: " & checked)
        End Using
    End Sub
End Class
