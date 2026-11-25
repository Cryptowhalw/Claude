using System.IO;
using System.Reflection;

namespace MobileServicePro.Services;

/// <summary>Версия программы и её рабочие папки.</summary>
public static class AppInfo
{
    public const string Name = "Mobile Service Pro";

    /// <summary>Версия из .csproj (&lt;Version&gt;), например «0.2.0».</summary>
    public static string Version { get; } =
        (typeof(AppInfo).Assembly
             .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? typeof(AppInfo).Assembly.GetName().Version?.ToString()
         ?? "0.0.0")
        .Split('+')[0];

    /// <summary>%LOCALAPPDATA%\MobileServicePro — сюда пишем логи и скачанные Platform-Tools.
    /// Папка установки (Program Files) может быть недоступна для записи без прав администратора.</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MobileServicePro");
}
