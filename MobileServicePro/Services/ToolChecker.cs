namespace MobileServicePro.Services;

/// <summary>Проверка доступности adb и fastboot (рядом с программой или в PATH).</summary>
public static class ToolChecker
{
    public static async Task<string> CheckAdbAsync()
    {
        try
        {
            var v = await AdbService.VersionAsync();
            var path = ProcessRunner.Resolve("adb");
            return $"adb: доступен — {v}  [{path}]";
        }
        catch (Exception ex) { return $"adb: не найден ({ex.Message})"; }
    }

    public static async Task<string> CheckFastbootAsync()
    {
        try
        {
            var v = await FastbootService.VersionAsync();
            var path = ProcessRunner.Resolve("fastboot");
            return $"fastboot: доступен — {v}  [{path}]";
        }
        catch (Exception ex) { return $"fastboot: не найден ({ex.Message})"; }
    }
}
