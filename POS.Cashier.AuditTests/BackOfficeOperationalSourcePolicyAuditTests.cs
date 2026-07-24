using System.Text.RegularExpressions;

namespace POS.Cashier.AuditTests;

internal static class BackOfficeOperationalSourcePolicyAuditTests
{
    public static Task PurchasingAndStockControlsAreWiredAsync()
    {
        VerifyStockAdjustmentReadOnlyBindings();
        VerifyStockAdjustmentRecalculationHook();
        VerifyPurchaseOrderBulkCostAction();
        VerifyPoLinkedGrnExpiryEditor();
        VerifyGrnMatrixEditCommit();
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

    private static void VerifyPurchaseOrderBulkCostAction()
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

        const string applyCostButtonPattern =
            "<Button\\s+Grid.Column=\"5\"\\s+Content=\"APPLY COST\"\\s+" +
            "Command=\"\\{Binding ApplyBulkMatrixExpectedCostCommand\\}\"";

        AuditAssert.True(
            Regex.IsMatch(xaml, applyCostButtonPattern, RegexOptions.CultureInvariant),
            "Purchase Order Bulk Cost input is not connected to APPLY COST.");
        AuditAssert.Contains(
            viewModel,
            "private void ApplyBulkMatrixExpectedCost()",
            "Purchase Order bulk expected-cost implementation");
        AuditAssert.Contains(
            viewModel,
            "item.ExpectedCost = BulkMatrixExpectedCost;",
            "Purchase Order bulk expected-cost row assignment");
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

    private static void VerifyGrnMatrixEditCommit()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml");
        string codeBehind = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml.cs");

        AuditAssert.Contains(
            xaml,
            "x:Name=\"MatrixVariantsGrid\"",
            "GRN matrix DataGrid name");
        AuditAssert.Contains(
            xaml,
            "Click=\"AddMatrixRows_Click\"",
            "GRN matrix add-row click handler");
        AuditAssert.True(
            Regex.IsMatch(
                xaml,
                "SelectedDate=\\\"\\{Binding\\s+ExpiryDate,\\s*Mode=TwoWay,\\s*" +
                "UpdateSourceTrigger=PropertyChanged(?:,\\s*ValidatesOnExceptions=True)?\\}\\\"",
                RegexOptions.CultureInvariant),
            "GRN matrix expiry binding does not update the source immediately.");

        string compact = Regex.Replace(
            codeBehind,
            "\\s+",
            string.Empty,
            RegexOptions.CultureInvariant);

        int clearFocusIndex = compact.IndexOf(
            "Keyboard.ClearFocus();",
            StringComparison.Ordinal);
        int cellCommitIndex = compact.IndexOf(
            "MatrixVariantsGrid.CommitEdit(DataGridEditingUnit.Cell,true);",
            StringComparison.Ordinal);
        int rowCommitIndex = compact.IndexOf(
            "MatrixVariantsGrid.CommitEdit(DataGridEditingUnit.Row,true);",
            StringComparison.Ordinal);
        int canExecuteIndex = compact.IndexOf(
            "viewModel.AddMatrixCommand.CanExecute(null)",
            StringComparison.Ordinal);
        int executeIndex = compact.IndexOf(
            "viewModel.AddMatrixCommand.Execute(null);",
            StringComparison.Ordinal);

        AuditAssert.True(
            clearFocusIndex >= 0 &&
            cellCommitIndex > clearFocusIndex &&
            rowCommitIndex > cellCommitIndex &&
            canExecuteIndex > rowCommitIndex &&
            executeIndex > canExecuteIndex,
            "GRN matrix expiry editor must clear focus and commit cell and row edits before AddMatrixCommand executes.");
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
