; Inno Setup Script for CTe LogLife
; This script creates a professional installer for the application
; Download Inno Setup from: https://jrsoftware.org/isdl.php

; Read version from VERSION file
#define VersionFile FileOpen("botCTE\VERSION")
#define MyAppVersion FileRead(VersionFile)
#expr FileClose(VersionFile)
#define MyAppVersion Trim(MyAppVersion)

#define MyAppName "CTe LogLife"
#define MyAppPublisher "LogLife"
#define MyAppURL "https://transportebiologico.com.br"
#define MyAppExeName "CTe LogLife.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
AppId={{A5B8C3D4-E6F7-8A9B-0C1D-2E3F4A5B6C7D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Uncomment the following line to run in non administrative install mode (install for current user only.)
;PrivilegesRequired=lowest
OutputDir=installer_output
OutputBaseFilename=CTe_LogLife_Setup_v{#MyAppVersion}
SetupIconFile=botCTE\botCTE\my_icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy all files from the dist folder
Source: "dist\CTe LogLife\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Custom code for installer

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Any post-install tasks can be added here
  end;
end;
