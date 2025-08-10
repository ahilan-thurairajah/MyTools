; Inno Setup script for MyTools MyCalculator
; Builds a Windows installer that installs to Program Files, adds a Start Menu shortcut,
; and optionally creates a Desktop shortcut.

#define MyAppName "My Calculator"
#define MyAppPublisher "MyTools"
#define MyAppVersion "1.0.0"
#define MyAppExe "MyTools.MyCalculator.exe"

[Setup]
AppId={{F2C2C3E3-3C6E-4A5D-9D47-6F9D3F0C2B1E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MyTools\MyCalculator
DefaultGroupName=MyTools
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=MyTools-MyCalculator-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
SetupIconFile=MyCalculator.ico

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Optional desktop shortcut
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; The build script publishes the app into the local 'app' folder next to this script
Source: "app\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
; Start Menu group shortcut (always)
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
; Optional desktop shortcut
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Optionally, you could add instructions at the end for pinning to taskbar, which Windows restricts programmatically.
