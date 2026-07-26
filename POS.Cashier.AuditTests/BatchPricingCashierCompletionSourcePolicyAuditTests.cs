namespace POS.Cashier.AuditTests;

internal static class BatchPricingCashierCompletionSourcePolicyAuditTests
{
    public static Task CashierBatchSelectionAndHistoricalSourceAreControlledAsync()
    {
        VerifySharedRouting();
        VerifySelector();
        VerifyProductSeek();
        VerifyHistoricalReporting();
        VerifyCurrentAndHistoricalOutputBoundary();
        return Task.CompletedTask;
    }

    private static void VerifySharedRouting()
    {
        string sales = Read("POS.Cashier.UI", "ViewModels", "SalesViewModel.cs");
        string repository = Read("POS.Core", "Repositories", "ItemMasterRepository.cs");
        string service = Read("POS.Cashier.UI", "Services", "CashierBatchSelectionService.cs");

        int exactBatchLookup = sales.IndexOf("GetSellableBatchByInternalBarcodeAsync(term)", StringComparison.Ordinal);
        int variantLookup = sales.IndexOf("GetSellableItemByBarcodeOrSkuAsync(term)", StringComparison.Ordinal);
        AuditAssert.True(exactBatchLookup >= 0 && variantLookup > exactBatchLookup,
            "Exact internal batch barcode must retain lookup precedence.");

        AuditAssert.Contains(sales, "RouteSellableItemToCartAsync(item, 1m)", "normal barcode shared routing");
        AuditAssert.Contains(sales, "RouteSellableItemToCartAsync(item, quantity)", "variant and requested-quantity shared routing");
        AuditAssert.Contains(sales, "batches.Count == 1", "single batch automatic route");
        AuditAssert.Contains(sales, "batches.Count == 0", "zero batch rejection route");
        AuditAssert.Contains(sales, "_batchSelectionService.SelectBatchAsync", "multiple batch selector route");
        AuditAssert.Contains(sales, "_batchSelectionGate.WaitAsync(0)", "single active selector gate");
        AuditAssert.Contains(sales, "ExpectedRetailPrice", "selector Retail stale check");
        AuditAssert.Contains(sales, "ExpectedWholesalePrice", "selector Wholesale stale check");
        AuditAssert.Contains(sales, "ExpectedPriceSource", "selector source stale check");
        AuditAssert.False(sales.Contains("Batch item. Scan GRN batch barcode.", StringComparison.Ordinal),
            "Generic batch barcode/SKU flow is still blocked.");

        AuditAssert.Contains(repository, "GetSellableBatchesByVariantIdAsync", "shared sellable batch query");
        AuditAssert.Contains(repository, "await GetSellableBatchesByVariantIdAsync(itemVariantId)", "Product Seek shares Cashier batch authority");
        AuditAssert.Contains(repository, "b.BatchNo.ToUpper() != GeneralBatchNo", "physical batch requirement");
        AuditAssert.Contains(repository, ".ThenBy(b => b.ItemBatchId)", "deterministic FEFO tie breaker");
        AuditAssert.Contains(service, "Window? active", "owner-aware selector service");
        AuditAssert.Contains(service, "ShowDialog", "modal exact-batch selection");
    }

    private static void VerifySelector()
    {
        string view = Read("POS.Cashier.UI", "Dialogs", "BatchSelectionDialog.xaml");
        string code = Read("POS.Cashier.UI", "Dialogs", "BatchSelectionDialog.xaml.cs");
        string viewModel = Read("POS.Cashier.UI", "ViewModels", "BatchSelectionViewModel.cs");

        AuditAssert.Contains(view, "Style=\"{StaticResource CashierOperationalWindow}\"", "Category 2 selector family");
        AuditAssert.Contains(view, "Header=\"Effective Retail\"", "selector effective Retail");
        AuditAssert.Contains(view, "Header=\"Effective Wholesale\"", "selector effective Wholesale");
        AuditAssert.Contains(view, "Header=\"Active Price\"", "selector active price");
        AuditAssert.Contains(view, "Header=\"Price Source\"", "selector price source");
        AuditAssert.Contains(view, "MouseDoubleClick=\"BatchDataGrid_MouseDoubleClick\"", "selector double-click confirmation");
        AuditAssert.Contains(code, "e.Key == Key.Enter", "selector Enter confirmation");
        AuditAssert.Contains(code, "e.Key == Key.Escape", "selector Escape cancellation");
        AuditAssert.Contains(viewModel, "HasSufficientQuantity", "selector requested-quantity validation");
        AuditAssert.Contains(viewModel, "SelectionResult = new CashierBatchSelectionResult", "selector immutable result snapshot");
        AuditAssert.Contains(viewModel, "IsSubmitting = true", "selector repeated-submission guard");
    }

