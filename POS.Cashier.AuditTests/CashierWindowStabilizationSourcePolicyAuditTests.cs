namespace POS.Cashier.AuditTests;

internal static class CashierWindowStabilizationSourcePolicyAuditTests
{
    private static readonly string[] RoutineDialogFiles =
    {
        "B2BCustomerDialogView.xaml",
        "CardTenderDialog.xaml",
        "CartCancellationReasonDialog.xaml",
        "CashMovementDialogView.xaml",
        "CashTenderDialog.xaml",
        "ChequeTenderDialog.xaml",
        "CustomerAccountPaymentDialog.xaml",
        "DrawerReasonDialog.xaml",
        "ExpressItemDialogView.xaml",
        "FloatCashDialog.xaml",
        "FreeItemReasonModalWindow.xaml",
        "GiftVoucherTenderDialog.xaml",
        "HoldRecallDialog.xaml",
        "LockRecoveryActionDialog.xaml",
        "LoyaltyCustomerDialogView.xaml",
        "ManagerAuthDialogView.xaml",
        "OpenShiftView.xaml",
        "PrintOptionsDialog.xaml",
        "ProductSeekDialog.xaml",
        "QuickCustomerCreateDialog.xaml",
        "ReturnInvoiceDialog.xaml",
        "SalesDocumentPreviewDialog.xaml",
        "SellGiftVoucherDialog.xaml",
        "ShiftCloseDialog.xaml",
        "ShiftMenuView.xaml",
        "ShiftSummaryDialog.xaml",
        "StockInquiryDialog.xaml",
        "TaxInvoiceIssueDialog.xaml",
        "ZReportSummaryDialog.xaml"
    };

    public static Task DialogPlacementAndNativeChromeAreControlledAsync()
    {
        string app = Read("POS.Cashier.UI", "App.xaml.cs");
        string placement = Read(
            "POS.Cashier.UI",
            "Services",
            "CashierWindowPlacementService.cs");

        AuditAssert.Contains(
            app,
            "CashierWindowPlacementService.Register()",
            "Cashier startup window-placement registration");
        AuditAssert.Contains(
            placement,
            "EventManager.RegisterClassHandler",
            "central Cashier window loaded handler");
        AuditAssert.Contains(
            placement,
            "MonitorFromWindow",
            "owner-monitor selection");
        AuditAssert.Contains(
            placement,
            "GetMonitorInfo",
            "monitor work-area lookup");
        AuditAssert.Contains(
            placement,
            "window.Owner is { IsVisible: true }",
            "owner-aware centering");
        AuditAssert.Contains(
            placement,
            "window.MaxHeight",
            "work-area height protection");
        AuditAssert.Contains(
            placement,
            "window.MaxWidth",
            "work-area width protection");

        foreach (string fileName in RoutineDialogFiles)
        {
            string xaml = Read("POS.Cashier.UI", "Dialogs", fileName);
            AuditAssert.Contains(
                xaml,
                "Style=\"{StaticResource CashierDialogWindow}\"",
                $"shared Cashier dialog style for {fileName}");
            AuditAssert.Contains(
                xaml,
                "WindowStartupLocation=\"CenterOwner\"",
                $"owner-centering rule for {fileName}");
            AuditAssert.False(
                xaml.Contains("WindowStyle=\"None\"", StringComparison.Ordinal),
                $"Routine dialog must use native window chrome: {fileName}");
            AuditAssert.False(
                xaml.Contains("AllowsTransparency=\"True\"", StringComparison.Ordinal),
                $"Routine dialog must not use transparent window chrome: {fileName}");
            AuditAssert.False(
                xaml.Contains("Topmost=\"True\"", StringComparison.Ordinal),
                $"Routine dialog must not remain topmost: {fileName}");
        }

        string login = Read("POS.Cashier.UI", "Views", "LoginView.xaml");
        AuditAssert.Contains(
            login,
            "WindowStartupLocation=\"CenterScreen\"",
            "top-level login centering exception");

        string lockScreen = Read("POS.Cashier.UI", "Dialogs", "LockScreenView.xaml");
        AuditAssert.Contains(lockScreen, "Topmost=\"True\"", "lock-screen security exception");
        AuditAssert.Contains(lockScreen, "WindowStyle=\"None\"", "lock-screen full-screen chrome exception");

        return Task.CompletedTask;
    }

