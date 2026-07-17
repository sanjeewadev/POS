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

        string terminalService = Read(
            "POS.Database.Setup",
            "TerminalProvisioningService.cs");
        AuditAssert.Contains(
            terminalService,
            "IsolationLevel.Serializable",
            "serial terminal assignment");
        AuditAssert.Contains(
            terminalService,
            "RegisterOrUpdateCurrentMachineAsync",
            "machine registration during terminal installation");
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
