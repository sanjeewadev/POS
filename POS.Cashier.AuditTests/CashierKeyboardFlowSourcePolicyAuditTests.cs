namespace POS.Cashier.AuditTests;

internal static class CashierKeyboardFlowSourcePolicyAuditTests
{
    public static Task ScanModeAndScannerQueueAreControlledAsync()
    {
        string viewModel = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.cs");

        AuditAssert.Contains(
            viewModel,
            "private readonly SemaphoreSlim _barcodeProcessingGate",
            "ordered barcode processing gate");
        AuditAssert.Contains(
            viewModel,
            "await ProcessBarcodeInOrderAsync(input)",
            "ordered barcode processing route");
        AuditAssert.Contains(
            viewModel,
            "public bool IsBarcodeProcessing",
            "pending barcode-operation state");
        AuditAssert.Contains(
            viewModel,
            "TerminalInputMode = \"READY TO SCAN\"",
            "explicit Scan mode label");
        AuditAssert.False(
            viewModel.Contains("IsLikelyQuantityInput", StringComparison.Ordinal),
            "Scan mode must not reinterpret short numeric barcodes as quantity.");

        return Task.CompletedTask;
    }

    public static Task KeyboardMapAndPaymentCompletionAreControlledAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        string code = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");

        AuditAssert.Contains(code, "e.Key == Key.Multiply", "Numpad Multiply quantity shortcut");
        AuditAssert.Contains(code, "e.Key == Key.Add", "Numpad Add Cash shortcut");
        AuditAssert.Contains(code, "e.Key == Key.Subtract", "Numpad Subtract remove shortcut");
        AuditAssert.Contains(code, "e.Key == Key.F6", "Suspend shortcut");
        AuditAssert.Contains(code, "SuspendBtn_Click", "dedicated Suspend route");
        AuditAssert.Contains(code, "e.Key == Key.F7", "Recall shortcut");
        AuditAssert.Contains(code, "RecallBtn_Click", "dedicated Recall route");
        AuditAssert.Contains(code, "e.Key == Key.F8", "Cash shortcut");
        AuditAssert.Contains(code, "e.Key == Key.F9", "Card shortcut");
        AuditAssert.Contains(code, "e.Key == Key.F10", "payment-mode shortcut");
        AuditAssert.Contains(
            code,
            "Wait for the pending scan before {actionName}",
            "payment and line-action pending-scan guard");
        AuditAssert.Contains(code, "if (e.IsRepeat)", "held Enter protection");
        AuditAssert.Contains(
            code,
            "await FinalizePaymentIfCompleteAsync()",
            "automatic existing-checkout completion after full tender");
        AuditAssert.Contains(view, "Content=\"Cash  F8 / +\"", "visible Cash shortcut hint");
        AuditAssert.Contains(view, "Content=\"Payment  F10\"", "visible payment shortcut hint");
        AuditAssert.Contains(view, "Content=\"Qty  F2 / *\"", "visible quantity shortcut hint");

