using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        private static void BatchPricingResolverUsesMasterAndWholesaleFallback()
        {
            (ItemVariant variant, ItemBatch batch) = CreateBatchPricingEntities(
                hasBatchTracking: true,
                itemType: ItemTypeCodes.StockItem,
                batchNo: "BATCH-001");

            variant.RetailPrice = 1200m;
            variant.WholesalePrice = 0m;
            batch.HasSellingPriceOverride = false;
            batch.RetailPrice = 999m;
            batch.WholesalePrice = 888m;

            EffectiveSellingPrice resolved =
                EffectiveSellingPriceResolver.Resolve(variant, batch);

            AssertMoney(1200m, resolved.RetailPrice, "master Retail resolution");
            AssertMoney(1200m, resolved.WholesalePrice, "master Wholesale fallback");
            AssertEqual(SellingPriceSourceCodes.Master, resolved.PriceSource, "master price source");
        }

        private static void BatchPricingResolverUsesEligibleOverride()
        {
            (ItemVariant variant, ItemBatch batch) = CreateBatchPricingEntities(
                hasBatchTracking: true,
                itemType: ItemTypeCodes.StockItem,
                batchNo: "BATCH-001");

            variant.RetailPrice = 1200m;
            variant.WholesalePrice = 1100m;
            batch.HasSellingPriceOverride = true;
            batch.RetailPrice = 1350m;
            batch.WholesalePrice = 1250m;

            EffectiveSellingPrice resolved =
                EffectiveSellingPriceResolver.Resolve(variant, batch);

            AssertMoney(1350m, resolved.RetailPrice, "override Retail resolution");
            AssertMoney(1250m, resolved.WholesalePrice, "override Wholesale resolution");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, resolved.PriceSource, "override price source");
        }

        private static void BatchPricingResolverIgnoresIneligibleOverrides()
        {
            (ItemVariant serviceVariant, ItemBatch serviceBatch) = CreateBatchPricingEntities(
                hasBatchTracking: false,
                itemType: ItemTypeCodes.Service,
                batchNo: "SERVICE");
            serviceVariant.RetailPrice = 800m;
            serviceVariant.WholesalePrice = 700m;
            serviceBatch.HasSellingPriceOverride = true;
            serviceBatch.RetailPrice = 999m;
            serviceBatch.WholesalePrice = 899m;

            EffectiveSellingPrice serviceResolved =
                EffectiveSellingPriceResolver.Resolve(serviceVariant, serviceBatch);

            AssertMoney(800m, serviceResolved.RetailPrice, "Service master Retail");
            AssertMoney(700m, serviceResolved.WholesalePrice, "Service master Wholesale");
            AssertEqual(SellingPriceSourceCodes.Master, serviceResolved.PriceSource, "Service price source");

            (ItemVariant stockVariant, ItemBatch generalBatch) = CreateBatchPricingEntities(
                hasBatchTracking: false,
                itemType: ItemTypeCodes.StockItem,
                batchNo: "GENERAL");
            stockVariant.RetailPrice = 900m;
            stockVariant.WholesalePrice = 850m;
            generalBatch.HasSellingPriceOverride = true;
            generalBatch.RetailPrice = 1000m;
            generalBatch.WholesalePrice = 950m;

            EffectiveSellingPrice generalResolved =
                EffectiveSellingPriceResolver.Resolve(stockVariant, generalBatch);

            AssertMoney(900m, generalResolved.RetailPrice, "GENERAL master Retail");
            AssertMoney(850m, generalResolved.WholesalePrice, "GENERAL master Wholesale");
            AssertEqual(SellingPriceSourceCodes.Master, generalResolved.PriceSource, "GENERAL price source");
        }

        private static void BatchPricingOverrideValidatesMasterBoundaries()
        {
            (ItemVariant variant, ItemBatch batch) = CreateBatchPricingEntities(
                hasBatchTracking: true,
                itemType: ItemTypeCodes.StockItem,
                batchNo: "BATCH-001");
            variant.MinimumPrice = 600m;
            variant.MaximumPrice = 1400m;

            EffectiveSellingPriceResolver.ValidateOverride(
                variant,
                batch,
                retailPrice: 1300m,
                wholesalePrice: 1100m);

            AssertThrows(
                () => EffectiveSellingPriceResolver.ValidateOverride(
                    variant,
                    batch,
                    retailPrice: 500m,
                    wholesalePrice: 1100m),
                "below the variant Minimum");

            AssertThrows(
                () => EffectiveSellingPriceResolver.ValidateOverride(
                    variant,
                    batch,
                    retailPrice: 1300m,
                    wholesalePrice: 1500m),
                "above the variant Maximum");
        }

        private static void BatchPricingMigrationAppliesFromEmptyDatabase()
        {
            using var factory = new MigrationTestDbContextFactory();
            using AppDbContext context = factory.CreateDbContext();

            context.Database.Migrate();

            string[] applied = context.Database.GetAppliedMigrations().ToArray();
            AssertTrue(
                applied.Contains("20260726090000_AddBatchSellingPriceOverrideFoundation"),
                "batch pricing migration applied from empty database");
            AssertTrue(
                SqliteColumnExists(context, "ItemBatches", "HasSellingPriceOverride"),
                "batch override column exists");
            AssertTrue(
                SqliteColumnExists(context, "SalesLines", "CataloguePriceSourceSnapshot"),
                "catalogue source column exists");
            AssertTrue(
                SqliteObjectExists(context, "index", "IX_SalesLines_CataloguePriceSourceSnapshot"),
                "catalogue source index exists");
        }


        private static void LegacyCartJsonDefaultsCataloguePriceSource()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();

            repository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario)))
                .GetAwaiter()
                .GetResult();

            using (AppDbContext context = factory.CreateDbContext())
            {
                CashierCartLine stored = context.CashierCartLines.Single();
                Dictionary<string, JsonElement>? payload =
                    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stored.SnapshotJson);

                if (payload == null ||
                    !payload.Remove(nameof(CashierCartLineSnapshotDto.CataloguePriceSource)))
                {
                    throw new InvalidOperationException(
                        "The saved cart JSON did not contain the catalogue price source field.");
                }

                stored.SnapshotJson = JsonSerializer.Serialize(payload);
                context.SaveChanges();
            }

            CashierCartLineSnapshotDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter()
                .GetResult()!
                .Lines
                .Single();

            AssertEqual(
                SellingPriceSourceCodes.LegacyUnknown,
                restored.CataloguePriceSource,
                "legacy cart catalogue price source");
        }

        private static void ExactBatchLookupReturnsEffectiveOverride()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            var repository = new ItemMasterRepository(factory);
            CashierBatchDto? batch = repository
                .GetSellableBatchByInternalBarcodeAsync("TEST-BATCH-BARCODE")
                .GetAwaiter()
                .GetResult();

            if (batch == null)
                throw new InvalidOperationException("Exact batch lookup did not return the test batch.");

            AssertEqual(scenario.StockBatchId, batch.ItemBatchId, "exact batch ID");
            AssertMoney(1300m, batch.RetailPrice, "exact batch effective Retail");
            AssertMoney(1100m, batch.WholesalePrice, "exact batch effective Wholesale");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, batch.PriceSource, "exact batch price source");
        }

        private static void BatchOverrideCheckoutPersistsSourceAndDeductsExactBatch()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            SalesLine line = CreateRepositoryTestLine(
                scenario.StockVariantId,
                scenario.StockBatchId,
                scenario.StockSku,
                "Test Stock Item",
                quantity: 1m,
                unitPrice: 1300m);
            line.CataloguePriceSourceSnapshot = SellingPriceSourceCodes.BatchOverride;

            var repository = new SalesRepository(factory);
            SalesHeader sale = repository.ProcessCheckoutAsync(
                    CreateRepositoryTestHeader(scenario.ShiftSessionId, 1300m),
                    new List<SalesLine> { line },
                    new List<SalesPayment> { CreateCashPayment(1300m) })
                .GetAwaiter()
                .GetResult();

            using AppDbContext context = factory.CreateDbContext();
            SalesLine saved = context.SalesLines.AsNoTracking().Single(row => row.SalesHeaderId == sale.Id);
            ItemBatch batch = context.ItemBatches.AsNoTracking().Single(row => row.Id == scenario.StockBatchId);

            AssertEqual(scenario.StockBatchId, saved.ItemBatchId.GetValueOrDefault(), "saved exact batch ID");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, saved.CataloguePriceSourceSnapshot, "saved price source");
            AssertMoney(1300m, saved.UnitPrice, "saved effective Retail price");
            AssertMoney(4m, batch.CurrentStock, "exact batch stock deduction");
        }

        private static void StaleBatchOverrideCheckoutRollsBack()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            SalesLine line = CreateRepositoryTestLine(
                scenario.StockVariantId,
                scenario.StockBatchId,
                scenario.StockSku,
                "Test Stock Item",
                quantity: 1m,
                unitPrice: 1300m);
            line.CataloguePriceSourceSnapshot = SellingPriceSourceCodes.BatchOverride;

            SetBatchOverride(factory, scenario.StockBatchId, 1350m, 1125m);

            var repository = new SalesRepository(factory);
            AssertThrows(
                () => repository.ProcessCheckoutAsync(
                        CreateRepositoryTestHeader(scenario.ShiftSessionId, 1300m),
                        new List<SalesLine> { line },
                        new List<SalesPayment> { CreateCashPayment(1300m) })
                    .GetAwaiter()
                    .GetResult(),
                "Catalogue price changed");

            using AppDbContext context = factory.CreateDbContext();
            AssertMoney(5m, context.ItemBatches.Single(row => row.Id == scenario.StockBatchId).CurrentStock, "stale checkout stock rollback");
            AssertEqual(0, context.SalesHeaders.Count(), "stale checkout sale rollback");
            AssertEqual(0, context.InventoryTransactions.Count(), "stale checkout inventory rollback");
        }

        private static void MasterPriceUpdatePreservesOverrideAndSyncsMirrors()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            int mirrorBatchId;
            using (AppDbContext context = factory.CreateDbContext())
            {
                var newMirrorBatch = new ItemBatch
                {
                    ItemVariantId = scenario.StockVariantId,
                    BatchNo = "TEST-BATCH-2",
                    InternalBatchBarcode = "TEST-BATCH-BARCODE-2",
                    ReceivedDate = new DateTime(2026, 7, 2),
                    CostPrice = 610m,
                    RetailPrice = 1180m,
                    WholesalePrice = 1062m,
                    HasSellingPriceOverride = false,
                    CurrentStock = 2m
                };
                context.ItemBatches.Add(newMirrorBatch);
                context.SaveChanges();
                mirrorBatchId = newMirrorBatch.Id;
            }

            var pricing = new PriceManagementSummaryDto
            {
                ItemVariantId = scenario.StockVariantId,
                ItemCode = "TEST-STOCK",
                MinimumPrice = 600m,
                RetailPrice = 1250m,
                WholesalePrice = 1150m,
                MaximumPrice = 1500m
            };

            var repository = new PriceManagementRepository(factory);
            repository.UpdatePricingAsync(
                    pricing,
                    updatedBy: "pricing.test",
                    changeReason: "Batch pricing foundation test",
                    applySellingPriceToCurrentStock: false)
                .GetAwaiter()
                .GetResult();

            using AppDbContext verify = factory.CreateDbContext();
            ItemVariant variant = verify.ItemVariants.Single(row => row.Id == scenario.StockVariantId);
            ItemBatch overrideBatch = verify.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
            ItemBatch mirrorBatch = verify.ItemBatches.Single(row => row.Id == mirrorBatchId);

            AssertMoney(1250m, variant.RetailPrice, "updated master Retail");
            AssertMoney(1150m, variant.WholesalePrice, "updated master Wholesale");
            AssertTrue(overrideBatch.HasSellingPriceOverride, "override state preserved");
            AssertMoney(1300m, overrideBatch.RetailPrice, "override Retail preserved");
            AssertMoney(1100m, overrideBatch.WholesalePrice, "override Wholesale preserved");
            AssertFalse(mirrorBatch.HasSellingPriceOverride, "mirror remains master-priced");
            AssertMoney(1250m, mirrorBatch.RetailPrice, "mirror Retail synchronized");
            AssertMoney(1150m, mirrorBatch.WholesalePrice, "mirror Wholesale synchronized");
            AssertEqual(1, verify.PriceChangeHistories.Count(), "master-only price history count");
        }

        private static void MasterBoundaryConflictRollsBackPricing()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            var pricing = new PriceManagementSummaryDto
            {
                ItemVariantId = scenario.StockVariantId,
                ItemCode = "TEST-STOCK",
                MinimumPrice = 1200m,
                RetailPrice = 1250m,
                WholesalePrice = 1200m,
                MaximumPrice = 1500m
            };

            AssertThrows(
                () => new PriceManagementRepository(factory)
                    .UpdatePricingAsync(
                        pricing,
                        updatedBy: "pricing.test",
                        changeReason: "Invalid boundary test")
                    .GetAwaiter()
                    .GetResult(),
                "conflicts with active batch overrides");

            using AppDbContext verify = factory.CreateDbContext();
            ItemVariant variant = verify.ItemVariants.Single(
                row => row.Id == scenario.StockVariantId);
            ItemBatch batch = verify.ItemBatches.Single(
                row => row.Id == scenario.StockBatchId);

            AssertMoney(600m, variant.MinimumPrice, "master boundary rollback");
            AssertMoney(1180m, variant.RetailPrice, "master Retail rollback");
            AssertMoney(1062m, variant.WholesalePrice, "master Wholesale rollback");
            AssertMoney(1300m, batch.RetailPrice, "override Retail rollback");
            AssertEqual(0, verify.PriceChangeHistories.Count(), "boundary conflict history rollback");
        }

        private static void GrnReceiptPreservesExistingBatchOverride()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            var header = new GrnHeader
            {
                SupplierId = scenario.SupplierId,
                SupplierInvoiceNo = $"OVR-{Guid.NewGuid():N}"[..20],
                InvoiceDate = DateTime.Today,
                ReceivedDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                CreditDays = 30,
                Remarks = "Existing override preservation test",
                IsTaxInclusive = false,
                CreatedBy = "grn.test",
                PostedBy = "grn.test"
            };
            var line = new GrnLine
            {
                ItemVariantId = scenario.StockVariantId,
                BatchNo = "TEST-BATCH",
                Uom = "PCS",
                ReceivedQty = 1m,
                UnitCost = 600m,
                LineDiscountMode = "Amount",
                LineDiscountValue = 0m,
                IsVatIncluded = false,
                UpdateSellingPrices = false
            };

            new GrnRepository(factory)
                .PostGrnAsync(header, new List<GrnLine> { line })
                .GetAwaiter()
                .GetResult();

            using AppDbContext context = factory.CreateDbContext();
            ItemBatch batch = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
            AssertMoney(6m, batch.CurrentStock, "existing batch received stock");
            AssertTrue(batch.HasSellingPriceOverride, "GRN preserved override state");
            AssertMoney(1300m, batch.RetailPrice, "GRN preserved override Retail");
            AssertMoney(1100m, batch.WholesalePrice, "GRN preserved override Wholesale");
        }

        private static void LabelsAndStockValuationUseEffectiveBatchPrice()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            BarcodePrintQueueItemDto? label = new BarcodePrinterRepository(factory)
                .FindItemForPrintingAsync("TEST-BATCH-BARCODE")
                .GetAwaiter()
                .GetResult();

            if (label == null)
                throw new InvalidOperationException("Batch label lookup did not return the test batch.");

            AssertMoney(1300m, label.Price, "batch label effective Retail");

            StockBalanceDto stock = new StockBalanceRepository(factory)
                .GetStockBalancesAsync(positiveStockOnly: true)
                .GetAwaiter()
                .GetResult()
                .Single(row => row.VariantId == scenario.StockVariantId);

            AssertMoney(6500m, stock.TotalRetailValue, "stock effective Retail valuation");
            AssertMoney(5500m, stock.TotalWholesaleValue, "stock effective Wholesale valuation");
            AssertMoney(1300m, stock.UnitRetail, "stock effective unit Retail");
            AssertMoney(1100m, stock.UnitWholesale, "stock effective unit Wholesale");
        }

        private static void CustomerReturnPreservesCurrentBatchOverride()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SetBatchOverride(factory, scenario.StockBatchId, 1300m, 1100m);

            SalesLine input = CreateRepositoryTestLine(
                scenario.StockVariantId,
                scenario.StockBatchId,
                scenario.StockSku,
                "Test Stock Item",
                quantity: 1m,
                unitPrice: 1300m);
            input.CataloguePriceSourceSnapshot = SellingPriceSourceCodes.BatchOverride;

            SalesHeader sale = new SalesRepository(factory)
                .ProcessCheckoutAsync(
                    CreateRepositoryTestHeader(scenario.ShiftSessionId, 1300m),
                    new List<SalesLine> { input },
                    new List<SalesPayment> { CreateCashPayment(1300m) })
                .GetAwaiter()
                .GetResult();

            SalesLine sourceLine = sale.SalesLines.Single();
            CreateCustomerReturnRepository(factory)
                .ProcessReturnAsync(
                    CreateReturnRequest(
                        sale,
                        scenario,
                        (sourceLine.Id, 1m)))
                .GetAwaiter()
                .GetResult();

            using AppDbContext context = factory.CreateDbContext();
            ItemBatch batch = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
            CustomerReturnLine returned = context.CustomerReturnLines.Single();

            AssertMoney(5m, batch.CurrentStock, "return restored exact batch stock");
            AssertTrue(batch.HasSellingPriceOverride, "return preserved override state");
            AssertMoney(1300m, batch.RetailPrice, "return preserved current override Retail");
            AssertMoney(1100m, batch.WholesalePrice, "return preserved current override Wholesale");
            AssertMoney(1300m, returned.LineTotalRefund, "return used original sale value");
        }

        private static void SupplierReturnPreservesCurrentBatchOverride()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);
            SetBatchOverride(factory, scenario.StandardBatchId, 1400m, 1250m);

            PostSupplierReturn(
                factory,
                scenario,
                scenario.MainGrnId,
                "supplier.return.test",
                (scenario.StandardLineId, 1m));

            using AppDbContext context = factory.CreateDbContext();
            ItemBatch batch = context.ItemBatches.Single(
                row => row.Id == scenario.StandardBatchId);

            AssertTrue(batch.HasSellingPriceOverride, "supplier return override state");
            AssertMoney(1400m, batch.RetailPrice, "supplier return Retail price");
            AssertMoney(1250m, batch.WholesalePrice, "supplier return Wholesale price");
        }

        private static (ItemVariant Variant, ItemBatch Batch) CreateBatchPricingEntities(
            bool hasBatchTracking,
            string itemType,
            string batchNo)
        {
            var parent = new ItemParent
            {
                Id = 1,
                ItemCode = "PRICE-TEST",
                ItemName = "Price Test",
                PrintName = "Price Test",
                ItemType = itemType,
                HasBatchTracking = hasBatchTracking
            };
            var variant = new ItemVariant
            {
                Id = 10,
                ItemParentId = parent.Id,
                ItemParent = parent,
                SkuCode = "PRICE-TEST-SKU",
                RetailPrice = 1000m,
                WholesalePrice = 900m
            };
            var batch = new ItemBatch
            {
                Id = 20,
                ItemVariantId = variant.Id,
                ItemVariant = variant,
                BatchNo = batchNo,
                RetailPrice = 1000m,
                WholesalePrice = 900m,
                CurrentStock = 1m
            };

            return (variant, batch);
        }

        private static void SetBatchOverride(
            RepositoryTestDbContextFactory factory,
            int batchId,
            decimal retailPrice,
            decimal wholesalePrice)
        {
            using AppDbContext context = factory.CreateDbContext();
            ItemBatch batch = context.ItemBatches.Single(row => row.Id == batchId);
            batch.HasSellingPriceOverride = true;
            batch.RetailPrice = retailPrice;
            batch.WholesalePrice = wholesalePrice;
            batch.UpdatedAt = DateTime.Now;
            context.SaveChanges();
        }
    }
}
