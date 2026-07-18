using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services.Licensing;

namespace POS.Cashier.AuditTests;

internal static class LicenseRecoverySourcePolicyAuditTests
{
    public static async Task CashierLicenceRecoveryAndUpgradeAreControlledAsync()
    {
        string cashierApp = Read(
            "POS.Cashier.UI",
            "App.xaml.cs");

        AuditAssert.Contains(
            cashierApp,
            "CashierLicenseRecoveryDialog",
            "licence failure opens the dedicated Cashier recovery window");
        AuditAssert.Contains(
            cashierApp,
            "\"--activate\"",
            "Cashier activation-only startup mode");
        AuditAssert.Contains(
            cashierApp,
            "!summary.CanRunCashier",
            "Cashier remains blocked until licence validation succeeds");
        AuditAssert.False(
            cashierApp.Contains(
                "throw new InvalidOperationException(\n                    summary.StatusMessage",
                StringComparison.Ordinal),
            "Cashier closes instead of presenting licence recovery");

        string recoveryWindow = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "CashierLicenseRecoveryDialog.xaml");
        AuditAssert.Contains(
            recoveryWindow,
            "COPY MACHINE CODE",
            "machine-code recovery action");
        AuditAssert.Contains(
            recoveryWindow,
            "COPY LICENCE REQUEST",
            "clipboard licence-request action");
        AuditAssert.Contains(
            recoveryWindow,
            "SAVE LICENCE REQUEST",
            "offline licence-request file action");
        AuditAssert.Contains(
            recoveryWindow,
            "IMPORT TERMINAL LICENCE",
            "direct terminal-licence import action");
        AuditAssert.Contains(
            recoveryWindow,
            "CONTINUE TO CASHIER",
            "post-import continuation without application restart");
        AuditAssert.Contains(
            recoveryWindow,
            "WindowStartupLocation=\"Manual\"",
            "licence recovery uses the registered monitor-aware placement service");
        AuditAssert.False(
            recoveryWindow.Contains(
                "WindowStartupLocation=\"CenterScreen\"",
                StringComparison.Ordinal),
            "licence recovery must not bypass controlled Cashier window placement");

