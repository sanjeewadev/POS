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

    public static Task DiscountRuleEntryPointIsRemovedAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        AuditAssert.False(view.Contains("Disc Rule", StringComparison.Ordinal),
            "Cashier SalesView still exposes the Disc Rule button.");
        AuditAssert.False(view.Contains("DiscountRuleBtn_Click", StringComparison.Ordinal),
            "Cashier SalesView still wires the Discount Rule click handler.");

        string code = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");
        AuditAssert.False(code.Contains("DiscountRuleBtn_Click", StringComparison.Ordinal),
            "Cashier code-behind still exposes the Discount Rule action.");
        AuditAssert.False(code.Contains("DiscountRuleDialog", StringComparison.Ordinal),
            "Cashier code-behind still opens the Discount Rule dialog.");

        string salesViewModel = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.cs");
        AuditAssert.False(salesViewModel.Contains("ApplyDiscountRuleToSelected", StringComparison.Ordinal),
            "Cashier SalesViewModel still exposes new Discount Rule application logic.");
        AuditAssert.False(salesViewModel.Contains("DiscountRuleApplyResult", StringComparison.Ordinal),
            "Cashier SalesViewModel still depends on the removed Discount Rule dialog result type.");
        string app = Read("POS.Cashier.UI", "App.xaml.cs");
        AuditAssert.False(app.Contains("DiscountRuleDialogViewModel", StringComparison.Ordinal),
            "Cashier startup still registers the removed Discount Rule UI.");

        string dialogXaml = Path.Combine(AuditPaths.RepositoryRoot, "POS.Cashier.UI", "Dialogs", "DiscountRuleDialog.xaml");
        string dialogCode = Path.Combine(AuditPaths.RepositoryRoot, "POS.Cashier.UI", "Dialogs", "DiscountRuleDialog.xaml.cs");
        string dialogViewModel = Path.Combine(AuditPaths.RepositoryRoot, "POS.Cashier.UI", "ViewModels", "DiscountRuleDialogViewModel.cs");
        AuditAssert.False(File.Exists(dialogXaml), "Discount Rule dialog XAML still exists.");
        AuditAssert.False(File.Exists(dialogCode), "Discount Rule dialog code-behind still exists.");
        AuditAssert.False(File.Exists(dialogViewModel), "Discount Rule dialog ViewModel still exists.");

        string saleLine = Read("POS.Core", "Models", "SalesLine.cs");
        string repository = Read("POS.Core", "Repositories", "SalesRepository.cs");
        AuditAssert.Contains(saleLine, "DiscountRuleId", "historical Discount Rule sale snapshot");
        AuditAssert.Contains(repository, "DiscountRuleId", "historical Discount Rule persistence");

        return Task.CompletedTask;
    }

    public static Task CashierBottomPanelIsCompactAndOrganizedAsync()
    {
        string view = Read("POS.Cashier.UI", "Views", "SalesView.xaml");
        int bottomStart = view.IndexOf("<!-- BOTTOM PANEL -->", StringComparison.Ordinal);
        AuditAssert.True(bottomStart >= 0, "Cashier bottom panel marker was not found.");
        string bottom = view.Substring(bottomStart);

        AuditAssert.Contains(bottom, "Columns=\"5\" Rows=\"3\" Margin=\"0,0,1,0\"",
            "compact left action grid");
        AuditAssert.Contains(view, "x:Key=\"FeatureIconButtonStyle\"",
            "selected large-icon action style");
        AuditAssert.Contains(view, "x:Key=\"CompactTextButtonStyle\"",
            "text-only action style");
        AuditAssert.Contains(view, "x:Key=\"RightActionIconButtonStyle\"",
            "right-side selected action icon style");
        AuditAssert.Contains(bottom, "Content=\"LOYALTY\"",
            "Loyalty action in the left action grid");

        int loyalty = bottom.IndexOf("Content=\"LOYALTY\"", StringComparison.Ordinal);
        int paymentPanel = bottom.IndexOf("<Grid Grid.Column=\"1\">", StringComparison.Ordinal);
        AuditAssert.True(loyalty >= 0 && paymentPanel >= 0 && loyalty < paymentPanel,
            "Loyalty must remain in the left action grid rather than the payment grid.");

        AuditAssert.Contains(bottom, "Columns=\"3\" Rows=\"2\" Margin=\"0,0,1,0\"",
            "organized two-row payment grid");
        AuditAssert.Contains(bottom, "<ColumnDefinition Width=\"4*\"/>",
            "payment grid width");
        AuditAssert.Contains(bottom, "<ColumnDefinition Width=\"1.2*\"/>",
            "larger Sub Total width");
        AuditAssert.Contains(bottom, "<ColumnDefinition Width=\"1.8*\"/>",
            "larger PLU and Cash width");

        AuditAssert.Contains(bottom, "Tag=\"&#xEC59;\"",
            "Cash checkout icon");
        AuditAssert.Contains(bottom, "Tag=\"&#xE8EF;\"",
            "Sub Total calculator icon");
        AuditAssert.Contains(bottom, "Tag=\"&#xEC5A;\"",
            "PLU scanner icon");
        AuditAssert.Contains(bottom, "Tag=\"&#xE77B;\"",
            "Customer Credit icon");
        AuditAssert.Contains(bottom, "Tag=\"&#xE7C3;\"",
            "Cheque icon");
        AuditAssert.Contains(bottom, "Tag=\"&#xE7B8;\"",
            "Gift Voucher icon");

        AuditAssert.Contains(view, "FontSize=\"28\"",
            "large selected action icons");
        AuditAssert.Contains(view, "FontSize=\"34\"",
            "large Cash checkout icon");
        AuditAssert.Contains(bottom, "Content=\"Print\"",
            "Print selected icon action");
        AuditAssert.Contains(bottom, "Content=\"More\"",
            "More selected icon action");
        AuditAssert.Contains(bottom, "Tag=\"&#xE735;\"",
            "filled Loyalty star");
        AuditAssert.Contains(view, "Content=\"Seek\"",
            "Seek selected icon action");
        AuditAssert.Contains(view, "Content=\"Cancel\"",
            "Cancel selected icon action");
        AuditAssert.Contains(view, "Content=\"Enter\"",
            "Enter selected icon action");
        AuditAssert.Contains(bottom, "Style=\"{StaticResource CardLogoButtonStyle}\"",
            "real card-logo button style");
        AuditAssert.Contains(view, "<Border Width=\"82\"",
            "card-logo display width");
        AuditAssert.Contains(view, "Height=\"32\"",
            "card-logo display height");
        AuditAssert.Contains(bottom, "<ImageBrush ImageSource=\"/POS.Cashier.UI;component/Resources/PaymentLogos/mastercard.png\"",
            "MasterCard logo brush");
        AuditAssert.Contains(bottom, "<ImageBrush ImageSource=\"/POS.Cashier.UI;component/Resources/PaymentLogos/visa.png\"",
            "VISA logo brush");
        AuditAssert.Contains(bottom, "<ImageBrush ImageSource=\"/POS.Cashier.UI;component/Resources/PaymentLogos/amex.png\"",
            "American Express logo brush");
        AuditAssert.Contains(bottom, "Content=\"MasterCard\"",
            "MasterCard label");
        AuditAssert.Contains(bottom, "Content=\"VISA\"",
            "VISA label");
        AuditAssert.Contains(bottom, "Content=\"AMEX\"",
            "American Express label");

        string logoFolder = Path.Combine(
            AuditPaths.RepositoryRoot,
            "POS.Cashier.UI",
            "Resources",
            "PaymentLogos");
        AuditAssert.True(File.Exists(Path.Combine(logoFolder, "mastercard.png")),
            "MasterCard logo file is missing.");
        AuditAssert.True(File.Exists(Path.Combine(logoFolder, "visa.png")),
            "VISA logo file is missing.");
        AuditAssert.True(File.Exists(Path.Combine(logoFolder, "amex.png")),
            "American Express logo file is missing.");

        string project = Read("POS.Cashier.UI", "POS.Cashier.UI.csproj");
        AuditAssert.Contains(project, "Resource Include=\"Resources\\PaymentLogos\\*.png\"",
            "embedded payment-logo resources");

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
