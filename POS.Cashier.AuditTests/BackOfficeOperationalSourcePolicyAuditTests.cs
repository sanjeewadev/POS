using System.Text.RegularExpressions;

namespace POS.Cashier.AuditTests;

internal static class BackOfficeOperationalSourcePolicyAuditTests
{
    public static Task PurchasingAndStockControlsAreWiredAsync()
    {
        VerifyStockAdjustmentReadOnlyBindings();
        VerifyStockAdjustmentRecalculationHook();
        VerifyStockBalanceOperationalViews();
        VerifyPurchaseOrderEntryAndPdfWorkflow();
        VerifyPurchaseOrderAndGrnHistoryReadability();
        VerifyPoLinkedGrnExpiryEditor();
        VerifyNonVatSupplierPurchasingPolicy();

        return Task.CompletedTask;
    }

    private static void VerifyStockAdjustmentReadOnlyBindings()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "StockAdjustmentView.xaml");

        AuditAssert.Equal(
            2,
            CountOccurrences(xaml, "Text=\"{Binding BatchDisplayText, Mode=OneWay}\""),
            "Stock Adjustment batch display OneWay binding count");
        AuditAssert.Equal(
            2,
            CountOccurrences(xaml, "Text=\"{Binding BatchBarcodeDisplayText, Mode=OneWay}\""),
            "Stock Adjustment batch barcode OneWay binding count");

        AuditAssert.False(
            xaml.Contains("Text=\"{Binding BatchDisplayText}\"", StringComparison.Ordinal),
            "Stock Adjustment still contains a default TwoWay BatchDisplayText TextBox binding.");
        AuditAssert.False(
            xaml.Contains("Text=\"{Binding BatchBarcodeDisplayText}\"", StringComparison.Ordinal),
            "Stock Adjustment still contains a default TwoWay BatchBarcodeDisplayText TextBox binding.");
    }

    private static void VerifyStockAdjustmentRecalculationHook()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "StockAdjustmentView.xaml");
        string codeBehind = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "StockAdjustmentView.xaml.cs");

        AuditAssert.Contains(
            xaml,
            "CellEditEnding=\"DgAdjustmentLines_CellEditEnding\"",
            "Stock Adjustment queued-line edit recalculation event");
        AuditAssert.Contains(
            codeBehind,
            "viewModel.RecalculateImpact();",
            "Stock Adjustment queued-line recalculation call");
    }

    private static void VerifyStockBalanceOperationalViews()
    {
        string view = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "StockBalanceView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "StockBalanceViewModel.cs");
        string repository = Read(
            "POS.Core",
            "Repositories",
            "StockBalanceRepository.cs");
        string dto = Read(
            "POS.Core",
            "Models",
            "DTOs",
            "StockBalanceDtos.cs");
        string exportBuilder = Read(
            "POS.Core",
            "Services",
            "Exports",
            "OperationalExportBuilder.cs");

        foreach (string viewName in new[]
        {
            "Stock Balance &amp; Valuation",
            "Expiry Monitor",
            "Stock Alerts"
        })
        {
            AuditAssert.Contains(
                view,
                $"Value=\"{viewName}\"",
                $"Stock Balance view trigger: {viewName}");
        }

        AuditAssert.Contains(
            viewModel,
            "\"Stock Alerts\"",
            "Stock Alerts view option");
        AuditAssert.Contains(
            viewModel,
            "GetStockAlertsAsync",
            "Stock Alerts repository call");
        AuditAssert.Contains(
            viewModel,
            "BuildStockAlertsCsv",
            "Stock Alerts CSV call");
        AuditAssert.False(
            viewModel.Contains("HideZeroStock", StringComparison.Ordinal),
            "Obsolete zero-stock checkbox state remains in the view model.");
        AuditAssert.False(
            viewModel.Contains("ShowNegativeOnly", StringComparison.Ordinal),
            "Obsolete negative-only checkbox state remains in the view model.");
        AuditAssert.False(
            viewModel.Contains("PositiveExpiryStockOnly", StringComparison.Ordinal),
            "Obsolete positive-expiry checkbox state remains in the view model.");

        AuditAssert.False(
            view.Contains("Hide Zero Stock Items", StringComparison.Ordinal),
            "Obsolete Hide Zero Stock Items checkbox remains visible.");
        AuditAssert.False(
            view.Contains("Show Negative Stock Only", StringComparison.Ordinal),
            "Obsolete Show Negative Stock Only checkbox remains visible.");
        AuditAssert.False(
            view.Contains("<CheckBox Content=\"Positive stock only\"", StringComparison.Ordinal),
            "Obsolete Expiry Monitor positive-stock checkbox remains visible.");
        AuditAssert.False(
            view.Contains("Missing Expiry", StringComparison.Ordinal),
            "Missing-expiry store-facing summary remains visible.");
        AuditAssert.Contains(
            view,
            "ItemsSource=\"{Binding StockAlertRows}\"",
            "Stock Alerts grid rows");
        AuditAssert.Contains(
            view,
            "ItemsSource=\"{Binding StockAlertFilters}\"",
            "Stock Alerts filter");
        AuditAssert.Contains(
            view,
            "Command=\"{Binding ExportStockAlertsCsvCommand}\"",
            "Stock Alerts CSV action");

        AuditAssert.Contains(
            repository,
            "positiveStockOnly && totalQty <= 0m",
            "Valuation view positive-stock rule");
        AuditAssert.Contains(
            repository,
            "GetStockAlertsAsync",
            "Stock Alerts repository method");
        AuditAssert.Contains(
            repository,
            "b.CurrentStock > 0m",
            "Expiry Monitor positive-stock rule");
        AuditAssert.Contains(
            repository,
            "b.ExpiryDate.HasValue",
            "Expiry Monitor requires an expiry date");

        AuditAssert.Contains(
            dto,
            "public static class StockAlertFilters",
            "Stock Alert filter definitions");
        AuditAssert.Contains(
            dto,
            "ReorderLevel > 0m",
            "Low-stock configured reorder-level rule");
        AuditAssert.Contains(
            dto,
            "TotalQtyOnHand <= ReorderLevel",
            "Low-stock threshold rule");
        AuditAssert.Contains(
            dto,
            "Negative Stock — Data Issue",
            "Negative-stock data-issue label");
        AuditAssert.Contains(
            exportBuilder,
            "BuildStockAlertsCsv",
            "Stock Alerts CSV builder");
    }

    private static void VerifyPurchaseOrderEntryAndPdfWorkflow()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Purchasing",
            "PurchaseOrderView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PurchaseOrderViewModel.cs");
        string phaseViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PurchaseOrderViewModel.Phase7D3.cs");
        string dialogXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "PurchasingVariantEntryDialog.xaml");
        string dialogCodeBehind = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "PurchasingVariantEntryDialog.xaml.cs");
        string dialogViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PurchasingVariantEntryDialogViewModel.cs");

        AuditAssert.False(
            xaml.Contains("ActiveMatrixVariants", StringComparison.Ordinal),
            "The removed inline PO matrix is still bound on the page.");
        AuditAssert.False(
            xaml.Contains("ADD MATRIX ITEMS", StringComparison.OrdinalIgnoreCase),
            "The removed PO matrix-transfer action is still visible.");
        AuditAssert.False(
            xaml.Contains("Item Parent", StringComparison.Ordinal),
            "The removed inline PO item-parent selector is still visible.");
        AuditAssert.Equal(
            1,
            CountOccurrences(xaml, "Content=\"ADD STOCK ITEM VARIANTS...\""),
            "PO reusable variant-entry action count");
        AuditAssert.Contains(
            xaml,
            "Command=\"{Binding OpenPurchasingVariantEntryDialogCommand}\"",
            "PO reusable variant-entry command");
        AuditAssert.Contains(
            xaml,
            "Key=\"Enter\" Command=\"{Binding AddItemCommand}\"",
            "PO fast SKU Enter-key action");
        AuditAssert.Contains(
            xaml,
            "Content=\"SAVE &amp; APPROVE PO\"",
            "PO save-and-approve action");

        foreach (string removedToken in new[]
        {
            "PoMatrixEntryDto",
            "ActiveMatrixVariants",
            "MatrixFilterText",
            "AddMatrixCommand",
            "ApplyBulkMatrix"
        })
        {
            AuditAssert.False(
                viewModel.Contains(removedToken, StringComparison.Ordinal),
                $"Removed PO matrix token remains in the view model: {removedToken}");
        }

        AuditAssert.False(
            phaseViewModel.Contains("_allMatrixVariants", StringComparison.Ordinal),
            "PO tax refresh still depends on the removed inline matrix.");
        AuditAssert.Contains(
            viewModel,
            "using POS.Core.Services;",
            "PO LocalLogService namespace import");
        AuditAssert.Contains(
            viewModel,
            "private void OpenPurchasingVariantEntryDialog()",
            "PO shared purchasing variant-entry command implementation");
        AuditAssert.Contains(
            viewModel,
            "expiryEntryEnabled: false",
            "PO disables expiry entry in the shared dialog");
        AuditAssert.Contains(
            viewModel,
            "minimumQuantityEnabled: true",
            "PO enables MOQ validation in the shared dialog");
        AuditAssert.Contains(
            viewModel,
            "primaryActionText: \"ADD ENTERED ROWS TO PO\"",
            "PO-specific shared-dialog apply label");
        AuditAssert.Contains(
            dialogXaml,
            "x:Name=\"MinimumQuantityColumn\"",
            "shared dialog named MOQ column");
        AuditAssert.Contains(
            dialogXaml,
            "x:Name=\"QuantityColumn\"",
            "shared dialog named quantity column");
        AuditAssert.Contains(
            dialogXaml,
            "x:Name=\"UnitCostColumn\"",
            "shared dialog named unit-cost column");
        AuditAssert.Contains(
            dialogXaml,
            "x:Name=\"ExpiryColumn\"",
            "shared dialog named expiry column");
        AuditAssert.False(
            dialogXaml.Contains("x:Reference DialogWindow", StringComparison.Ordinal),
            "shared dialog still contains the cyclical root-window reference.");
        AuditAssert.Contains(
            dialogCodeBehind,
            "ConfigureDocumentColumns();",
            "shared dialog constructor column configuration");
        AuditAssert.Contains(
            dialogCodeBehind,
            "MinimumQuantityColumn.Visibility = _viewModel.MinimumQuantityEnabled",
            "shared dialog MOQ visibility configuration");
        AuditAssert.Contains(
            dialogCodeBehind,
            "QuantityColumn.Header = _viewModel.QuantityLabel;",
            "shared dialog quantity header configuration");
        AuditAssert.Contains(
            dialogCodeBehind,
            "UnitCostColumn.Header = _viewModel.UnitCostLabel;",
            "shared dialog cost header configuration");
        AuditAssert.Contains(
            dialogCodeBehind,
            "ExpiryColumn.Visibility = _viewModel.ExpiryEntryEnabled",
            "shared dialog expiry visibility configuration");
        AuditAssert.Contains(
            dialogViewModel,
            "MinimumQuantityEnabled && row.Quantity < row.MinimumQuantity",
            "shared dialog MOQ validation");

        int saveIndex = CompactSource(viewModel).IndexOf(
            "await_poRepository.SavePurchaseOrderAsync(header,linesToSave);",
            StringComparison.Ordinal);
        int promptIndex = CompactSource(viewModel).IndexOf(
            "ExportitasPDFnow?",
            StringComparison.Ordinal);
        int exportIndex = CompactSource(viewModel).IndexOf(
            "saveStatus=awaitExportSavedPurchaseOrderPdfAsync(header);",
            StringComparison.Ordinal);
        int clearIndex = CompactSource(viewModel).IndexOf(
            "Clear();StatusMessage=saveStatus;",
            StringComparison.Ordinal);

        AuditAssert.True(
            saveIndex >= 0 &&
            promptIndex > saveIndex &&
            exportIndex > promptIndex &&
            clearIndex > exportIndex,
            "PO must save first, then offer PDF export, then clear the completed form.");
        AuditAssert.Contains(
            viewModel,
            "_exportBuilder.BuildPurchaseOrder(",
            "saved PO PDF document builder");
        AuditAssert.Contains(
            viewModel,
            "_exportDialogService.SaveTablePdf(",
            "saved PO PDF save dialog");
        AuditAssert.Contains(
            viewModel,
            "was saved successfully,",
            "saved-PO export failure preserves successful PO-save status");
        AuditAssert.Contains(
            viewModel,
            "but the PDF could not be exported.",
            "saved-PO export failure reports only the PDF failure");
    }

    private static void VerifyPurchaseOrderAndGrnHistoryReadability()
    {
        string grnXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnDashboardView.xaml");
        string grnViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GrnDashboardViewModel.cs");
        string poXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Purchasing",
            "PurchaseOrderDashboardView.xaml");

        foreach (string removedGrnToken in new[]
        {
            "Show Cancelled GRNs",
            "FilterStatuses",
            "SelectedStatusFilter",
            "ShowCancelledGrns",
            "Header=\"Status\"",
            "ViewingGrnDetails.Status",
            "Header=\"Price Updated\"",
            "Text=\"Due: \""
        })
        {
            AuditAssert.False(
                grnXaml.Contains(removedGrnToken, StringComparison.Ordinal),
                $"Removed GRN history token remains visible: {removedGrnToken}");
        }

        foreach (string removedGrnViewModelToken in new[]
        {
            "_selectedStatusFilter",
            "_showCancelledGrns",
            "FilterStatuses",
            "RefreshStatusFilters",
            "OnSelectedStatusFilterChanged",
            "OnShowCancelledGrnsChanged"
        })
        {
            AuditAssert.False(
                grnViewModel.Contains(removedGrnViewModelToken, StringComparison.Ordinal),
                $"Removed GRN history filter state remains: {removedGrnViewModelToken}");
        }

        AuditAssert.Contains(
            grnViewModel,
            "statusFilter: \"Posted\"",
            "GRN history fixed posted-document query");
        AuditAssert.Contains(
            grnViewModel,
            "showCancelled: false",
            "GRN history excludes unsupported cancelled documents");
        AuditAssert.Contains(
            grnXaml,
            "Text=\"Payment Due: \"",
            "GRN payment-due label");
        AuditAssert.Contains(
            grnXaml,
            "Header=\"Disc Mode\"",
            "GRN discount mode history column");
        AuditAssert.Contains(
            grnXaml,
            "Header=\"Disc Value\"",
            "GRN discount value history column");
        AuditAssert.Contains(
            grnXaml,
            "Header=\"Disc Amt\"",
            "GRN calculated discount history column");
        AuditAssert.Contains(
            grnXaml,
            "Header=\"Retail Price\"",
            "GRN Retail Price history column");
        AuditAssert.Contains(
            grnXaml,
            "Header=\"Wholesale Price\"",
            "GRN Wholesale Price history column");
        AuditAssert.Contains(
            grnXaml,
            "This GRN is read-only",
            "GRN history read-only message");
        AuditAssert.Contains(
            grnXaml,
            "cannot be edited after posting.",
            "GRN posted-document edit restriction");
        AuditAssert.Equal(
            1,
            CountOccurrences(
                grnXaml,
                "ScrollViewer.HorizontalScrollBarVisibility=\"Auto\""),
            "GRN detail horizontal scrollbar count");

        AuditAssert.False(
            poXaml.Contains("Binding=\"{Binding TrackingSummary}\"", StringComparison.Ordinal),
            "PO dashboard tracking summary remains visible.");
        AuditAssert.False(
            poXaml.Contains("Binding=\"{Binding TrackingText}\"", StringComparison.Ordinal),
            "PO detail tracking column remains visible.");
        AuditAssert.False(
            poXaml.Contains("Header=\"Tracking\"", StringComparison.Ordinal),
            "PO history still exposes an unclear Tracking column.");
        AuditAssert.Contains(
            poXaml,
            "ItemsSource=\"{Binding FilterStatuses}\"",
            "PO status filter remains available");
        AuditAssert.Contains(
            poXaml,
            "Content=\"Show Cancelled POs\"",
            "PO cancelled-document filter remains available");
        AuditAssert.Contains(
            poXaml,
            "This Purchase Order is read-only",
            "PO history read-only message");
        AuditAssert.Contains(
            poXaml,
            "on this history page.",
            "PO history-page read-only scope");
        AuditAssert.Equal(
            1,
            CountOccurrences(
                poXaml,
                "ScrollViewer.HorizontalScrollBarVisibility=\"Auto\""),
            "PO detail horizontal scrollbar count");
    }

    private static void VerifyPoLinkedGrnExpiryEditor()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GrnViewModel.cs");
        string dto = Read(
            "POS.Core",
            "Models",
            "DTOs",
            "GrnDtos.cs");

        AuditAssert.Equal(
            1,
            CountOccurrences(
                xaml,
                "SelectedDate=\"{Binding ExpiryDate, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\""),
            "GRN final-line expiry editor binding count");
        AuditAssert.Equal(
            1,
            CountOccurrences(
                xaml,
                "IsEnabled=\"{Binding IsExpiryEnabled, Mode=OneWay}\""),
            "GRN expiry editor item-policy enablement count");
        AuditAssert.False(
            Regex.IsMatch(
                xaml,
                "<DataGridTextColumn\\s+Header=\"Expiry\"\\s+" +
                "Binding=\"\\{Binding ExpiryDisplayText\\}\"",
                RegexOptions.CultureInvariant),
            "GRN final lines still use a read-only expiry text column.");
        AuditAssert.Contains(
            viewModel,
            "RequiresExpiry = poLine.RequiresExpiry,",
            "PO-to-GRN expiry requirement mapping");
        AuditAssert.Contains(
            dto,
            "if (RequiresExpiry && !ExpiryDate.HasValue)",
            "GRN mandatory expiry validation");
    }

    public static Task GrnVariantEntryAndSellingPriceWorkflowsAreControlledAsync()
    {
        VerifyGrnVariantEntryDialog();
        VerifyGrnSellingPriceWorkflow();
        VerifyConditionalVatRetry();

        return Task.CompletedTask;
    }

    private static void VerifyGrnVariantEntryDialog()
    {
        string grnXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml");
        string grnCodeBehind = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml.cs");
        string dialogXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "PurchasingVariantEntryDialog.xaml");
        string dialogCodeBehind = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "PurchasingVariantEntryDialog.xaml.cs");
        string dialogViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PurchasingVariantEntryDialogViewModel.cs");
        string grnViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GrnViewModel.cs");

        AuditAssert.False(
            grnXaml.Contains("x:Name=\"MatrixVariantsGrid\"", StringComparison.Ordinal),
            "The removed inline GRN matrix is still present.");
        AuditAssert.False(
            grnXaml.Contains("ADD MATRIX ROWS", StringComparison.OrdinalIgnoreCase),
            "The removed inline matrix transfer action is still visible.");
        AuditAssert.False(
            grnCodeBehind.Contains("AddMatrixRows_Click", StringComparison.Ordinal),
            "The removed inline matrix click handler is still present.");
        AuditAssert.True(
            grnCodeBehind.Contains("using System.Windows.Controls;", StringComparison.Ordinal) ||
            grnCodeBehind.Contains("System.Windows.Controls.UserControl", StringComparison.Ordinal),
            "GRN view code-behind must resolve the WPF UserControl type explicitly.");
        AuditAssert.Contains(
            grnXaml,
            "Content=\"ADD STOCK ITEM VARIANTS...\"",
            "GRN reusable variant-entry action");
        AuditAssert.Contains(
            grnXaml,
            "Command=\"{Binding OpenPurchasingVariantEntryDialogCommand}\"",
            "GRN reusable variant-entry command");
        AuditAssert.Contains(
            dialogXaml,
            "x:Name=\"VariantGrid\"",
            "Purchasing variant-entry DataGrid");
        AuditAssert.False(
            dialogXaml.Contains("x:Reference DialogWindow", StringComparison.Ordinal),
            "Purchasing variant-entry dialog still contains a root-window x:Reference cycle.");
        AuditAssert.Equal(
            1,
            CountOccurrences(dialogXaml, "x:Name=\"MinimumQuantityColumn\""),
            "Purchasing variant-entry MOQ column name count");
        AuditAssert.Equal(
            1,
            CountOccurrences(dialogXaml, "x:Name=\"QuantityColumn\""),
            "Purchasing variant-entry quantity column name count");
        AuditAssert.Equal(
            1,
            CountOccurrences(dialogXaml, "x:Name=\"UnitCostColumn\""),
            "Purchasing variant-entry cost column name count");
        AuditAssert.Equal(
            1,
            CountOccurrences(dialogXaml, "x:Name=\"ExpiryColumn\""),
            "Purchasing variant-entry expiry column name count");
        AuditAssert.True(
            Regex.IsMatch(
                dialogXaml,
                "SelectedDate=\"\\{Binding\\s+ExpiryDate,\\s*Mode=TwoWay,\\s*" +
                "UpdateSourceTrigger=PropertyChanged(?:,\\s*ValidatesOnExceptions=True)?\\}\"",
                RegexOptions.CultureInvariant),
            "Variant-entry expiry binding does not update the source immediately.");

        string compact = Regex.Replace(
            dialogCodeBehind,
            "\\s+",
            string.Empty,
            RegexOptions.CultureInvariant);

        int clearFocusIndex = compact.IndexOf(
            "Keyboard.ClearFocus();",
            StringComparison.Ordinal);
        int cellCommitIndex = compact.IndexOf(
            "VariantGrid.CommitEdit(DataGridEditingUnit.Cell,true);",
            StringComparison.Ordinal);
        int rowCommitIndex = compact.IndexOf(
            "VariantGrid.CommitEdit(DataGridEditingUnit.Row,true);",
            StringComparison.Ordinal);
        int commitGuardIndex = compact.IndexOf(
            "if(!cellCommitted||!rowCommitted)",
            StringComparison.Ordinal);
        int collectIndex = compact.IndexOf(
            "_viewModel.TryCollectAcceptedRows(",
            StringComparison.Ordinal);
        int resultIndex = compact.IndexOf(
            "DialogResult=true;",
            StringComparison.Ordinal);

        AuditAssert.True(
            clearFocusIndex >= 0 &&
            cellCommitIndex > clearFocusIndex &&
            rowCommitIndex > cellCommitIndex &&
            commitGuardIndex > rowCommitIndex &&
            collectIndex > commitGuardIndex &&
            resultIndex > collectIndex,
            "Variant entry must commit the active cell and row, stop on failure, validate, and only then return accepted rows.");

        AuditAssert.False(
            dialogViewModel.Contains("GrnLineEntryDto", StringComparison.Ordinal),
            "Reusable purchasing variant-entry state depends on the GRN line DTO.");
        AuditAssert.False(
            dialogViewModel.Contains("PoMatrixEntryDto", StringComparison.Ordinal),
            "Reusable purchasing variant-entry state depends on the PO matrix DTO.");
        AuditAssert.Contains(
            dialogViewModel,
            "Func<int, Task<IReadOnlyList<PurchasingVariantSource>>>",
            "neutral asynchronous purchasing-variant loader");
        AuditAssert.False(
            Regex.IsMatch(
                grnViewModel,
                @"private\s+async\s+Task\s+OpenPurchasingVariantEntryDialogAsync\s*\(",
                RegexOptions.CultureInvariant),
            "The GRN variant-entry command must not be marked async when its outer body has no await.");
        AuditAssert.False(
            Regex.IsMatch(
                dialogViewModel,
                @"\b_selectedItem\s*=",
                RegexOptions.CultureInvariant),
            "The generated SelectedItem property backing field must not be assigned directly.");
        AuditAssert.Contains(
            dialogViewModel,
            "SelectedItem = AvailableItems[0];",
            "generated SelectedItem property initialization");
        AuditAssert.Contains(
            dialogViewModel,
            "ExpiryEntryEnabled && row.RequiresExpiry && !row.ExpiryDate.HasValue",
            "variant-entry expiry requirement validation");
        AuditAssert.Contains(
            dialogViewModel,
            "row.ExpiryDate.Value.Date < DocumentDate",
            "variant-entry expiry date boundary validation");
        AuditAssert.Contains(
            grnViewModel,
            "BuildLineFromPurchasingEntry",
            "GRN adapter from neutral purchasing variant rows");
    }

    private static void VerifyGrnSellingPriceWorkflow()
    {
        string grnXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml");
        string dialogXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "GrnBulkSellingPriceDialog.xaml");
        string dialogCodeBehind = Read(
            "POS.BackOffice.UI",
            "Views",
            "Dialogs",
            "GrnBulkSellingPriceDialog.xaml.cs");
        string dto = Read(
            "POS.Core",
            "Models",
            "DTOs",
            "GrnDtos.cs");

        AuditAssert.Equal(
            1,
            CountOccurrences(grnXaml, "Command=\"{Binding OpenBulkSellingPriceDialogCommand}\""),
            "GRN selling-price dialog entry-point count");
        AuditAssert.Contains(
            grnXaml,
            "Content=\"CHANGE SELLING PRICES...\"",
            "single GRN selling-price action label");
        AuditAssert.False(
            grnXaml.Contains("BULK SELLING PRICES", StringComparison.OrdinalIgnoreCase),
            "The duplicate matrix selling-price action remains visible.");
        AuditAssert.Contains(
            grnXaml,
            "Content=\"CLEAR PROPOSED PRICES\"",
            "clear proposed prices action");

        AuditAssert.False(
            dialogXaml.Contains("ComboBox", StringComparison.Ordinal),
            "The selling-price dialog still contains method or rounding dropdowns.");
        AuditAssert.Contains(dialogXaml, "x:Name=\"txtBulkRetail\"", "bulk Retail field");
        AuditAssert.Contains(dialogXaml, "x:Name=\"txtBulkWholesale\"", "bulk Wholesale field");
        AuditAssert.Contains(dialogXaml, "x:Name=\"txtBulkMinimum\"", "bulk Minimum field");
        AuditAssert.Contains(dialogXaml, "x:Name=\"txtBulkMaximum\"", "bulk Maximum field");
        AuditAssert.Contains(dialogXaml, "Header=\"Current Retail\"", "current Retail column");
        AuditAssert.Contains(dialogXaml, "Header=\"New Retail\"", "new Retail column");
        AuditAssert.Contains(dialogXaml, "Header=\"Current W/S\"", "current Wholesale column");
        AuditAssert.Contains(dialogXaml, "Header=\"New W/S\"", "new Wholesale column");
        AuditAssert.Contains(dialogXaml, "Header=\"Current Min\"", "current Minimum column");
        AuditAssert.Contains(dialogXaml, "Header=\"New Min\"", "new Minimum column");
        AuditAssert.Contains(dialogXaml, "Header=\"Current Max\"", "current Maximum column");
        AuditAssert.Contains(dialogXaml, "Header=\"New Max\"", "new Maximum column");
        AuditAssert.Contains(dialogXaml, "Content=\"SELECT ALL\"", "select-all action");
        AuditAssert.Contains(dialogXaml, "Content=\"CLEAR SELECTION\"", "clear-selection action");
        AuditAssert.Contains(dialogXaml, "Content=\"APPLY PRICES\"", "apply-prices action");
        AuditAssert.Contains(dialogXaml, "Content=\"CANCEL\"", "cancel action");

        AuditAssert.Contains(
            dialogCodeBehind,
            "line.NewMinimumPrice = RoundMoney(row.NewMinimumPrice);",
            "Minimum Price proposal assignment");
        AuditAssert.Contains(
            dialogCodeBehind,
            "line.NewMaximumPrice = RoundMoney(row.NewMaximumPrice);",
            "Maximum Price proposal assignment");
        AuditAssert.Contains(
            dialogCodeBehind,
            "line.UpdateSellingPrices = row.HasAnyChange;",
            "four-level selling-price update flag");
        AuditAssert.False(
            dialogCodeBehind.Contains("SourceLine.NewRetailPrice =", StringComparison.Ordinal),
            "Selling prices are mutated before the final Apply action.");
        AuditAssert.Contains(dto, "public bool HasMinimumPriceChange", "Minimum Price change detection");
        AuditAssert.Contains(dto, "public bool HasMaximumPriceChange", "Maximum Price change detection");
        AuditAssert.Contains(dto, "public bool HasAnySellingPriceChange", "four-level price-change detection");
        AuditAssert.Contains(dto, "parts.Add($\"Min ", "Minimum Price summary text");
        AuditAssert.Contains(dto, "parts.Add($\"Max ", "Maximum Price summary text");
    }

    private static void VerifyConditionalVatRetry()
    {
        string grnXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml");
        string phaseViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GrnViewModel.Phase7D2.cs");
        string grnViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GrnViewModel.cs");

        AuditAssert.False(
            grnXaml.Contains("REFRESH VAT PREVIEW", StringComparison.OrdinalIgnoreCase),
            "The normal GRN toolbar still exposes the manual VAT preview action.");
        AuditAssert.Equal(
            1,
            CountOccurrences(grnXaml, "Content=\"RETRY VAT\""),
            "conditional VAT retry action count");
        AuditAssert.Contains(
            grnXaml,
            "Visibility=\"{Binding IsTaxPreviewRetryVisible, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            "conditional VAT retry visibility binding");
        AuditAssert.Contains(
            phaseViewModel,
            "IsTaxPreviewRetryVisible = false;",
            "successful authoritative preview clears VAT retry state");
        AuditAssert.Contains(
            phaseViewModel,
            "IsTaxPreviewRetryVisible = true;",
            "failed authoritative preview exposes VAT retry state");
        AuditAssert.True(
            CountOccurrences(grnViewModel, "RecalculateTotalsAuthoritativelyAsync(showErrors: true)") >= 2,
            "GRN posting must retain authoritative tax preview checks before posting.");
    }

    private static void VerifyNonVatSupplierPurchasingPolicy()
    {
        string service = Read(
            "POS.Core",
            "Services",
            "Tax",
            "PurchasingTaxService.cs");
        string poRepository = Read(
            "POS.Core",
            "Repositories",
            "PoRepository.cs");
        string grnRepository = Read(
            "POS.Core",
            "Repositories",
            "GrnRepository.cs");
        string poViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PurchaseOrderViewModel.Phase7D3.cs");
        string grnViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "GrnViewModel.Phase7D2.cs");
        string poXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Purchasing",
            "PurchaseOrderView.xaml");
        string grnXaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml");

        int supplierMethodStart = service.IndexOf(
            "ResolveProfilesForSupplierAsync",
            StringComparison.Ordinal);
        int calculateMethodStart = service.IndexOf(
            "public PurchasingTaxDocumentResult CalculateDocument",
            StringComparison.Ordinal);

        AuditAssert.True(
            supplierMethodStart >= 0 && calculateMethodStart > supplierMethodStart,
            "Supplier-aware purchasing-tax resolver is missing.");

        string supplierMethod = service.Substring(
            supplierMethodStart,
            calculateMethodStart - supplierMethodStart);

        AuditAssert.Contains(
            supplierMethod,
            "TaxCategoryId = pair.Value.TaxCategoryId",
            "non-VAT purchasing preserves item Tax Category ID");
        AuditAssert.Contains(
            supplierMethod,
            "TaxCategoryCode = pair.Value.TaxCategoryCode",
            "non-VAT purchasing preserves item Tax Category code");
        AuditAssert.Contains(
            supplierMethod,
            "TaxRateId = null",
            "non-VAT purchasing removes the applied Tax Rate reference");
        AuditAssert.Contains(
            supplierMethod,
            "RatePercent = 0m",
            "non-VAT purchasing forces zero input VAT");
        AuditAssert.False(
            supplierMethod.Contains(
                "TaxCategoryCodes.OutOfScope",
                StringComparison.Ordinal),
            "non-VAT purchasing must not remap the item Tax Category to Out of Scope.");

        AuditAssert.Contains(
            poRepository,
            "ResolveProfilesForSupplierAsync",
            "Purchase Order authoritative supplier-aware tax path");
        AuditAssert.Contains(
            poRepository,
            "supplier.HasVat",
            "Purchase Order supplier VAT status query");
        AuditAssert.Contains(
            grnRepository,
            "ResolveProfilesForSupplierAsync",
            "GRN authoritative supplier-aware tax path");
        AuditAssert.Contains(
            grnRepository,
            "supplier.HasVat",
            "GRN supplier VAT status query");
        AuditAssert.Contains(
            poViewModel,
            "CanUseSupplierVatPriceMode",
            "Purchase Order non-VAT price-mode gate");
        AuditAssert.Contains(
            grnViewModel,
            "CanUseSupplierVatPriceMode",
            "GRN non-VAT price-mode gate");
        AuditAssert.Contains(
            poXaml,
            "IsEnabled=\"{Binding CanUseSupplierVatPriceMode}\"",
            "Purchase Order VAT-inclusive checkbox gate");
        AuditAssert.Contains(
            grnXaml,
            "IsEnabled=\"{Binding CanUseSupplierVatPriceMode}\"",
            "GRN VAT-inclusive checkbox gate");
    }

    private static string CompactSource(string source)
    {
        return Regex.Replace(source, @"\s+", string.Empty);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
