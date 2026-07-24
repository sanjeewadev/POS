using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;
using POS.Core.Services.Exports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace POS.Core.CalculationTests;

internal static class Phase9BExportDashboardTests
{
    public static void CsvEscapesUnicodeQuotesCommasAndLineBreaks()
    {
        var service = new CsvExportService();
        string csv = service.Format(
            new[] { "Name", "Remarks" },
            new[] { (IReadOnlyList<string?>)new string?[] { "කොළඹ, Store", "He said \"yes\"\nNext line" } });

        AssertContains(csv, "\"කොළඹ, Store\"");
        AssertContains(csv, "\"He said \"\"yes\"\"\nNext line\"");
    }

    public static void CsvRejectsMismatchedRows()
    {
        var service = new CsvExportService();
        AssertThrows<InvalidOperationException>(() => service.Format(
            new[] { "A", "B" },
            new[] { (IReadOnlyList<string?>)new string?[] { "one" } }));
    }

    public static void ExportFileNamesAreSafe()
    {
        string name = ExportFileNameHelper.Create("Purchase Order", "PO/25:001", "pdf");
        AssertFalse(name.Contains('/'), "slash removed");
        AssertFalse(name.Contains(':'), "colon removed");
        AssertTrue(name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase), "PDF extension");
    }

    public static void PurchaseOrderPdfUsesSavedDocumentValues()
    {
        var po = new PoHeader
        {
            PoNumber = "PO-0001",
            Status = "Approved",
            Supplier = new Supplier { SupplierCode = "SUP-1", SupplierName = "Supplier One" },
            OrderDate = new DateTime(2026, 7, 1),
            ExpectedDate = new DateTime(2026, 7, 8),
            Subtotal = 1000m,
            TotalDiscountAmount = 50m,
            TotalTaxAmount = 171m,
            NetPayable = 1121m,
            PoLines = new List<PoLine>
            {
                new()
                {
                    Id = 1,
                    ItemCode = "IT-1",
                    Description = "Saved Item",
                    SkuCode = "SKU-1",
                    Uom = "Each",
                    OrderQty = 2m,
                    ExpectedCost = 500m,
                    LineDiscount = 50m,
                    TaxAmount = 171m,
                    LineTotal = 1121m
                }
            }
        };
        var settings = StoreSettingsRepository.CreateDefaultSettings();
        var builder = new OperationalExportBuilder(new CsvExportService());
        PdfTableDocumentDto document = builder.BuildPurchaseOrder(po, settings, "Manager");

        AssertEqual("PURCHASE ORDER", document.Title, "PO title");
        AssertEqual(1, document.Rows.Count, "PO line count");
        AssertEqual("1121.00", document.Rows[0][7], "saved PO total");
        AssertFalse(document.Title.Contains("GRN", StringComparison.OrdinalIgnoreCase), "GRN PDF excluded");
    }

    public static void ItemSalesExportsServicesWithoutStock()
    {
        var builder = new OperationalExportBuilder(new CsvExportService());
        var rows = new List<ItemPerformanceDto>
        {
            new()
            {
                Rank = 1,
                ItemCode = "SV-1",
                SkuCode = "SERVICE-1",
                ItemName = "Repair Service",
                ItemType = ItemTypeCodes.Service,
                CategoryName = "Services",
                SoldQuantity = 2m,
                GrossSales = 4000m,
                CurrentStock = null
            }
        };

        PdfTableDocumentDto pdf = builder.BuildItemSales(rows, DateTime.Today, DateTime.Today, "Store", "Manager");
        string csv = builder.BuildItemSalesCsv(rows);
        AssertEqual("N/A", pdf.Rows[0][8], "service stock PDF");
        AssertContains(csv, "\"Service\"");
        AssertContains(csv, "\"\"");
    }

    public static void FinancialSummaryExportKeepsVoucherSeparate()
    {
        var summary = new FinancialSummaryDto
        {
            GrossMerchandiseSales = 10000m,
            TotalDiscounts = 500m,
            CustomerReturns = 1000m,
            SaleCostOfGoods = 6000m,
            ReturnedCostOfGoods = 600m,
            GiftVoucherIssueValue = 2000m,
            TotalSalesCount = 3
        };
        var builder = new OperationalExportBuilder(new CsvExportService());
        PdfTableDocumentDto document = builder.BuildFinancialSummary(
            summary, DateTime.Today, DateTime.Today, "Store", "Manager");

        AssertTrue(document.Rows.Any(row => row[0]!.Contains("Gift Voucher", StringComparison.Ordinal)), "voucher row");
        AssertTrue(document.Rows.Any(row => row[0]!.Contains("Returned COGS", StringComparison.Ordinal)), "returned cost row");
        AssertEqual("8500.00", document.Rows.Single(row => row[0] == "Net sales")[1], "net sales");
    }

    public static void SupplierSummaryCombinesAllSources()
    {
        var builder = new OperationalExportBuilder(new CsvExportService());
        var debt = new[] { new SupplierOutstandingSummaryDto { SupplierId = 1, SupplierCode = "S1", SupplierName = "Supplier", NetOutstanding = 500m } };
        var purchases = new[] { new SupplierPurchaseVolumeDto { SupplierId = 1, SupplierCode = "S1", SupplierName = "Supplier", GrnCount = 2, TotalGrnValue = 1000m } };
        var returns = new[] { new SupplierReturnSummaryDto { SupplierId = 1, SupplierCode = "S1", SupplierName = "Supplier", ReturnDocumentCount = 1, TotalReturnedQty = 2m, NetSupplierCredit = 100m } };

        PdfTableDocumentDto pdf = builder.BuildSupplierSummary(debt, purchases, returns, DateTime.Today, DateTime.Today, "Store", "Manager");
        string csv = builder.BuildSupplierSummaryCsv(debt, purchases, returns);
        AssertEqual(1, pdf.Rows.Count, "supplier row count");
        AssertContains(csv, "\"500.00\"");
        AssertContains(csv, "\"1000.00\"");
        AssertContains(csv, "\"100.00\"");
    }

    public static void VatExportsUseSnapshotRows()
    {
        var builder = new OperationalExportBuilder(new CsvExportService());
        var summary = new VatReportSummaryDto
        {
            StoreName = "VAT Store",
            IsCurrentlyVatRegistered = true,
            VatRegistrationNumber = "VAT-1",
            GrossOutputVat = 180m,
            NetOutputVat = 180m,
            GrossInputVat = 90m,
            NetInputVat = 90m,
            OperationalVatPosition = 90m,
            CompleteDocumentCount = 1
        };
        var documents = new[]
        {
            new VatReportDocumentRowDto
            {
                SourceType = "Sale", DocumentNumber = "INV-1", DocumentDate = DateTime.Today,
                TaxableAmount = 1000m, VatAmount = 180m, InclusiveAmount = 1180m, SnapshotStatus = "Complete"
            }
        };
        var reconciliation = new[]
        {
            new VatReconciliationRowDto
            {
                SourceType = "Sale", DocumentNumber = "INV-1", DocumentDate = DateTime.Today,
                FieldChecked = "VAT", HeaderOrSourceAmount = 180m, DetailOrReturnedAmount = 180m, Difference = 0m,
                Explanation = "Reconciled"
            }
        };

        PdfTableDocumentDto pdf = builder.BuildVatSummary(summary, Array.Empty<VatCategoryRateRowDto>(), DateTime.Today, DateTime.Today, "Manager");
        string csv = builder.BuildVatDetailsCsv(documents, reconciliation);
        AssertContains(pdf.StoreHeading, "VAT-1");
        AssertContains(csv, "\"INV-1\"");
        AssertContains(csv, "\"Complete\"");
        AssertContains(csv, "\"Reconciled\"");
    }

    public static void SupplierClaimStatementUsesNetAmounts()
    {
        var builder = new OperationalExportBuilder(new CsvExportService());
        var claims = new[]
        {
            new FreeItemClaimSearchDto
            {
                SupplierName = "Supplier", ClaimReferenceNo = "FI-1", InvoiceNo = "INV-1",
                ItemDescription = "Free Item", Quantity = 3m, ReturnedQuantity = 1m,
                ClaimValue = 300m, ClaimValueReduction = 100m, ClaimStatus = SupplierClaimStatusCodes.Submitted
            }
        };
        PdfTableDocumentDto document = builder.BuildSupplierClaimStatement(claims, DateTime.Today, DateTime.Today, "Manager");
        AssertEqual("2", document.Rows[0][4], "net claim quantity");
        AssertEqual("200.00", document.Rows[0][5], "net claim value");
    }

    public static void PaymentReceiptAllocationsReconcile()
    {
        var document = new CustomerPaymentReceiptDocumentDto
        {
            StoreName = "Store",
            ReceiptNo = "CPR-1",
            PaymentDate = DateTime.Today,
            CustomerCode = "C1",
            CustomerName = "Customer",
            PaymentMethod = "Cash",
            Amount = 1500m,
            RemainingBalance = 500m,
            Allocations = new[]
            {
                new CustomerLedgerAllocationDto { InvoiceNo = "INV-1", Amount = 1000m },
                new CustomerLedgerAllocationDto { InvoiceNo = "INV-2", Amount = 500m }
            }
        };
        string text = new CustomerPaymentReceiptTextFormatter().Format(document);
        AssertContains(text, "Allocated: Rs. 1,500.00");
        AssertContains(text, "INV-1");
        AssertContains(text, "INV-2");
    }

    public static void StockBalanceCsvUsesSharedFormatting()
    {
        var rows = new[]
        {
            new StockBalanceDto
            {
                ItemCode = "IT-1", SkuCode = "SKU,1", Description = "Quoted \"Item\"",
                TotalQtyOnHand = 2m, ReorderLevel = 5, UnitCost = 100m, UnitRetail = 150m,
                TotalCostValue = 200m, TotalRetailValue = 300m
            }
        };
        var builder = new OperationalExportBuilder(new CsvExportService());
        string csv = builder.BuildStockBalanceCsv(rows);
        AssertContains(csv, "\"SKU,1\"");
        AssertContains(csv, "\"Quoted \"\"Item\"\"\"");

        string alertCsv = builder.BuildStockAlertsCsv(rows);
        AssertContains(alertCsv, "AlertType");
        AssertContains(alertCsv, StockAlertFilters.LowStock);
        AssertContains(alertCsv, "ReorderLevel");

        AssertEqual(4, ExpiryMonitorFilters.Values.Count, "simple expiry filter count");
        AssertFalse(ExpiryMonitorFilters.Values.Contains(ExpiryMonitorFilters.Within60Days), "sixty-day filter hidden");
        AssertFalse(ExpiryMonitorFilters.Values.Contains(ExpiryMonitorFilters.Within90Days), "ninety-day filter hidden");
        AssertFalse(ExpiryMonitorFilters.Values.Contains(ExpiryMonitorFilters.MissingExpiry), "missing-expiry filter hidden");

        using var factory = new TestDbContextFactory();
        using (AppDbContext context = factory.CreateDbContext())
        {
            var category = new Category
            {
                CategoryCode = "STK",
                CategoryName = "Stock",
                CreatedBy = "Tests",
                UpdatedBy = "Tests"
            };
            var uom = new UnitOfMeasure
            {
                UomCode = "EA",
                UomDescription = "Each",
                IsActive = true
            };
            context.Categories.Add(category);
            context.UnitsOfMeasure.Add(uom);
            context.SaveChanges();

            var normalParent = new ItemParent
            {
                ItemCode = "NORMAL",
                ItemName = "Normal Stock",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                ItemType = ItemTypeCodes.StockItem,
                HasBatchTracking = false
            };
            var lowParent = new ItemParent
            {
                ItemCode = "LOW",
                ItemName = "Low Stock",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                ItemType = ItemTypeCodes.StockItem,
                HasBatchTracking = false
            };
            var zeroParent = new ItemParent
            {
                ItemCode = "ZERO",
                ItemName = "Out of Stock",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                ItemType = ItemTypeCodes.StockItem,
                HasBatchTracking = false
            };
            var negativeParent = new ItemParent
            {
                ItemCode = "NEG",
                ItemName = "Negative Stock",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                ItemType = ItemTypeCodes.StockItem,
                HasBatchTracking = false
            };
            var serviceParent = new ItemParent
            {
                ItemCode = "SERVICE",
                ItemName = "Service",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                ItemType = ItemTypeCodes.Service,
                HasBatchTracking = false
            };
            context.ItemParents.AddRange(
                normalParent,
                lowParent,
                zeroParent,
                negativeParent,
                serviceParent);
            context.SaveChanges();

            var normal = new ItemVariant
            {
                ItemParentId = normalParent.Id,
                SkuCode = "NORMAL-1",
                ReorderLevel = 5
            };
            var low = new ItemVariant
            {
                ItemParentId = lowParent.Id,
                SkuCode = "LOW-1",
                ReorderLevel = 5
            };
            var zero = new ItemVariant
            {
                ItemParentId = zeroParent.Id,
                SkuCode = "ZERO-1",
                ReorderLevel = 5
            };
            var negative = new ItemVariant
            {
                ItemParentId = negativeParent.Id,
                SkuCode = "NEG-1",
                ReorderLevel = 5
            };
            var service = new ItemVariant
            {
                ItemParentId = serviceParent.Id,
                SkuCode = "SERVICE-1",
                ReorderLevel = 5
            };
            context.ItemVariants.AddRange(normal, low, zero, negative, service);
            context.SaveChanges();

            context.ItemBatches.AddRange(
                new ItemBatch
                {
                    ItemVariantId = normal.Id,
                    BatchNo = "GENERAL",
                    CurrentStock = 10m,
                    ReceivedDate = DateTime.Today
                },
                new ItemBatch
                {
                    ItemVariantId = low.Id,
                    BatchNo = "GENERAL",
                    CurrentStock = 2m,
                    ReceivedDate = DateTime.Today
                },
                new ItemBatch
                {
                    ItemVariantId = negative.Id,
                    BatchNo = "GENERAL",
                    CurrentStock = -1m,
                    ReceivedDate = DateTime.Today
                },
                new ItemBatch
                {
                    ItemVariantId = service.Id,
                    BatchNo = "GENERAL",
                    CurrentStock = 1m,
                    ReceivedDate = DateTime.Today
                });
            context.SaveChanges();
        }

        var repository = new StockBalanceRepository(factory);
        List<StockBalanceDto> valuationRows = repository
            .GetStockBalancesAsync()
            .GetAwaiter()
            .GetResult();
        List<StockBalanceDto> allAlerts = repository
            .GetStockAlertsAsync()
            .GetAwaiter()
            .GetResult();
        List<StockBalanceDto> lowAlerts = repository
            .GetStockAlertsAsync(alertFilter: StockAlertFilters.LowStock)
            .GetAwaiter()
            .GetResult();
        List<StockBalanceDto> zeroAlerts = repository
            .GetStockAlertsAsync(alertFilter: StockAlertFilters.OutOfStock)
            .GetAwaiter()
            .GetResult();
        List<StockBalanceDto> negativeAlerts = repository
            .GetStockAlertsAsync(alertFilter: StockAlertFilters.Negative)
            .GetAwaiter()
            .GetResult();

        AssertEqual(2, valuationRows.Count, "positive valuation row count");
        AssertTrue(valuationRows.All(row => row.TotalQtyOnHand > 0m), "valuation rows are positive");
        AssertFalse(valuationRows.Any(row => row.ItemCode == "SERVICE"), "services excluded from valuation");
        AssertEqual(3, allAlerts.Count, "all stock alert row count");
        AssertEqual(1, lowAlerts.Count, "low-stock row count");
        AssertEqual("LOW", lowAlerts[0].ItemCode, "low-stock row");
        AssertEqual(1, zeroAlerts.Count, "out-of-stock row count");
        AssertEqual("ZERO", zeroAlerts[0].ItemCode, "out-of-stock row");
        AssertEqual(1, negativeAlerts.Count, "negative-stock row count");
        AssertEqual("NEG", negativeAlerts[0].ItemCode, "negative-stock row");
        AssertFalse(allAlerts.Any(row => row.ItemCode == "NORMAL"), "normal stock excluded from alerts");
        AssertFalse(allAlerts.Any(row => row.ItemCode == "SERVICE"), "services excluded from alerts");
    }

    public static void PdfTextExportCreatesValidPdf()
    {
        string path = TempFile("phase9b_text", ".pdf");
        try
        {
            new PdfExportService().WriteTextPdf(path, "RECEIPT", "INV-1", "Saved receipt snapshot", "Cashier");
            AssertPdf(path);
        }
        finally
        {
            Delete(path);
        }
    }

    public static void PdfTableExportSupportsMultiplePages()
    {
        string path = TempFile("phase9b_table", ".pdf");
        try
        {
            var document = new PdfTableDocumentDto
            {
                Title = "MULTI PAGE TEST",
                GeneratedBy = "Tests",
                Columns = new[]
                {
                    new PdfTableColumnDto { Header = "No", WidthCentimeters = 2, IsNumeric = true },
                    new PdfTableColumnDto { Header = "Description", WidthCentimeters = 12 }
                },
                Rows = Enumerable.Range(1, 180)
                    .Select(index => (IReadOnlyList<string?>)new string?[] { index.ToString(), $"Export row {index}" })
                    .ToList()
            };
            new PdfExportService().WriteTablePdf(path, document);
            AssertPdf(path);
            AssertTrue(new FileInfo(path).Length > 2000, "multipage PDF size");
        }
        finally
        {
            Delete(path);
        }
    }

    public static void ExportAuthorizationEnforcesRoles()
    {
        using var factory = new TestDbContextFactory();
        var auth = new AuthService(new UserRepository(factory));
        var authorization = new ExportAuthorizationService(auth);
        AssertThrows<UnauthorizedAccessException>(authorization.EnsureOperationalDocumentAllowed);

        SetCurrentUser(auth, new User { Username = "cashier", Role = UserRole.Cashier, IsActive = true });
        authorization.EnsureOperationalDocumentAllowed();
        AssertThrows<UnauthorizedAccessException>(authorization.EnsureBulkFinancialExportAllowed);

        SetCurrentUser(auth, new User { Username = "manager", Role = UserRole.Manager, IsActive = true });
        authorization.EnsureBulkFinancialExportAllowed();
        SetCurrentUser(auth, new User { Username = "admin", Role = UserRole.Admin, IsActive = true });
        authorization.EnsureBulkFinancialExportAllowed();
    }

    public static void DashboardExcludesServicesFromStockAlerts()
    {
        using var factory = new TestDbContextFactory();
        using (AppDbContext context = factory.CreateDbContext())
        {
            var category = new Category { CategoryCode = "TST", CategoryName = "Test", CreatedBy = "Tests", UpdatedBy = "Tests" };
            var uom = new UnitOfMeasure { UomCode = "EA", UomDescription = "Each", IsActive = true };
            context.Categories.Add(category);
            context.UnitsOfMeasure.Add(uom);
            context.SaveChanges();

            var lowParent = new ItemParent { ItemCode = "LOW", ItemName = "Low Stock", CategoryId = category.Id, UnitOfMeasureId = uom.Id, ItemType = ItemTypeCodes.StockItem };
            var negativeParent = new ItemParent { ItemCode = "NEG", ItemName = "Negative Stock", CategoryId = category.Id, UnitOfMeasureId = uom.Id, ItemType = ItemTypeCodes.StockItem };
            var serviceParent = new ItemParent { ItemCode = "SVC", ItemName = "Service", CategoryId = category.Id, UnitOfMeasureId = uom.Id, ItemType = ItemTypeCodes.Service, HasBatchTracking = false };
            context.ItemParents.AddRange(lowParent, negativeParent, serviceParent);
            context.SaveChanges();

            var low = new ItemVariant { ItemParentId = lowParent.Id, SkuCode = "LOW-1", ReorderLevel = 5 };
            var negative = new ItemVariant { ItemParentId = negativeParent.Id, SkuCode = "NEG-1", ReorderLevel = 2 };
            var service = new ItemVariant { ItemParentId = serviceParent.Id, SkuCode = "SVC-1", ReorderLevel = 999 };
            context.ItemVariants.AddRange(low, negative, service);
            context.SaveChanges();
            context.ItemBatches.Add(new ItemBatch { ItemVariantId = negative.Id, BatchNo = "GENERAL", CurrentStock = -1m });
            context.SaveChanges();
        }

        var repository = new DashboardRepository(
            factory,
            new FinancialAnalyticsRepository(factory),
            new SalesAnalyticsRepository(factory),
            new SupplierReportRepository(factory));
        DashboardSummaryDto result = repository.GetSummaryAsync(DateTime.Today, DateTime.Today).GetAwaiter().GetResult();
        AssertEqual(1, result.LowStockCount, "low stock count");
        AssertEqual(1, result.NegativeStockCount, "negative stock count");
        AssertFalse(result.AttentionItems.Any(row => row.Message.Contains("Service", StringComparison.OrdinalIgnoreCase)), "service excluded from stock alerts");
    }

    public static void EmptyExportsCreateNoOutput()
    {
        var builder = new OperationalExportBuilder(new CsvExportService());
        AssertThrows<InvalidOperationException>(() => builder.BuildItemSalesCsv(Array.Empty<ItemPerformanceDto>()));
        AssertThrows<InvalidOperationException>(() => builder.BuildStockBalanceCsv(Array.Empty<StockBalanceDto>()));
        AssertThrows<InvalidOperationException>(() => builder.BuildStockAlertsCsv(Array.Empty<StockBalanceDto>()));
        string path = TempFile("phase9b_empty", ".pdf");
        Delete(path);
        AssertThrows<InvalidOperationException>(() => new PdfExportService().WriteTextPdf(path, "EMPTY", string.Empty, string.Empty, "Tests"));
        AssertFalse(File.Exists(path), "empty PDF not created");
    }

    private static void SetCurrentUser(AuthService auth, User user)
    {
        FieldInfo field = typeof(AuthService).GetField("<CurrentUser>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AuthService CurrentUser backing field was not found.");
        field.SetValue(auth, user);
    }

    private static void AssertPdf(string path)
    {
        AssertTrue(File.Exists(path), "PDF exists");
        byte[] header = File.ReadAllBytes(path).Take(5).ToArray();
        AssertEqual("%PDF-", System.Text.Encoding.ASCII.GetString(header), "PDF header");
    }

    private static string TempFile(string prefix, string extension) =>
        Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}{extension}");

    private static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void AssertContains(string value, string expected)
    {
        if (value == null || !value.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected text was not found: {expected}");
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Assertion failed: {name}.");
    }

    private static void AssertFalse(bool condition, string name) => AssertTrue(!condition, name);

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Assertion failed for {name}. Expected {expected}; actual {actual}.");
    }

    private static void AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            using AppDbContext context = CreateDbContext();
            context.Database.EnsureCreated();
        }

        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _connection.Dispose();
    }
}
