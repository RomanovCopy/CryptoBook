#ifndef SourceDir
  #error SourceDir must point to the published CryptoBook files.
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#ifndef MyAppVersion
  #define MyAppVersion "1.1.3.7"
#endif

#ifndef VersionInfoVersion
  #define VersionInfoVersion "1.1.3.7"
#endif

#define MyAppName "CryptoBook"
#define MyAppPublisher "Романов Сергей"
#define MyAppExeName "CryptoBook.exe"

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
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; CryptoBook releases before 1.1.2.5 used a multi-file self-contained layout.
; Remove only known application/runtime artifacts so the single-file .NET 10
; host cannot load stale .NET 8 assemblies during an in-place upgrade.
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.deps.json"
Type: files; Name: "{app}\*.runtimeconfig.json"
Type: files; Name: "{app}\*.config"
Type: files; Name: "{app}\createdump.exe"
Type: filesandordirs; Name: "{app}\cs"
Type: filesandordirs; Name: "{app}\de"
Type: filesandordirs; Name: "{app}\es"
Type: filesandordirs; Name: "{app}\fr"
Type: filesandordirs; Name: "{app}\it"
Type: filesandordirs; Name: "{app}\ja"
Type: filesandordirs; Name: "{app}\ko"
Type: filesandordirs; Name: "{app}\pl"
Type: filesandordirs; Name: "{app}\pt-BR"
Type: filesandordirs; Name: "{app}\ru"
Type: filesandordirs; Name: "{app}\tr"
Type: filesandordirs; Name: "{app}\uk"
Type: filesandordirs; Name: "{app}\zh-Hans"
Type: filesandordirs; Name: "{app}\zh-Hant"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\LICENSES"
Type: filesandordirs; Name: "{app}\compliance"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\COPYRIGHT.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SOURCE_CODE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ASSET_PROVENANCE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSES\*"; DestDir: "{app}\LICENSES"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\compliance\*"; DestDir: "{app}\compliance"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: "CryptoBook.Document"; ValueType: string; ValueName: ""; ValueData: "CryptoBook document"; Flags: uninsdeletekey
Root: HKCR; Subkey: "CryptoBook.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "CryptoBook.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCR; Subkey: ".cbook\OpenWithProgids"; ValueType: none; ValueName: "CryptoBook.Document"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".cbox\OpenWithProgids"; ValueType: none; ValueName: "CryptoBook.Document"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".cbook"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".cbox"; ValueData: ""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  SHCNE_ASSOCCHANGED = $08000000;
  SHCNF_IDLIST = $0000;

procedure SHChangeNotify(wEventId: LongWord; uFlags: LongWord;
  dwItem1: LongInt; dwItem2: LongInt);
  external 'SHChangeNotify@shell32.dll stdcall';

function MigratePinnedTaskbarShortcutIcon: Boolean;
var
  ApplicationPath: String;
  ExpectedIconLocation: String;
  PinnedShortcutPath: String;
  SavedIconLocation: String;
  SavedTargetPath: String;
  ShortcutTargetPath: String;
  Shell: Variant;
  Shortcut: Variant;
begin
  Result := True;
  PinnedShortcutPath := ExpandConstant(
    '{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\{#MyAppName}.lnk');

  if not FileExists(PinnedShortcutPath) then
  begin
    Log('No existing CryptoBook taskbar shortcut requires migration.');
    Exit;
  end;

  ApplicationPath := ExpandConstant('{app}\{#MyAppExeName}');
  ExpectedIconLocation := ApplicationPath + ',0';

  try
    Shell := CreateOleObject('WScript.Shell');
    Shortcut := Shell.CreateShortcut(PinnedShortcutPath);
    ShortcutTargetPath := Shortcut.TargetPath;

    if CompareText(ShortcutTargetPath, ApplicationPath) <> 0 then
      Log('Retargeting the legacy CryptoBook taskbar shortcut from: ' +
        ShortcutTargetPath);

    Shortcut.TargetPath := ApplicationPath;
    Shortcut.IconLocation := ExpectedIconLocation;
    Shortcut.WorkingDirectory := ExpandConstant('{app}');
    Shortcut.Save;

    Shortcut := Shell.CreateShortcut(PinnedShortcutPath);
    SavedTargetPath := Shortcut.TargetPath;
    SavedIconLocation := Shortcut.IconLocation;
    if (CompareText(SavedTargetPath, ApplicationPath) <> 0) or
       (CompareText(SavedIconLocation, ExpectedIconLocation) <> 0) then
    begin
      Log('Keeping legacy icons because the migrated CryptoBook taskbar ' +
        'shortcut could not be verified.');
      Result := False;
      Exit;
    end;

    Log('Migrated the CryptoBook taskbar shortcut to the executable icon.');
    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0);
  except
    Log('Keeping legacy icons because taskbar shortcut migration failed: ' +
      GetExceptionMessage);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    if MigratePinnedTaskbarShortcutIcon then
    begin
      if DelTree(ExpandConstant('{app}\CryptoBook-*.ico'),
        False, True, False) then
        Log('Removed obsolete versioned CryptoBook icon files.')
      else
        Log('One or more obsolete versioned CryptoBook icon files could not be removed.');
    end;
  end;
end;
