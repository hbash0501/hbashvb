using Basira.Platform.Intelligence;
using Basira.Platform.Intelligence.Semantic;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>النموذج الدلالي المحلي (لمدير النظام): حالته ومكانه، وتنزيله، وإعادة البحث عنه، ومعايرته وقياس أثره.</summary>
public static class SemanticApi
{
    public static object Status(SemanticService semantic, SemanticDownloader downloader, ClassificationService classifier)
    {
        var m = semantic.Model;
        var cal = classifier.Calibration;
        return new
        {
            loaded = m is not null,
            model = m is null ? null : new { name = m.Manifest.Name, file = Path.GetFileName(m.ModelFile), folder = m.Folder, dimension = m.Dimension, id = m.Id, prefix = m.Manifest.Prefix },
            candidates = semantic.Candidates().ToList(),
            downloadTarget = semantic.DownloadTarget(),
            error = semantic.Error,
            activity = classifier.SemanticActivity,
            preparationError = classifier.SemanticError,
            calibration = cal is null ? null : new { cal, current = m is not null && cal.ModelId == m.Id },
            download = new { running = downloader.Running, file = downloader.File, received = downloader.Received, total = downloader.Total, error = downloader.Error, completed = downloader.Completed, source = downloader.Source },
        };
    }

    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/semantic", (SemanticService semantic, SemanticDownloader downloader, ClassificationService classifier) =>
            Results.Ok(Status(semantic, downloader, classifier)));

        // يعيد البحث عن النموذج (بعد نسخه يدوياً بجوار المنصة أو تغيير مساره).
        admin.MapPost("/semantic/reload", (SemanticService semantic, SemanticDownloader downloader, ClassificationService classifier) =>
        {
            semantic.Reset();
            return Results.Ok(Status(semantic, downloader, classifier));
        });

        admin.MapPost("/semantic/download", (SemanticDownloader downloader) =>
            downloader.Start() ? Results.Ok(new { started = true }) : Error("التنزيل جارٍ بالفعل.", 409));

        admin.MapPost("/semantic/calibrate", (HttpContext c, ClassificationService classifier) =>
        {
            try { return Results.Ok(classifier.Calibrate(U(c).UserName)); }
            catch (InvalidOperationException ex) { return Error(ex.Message, 409); }
        });
    }
}
