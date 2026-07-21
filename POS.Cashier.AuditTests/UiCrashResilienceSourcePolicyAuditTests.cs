namespace POS.Cashier.AuditTests;

internal static class UiCrashResilienceSourcePolicyAuditTests
{
    public static Task RecoverableUiFailuresAreContainedAsync()
    {
        VerifyBackOfficeDatabaseRoutes();
        VerifyBackOfficeIoRoutes();
        VerifyBackOfficeDialogAndNavigationRoutes();
        VerifyCashierDialogRoutes();
        VerifyFatalHandlersRemainFinalFailSafes();

        return Task.CompletedTask;
    }

    private static void VerifyBackOfficeDatabaseRoutes()
    {
        AssertGuarded(
            Read("POS.BackOffice.UI", "ViewModels", "FloatCashLogViewModel.cs"),
            "private async Task OpenDrillDownAsync",
            "Open Float Cash Log drill-down");

        string express = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "ExpressItemAdminViewModel.cs");

        AssertGuarded(express, "private async Task SearchAsync", "Search Express Item candidates");
        AssertGuarded(express, "private async Task ClearAsync", "Clear Express Item form");
        AssertGuarded(express, "private async Task RefreshAsync", "Refresh Express Item layouts");
        AuditAssert.Contains(
            ExtractMethodBody(express, "private async Task LoadLayoutsAsync"),
            "var rows = await _repository.GetAdminLayoutsAsync();",
            "Express Item layout query before collection replacement");

        string grnDashboard = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GrnDashboardViewModel.cs");

        AssertGuarded(grnDashboard, "private async Task LoadDataAsync", "Load GRN Dashboard");
        AssertQueryBeforeClear(
            grnDashboard,
            "private async Task LoadDataInternalAsync",
            "GetGrnSummariesAsync",
            "GrnDocuments.Clear()",
            "GRN Dashboard preserves prior rows on query failure");

        string poDashboard = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PurchaseOrderDashboardViewModel.cs");

        AssertGuarded(poDashboard, "private async Task LoadDataAsync", "Load Purchase Order Dashboard");
        AssertQueryBeforeClear(
            poDashboard,
            "private async Task LoadDataInternalAsync",
            "GetPoSummariesAsync",
            "PurchaseOrders.Clear()",
            "Purchase Order Dashboard preserves prior rows on query failure");

