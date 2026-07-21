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
