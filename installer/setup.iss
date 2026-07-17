; Universal Live Server Inno Setup Script
; Compile: ISCC.exe setup.iss

#define MyAppName "Universal Live Server"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Moaaz Besher"
#define MyAppURL "https://github.com/MoaazBesher/liveserver"
#define MyAppExeName "liveServer.exe"

[Setup]
AppId={{B8F4A3D2-9C7E-4F1E-8D5A-2E6F7C1B3A5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=UniversalLiveServer-{#MyAppVersion}-Setup
SetupIconFile=..\src\app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
CloseApplications=yes
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "contextmenu"; Description: "Add &folder right-click menu (Open with Universal Live Server)"; GroupDescription: "Integration:"; Flags: checkedonce
Name: "path"; Description: "Add &uls command to system PATH"; GroupDescription: "Integration:"; Flags: checkedonce

[Files]
Source: "..\liveServer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "uninstall.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Registry]
; Context menu entry
Root: "HKCR"; Subkey: "Directory\shell\UniversalLiveServer"; Flags: uninsdeletekey; Tasks: contextmenu
Root: "HKCR"; Subkey: "Directory\shell\UniversalLiveServer"; ValueType: string; ValueName: ""; ValueData: "Open with Universal Live Server"; Tasks: contextmenu
Root: "HKCR"; Subkey: "Directory\shell\UniversalLiveServer"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu
Root: "HKCR"; Subkey: "Directory\shell\UniversalLiveServer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%V"""; Tasks: contextmenu

; Background context menu (click on folder background)
Root: "HKCR"; Subkey: "Directory\Background\shell\UniversalLiveServer"; Flags: uninsdeletekey; Tasks: contextmenu
Root: "HKCR"; Subkey: "Directory\Background\shell\UniversalLiveServer"; ValueType: string; ValueName: ""; ValueData: "Open with Universal Live Server"; Tasks: contextmenu
Root: "HKCR"; Subkey: "Directory\Background\shell\UniversalLiveServer"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu
Root: "HKCR"; Subkey: "Directory\Background\shell\UniversalLiveServer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%V\."""; Tasks: contextmenu

[Environment]
; Add to PATH via a batch launcher
Name: "PATH"; Value: "{app}"; Tasks: path

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Universal Live Server"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\uninstall.ps1"; Flags: runhidden skipifdoesntexist

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ulsPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    ulsPath := ExpandConstant('{app}') + '\uls.bat';
    if not FileExists(ulsPath) then
    begin
      SaveStringToFile(ulsPath,
        '@echo off' + #13#10 +
        'start "" "%~dp0{#MyAppExeName}" %*' + #13#10,
        False);
    end;
  end;
end;
