# tools\platform-tools

Сюда **по желанию** кладутся официальные Android SDK Platform-Tools
(`adb.exe`, `fastboot.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll` и т.д.).

Всё, что лежит в этой папке, при сборке копируется рядом с `MobileServicePro.exe`
и попадает в установщик. Содержимое папки в git не хранится (см. `.gitignore`).

Как заполнить автоматически:
```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -BundlePlatformTools
```

**Обычно эта папка не нужна:** программа сама предлагает мастеру скачать
Platform-Tools с сервера Google (кнопка «Скачать Platform-Tools»).
Почему по умолчанию не вшиваем — см. `BUILD.md`, раздел «Лицензия Platform-Tools».
