namespace POS.Cashier.AuditTests;

internal static class ProductionDeploymentSourcePolicyAuditTests
{
    public static Task ProductionDeploymentIsControlledAsync()
    {
        string setupProgram = Read(
            "POS.Database.Setup",
            "Program.cs");

        foreach (string command in new[]
        {
            "provision-empty",
            "provision-restore",
            "configure-terminal",
            "import-license",
            "backup-copy",
            "status"
        })
        {
            AuditAssert.Contains(
                setupProgram,
                $"case \"{command}\"",
                $"production database command '{command}'");
        }

        AuditAssert.Contains(
            setupProgram,
            "POS_SETUP_APP_PASSWORD",
            "environment-only SQL application password handoff");
        string commandLine = Read(
            "POS.Database.Setup",
            "CommandLineArguments.cs");
        AuditAssert.Contains(
            commandLine,
            "Secret option --{name} is not accepted on the command line",
            "command-line secret rejection");
        AuditAssert.Contains(
            setupProgram,
            "confirm-destructive-restore",
            "explicit destructive restore confirmation");
        AuditAssert.Contains(
            setupProgram,
            "USER_MESSAGE:",
            "customer-safe database setup error channel");
        AuditAssert.Contains(
            setupProgram,
            "SetupFailureLog.Write",
            "technical setup failure logging");
        AuditAssert.False(
            setupProgram.Contains(
                "Console.Error.WriteLine(ex);",
                StringComparison.Ordinal),
            "raw setup stack trace is exposed to the customer dialog");

        string terminalService = Read(
            "POS.Database.Setup",
            "TerminalProvisioningService.cs");
        AuditAssert.Contains(
            terminalService,
            "IsolationLevel.Serializable",
            "serial terminal assignment");
        AuditAssert.Contains(
            terminalService,
            "context.RegisteredTerminals.AddAsync",
            "atomic machine registration during terminal installation");
        AuditAssert.Contains(
            terminalService,
            "WasAlreadyConfigured",
            "idempotent same-machine terminal configuration");
        AuditAssert.Contains(
            terminalService,
            "TERMINAL_ASSIGNED_TO_OTHER_COMPUTER",
            "controlled cross-computer terminal conflict");
        AuditAssert.Contains(
            terminalService,
            "WriteSqlServerProfile",
            "machine-local encrypted profile creation");

        string initialization = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseInitializationService.cs");
        AuditAssert.Contains(
            initialization,
            "ProductReleaseInfo.RequiredSqlServerMigration",
            "release-to-schema compatibility check");
        AuditAssert.Contains(
            initialization,
            "Run the matching server setup or upgrade utility",
            "controlled incompatible-schema failure");

        foreach (string wizardSource in new[]
        {
            "App.xaml.cs",
            "MainWindow.xaml.cs",
            "DeploymentSettingsWriter.cs",
            "SetupProcessRunner.cs"
        })
        {
            string source = Read(
                "POS.Deployment.Wizard",
                wizardSource);

            AuditAssert.Contains(
                source,
                "using System.IO;",
                $"explicit System.IO import in {wizardSource}");
        }

        string wizard = Read(
            "POS.Deployment.Wizard",
            "MainWindow.xaml.cs");
        AuditAssert.Contains(
            wizard,
            "POS_SETUP_APP_PASSWORD",
            "masked installer password handoff");
        AuditAssert.False(
            wizard.Contains("--app-password", StringComparison.Ordinal),
            "production wizard exposes the SQL password on the command line");
        AuditAssert.Contains(
            wizard,
            "configure-terminal",
            "Cashier machine binding workflow");
        AuditAssert.Contains(
            wizard,
            "import-license",
            "installer licence import workflow");
        AuditAssert.Contains(
            wizard,
            "_serverCashierSelected",
            "installer-selected server Cashier role is authoritative");
        AuditAssert.False(
            wizard.Contains(
                "bool cashierInstalled = File.Exists",
                StringComparison.Ordinal),
            "stale Cashier files control the server terminal role");
        AuditAssert.Contains(
            wizard,
            "CASHIER TERMINAL CONFIGURATION ALREADY COMPLETE",
            "safe repeated Cashier configuration result");
        AuditAssert.Contains(
            wizard,
            "WriteWizardFailureLog",
            "deployment wizard technical logging");

        string serverInstaller = Read(
            "installers",
            "AdvancedPOSServer.iss");
        AuditAssert.Contains(
            serverInstaller,
            "Advanced_POS_Server_Setup_",
            "server setup output");
        AuditAssert.Contains(
            serverInstaller,
            "Server, BackOffice, and Cashier on this computer",
            "optional server Cashier component");
        AuditAssert.Contains(
            serverInstaller,
            "POS.Deployment.Wizard.exe",
            "guided server deployment wizard");
        AuditAssert.Contains(
            serverInstaller,
            "--server-cashier {code:GetServerCashierArgument}",
            "explicit server role handoff to the deployment wizard");
        AuditAssert.Contains(
            serverInstaller,
            "AfterInstall: InstallSqlExpressIfNeeded",
            "checked SQL Server Express bootstrap");
        AuditAssert.Contains(
            serverInstaller,
            "ResultCode = 3010",
            "SQL Server prerequisite restart handling");
        AuditAssert.Contains(
            serverInstaller,
            "IsServerOnlyInstall",
            "stale server Cashier component cleanup");
        AuditAssert.False(
            serverInstaller.Contains(
                "delete production database",
                StringComparison.OrdinalIgnoreCase),
            "server uninstall contains a production-database deletion action");

        string cashierInstaller = Read(
            "installers",
            "AdvancedPOSCashier.iss");
        AuditAssert.Contains(
            cashierInstaller,
            "Advanced_POS_Cashier_Setup_",
            "Cashier setup output");
        AuditAssert.Contains(
            cashierInstaller,
            "POS.Deployment.Wizard.exe",
            "guided Cashier deployment wizard");
        AuditAssert.False(
            cashierInstaller.Contains(
                "SQLEXPR",
                StringComparison.OrdinalIgnoreCase),
            "Cashier installer contains a SQL Server prerequisite");

        string networkScript = Read(
            "tools",
            "network",
            "Configure-POS-SqlServer-Network.ps1");
        AuditAssert.Contains(
            networkScript,
            "-RemoteAddress LocalSubnet",
            "private local-subnet SQL firewall restriction");
        AuditAssert.Contains(
            networkScript,
            "-Profile Private",
            "Private-profile SQL firewall restriction");
        AuditAssert.Contains(
            networkScript,
            "$majorVersion -ne 16",
            "tested SQL Server 2022 major-version gate");

        string builder = Read(
            "tools",
            "deployment",
            "Build-POS-Production-Installers.ps1");
        AuditAssert.Contains(
            builder,
            "Test-PowerShellSources",
            "PowerShell parser gate");
        AuditAssert.Contains(
            builder,
            "Core regression suite",
            "core regression gate");
        AuditAssert.Contains(
            builder,
            "SQL Server Cashier audit suite",
            "SQL Server regression gate");
        AuditAssert.Contains(
            builder,
            "SHA256SUMS.txt",
            "installer hash manifest");
        AuditAssert.Contains(
            builder,
            "Resolve-SqlServerExpressInstaller",
            "mandatory SQL Server Express prerequisite resolution");
        AuditAssert.Contains(
            builder,
            "Get-AuthenticodeSignature",
            "Microsoft prerequisite signature validation");
        AuditAssert.Contains(
            builder,
            "CustomerReadyServerInstaller",
            "release manifest customer-readiness flag");

        string licenseManager = Read(
            "POS.Core",
            "Services",
            "Licensing",
            "LicenseManagerService.cs");
        AuditAssert.Contains(
            licenseManager,
            "GetByMachineNameAsync",
            "non-creating terminal lookup during BackOffice licence checks");
        AuditAssert.False(
            licenseManager.Contains(
                "GetOrCreateForCurrentMachineAsync",
                StringComparison.Ordinal),
            "BackOffice licence check silently reserves Terminal 01");

        string terminalSettingsViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "TerminalSettingsViewModel.cs");
        AuditAssert.Contains(
            terminalSettingsViewModel,
            "GetByMachineNameAsync",
            "non-creating BackOffice Terminal Settings lookup");
        AuditAssert.Contains(
            terminalSettingsViewModel,
            "This computer is not assigned as a Cashier terminal",
            "server-only Terminal Settings guidance");
        AuditAssert.False(
            terminalSettingsViewModel.Contains(
                "GetOrCreateForCurrentMachineAsync",
                StringComparison.Ordinal),
            "BackOffice Terminal Settings silently reserves Terminal 01");