        string recoveryCode = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "CashierLicenseRecoveryDialog.xaml.cs");
        AuditAssert.Contains(
            recoveryCode,
            "ImportTerminalLicenseFileAsync",
            "terminal-only licence import path");
        AuditAssert.Contains(
            recoveryCode,
            "BuildRequestText",
            "complete terminal licence request generation");
        AuditAssert.Contains(
            recoveryCode,
            "LocalLogService.WriteException",
            "technical recovery logging");
        AuditAssert.False(
            recoveryCode.Contains(
                "MessageBox.Show(ex.ToString",
                StringComparison.Ordinal),
            "raw activation exception is displayed to the customer");

        string licenseManager = Read(
            "POS.Core",
            "Services",
            "Licensing",
            "LicenseManagerService.cs");
        AuditAssert.Contains(
            licenseManager,
            "GetCurrentTerminalLicenseRequestAsync",
            "current-machine licence request service");
        AuditAssert.Contains(
            licenseManager,
            "ImportTerminalLicenseFileAsync",
            "Cashier terminal-only import API");
        AuditAssert.Contains(
            licenseManager,
            "Store licences must be imported from BackOffice",
            "Cashier cannot import the store licence");

        string licenseRepository = Read(
            "POS.Core",
            "Repositories",
            "LicenseRepository.cs");
        AuditAssert.Contains(
            licenseRepository,
            "existingIdentical",
            "repeated licence import idempotence");
        AuditAssert.Contains(
            licenseRepository,
            "existingIdentical.LastVerifiedAt",
            "same licence revalidation without duplicate replacement");

        string terminalManagement = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "TerminalManagementViewModel.cs");
        AuditAssert.Contains(
            terminalManagement,
            "CopyLicenseRequestAsync",
            "BackOffice remote terminal licence request copying");
        AuditAssert.Contains(
            terminalManagement,
            "SelectedTerminal.MachineCode",
            "registered remote machine code is used in the request");

        string cashierInstaller = Read(
            "installers",
            "AdvancedPOSCashier.iss");
        AuditAssert.Contains(
            cashierInstaller,
            "Activate Advanced POS Cashier",
            "installed Cashier activation shortcut");
        AuditAssert.Contains(
            cashierInstaller,
            "Upgrade or repair Advanced POS Cashier",
            "in-place Cashier maintenance page");
        AuditAssert.Contains(
            cashierInstaller,
            "ShouldRunCashierConfiguration",
            "upgrade preserves configuration unless reconfiguration is selected");

        string serverInstaller = Read(
            "installers",
            "AdvancedPOSServer.iss");
        AuditAssert.Contains(
            serverInstaller,
            "Upgrade or repair Advanced POS Server",
            "in-place Server maintenance page");
        AuditAssert.Contains(
            serverInstaller,
            "ShouldRunServerConfiguration",
            "Server upgrade preserves the existing database configuration");
        AuditAssert.Contains(
            serverInstaller,
            "Activate Advanced POS Cashier",
            "optional server Cashier activation shortcut");

        string configurationLauncher = Read(
            "POS.Cashier.UI",
            "Services",
            "CashierConfigurationLauncher.cs");
        AuditAssert.Contains(
            configurationLauncher,
            "using POS.Core.Services;",
            "Cashier configuration logging-service import");
        AuditAssert.Contains(
            configurationLauncher,
            "LocalLogService.WriteException",
            "Cashier configuration technical logging");

        string releaseInfo = Read(
            "POS.Core",
            "Configuration",
            "ProductReleaseInfo.cs");
        AuditAssert.Contains(
            releaseInfo,
            "ProductVersion = \"1.0.1\"",
            "installed-system recovery release version");

        LicenseRequestInfo request =
            LicenseRequestService.CreateTerminalRequest(
                "STORE-001",
                "Audit Store",
                "01",
                "Main Cashier",
                "AUDIT-PC",
                "AUDIT-MACHINE-CODE",
                LicenseStatus.ExpiredReadOnly,
                new DateTime(2026, 7, 17));

        string requestText = request.BuildRequestText();

        AuditAssert.Contains(
            requestText,
            "Store ID: STORE-001",
            "licence request Store ID");
        AuditAssert.Contains(
            requestText,
            "Terminal Number: 01",
            "licence request terminal number");
        AuditAssert.Contains(
            requestText,
            "Machine Code: AUDIT-MACHINE-CODE",
            "licence request machine code");
        AuditAssert.Contains(
            requestText,
            "Current Licence Status: Expired",
            "licence request current status");
        AuditAssert.Equal(
            "POS_Terminal_01_Licence_Request_AUDIT-PC.txt",
            request.BuildSuggestedFileName(),
            "offline licence request filename");

        using var factory = new AuditDbContextFactory();
        var repository = new LicenseRepository(factory);

        InstalledLicense firstImport =
            await repository.ImportLicenseAsync(
                CreateAuditTerminalLicense(),
                "Audit first import");

        InstalledLicense repeatedImport =
            await repository.ImportLicenseAsync(
                CreateAuditTerminalLicense(),
                "Audit repeated import");

        AuditAssert.Equal(
            firstImport.Id,
            repeatedImport.Id,
            "repeated terminal licence import ID");

        using var context = factory.CreateDbContext();

        AuditAssert.Equal(
            1,
            context.InstalledLicenses.Count(
                license =>
                    license.LicenseId ==
                        "AUDIT-TERMINAL-LICENCE"),
            "repeated terminal licence database records");

        AuditAssert.Equal(
            "Audit repeated import",
            context.InstalledLicenses
                .Single(license =>
                    license.LicenseId ==
                        "AUDIT-TERMINAL-LICENCE")
                .ImportedBy,
            "repeated terminal licence verification identity");
    }

    private static InstalledLicense CreateAuditTerminalLicense()
    {
        return new InstalledLicense
        {
            LicenseId = "AUDIT-TERMINAL-LICENCE",
            LicenseType = LicenseType.TerminalLicense,
            LicenseStatus = LicenseStatus.Active,
            StoreId = "STORE-001",
            StoreName = "Audit Store",
            TerminalNo = "01",
            MachineCode = "AUDIT-MACHINE-CODE",
            IssuedOn = new DateTime(2026, 1, 1),
            ExpiresOn = new DateTime(2027, 1, 1),
            RawLicenseJson = "{}",
            Signature = "AUDIT-SIGNATURE",
            IsActive = true
        };
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
