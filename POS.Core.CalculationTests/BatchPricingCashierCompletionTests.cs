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
        private static void CashierSellableBatchesUseFefoAndEffectivePrices()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                ItemBatch first = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
                first.ExpiryDate = DateTime.Today.AddDays(20);
                first.ReceivedDate = DateTime.Today.AddDays(-10);
                first.HasSellingPriceOverride = false;

                context.ItemBatches.Add(new ItemBatch
                {
                    ItemVariantId = scenario.StockVariantId,
                    BatchNo = "FEFO-FIRST",
                    InternalBatchBarcode = "FEFO-FIRST-BC",
                    ExpiryDate = DateTime.Today.AddDays(5),
                    ReceivedDate = DateTime.Today.AddDays(-5),
                    CostPrice = 620m,
                    RetailPrice = 1300m,
                    WholesalePrice = 1120m,
                    HasSellingPriceOverride = true,
                    CurrentStock = 3m
                });

                context.SaveChanges();
            }

            var repository = new ItemMasterRepository(factory);
            List<CashierBatchDto> rows = repository
                .GetSellableBatchesByVariantIdAsync(scenario.StockVariantId)
                .GetAwaiter().GetResult();

            AssertEqual(2, rows.Count, "sellable physical batch count");
            AssertEqual("FEFO-FIRST", rows[0].BatchNo, "FEFO first batch");
            AssertEqual("FEFO-FIRST-BC", rows[0].InternalBatchBarcode, "exact internal barcode");
            AssertMoney(1300m, rows[0].RetailPrice, "override Retail");
            AssertMoney(1120m, rows[0].WholesalePrice, "override Wholesale");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, rows[0].PriceSource, "override source");
            AssertEqual("Batch Override", rows[0].PriceSourceText, "override source text");
            AssertEqual(SellingPriceSourceCodes.Master, rows[1].PriceSource, "master source");
        }

        private static void CashierSellableBatchesExcludeInvalidRows()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                context.ItemBatches.AddRange(
                    new ItemBatch
                    {
                        ItemVariantId = scenario.StockVariantId,
                        BatchNo = "EXPIRED",
                        InternalBatchBarcode = "EXPIRED-BC",
                        ExpiryDate = DateTime.Today.AddDays(-1),
                        ReceivedDate = DateTime.Today.AddDays(-20),
                        CostPrice = 600m,
                        RetailPrice = 1180m,
                        WholesalePrice = 1062m,
                        CurrentStock = 4m
                    },
                    new ItemBatch
                    {
                        ItemVariantId = scenario.StockVariantId,
                        BatchNo = "ZERO",
                        InternalBatchBarcode = "ZERO-BC",
                        ExpiryDate = DateTime.Today.AddDays(10),
                        ReceivedDate = DateTime.Today,
                        CostPrice = 600m,
                        RetailPrice = 1180m,
                        WholesalePrice = 1062m,
                        CurrentStock = 0m
                    },
                    new ItemBatch
                    {
                        ItemVariantId = scenario.StockVariantId,
                        BatchNo = "GENERAL",
                        InternalBatchBarcode = "GENERAL-BC",
                        ExpiryDate = null,
                        ReceivedDate = DateTime.Today,
                        CostPrice = 600m,
                        RetailPrice = 1180m,
                        WholesalePrice = 1062m,
                        CurrentStock = 5m
                    },
                    new ItemBatch
                    {
                        ItemVariantId = scenario.StockVariantId,
                        BatchNo = "DEACTIVATED",
                        InternalBatchBarcode = "DEACT-BC",
                        ExpiryDate = DateTime.Today.AddDays(10),
                        ReceivedDate = DateTime.Today,
                        CostPrice = 600m,
                        RetailPrice = 1180m,
                        WholesalePrice = 1062m,
                        CurrentStock = 5m,
                        IsDeactivated = true
                    });
                context.SaveChanges();
            }

            var repository = new ItemMasterRepository(factory);
            List<CashierBatchDto> cashierRows = repository
                .GetSellableBatchesByVariantIdAsync(scenario.StockVariantId)
                .GetAwaiter().GetResult();
            List<BatchSeekDto> seekRows = repository
                .GetSeekBatchesByVariantIdAsync(scenario.StockVariantId)
                .GetAwaiter().GetResult();

            AssertEqual(1, cashierRows.Count, "invalid Cashier rows excluded");
            AssertEqual("TEST-BATCH", cashierRows.Single().BatchNo, "remaining physical batch");
            AssertEqual(1, seekRows.Count, "invalid Product Seek rows excluded");
            AssertEqual(cashierRows.Single().ItemBatchId, seekRows.Single().ItemBatchId, "shared exact-batch authority");
        }

        private static void ProductSeekRowsExposeBothPricesAndSource()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                ItemBatch batch = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
                batch.HasSellingPriceOverride = true;
                batch.RetailPrice = 1275m;
                batch.WholesalePrice = 1095m;
                batch.ExpiryDate = DateTime.Today.AddDays(15);
                context.SaveChanges();
            }

            var repository = new ItemMasterRepository(factory);
            BatchSeekDto row = repository
                .GetSeekBatchesByVariantIdAsync(scenario.StockVariantId)
                .GetAwaiter().GetResult()
                .Single();

            AssertMoney(1275m, row.RetailPrice, "Product Seek effective Retail");
            AssertMoney(1095m, row.WholesalePrice, "Product Seek effective Wholesale");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, row.PriceSource, "Product Seek source");
            AssertEqual("Batch Override", row.PriceSourceText, "Product Seek source text");
        }

        private static void SalesExplorerUsesSavedBatchAndPriceSourceSnapshots()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);

            int saleId;
            using (AppDbContext context = factory.CreateDbContext())
            {
                var header = new SalesHeader
                {
                    ShiftSessionId = scenario.ShiftSessionId,
                    InvoiceNo = "P3-HISTORY-001",
                    TerminalNo = "T01",
                    CashierName = "History Cashier",
                    CustomerName = "Walk-In",
                    CustomerType = "Walk-In",
                    TransactionDate = DateTime.Today,
                    GrossTotal = 1275m,
                    NetTotal = 1275m,
                    PaymentMethod = "Cash",
                    Status = "Completed",
                    TaxSnapshotStatus = "Complete"
                };

                header.SalesLines.Add(new SalesLine
                {
                    ItemVariantId = scenario.StockVariantId,
                    ItemBatchId = scenario.StockBatchId,
                    SkuCode = scenario.StockSku,
                    Barcode = "TEST-STOCK-BARCODE",
                    ItemDescription = "Test Stock Item / Standard",
                    ItemTypeSnapshot = ItemTypeCodes.StockItem,
                    BatchNo = "HISTORICAL-BATCH",
                    CataloguePriceSourceSnapshot = SellingPriceSourceCodes.BatchOverride,
                    Uom = "PCS",
                    Quantity = 1m,
                    UnitPrice = 1275m,
                    OriginalUnitPrice = 1275m,
                    CostPrice = 600m,
                    GrossAmount = 1275m,
                    LineTotal = 1275m,
                    TaxSnapshotStatus = "Complete"
                });

                context.SalesHeaders.Add(header);
                context.SaveChanges();
                saleId = header.Id;

                ItemBatch current = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
                current.BatchNo = "CURRENT-BATCH-NAME";
                current.HasSellingPriceOverride = false;
                current.RetailPrice = 1180m;
                context.SaveChanges();
            }

            var repository = new MasterSalesAnalyticsRepository(factory);
            SaleReceiptDetailsDto details = repository
                .GetSaleReceiptDetailsAsync(saleId)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Historical sale details were not loaded.");
            SaleReceiptLineDto line = details.Lines.Single();

            AssertEqual("HISTORICAL-BATCH", line.BatchNo, "saved historical batch");
            AssertEqual(SellingPriceSourceCodes.BatchOverride, line.CataloguePriceSource, "saved historical source");
            AssertEqual("Batch Override", line.CataloguePriceSourceText, "historical source display");
            AssertMoney(1275m, line.UnitPrice, "saved historical unit price");
        }
    }
}
