#define AppName "DataDeveloper"
#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
#ifndef Platform
  #define Platform "win-x64"
#endif
#ifndef PublishDir
  #define PublishDir "..\\..\\artifacts\\windows\\" + Platform + "\\publish"
#endif

[Setup]
AppId={{7A1120F9-8E99-4D6B-A9F5-4476C08F3D09}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=DataDeveloper
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts\windows\{#Platform}
OutputBaseFilename=DataDeveloper-{#AppVersion}-{#Platform}-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\DataDeveloper.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\DataDeveloper.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DataDeveloper.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DataDeveloper.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
