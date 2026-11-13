#define MyAppName "ImageClipboard"
#define MyAppExe "ImageClipboard.exe"
#define MyAppPublisher "ImageClipboard"
#define MyAppUrl "https://github.com/"
#define MyAppId "{{A4F9B1C2-7D3E-4F8A-9B2C-1E5D6A7B8C9D}"
#define MyAppVersion GetVersionNumbersString("..\ImageClipboard\bin\Release\net8.0-windows\win-x64\publish\ImageClipboard.dll")

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=ImageClipboardSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UsePreviousAppDir=yes
DisableDirPage=auto
PrivilegesRequired=lowest
CloseApplicationsFilter=ImageClipboard.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExe}
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Запускать с Windows"; GroupDescription: "Дополнительно:"; Flags: checkedonce

[Files]
Source: "..\ImageClipboard\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: autostart

[Registry]
Root: HKCU; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "DisplayVersion"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ImageClipboard"; ValueData: """{app}\{#MyAppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function GetInstalledVersion(Param: string): String;
begin
  Result := '';
  if not RegQueryStringValue(HKCU, 'Software\{#MyAppName}', 'DisplayVersion', Result) then
    Result := '';
end;

function VersionGreaterOrEqual(V1, V2: String): Boolean;
var
  P1, P2: Integer;
  N1, N2: Integer;
  S1, S2: String;
begin
  S1 := V1;
  S2 := V2;
  Result := True;

  while (S1 <> '') or (S2 <> '') do
  begin
    P1 := Pos('.', S1);
    P2 := Pos('.', S2);
    if P1 = 0 then P1 := Length(S1) + 1;
    if P2 = 0 then P2 := Length(S2) + 1;

    N1 := StrToIntDef(Copy(S1, 1, P1 - 1), 0);
    N2 := StrToIntDef(Copy(S2, 1, P2 - 1), 0);

    if N1 > N2 then
    begin
      Result := True;
      Exit;
    end;
    if N1 < N2 then
    begin
      Result := False;
      Exit;
    end;

    Delete(S1, 1, P1);
    Delete(S2, 1, P2);
  end;
end;

function InitializeSetup(): Boolean;
var
  Installed: String;
  SetupVer: String;
begin
  Result := True;
  Installed := GetInstalledVersion('');
  SetupVer := '{#MyAppVersion}';

  if Installed <> '' then
  begin
    if VersionGreaterOrEqual(Installed, SetupVer) and (Installed <> SetupVer) then
      if MsgBox('Уже установлена более новая версия (' + Installed + ').'#13#10 +
                'Продолжить установку более старой (' + SetupVer + ')?',
                mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
