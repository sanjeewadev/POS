using POS.Core.Models.Licensing;
using POS.Core.Services.Licensing;
using POS.Core.Utilities;

namespace POS.Cashier.AuditTests;

internal static class StoreIdentitySourcePolicyAuditTests
{
    public static Task StoreIdentityAndLicenceRequestsAreConsistentAsync()
    {
        VerifyRequestModels();
        VerifyCurrentStoreIdentityFeedsLicensing();
        VerifyBackOfficeIdentityUi();
        VerifyBackOfficeShellUsesCurrentStoreIdentity();
        VerifyStoreSettingsIsAuthoritative();
        VerifyGiftVoucherUsesActiveStoreIdentity();
        VerifyBarcodeStoreNameIsReadOnly();
        VerifyNoCustomerSpecificIdentityLiterals();

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
            "Command=\"{Binding CopySelectedTerminalLicenseRequestCommand}\"",
            "Selected terminal licence request action");
        AuditAssert.Contains(
            viewModel,
            "GetCurrentStoreLicenseRequestAsync",
            "Store request service call");
        AuditAssert.Contains(
            viewModel,
            "CreateTerminalRequest",
            "Selected terminal request service call");
    }


    private static void VerifyBackOfficeShellUsesCurrentStoreIdentity()
    {
        AuditAssert.Equal(
            "Advanced POS BackOffice",
            StoreIdentityDisplayFormatter.BuildBackOfficeTitle(
                string.Empty,
                string.Empty),
            "Neutral BackOffice title");

        AuditAssert.Equal(
            "Advanced POS BackOffice — Current Trading Name",
            StoreIdentityDisplayFormatter.BuildBackOfficeTitle(
                " Current Trading Name ",
                "Current Legal Name (Pvt) Ltd"),
            "Trading-name BackOffice title");

        AuditAssert.Equal(
            "Advanced POS BackOffice — Current Legal Name (Pvt) Ltd",
            StoreIdentityDisplayFormatter.BuildBackOfficeTitle(
                string.Empty,
                " Current Legal Name (Pvt) Ltd "),
            "Legal-name BackOffice title fallback");

        string shell = Read(
            "POS.BackOffice.UI",
            "Views",
            "Layout",
            "ManagementShellView.xaml");
        string mainViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "MainViewModel.cs");
        string app = Read(
            "POS.BackOffice.UI",
            "App.xaml.cs");
        string settingsViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "StoreSettingsViewModel.cs");

        AuditAssert.Contains(
            shell,
            "Title=\"{Binding ApplicationTitleText, Mode=OneWay}\"",
            "Dynamic BackOffice title binding");
        AuditAssert.Contains(
            mainViewModel,
            "StoreSettingsRepository",
            "BackOffice title Store Settings dependency");
        AuditAssert.Contains(
            mainViewModel,
            "RefreshStoreIdentityAsync",
            "BackOffice title initialization");
        AuditAssert.Contains(
            mainViewModel,
            "StoreIdentityDisplayFormatter.BuildBackOfficeTitle",
            "BackOffice title formatter");
        AuditAssert.Contains(
            app,
            "await mainViewModel.RefreshStoreIdentityAsync();",
            "BackOffice startup store identity load");
        AuditAssert.Contains(
            settingsViewModel,
            "_mainViewModel.ApplyStoreIdentity",
            "Live BackOffice title refresh after Store Settings save");
    }

    private static void VerifyGiftVoucherUsesActiveStoreIdentity()
    {
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GiftVoucherAdminViewModel.cs");

        AuditAssert.Contains(
            viewModel,
            "StoreSettingsRepository",
            "Gift Voucher Store Settings dependency");
        AuditAssert.Contains(
            viewModel,
            "_storeSettingsRepository.GetActiveAsync()",
            "Gift Voucher active store identity lookup");
        AuditAssert.False(
            viewModel.Contains(
                "context.StoreSettings.AsNoTracking().FirstOrDefaultAsync()",
                StringComparison.Ordinal),
            "Gift Voucher printing still selects an arbitrary Store Settings row.");
    }

    private static void VerifyNoCustomerSpecificIdentityLiterals()
    {
        string[] sourceRoots =
        {
            "POS.BackOffice.UI",
            "POS.Cashier.UI",
            "POS.Core",
            "POS.Database.Setup",
            "POS.Deployment.Wizard"
        };

        string[] prohibitedTerms =
        {
            "KUMARA",
            "KOTTAWA"
        };

        string[] sourceExtensions =
        {
            ".cs",
            ".xaml",
            ".ps1",
            ".iss"
        };

        foreach (string sourceRoot in sourceRoots)
        {
            string root = Path.Combine(
                AuditPaths.RepositoryRoot,
                sourceRoot);

            foreach (string file in Directory.EnumerateFiles(
                         root,
                         "*.*",
                         SearchOption.AllDirectories))
            {
                string relativePath =
                    Path.GetRelativePath(
                        AuditPaths.RepositoryRoot,
                        file);

                string[] pathSegments =
                    relativePath.Split(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

                if (pathSegments.Any(segment =>
                        string.Equals(
                            segment,
                            "bin",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            segment,
                            "obj",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!sourceExtensions.Contains(
                        Path.GetExtension(file),
                        StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text = File.ReadAllText(file);

                foreach (string prohibitedTerm in prohibitedTerms)
                {
                    AuditAssert.False(
                        text.Contains(
                            prohibitedTerm,
                            StringComparison.OrdinalIgnoreCase),
                        $"Customer-specific store identity literal '{prohibitedTerm}' remains in {relativePath}.");
                }
            }
        }
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
