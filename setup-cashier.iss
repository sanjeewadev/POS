; ===================================================================
; Advanced POS - Cashier Only Setup Script
; ===================================================================
; This script is controlled by the build.ps1 PowerShell script.

#define MyAppName "Advanced POS Cashier"
#define MyAppPublisher "Your Company Name"
#define MyAppURL "<https://www.yourcompany.com>"
; The version is passed from the PowerShell script using /DAppVersion=1.0.8
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define MyAppExeName "POS.Deployment.Wizard.exe"
#define MyAppOutputName "Advanced-POS-Cashier-Setup-" + AppVersion

[Setup]
AppId={{AUTO_GUID}}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Advanced POS
DefaultGroupName=Advanced POS
DisableProgramGroupPage=yes
OutputBaseFilename={#MyAppOutputName}
OutputDir=Output
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; This section copies only the necessary application files from our 'dist' staging folder.
; It deliberately excludes the SQL Server prerequisite.
Source: "dist\POS.Deployment.Wizard\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\POS.Cashier.UI\*"; DestDir: "{app}\Cashier"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\POS.Database.Setup\*"; DestDir: "{app}\DatabaseSetup"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\Tools\*"; DestDir: "{app}\Tools"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcuts
Name: "{group}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"
Name: "{group}\Advanced POS Setup"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
; Desktop shortcut to the main application (Cashier)
Name: "{commondesktop}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; Tasks: desktopicon

[Run]
; This runs the Deployment Wizard automatically after installation finishes.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait skipifsilent