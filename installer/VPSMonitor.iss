; Instalador de VPS Monitor (Inno Setup 6).
; Se compila con installer\build-installer.ps1, que publica la app y pasa la versión.

#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\VPSMonitor-win-x64"
#endif

#define AppName "VPS Monitor"
#define AppExe "VPSMonitor.exe"

[Setup]
AppId={{6C1F8E5B-2D0A-4C7E-9B43-5A8E1D7F2C90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=VPS Monitor
AppPublisherURL=https://github.com/luks95/vps-status
AppSupportURL=https://github.com/luks95/vps-status/issues
DefaultDirName={autopf}\VPS Monitor
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\artifacts\installer
OutputBaseFilename=VPSMonitor-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
; Instala solo para el usuario actual por defecto (sin pedir administrador); el asistente permite elegir.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "startup"; Description: "Iniciar VPS Monitor con Windows (queda en la bandeja)"; GroupDescription: "Opciones:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VPSMonitor"; ValueData: """{app}\{#AppExe}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

; Al desinstalar NO se borran los datos del usuario (%LOCALAPPDATA%\VPSMonitor ni las credenciales de Windows).
