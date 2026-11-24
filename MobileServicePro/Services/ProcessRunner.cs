using System.Diagnostics;
using System.IO;

namespace MobileServicePro.Services;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Ok => ExitCode == 0;
    public string Combined => string.IsNullOrWhiteSpace(StdErr) ? StdOut : $"{StdOut}\n{StdErr}";
}

/// <summary>
/// Запуск внешних инструментов (adb, fastboot) с тайм-аутом, без окна консоли.
/// Инструменты ищутся сначала рядом с программой (папка tools\platform-tools),
/// затем в PATH. Так программу можно поставлять вместе с официальными
/// Android SDK Platform-Tools, не требуя ручной настройки PATH у мастера.
/// </summary>
public static class ProcessRunner
{
    /// <summary>Полный путь к исполняемому файлу или имя для поиска в PATH.</summary>
    public static string Resolve(string executable)
    {
        var exeName = OperatingSystem.IsWindows() ? executable + ".exe" : executable;

        var baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "tools", "platform-tools", exeName),
            Path.Combine(baseDir, "platform-tools", exeName),
            Path.Combine(baseDir, exeName),
        ];
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return executable; // отдать на откуп PATH
    }

    public static async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        int timeoutSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo(Resolve(executable))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in args) info.ArgumentList.Add(a);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Не удалось запустить «{executable}».");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var outTask = process.StandardOutput.ReadToEndAsync(linked.Token);
        var errTask = process.StandardError.ReadToEndAsync(linked.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* уже завершён */ }
            throw new TimeoutException($"«{executable} {string.Join(' ', args)}» превысил {timeoutSeconds} с.");
        }

        return new ProcessResult(process.ExitCode, (await outTask).Trim(), (await errTask).Trim());
    }
}
