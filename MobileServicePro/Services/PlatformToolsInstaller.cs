using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace MobileServicePro.Services;

/// <summary>
/// Загрузка официальных Android SDK Platform-Tools (adb, fastboot) с сервера Google
/// в %LOCALAPPDATA%\MobileServicePro\platform-tools.
///
/// Зачем так, а не вшивать adb в установщик: лицензия Android SDK ограничивает
/// распространение бинарников Google третьими лицами. Когда мастер сам скачивает
/// их с dl.google.com на свой ПК, мы их не распространяем.
/// </summary>
public static class PlatformToolsInstaller
{
    public const string DownloadUrl =
        "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";

    public static string InstallDirectory { get; } =
        Path.Combine(AppInfo.DataDirectory, "platform-tools");

    public static bool IsInstalled => File.Exists(Path.Combine(InstallDirectory, "adb.exe"));

    /// <param name="progress">Доля загрузки 0..1 (если сервер сообщил размер).</param>
    /// <returns>Папка, куда установлены инструменты.</returns>
    public static async Task<string> InstallAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(AppInfo.DataDirectory);
        var tempZip = Path.Combine(Path.GetTempPath(), $"msp-platform-tools-{Guid.NewGuid():N}.zip");
        var staging = Path.Combine(AppInfo.DataDirectory, $"platform-tools.new-{Guid.NewGuid():N}");

        try
        {
            await DownloadAsync(tempZip, progress, ct);

            // .NET проверяет пути внутри архива и не даёт распаковать файлы за пределы папки.
            ZipFile.ExtractToDirectory(tempZip, staging);

            var extracted = Path.Combine(staging, "platform-tools");
            if (!File.Exists(Path.Combine(extracted, "adb.exe")) ||
                !File.Exists(Path.Combine(extracted, "fastboot.exe")))
                throw new InvalidDataException(
                    "В скачанном архиве нет adb.exe/fastboot.exe — загрузка повреждена или Google изменил формат архива.");

            // Запущенный adb-сервер держит файл adb.exe — останавливаем его перед заменой.
            var oldAdb = Path.Combine(InstallDirectory, "adb.exe");
            if (File.Exists(oldAdb))
            {
                try { await ProcessRunner.RunAsync(oldAdb, ["kill-server"], 10, ct); }
                catch { /* сервер мог быть не запущен */ }
            }

            await DeleteDirectoryWithRetryAsync(InstallDirectory, ct);
            Directory.Move(extracted, InstallDirectory);
            return InstallDirectory;
        }
        finally
        {
            TryDeleteFile(tempZip);
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
        }
    }

    private static async Task DownloadAsync(string targetFile, IProgress<double>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MobileServicePro/{AppInfo.Version}");

        using var response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(targetFile);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            if (total is > 0) progress?.Report((double)received / total.Value);
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 5) { await Task.Delay(500 * attempt, ct); }
            catch (UnauthorizedAccessException) when (attempt < 5) { await Task.Delay(500 * attempt, ct); }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
