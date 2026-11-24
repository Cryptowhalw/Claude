namespace MobileServicePro.Core;

/// <summary>
/// Результат диагностики Android-устройства через ADB.
/// Все поля — только чтение свойств устройства, без изменения аппарата.
/// </summary>
public sealed class AndroidDevice
{
    public string Serial { get; init; } = "";
    public string State { get; init; } = "";          // device / unauthorized / offline / recovery / sideload
    public string Model { get; init; } = "";          // ro.product.model
    public string Brand { get; init; } = "";          // ro.product.brand
    public string Manufacturer { get; init; } = "";   // ro.product.manufacturer
    public string Device { get; init; } = "";         // ro.product.device (кодовое имя)
    public string AndroidVersion { get; init; } = ""; // ro.build.version.release
    public int ApiLevel { get; init; }                // ro.build.version.sdk
    public string SecurityPatch { get; init; } = "";  // ro.build.version.security_patch
    public string BuildId { get; init; } = "";        // ro.build.display.id
    public string Chipset { get; init; } = "";        // ro.board.platform / ro.hardware
    public string Abi { get; init; } = "";            // ro.product.cpu.abi
    public long TotalRamMb { get; init; }
    public string ScreenResolution { get; init; } = "";
    public int BatteryLevel { get; init; } = -1;      // %
    public string BatteryHealth { get; init; } = "";
    public int BatteryTempC { get; init; } = -1;      // десятые градуса преобразованы в целые
    public string BootloaderState { get; init; } = "";// locked / unlocked / неизвестно (ro.boot.flash.locked)
    public bool OemUnlockAllowed { get; init; }       // sys.oem_unlock_allowed
    public bool IsRooted { get; init; }               // наличие su (эвристика)

    public string Title => string.IsNullOrWhiteSpace(Model)
        ? (string.IsNullOrWhiteSpace(Serial) ? "Android-устройство" : Serial)
        : $"{Brand} {Model}".Trim();
}