        return Task.CompletedTask;
    }

    public static Task ProcessingLockAndPaymentRowsAreVisibleAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        string code = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");
        string viewModel = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.cs");
        string lifecycle = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.CartLifecycle.cs");

        AuditAssert.Contains(view, "Text=\"PROCESSING SALE\"", "visible checkout processing overlay");
        AuditAssert.Contains(view, "Binding=\"{Binding IsCheckoutInProgress}\"", "processing overlay binding");
        AuditAssert.Contains(view, "x:Name=\"PaymentDataGrid\"", "visible split-payment rows");
        AuditAssert.Contains(view, "ItemsSource=\"{Binding PaymentLines}\"", "payment-row binding");
        AuditAssert.Contains(code, "if (ViewModel.IsCheckoutInProgress)", "main keyboard processing lock");
        AuditAssert.Contains(viewModel, "TerminalInputMode = \"PROCESSING\"", "processing state label");
        AuditAssert.Contains(lifecycle, "OnPropertyChanged(nameof(IsCheckoutInProgress))", "observable processing state");
        AuditAssert.False(view.Contains("Printer ON", StringComparison.Ordinal),
            "Cashier must not display a hard-coded printer-online status.");
        AuditAssert.False(view.Contains("Text=\"LAN\"", StringComparison.Ordinal),
            "Cashier must not display a hard-coded LAN-online status.");

        return Task.CompletedTask;
    }

    public static Task GridAndLookupKeyboardFlowAreReadableAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        string productSeek = Read("POS.Cashier.UI", "Dialogs", "ProductSeekDialog.xaml.cs");
        string b2b = Read("POS.Cashier.UI", "Dialogs", "B2BCustomerDialogView.xaml.cs");
        string loyalty = Read("POS.Cashier.UI", "Dialogs", "LoyaltyCustomerDialogView.xaml.cs");
        string customerViewModel = Read("POS.Cashier.UI", "ViewModels", "B2BCustomerViewModel.cs");

        AuditAssert.Contains(view, "Value=\"#123A66\"", "dark-blue selected sale line");
        AuditAssert.Contains(view, "Foreground=\"#0F3B66\"", "dark-blue main total");
        AuditAssert.Contains(view, "Background=\"{Binding PaymentStatusColor}\"", "functional payment status colour");
        AuditAssert.Contains(productSeek, "e.Key == Key.Left", "Product Search Left navigation");
        AuditAssert.Contains(productSeek, "e.Key == Key.Right", "Product Search Right navigation");
        AuditAssert.Contains(b2b, "CustomerDataGrid_PreviewKeyDown", "customer keyboard selection");
        AuditAssert.Contains(loyalty, "CustomerDataGrid_PreviewKeyDown", "loyalty keyboard selection");
        AuditAssert.Contains(customerViewModel, "SelectedCustomer = Customers.FirstOrDefault()",
            "first customer result selection");

        return Task.CompletedTask;
    }

    public static Task ConfirmationAndRecoveryDialogsAreKeyboardControlledAsync()
    {
        string sales = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");
        string confirmation = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "CashierConfirmationDialog.xaml.cs");
        string recovery = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "ActiveCartRecoveryDialog.xaml.cs");

        AuditAssert.Contains(
            sales,
            "new CashierConfirmationDialog(",
            "keyboard-native Cashier confirmation route");
        AuditAssert.Contains(
            sales,
            "new ActiveCartRecoveryDialog(",
            "keyboard-native recovered-cart route");
        AuditAssert.Contains(
            confirmation,
            "e.Key == Key.Left || e.Key == Key.Right",
            "confirmation Left and Right navigation");
        AuditAssert.Contains(
            confirmation,
            "e.Key == Key.Escape",
            "confirmation Escape cancellation");
        AuditAssert.Contains(
            confirmation,
            "e.Key == Key.Enter",
            "confirmation explicit Enter selection");
        AuditAssert.Contains(
            recovery,
            "e.Key == Key.Enter",
            "recovered-cart explicit Enter selection");
        AuditAssert.Contains(
            recovery,
            "ActiveCartRecoveryAction.CancelCart",
            "recovered-cart audited cancellation choice");
        AuditAssert.Contains(
            recovery,
            "ActiveCartRecoveryAction.LogOff",
            "recovered-cart log-off choice");
        AuditAssert.False(
            sales.Contains(
                "MessageBoxButton.YesNo,\n                MessageBoxImage.Warning",
                StringComparison.Ordinal),
            "Primary remove-line flow must not use a Windows MessageBox confirmation.");

        return Task.CompletedTask;
    }

    public static Task NumLockVisibilityAndModalFocusAreControlledAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        string code = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");

        AuditAssert.Contains(
            view,
            "x:Name=\"NumLockWarningBorder\"",
            "visible Num Lock warning indicator");
        AuditAssert.Contains(
            view,
            "Text=\"NUM LOCK OFF\"",
            "clear Num Lock warning text");
        AuditAssert.Contains(
            code,
            "Keyboard.IsKeyToggled(Key.NumLock)",
            "physical keypad Num Lock state check");
        AuditAssert.Contains(
            code,
            "e.Key == Key.NumLock",
            "live Num Lock status refresh");
        AuditAssert.Contains(
            code,
            "DimmingCurtain.Visibility = Visibility.Visible",
            "modal Cashier dimming guard");
        AuditAssert.Contains(
            code,
            "_isDialogOpen = false;",
            "modal Cashier input restoration");

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