        string terminalRepository = Read(
            "POS.Core",
            "Repositories",
            "TerminalManagementRepository.cs");
        AuditAssert.Contains(
            terminalRepository,
            "ReleaseMachineAssignmentAsync",
            "controlled terminal machine release");
        AuditAssert.Contains(
            terminalRepository,
            "shift.Status == \"Open\"",
            "open-shift release block");
        AuditAssert.Contains(
            terminalRepository,
            "CashierCartStatusCodes.Held",
            "held-cart release block");

        string terminalViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "TerminalManagementViewModel.cs");
        AuditAssert.Contains(
            terminalViewModel,
            "ReleaseMachineAssignmentAsync",
            "Administrator terminal-release command");

        foreach (string tool in new[]
        {
            "Backup-POS-Production.ps1",
            "Restore-POS-Production.ps1",
            "Check-POS-Production.ps1",
            "Show-POS-Server-Status.ps1"
        })
        {
            string path = Path.Combine(
                AuditPaths.RepositoryRoot,
                "tools",
                "deployment",
                tool);

            AuditAssert.True(
                File.Exists(path),
                $"production recovery tool is missing: {tool}");
        }

        return Task.CompletedTask;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(
            new[] { AuditPaths.RepositoryRoot }
                .Concat(parts)
                .ToArray());

        return File.ReadAllText(path);
    }
}
