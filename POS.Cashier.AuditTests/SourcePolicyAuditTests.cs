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
        AuditAssert.Contains(bottom, "Style=\"{StaticResource MasterCardLogoButtonStyle}\"",
            "MasterCard logo button style");
        AuditAssert.Contains(bottom, "Style=\"{StaticResource VisaLogoButtonStyle}\"",
            "VISA logo button style");
        AuditAssert.Contains(bottom, "Style=\"{StaticResource AmexLogoButtonStyle}\"",
            "American Express logo button style");
        AuditAssert.Contains(view, "<Border Width=\"82\"",
            "card-logo display width");
        AuditAssert.Contains(view, "Height=\"32\"",
            "card-logo display height");
        AuditAssert.Contains(view, "x:Key=\"PrintFeatureIconButtonStyle\"",
            "slightly smaller Print icon style");
        AuditAssert.Contains(view, "<Border Width=\"94\"",
            "boosted MasterCard logo width");
        AuditAssert.Contains(view, "<Border Width=\"75\"",
            "reduced VISA logo width");
        AuditAssert.Contains(view, "<Border Width=\"103\"",
            "boosted AMEX logo width");
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

    public static Task CashPaymentDialogAndSharedNumpadArePolishedAsync()
    {
        string app = Read("POS.Cashier.UI", "App.xaml");
        AuditAssert.Contains(
            app,
            "Resources/CashierTransactionDialogs.xaml",
            "Category 1 transaction-dialog resource dictionary");

        string transactionStyles = Read(
            "POS.Cashier.UI",
            "Resources",
            "CashierTransactionDialogs.xaml");
        AuditAssert.Contains(
            transactionStyles,
            "x:Key=\"CashierTransactionDialogHeader\"",
            "shared transaction-dialog header");
        AuditAssert.Contains(
            transactionStyles,
            "x:Key=\"CashierTransactionFieldIcon\"",
            "shared transaction field icon");
        AuditAssert.Contains(
            transactionStyles,
            "x:Key=\"CashierTransactionAmountPanel\"",
            "shared transaction amount panel");
        AuditAssert.Contains(
            transactionStyles,
            "x:Key=\"CashierTransactionConfirmButton\"",
            "shared transaction confirmation button");
        AuditAssert.Contains(
            transactionStyles,
            "x:Key=\"CashierTransactionNumpadPanel\"",
            "shared transaction numpad panel");

        string dialog = Read("POS.Cashier.UI", "Dialogs", "CashTenderDialog.xaml");
        AuditAssert.Contains(dialog, "Width=\"900\"", "Cash Payment reference width");
        AuditAssert.Contains(dialog, "Height=\"620\"", "Cash Payment reference height");
        AuditAssert.Contains(dialog, "MinWidth=\"820\"", "Cash Payment minimum width");
        AuditAssert.Contains(dialog, "MinHeight=\"540\"", "Cash Payment minimum height");
        AuditAssert.Contains(
            dialog,
            "Style=\"{StaticResource CashierTransactionDialogHeader}\"",
            "Cash Payment transaction header");
        AuditAssert.Contains(
            dialog,
            "VerticalScrollBarVisibility=\"Auto\"",
            "Cash Payment high-scaling overflow protection");
        AuditAssert.Contains(
            dialog,
            "KeyboardNavigation.TabNavigation=\"Cycle\"",
            "Cash Payment controlled keyboard navigation");
        AuditAssert.Contains(
            dialog,
            "PreviewTextInput=\"TenderedAmountTextBox_PreviewTextInput\"",
            "Cash Payment typed-input validation");
        AuditAssert.Contains(
            dialog,
            "DataObject.Pasting=\"TenderedAmountTextBox_Pasting\"",
            "Cash Payment pasted-input validation");
        AuditAssert.Contains(
            dialog,
            "IsEnterEnabled=\"{Binding CanConfirm}\"",
            "Cash Payment numpad confirmation availability");
        AuditAssert.Contains(
            dialog,
            "IsEnabled=\"{Binding CanConfirm}\"",
            "Cash Payment footer confirmation availability");
        AuditAssert.Contains(dialog, "IsDefault=\"True\"", "Cash Payment keyboard confirm action");
        AuditAssert.Contains(dialog, "IsCancel=\"True\"", "Cash Payment keyboard cancel action");
        AuditAssert.False(
            dialog.Contains("<Window.Resources>", StringComparison.Ordinal),
            "Cash Payment must not reintroduce local transaction styles.");

        string codeBehind = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "CashTenderDialog.xaml.cs");
        AuditAssert.Contains(
            codeBehind,
            "private bool _completionInProgress",
            "Cash Payment repeated-completion guard");
        AuditAssert.Contains(
            codeBehind,
            "private void ExecuteConfirm()",
            "Cash Payment single confirm route");
        AuditAssert.Contains(
            codeBehind,
            "private static bool IsValidMoneyCandidate",
            "Cash Payment numeric-entry policy");
        AuditAssert.Contains(
            codeBehind,
            "DialogResult = accepted",
            "Cash Payment single modal completion");

        string numpad = Read("POS.Cashier.UI", "Components", "TenderNumpadControl.xaml");
        AuditAssert.Contains(numpad, "MinWidth=\"280\"", "shared tender numpad width");
        AuditAssert.Contains(numpad, "MinHeight=\"360\"", "shared tender numpad height");
        AuditAssert.Contains(
            numpad,
            "Style=\"{StaticResource CashierTransactionNumpadPanel}\"",
            "shared transaction numpad panel style");
        AuditAssert.Contains(numpad, "Content=\"BACK\"", "stable textual backspace action");
        AuditAssert.Contains(
            numpad,
            "IsEnabled=\"{Binding IsEnterEnabled",
            "shared tender numpad disabled-enter behavior");

        string numpadCode = Read(
            "POS.Cashier.UI",
            "Components",
            "TenderNumpadControl.xaml.cs");
        AuditAssert.Contains(
            numpadCode,
            "IsEnterEnabledProperty",
            "shared tender numpad enter-enabled dependency property");

        string controls = Read("POS.Cashier.UI", "Resources", "CashierControls.xaml");
        AuditAssert.Contains(controls, "x:Key=\"CashierNumpadButton\"", "shared tender numpad button style");
        AuditAssert.Contains(controls, "CornerRadius=\"4\"", "shared tender numpad key corners");
        AuditAssert.Contains(
            controls,
            "CashierTransactionNumpadKeyBrush",
            "shared tender numpad resource colours");

        return Task.CompletedTask;
    }

    public static Task CheckoutTenderDialogsUseTransactionFamilyAsync()
    {
        string styles = Read(
            "POS.Cashier.UI",
            "Resources",
            "CashierTransactionDialogs.xaml");
        AuditAssert.Contains(
            styles,
            "x:Key=\"CashierTransactionInputTextBox\"",
            "shared transaction text input");
        AuditAssert.Contains(
            styles,
            "x:Key=\"CashierTransactionMoneyInputTextBox\"",
            "shared transaction money input");
        AuditAssert.Contains(
            styles,
            "x:Key=\"CashierTransactionReadOnlyPanel\"",
            "shared transaction read-only amount panel");
        AuditAssert.Contains(
            styles,
            "x:Key=\"CashierTransactionProtectedButton\"",
            "shared protected transaction action");

        string[] tenderDialogs =
        {
            "CardTenderDialog.xaml",
            "ChequeTenderDialog.xaml",
            "GiftVoucherTenderDialog.xaml"
        };

        foreach (string fileName in tenderDialogs)
        {
            string dialog = Read("POS.Cashier.UI", "Dialogs", fileName);
            AuditAssert.Contains(
                dialog,
                "Style=\"{StaticResource CashierTransactionDialogHeader}\"",
                $"{fileName} transaction header");
            AuditAssert.Contains(
                dialog,
                "Style=\"{StaticResource CashierTransactionConfirmButton}\"",
                $"{fileName} transaction confirmation button");
            AuditAssert.Contains(
                dialog,
                "VerticalScrollBarVisibility=\"Auto\"",
                $"{fileName} high-scaling overflow protection");
            AuditAssert.Contains(
                dialog,
                "KeyboardNavigation.TabNavigation=\"Cycle\"",
                $"{fileName} keyboard navigation");
            AuditAssert.Contains(
                dialog,
                "WindowStartupLocation=\"CenterOwner\"",
                $"{fileName} owner centering");
            AuditAssert.False(
                dialog.Contains("<Window.Resources>", StringComparison.Ordinal),
                $"{fileName} must use shared transaction styles instead of local styles.");
        }

        string card = Read("POS.Cashier.UI", "Dialogs", "CardTenderDialog.xaml");
        AuditAssert.Contains(
            card,
            "Style=\"{StaticResource CashierTransactionMoneyInputTextBox}\"",
            "Card Payment shared money input");
        AuditAssert.Contains(
            card,
            "<components:TenderNumpadControl",
            "Card Payment shared tender numpad");
        AuditAssert.Contains(
            card,
            "IsEnabled=\"{Binding CanConfirm}\"",
            "Card Payment confirmation availability");

        string cheque = Read("POS.Cashier.UI", "Dialogs", "ChequeTenderDialog.xaml");
        AuditAssert.Contains(
            cheque,
            "Style=\"{StaticResource CashierTransactionMoneyInputTextBox}\"",
            "Cheque Payment shared money input");
        AuditAssert.Contains(
            cheque,
            "<components:TenderNumpadControl",
            "Cheque Payment shared tender numpad");
        AuditAssert.Contains(
            cheque,
            "Style=\"{StaticResource CashierDatePicker}\"",
            "Cheque Payment shared date input");

        string voucher = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "GiftVoucherTenderDialog.xaml");
        AuditAssert.Contains(
            voucher,
            "CashierTransactionProtectedButton",
            "Gift Voucher manager-approval action");
        AuditAssert.Contains(
            voucher,
            "Command=\"{Binding SearchVoucherCommand}\"",
            "Gift Voucher verification command");
        AuditAssert.Contains(
            voucher,
            "IsEnabled=\"{Binding CanConfirm}\"",
            "Gift Voucher confirmation availability");

        string salesView = Read("POS.Cashier.UI", "Views", "SalesView.xaml.cs");
        AuditAssert.Contains(
            salesView,
            "AddConfirmedCustomerCreditPayment(amount)",
            "existing direct Customer Credit checkout route");
        AuditAssert.False(
            salesView.Contains("new CustomerCreditTenderDialog", StringComparison.Ordinal),
            "Category 1A must not introduce an unapproved Customer Credit dialog or workflow change.");

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
        int database = source.IndexOf(
            "await InitializeDatabaseAsync()",
            StringComparison.Ordinal);
        int initialize = source.IndexOf(
            "await InitializeTerminalAndLicenseAsync()",
            StringComparison.Ordinal);
        int login = source.IndexOf(
            "await ShowLoginWindowAsync()",
            StringComparison.Ordinal);

        AuditAssert.True(
            database >= 0 &&
            initialize >= 0 &&
            login >= 0 &&
            database < initialize &&
            initialize < login,
            "Cashier startup must initialize the database, enforce the terminal licence, and only then show login.");
        AuditAssert.Contains(source, "CanRunCashier", "Cashier run licence gate");
        AuditAssert.Contains(source, "ExpiringSoon", "licence expiry warning path");
        AuditAssert.Contains(
            source,
            "InitializeCashierAsync",
            "delegated Cashier database initialization");

        string databaseInitialization = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseInitializationService.cs");
        AuditAssert.Contains(
            databaseInitialization,
            "_settings.IsStandaloneSqlite",
            "standalone database initialization branch");
        AuditAssert.Contains(
            databaseInitialization,
            "Database.MigrateAsync",
            "standalone startup migration path");
        AuditAssert.Contains(
            databaseInitialization,
            "Database.CanConnectAsync",
            "central database connectivity validation");
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
