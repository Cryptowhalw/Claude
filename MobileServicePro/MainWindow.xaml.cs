using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using MobileServicePro.Core;
using MobileServicePro.Services;

namespace MobileServicePro;

public partial class MainWindow : Window
{
    private IReadOnlyList<AndroidDevice> _lastAndroid = [];
    private IReadOnlyList<FastbootDevice> _lastFastboot = [];

    public MainWindow()
    {
        InitializeComponent();
        Title = $"{AppInfo.Name} — {AppInfo.Version}";
        SubtitleText.Text = $"Сервисная диагностика Android • ADB / Fastboot • версия {AppInfo.Version}";

        Log("Программа запущена. Подключите телефон и выберите действие.");
        Log("ADB-диагностика требует включённой «Отладки по USB» и подтверждения на телефоне.");
        if (ProcessRunner.Resolve("adb") == "adb" && !PlatformToolsInstaller.IsInstalled)
            Log("adb рядом с программой не найден. Если его нет и в PATH — нажмите «Скачать Platform-Tools».");
    }

    private void Log(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
        AppLog.Write(message);
    }

    private async void RefreshUsb_Click(object sender, RoutedEventArgs e)
    {
        RefreshUsbButton.IsEnabled = false;
        Log("Сканирование USB-устройств...");
        try
        {
            var devices = await UsbDiscovery.DiscoverAsync();
            DeviceGrid.ItemsSource = devices;
            Log($"Найдено USB-устройств: {devices.Count}.");
        }
        catch (Exception ex) { Log($"Ошибка USB: {ex.Message}"); }
        finally { RefreshUsbButton.IsEnabled = true; }
    }

    private async void Android_Click(object sender, RoutedEventArgs e)
    {
        AndroidButton.IsEnabled = false;
        Log("Опрос устройств по ADB (getprop, battery)...");
        try
        {
            _lastAndroid = await AdbService.DiscoverAsync();
            AndroidGrid.ItemsSource = _lastAndroid;
            if (_lastAndroid.Count == 0)
                Log("Устройств в ADB не найдено. Проверьте «Отладку по USB» и драйвер.");
            foreach (var d in _lastAndroid)
            {
                if (d.State == "unauthorized")
                    Log($"{d.Serial}: не авторизовано — подтвердите отладку на экране телефона.");
                else if (d.State != "device")
                    Log($"{d.Serial}: состояние «{d.State}».");
                else
                    Log($"{d.Title}: Android {d.AndroidVersion}, {d.Chipset}, батарея {d.BatteryLevel}%, bootloader — {d.BootloaderState}.");
            }
        }
        catch (Exception ex) { Log($"Ошибка ADB: {ex.Message}"); }
        finally { AndroidButton.IsEnabled = true; }
    }

    private async void Fastboot_Click(object sender, RoutedEventArgs e)
    {
        FastbootButton.IsEnabled = false;
        Log("Опрос устройств в режиме fastboot (getvar all)...");
        try
        {
            _lastFastboot = await FastbootService.DiscoverAsync();
            FastbootGrid.ItemsSource = _lastFastboot;
            if (_lastFastboot.Count == 0)
                Log("Устройств в fastboot не найдено. Переведите аппарат в режим bootloader.");
            foreach (var d in _lastFastboot)
                Log($"{d.Title}: {d.UnlockedState}, OEM-разблокировка {(d.UnlockAbility ? "разрешена" : "запрещена")}, слот {d.CurrentSlot}.");
        }
        catch (Exception ex) { Log($"Ошибка fastboot: {ex.Message}"); }
        finally { FastbootButton.IsEnabled = true; }
    }

    private async void CheckTools_Click(object sender, RoutedEventArgs e)
    {
        Log(await ToolChecker.CheckAdbAsync());
        Log(await ToolChecker.CheckFastbootAsync());
        Log("Если инструменты не найдены — нажмите «Скачать Platform-Tools» (официальный архив Google).");
    }

