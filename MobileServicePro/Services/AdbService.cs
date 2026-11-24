using System.Text.RegularExpressions;
using MobileServicePro.Core;

namespace MobileServicePro.Services;

/// <summary>
/// Диагностика Android через официальный adb (Android SDK Platform-Tools).
/// Выполняет ТОЛЬКО чтение свойств устройства (getprop, dumpsys battery).
/// Не изменяет аппарат, не снимает блокировки, не пишет разделы.
/// </summary>
public static partial class AdbService
{
    [GeneratedRegex(@"^\[(?<k>[^\]]+)\]:\s*\[(?<v>.*)\]$", RegexOptions.Multiline)]
    private static partial Regex GetPropLine();

    /// <summary>Запустить локальный adb-сервер (безвредно, если уже запущен).</summary>
    public static Task StartServerAsync(CancellationToken ct = default)
        => ProcessRunner.RunAsync("adb", ["start-server"], 15, ct);

    public static async Task<string> VersionAsync(CancellationToken ct = default)
    {
        var r = await ProcessRunner.RunAsync("adb", ["version"], 8, ct);
        return r.Ok ? r.StdOut.Split('\n')[0].Trim() : r.Combined;
    }

    /// <summary>
    /// Список подключённых устройств с полной диагностикой каждого,
    /// находящегося в состоянии «device».
    /// </summary>
    public static async Task<IReadOnlyList<AndroidDevice>> DiscoverAsync(CancellationToken ct = default)
    {
        await StartServerAsync(ct);
        var list = await ProcessRunner.RunAsync("adb", ["devices", "-l"], 15, ct);
        if (!list.Ok) throw new InvalidOperationException(list.Combined);

        var devices = new List<AndroidDevice>();
        foreach (var line in list.StdOut.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("List of devices")) continue;

            var parts = t.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var serial = parts[0];
            var state = parts[1];

            if (state != "device")
            {
                // unauthorized / offline / recovery / sideload — показываем, но не опрашиваем
                devices.Add(new AndroidDevice { Serial = serial, State = state });
                continue;
            }

            devices.Add(await InspectAsync(serial, ct));
        }
        return devices;
    }

    private static async Task<AndroidDevice> InspectAsync(string serial, CancellationToken ct)
    {
        var props = await ReadPropsAsync(serial, ct);
        string P(string key) => props.TryGetValue(key, out var v) ? v : "";

        int.TryParse(P("ro.build.version.sdk"), out var api);

        long ram = 0;
        var memResult = await ProcessRunner.RunAsync(
            "adb", ["-s", serial, "shell", "cat", "/proc/meminfo"], 8, ct);
        if (memResult.Ok)
        {
            var m = Regex.Match(memResult.StdOut, @"MemTotal:\s+(\d+)\s*kB");
            if (m.Success && long.TryParse(m.Groups[1].Value, out var kb)) ram = kb / 1024;
        }

        var (level, health, tempC) = await ReadBatteryAsync(serial, ct);

        var screen = "";
        var wm = await ProcessRunner.RunAsync("adb", ["-s", serial, "shell", "wm", "size"], 8, ct);
        if (wm.Ok)
        {
            var m = Regex.Match(wm.StdOut, @"(\d+x\d+)");
            if (m.Success) screen = m.Groups[1].Value;
        }

        var locked = P("ro.boot.flash.locked");
        var bootloaderState = locked switch
        {
            "1" => "заблокирован",
            "0" => "разблокирован",
            _ => P("ro.boot.verifiedbootstate") switch
            {
                "green" => "заблокирован (verified)",
                "orange" => "разблокирован",
                "yellow" => "разблокирован (custom key)",
                _ => "неизвестно"
            }
        };

        var oemAllowed = P("sys.oem_unlock_allowed") == "1";

        var su = await ProcessRunner.RunAsync(
            "adb", ["-s", serial, "shell", "which", "su"], 6, ct);
        var rooted = su.Ok && !string.IsNullOrWhiteSpace(su.StdOut) && !su.StdOut.Contains("not found");

        return new AndroidDevice
        {
            Serial = serial,
            State = "device",
            Model = P("ro.product.model"),
            Brand = P("ro.product.brand"),
            Manufacturer = P("ro.product.manufacturer"),
            Device = P("ro.product.device"),
            AndroidVersion = P("ro.build.version.release"),
            ApiLevel = api,
            SecurityPatch = P("ro.build.version.security_patch"),
            BuildId = P("ro.build.display.id"),
            Chipset = !string.IsNullOrWhiteSpace(P("ro.board.platform")) ? P("ro.board.platform") : P("ro.hardware"),
            Abi = P("ro.product.cpu.abi"),
            TotalRamMb = ram,
            ScreenResolution = screen,
            BatteryLevel = level,
            BatteryHealth = health,
            BatteryTempC = tempC,
            BootloaderState = bootloaderState,
            OemUnlockAllowed = oemAllowed,
            IsRooted = rooted,
        };
    }

    private static async Task<Dictionary<string, string>> ReadPropsAsync(string serial, CancellationToken ct)
    {
        var r = await ProcessRunner.RunAsync("adb", ["-s", serial, "shell", "getprop"], 15, ct);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!r.Ok) return dict;
        foreach (Match m in GetPropLine().Matches(r.StdOut))
            dict[m.Groups["k"].Value] = m.Groups["v"].Value;
        return dict;
    }

    private static async Task<(int level, string health, int tempC)> ReadBatteryAsync(string serial, CancellationToken ct)
    {
        var r = await ProcessRunner.RunAsync("adb", ["-s", serial, "shell", "dumpsys", "battery"], 8, ct);
        if (!r.Ok) return (-1, "", -1);

        int level = -1, temp = -1;
        var lvl = Regex.Match(r.StdOut, @"level:\s*(\d+)");
        if (lvl.Success) int.TryParse(lvl.Groups[1].Value, out level);

        var tmp = Regex.Match(r.StdOut, @"temperature:\s*(\d+)");
        if (tmp.Success && int.TryParse(tmp.Groups[1].Value, out var raw)) temp = raw / 10;

        var hl = Regex.Match(r.StdOut, @"health:\s*(\d+)");
        var health = hl.Success ? HealthText(hl.Groups[1].Value) : "";

        return (level, health, temp);
    }

    private static string HealthText(string code) => code switch
    {
        "1" => "неизвестно",
        "2" => "хорошее",
        "3" => "перегрев",
        "4" => "неисправна",
        "5" => "перенапряжение",
        "6" => "сбой",
        "7" => "холодная",
        _ => code
    };
}