    private static void VerifyProductSeek()
    {
        string view = Read("POS.Cashier.UI", "Dialogs", "ProductSeekDialog.xaml");
        string code = Read("POS.Cashier.UI", "Dialogs", "ProductSeekDialog.xaml.cs");
        string viewModel = Read("POS.Cashier.UI", "ViewModels", "PluSearchViewModel.cs");

        AuditAssert.Contains(view, "Header=\"Master Retail\"", "Product Seek master Retail label");
        AuditAssert.Contains(view, "Header=\"Master Wholesale\"", "Product Seek master Wholesale label");
        AuditAssert.Contains(view, "Header=\"Effective Retail\"", "Product Seek effective Retail");
        AuditAssert.Contains(view, "Header=\"Effective Wholesale\"", "Product Seek effective Wholesale");
        AuditAssert.Contains(view, "Header=\"Active Price\"", "Product Seek active price");
        AuditAssert.Contains(view, "Header=\"Source\"", "Product Seek source");
        AuditAssert.Contains(viewModel, "ConfigurePricingMode", "Product Seek sale-mode configuration");
        AuditAssert.Contains(viewModel, "batch.ActivePrice = IsWholesaleMode", "Product Seek mode-aware active price");
        AuditAssert.Contains(code, "_viewModel.SelectedBatch = batch", "single click selects Product Seek batch");
        AuditAssert.Contains(code, "ExecuteBatchRowAction(_viewModel.SelectedBatch)", "Enter confirms Product Seek batch");
    }

    private static void VerifyHistoricalReporting()
    {
        string dto = Read("POS.Core", "Models", "DTOs", "SalesExplorerDtos.cs");
        string repository = Read("POS.Core", "Repositories", "MasterSalesAnalyticsRepository.cs");
        string view = Read("POS.BackOffice.UI", "Views", "Pages", "Sales", "SalesExplorerView.xaml");

        AuditAssert.Contains(dto, "CataloguePriceSourceText", "historical source display mapping");
        AuditAssert.Contains(repository, "BatchNo = line.BatchNo", "saved sale-line batch snapshot");
        AuditAssert.Contains(repository, "CataloguePriceSource = line.CataloguePriceSourceSnapshot", "saved sale-line source snapshot");
        AuditAssert.Contains(view, "Header=\"Batch\"", "Sales Explorer batch column");
        AuditAssert.Contains(view, "Header=\"Price Source\"", "Sales Explorer source column");
        AuditAssert.False(repository.Contains("EffectiveSellingPriceResolver", StringComparison.Ordinal),
            "Historical Sales Explorer must not recalculate current prices.");
    }


    private static void VerifyCurrentAndHistoricalOutputBoundary()
    {
        string barcodeRepository = Read("POS.Core", "Repositories", "BarcodePrinterRepository.cs");
        string stockRepository = Read("POS.Core", "Repositories", "StockBalanceRepository.cs");
        string exportBuilder = Read("POS.Core", "Services", "Exports", "OperationalExportBuilder.cs");
        string dashboardRepository = Read("POS.Core", "Repositories", "DashboardRepository.cs");
        string historicalRepository = Read("POS.Core", "Repositories", "MasterSalesAnalyticsRepository.cs");

        AuditAssert.Contains(barcodeRepository, "EffectiveSellingPriceResolver.Resolve", "current barcode-label effective price");
        AuditAssert.Contains(stockRepository, "EffectiveSellingPriceResolver.Resolve", "current stock and expiry effective price");
        AuditAssert.Contains(exportBuilder, "row.EffectiveRetailPrice", "current export effective Retail");
        AuditAssert.Contains(exportBuilder, "row.EffectiveWholesalePrice", "current export effective Wholesale");
        AuditAssert.Contains(exportBuilder, "row.PriceSourceText", "current export price source");
        AuditAssert.Contains(dashboardRepository, "variant.ItemParent.ItemType != ItemTypeCodes.Service", "dashboard Services excluded from stock alerts");
        AuditAssert.False(dashboardRepository.Contains("RetailPrice", StringComparison.Ordinal),
            "Quantity-only Dashboard must not invent a current Retail valuation.");
        AuditAssert.False(dashboardRepository.Contains("WholesalePrice", StringComparison.Ordinal),
            "Quantity-only Dashboard must not invent a current Wholesale valuation.");
        AuditAssert.False(historicalRepository.Contains("HasSellingPriceOverride", StringComparison.Ordinal),
            "Historical analytics must not infer source from current override state.");
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
