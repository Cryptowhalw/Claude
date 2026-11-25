using System.Windows;
using System.Windows.Threading;
using MobileServicePro.Services;

namespace MobileServicePro;

public partial class App : Application
{
    /// <summary>То же имя указано в установщике (AppMutex): он попросит закрыть программу перед обновлением.</summary>
    private const string InstanceMutexName = "MobileServicePro_SingleInstance";

    private static Mutex? _instanceMutex;
    private static bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Два экземпляра одновременно мешали бы друг другу работать с adb.
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show($"{AppInfo.Name} уже запущена.", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Write($"КРИТИЧЕСКАЯ ОШИБКА: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Write($"Необработанная ошибка фоновой задачи: {args.Exception}");
            args.SetObserved();
        };

        AppLog.Cleanup(TimeSpan.FromDays(30));
        AppLog.Write($"=== Запуск {AppInfo.Name} {AppInfo.Version}, Windows {Environment.OSVersion.Version} ===");

        new MainWindow().Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Write($"Ошибка интерфейса: {e.Exception}");
        MessageBox.Show(
            $"Произошла непредвиденная ошибка:\n{e.Exception.Message}\n\n" +
            $"Подробности записаны в журнал:\n{AppLog.LogDirectory}",
            AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // программа продолжает работать
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
