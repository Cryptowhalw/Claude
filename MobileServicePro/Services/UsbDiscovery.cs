using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MobileServicePro.Core;

namespace MobileServicePro.Services;

public static class UsbDiscovery
{
    // Read-only inventory. No driver installation, firmware writes, or elevated access.
    public static async Task<IReadOnlyList<UsbDevice>> DiscoverAsync()
    {
        const string script = "$ErrorActionPreference='Stop'; " +
            "Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like 'USB*' } | " +
            "Select-Object FriendlyName,InstanceId,Status | ConvertTo-Json -Compress -Depth 3";
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add(script);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("PowerShell не запустился.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new TimeoutException("Сканирование USB превысило 20 секунд."); }
        var output = await stdoutTask;
        var error = await stderrTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        if (string.IsNullOrWhiteSpace(output)) return [];
        using var doc = JsonDocument.Parse(output);
        var elements = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : [doc.RootElement];
        return elements.Select(e =>
        {
            string Read(string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? "" : "";
            var id = Read("InstanceId");
            var match = Regex.Match(id, @"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})");
            var hardwareId = match.Success ? $"{match.Groups[1].Value}:{match.Groups[2].Value}" : id;
            var name = Read("FriendlyName");
            var vendor = name.Contains("TECNO", StringComparison.OrdinalIgnoreCase) ? "TECNO" :
                         name.Contains("Apple", StringComparison.OrdinalIgnoreCase) ? "Apple" :
                         "Не определён";
            return new UsbDevice(string.IsNullOrWhiteSpace(name) ? "USB-устройство" : name,
                                 vendor, hardwareId, Read("Status"));
        }).OrderBy(d => d.Vendor).ThenBy(d => d.Name).ToArray();
    }
}
