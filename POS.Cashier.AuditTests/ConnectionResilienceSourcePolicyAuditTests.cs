namespace POS.Cashier.AuditTests;

internal static class ConnectionResilienceSourcePolicyAuditTests
{
    public static Task StoreConnectionRecoveryIsControlledAsync()
    {
        string initialization = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseInitializationService.cs");

        AuditAssert.Contains(
            initialization,
            "SqlServerStartupAttemptCount = 3",
            "controlled SQL Server startup retry count");
        AuditAssert.Contains(
            initialization,
            "Task.Delay",
            "delay between SQL Server startup retries");
        AuditAssert.Contains(
            initialization,
            "Use Repair Connection",
            "connection-change recovery guidance");

        string cashierApp = Read(
            "POS.Cashier.UI",
            "App.xaml.cs");

        AuditAssert.Contains(
            cashierApp,
            "EnsureDatabaseAvailableAsync",
            "Cashier database recovery gate");
        AuditAssert.Contains(
            cashierApp,
            "DatabaseConnectionRecoveryDialog",
            "Cashier startup recovery window");
        AuditAssert.Contains(
            cashierApp,
            "ConnectionRestored",
            "successful retry confirmation");
        AuditAssert.Contains(
            cashierApp,
            "LocalServerConnectionProfileRecovery",
            "same-computer legacy IP recovery gate");

        string backOfficeApp = Read(
            "POS.BackOffice.UI",
            "App.xaml.cs");

        AuditAssert.Contains(
            backOfficeApp,
            "LocalServerConnectionProfileRecovery",
            "BackOffice same-computer legacy IP recovery gate");
        AuditAssert.Contains(
            backOfficeApp,
            "Configure Advanced POS Server",
            "BackOffice repair guidance");

        string localRecovery = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "LocalServerConnectionProfileRecovery.cs");

        AuditAssert.Contains(
            localRecovery,
            "deployment.server.json",
            "verified local Server deployment record");
        AuditAssert.Contains(
            localRecovery,
            "ServerHost = \"localhost\"",
            "same-computer endpoint repair");
        AuditAssert.Contains(
            localRecovery,
            "POS_DISABLE_LOCAL_SERVER_PROFILE_RECOVERY",
            "developer override for local endpoint repair");
        AuditAssert.Contains(
            localRecovery,
            "IsValidServerInstallation",
            "local Server installation validation");