    public static Task RepeatedSubmissionAndModalCloseAreGuardedAsync()
    {
        string payment = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "CustomerAccountPaymentDialog.xaml.cs");
        AuditAssert.Contains(payment, "private bool _isSaving", "customer-payment save guard");
        AuditAssert.Contains(payment, "if (_isSaving)", "customer-payment repeated-submit rejection");
        AuditAssert.Contains(payment, "SetSavingState(true)", "customer-payment control lock");
        AuditAssert.Contains(payment, "ReceiptToken = _receiptToken", "stable idempotency token");
        AuditAssert.Contains(payment, "OnClosing(CancelEventArgs e)", "customer-payment close guard");

        string shiftMenu = Read("POS.Cashier.UI", "Dialogs", "ShiftMenuView.xaml.cs");
        AuditAssert.Contains(shiftMenu, "private bool _isOperationInProgress", "shift-menu operation guard");
        AuditAssert.Contains(shiftMenu, "TryBeginOperation", "shift-menu re-entry prevention");
        AuditAssert.Contains(shiftMenu, "OnClosing(CancelEventArgs e)", "shift-menu close guard");
        AuditAssert.Contains(shiftMenu, "finally", "shift-menu operation cleanup");

        string summary = Read("POS.Cashier.UI", "Dialogs", "ShiftSummaryDialog.xaml.cs");
        AuditAssert.Contains(summary, "private bool _isPrinting", "shift-report print guard");
        AuditAssert.Contains(summary, "OnClosing(CancelEventArgs e)", "shift-report close guard");

