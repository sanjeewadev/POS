; ===================================================================
; Advanced POS - Inno Setup Script
; ===================================================================
; This script is controlled by the build.ps1 PowerShell script.

#define MyAppName "Advanced POS"
#define MyAppPublisher "Your Company Name"
#define MyAppURL "<https://www.yourcompany.com>"
; The version is passed from the PowerShell script using /DAppVersion=1.0.8
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define MyAppExeName "POS.Deployment.Wizard.exe"
#define MyAppOutputName "Advanced-POS-Setup-" + AppVersion

[Setup]
AppId={{AUTO_GUID}}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename={#MyAppOutputName}
; Place the final setup.exe in an 'Output' folder
OutputDir=Output
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation (Server, BackOffice, and Cashier)"
Name: "compact"; Description: "Server and BackOffice only"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "server"; Description: "Server and BackOffice"; Types: full compact custom; Flags: fixed
Name: "cashier"; Description: "Cashier Terminal"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; This section copies all the files from our 'dist' staging folder into the user's installation directory.
; It also bundles the SQL Server Express installer, which will be run during setup.
Source: "prerequisites\SQLEXPR_x64_ENU.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

; Application Files
; The structure here matches the paths used in your Deployment Wizard.
Source: "dist\POS.Deployment.Wizard\*"; DestDir: "{app}"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\POS.BackOffice.UI\*"; DestDir: "{app}\BackOffice"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\POS.Cashier.UI\*"; DestDir: "{app}\Cashier"; Components: cashier; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\POS.Database.Setup\*"; DestDir: "{app}\DatabaseSetup"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\Tools\*"; DestDir: "{app}\Tools"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcuts
Name: "{group}\Advanced POS BackOffice"; Filename: "{app}\BackOffice\POS.BackOffice.UI.exe"; Components: server
Name: "{group}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; Components: cashier
Name: "{group}\Advanced POS Setup"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
; Desktop shortcut to the main application (BackOffice)
Name: "{commondesktop}\Advanced POS BackOffice"; Filename: "{app}\BackOffice\POS.BackOffice.UI.exe"; Tasks: desktopicon; Components: server
Name: "{commondesktop}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; Tasks: desktopicon; Components: cashier

[Run]
; 1. First, install SQL Server 2022 Express silently. This is required for the Server/BackOffice.
;    The parameters ensure a quiet installation of only the database engine with the correct instance name.
;    NOTE: This can take several minutes to complete.
Filename: "{tmp}\SQLEXPR_x64_ENU.exe"; Parameters: "/Q /IACCEPTSQLSERVERLICENSETERMS /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS /SQLSVCACCOUNT=""NT AUTHORITY\System"" /SQLSYSADMINACCOUNTS=""BUILTIN\Administrators"" /TCPENABLED=1"; StatusMsg: "Installing SQL Server 2022 Express (this may take several minutes)..."; Flags: waituntilterminated

; 2. After SQL Server is installed, run the Deployment Wizard automatically to configure the POS database.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--server {code:GetServerCashierArgument}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait skipifsilent

[Code]
function GetServerCashierArgument(Param: string): string;
begin
  if IsComponentSelected('cashier') then
    Result := '--server-cashier'
  else
    Result := '';
end;