        string cashMovement = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "CashMovementDashboardViewModel.cs");

        AssertGuarded(cashMovement, "private async Task LoadDataAsync", "Load Cash Movement Dashboard");

        string itemMaster = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "ItemMasterViewModel.cs");

        AssertGuarded(itemMaster, "private async Task GenerateVariantsAsync", "Generate Item Master variants");
        AssertGuarded(itemMaster, "private async Task DeleteUnusedItemAsync", "Delete unused Item Master item");
        AssertGuarded(itemMaster, "private async Task DeactivateItemAsync", "Deactivate Item Master item");
        AssertGuarded(itemMaster, "private async Task ReactivateItemAsync", "Reactivate Item Master item");
        AssertGuarded(itemMaster, "private void AssignSupplierToSelectedVariants", "Assign suppliers to Item Master variants");

        string supplierClaims = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "SupplierClaimsViewModel.cs");

        AssertGuarded(supplierClaims, "private async Task RunBusyAsync", "Supplier Claims operation");
        AuditAssert.Contains(
            ExtractMethodBody(supplierClaims, "private async Task SubmitAsync"),
            "await RunBusyAsync",
            "Supplier Claims submit uses guarded operation");
    }

    private static void VerifyBackOfficeIoRoutes()
    {
        string backup = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "BackupRestoreViewModel.cs");

        AssertGuarded(backup, "private async Task CreateBackupAsync", "Create manual POS backup");
        AssertGuarded(backup, "private async Task RestoreBackupAsync", "Restore POS backup");
        AssertGuarded(backup, "private bool EnsureSelectedBackupFile", "Select required POS backup file");

        string floatCash = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "FloatCashLogViewModel.cs");
        AssertGuarded(floatCash, "private void ExportToExcel", "Export Float Cash Log CSV");

        string cashMovement = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "CashMovementDashboardViewModel.cs");
        AssertGuarded(cashMovement, "private void Export", "Export Cash Movement CSV");

        string vouchers = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GiftVoucherAdminViewModel.cs");
        AssertGuarded(vouchers, "private void ExportCsv", "Export Gift Voucher CSV");

        string ledger = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "CustomerLedgerViewModel.cs");
        AssertGuarded(ledger, "private void PrintStatement", "Print Customer Ledger statement");

        AssertGuarded(
            Read("POS.BackOffice.UI", "Views", "Dialogs", "CreditNotePreviewDialog.xaml.cs"),
            "private void Copy_Click",
            "Copy Customer Credit Note");

        AssertGuarded(
            Read("POS.BackOffice.UI", "Views", "Dialogs", "SupplierDebitNotePreviewDialog.xaml.cs"),
            "private void Copy_Click",
            "Copy Supplier Debit Note");
    }

    private static void VerifyBackOfficeDialogAndNavigationRoutes()
    {
        AssertGuarded(
            Read("POS.BackOffice.UI", "ViewModels", "MainViewModel.cs"),
            "private void NavigateTo<TPage>",
            "Navigate to");

        AssertGuarded(
            Read("POS.BackOffice.UI", "ViewModels", "GrnViewModel.Phase7D2.cs"),
            "private async Task OpenBulkSellingPriceDialogAsync",
            "Open GRN bulk selling-price dialog");

        string claimView = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Finance",
            "SupplierClaimsView.xaml.cs");

        AssertGuarded(claimView, "private async void Settle_Click", "Settle Supplier Claim");
        AssertGuarded(claimView, "private async void Reject_Click", "Reject Supplier Claim");

        AssertGuarded(
            Read(
                "POS.BackOffice.UI",
                "Views",
                "Pages",
                "Admin",
                "UserManagementView.xaml.cs"),
            "private async void BtnSaveUser_Click",
            "Save User Management user");
    }

    private static void VerifyCashierDialogRoutes()
    {
        string sales = Read(
            "POS.Cashier.UI",
            "ViewModels",
            "SalesViewModel.cs");

        AssertGuarded(sales, "public async Task AddFloatAsync", "Open Float Cash workflow");
        AssertGuarded(sales, "public async Task PrintXReportAsync", "Print X Report command");
    }

    private static void VerifyFatalHandlersRemainFinalFailSafes()
    {
        string backOfficeApp = Read("POS.BackOffice.UI", "App.xaml.cs");
        string cashierApp = Read("POS.Cashier.UI", "App.xaml.cs");

        AuditAssert.Contains(
            ExtractMethodBody(backOfficeApp, "private void App_DispatcherUnhandledException"),
            "Shutdown(-1);",
            "BackOffice fatal handler remains final fail-safe");

        AuditAssert.Contains(
            ExtractMethodBody(cashierApp, "private void App_DispatcherUnhandledException"),
            "Shutdown(-1);",
            "Cashier fatal handler remains final fail-safe");
    }

    private static void AssertGuarded(
        string source,
        string methodMarker,
        string logContext)
    {
        string body = ExtractMethodBody(source, methodMarker);

        AuditAssert.Contains(
            body,
            "catch (Exception ex)",
            $"{methodMarker} local exception boundary");

        AuditAssert.Contains(
            body,
            "LocalLogService.WriteException",
            $"{methodMarker} technical logging");

        AuditAssert.Contains(
            body,
            logContext,
            $"{methodMarker} log context");
    }

    private static void AssertQueryBeforeClear(
        string source,
        string methodMarker,
        string queryMarker,
        string clearMarker,
        string label)
    {
        string body = ExtractMethodBody(source, methodMarker);
        int queryIndex = body.IndexOf(queryMarker, StringComparison.Ordinal);
        int clearIndex = body.IndexOf(clearMarker, StringComparison.Ordinal);

        AuditAssert.True(
            queryIndex >= 0 && clearIndex > queryIndex,
            $"{label}: query must complete before existing rows are cleared.");
    }

    private static string ExtractMethodBody(string source, string methodMarker)
    {
        int methodIndex = source.IndexOf(methodMarker, StringComparison.Ordinal);
        AuditAssert.True(methodIndex >= 0, $"Method marker was not found: {methodMarker}");

        int openBrace = source.IndexOf('{', methodIndex);
        AuditAssert.True(openBrace >= 0, $"Method opening brace was not found: {methodMarker}");

        int depth = 0;
        for (int index = openBrace; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                        return source[openBrace..(index + 1)];
                    break;
            }
        }

        throw new InvalidOperationException(
            $"Method closing brace was not found: {methodMarker}");
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