        string recoveryXaml = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "DatabaseConnectionRecoveryDialog.xaml");

        foreach (string action in new[]
                 {
                     "RETRY CONNECTION",
                     "REPAIR CONNECTION",
                     "SAVE DIAGNOSTICS",
                     "RESTART CASHIER",
                     "EXIT CASHIER"
                 })
        {
            AuditAssert.Contains(
                recoveryXaml,
                action,
                $"Cashier connection recovery action '{action}'");
        }

        AuditAssert.Contains(
            recoveryXaml,
            "WindowStartupLocation=\"Manual\"",
            "monitor-aware connection recovery placement");
        AuditAssert.False(
            recoveryXaml.Contains(
                "WindowStartupLocation=\"CenterScreen\"",
                StringComparison.Ordinal),
            "connection recovery window bypasses monitor-aware placement");

        string diagnostics = Read(
            "POS.Cashier.UI",
            "Services",
            "CashierConnectionDiagnosticsService.cs");

        AuditAssert.Contains(
            diagnostics,
            "TcpClient",
            "Cashier TCP diagnostic test");
        AuditAssert.Contains(
            diagnostics,
            "GetHostAddressesAsync",
            "Cashier server-name resolution diagnostic");
        AuditAssert.Contains(
            diagnostics,
            "Password: [not written to diagnostics]",
            "diagnostic password redaction notice");
        AuditAssert.False(
            diagnostics.Contains(
                "settings.Password",
                StringComparison.Ordinal),
            "Cashier diagnostics write the SQL password");

        string launcher = Read(
            "POS.Cashier.UI",
            "Services",
            "CashierConfigurationLauncher.cs");

        AuditAssert.Contains(
            launcher,
            "repair or configuration option",
            "missing-tool repair guidance");
        AuditAssert.Contains(
            launcher,
            "Environment.SpecialFolder.ProgramFiles",
            "installed configuration-tool fallback lookup");
        AuditAssert.Contains(
            launcher,
            "Restart Cashier",
            "profile reload guidance after repair");

        string wizard = Read(
            "POS.Deployment.Wizard",
            "MainWindow.xaml.cs");

        AuditAssert.Contains(
            wizard,
            "ServerHostTextBox.Text = \"localhost\"",
            "same-computer server profile uses localhost");
        AuditAssert.Contains(
            wizard,
            "LoadExistingConnectionProfile",
            "repair wizard reloads the existing encrypted profile");
        AuditAssert.Contains(
            wizard,
            "POS Server computer name or IP address",
            "Cashier server-name input guidance");
        AuditAssert.Contains(
            wizard,
            "RecommendedCashierHost = Environment.MachineName",
            "server computer-name handover record");

        string networkScript = Read(
            "tools",
            "network",
            "Configure-POS-SqlServer-Network.ps1");

        AuditAssert.Contains(
            networkScript,
            "-StartupType Automatic",
            "automatic SQL Server service startup");
        AuditAssert.Contains(
            networkScript,
            "restart/60000/restart/60000/restart/60000",
            "SQL Server service recovery actions");
        AuditAssert.Contains(
            networkScript,
            "failureflag",
            "SQL Server non-crash failure recovery flag");
        AuditAssert.Contains(
            networkScript,
            "FailureActionsBase64",
            "SQL Server service recovery backup");

        string networkRestoreScript = Read(
            "tools",
            "network",
            "Restore-POS-SqlServer-Network.ps1");

        AuditAssert.Contains(
            networkRestoreScript,
            "FailureActionsBase64",
            "SQL Server service recovery restoration");

        string productionRestoreScript = Read(
            "tools",
            "deployment",
            "Restore-POS-Production.ps1");

        AuditAssert.Contains(
            productionRestoreScript,
            "--host localhost",
            "local production restore is independent of router IP changes");

        string serverStatusScript = Read(
            "tools",
            "deployment",
            "Show-POS-Server-Status.ps1");

        AuditAssert.Contains(
            serverStatusScript,
            "Test-NetConnection -ComputerName localhost",
            "local server status is independent of router IP changes");

        string serverInstaller = Read(
            "installers",
            "AdvancedPOSServer.iss");

        AuditAssert.Contains(
            serverInstaller,
            "Repair or Configure Advanced POS Cashier",
            "same-computer Cashier repair shortcut");
        AuditAssert.Contains(
            serverInstaller,
            "Configure Advanced POS Cashier.lnk",
            "stale Server-hosted Cashier shortcut cleanup");
        AuditAssert.Contains(
            serverInstaller,
            "#define AppVersion \"1.0.3\"",
            "Server resilience installer version");

        string cashierInstaller = Read(
            "installers",
            "AdvancedPOSCashier.iss");

        AuditAssert.Contains(
            cashierInstaller,
            "Repair or Configure Advanced POS Cashier",
            "installed Cashier repair shortcut");
        AuditAssert.Contains(
            cashierInstaller,
            "Configure Advanced POS Cashier.lnk",
            "stale Cashier shortcut cleanup");
        AuditAssert.Contains(
            cashierInstaller,
            "#define AppVersion \"1.0.3\"",
            "Cashier resilience installer version");

        string databaseSetup = Read(
            "POS.Database.Setup",
            "Program.cs");

        AuditAssert.Contains(
            databaseSetup,
            "Production {ProductReleaseInfo.ProductVersion}",
            "database setup utility release version");

        string releaseInfo = Read(
            "POS.Core",
            "Configuration",
            "ProductReleaseInfo.cs");

        AuditAssert.Contains(
            releaseInfo,
            "ProductVersion = \"1.0.3\"",
            "connection resilience release version");

        return Task.CompletedTask;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(
            new[] { AuditPaths.RepositoryRoot }
                .Concat(parts)
                .ToArray());

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Required source file was not found: {path}");
        }

        return File.ReadAllText(path);
    }
}
