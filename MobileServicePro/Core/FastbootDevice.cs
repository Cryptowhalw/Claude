namespace MobileServicePro.Core;

/// <summary>
/// Данные устройства в режиме bootloader/fastboot (fastboot getvar all).
/// Только чтение переменных. Никаких записей/прошивки здесь не выполняется.
/// </summary>
public sealed class FastbootDevice
{
    public string Serial { get; init; } = "";
    public string Product { get; init; } = "";
    public string Variant { get; init; } = "";
    public string UnlockedState { get; init; } = "";   // yes / no / неизвестно
    public bool UnlockAbility { get; init; }            // можно ли вообще разблокировать (OEM unlocking в настройках)
    public string Secure { get; init; } = "";
    public string BootloaderVersion { get; init; } = "";
    public string BasebandVersion { get; init; } = "";
    public string CurrentSlot { get; init; } = "";      // A/B слоты
    public string Partitions { get; init; } = "";       // краткая сводка
    public IReadOnlyDictionary<string, string> Raw { get; init; } = new Dictionary<string, string>();

    public string Title => string.IsNullOrWhiteSpace(Product)
        ? (string.IsNullOrWhiteSpace(Serial) ? "Fastboot-устройство" : Serial)
        : Product;
}
