using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Pricing;

namespace POS.Core.CalculationTests
{
    internal static partial class Program
    {
        private static void BatchPricingBackOfficeOverrideLifecycleIsAudited()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new PriceManagementRepository(factory);

            repository.SetBatchPriceOverrideAsync(
                    scenario.StockVariantId,
                    scenario.StockBatchId,
                    retailPrice: 1300m,
                    wholesalePrice: 1100m,
                    changedBy: "pricing.manager")
                .GetAwaiter()
                .GetResult();

            repository.SetBatchPriceOverrideAsync(
                    scenario.StockVariantId,
                    scenario.StockBatchId,
                    retailPrice: 1325m,
                    wholesalePrice: 1125m,
                    changedBy: "pricing.manager")
                .GetAwaiter()
                .GetResult();

            repository.RemoveBatchPriceOverrideAsync(
                    scenario.StockVariantId,
                    scenario.StockBatchId,
                    changedBy: "pricing.manager")
                .GetAwaiter()
                .GetResult();

            using AppDbContext verify = factory.CreateDbContext();
            ItemVariant variant = verify.ItemVariants.Single(row => row.Id == scenario.StockVariantId);
            ItemBatch batch = verify.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
            List<PriceChangeHistory> history = verify.PriceChangeHistories
                .OrderBy(row => row.Id)
                .ToList();

