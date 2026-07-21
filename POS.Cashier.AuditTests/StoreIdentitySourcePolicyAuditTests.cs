using POS.Core.Models.Licensing;
using POS.Core.Services.Licensing;

namespace POS.Cashier.AuditTests;

internal static class StoreIdentitySourcePolicyAuditTests
{
    public static Task StoreIdentityAndLicenceRequestsAreConsistentAsync()
    {
        VerifyRequestModels();
        VerifyCurrentStoreIdentityFeedsLicensing();
        VerifyBackOfficeIdentityUi();
        VerifyStoreSettingsIsAuthoritative();
        VerifyBarcodeStoreNameIsReadOnly();

        return Task.CompletedTask;
    }

    private static void VerifyRequestModels()
    {
        LicenseRequestInfo storeRequest =
            LicenseRequestService.CreateStoreRequest(
                string.Empty,
                "Current Trading Name",
                "Current Legal Name (Pvt) Ltd",
                LicenseStatus.Missing,
                null);

        string storeText = storeRequest.BuildRequestText();

        AuditAssert.Contains(
            storeText,
            "Request Type: Store License",
            "Store request type");
        AuditAssert.Contains(
            storeText,
            "Store Name: Current Trading Name",
            "Store request trading name");
        AuditAssert.Contains(
            storeText,
            "Legal Name: Current Legal Name (Pvt) Ltd",
            "Store request legal name");
        AuditAssert.False(
            storeText.Contains("Terminal Number:", StringComparison.Ordinal),
            "Store request must not contain terminal identity fields.");

        var summary = new LicenseSummary
        {
            StoreId = "STORE-ABC123",
            CurrentStoreName = "Current Trading Name",
            CurrentLegalName = "Current Legal Name (Pvt) Ltd",
            CurrentTerminalNo = "02",
            CurrentTerminalName = "Front Counter",
            CurrentMachineName = "CASHIER-02",
            CurrentMachineCode = "MACHINE-CODE",
            TerminalLicenseStatus = LicenseStatus.Active,
            TerminalExpiryDate = new DateTime(2027, 7, 21)
        };

        LicenseRequestInfo terminalRequest =
            LicenseRequestService.CreateCurrentTerminalRequest(summary);

        string terminalText = terminalRequest.BuildRequestText();

        AuditAssert.Contains(
            terminalText,
            "Request Type: Terminal License",
            "Terminal request type");
        AuditAssert.Contains(
            terminalText,
            "Store Name: Current Trading Name",
            "Terminal request current trading name");
        AuditAssert.Contains(
            terminalText,
            "Terminal Number: 02",
            "Terminal request number");
        AuditAssert.Contains(
            terminalText,
            "Machine Code: MACHINE-CODE",
            "Terminal request machine code");
    }

    private static void VerifyCurrentStoreIdentityFeedsLicensing()
    {
        string summary = Read(
            "POS.Core",
            "Models",
            "Licensing",
            "LicenseSummary.cs");
        string manager = Read(
            "POS.Core",
            "Services",
            "Licensing",
            "LicenseManagerService.cs");
        string terminalManagement = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "TerminalManagementViewModel.cs");

        AuditAssert.Contains(
            summary,
            "public string CurrentStoreName",
            "Current operational store name");
        AuditAssert.Contains(
            summary,
            "public string LicensedStoreName",
            "Historical signed store name");
        AuditAssert.Contains(
            manager,
            "StoreSettingsRepository",
            "Licence summary Store Settings dependency");
        AuditAssert.Contains(
            manager,
            "storeSettings?.StoreName",
            "Current trading name resolution");
        AuditAssert.Contains(
            manager,
            "LicensedStoreName =",
            "Signed store name mapping");
        AuditAssert.Contains(
            terminalManagement,
            "storeSummary.CurrentStoreName",
            "Terminal Management current store name request");
    }

    private static void VerifyBackOfficeIdentityUi()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Admin",
            "LicenseManagementView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "LicenseManagementViewModel.cs");

        AuditAssert.Contains(
            xaml,
            "Text=\"Current Store Name\"",
            "Current Store Name label");
        AuditAssert.Contains(
            xaml,
            "Text=\"Licensed Store Name\"",
            "Licensed Store Name label");
        AuditAssert.Contains(
            xaml,
            "Command=\"{Binding CopyStoreLicenseRequestCommand}\"",
            "Store licence request action");
        AuditAssert.Contains(
            xaml,
            "Command=\"{Binding CopyTerminalLicenseRequestCommand}\"",
            "Terminal licence request action");
        AuditAssert.Contains(
            viewModel,
            "GetCurrentStoreLicenseRequestAsync",
            "Store request service call");
        AuditAssert.Contains(
            viewModel,
            "GetCurrentTerminalLicenseRequestAsync",
            "Terminal request service call");
    }


    private static void VerifyStoreSettingsIsAuthoritative()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Admin",
            "StoreSettingsView.xaml");

        AuditAssert.Contains(
            xaml,
            "This page is the authoritative store identity.",
            "Store Settings identity guidance");
        AuditAssert.Contains(
            xaml,
            "new licence requests",
            "Store Settings licence-request guidance");
    }

    private static void VerifyBarcodeStoreNameIsReadOnly()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "BarcodePrinterView.xaml");

        AuditAssert.Contains(
            xaml,
            "Text=\"{Binding PrintConfig.StoreName, Mode=OneWay}\"",
            "Barcode configured store-name OneWay binding");
        AuditAssert.Contains(
            xaml,
            "IsReadOnly=\"True\"",
            "Barcode configured store name is read-only");
        AuditAssert.False(
            xaml.Contains(
                "PrintConfig.StoreName, Mode=TwoWay",
                StringComparison.Ordinal),
            "Barcode printer still exposes a separate editable store name.");
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
