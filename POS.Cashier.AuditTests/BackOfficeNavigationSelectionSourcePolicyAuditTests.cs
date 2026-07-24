using System.Xml.Linq;

namespace POS.Cashier.AuditTests;

internal static class BackOfficeNavigationSelectionSourcePolicyAuditTests
{
    private static readonly XNamespace PresentationNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    public static Task NavigationAndSelectionAreOrganizedAsync()
    {
        VerifyOwnerFriendlyNavigation();
        VerifyReadableSelectionTheme();

        return Task.CompletedTask;
    }

    private static void VerifyOwnerFriendlyNavigation()
    {
        XDocument document = LoadXaml(
            "POS.BackOffice.UI",
            "Views",
            "Layout",
            "ManagementShellView.xaml");

        XElement menu = document
            .Descendants(PresentationNamespace + "Menu")
            .Single();

        XElement[] topLevelItems = menu
            .Elements(PresentationNamespace + "MenuItem")
            .ToArray();

        var expectedTopLevel = new[]
        {
            new ExpectedTopLevelMenu(
                "System",
                null,
                new[]
                {
                    new ExpectedMenuItem("Cashier Terminal Settings", "NavigateToTerminalSettingsCommand"),
                    new ExpectedMenuItem("Database Backup / Restore", "NavigateToBackupRestoreCommand"),
                    new ExpectedMenuItem("Exit", "ExitApplicationCommand")
                }),
            new ExpectedTopLevelMenu(
                "Dashboard",
                "NavigateToDashboardCommand",
                Array.Empty<ExpectedMenuItem>()),
            new ExpectedTopLevelMenu(
                "Products",
                null,
                new[]
                {
                    new ExpectedMenuItem("Item Master", "NavigateToItemMasterCommand"),
                    new ExpectedMenuItem("Selling Prices", "NavigateToPriceManagementCommand"),
                    new ExpectedMenuItem("Price Change History", "NavigateToPriceChangeHistoryCommand"),
                    new ExpectedMenuItem("Categories", "NavigateToCategoryCommand"),
                    new ExpectedMenuItem("Subcategories", "NavigateToSubCategoryCommand"),
                    new ExpectedMenuItem("Item Properties", "NavigateToItemPropertyCommand"),
                    new ExpectedMenuItem("Units of Measure", "NavigateToUnitOfMeasureCommand"),
                    new ExpectedMenuItem("Tax Rates", "NavigateToTaxRateCommand"),
                    new ExpectedMenuItem("Barcode Management", "NavigateToBarcodeManagementCommand"),
                    new ExpectedMenuItem("Print Barcode Labels", "NavigateToBarcodePrinterCommand"),
                    new ExpectedMenuItem("Express Item Buttons", "NavigateToExpressItemAdminCommand")
                }),
            new ExpectedTopLevelMenu(
                "Stock",
                null,
                new[]
                {
                    new ExpectedMenuItem("Stock Balance & Expiry", "NavigateToStockBalanceCommand"),
                    new ExpectedMenuItem("Stock Adjustment", "NavigateToStockAdjustmentCommand")
                }),
            new ExpectedTopLevelMenu(
                "Purchasing",
                null,
                new[]
                {
                    new ExpectedMenuItem("Suppliers", "NavigateToSupplierCommand"),
                    new ExpectedMenuItem("New Purchase Order", "NavigateToPurchaseOrderCommand"),
                    new ExpectedMenuItem("Purchase Order History", "NavigateToPurchaseOrderDashboardCommand"),
                    new ExpectedMenuItem("New Goods Received Note", "NavigateToGoodsReceivedNoteCommand"),
                    new ExpectedMenuItem("GRN History", "NavigateToGrnDashboardCommand"),
                    new ExpectedMenuItem("Supplier Return", "NavigateToSupplierReturnCommand"),
                    new ExpectedMenuItem("Supplier Claims", "NavigateToSupplierClaimsCommand")
                }),
            new ExpectedTopLevelMenu(
                "Sales",
                null,
                new[]
                {
                    new ExpectedMenuItem("Sales Explorer", "NavigateToSalesExplorerCommand"),
                    new ExpectedMenuItem("Customer Returns", "NavigateToCustomerReturnsAuditCommand"),
                    new ExpectedMenuItem("Item Sales Analysis", "NavigateToItemSalesAnalyticsCommand"),
                    new ExpectedMenuItem("Gift Vouchers", "NavigateToGiftVoucherCommand"),
                    new ExpectedMenuItem("Free Issue Rules", "NavigateToFreeIssueRuleSetupCommand")
                }),
            new ExpectedTopLevelMenu(
                "Customers",
                null,
                new[]
                {
                    new ExpectedMenuItem("Customer Master", "NavigateToCustomerMasterCommand"),
                    new ExpectedMenuItem("Customer Accounts & Credit", "NavigateToCustomerLedgerCommand")
                }),
            new ExpectedTopLevelMenu(
                "Finance",
                null,
                new[]
                {
                    new ExpectedMenuItem("Supplier Accounts", "NavigateToSupplierLedgerCommand"),
                    new ExpectedMenuItem("Cash Movement", "NavigateToCashMovementDashboardCommand"),
                    new ExpectedMenuItem("Float Cash Log", "NavigateToFloatCashLogCommand"),
                    new ExpectedMenuItem("Financial Summary", "NavigateToFinancialSummaryCommand")
                }),
            new ExpectedTopLevelMenu(
                "Reports",
                null,
                new[]
                {
                    new ExpectedMenuItem("Supplier Report", "NavigateToSupplierReportCommand"),
                    new ExpectedMenuItem("VAT Report & Reconciliation", "NavigateToVatReportCommand"),
                    new ExpectedMenuItem("Suspended Transactions", "NavigateToSuspendedTransactionsMonitorCommand")
                }),
            new ExpectedTopLevelMenu(
                "Administration",
                null,
                new[]
                {
                    new ExpectedMenuItem("Store Settings", "NavigateToStoreSettingsCommand"),
                    new ExpectedMenuItem("User Management", "NavigateToUserManagementCommand"),
                    new ExpectedMenuItem("License Management", "NavigateToLicenseManagementCommand"),
                    new ExpectedMenuItem("Cashier Terminal Management", "NavigateToTerminalManagementCommand"),
                    new ExpectedMenuItem("Security Audit", "NavigateToSecurityAuditCommand")
                })
        };

        AuditAssert.Equal(
            expectedTopLevel.Length,
            topLevelItems.Length,
            "BackOffice top-level menu count");

        for (int index = 0; index < expectedTopLevel.Length; index++)
        {
            ExpectedTopLevelMenu expected = expectedTopLevel[index];
            XElement actual = topLevelItems[index];

            AuditAssert.Equal(
                expected.Header,
                NormalizeHeader(actual.Attribute("Header")?.Value),
                $"BackOffice top-level menu header at position {index + 1}");

            string? actualTopLevelCommand = ReadBindingCommand(actual);
            AuditAssert.Equal(
                expected.Command,
                actualTopLevelCommand,
                $"BackOffice top-level command for {expected.Header}");

            XElement[] actualChildren = actual
                .Elements(PresentationNamespace + "MenuItem")
                .ToArray();

            AuditAssert.Equal(
                expected.Items.Length,
                actualChildren.Length,
                $"BackOffice child menu count for {expected.Header}");

            for (int childIndex = 0; childIndex < expected.Items.Length; childIndex++)
            {
                ExpectedMenuItem expectedChild = expected.Items[childIndex];
                XElement actualChild = actualChildren[childIndex];

                AuditAssert.Equal(
                    expectedChild.Header,
                    NormalizeHeader(actualChild.Attribute("Header")?.Value),
                    $"BackOffice menu label in {expected.Header} at position {childIndex + 1}");
                AuditAssert.Equal(
                    expectedChild.Command,
                    ReadBindingCommand(actualChild),
                    $"BackOffice command for {expected.Header} / {expectedChild.Header}");
            }
        }

        string[] commandBindings = menu
            .DescendantsAndSelf(PresentationNamespace + "MenuItem")
            .Select(ReadBindingCommand)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Cast<string>()
            .ToArray();

        AuditAssert.Equal(
            43,
            commandBindings.Length,
            "BackOffice menu command binding count");
        AuditAssert.Equal(
            43,
            commandBindings.Distinct(StringComparer.Ordinal).Count(),
            "BackOffice unique menu command binding count");
    }