    private async void PlatformTools_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Будут загружены официальные Android SDK Platform-Tools (adb, fastboot) " +
            "с сервера Google (dl.google.com), около 8–10 МБ.\n\n" +
            "Используя их, вы принимаете условия лицензии Android SDK от Google.\n\n" +
            $"Папка установки:\n{PlatformToolsInstaller.InstallDirectory}\n\nПродолжить?",
            "Скачать Platform-Tools", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        PlatformToolsButton.IsEnabled = false;
        Log("Загрузка Platform-Tools с dl.google.com...");
        var lastStep = -1;
        var progress = new Progress<double>(p =>
        {
            var step = (int)(p * 10);
            if (step == lastStep) return;
            lastStep = step;
            Log($"  загружено {step * 10}%");
        });
        try
        {
            var dir = await PlatformToolsInstaller.InstallAsync(progress);
            Log($"Platform-Tools установлены: {dir}");
            Log(await ToolChecker.CheckAdbAsync());
            Log(await ToolChecker.CheckFastbootAsync());
        }
        catch (Exception ex) { Log($"Ошибка загрузки Platform-Tools: {ex.Message}"); }
        finally { PlatformToolsButton.IsEnabled = true; }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppLog.LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppLog.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) { Log($"Не удалось открыть папку логов: {ex.Message}"); }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_lastAndroid.Count == 0 && _lastFastboot.Count == 0)
        {
            Log("Нет данных для экспорта. Сначала выполните диагностику.");
            return;
        }
        var dlg = new SaveFileDialog
        {
            FileName = $"service-report-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Filter = "JSON отчёт (*.json)|*.json|Текстовый отчёт (*.txt)|*.txt"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(dlg.FileName, BuildTextReport(), Encoding.UTF8);
            else
                File.WriteAllText(dlg.FileName,
                    JsonSerializer.Serialize(new { generated = DateTime.Now, android = _lastAndroid, fastboot = _lastFastboot },
                        new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            Log($"Отчёт сохранён: {dlg.FileName}");
        }
        catch (Exception ex) { Log($"Ошибка экспорта: {ex.Message}"); }
    }

    private string BuildTextReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mobile Service Pro — отчёт диагностики");
        sb.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine(new string('-', 50));
        foreach (var d in _lastAndroid)
        {
            sb.AppendLine($"Устройство: {d.Title}");
            sb.AppendLine($"  Serial: {d.Serial}  Состояние: {d.State}");
            sb.AppendLine($"  Android {d.AndroidVersion} (API {d.ApiLevel}), патч {d.SecurityPatch}");
            sb.AppendLine($"  Чипсет: {d.Chipset}, ABI: {d.Abi}, ОЗУ: {d.TotalRamMb} МБ, экран: {d.ScreenResolution}");
            sb.AppendLine($"  Батарея: {d.BatteryLevel}% / {d.BatteryHealth} / {d.BatteryTempC}°C");
            sb.AppendLine($"  Bootloader: {d.BootloaderState}, OEM unlock: {(d.OemUnlockAllowed ? "разрешён" : "нет")}, root: {(d.IsRooted ? "да" : "нет")}");
            sb.AppendLine();
        }
        foreach (var d in _lastFastboot)
        {
            sb.AppendLine($"Fastboot: {d.Title}  Serial: {d.Serial}");
            sb.AppendLine($"  {d.UnlockedState}, OEM unlock ability: {d.UnlockAbility}, слот: {d.CurrentSlot}");
            sb.AppendLine($"  Bootloader: {d.BootloaderVersion}, Baseband: {d.BasebandVersion}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private void Apple_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Для официального обновления и восстановления iPhone на Windows используйте Apple Devices.\n\n" +
            "Восстановление может удалить данные и НЕ снимает блокировку активации Apple ID.\n" +
            "Автоматическое управление прошивкой iPhone в этой версии не реализовано.",
            "Apple Service", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
