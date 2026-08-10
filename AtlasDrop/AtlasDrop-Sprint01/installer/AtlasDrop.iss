#ifndef AppVersion
  #define AppVersion "1.1.7"
#endif
#define AppName "Atlas Drop"
#define AppExeName "AtlasDrop.App.exe"

[Setup]
AppId={{D84BCB52-51A2-4D1C-A07A-B511E37C4DC1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Atlas
DefaultDirName={localappdata}\AtlasDrop
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=output
OutputBaseFilename=Installer-Atlas-Drop
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Register-ModernContextMenu.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Unregister-ModernContextMenu.ps1"; DestDir: "{app}"; Flags: ignoreversion
; Whisper.net sous Windows nécessite le runtime Microsoft Visual C++ 2022 x64.
; Le pipeline télécharge ce redistribuable uniquement depuis Microsoft et vérifie sa signature.
Source: "redist\vc_redist.x64.exe"; DestDir: "{tmp}"; DestName: "AtlasDrop-vc_redist.x64.exe"; Flags: deleteafterinstall

[InstallDelete]
; Supprime uniquement les anciens raccourcis officiels Atlas Drop avant de recréer le bon.
Type: files; Name: "{commondesktop}\Atlas Drop.lnk"
Type: files; Name: "{userdesktop}\Atlas Drop.lnk"

[Icons]
Name: "{userdesktop}\Atlas Drop"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{userstartup}\Atlas Drop"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"

[Registry]
; Compatibilité uniquement : ces verbes historiques restent dans « Afficher plus d'options »
; si une stratégie Windows bloque le package moderne IExplorerCommand.
Root: HKA; Subkey: "Software\Classes\*\shell\AtlasDrop"; ValueType: string; ValueName: ""; ValueData: "Ranger avec Atlas Drop"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\*\shell\AtlasDrop"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExeName}"""
Root: HKA; Subkey: "Software\Classes\*\shell\AtlasDrop"; ValueType: string; ValueName: "Position"; ValueData: "Top"
Root: HKA; Subkey: "Software\Classes\*\shell\AtlasDrop\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Directory\shell\AtlasDrop"; ValueType: string; ValueName: ""; ValueData: "Ranger avec Atlas Drop"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\shell\AtlasDrop"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExeName}"""
Root: HKA; Subkey: "Software\Classes\Directory\shell\AtlasDrop"; ValueType: string; ValueName: "Position"; ValueData: "Top"
Root: HKA; Subkey: "Software\Classes\Directory\shell\AtlasDrop\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

[Run]
; Installation idempotente du prérequis natif Whisper Windows.
Filename: "{tmp}\AtlasDrop-vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; Flags: runhidden waituntilterminated
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{app}\Register-ModernContextMenu.ps1"" -PackagePath ""{app}\AtlasDrop.ContextMenu.msix"" -ExternalLocation ""{app}"" -CertificatePath ""{app}\AtlasDrop.ContextMenu.cer"""; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "Lancer Atlas Drop"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExeName}"; Flags: nowait; Check: IsUpdateMode

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExeName} /F >NUL 2>&1"; Flags: runhidden; RunOnceId: "StopAtlasDrop"
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{app}\Unregister-ModernContextMenu.ps1"" -ExternalLocation ""{app}"""; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterAtlasDropModernMenu"

[Code]
function IsUpdateMode: Boolean;
begin
  Result := ExpandConstant('{param:UPDATE|0}') = '1';
end;