    private static void VerifyReadableSelectionTheme()
    {
        string colors = Read(
            "POS.BackOffice.UI",
            "Resources",
            "Colors.xaml");
        string controls = Read(
            "POS.BackOffice.UI",
            "Resources",
            "BackOfficeControls.xaml");

        AuditAssert.Contains(
            colors,
            "x:Key=\"LightSelectionBrush\" Color=\"#D0E8E8\"",
            "authoritative active selection colour");
        AuditAssert.Contains(
            colors,
            "x:Key=\"PrimaryTextBrush\" Color=\"Black\"",
            "authoritative selected-text colour");
        AuditAssert.Contains(
            controls,
            "InactiveSelectionHighlightBrushKey}\" Color=\"#E4F0F0\"",
            "authoritative inactive selection colour");

        string[] correctedViews =
        {
            Path.Combine(
                "POS.BackOffice.UI",
                "Views",
                "Pages",
                "InventoryOperations",
                "BarcodeManagementView.xaml"),
            Path.Combine(
                "POS.BackOffice.UI",
                "Views",
                "Pages",
                "InventoryOperations",
                "BarcodePrinterView.xaml"),
            Path.Combine(
                "POS.BackOffice.UI",
                "Views",
                "Pages",
                "InventoryOperations",
                "ExpressItemAdminView.xaml"),
            Path.Combine(
                "POS.BackOffice.UI",
                "Views",
                "Pages",
                "Reports",
                "FloatCashLogView.xaml")
        };

        foreach (string relativePath in correctedViews)
        {
            XDocument document = LoadXaml(relativePath);
            XElement[] selectionTriggers = FindSelectedStateTriggers(document).ToArray();

            AuditAssert.Equal(
                1,
                selectionTriggers.Length,
                $"selected-row trigger count in {relativePath}");

            XElement trigger = selectionTriggers[0];
            AuditAssert.Equal(
                "{DynamicResource LightSelectionBrush}",
                ReadSetterValue(trigger, "Background"),
                $"selected-row background in {relativePath}");
            AuditAssert.Equal(
                "{DynamicResource PrimaryTextBrush}",
                ReadSetterValue(trigger, "Foreground"),
                $"selected-row foreground in {relativePath}");
        }

        string backOfficeRoot = Path.Combine(
            AuditPaths.RepositoryRoot,
            "POS.BackOffice.UI");

        foreach (string xamlPath in Directory.EnumerateFiles(
                     backOfficeRoot,
                     "*.xaml",
                     SearchOption.AllDirectories))
        {
            XDocument document = XDocument.Load(
                xamlPath,
                LoadOptions.PreserveWhitespace);

            foreach (XElement trigger in FindSelectedStateTriggers(document))
            {
                string? selectedBackground = ReadSetterValue(trigger, "Background");

                AuditAssert.False(
                    string.Equals(
                        selectedBackground,
                        "#005555",
                        StringComparison.OrdinalIgnoreCase),
                    $"Dark teal selection background remains in {Path.GetRelativePath(AuditPaths.RepositoryRoot, xamlPath)}.");
            }
        }
    }

