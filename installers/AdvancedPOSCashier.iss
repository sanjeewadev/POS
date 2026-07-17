#ifndef SourceRoot
  #error SourceRoot must be supplied by Build-POS-Production-Installers.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by Build-POS-Production-Installers.ps1
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "Advanced POS Cashier"
#define PublisherName "Advanced POS"
#define AppExeName "POS.Cashier.UI.exe"

[Setup]
AppId={{7A34BE87-6A8F-4A91-A12B-11D000000002}
AppName={#AppName}
AppVersion={#AppVersion}
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
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes

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
Name: "{group}\Configure Advanced POS Cashier"; Filename: "{app}\DeploymentWizard\POS.Deployment.Wizard.exe"; Parameters: "--mode cashier --install-root ""{app}"""; WorkingDir: "{app}"

[Run]
Filename: "{app}\DeploymentWizard\POS.Deployment.Wizard.exe"; Parameters: "--mode cashier --install-root ""{app}"""; WorkingDir: "{app}"; Description: "Configure this Cashier terminal"; StatusMsg: "Opening Advanced POS Cashier configuration..."; Flags: waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Temp"
