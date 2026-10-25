using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using Basira.Platform.Processing;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

public static class InboxApi
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapPost("/inbox", async (HttpContext c, InboxService inbox, PlatformOptions o) =>
        {
            var u = U(c);
            if (!UserStore.Can(u, "import")) return Forbidden("الإدراج");
            if (!c.Request.HasFormContentType) return Error("أرسل الملفات كنموذج متعدد الأجزاء.");
            var form = await c.Request.ReadFormAsync();
            if (form.Files.Count == 0) return Error("اختر ملفاً واحداً على الأقل.");
            var options = new ImportOptions(form["direction"].ToString() is { Length: > 0 } dir ? dir : "وارد", form["category"].ToString(),
                form["confidentiality"].ToString() is { Length: > 0 } conf ? conf : "عادي", form.Files.Count == 1 ? form["title"].ToString() : "");
            var results = new List<object>();
            foreach (var f in form.Files)
            {
                if (f.Length > o.MaxUploadBytes) { results.Add(new { name = f.FileName, error = "الملف أكبر من الحد المسموح." }); continue; }
                try
                {
                    await using var s = f.OpenReadStream();
                    var r = await inbox.ImportAsync(s, f.FileName, options, u.UserName, c.RequestAborted);
                    results.Add(new { name = f.FileName, id = r.Document.Id, archiveNo = r.Document.ArchiveNo, duplicate = r.Duplicate, title = r.Document.Title });
                }
                catch (ArgumentException ex) { results.Add(new { name = f.FileName, error = ex.Message }); }
            }
            return Results.Ok(results);
        }).DisableAntiforgery();

        g.MapGet("/jobs", (ProcessingQueue q, int? take) => Results.Ok(q.Recent(Math.Clamp(take ?? 30, 1, 200))));

        g.MapGet("/events", async (HttpContext c, EventHub hub) =>
        {
            c.Response.Headers.ContentType = "text/event-stream";
            c.Response.Headers.CacheControl = "no-store";
            c.Response.Headers["X-Accel-Buffering"] = "no";
            var (id, reader) = hub.Subscribe();
            try
            {
                await c.Response.WriteAsync(": connected\n\n", c.RequestAborted);
                await c.Response.Body.FlushAsync(c.RequestAborted);
                while (!c.RequestAborted.IsCancellationRequested)
                {
                    using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted);
                    heartbeat.CancelAfter(TimeSpan.FromSeconds(20));
                    try
                    {
                        var msg = await reader.ReadAsync(heartbeat.Token);
                        await c.Response.WriteAsync("data: " + msg + "\n\n", c.RequestAborted);
                    }
                    catch (OperationCanceledException) when (!c.RequestAborted.IsCancellationRequested)
                    {
                        await c.Response.WriteAsync(": ping\n\n", c.RequestAborted);
                    }
                    await c.Response.Body.FlushAsync(c.RequestAborted);
                }
            }
            catch (OperationCanceledException) { }
            catch (System.Threading.Channels.ChannelClosedException) { }
            finally { hub.Unsubscribe(id); }
        });
    }
}