    private static IEnumerable<XElement> FindSelectedStateTriggers(
        XDocument document) =>
        document
            .Descendants(PresentationNamespace + "Trigger")
            .Where(trigger =>
                string.Equals(
                    trigger.Attribute("Property")?.Value,
                    "IsSelected",
                    StringComparison.Ordinal) &&
                string.Equals(
                    trigger.Attribute("Value")?.Value,
                    "True",
                    StringComparison.OrdinalIgnoreCase));

    private static string? ReadSetterValue(
        XElement trigger,
        string propertyName) =>
        trigger
            .Elements(PresentationNamespace + "Setter")
            .SingleOrDefault(setter =>
                string.Equals(
                    setter.Attribute("Property")?.Value,
                    propertyName,
                    StringComparison.Ordinal))?
            .Attribute("Value")?
            .Value;

    private static string NormalizeHeader(string? header) =>
        (header ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal);

    private static string? ReadBindingCommand(XElement menuItem)
    {
        string? binding = menuItem.Attribute("Command")?.Value;
        const string prefix = "{Binding ";
        const string suffix = "}";

        if (string.IsNullOrWhiteSpace(binding))
        {
            return null;
        }

        AuditAssert.True(
            binding.StartsWith(prefix, StringComparison.Ordinal) &&
            binding.EndsWith(suffix, StringComparison.Ordinal),
            $"Unexpected BackOffice menu command binding syntax: {binding}");

        return binding[prefix.Length..^suffix.Length].Trim();
    }

    private static XDocument LoadXaml(params string[] segments) =>
        XDocument.Load(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()),
            LoadOptions.PreserveWhitespace);

    private static XDocument LoadXaml(string relativePath) =>
        XDocument.Load(
            Path.Combine(
                AuditPaths.RepositoryRoot,
                relativePath),
            LoadOptions.PreserveWhitespace);

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));

    private sealed record ExpectedTopLevelMenu(
        string Header,
        string? Command,
        ExpectedMenuItem[] Items);

    private sealed record ExpectedMenuItem(
        string Header,
        string Command);
}
