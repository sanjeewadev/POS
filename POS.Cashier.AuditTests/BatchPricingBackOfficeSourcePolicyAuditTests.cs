using POS.Core.Configuration;

namespace POS.Cashier.AuditTests;

internal static class BatchPricingBackOfficeSourcePolicyAuditTests
{
    public static Task BackOfficeBatchPricingWorkflowIsExplicitAndGroupedAsync()
    {
        VerifyPricingWorkflow();
        VerifyGroupedHistory();
        VerifyDatabaseAuthority();
        return Task.CompletedTask;
    }

    private static void VerifyPricingWorkflow()
    {
        string view = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "PriceManagementView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PriceManagementViewModel.cs");
        string viewCodeBehind = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "PriceManagementView.xaml.cs");
        string quickDialog = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "MasterPriceQuickChangeDialog.xaml");
        string quickDialogCode = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "MasterPriceQuickChangeDialog.xaml.cs");
        string quickDialogViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "MasterPriceQuickChangeDialogViewModel.cs");
        string calculator = Read(
            "POS.Core",
            "Services",
            "Pricing",
            "SellingPriceSuggestionCalculator.cs");
        string repository = Read(
            "POS.Core",
            "Repositories",
            "PriceManagementRepository.cs");

        AuditAssert.Contains(view, "SELECTED ITEM — MASTER PRICING REFERENCE", "read-only master-price reference");
        AuditAssert.Contains(view, "QUICK CHANGE MASTER PRICE", "master price dialog entry point");
        AuditAssert.Contains(view, "SELECTED ITEM — EXACT BATCH PRICING", "selected exact-batch panel");
        AuditAssert.Contains(view, "USE MASTER PRICE", "clear override-reversion action");
        AuditAssert.Contains(view, "Batch Cost", "exact batch cost reference");
        AuditAssert.Contains(view, "Average Cost", "average cost reference");
        AuditAssert.Contains(view, "Retail profit / margin", "batch Retail profitability preview");
        AuditAssert.Contains(view, "W/S profit / margin", "batch Wholesale profitability preview");
        AuditAssert.Contains(view, "Binding=\"{Binding ItemTypeText, Mode=OneWay}\"", "read-only item type binding");
        AuditAssert.Contains(view, "Binding=\"{Binding PriceSourceText, Mode=OneWay}\"", "read-only batch price-source binding");
        AuditAssert.Contains(view, "ShowBatchEmptyState", "Pricing batch empty-state visibility");
        AuditAssert.Contains(view, "UpdateSourceTrigger=LostFocus", "stable batch decimal editor binding");
        AuditAssert.Contains(view, "ValidatesOnExceptions=True", "invalid batch decimal validation");
        AuditAssert.Contains(view, "Click=\"SaveBatchOverride_Click\"", "batch price explicit commit route");
        AuditAssert.False(view.Contains("MasterMinimumPriceTextBox", StringComparison.Ordinal), "inline master price editor remains.");
        AuditAssert.False(view.Contains("REMOVE BATCH PRICE OVERRIDE", StringComparison.Ordinal), "destructive override wording remains.");
        AuditAssert.False(view.Contains("DangerButton", StringComparison.Ordinal), "batch reversion remains styled as destructive.");

        AuditAssert.Contains(viewCodeBehind, "MasterPriceQuickChangeDialog", "owner-aware master price dialog route");
        AuditAssert.Contains(viewCodeBehind, "Owner = Window.GetWindow(this)", "master price dialog owner");
        AuditAssert.Contains(viewCodeBehind, "TryCommitPriceEditors", "batch editor commit helper");
        AuditAssert.Contains(viewCodeBehind, "binding?.UpdateSource()", "batch binding source commit");
        AuditAssert.Contains(viewCodeBehind, "Validation.GetHasError", "batch invalid-text guard");

        AuditAssert.Contains(quickDialog, "CALCULATE FROM COST (OPTIONAL)", "cost-based price suggestion section");
        AuditAssert.Contains(quickDialogViewModel, "SellingPriceCalculationMethods.MarkupOnCost", "master price markup option");
        AuditAssert.Contains(quickDialogViewModel, "SellingPriceCalculationMethods.TargetMargin", "master price margin option");
        AuditAssert.Contains(quickDialogViewModel, "SellingPriceCalculationMethods.DiscountFromRetail", "Wholesale Retail-discount option");
        AuditAssert.Contains(quickDialog, "Average Cost and Last Cost are read-only", "cost authority guidance");
        AuditAssert.Contains(quickDialogCode, "TryBuildResult", "master dialog explicit validation");
        AuditAssert.Contains(quickDialogCode, "Confirm Master Price Change", "master dialog confirmation");
        AuditAssert.Contains(quickDialogViewModel, "Wholesale price cannot be greater than Retail price", "Wholesale ordering validation");
        AuditAssert.Contains(quickDialogViewModel, "No master price has changed", "no-change master guard");
        AuditAssert.Contains(calculator, "FromMarkup", "markup calculation authority");
        AuditAssert.Contains(calculator, "FromTargetMargin", "target-margin calculation authority");
        AuditAssert.Contains(calculator, "FromRetailDiscount", "Wholesale Retail-discount authority");
        AuditAssert.Contains(calculator, "RoundToIncrement", "price rounding authority");

        AuditAssert.Contains(viewModel, "ApplyMasterPriceChangeAsync", "master price save handoff");
        AuditAssert.Contains(viewModel, "SaveBatchOverrideAsync", "Pricing batch override save command");
        AuditAssert.Contains(viewModel, "UseMasterPriceAsync", "Pricing master-price reversion command");
        AuditAssert.Contains(viewModel, "ProposedRetailProfit", "batch profit confirmation");
        AuditAssert.False(viewModel.Contains("\"Admin\"", StringComparison.Ordinal), "Pricing still hard-codes Admin.");

        AuditAssert.Contains(repository, "IsolationLevel.Serializable", "Pricing serializable transaction");
        AuditAssert.Contains(repository, PriceChangeActionCodes.BatchOverrideCreated, "batch override created history action");
        AuditAssert.Contains(repository, PriceChangeActionCodes.BatchOverrideUpdated, "batch override updated history action");
        AuditAssert.Contains(repository, PriceChangeActionCodes.BatchOverrideRemoved, "batch override removed history action");
        AuditAssert.Contains(repository, "OldPriceSource = oldPrice.PriceSource", "batch history old price source");
        AuditAssert.Contains(repository, "NewPriceSource = newPrice.PriceSource", "batch history new price source");
    }

    private static void VerifyGroupedHistory()
    {
        string view = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "PriceChangeHistoryView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PriceChangeHistoryViewModel.cs");
        string repository = Read(
            "POS.Core",
            "Repositories",
            "PriceChangeHistoryRepository.cs");
        string dto = Read(
            "POS.Core",
            "Models",
            "DTOs",
            "PriceChangeHistoryDtos.cs");

        AuditAssert.Contains(view, "ItemsSource=\"{Binding Operations}\"", "grouped history operation grid");
        AuditAssert.Contains(view, "ItemsSource=\"{Binding OperationDetails}\"", "grouped history detail grid");
        AuditAssert.Contains(view, "Header=\"Master Change\"", "grouped master summary column");
        AuditAssert.Contains(view, "Header=\"Batch Overrides\"", "grouped batch summary column");
        AuditAssert.Contains(view, "Header=\"Old Source\"", "history old source column");
        AuditAssert.Contains(view, "Header=\"New Source\"", "history new source column");
        AuditAssert.Contains(view, "Visibility=\"{Binding HasSelectedOperation, Converter={StaticResource BooleanToVisibilityConverter}}\"", "history detail visibility");
        AuditAssert.Contains(view, "No saved price-change operations match the selected filters.", "history empty-state guidance");
        AuditAssert.Contains(viewModel, "IsHistoryEmpty", "history empty-state property");

        AuditAssert.Contains(viewModel, "ObservableCollection<PriceChangeOperationSummaryDto>", "operation summary collection");
        AuditAssert.Contains(viewModel, "ObservableCollection<PriceChangeDetailDto>", "operation detail collection");
        AuditAssert.Contains(repository, ".GroupBy(row => row.PriceChangeNo)", "PriceChangeNo grouping");
        AuditAssert.Contains(repository, ".Take(maxOperations)", "grouped operation row limit");
        AuditAssert.Contains(repository, "GetOperationDetailsAsync", "complete grouped detail query");
        AuditAssert.Contains(dto, "OperationKey", "stable grouped history key");
        AuditAssert.Contains(dto, "PriceSourceTransition", "history source transition display");
    }

    private static void VerifyDatabaseAuthority()
    {
        string sqliteMigration = Read(
            "POS.Core",
            "Migrations",
            "20260726120000_CompleteBatchPricingBackOfficeWorkflow.cs");
        string sqlMigration = Read(
            "POS.Database.Setup",
            "Migrations",
            "20260726121000_CompleteBatchPricingBackOfficeWorkflow.cs");
        string releaseInfo = Read(
            "POS.Core",
            "Configuration",
            "ProductReleaseInfo.cs");

        AuditAssert.Contains(sqliteMigration, "ChangeAction", "SQLite history action column");
        AuditAssert.Contains(sqliteMigration, "OldPriceSource", "SQLite old source column");
        AuditAssert.Contains(sqliteMigration, "NewPriceSource", "SQLite new source column");
        AuditAssert.Contains(sqliteMigration, "SellingPriceAction", "SQLite GRN action column");
        AuditAssert.Contains(sqlMigration, "ChangeAction", "SQL Server history action column");
        AuditAssert.Contains(sqlMigration, "SellingPriceAction", "SQL Server GRN action column");
        AuditAssert.Contains(releaseInfo, "20260726121000_CompleteBatchPricingBackOfficeWorkflow", "required SQL Server Patch 2 migration");
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
