#ifndef SourceDir
  #error SourceDir must point to the published CryptoBook files.
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#ifndef MyAppVersion
  #define MyAppVersion "1.1.2.4"
#endif

#ifndef VersionInfoVersion
  #define VersionInfoVersion "1.1.2.4"
#endif

#define MyAppName "CryptoBook"
#define MyAppPublisher "Романов Сергей"
#define MyAppExeName "CryptoBook.exe"
#define MyShortcutIconName "CryptoBook-" + MyAppVersion + ".ico"

[Setup]
AppId={{9D51F202-0EB4-4A62-A45E-0601F8C12D01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=CryptoBook-Setup-{#MyAppVersion}
SetupIconFile=..\CryptoBook\Resources\Icons\AppIcon.ico
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
AppMutex=CryptoBook.Application
VersionInfoVersion={#VersionInfoVersion}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
Type: files; Name: "{app}\CryptoBook-*.ico"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\CryptoBook\Resources\Icons\AppIcon.ico"; DestDir: "{app}"; DestName: "{#MyShortcutIconName}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\COPYRIGHT.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SOURCE_CODE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ASSET_PROVENANCE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSES\*"; DestDir: "{app}\LICENSES"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\compliance\*"; DestDir: "{app}\compliance"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyShortcutIconName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyShortcutIconName}"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: "CryptoBook.Document"; ValueType: string; ValueName: ""; ValueData: "CryptoBook document"; Flags: uninsdeletekey
Root: HKCR; Subkey: "CryptoBook.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyShortcutIconName},0"
Root: HKCR; Subkey: "CryptoBook.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCR; Subkey: ".cbook\OpenWithProgids"; ValueType: none; ValueName: "CryptoBook.Document"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".cbox\OpenWithProgids"; ValueType: none; ValueName: "CryptoBook.Document"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyShortcutIconName},0"
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".cbook"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".cbox"; ValueData: ""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
