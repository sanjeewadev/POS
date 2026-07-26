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
        string repository = Read(
            "POS.Core",
            "Repositories",
            "PriceManagementRepository.cs");

        AuditAssert.Contains(view, "SELECTED ITEM — MASTER PRICES", "selected master-price editor");
        AuditAssert.Contains(view, "SELECTED ITEM — BATCH PRICES", "selected exact-batch panel");
        AuditAssert.Contains(view, "Binding=\"{Binding BatchPriceStatus}\"", "batch-price status column");
        AuditAssert.Contains(view, "Binding=\"{Binding PriceSourceText}\"", "batch price-source column");
        AuditAssert.Contains(view, "MinHeight=\"255\"", "Pricing editor minimum height");
        AuditAssert.Contains(view, "LastChildFill=\"False\"", "Pricing compact header docking");
        AuditAssert.Contains(view, "ShowBatchEmptyState", "Pricing batch empty-state visibility");
        AuditAssert.Contains(viewModel, "IsPricingListEmpty", "Pricing item empty-state property");
        AuditAssert.Contains(viewModel, "No active physical batches with available stock", "Pricing batch empty-state guidance");
        AuditAssert.False(view.Contains("Change Reason", StringComparison.OrdinalIgnoreCase), "Pricing still requires a reason.");
        AuditAssert.False(view.Contains("Projected", StringComparison.OrdinalIgnoreCase), "Pricing still shows projected-value cards.");
        AuditAssert.False(view.Contains("Good Margin", StringComparison.OrdinalIgnoreCase), "Pricing still shows margin classifications.");

        AuditAssert.Contains(viewModel, "CurrentUsername()", "Pricing authenticated username retrieval");
        AuditAssert.Contains(viewModel, "SaveBatchOverrideAsync", "Pricing batch override save command");
        AuditAssert.Contains(viewModel, "RemoveBatchOverrideAsync", "Pricing batch override removal command");
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
