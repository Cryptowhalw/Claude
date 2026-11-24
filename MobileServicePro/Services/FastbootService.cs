using System.Text.RegularExpressions;
using MobileServicePro.Core;

namespace MobileServicePro.Services;

/// <summary>
/// Чтение состояния устройства в режиме bootloader через официальный fastboot.
/// Выполняет ТОЛЬКО «fastboot getvar all» (чтение переменных).
/// Запись, прошивка и разблокировка здесь НЕ реализованы.
/// </summary>
public static class FastbootService
{
    public static async Task<string> VersionAsync(CancellationToken ct = default)
    {
        var r = await ProcessRunner.RunAsync("fastboot", ["--version"], 8, ct);
        return r.Ok ? r.StdOut.Split('\n')[0].Trim() : r.Combined;
    }

    public static async Task<IReadOnlyList<FastbootDevice>> DiscoverAsync(CancellationToken ct = default)
    {
        var list = await ProcessRunner.RunAsync("fastboot", ["devices"], 10, ct);
        // fastboot часто пишет список в stderr — используем оба потока
        var text = list.Combined;

        var serials = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && (l.Contains("fastboot", StringComparison.OrdinalIgnoreCase)))
            .Select(l => l.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0])
            .Distinct()
            .ToArray();

        var result = new List<FastbootDevice>();
        foreach (var serial in serials)
            result.Add(await GetVarsAsync(serial, ct));
        return result;
    }

    private static async Task<FastbootDevice> GetVarsAsync(string serial, CancellationToken ct)
    {
        var r = await ProcessRunner.RunAsync("fastboot", ["-s", serial, "getvar", "all"], 20, ct);
        // getvar пишет строки вида "(bootloader) unlocked: yes" в stderr
        var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(r.Combined, @"\(bootloader\)\s*(?<k>[^:]+):\s*(?<v>.*)"))
            raw[m.Groups["k"].Value.Trim()] = m.Groups["v"].Value.Trim();

        string V(params string[] keys)
        {
            foreach (var k in keys)
                if (raw.TryGetValue(k, out var v)) return v;
            return "";
        }

        var unlocked = V("unlocked");
        var unlockability = V("unlock_ability");

        return new FastbootDevice
        {
            Serial = serial,
            Product = V("product"),
            Variant = V("variant"),
            UnlockedState = unlocked switch { "yes" => "разблокирован", "no" => "заблокирован", _ => "неизвестно" },
            UnlockAbility = unlockability == "1" || unlockability.Equals("yes", StringComparison.OrdinalIgnoreCase),
            Secure = V("secure"),
            BootloaderVersion = V("version-bootloader", "bootloader-version"),
            BasebandVersion = V("version-baseband", "baseband-version"),
            CurrentSlot = V("current-slot"),
            Partitions = string.Join(", ", raw.Keys.Where(k => k.StartsWith("partition-type:", StringComparison.OrdinalIgnoreCase)).Take(8)),
            Raw = raw,
        };
    }
}
