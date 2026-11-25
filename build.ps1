<#
.SYNOPSIS
    Сборка Mobile Service Pro: готовый .exe и установщик.

.DESCRIPTION
    1) проверяет .NET SDK;
    2) собирает автономный однофайловый MobileServicePro.exe (не требует .NET у мастера);
    3) собирает установщик через Inno Setup (если он установлен).

    Результат:
      artifacts\publish\MobileServicePro.exe                      — переносная версия
      artifacts\installer\MobileServicePro-Setup-<версия>.exe     — установщик

.PARAMETER SkipInstaller
    Только .exe, без установщика.

.PARAMETER BundlePlatformTools
    Скачать Android SDK Platform-Tools и вшить их в сборку/установщик.
    По умолчанию НЕ вшиваются (см. BUILD.md, раздел про лицензию): программа
    сама предлагает скачать их у мастера на ПК.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipInstaller,
    [switch]$BundlePlatformTools
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # иначе Invoke-WebRequest в PowerShell 5.1 качает в разы медленнее
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root         = $PSScriptRoot
$project      = Join-Path $root 'MobileServicePro\MobileServicePro.csproj'
$publishDir   = Join-Path $root 'artifacts\publish'
$installerDir = Join-Path $root 'artifacts\installer'
$issFile      = Join-Path $root 'installer\MobileServicePro.iss'

function Step([string]$text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# 1. .NET SDK ---------------------------------------------------------------
Step 'Проверка .NET SDK'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK не найден. Установите .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
}
$sdk = (& dotnet --version).Trim()
Write-Host "dotnet $sdk"
if ([int]($sdk.Split('.')[0]) -lt 10) { throw "Нужен .NET SDK 10 или новее, найден $sdk." }

# 2. Версия из .csproj ------------------------------------------------------
[xml]$xml = Get-Content $project -Raw -Encoding UTF8
$version = @($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $version) { throw 'В MobileServicePro.csproj не найден <Version>.' }
Write-Host "Версия: $version"

# 3. Platform-Tools (только по явному запросу) ------------------------------
if ($BundlePlatformTools) {
    Step 'Загрузка Android SDK Platform-Tools для включения в сборку'
    Write-Warning 'Вы распространяете бинарники Google вместе со своей программой. Прочитайте раздел про лицензию в BUILD.md.'
    $toolsDir  = Join-Path $root 'MobileServicePro\tools'
    $targetDir = Join-Path $toolsDir 'platform-tools'
    $oldAdb    = Join-Path $targetDir 'adb.exe'
    if (Test-Path $oldAdb) { & $oldAdb kill-server 2>$null | Out-Null }
    Get-ChildItem $targetDir -Exclude 'README.md' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

    $zip = Join-Path ([IO.Path]::GetTempPath()) 'msp-platform-tools-latest-windows.zip'
    Invoke-WebRequest 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' -OutFile $zip -UseBasicParsing
    Expand-Archive $zip -DestinationPath $toolsDir -Force
    Remove-Item $zip -Force
    if (-not (Test-Path (Join-Path $targetDir 'adb.exe'))) { throw 'После распаковки adb.exe не найден.' }
    Write-Host "Platform-Tools: $targetDir"
}

# 4. Сборка .exe ------------------------------------------------------------
Step 'Сборка MobileServicePro.exe (dotnet publish)'
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish завершился с ошибкой (код $LASTEXITCODE)." }

$exe = Join-Path $publishDir 'MobileServicePro.exe'
if (-not (Test-Path $exe)) { throw "Не найден $exe" }
Write-Host ('Готово: {0} ({1:N1} МБ)' -f $exe, ((Get-Item $exe).Length / 1MB)) -ForegroundColor Green

# 5. Установщик -------------------------------------------------------------
if ($SkipInstaller) {
    Write-Host 'Установщик пропущен (-SkipInstaller).'
    return
}

Step 'Сборка установщика (Inno Setup)'
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup*\ISCC.exe",
        "$env:ProgramFiles\Inno Setup*\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup*\ISCC.exe"
    ) | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $iscc) {
    Write-Warning ('Inno Setup не найден — установщик не собран. ' +
        'Установите Inno Setup 6.3+ (https://jrsoftware.org/isdl.php или команда: winget install JRSoftware.InnoSetup) ' +
        'и запустите скрипт снова. Переносной .exe уже готов: ' + $exe)
    return
}

& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" "/O$installerDir" $issFile
if ($LASTEXITCODE -ne 0) { throw "Inno Setup завершился с ошибкой (код $LASTEXITCODE)." }

$setup = Join-Path $installerDir "MobileServicePro-Setup-$version.exe"
Write-Host "`nУстановщик: $setup" -ForegroundColor Green
