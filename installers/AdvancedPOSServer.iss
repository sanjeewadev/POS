#ifndef SourceRoot
  #error SourceRoot must be supplied by Build-POS-Production-Installers.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by Build-POS-Production-Installers.ps1
#endif
#ifndef AppVersion
  #define AppVersion "1.0.2"
#endif

#define AppName "Advanced POS Server"
#define PublisherName "Advanced POS"
#define AppExeName "POS.BackOffice.UI.exe"

[Setup]
AppId={{7A34BE87-6A8F-4A91-A12B-11D000000001}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#PublisherName}
DefaultDirName={autopf}\Advanced POS\Server
DefaultGroupName=Advanced POS
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Advanced_POS_Server_Setup_{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\BackOffice\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
CreateUninstallRegKey=yes
UpdateUninstallLogAppName=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
UsePreviousGroup=yes
UsePreviousLanguage=yes
UsePreviousSetupType=yes

[Types]
Name: "serveronly"; Description: "Server and BackOffice"
Name: "servercashier"; Description: "Server, BackOffice, and Cashier on this computer"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "server"; Description: "SQL database tools and BackOffice"; Types: serveronly servercashier custom; Flags: fixed
Name: "cashier"; Description: "Cashier application on the server computer"; Types: servercashier

[Tasks]
Name: "desktopbackoffice"; Description: "Create a BackOffice desktop shortcut"; GroupDescription: "Desktop shortcuts:"; Flags: checkedonce
Name: "desktopcashier"; Description: "Create a Cashier desktop shortcut"; GroupDescription: "Desktop shortcuts:"; Components: cashier; Flags: checkedonce

[Files]
Source: "{#SourceRoot}\Server\BackOffice\*"; DestDir: "{app}\BackOffice"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Server\Cashier\*"; DestDir: "{app}\Cashier"; Components: cashier; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Server\DatabaseSetup\*"; DestDir: "{app}\DatabaseSetup"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Server\DeploymentWizard\*"; DestDir: "{app}\DeploymentWizard"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Server\Tools\*"; DestDir: "{app}\Tools"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Server\Docs\*"; DestDir: "{app}\Docs"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
#ifdef SqlExpressInstaller
Source: "{#SqlExpressInstaller}"; DestDir: "{tmp}"; DestName: "SQLEXPR_x64_ENU.exe"; Flags: deleteafterinstall; AfterInstall: InstallSqlExpressIfNeeded
#endif

[Icons]
Name: "{group}\Advanced POS BackOffice"; Filename: "{app}\BackOffice\POS.BackOffice.UI.exe"; WorkingDir: "{app}\BackOffice"
Name: "{autodesktop}\Advanced POS BackOffice"; Filename: "{app}\BackOffice\POS.BackOffice.UI.exe"; WorkingDir: "{app}\BackOffice"; Tasks: desktopbackoffice
Name: "{group}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; WorkingDir: "{app}\Cashier"; Components: cashier
Name: "{autodesktop}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; WorkingDir: "{app}\Cashier"; Components: cashier; Tasks: desktopcashier
Name: "{group}\Activate Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; Parameters: "--activate"; WorkingDir: "{app}\Cashier"; Components: cashier
Name: "{group}\Repair or Configure Advanced POS Cashier"; Filename: "{app}\DeploymentWizard\POS.Deployment.Wizard.exe"; Parameters: "--mode cashier --install-root ""{app}"""; WorkingDir: "{app}"; Components: cashier
Name: "{group}\Configure Advanced POS Server"; Filename: "{app}\DeploymentWizard\POS.Deployment.Wizard.exe"; Parameters: "--mode server --install-root ""{app}"" --server-cashier {code:GetServerCashierArgument}"; WorkingDir: "{app}"
Name: "{group}\Backup POS Database"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -NoProfile -ExecutionPolicy Bypass -File ""{app}\Tools\Backup-POS-Production.ps1"" -InstallRoot ""{app}"""; WorkingDir: "{app}\Tools"
Name: "{group}\Restore POS Database"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -NoProfile -ExecutionPolicy Bypass -File ""{app}\Tools\Restore-POS-Production.ps1"" -InstallRoot ""{app}"""; WorkingDir: "{app}\Tools"
Name: "{group}\Check POS Database"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -NoProfile -ExecutionPolicy Bypass -File ""{app}\Tools\Check-POS-Production.ps1"" -InstallRoot ""{app}"""; WorkingDir: "{app}\Tools"
Name: "{group}\POS Server Status"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -NoProfile -ExecutionPolicy Bypass -File ""{app}\Tools\Show-POS-Server-Status.ps1"" -InstallRoot ""{app}"""; WorkingDir: "{app}\Tools"

[Run]
Filename: "{app}\DeploymentWizard\POS.Deployment.Wizard.exe"; Parameters: "--mode server --install-root ""{app}"" --server-cashier {code:GetServerCashierArgument}"; WorkingDir: "{app}"; Description: "Configure the production POS Server"; StatusMsg: "Opening Advanced POS Server configuration..."; Flags: waituntilterminated; Check: ShouldRunServerConfiguration

[InstallDelete]
Type: files; Name: "{group}\Configure Advanced POS Cashier.lnk"
Type: filesandordirs; Name: "{app}\Cashier"; Check: IsServerOnlyInstall
Type: files; Name: "{autodesktop}\Advanced POS Cashier.lnk"; Check: IsServerOnlyInstall
Type: files; Name: "{group}\Advanced POS Cashier.lnk"; Check: IsServerOnlyInstall
Type: files; Name: "{group}\Activate Advanced POS Cashier.lnk"; Check: IsServerOnlyInstall
Type: files; Name: "{group}\Repair or Configure Advanced POS Cashier.lnk"; Check: IsServerOnlyInstall

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Temp"

[Code]
var
  SqlExpressRestartRequired: Boolean;
  MaintenancePage: TInputOptionWizardPage;
  ExistingInstallationDetected: Boolean;

function HasExistingServerInstallation(): Boolean;
begin
  Result :=
    RegKeyExists(
      HKLM64,
      'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7A34BE87-6A8F-4A91-A12B-11D000000001}_is1') or
    RegKeyExists(
      HKLM,
      'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7A34BE87-6A8F-4A91-A12B-11D000000001}_is1');
end;

procedure InitializeWizard();
begin
  ExistingInstallationDetected :=
    HasExistingServerInstallation();

  if ExistingInstallationDetected then
  begin
    MaintenancePage := CreateInputOptionPage(
      wpWelcome,
      'Upgrade or repair Advanced POS Server',
      'An existing Server installation was detected.',
      'Choose how setup should handle the installed system. The SQL database, ' +
      'store data, users, licences, connection profiles, terminal assignments, ' +
      'and backup files are preserved.',
      True,
      False);

    MaintenancePage.Add(
      'Upgrade or repair application files and keep the current configuration');
    MaintenancePage.Add(
      'Upgrade application files and open Server configuration after setup');
    MaintenancePage.SelectedValueIndex := 0;
  end;
end;

function ShouldRunServerConfiguration(): Boolean;
begin
  Result :=
    (not ExistingInstallationDetected) or
    ((MaintenancePage <> nil) and
     (MaintenancePage.SelectedValueIndex = 1));
end;

function SqlExpressServiceExists(): Boolean;
begin
  Result :=
    RegKeyExists(HKLM64, 'SYSTEM\CurrentControlSet\Services\MSSQL$SQLEXPRESS') or
    RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\MSSQL$SQLEXPRESS');
end;

function ShouldInstallSqlExpress(): Boolean;
begin
  Result := not SqlExpressServiceExists();
end;

function IsServerCashierInstall(): Boolean;
begin
  Result := WizardIsComponentSelected('cashier');
end;

function IsServerOnlyInstall(): Boolean;
begin
  Result := not IsServerCashierInstall();
end;

function GetServerCashierArgument(Param: String): String;
begin
  if IsServerCashierInstall() then
    Result := 'true'
  else
    Result := 'false';
end;

#ifdef SqlExpressInstaller
procedure InstallSqlExpressIfNeeded();
var
  ResultCode: Integer;
  Parameters: String;
begin
  if not ShouldInstallSqlExpress() then
  begin
    Log('Existing SQLEXPRESS service detected; bundled SQL Server Express installation skipped.');
    exit;
  end;

  Parameters :=
    '/Q /ACTION=Install /FEATURES=SQLEngine ' +
    '/INSTANCENAME=SQLEXPRESS /INSTANCEID=SQLEXPRESS ' +
    '/SQLSVCSTARTUPTYPE=Automatic ' +
    '/SQLSYSADMINACCOUNTS="BUILTIN\ADMINISTRATORS" ' +
    '/TCPENABLED=0 /NPENABLED=0 ' +
    '/IACCEPTSQLSERVERLICENSETERMS /UPDATEENABLED=False';

  if not Exec(
      ExpandConstant('{tmp}\SQLEXPR_x64_ENU.exe'),
      Parameters,
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode) then
  begin
    RaiseException(
      'SQL Server Express could not be started. Setup cannot continue.');
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    RaiseException(
      Format(
        'SQL Server Express installation failed with exit code %d. ' +
        'Review the Advanced POS installer log before retrying.', [ResultCode]));
  end;

  if ResultCode = 3010 then
  begin
    SqlExpressRestartRequired := True;
    Log('SQL Server Express requested a Windows restart (exit code 3010).');
  end;

  if not SqlExpressServiceExists() then
  begin
    RaiseException(
      'SQL Server Express installation finished but the SQLEXPRESS service ' +
      'was not created. Setup cannot continue.');
  end;
end;
#endif

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
#ifndef SqlExpressInstaller
  if not SqlExpressServiceExists() then
  begin
    Result :=
      'This Server installer does not contain SQL Server Express and the ' +
      'SQLEXPRESS instance is not installed. Use the full production Server ' +
      'installer that includes the approved SQL Server Express prerequisite.';
  end;
#endif
end;
function NeedRestart(): Boolean;
begin
  Result := SqlExpressRestartRequired;
end;