        return Task.CompletedTask;
    }

    public static Task FloatCashAndOwnedDialogRoutesAreControlledAsync()
    {
        string salesViewModel = Read(
            "POS.Cashier.UI",
            "ViewModels",
            "SalesViewModel.cs");
        AuditAssert.Contains(
            salesViewModel,
            "Window? owner = Application.Current?.MainWindow",
            "Float Cash owner resolution");
        AuditAssert.Contains(
            salesViewModel,
            "Owner = owner",
            "Float Cash and manager-auth owner assignment");

        string zReport = Read("POS.Cashier.UI", "Dialogs", "ZReportSummaryDialog.xaml");
        AuditAssert.False(
            zReport.Contains("Topmost=\"True\"", StringComparison.Ordinal),
            "Z Report must not be topmost.");
        AuditAssert.Contains(
            zReport,
            "WindowStartupLocation=\"CenterOwner\"",
            "Z Report owner centering");

        string cashierRoot = Path.Combine(AuditPaths.RepositoryRoot, "POS.Cashier.UI");
        string[] unexpectedCenterScreen = Directory
            .GetFiles(cashierRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(
                Path.Combine("Views", "LoginView.xaml"),
                StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(
                "WindowStartupLocation=\"CenterScreen\"",
                StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path) ?? path)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        AuditAssert.True(
            unexpectedCenterScreen.Length == 0,
            "Only Login may use CenterScreen. Found: " +
            string.Join(", ", unexpectedCenterScreen));

        return Task.CompletedTask;
    }

    public static Task LargeDialogsAreResponsiveAndScrollableAsync()
    {
        AssertResponsive("B2BCustomerDialogView.xaml");
        AssertResponsive("LoyaltyCustomerDialogView.xaml");
        AssertResponsive("ExpressItemDialogView.xaml");
        AssertResponsive("FloatCashDialog.xaml");
        AssertResponsive("ReturnInvoiceDialog.xaml");
        AssertResponsive("SalesDocumentPreviewDialog.xaml");
        AssertResponsive("ShiftMenuView.xaml");
        AssertResponsive("ShiftSummaryDialog.xaml");

        string express = Read("POS.Cashier.UI", "Dialogs", "ExpressItemDialogView.xaml");
        AuditAssert.Contains(express, "<ScrollViewer", "Express Items scroll container");
        AuditAssert.Contains(express, "<WrapPanel", "Express Items responsive wrapping");
        AuditAssert.False(
            express.Contains("<UniformGrid Columns=\"5\"", StringComparison.Ordinal),
            "Express Items must not return to the clipping-prone fixed five-column layout.");

        return Task.CompletedTask;
    }

    public static Task KeyboardFocusAndSingleInitialLoadAreControlledAsync()
    {
        string[] keyboardDialogs =
        {
            "CartCancellationReasonDialog.xaml",
            "CashMovementDialogView.xaml",
            "CustomerAccountPaymentDialog.xaml",
            "DrawerReasonDialog.xaml",
            "ExpressItemDialogView.xaml",
            "FloatCashDialog.xaml",
            "HoldRecallDialog.xaml",
            "LockRecoveryActionDialog.xaml",
            "ManagerAuthDialogView.xaml",
            "ShiftCloseDialog.xaml",
            "ShiftMenuView.xaml",
            "ShiftSummaryDialog.xaml",
            "StockInquiryDialog.xaml",
            "TaxInvoiceIssueDialog.xaml",
            "ZReportSummaryDialog.xaml"
        };

        foreach (string fileName in keyboardDialogs)
        {
            string xaml = Read("POS.Cashier.UI", "Dialogs", fileName);
            AuditAssert.Contains(
                xaml,
                "PreviewKeyDown=",
                $"Escape keyboard handling for {fileName}");
            AuditAssert.Contains(
                xaml,
                "IsCancel=\"True\"",
                $"cancel-button semantics for {fileName}");
        }

        string customerViewModel = Read(
            "POS.Cashier.UI",
            "ViewModels",
            "B2BCustomerViewModel.cs");
        string constructor = ExtractWindow(
            customerViewModel,
            "public B2BCustomerViewModel(CustomerRepository repository)",
            600);
        AuditAssert.False(
            constructor.Contains("SearchAsync", StringComparison.Ordinal),
            "Customer lookup constructor must not start a duplicate initial search.");

        string b2b = Read("POS.Cashier.UI", "Dialogs", "B2BCustomerDialogView.xaml.cs");
        string loyalty = Read("POS.Cashier.UI", "Dialogs", "LoyaltyCustomerDialogView.xaml.cs");
        AuditAssert.Contains(b2b, "await ViewModel.ReloadAsync()", "B2B initial customer load");
        AuditAssert.Contains(loyalty, "await ViewModel.ReloadAsync()", "loyalty initial customer load");

        return Task.CompletedTask;
    }

    private static void AssertResponsive(string fileName)
    {
        string xaml = Read("POS.Cashier.UI", "Dialogs", fileName);
        AuditAssert.Contains(xaml, "MinWidth=", $"minimum width for {fileName}");
        AuditAssert.Contains(xaml, "MinHeight=", $"minimum height for {fileName}");
        AuditAssert.True(
            xaml.Contains("ResizeMode=\"CanResize", StringComparison.Ordinal),
            $"Large dialog must be resizable: {fileName}");
    }

    private static string ExtractWindow(string text, string marker, int length)
    {
        int start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException($"Source marker was not found: {marker}");

        return text.Substring(start, Math.Min(length, text.Length - start));
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(new[] { AuditPaths.RepositoryRoot }.Concat(parts).ToArray());
        if (!File.Exists(path))
            throw new InvalidOperationException($"Required source file was not found: {path}");
        return File.ReadAllText(path);
    }
}
