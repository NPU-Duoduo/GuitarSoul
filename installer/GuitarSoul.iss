#ifndef AppVersion
  #define AppVersion "0.15.3"
#endif
#define Payload "..\dist\GuitarSoul-" + AppVersion + "-win-x64"

[Setup]
AppId={{52D6AF53-2E58-4BA2-82C4-DF8207251360}
AppName=GuitarSoul
AppVersion={#AppVersion}
AppPublisher=Duoduo_127
AppCopyright=Copyright (C) 2026 Duoduo_127
VersionInfoCompany=Duoduo_127
VersionInfoProductName=GuitarSoul
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\GuitarSoul
DefaultGroupName=GuitarSoul
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableDirPage=no
DisableProgramGroupPage=yes
DisableWelcomePage=no
WizardStyle=modern
SetupIconFile=..\src\GuitarSoul.App\Assets\guitar-soul-icon-v1.ico
UninstallDisplayIcon={app}\GuitarSoul.exe
OutputDir=..\dist
OutputBaseFilename=GuitarSoul-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
Uninstallable=yes
UsePreviousAppDir=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\GuitarSoul"; Filename: "{app}\GuitarSoul.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,GuitarSoul}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GuitarSoul"; Filename: "{app}\GuitarSoul.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\GuitarSoul.exe"; Description: "{cm:LaunchProgram,GuitarSoul}"; Flags: nowait postinstall skipifsilent

; User settings, memories and audio live outside {app}; uninstall only removes installed files.
