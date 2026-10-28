using Basira.Platform.Data;
using Basira.Platform.Infrastructure;
using static Basira.Platform.Api.ApiCommon;

namespace Basira.Platform.Api;

/// <summary>النسخ الاحتياطي والاستعادة (لمدير النظام).</summary>
public static class BackupApi
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/backup", (SettingsStore s, BackupService backup) =>
        {
            var last = s.GetRaw("backup_last").Split('|');
            var error = s.GetRaw("backup_error").Split('|', 2);
            return Results.Ok(new
            {
                folder = s.Get("backup_folder"), keep = s.Get("backup_keep"),
                last = last.Length == 3 ? new { at = last[0], path = last[1], size = long.TryParse(last[2], out var n) ? n : 0 } : null,
                error = error.Length == 2 ? new { at = error[0], message = error[1] } : null,
                pending = backup.Pending(), restored = BackupService.RestoredMessage,
            });
        });

        // تُبنى النسخة في ملف مؤقت (يُحذف بعد الإرسال) ثم تُرسل بطولها المعروف.
        admin.MapGet("/backup/download", async (HttpContext c, BackupService backup, PlatformOptions o) =>
        {
            var temp = Path.Combine(o.DataFolder, "download-" + Guid.NewGuid().ToString("N") + ".zip");
            var fs = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            try { await backup.WriteAsync(fs, U(c).UserName, c.RequestAborted); }
            catch { await fs.DisposeAsync(); throw; }
            fs.Position = 0;
            return Results.File(fs, "application/zip", $"{BackupService.Prefix}{DateTime.Now:yyyy-MM-dd_HHmmss}.zip");
        });

        admin.MapPost("/backup/run", async (HttpContext c, SettingsStore s, BackupService backup) =>
        {
            var folder = s.Get("backup_folder").Trim();
            if (folder == "") return Error("حدّد مجلد النسخ الاحتياطية واحفظه أولاً.");
            try { var path = await backup.CreateInFolderAsync(folder, s.GetInt("backup_keep", 14, 1, 365), U(c).UserName, c.RequestAborted); return Results.Ok(new { path }); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Error("تعذر الحفظ في المجلد: " + ex.Message); }
        });

        admin.MapPost("/backup/restore", async (HttpContext c, BackupService backup) =>
        {
            if (!c.Request.HasFormContentType) return Error("أرسل ملف النسخة.");
            var form = await c.Request.ReadFormAsync();
            if (form.Files.Count != 1) return Error("اختر ملف نسخة واحداً.");
            try
            {
                await using var s = form.Files[0].OpenReadStream();
                return Results.Ok(await backup.StageAsync(s, U(c).UserName, c.RequestAborted));
            }
            catch (InvalidDataException ex) { return Error(ex.Message); }
        }).DisableAntiforgery();

        admin.MapPost("/backup/restore/cancel", (HttpContext c, BackupService backup) => { backup.CancelPending(U(c).UserName); return Results.Ok(new { ok = true }); });
    }
}
