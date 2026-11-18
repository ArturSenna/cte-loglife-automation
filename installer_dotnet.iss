; Inno Setup script for the C# version of CTe LogLife.
; Compiled by build_installer_dotnet.ps1, which passes the version and the folder to package.
;
; The installer is published on GitHub releases, where the Python version (1.x) finds it and offers
; it as an update. Replacing that version in place depends on three things in this file:
;
;   1. The same AppId as the 1.x installers.
;   2. 32-bit install mode, like the 1.x installers (no ArchitecturesInstallIn64BitMode). They
;      registered the app in the 32-bit view of the registry and put it under "Program Files (x86)".
;      In 64-bit mode Setup does not see that installation and leaves a second copy under
;      "Program Files". The app itself is 64-bit either way.
;   3. The Python version kept the LogLife login in _internal\.env. That folder is removed below, so
;      the file is first copied next to the exe, where the new app imports it on its first start.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

; Folder with the published app (dotnet publish output).
#ifndef SourceDir
  #define SourceDir "publish\CTe LogLife"
#endif

#define MyAppName "CTe LogLife"
#define MyAppPublisher "LogLife"
#define MyAppURL "https://transportebiologico.com.br"
#define MyAppExeName "CTe LogLife.exe"

; /DSelfCheck builds the copy that build_installer_dotnet.ps1 installs into a temporary folder to
; prove that this script works: same files and same steps, but with an AppId of its own, without
; administrator rights, shortcuts or registry entries. It can never touch a real installation.
#ifdef SelfCheck
  #define MyAppId "{{7C1E5B0A-93D4-4F6E-B2A8-5D0F3C6E9A14}"
#else
  #define MyAppId "{{A5B8C3D4-E6F7-8A9B-0C1D-2E3F4A5B6C7D}"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer_output
OutputBaseFilename=CTe_LogLife_Setup_v{#MyAppVersion}
SetupIconFile=botCTE\botCTE\my_icon.ico
WizardStyle=modern
ShowLanguageDialog=auto
UninstallDisplayIcon={app}\{#MyAppExeName}
; The app is published for win-x64 only.
ArchitecturesAllowed=x64compatible
#ifdef SelfCheck
PrivilegesRequired=lowest
Uninstallable=no
CloseApplications=no
Compression=none
#else
CloseApplications=yes
Compression=lzma2/max
SolidCompression=yes
#endif

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; What the Python (PyInstaller) version left in the folder.
Type: filesandordirs; Name: "{app}\_internal"
; Screenshots removed from a newer version must not linger.
Type: filesandordirs; Name: "{app}\Templates"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
; The login file kept from the Python version (see [Code]).
Type: files; Name: "{app}\.env"

#ifndef SelfCheck
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
#endif

[Code]
// Runs before [InstallDelete] removes the Python version's _internal folder.
procedure CurStepChanged(CurStep: TSetupStep);
var
  OldLogin, KeptLogin: String;
begin
  if CurStep <> ssInstall then
    exit;
  OldLogin := ExpandConstant('{app}\_internal\.env');
  KeptLogin := ExpandConstant('{app}\.env');
  if FileExists(OldLogin) and not FileExists(KeptLogin) then
  begin
    if CopyFile(OldLogin, KeptLogin, True) then
      Log('Login file of the Python version kept as ' + KeptLogin)
    else
      Log('Could not keep the login file of the Python version: ' + OldLogin);
  end;
end;
