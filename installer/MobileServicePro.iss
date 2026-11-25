; Mobile Service Pro - Inno Setup installer script
;
; Normally built by build.ps1 in the repository root (it publishes the app first).
; Manual build:  ISCC.exe /DAppVersion=0.3.0 installer\MobileServicePro.iss
; Requires Inno Setup 6.3 or newer: https://jrsoftware.org/isinfo.php

#define AppName      "Mobile Service Pro"
#define AppExeName   "MobileServicePro.exe"
#define AppPublisher "Mobile Service Pro"

#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif

[Setup]
; AppId must NEVER change: it is how a new version finds and upgrades the old one.
AppId={{A94DE3EE-2F02-49CF-8D3A-7041C3737453}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\MobileServicePro\Assets\app.ico
OutputDir=..\artifacts\installer
OutputBaseFilename=MobileServicePro-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Lets the user choose "for all users" (admin) or "only for me" (no admin).
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; Same mutex name as in App.xaml.cs: setup asks to close the running program first.
AppMutex=MobileServicePro_SingleInstance
CloseApplications=yes

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Everything produced by dotnet publish: the single-file exe and, if bundled, tools\platform-tools.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; A running adb server keeps adb.exe locked; stop it so the folder can be removed.
Filename: "{app}\tools\platform-tools\adb.exe"; Parameters: "kill-server"; Flags: runhidden skipifdoesntexist; RunOnceId: "KillAdbServer"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  AdbPath: String;
  ResultCode: Integer;
begin
  { Upgrade: stop the bundled adb server (if any) so its files can be replaced. }
  AdbPath := ExpandConstant('{app}\tools\platform-tools\adb.exe');
  if FileExists(AdbPath) then
    Exec(AdbPath, 'kill-server', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
