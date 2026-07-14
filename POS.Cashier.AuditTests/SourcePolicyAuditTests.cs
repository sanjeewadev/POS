namespace POS.Cashier.AuditTests;

internal static class SourcePolicyAuditTests
{
    public static Task CashierUtilityButtonsAreWiredAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        AuditAssert.Contains(view, "Click=\"ReloadBtn_Click\"", "Cashier Reload button");
        AuditAssert.Contains(view, "Content=\"{Binding PricingModeButtonText}\"",
            "Retail/Wholesale mode button label");
        AuditAssert.Contains(view, "Click=\"PricingModeBtn_Click\"",
            "Retail/Wholesale mode button handler");

        string code = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");
        AuditAssert.Contains(code, "ReloadCashierAsync", "Cashier Reload action");
        AuditAssert.Contains(code, "TogglePricingMode", "Retail/Wholesale action");
        AuditAssert.Contains(code, "GetRequiredService<StockInquiryViewModel>",
            "Stock Inquiry ViewModel resolution");

        string stockDialog = Read("POS.Cashier.UI", "Dialogs", "StockInquiryDialog.xaml");
        AuditAssert.Contains(stockDialog, "Command=\"{Binding CheckStockCommand}\"",
            "CHECK STOCK command binding");

        string salesViewModel = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.cs");
        string reload = ExtractMethodWindow(salesViewModel, "public async Task ReloadCashierAsync()", 5000);
        AuditAssert.Contains(reload, "FlushCartPersistenceAsync", "Reload cart preservation");
        AuditAssert.False(reload.Contains("Cart.Clear", StringComparison.Ordinal),
            "Reload must not clear the current cart.");
        AuditAssert.Contains(salesViewModel, "IsWholesaleSale = IsWholesaleMode",
            "selected pricing mode sale snapshot");

        return Task.CompletedTask;
    }

    public static Task HiddenCreditNotePlaceholderIsRemovedAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        AuditAssert.False(view.Contains("Content=\"Credit Note\"", StringComparison.Ordinal),
            "The hidden disabled Credit Note payment placeholder is still present on SalesView.");
        return Task.CompletedTask;
    }

    public static Task PaidInAndPaidOutDoNotRequireManagerPasswordAsync()
    {
        string source = Read("POS.Cashier.UI", "ViewModels", "CashMovementViewModel.cs");
        string confirm = ExtractMethodWindow(source, "private async Task ConfirmAsync()", 6500);
        AuditAssert.False(confirm.Contains("ManagerAuthDialogView", StringComparison.Ordinal),
            "Cash Movement ConfirmAsync still opens manager-password authorization. Paid In and Paid Out must use cashier identity and complete audit data without manager credentials.");
        AuditAssert.False(confirm.Contains("GetRequiredService<ManagerAuthViewModel>", StringComparison.Ordinal),
            "Cash Movement ConfirmAsync still resolves ManagerAuthViewModel.");
        return Task.CompletedTask;
    }

    public static Task PriceOverrideUiCapturesManagerIdentityAsync()
    {
        string source = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");
        string method = ExtractMethodWindow(source, "private void ApplyNewPriceFromTerminalInput()", 5000);
        AuditAssert.Contains(method, "ManagerAuthDialogView", "below-minimum New Price manager authentication");
        AuditAssert.Contains(method, "AuthorizedUsername", "below-minimum New Price approver identity");
        AuditAssert.Contains(method, "ApplyNewPriceToSelected(newPrice, approvedBy)", "New Price approver handoff");
        return Task.CompletedTask;
    }

    public static Task CheckoutFailurePathWritesTechnicalLogAsync()
    {
        string source = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.cs");
        string method = ExtractMethodWindow(source, "public async Task<bool> FinalizeCheckoutAsync()", 18000);
        int failureMessage = method.IndexOf("Transaction failed:", StringComparison.Ordinal);
        AuditAssert.True(failureMessage >= 0, "FinalizeCheckoutAsync financial failure notification was not found.");
        int start = Math.Max(0, failureMessage - 700);
        int length = Math.Min(1400, method.Length - start);
        string catchBlock = method.Substring(start, length);
        AuditAssert.Contains(catchBlock, "LocalLogService.WriteException", "checkout financial failure logging");
        return Task.CompletedTask;
    }

    public static Task StartupEnforcesLicencesBeforeLoginAsync()
    {
        string source = Read("POS.Cashier.UI", "App.xaml.cs");
        int initialize = source.IndexOf("InitializeTerminalAndLicenseAsync", StringComparison.Ordinal);
        int login = source.IndexOf("ShowLoginWindowAsync", StringComparison.Ordinal);
        AuditAssert.True(initialize >= 0 && login >= 0 && initialize < login,
            "Cashier startup does not clearly initialize terminal/licence enforcement before login.");
        AuditAssert.Contains(source, "CanRunCashier", "Cashier run licence gate");
        AuditAssert.Contains(source, "ExpiringSoon", "licence expiry warning path");
        AuditAssert.Contains(source, "Database.MigrateAsync", "startup migration path");
        return Task.CompletedTask;
    }

    public static Task ShiftRestorationPreservesCashierOwnershipAsync()
    {
        string source = Read("POS.Cashier.UI", "App.xaml.cs");
        string method = ExtractMethodWindow(source, "private async Task ShowLoginWindowAsync", 14000);
        AuditAssert.Contains(method, "activeShift.CashierName", "restored shift cashier comparison");
        AuditAssert.Contains(method, "already has an open shift", "wrong-cashier shift rejection message");
        AuditAssert.Contains(method, "SalesViewModel", "SalesViewModel creation after shift validation");
        return Task.CompletedTask;
    }

    public static Task SaleCommitsBeforeHardwareActionsAsync()
    {
        string source = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.cs");
        string method = ExtractMethodWindow(source, "public async Task<bool> FinalizeCheckoutAsync()", 18000);
        int checkout = method.IndexOf("ProcessCheckoutAsync", StringComparison.Ordinal);
        int hardware = method.IndexOf("CompleteReceiptAndDrawerAsync", StringComparison.Ordinal);
        AuditAssert.True(checkout >= 0 && hardware >= 0 && checkout < hardware,
            "Receipt/drawer actions are not clearly sequenced after the committed sale.");
        AuditAssert.Contains(method, "Payment was saved", "post-commit hardware failure warning");
        return Task.CompletedTask;
    }

    public static Task LiveDatabasePathIsExcludedAsync()
    {
        string live = AuditPaths.GetLiveDatabasePath();
        string audit = Path.Combine(AuditPaths.GetAuditTempRoot(), "safety-check.db");
        AuditPaths.AssertSafeDatabasePath(audit);
        AuditAssert.False(string.Equals(Path.GetFullPath(live), Path.GetFullPath(audit), StringComparison.OrdinalIgnoreCase),
            "Audit database resolved to the live POS database.");

        bool rejected = false;
        try
        {
            AuditPaths.AssertSafeDatabasePath(live);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        AuditAssert.True(rejected, "The audit safety guard did not reject the live database path.");
        return Task.CompletedTask;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(new[] { AuditPaths.RepositoryRoot }.Concat(parts).ToArray());
        if (!File.Exists(path))
            throw new InvalidOperationException($"Required source file was not found: {path}");
        return File.ReadAllText(path);
    }

    private static string ExtractMethodWindow(string source, string signature, int maximumLength)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException($"Method signature was not found: {signature}");
        return source.Substring(start, Math.Min(maximumLength, source.Length - start));
    }
}