            AssertFalse(batch.HasSellingPriceOverride, "removed override state");
            AssertMoney(variant.RetailPrice, batch.RetailPrice, "removed override Retail mirror");
            AssertMoney(variant.WholesalePrice, batch.WholesalePrice, "removed override Wholesale mirror");
            AssertEqual(3, history.Count, "override lifecycle history count");
            AssertEqual(PriceChangeActionCodes.BatchOverrideCreated, history[0].ChangeAction, "override create action");
            AssertEqual(SellingPriceSourceCodes.Master, history[0].OldPriceSource, "override create old source");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, history[0].NewPriceSource, "override create new source");
            AssertEqual(PriceChangeActionCodes.BatchOverrideUpdated, history[1].ChangeAction, "override update action");
            AssertEqual(PriceChangeActionCodes.BatchOverrideRemoved, history[2].ChangeAction, "override remove action");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, history[2].OldPriceSource, "override remove old source");
            AssertEqual(SellingPriceSourceCodes.Master, history[2].NewPriceSource, "override remove new source");
            AssertTrue(history.All(row => row.ChangedBy == "pricing.manager"), "authenticated pricing username");
            AssertEqual(3, history.Select(row => row.PriceChangeNo).Distinct().Count(), "one operation number per override action");
        }

        private static void GrnMasterAndBatchActionsShareGroupedHistory()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);

            var header = new GrnHeader
            {
                SupplierId = scenario.SupplierId,
                SupplierInvoiceNo = $"P2-{Guid.NewGuid():N}"[..20],
                InvoiceDate = DateTime.Today,
                ReceivedDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                CreditDays = 30,
                Remarks = "Patch 2 grouped GRN price action test",
                IsTaxInclusive = false,
                CreatedBy = "grn.manager",
                PostedBy = "grn.manager"
            };

            var masterLine = new GrnLine
            {
                ItemVariantId = scenario.StockVariantId,
                BatchNo = "TEST-BATCH",
                Uom = "PCS",
                ReceivedQty = 1m,
                UnitCost = 610m,
                LineDiscountMode = "Amount",
                SellingPriceAction = GrnSellingPriceActionCodes.UpdateMasterPrice,
                UpdateSellingPrices = true,
                CurrentMinimumPrice = 600m,
                CurrentRetailPrice = 1180m,
                CurrentWholesalePrice = 1062m,
                CurrentMaximumPrice = 0m,
                NewMinimumPrice = 600m,
                NewRetailPrice = 1200m,
                NewWholesalePrice = 1080m,
                NewMaximumPrice = 1500m
            };

            var batchLine = new GrnLine
            {
                ItemVariantId = scenario.StockVariantId,
                BatchNo = "TEST-BATCH-2",
                Uom = "PCS",
                ReceivedQty = 2m,
                UnitCost = 620m,
                LineDiscountMode = "Amount",
                SellingPriceAction = GrnSellingPriceActionCodes.SetBatchPriceOverride,
                UpdateSellingPrices = false,
                CurrentMinimumPrice = 600m,
                CurrentRetailPrice = 1180m,
                CurrentWholesalePrice = 1062m,
                CurrentMaximumPrice = 0m,
                NewMinimumPrice = 600m,
                NewRetailPrice = 1350m,
                NewWholesalePrice = 1150m,
                NewMaximumPrice = 1500m
            };

            new GrnRepository(factory)
                .PostGrnAsync(header, new List<GrnLine> { masterLine, batchLine })
                .GetAwaiter()
                .GetResult();

            using AppDbContext verify = factory.CreateDbContext();
            ItemVariant variant = verify.ItemVariants.Single(row => row.Id == scenario.StockVariantId);
            ItemBatch exactBatch = verify.ItemBatches.Single(row => row.ItemVariantId == scenario.StockVariantId && row.BatchNo == "TEST-BATCH-2");
            GrnHeader savedHeader = verify.GrnHeaders
                .Include(row => row.GrnLines)
                .Single(row => row.Id == header.Id);
            List<PriceChangeHistory> history = verify.PriceChangeHistories
                .Where(row => row.SourceDocumentId == header.Id)
                .OrderBy(row => row.Id)
                .ToList();

            AssertMoney(1200m, variant.RetailPrice, "GRN updated master Retail");
            AssertMoney(1080m, variant.WholesalePrice, "GRN updated master Wholesale");
            AssertTrue(exactBatch.HasSellingPriceOverride, "GRN created exact-batch override");
            AssertMoney(1350m, exactBatch.RetailPrice, "GRN batch override Retail");
            AssertMoney(1150m, exactBatch.WholesalePrice, "GRN batch override Wholesale");
            AssertTrue(savedHeader.GrnLines.Any(row => row.SellingPriceAction == GrnSellingPriceActionCodes.UpdateMasterPrice), "persisted Master action");
            AssertTrue(savedHeader.GrnLines.Any(row => row.SellingPriceAction == GrnSellingPriceActionCodes.SetBatchPriceOverride), "persisted Batch action");
            AssertEqual(2, history.Count, "GRN grouped history detail count");
            AssertEqual(1, history.Select(row => row.PriceChangeNo).Distinct().Count(), "GRN one grouped PriceChangeNo");
            AssertTrue(history.Any(row => row.ChangeAction == PriceChangeActionCodes.MasterPriceUpdated), "GRN master history action");
            PriceChangeHistory batchHistory = history.Single(row => row.PriceLevel == "Batch");
            AssertEqual(exactBatch.Id, batchHistory.ItemBatchId ?? 0, "GRN history actual batch ID");
            AssertEqual(SellingPriceSourceCodes.Master, batchHistory.OldPriceSource, "GRN batch old source");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, batchHistory.NewPriceSource, "GRN batch new source");
            AssertTrue(history.All(row => row.ChangedBy == "grn.manager"), "GRN authenticated poster");
        }

        private static void GroupedPriceHistoryReturnsWholeOperation()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            GrnMasterAndBatchActionsShareGroupedHistoryFixture(factory, scenario);

            var repository = new PriceChangeHistoryRepository(factory);
            List<PriceChangeOperationSummaryDto> operations = repository
                .GetOperationsAsync(maxOperations: 1)
                .GetAwaiter()
                .GetResult();

            AssertEqual(1, operations.Count, "grouped operation limit");
            AssertEqual(2, operations[0].DetailRowCount, "grouped operation includes all details");
            AssertEqual(1, operations[0].BatchOverrideCount, "grouped batch override count");
            AssertTrue(operations[0].MasterChangeSummary.Contains("Retail", StringComparison.Ordinal), "grouped master summary");

            List<PriceChangeDetailDto> details = repository
                .GetOperationDetailsAsync(operations[0].OperationKey)
                .GetAwaiter()
                .GetResult();

            AssertEqual(2, details.Count, "grouped detail retrieval");
            AssertTrue(details.Any(row => row.PriceLevel == "Master"), "grouped master detail");
            AssertTrue(details.Any(row => row.PriceLevel == "Batch"), "grouped batch detail");
        }

        private static void StockAndExpiryViewsExposeEffectiveBatchSource()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                ItemParent parent = context.ItemParents.Single(row => row.Id == scenario.StockParentId);
                ItemBatch batch = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
                parent.HasExpiryTracking = true;
                batch.ExpiryDate = DateTime.Today.AddDays(30);
                batch.HasSellingPriceOverride = true;
                batch.RetailPrice = 1400m;
                batch.WholesalePrice = 1200m;
                context.SaveChanges();
            }

            var repository = new StockBalanceRepository(factory);
            StockBalanceDto stock = repository
                .GetStockBalancesAsync(positiveStockOnly: true)
                .GetAwaiter()
                .GetResult()
                .Single(row => row.VariantId == scenario.StockVariantId);
            ItemBatchDto batchRow = stock.Batches.Single(row => row.BatchId == scenario.StockBatchId);
            ExpiryMonitorRowDto expiry = repository
                .GetExpiryMonitorAsync(expiryFilter: ExpiryMonitorFilters.All)
                .GetAwaiter()
                .GetResult()
                .Single(row => row.BatchId == scenario.StockBatchId);

            AssertMoney(1400m, batchRow.EffectiveRetailPrice, "Stock Balance effective Retail");
            AssertMoney(1200m, batchRow.EffectiveWholesalePrice, "Stock Balance effective Wholesale");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, batchRow.PriceSource, "Stock Balance source");
            AssertMoney(1400m, expiry.EffectiveRetailPrice, "Expiry effective Retail");
            AssertMoney(1200m, expiry.EffectiveWholesalePrice, "Expiry effective Wholesale");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, expiry.PriceSource, "Expiry source");
        }

        private static void BatchPricingBackOfficeMigrationAppliesFromEmptyDatabase()
        {
            using var factory = new MigrationTestDbContextFactory();
            using AppDbContext context = factory.CreateDbContext();
            context.Database.Migrate();

            string[] applied = context.Database.GetAppliedMigrations().ToArray();
            AssertTrue(
                applied.Contains("20260726120000_CompleteBatchPricingBackOfficeWorkflow"),
                "Patch 2 SQLite migration applied from empty database");
            AssertTrue(SqliteColumnExists(context, "PriceChangeHistories", "ChangeAction"), "Price history action column exists");
            AssertTrue(SqliteColumnExists(context, "PriceChangeHistories", "OldPriceSource"), "Price history old source column exists");
            AssertTrue(SqliteColumnExists(context, "PriceChangeHistories", "NewPriceSource"), "Price history new source column exists");
            AssertTrue(SqliteColumnExists(context, "GrnLines", "SellingPriceAction"), "GRN selling-price action column exists");
        }

        private static void GrnMasterAndBatchActionsShareGroupedHistoryFixture(
            RepositoryTestDbContextFactory factory,
            RepositoryTestScenario scenario)
        {
            var header = new GrnHeader
            {
                SupplierId = scenario.SupplierId,
                SupplierInvoiceNo = $"P2G-{Guid.NewGuid():N}"[..20],
                InvoiceDate = DateTime.Today,
                ReceivedDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                CreditDays = 30,
                Remarks = "Grouped history fixture",
                IsTaxInclusive = false,
                CreatedBy = "history.manager",
                PostedBy = "history.manager"
            };

            var masterLine = new GrnLine
            {
                ItemVariantId = scenario.StockVariantId,
                BatchNo = "TEST-BATCH",
                Uom = "PCS",
                ReceivedQty = 1m,
                UnitCost = 610m,
                LineDiscountMode = "Amount",
                SellingPriceAction = GrnSellingPriceActionCodes.UpdateMasterPrice,
                UpdateSellingPrices = true,
                CurrentMinimumPrice = 600m,
                CurrentRetailPrice = 1180m,
                CurrentWholesalePrice = 1062m,
                NewMinimumPrice = 600m,
                NewRetailPrice = 1210m,
                NewWholesalePrice = 1090m,
                NewMaximumPrice = 1500m
            };

            var batchLine = new GrnLine
            {
                ItemVariantId = scenario.StockVariantId,
                BatchNo = "TEST-BATCH-GROUP",
                Uom = "PCS",
                ReceivedQty = 1m,
                UnitCost = 620m,
                LineDiscountMode = "Amount",
                SellingPriceAction = GrnSellingPriceActionCodes.SetBatchPriceOverride,
                CurrentMinimumPrice = 600m,
                CurrentRetailPrice = 1180m,
                CurrentWholesalePrice = 1062m,
                NewMinimumPrice = 600m,
                NewRetailPrice = 1360m,
                NewWholesalePrice = 1160m,
                NewMaximumPrice = 1500m
            };

            new GrnRepository(factory)
                .PostGrnAsync(header, new List<GrnLine> { masterLine, batchLine })
                .GetAwaiter()
                .GetResult();
        }
    }
}
