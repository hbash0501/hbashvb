using System.Diagnostics;
using System.Text;

namespace Basira.Platform.Processing;

/// <summary>تشغيل أداة خارجية (Tesseract، pdftoppm) مع قراءة متوازية للمخرجات ومهلة وإلغاء.</summary>
public static class ProcessRunner
{
    public static async Task<(int Exit, string Out, string Err)> RunAsync(string file, IEnumerable<string> args, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        // التوازي يأتي من معالجة عدة مستندات معاً؛ خيوط OpenMP داخل كل قراءة تتزاحم حينها وتبطئ Tesseract بشدة.
        psi.Environment["OMP_THREAD_LIMIT"] = "1";
        using var p = new Process { StartInfo = psi };
        p.Start();
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            throw new TimeoutException("انتهت مهلة الأداة الخارجية " + Path.GetFileName(file) + ".");
        }
        return (p.ExitCode, await outTask, await errTask);
    }

    /// <summary>يبحث عن الأداة: المسار المحدد في الإعدادات، ثم PATH، ثم مواقع التثبيت المعتادة على Windows.</summary>
    public static string? Locate(string configured, string exeName, params string[] windowsCandidates)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var names = OperatingSystem.IsWindows() ? new[] { exeName + ".exe" } : new[] { exeName };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var n in names)
            {
                var full = Path.Combine(dir.Trim('"'), n);
                if (File.Exists(full)) return full;
            }
        if (OperatingSystem.IsWindows())
            foreach (var c in windowsCandidates)
            {
                var expanded = Environment.ExpandEnvironmentVariables(c);
                if (File.Exists(expanded)) return expanded;
            }
        return null;
    }
}
