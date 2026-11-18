; LOQ Keyboard RGB setup (Inno Setup 6)
; Build: run build.cmd first, then
;   ISCC.exe installer\setup.iss /DAppVersion=1.0.0
; Output: dist\LOQKeyboardRGB-Setup-<version>.exe

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "LOQ Keyboard RGB"
#define AppExe "LOQKeyboardRGB.exe"
#define AppPublisher "Salman MP"
#define AppUrl "https://github.com/salmankavanur/LOQ-Keyboard-RGB"

[Setup]
AppId={{8D3C1B52-6E4F-4C1A-9B7E-2F5A4C9D1E63}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=© 2026 {#AppPublisher}
; Per-user install: no admin prompt. {autopf} becomes %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=LOQKeyboardRGB-Setup-{#AppVersion}
SetupIconFile=..\assets\app.ico
WizardSmallImageFile=..\assets\wizard-small-55.bmp,..\assets\wizard-small-110.bmp
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Close a running copy (it is also asked to exit politely in PrepareToInstall)
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoCopyright=© 2026 {#AppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start with Windows (keeps your lighting applied, runs in the tray)"; GroupDescription: "Startup:"

[Files]
Source: "..\bin\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Control the keyboard backlight of Lenovo LOQ laptops"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; "Start with Windows" (the app's own checkbox uses the same value)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LOQKeyboardRGB"; ValueData: """{app}\{#AppExe}"" --tray"; Tasks: autostart
; Always remove that value on uninstall, even if it was set later from inside the app
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "LOQKeyboardRGB"; Flags: uninsdeletevalue dontcreatekey
; Replace the Apps entry of the app's built-in per-user installer, if it was used before
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\LOQKeyboardRGB"; ValueType: none; Flags: deletekey

[InstallDelete]
; Shortcut created by the built-in installer (setup creates its own with the same name)
Type: files; Name: "{userprograms}\LOQ Keyboard RGB.lnk"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "ExitApp"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { Ask an already-installed copy to quit so its exe can be replaced. }
  if FileExists(ExpandConstant('{app}\{#AppExe}')) then
    Exec(ExpandConstant('{app}\{#AppExe}'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    SettingsDir := ExpandConstant('{userappdata}\LOQ Keyboard RGB');
    if DirExists(SettingsDir) and (not UninstallSilent) then
      if MsgBox('Also delete your settings and profiles?' #13#10#13#10 + SettingsDir,
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(SettingsDir, True, True, True);
  end;
end;
