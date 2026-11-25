using System.IO;
using System.Text;

namespace MobileServicePro.Services;

/// <summary>
/// Журнал в файл: %LOCALAPPDATA%\MobileServicePro\logs\msp-ГГГГ-ММ-ДД.log.
/// Нужен для поддержки: мастер присылает файл — видно, что происходило и где упало.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();

    public static string LogDirectory { get; } = Path.Combine(AppInfo.DataDirectory, "logs");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    Path.Combine(LogDirectory, $"msp-{DateTime.Now:yyyy-MM-dd}.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Журнал никогда не должен ронять программу (диск полон, нет прав и т.п.)
        }
    }

    /// <summary>Удалить логи старше заданного срока.</summary>
    public static void Cleanup(TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(LogDirectory)) return;
            var border = DateTime.Now - maxAge;
            foreach (var file in Directory.EnumerateFiles(LogDirectory, "msp-*.log"))
                if (File.GetLastWriteTime(file) < border)
                    File.Delete(file);
        }
        catch
        {
            // не критично
        }
    }
}
