namespace POS.Cashier.AuditTests;

internal static class SetupUpgradeRepairSourcePolicyAuditTests
{
    public static Task SetupUpgradeRepairAndWizardLayoutAreControlledAsync()
    {
        string setupProgram = Read(
            "POS.Database.Setup",
            "Program.cs");

        AuditAssert.Contains(
            setupProgram,
            "case \"inspect-server\"",
            "non-destructive existing-installation inspection command");
        AuditAssert.Contains(
            setupProgram,
            "case \"upgrade-existing\"",
            "existing-store upgrade or repair command");
        AuditAssert.Contains(
            setupProgram,
            "Creating a verified pre-upgrade safety backup",
            "pre-upgrade safety backup");
        AuditAssert.Contains(
            setupProgram,
            "Applying pending Advanced POS database migrations",
            "pending migration application");
        AuditAssert.Contains(
            setupProgram,
            "Creating or repairing the restricted application login",
            "restricted login repair");
        AuditAssert.Contains(
            setupProgram,
            "POS NETWORK EXISTING-STORE UPGRADE OR REPAIR PASSED",
            "successful upgrade or repair result");

        int upgradeMethod = setupProgram.IndexOf(
            "private static async Task UpgradeExistingAsync",
            StringComparison.Ordinal);
        int integrityCheck = setupProgram.IndexOf(
            "Checking the existing production database before upgrade",
            upgradeMethod,
            StringComparison.Ordinal);
        int preUpgradeBackup = setupProgram.IndexOf(
            "Creating a verified pre-upgrade safety backup",
            upgradeMethod,
            StringComparison.Ordinal);
        int applyMigrations = setupProgram.IndexOf(
            "Applying pending Advanced POS database migrations",
            upgradeMethod,
            StringComparison.Ordinal);
        int repairLogin = setupProgram.IndexOf(
            "Creating or repairing the restricted application login",
            upgradeMethod,
            StringComparison.Ordinal);

        AuditAssert.True(
            upgradeMethod >= 0 &&
            integrityCheck > upgradeMethod &&
            preUpgradeBackup > integrityCheck &&
            applyMigrations > preUpgradeBackup &&
            repairLogin > applyMigrations,
            "Upgrade or repair must verify integrity, back up, migrate, and then repair the login in that order.");

        string provisioning = Read(
            "POS.Database.Setup",
            "ServerProvisioningService.cs");

        foreach (string errorCode in new[]
                 {
                     "EXISTING_INSTALLATION_DETECTED",
                     "EXISTING_DATABASE_DETECTED",
                     "PARTIAL_SETUP_LOGIN_EXISTS",
                     "NO_EXISTING_INSTALLATION",
                     "PARTIAL_SETUP_DATABASE_MISSING",
                     "EMPTY_DATABASE_REQUIRES_REVIEW",
                     "DATABASE_IDENTITY_MISMATCH",
                     "DATABASE_NEWER_THAN_APPLICATION",
                     "SQL_SERVER_UNAVAILABLE",
                     "DATABASE_UNAVAILABLE"
                 })
        {
            AuditAssert.Contains(
                provisioning,
                errorCode,
                $"controlled setup state '{errorCode}'");
        }

        AuditAssert.Contains(
            provisioning,
            "InspectInstallationAsync",
            "existing database and login inspection");
        AuditAssert.Contains(
            provisioning,
            "IsAdvancedPosDatabase",
            "Advanced POS database identity verification");
        AuditAssert.Contains(
            provisioning,
            "DatabaseIsNewerThanApplication",
            "newer database downgrade refusal");
        string upgradeScope = Slice(
            setupProgram,
            "private static async Task UpgradeExistingAsync",
            "private static async Task ProvisionEmptyAsync");

        AuditAssert.False(
            upgradeScope.Contains("DROP DATABASE", StringComparison.OrdinalIgnoreCase),
            "Upgrade or repair must never drop the production database.");
        AuditAssert.False(
            upgradeScope.Contains("replaceExisting", StringComparison.Ordinal),
            "Upgrade or repair must not use the destructive fresh-provisioning replacement path.");

        string wizardXaml = Read(
            "POS.Deployment.Wizard",
            "MainWindow.xaml");

        AuditAssert.Contains(
            wizardXaml,
            "Tag=\"UpgradeRepair\"",
            "explicit Upgrade or Repair mode");
        AuditAssert.Contains(
            wizardXaml,
            "WindowStartupLocation=\"Manual\"",
            "work-area-aware wizard placement");
        AuditAssert.Contains(
            wizardXaml,
            "Grid.Row=\"2\"",
            "fixed footer row");
        AuditAssert.Contains(
            wizardXaml,
            "x:Name=\"RunButton\"",
            "fixed Start Setup button");
        AuditAssert.Contains(
            wizardXaml,
            "x:Name=\"CloseButton\"",
            "fixed Close button");
        AuditAssert.Contains(
            wizardXaml,
            "MinHeight=\"0\"",
            "scrollable body may shrink without hiding footer actions");

        string wizardCode = Read(
            "POS.Deployment.Wizard",
            "MainWindow.xaml.cs");

        AuditAssert.Contains(
            wizardCode,
            "FitWindowToWorkArea",
            "wizard work-area sizing");
        AuditAssert.Contains(
            wizardCode,
            "SystemParameters.WorkArea",
            "desktop work-area boundary");
        AuditAssert.Contains(
            wizardCode,
            "DetectExistingServerInstallationAsync",
            "automatic existing-installation detection");
        AuditAssert.Contains(
            wizardCode,
            "SelectInstallMode(ServerInstallMode.UpgradeRepair)",
            "automatic Upgrade or Repair selection");
        AuditAssert.Contains(
            wizardCode,
            "ValidateServerModeAgainstInspection",
            "pre-modification mode and state validation");
        AuditAssert.Contains(
            wizardCode,
            "upgrade-existing",
            "wizard upgrade or repair execution");
        AuditAssert.Contains(
            wizardCode,
            "Existing business data and licences were preserved",
            "successful data-preservation confirmation");

        string serverInstaller = Read(
            "installers",
            "AdvancedPOSServer.iss");
        string cashierInstaller = Read(
            "installers",
            "AdvancedPOSCashier.iss");

        AuditAssert.Contains(
            serverInstaller,
            "AppId={{7A34BE87-6A8F-4A91-A12B-11D000000001}",
            "permanent Server installer identity");
        AuditAssert.Contains(
            cashierInstaller,
            "AppId={{7A34BE87-6A8F-4A91-A12B-11D000000002}",
            "permanent Cashier installer identity");
        AuditAssert.Contains(
            serverInstaller,
            "#define AppVersion \"1.0.3\"",
            "Server setup hardening version");
        AuditAssert.Contains(
            cashierInstaller,
            "#define AppVersion \"1.0.3\"",
            "Cashier setup hardening version");
        AuditAssert.Contains(
            serverInstaller,
            "Configure, Upgrade or Repair Advanced POS Server",
            "installed Server maintenance shortcut");

        string releaseInfo = Read(
            "POS.Core",
            "Configuration",
            "ProductReleaseInfo.cs");

        AuditAssert.Contains(
            releaseInfo,
            "ProductVersion = \"1.0.3\"",
            "setup and upgrade hardening release version");

        return Task.CompletedTask;
    }

    private static string Slice(
        string text,
        string startMarker,
        string endMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException(
                $"Start marker was not found: {startMarker}");
        }

        int end = text.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException(
                $"End marker was not found: {endMarker}");
        }

        return text[start..end];
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
