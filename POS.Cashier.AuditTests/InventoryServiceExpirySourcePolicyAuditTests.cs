using POS.Core.Configuration;
using POS.Core.Models.DTOs;

namespace POS.Cashier.AuditTests;

internal static class InventoryServiceExpirySourcePolicyAuditTests
{
    public static Task InventoryServicePricingAndExpiryWorkflowsAreExplicitAsync()
    {
        VerifyServicePricingPresentation();
        VerifyExpiryStatusRules();
        VerifyItemMasterFilteringAndServiceSafety();
        VerifyPricingFiltersAndStockSafety();
        VerifyExpiryMonitorAndExport();

        return Task.CompletedTask;
    }

    private static void VerifyServicePricingPresentation()
    {
        var service = new PriceManagementSummaryDto
        {
            ItemType = ItemTypeCodes.Service,
            TotalSoh = 12.5m,
            CurrentStockValue = 2500m,
            RetailPrice = 400m,
            WholesalePrice = 350m
        };

        AuditAssert.Equal(
            "Service",
            service.ItemTypeText,
            "Service pricing item type");
        AuditAssert.Equal(
            "Service / No Stock",
            service.TrackingText,
            "Service tracking presentation");
        AuditAssert.Equal(
            "Standard Cost",
            service.CostMethodText,
            "Service cost method presentation");
        AuditAssert.False(
            service.InventoryStockOnHand.HasValue,
            "Service pricing must not expose stock on hand.");
        AuditAssert.False(
            service.InventoryStockValue.HasValue,
            "Service pricing must not expose stock value.");
        AuditAssert.False(
            service.InventoryRetailValue.HasValue,
            "Service pricing must not expose inventory retail value.");
    }

    private static void VerifyExpiryStatusRules()
    {
        var expired = new ExpiryMonitorRowDto
        {
            ExpiryDate = DateTime.Today.AddDays(-1)
        };

        var withinSevenDays = new ExpiryMonitorRowDto
        {
            ExpiryDate = DateTime.Today.AddDays(7)
        };

        var missing = new ExpiryMonitorRowDto();

        AuditAssert.True(expired.IsExpired, "Expired batch detection");
        AuditAssert.Equal("Expired", expired.ExpiryStatus, "Expired batch status");
        AuditAssert.True(
            withinSevenDays.IsExpiringWithin(7),
            "Seven-day expiry detection");
        AuditAssert.Equal(
            "Within 7 Days",
            withinSevenDays.ExpiryStatus,
            "Seven-day expiry status");
        AuditAssert.True(missing.IsMissingExpiry, "Missing expiry detection");
        AuditAssert.Equal(
            "No Expiry Date",
            missing.ExpiryStatus,
            "Missing expiry status");
    }

    private static void VerifyItemMasterFilteringAndServiceSafety()
    {
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "ItemMasterViewModel.cs");
        string view = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryPages",
            "ItemMasterView.xaml");
        string repository = Read(
            "POS.Core",
            "Repositories",
            "ItemMasterRepository.cs");

        AuditAssert.Contains(
            view,
            "ItemsSource=\"{Binding ItemTypeFilters}\"",
            "Item Master type filter source");
        AuditAssert.Contains(
            view,
            "SelectedItem=\"{Binding SelectedItemTypeFilter, Mode=TwoWay}\"",
            "Item Master selected type filter");
        AuditAssert.Contains(
            viewModel,
            "ApplyServiceSafetyDefaults();",
            "Item Master service safety application");
        AuditAssert.Contains(
            viewModel,
            "CurrentItem.HasBatchTracking = false;",
            "Service batch tracking disabled");
        AuditAssert.Contains(
            viewModel,
            "CurrentItem.HasExpiryTracking = false;",
            "Service expiry tracking disabled");
        AuditAssert.Contains(
            viewModel,
            "CurrentItem.IsPurchaseLocked = true;",
            "Service purchasing locked");
        AuditAssert.Contains(
            repository,
            "string? itemType = null",
            "Item Master repository type filter parameter");
        AuditAssert.Contains(
            repository,
            "query = query.Where(p => p.ItemType == normalizedItemType);",
            "Item Master repository type filter query");
    }

    private static void VerifyPricingFiltersAndStockSafety()
    {
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PriceManagementViewModel.cs");
        string view = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "PriceManagementView.xaml");
        string repository = Read(
            "POS.Core",
            "Repositories",
            "PriceManagementRepository.cs");

        AuditAssert.Contains(
            viewModel,
            "ItemTypeFilters",
            "Pricing item-type filter collection");
        AuditAssert.Contains(
            view,
            "Header=\"Type\"",
            "Pricing item-type column");
        AuditAssert.Contains(
            view,
            "Stock Items only",
            "Pricing stock synchronization guidance");
        AuditAssert.Contains(
            repository,
            "string itemTypeFilter = \"All\"",
            "Pricing repository item-type filter parameter");
        AuditAssert.Contains(
            repository,
            "bool canSyncCurrentStock =",
            "Pricing stock synchronization guard");
        AuditAssert.Contains(
            repository,
            "variant.ItemParent.ItemType",
            "Pricing item type used for synchronization guard");
        AuditAssert.Contains(
            repository,
            "ItemTypeCodes.StockItem",
            "Pricing synchronization limited to stock items");
    }

    private static void VerifyExpiryMonitorAndExport()
    {
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "StockBalanceViewModel.cs");
        string view = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "StockBalanceView.xaml");
        string repository = Read(
            "POS.Core",
            "Repositories",
            "StockBalanceRepository.cs");
        string exportBuilder = Read(
            "POS.Core",
            "Services",
            "Exports",
            "OperationalExportBuilder.cs");

        AuditAssert.Contains(
            viewModel,
            "\"Expiry Monitor\"",
            "Expiry Monitor view option");
        AuditAssert.Contains(
            viewModel,
            "GetExpiryMonitorAsync",
            "Expiry Monitor repository call");
        AuditAssert.Contains(
            view,
            "ItemsSource=\"{Binding ExpiryRows}\"",
            "Expiry Monitor grid rows");
        AuditAssert.Contains(
            view,
            "Command=\"{Binding ExportExpiryCsvCommand}\"",
            "Expiry Monitor CSV action");
        AuditAssert.Contains(
            repository,
            "b.ItemVariant.ItemParent.ItemType == ItemTypeCodes.StockItem",
            "Expiry Monitor excludes services");
        AuditAssert.Contains(
            repository,
            "b.ItemVariant.ItemParent.HasBatchTracking",
            "Expiry Monitor requires batch tracking");
        AuditAssert.Contains(
            repository,
            "ExpiryMonitorFilters.Within90Days",
            "Expiry Monitor ninety-day filter");
        AuditAssert.Contains(
            exportBuilder,
            "BuildExpiryMonitorCsv",
            "Expiry Monitor CSV builder");
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
