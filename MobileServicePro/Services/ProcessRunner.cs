using System.ComponentModel;
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
/// Порядок поиска:
///   1) tools\platform-tools рядом с программой (если положены при сборке);
///   2) %LOCALAPPDATA%\MobileServicePro\platform-tools (скачаны кнопкой в программе);
///   3) PATH.
/// </summary>
public static class ProcessRunner
{
    /// <summary>Полный путь к исполняемому файлу или имя для поиска в PATH.</summary>
    public static string Resolve(string executable)
    {
        if (Path.IsPathRooted(executable)) return executable;

        var exeName = OperatingSystem.IsWindows() ? executable + ".exe" : executable;
        var baseDir = AppContext.BaseDirectory;

        string[] candidates =
        [
            Path.Combine(baseDir, "tools", "platform-tools", exeName),
            Path.Combine(baseDir, "platform-tools", exeName),
            Path.Combine(PlatformToolsInstaller.InstallDirectory, exeName),
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
        var argList = args.ToArray();
        var info = new ProcessStartInfo(Resolve(executable))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in argList) info.ArgumentList.Add(a);

        Process? started;
        try
        {
            started = Process.Start(info);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2) // ERROR_FILE_NOT_FOUND
        {
            throw new FileNotFoundException(
                $"«{Path.GetFileName(executable)}» не найден. Нажмите «Скачать Platform-Tools» " +
                "или положите adb/fastboot в папку tools\\platform-tools рядом с программой.",
                executable, ex);
        }

        using var process = started
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
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException(
                $"«{Path.GetFileName(executable)} {string.Join(' ', argList)}» не ответил за {timeoutSeconds} с.");
        }

        return new ProcessResult(process.ExitCode, (await outTask).Trim(), (await errTask).Trim());
    }
}
