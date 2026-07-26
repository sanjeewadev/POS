using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Services.Pricing;

namespace POS.Cashier.AuditTests;

internal static class MigrationAuditTests
{
    private const string LatestMigration = "20260726090000_AddBatchSellingPriceOverrideFoundation";

    public static Task MigrationFromEmptyCreatesCurrentSchemaAsync()
    {
        using var factory = new AuditDbContextFactory(migrate: true);
        using AppDbContext context = factory.CreateDbContext();

        string[] applied = context.Database.GetAppliedMigrations().ToArray();
        AuditAssert.True(applied.Contains(LatestMigration), $"Latest migration '{LatestMigration}' was not applied.");

        string[] requiredTables =
        {
            "SalesHeaders",
            "SalesLines",
            "SalesPayments",
            "CashierCartSessions",
            "ShiftSessions",
            "ShiftCloseSnapshots",
            "CustomerReturnHeaders",
            "GiftVouchers",
            "GiftVoucherTransactions",
            "FreeIssueRules",
            "FreeItemClaimLogs"
        };

        foreach (string table in requiredTables)
            AuditAssert.True(SqliteObjectExists(context, "table", table), $"Required table '{table}' is missing.");

        AuditAssert.True(SqliteObjectExists(context, "index", "IX_SalesHeaders_CheckoutToken"),
            "Checkout-token index is missing.");
        AuditAssert.True(SqliteColumnExists(context, "ItemBatches", "HasSellingPriceOverride"),
            "Batch selling-price override state is missing.");
        AuditAssert.True(SqliteColumnExists(context, "SalesLines", "CataloguePriceSourceSnapshot"),
            "Sales catalogue-price source snapshot is missing.");
        AuditAssert.True(SqliteObjectExists(context, "index", "IX_SalesLines_CataloguePriceSourceSnapshot"),
            "Catalogue-price source index is missing.");
        return Task.CompletedTask;
    }

    public static Task ApprovedBaselineUpgradesSafelyAsync()
    {
        string? baseline = Environment.GetEnvironmentVariable("POS_AUDIT_UPGRADE_BASELINE");
        if (string.IsNullOrWhiteSpace(baseline) || !File.Exists(baseline))
            throw new AuditSkippedException("No approved upgrade baseline database was supplied.");

        string target = Path.Combine(AuditPaths.GetAuditTempRoot(), $"upgrade-{Guid.NewGuid():N}.db");
        AuditPaths.AssertSafeDatabasePath(target);
        File.Copy(baseline, target, overwrite: false);

        try
        {
            using var factory = new AuditDbContextFactory(migrate: true, existingDatabasePath: target);
            using AppDbContext context = factory.CreateDbContext();
            string[] applied = context.Database.GetAppliedMigrations().ToArray();
            AuditAssert.True(applied.Contains(LatestMigration),
                $"Approved baseline did not upgrade through '{LatestMigration}'.");
            AuditAssert.True(SqliteObjectExists(context, "table", "SalesHeaders"),
                "SalesHeaders table is missing after upgrade.");
            AuditAssert.True(SqliteObjectExists(context, "table", "ShiftCloseSnapshots"),
                "ShiftCloseSnapshots table is missing after upgrade.");
            AuditAssert.True(SqliteColumnExists(context, "ItemBatches", "HasSellingPriceOverride"),
                "Batch override state is missing after approved-baseline upgrade.");
            AuditAssert.True(SqliteColumnExists(context, "SalesLines", "CataloguePriceSourceSnapshot"),
                "Catalogue-price source snapshot is missing after approved-baseline upgrade.");

            var markerVariant = context.ItemVariants
                .AsNoTracking()
                .SingleOrDefault(row => row.SkuCode == "BP-LEGACY-SKU");
            bool fixtureRequired = string.Equals(
                Environment.GetEnvironmentVariable("POS_AUDIT_REQUIRE_BATCH_PRICING_FIXTURE"),
                "1",
                StringComparison.Ordinal);

            if (fixtureRequired && markerVariant is null)
            {
                throw new InvalidOperationException(
                    "The generated 1.0.7 batch-pricing fixture rows are missing.");
            }

            if (markerVariant is not null)
            {
                ItemBatch markerBatch = context.ItemBatches
                    .AsNoTracking()
                    .Single(row =>
                        row.ItemVariantId == markerVariant.Id &&
                        row.BatchNo == "BP-LEGACY-BATCH");
                SalesLine markerLine = context.SalesLines
                    .AsNoTracking()
                    .Single(row =>
                        row.SkuCode == "BP-LEGACY-SKU" &&
                        row.BatchNo == "BP-LEGACY-BATCH");

                AuditAssert.False(
                    markerBatch.HasSellingPriceOverride,
                    "Generated 1.0.7 batch was not migrated as master-priced.");
                AuditAssert.Money(
                    markerVariant.RetailPrice,
                    markerBatch.RetailPrice,
                    "Generated 1.0.7 batch Retail mirror");
                AuditAssert.Money(
                    markerVariant.WholesalePrice,
                    markerBatch.WholesalePrice,
                    "Generated 1.0.7 batch Wholesale mirror");
                AuditAssert.Money(3m, markerBatch.CurrentStock, "Generated 1.0.7 batch stock");
                AuditAssert.Money(500m, markerBatch.CostPrice, "Generated 1.0.7 batch cost");
                AuditAssert.Equal(
                    "BP107BATCH0001",
                    markerBatch.InternalBatchBarcode,
                    "Generated 1.0.7 batch barcode");
                AuditAssert.Equal(
                    SellingPriceSourceCodes.LegacyUnknown,
                    markerLine.CataloguePriceSourceSnapshot,
                    "Generated 1.0.7 sale catalogue source");
                AuditAssert.Money(1200m, markerLine.UnitPrice, "Generated 1.0.7 sale price");
                AuditAssert.Money(500m, markerLine.CostPrice, "Generated 1.0.7 sale cost");
            }
        }
        finally
        {
            DeleteIfPresent(target);
            DeleteIfPresent(target + "-shm");
            DeleteIfPresent(target + "-wal");
        }

        return Task.CompletedTask;
    }

    private static bool SqliteColumnExists(AppDbContext context, string tableName, string columnName)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        if (command.Connection?.State != System.Data.ConnectionState.Open)
            command.Connection?.Open();
        command.CommandText = $"PRAGMA table_info([{tableName.Replace("]", "]]", StringComparison.Ordinal)}]);";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool SqliteObjectExists(AppDbContext context, string objectType, string objectName)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        if (command.Connection?.State != System.Data.ConnectionState.Open)
            command.Connection?.Open();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = $type AND name = $name;";
        var typeParameter = command.CreateParameter();
        typeParameter.ParameterName = "$type";
        typeParameter.Value = objectType;
        command.Parameters.Add(typeParameter);
        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "$name";
        nameParameter.Value = objectName;
        command.Parameters.Add(nameParameter);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
