#ifndef SourceRoot
  #error SourceRoot must be supplied by Build-POS-Production-Installers.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by Build-POS-Production-Installers.ps1
#endif
#ifndef AppVersion
  #define AppVersion "1.0.3"
#endif

#define AppName "Advanced POS Cashier"
#define PublisherName "Advanced POS"
#define AppExeName "POS.Cashier.UI.exe"

[Setup]
AppId={{7A34BE87-6A8F-4A91-A12B-11D000000002}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#PublisherName}
DefaultDirName={autopf}\Advanced POS\Cashier
DefaultGroupName=Advanced POS
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Advanced_POS_Cashier_Setup_{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Cashier\{#AppExeName}
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

[Tasks]
Name: "desktopcashier"; Description: "Create a Cashier desktop shortcut"; GroupDescription: "Desktop shortcuts:"; Flags: checkedonce

[Files]
Source: "{#SourceRoot}\Cashier\Cashier\*"; DestDir: "{app}\Cashier"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Cashier\DatabaseSetup\*"; DestDir: "{app}\DatabaseSetup"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Cashier\DeploymentWizard\*"; DestDir: "{app}\DeploymentWizard"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Cashier\Tools\*"; DestDir: "{app}\Tools"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Cashier\Docs\*"; DestDir: "{app}\Docs"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; WorkingDir: "{app}\Cashier"
Name: "{autodesktop}\Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; WorkingDir: "{app}\Cashier"; Tasks: desktopcashier
Name: "{group}\Activate Advanced POS Cashier"; Filename: "{app}\Cashier\POS.Cashier.UI.exe"; Parameters: "--activate"; WorkingDir: "{app}\Cashier"
Name: "{group}\Repair or Configure Advanced POS Cashier"; Filename: "{app}\DeploymentWizard\POS.Deployment.Wizard.exe"; Parameters: "--mode cashier --install-root ""{app}"""; WorkingDir: "{app}"

[Run]
Filename: "{app}\DeploymentWizard\POS.Deployment.Wizard.exe"; Parameters: "--mode cashier --install-root ""{app}"""; WorkingDir: "{app}"; Description: "Repair or configure this Cashier terminal"; StatusMsg: "Opening Advanced POS Cashier configuration..."; Flags: waituntilterminated; Check: ShouldRunCashierConfiguration

[InstallDelete]
Type: files; Name: "{group}\Configure Advanced POS Cashier.lnk"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Temp"
[Code]
var
  MaintenancePage: TInputOptionWizardPage;
  ExistingInstallationDetected: Boolean;

function HasExistingCashierInstallation(): Boolean;
begin
  Result :=
    RegKeyExists(
      HKLM64,
      'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7A34BE87-6A8F-4A91-A12B-11D000000002}_is1') or
    RegKeyExists(
      HKLM,
      'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7A34BE87-6A8F-4A91-A12B-11D000000002}_is1');
end;

procedure InitializeWizard();
begin
  ExistingInstallationDetected :=
    HasExistingCashierInstallation();

  if ExistingInstallationDetected then
  begin
    MaintenancePage := CreateInputOptionPage(
      wpWelcome,
      'Upgrade or repair Advanced POS Cashier',
      'An existing Cashier installation was detected.',
      'Choose how setup should handle the installed system. ' +
      'The database connection, terminal assignment, machine registration, ' +
      'licence, printer settings, and drawer settings are preserved.',
      True,
      False);

    MaintenancePage.Add(
      'Upgrade or repair application files and keep the current configuration');
    MaintenancePage.Add(
      'Upgrade application files and open Cashier configuration after setup');
    MaintenancePage.SelectedValueIndex := 0;
  end;
end;

function ShouldRunCashierConfiguration(): Boolean;
begin
  Result :=
    (not ExistingInstallationDetected) or
    ((MaintenancePage <> nil) and
     (MaintenancePage.SelectedValueIndex = 1));
end;
