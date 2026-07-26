using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Services.Pricing;

namespace POS.Cashier.AuditTests;

internal static class MigrationAuditTests
{
    private const string LatestMigration = "20260726120000_CompleteBatchPricingBackOfficeWorkflow";

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
        AuditAssert.True(SqliteColumnExists(context, "PriceChangeHistories", "ChangeAction"),
            "Price Change action is missing.");
        AuditAssert.True(SqliteColumnExists(context, "PriceChangeHistories", "OldPriceSource"),
            "Price Change old source is missing.");
        AuditAssert.True(SqliteColumnExists(context, "PriceChangeHistories", "NewPriceSource"),
            "Price Change new source is missing.");
        AuditAssert.True(SqliteColumnExists(context, "GrnLines", "SellingPriceAction"),
            "GRN selling-price action is missing.");
        AuditAssert.True(SqliteObjectExists(context, "index", "IX_PriceChangeHistories_ChangeAction"),
            "Price Change action index is missing.");
        AuditAssert.True(SqliteObjectExists(context, "index", "IX_GrnLines_SellingPriceAction"),
            "GRN selling-price action index is missing.");
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
            AuditAssert.True(SqliteColumnExists(context, "PriceChangeHistories", "ChangeAction"),
                "Price Change action is missing after approved-baseline upgrade.");
            AuditAssert.True(SqliteColumnExists(context, "PriceChangeHistories", "OldPriceSource"),
                "Price Change old source is missing after approved-baseline upgrade.");
            AuditAssert.True(SqliteColumnExists(context, "PriceChangeHistories", "NewPriceSource"),
                "Price Change new source is missing after approved-baseline upgrade.");
            AuditAssert.True(SqliteColumnExists(context, "GrnLines", "SellingPriceAction"),
                "GRN selling-price action is missing after approved-baseline upgrade.");

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

            bool backOfficeFixtureRequired = string.Equals(
                Environment.GetEnvironmentVariable("POS_AUDIT_REQUIRE_BATCH_PRICING_BACKOFFICE_FIXTURE"),
                "1",
                StringComparison.Ordinal);

            PriceChangeHistory? legacyMasterHistory = context.PriceChangeHistories
                .AsNoTracking()
                .SingleOrDefault(row => row.PriceChangeNo == "PCH-P1-MASTER");
            PriceChangeHistory? legacyBatchHistory = context.PriceChangeHistories
                .AsNoTracking()
                .SingleOrDefault(row => row.PriceChangeNo == "PCH-P1-BATCH");
            GrnLine? legacyMasterGrnLine = context.GrnLines
                .AsNoTracking()
                .SingleOrDefault(row => row.BatchNo == "BP-P1-MASTER");
            GrnLine? legacyCurrentGrnLine = context.GrnLines
                .AsNoTracking()
                .SingleOrDefault(row => row.BatchNo == "BP-P1-CURRENT");

            if (backOfficeFixtureRequired &&
                (legacyMasterHistory is null || legacyBatchHistory is null ||
                 legacyMasterGrnLine is null || legacyCurrentGrnLine is null))
            {
                throw new InvalidOperationException(
                    "The generated Patch 1 BackOffice pricing fixture rows are missing.");
            }

            if (legacyMasterHistory is not null)
            {
                AuditAssert.Equal(PriceChangeActionCodes.LegacyMasterChange, legacyMasterHistory.ChangeAction, "legacy Master history action");
                AuditAssert.Equal(SellingPriceSourceCodes.Master, legacyMasterHistory.OldPriceSource, "legacy Master old source");
                AuditAssert.Equal(SellingPriceSourceCodes.Master, legacyMasterHistory.NewPriceSource, "legacy Master new source");
            }

            if (legacyBatchHistory is not null)
            {
                AuditAssert.Equal(PriceChangeActionCodes.LegacyBatchChange, legacyBatchHistory.ChangeAction, "legacy Batch history action");
                AuditAssert.Equal(SellingPriceSourceCodes.LegacyUnknown, legacyBatchHistory.OldPriceSource, "legacy Batch old source");
                AuditAssert.Equal(SellingPriceSourceCodes.LegacyUnknown, legacyBatchHistory.NewPriceSource, "legacy Batch new source");
            }

            if (legacyMasterGrnLine is not null)
            {
                AuditAssert.Equal(GrnSellingPriceActionCodes.UpdateMasterPrice, legacyMasterGrnLine.SellingPriceAction, "legacy GRN master action backfill");
            }

            if (legacyCurrentGrnLine is not null)
            {
                AuditAssert.Equal(GrnSellingPriceActionCodes.UseCurrentMasterPrice, legacyCurrentGrnLine.SellingPriceAction, "legacy GRN current action backfill");
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
