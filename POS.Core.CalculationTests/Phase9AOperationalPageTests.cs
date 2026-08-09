using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Returns;

namespace POS.Core.CalculationTests;

internal static class Phase9AOperationalPageTests
{
    public static void CustomerReturnHistoryFiltersAndLoadsDetails()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        TestDocuments documents = SeedOperationalDocuments(factory, scenario);
        var repository = new CustomerReturnRepository(factory, new CustomerReturnAllocationCalculator());

        List<CustomerReturnHistoryRowDto> rows = repository
            .GetReturnHistoryAsync(DateTime.Today, DateTime.Today, documents.CreditNoteNo)
            .GetAwaiter().GetResult();
        AssertEqual(1, rows.Count, "return history count");
        AssertEqual(documents.CreditNoteNo, rows[0].CreditNoteNo, "credit note number");
        AssertMoney(1180m, rows[0].TotalRefundAmount, "return total");

        CustomerReturnHistoryDetailsDto? details = repository
            .GetReturnHistoryDetailsAsync(rows[0].Id)
            .GetAwaiter().GetResult();
        AssertTrue(details != null, "return details exist");
        AssertEqual(1, details!.Lines.Count, "return line count");
        AssertEqual(ItemTypeCodes.StockItem, details.Lines[0].ItemType, "return item type");
        AssertMoney(180m, details.TotalVatAmount ?? 0m, "return VAT snapshot");
    }

    public static void BackOfficeCartHistoryShowsStatusesAndLines()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        int heldId;
        using (AppDbContext context = factory.CreateDbContext())
        {
            DateTime nowUtc = LocalTodayUtc(11);
            var held = new CashierCartSession
            {
                CartToken = Guid.NewGuid(),
                ReferenceNo = "CART-HELD-9A",
                ShiftSessionId = scenario.ShiftSessionId,
                TerminalNo = "T01",
                CashierName = "Audit Cashier",
                CustomerNameSnapshot = "Walk-In",
                GrossTotal = 2360m,
                NetTotal = 2360m,
                ItemCount = 1,
                TotalQuantity = 2m,
                Status = "Held",
                CreatedAtUtc = nowUtc.AddMinutes(-10),
                UpdatedAtUtc = nowUtc,
                HeldAtUtc = nowUtc,
                HeldBy = "Audit Cashier",
                CreatedBy = "Audit Cashier",
                UpdatedBy = "Audit Cashier",
                Lines = new List<CashierCartLine>
                {
                    new()
                    {
                        LineNumber = 1,
                        LineType = CashierCartLineTypeCodes.StockItem,
                        ItemVariantId = scenario.StockVariantId,
                        ItemBatchId = scenario.StockBatchId,
                        Description = "Test Stock Item",
                        Quantity = 2m,
                        UnitPrice = 1180m,
                        LineTotal = 2360m,
                        SnapshotJson = "{}"
                    }
                }
            };
            context.CashierCartSessions.Add(held);
            context.SaveChanges();
            heldId = held.Id;
        }

        var repository = new CashierCartRepository(factory);
        List<BackOfficeCartSessionDto> rows = repository
            .GetBackOfficeSessionsAsync(DateTime.Today, DateTime.Today, "Held", "CART-HELD")
            .GetAwaiter().GetResult();
        AssertEqual(1, rows.Count, "held cart count");
        AssertEqual("Held", rows[0].Status, "held cart status");

        BackOfficeCartDetailsDto? details = repository
            .GetBackOfficeSessionDetailsAsync(heldId)
            .GetAwaiter().GetResult();
        AssertTrue(details != null, "cart details exist");
        AssertEqual(1, details!.Lines.Count, "cart line count");
        AssertMoney(2360m, details.Session.NetTotal, "cart net total");
    }

    public static void ItemAnalyticsSupportsStockAndService()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        SeedOperationalDocuments(factory, scenario);
        var repository = new SalesAnalyticsRepository(factory);

        ItemSalesAnalyticsResultDto result = repository
            .GetAnalyticsAsync(DateTime.Today, DateTime.Today, string.Empty)
            .GetAwaiter().GetResult();

        ItemPerformanceDto stock = result.Items.Single(row => row.ItemVariantId == scenario.StockVariantId);
        ItemPerformanceDto service = result.Items.Single(row => row.ItemVariantId == scenario.ServiceVariantId);
        AssertEqual(ItemTypeCodes.StockItem, stock.ItemType, "stock type");
        AssertTrue(stock.CurrentStock.HasValue, "stock current quantity exists");
        AssertEqual(ItemTypeCodes.Service, service.ItemType, "service type");
        AssertFalse(service.CurrentStock.HasValue, "service has no inventory quantity");
        AssertEqual("Test Sales", service.CategoryName, "service category");
    }

    public static void ItemAnalyticsUsesPeriodActivityReturns()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        SeedOperationalDocuments(factory, scenario);
        var repository = new SalesAnalyticsRepository(factory);

        ItemSalesAnalyticsResultDto result = repository
            .GetAnalyticsAsync(DateTime.Today, DateTime.Today, scenario.StockSku)
            .GetAwaiter().GetResult();
        ItemPerformanceDto row = result.Items.Single();
        AssertQuantity(2m, row.SoldQuantity, "sold quantity");
        AssertQuantity(1m, row.ReturnedQuantity, "returned quantity");
        AssertQuantity(1m, row.NetQuantity, "net quantity");
        AssertMoney(1080m, row.NetSales, "period net sales");
        AssertMoney(600m, row.NetCost, "period net cost");
    }

    public static void SalesExplorerExcludesGiftVoucherIssueValue()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        TestDocuments documents = SeedOperationalDocuments(factory, scenario);
        var repository = new MasterSalesAnalyticsRepository(factory);

        PagedSalesResult result = repository
            .GetPagedSalesAsync(DateTime.Today, DateTime.Today, documents.InvoiceNo, "T01", "All", "All", 1, 50)
            .GetAwaiter().GetResult();
        AssertEqual(1, result.TotalCount, "sales explorer count");
        AssertMoney(3440m, result.Records[0].NetAmount, "merchandise net excludes voucher issue");
        AssertMoney(1180m, result.Records[0].ReturnedAmount, "returned amount");
        AssertEqual("Partially Returned", result.Records[0].ReturnStatus, "return status");
    }

    public static void SalesExplorerLoadsTaxPaymentsCreditNotesAndAudits()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        TestDocuments documents = SeedOperationalDocuments(factory, scenario);
        var repository = new MasterSalesAnalyticsRepository(factory);

        SaleReceiptDetailsDto? details = repository
            .GetSaleReceiptDetailsAsync(documents.SalesHeaderId)
            .GetAwaiter().GetResult();
        AssertTrue(details != null, "sales explorer details exist");
        AssertEqual(3, details!.Lines.Count, "sale detail line count");
        AssertEqual(2, details.Payments.Count, "payment detail count");
        AssertEqual(1, details.CreditNotes.Count, "credit note link count");
        AssertEqual(1, details.DocumentAudits.Count, "document audit count");
        AssertMoney(540m, details.TotalVatAmount ?? 0m, "sale VAT snapshot");
        AssertEqual("Complete", details.TaxSnapshotStatus, "sale tax status");
    }

    public static void FinancialSummaryExcludesVoucherAndReversesReturnedCost()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        SeedOperationalDocuments(factory, scenario);
        var repository = new FinancialAnalyticsRepository(factory);

        FinancialSummaryDto result = repository
            .GetFinancialSummaryAsync(DateTime.Today, DateTime.Today)
            .GetAwaiter().GetResult();
        AssertMoney(3540m, result.GrossMerchandiseSales, "gross merchandise sales");
        AssertMoney(100m, result.TotalDiscounts, "discount total");
        AssertMoney(1180m, result.CustomerReturns, "customer returns");
        AssertMoney(1600m, result.SaleCostOfGoods, "sale cost");
        AssertMoney(600m, result.ReturnedCostOfGoods, "returned cost");
        AssertMoney(1000m, result.NetCostOfGoods, "net cost");
        AssertMoney(1000m, result.GiftVoucherIssueValue, "voucher issue value");
    }

    public static void FinancialSummarySeparatesCashMovementsAndTenders()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        SeedOperationalDocuments(factory, scenario);
        using (AppDbContext context = factory.CreateDbContext())
        {
            DateTime timestamp = DateTime.Today.AddHours(12);
            context.CashMovements.AddRange(
                Movement(scenario, CashMovementTypeCodes.PaidIn, 100m, CashMovementReasonCodes.Other, timestamp),
                Movement(scenario, CashMovementTypeCodes.PaidOut, 50m, CashMovementReasonCodes.StoreExpense, timestamp),
                Movement(scenario, CashMovementTypeCodes.PaidIn, 200m, CashMovementReasonCodes.FloatIn, timestamp),
                Movement(scenario, CashMovementTypeCodes.PaidOut, 150m, CashMovementReasonCodes.FloatOut, timestamp),
                Movement(scenario, CashMovementTypeCodes.PaidOut, 30m, CashMovementReasonCodes.CustomerRefund, timestamp));
            context.SaveChanges();
        }

        FinancialSummaryDto result = new FinancialAnalyticsRepository(factory)
            .GetFinancialSummaryAsync(DateTime.Today, DateTime.Today)
            .GetAwaiter().GetResult();
        AssertMoney(100m, result.PaidIn, "ordinary paid in");
        AssertMoney(50m, result.PaidOut, "ordinary paid out");
        AssertMoney(200m, result.FloatIn, "float in");
        AssertMoney(150m, result.FloatOut, "float out");
        AssertMoney(30m, result.CustomerCashRefunds, "cash refunds");
        AssertEqual(2, result.TenderTotals.Count, "tender group count");
        AssertMoney(4440m, result.TenderTotals.Sum(row => row.Amount), "tender total");
    }

    public static void SecurityAuditAggregatesOperationalEvents()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        TestDocuments documents = SeedOperationalDocuments(factory, scenario);
        using (AppDbContext context = factory.CreateDbContext())
        {
            DateTime utc = LocalTodayUtc(13);
            context.CashierCartSessions.Add(new CashierCartSession
            {
                CartToken = Guid.NewGuid(), ReferenceNo = "CART-CANCELLED-9A", ShiftSessionId = scenario.ShiftSessionId,
                TerminalNo = "T01", CashierName = "Audit Cashier", Status = "Cancelled",
                CreatedAtUtc = utc.AddMinutes(-5), UpdatedAtUtc = utc, CancelledAtUtc = utc,
                CancelledBy = "Audit Cashier", CancellationReasonCode = "CUSTOMER_CANCELLED",
                CancellationReasonText = "Customer changed mind", CreatedBy = "Audit Cashier", UpdatedBy = "Audit Cashier"
            });
            context.LoginAuditEvents.Add(new LoginAuditEvent
            {
                UsernameAttempted = "FailedUser", EventType = "Failed", ApplicationName = "BackOffice",
                MachineName = "TEST-PC", Message = "Invalid password", EventTimeUtc = utc
            });
            context.CashDrawerEvents.Add(new CashDrawerEvent
            {
                ShiftSessionId = scenario.ShiftSessionId, TerminalNo = "T01", CashierName = "Audit Cashier",
                EventType = CashDrawerEventTypeCodes.NoSale, Reason = "No Sale", RequestedAtUtc = utc,
                Succeeded = false, FailureMessage = "Drawer offline"
            });
            context.SaveChanges();
        }

        SecurityAuditResultDto result = new SecurityAuditRepository(factory)
            .GetAuditAsync(DateTime.Today, DateTime.Today, string.Empty)
            .GetAwaiter().GetResult();
        AssertTrue(result.Summary.TotalEventCount >= 4, "audit event count");
        AssertEqual(1, result.Summary.FailedLoginCount, "failed login count");
        AssertEqual(1, result.Summary.CancelledCartCount, "cancelled cart count");
        AssertEqual(1, result.Summary.CustomerReturnCount, "return count");
        AssertEqual(1, result.Summary.DrawerFailureCount, "drawer failure count");
        AssertTrue(result.Events.Any(row => row.ReferenceNo == documents.CreditNoteNo), "credit note audit event");
    }

    public static void SecurityAuditSearchFiltersEvents()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        using (AppDbContext context = factory.CreateDbContext())
        {
            context.LoginAuditEvents.Add(new LoginAuditEvent
            {
                UsernameAttempted = "UniqueFailedUser", EventType = "Failed", ApplicationName = "Cashier",
                MachineName = "TEST-PC", Message = "Invalid password", EventTimeUtc = LocalTodayUtc(9)
            });
            context.SaveChanges();
        }

        SecurityAuditResultDto result = new SecurityAuditRepository(factory)
            .GetAuditAsync(DateTime.Today, DateTime.Today, "UniqueFailedUser")
            .GetAwaiter().GetResult();
        AssertEqual(1, result.Events.Count, "filtered audit count");
        AssertEqual("UniqueFailedUser", result.Events[0].Actor, "filtered audit actor");
    }

    public static void SupplierReportsFilterAndReconcile()
    {
        using var factory = new TestDbContextFactory();
        TestScenario scenario = SeedScenario(factory);
        SeedSupplierReporting(factory, scenario);
        var repository = new SupplierReportRepository(factory);

        List<SupplierOutstandingSummaryDto> outstanding = repository
            .GetSupplierOutstandingSummaryAsync("Alpha")
            .GetAwaiter().GetResult();
        List<SupplierPurchaseVolumeDto> purchases = repository
            .GetPurchasingVolumeAsync(DateTime.Today, DateTime.Today, "Alpha")
            .GetAwaiter().GetResult();
        List<SupplierReturnSummaryDto> returns = repository
            .GetSupplierReturnSummaryAsync(DateTime.Today, DateTime.Today, "Alpha")
            .GetAwaiter().GetResult();

        AssertEqual(1, outstanding.Count, "filtered outstanding rows");
        AssertMoney(700m, outstanding[0].NetOutstanding, "supplier outstanding");
        AssertEqual(1, purchases.Count, "filtered purchase rows");
        AssertMoney(1000m, purchases[0].TotalGrnValue, "supplier purchases");
        AssertEqual(1, returns.Count, "filtered supplier return rows");
        AssertMoney(100m, returns[0].NetSupplierCredit, "supplier return credit");
        AssertQuantity(2m, returns[0].TotalReturnedQty, "supplier return quantity");
    }

    public static void OperationalRepositoriesRejectInvalidDateRanges()
    {
        using var factory = new TestDbContextFactory();
        DateTime start = DateTime.Today;
        DateTime end = start.AddDays(-1);
        AssertThrowsArgument(() => new SalesAnalyticsRepository(factory).GetAnalyticsAsync(start, end, string.Empty).GetAwaiter().GetResult());
        AssertThrowsArgument(() => new MasterSalesAnalyticsRepository(factory).GetPagedSalesAsync(start, end, string.Empty, string.Empty, "All", "All", 1, 50).GetAwaiter().GetResult());
        AssertThrowsArgument(() => new SecurityAuditRepository(factory).GetAuditAsync(start, end, string.Empty).GetAwaiter().GetResult());
        AssertThrowsArgument(() => new FinancialAnalyticsRepository(factory).GetFinancialSummaryAsync(start, end).GetAwaiter().GetResult());
    }

    private static TestDocuments SeedOperationalDocuments(TestDbContextFactory factory, TestScenario scenario)
    {
        using AppDbContext context = factory.CreateDbContext();
        string invoiceNo = $"INV-9A-{Guid.NewGuid():N}"[..18];
        var sale = new SalesHeader
        {
            ShiftSessionId = scenario.ShiftSessionId,
            InvoiceNo = invoiceNo,
            TerminalNo = "T01",
            CashierName = "Audit Cashier",
            CustomerCode = "CUST-9A",
            CustomerName = "Phase 9A Customer",
            CustomerType = "Retail",
            TransactionDate = DateTime.Today.AddHours(10),
            DocumentType = "TaxInvoice",
            TaxInvoiceNo = "TAX-9A-001",
            IsVatRegisteredSale = true,
            GrossTotal = 4540m,
            TotalDiscount = 100m,
            NetTotal = 4440m,
            GiftVoucherIssueTotal = 1000m,
            AmountTendered = 4440m,
            PaymentMethod = "Split",
            Status = "Completed",
            TaxableAmountTotal = 3000m,
            TotalVatAmount = 540m,
            StandardRatedAmount = 3540m,
            ZeroRatedAmount = 0m,
            ExemptAmount = 0m,
            OutOfScopeAmount = 1000m,
            TaxSnapshotStatus = "Complete"
        };
        sale.SalesLines.Add(SaleLine(scenario.StockVariantId, scenario.StockBatchId, scenario.StockSku, "Test Stock Item", ItemTypeCodes.StockItem, 2m, 1180m, 600m, 2360m, 100m, 2260m, 2000m, 360m));
        sale.SalesLines.Add(SaleLine(scenario.ServiceVariantId, null, scenario.ServiceSku, "Installation Service", ItemTypeCodes.Service, 1m, 1180m, 400m, 1180m, 0m, 1180m, 1000m, 180m));
        sale.SalesLines.Add(new SalesLine
        {
            ItemVariantId = null,
            ItemBatchId = null,
            SkuCode = "GV-9A",
            Barcode = "GV-9A",
            ItemDescription = "Gift Voucher",
            Uom = "EA",
            Quantity = 1m,
            UnitPrice = 1000m,
            GrossAmount = 1000m,
            LineTotal = 1000m,
            CostPrice = 0m,
            OriginalUnitPrice = 1000m,
            IsGiftVoucherSale = true,
            ItemTypeSnapshot = "GiftVoucher",
            TaxCategoryCodeSnapshot = TaxCategoryCodes.OutOfScope,
            TaxableAmountSnapshot = 0m,
            VatAmountSnapshot = 0m,
            TaxInclusiveAmountSnapshot = 0m,
            TaxSnapshotStatus = "Complete"
        });
        sale.SalesPayments.Add(new SalesPayment { PaymentType = PaymentTypeCodes.Cash, Amount = 2000m, TenderedAmount = 2000m, PaymentDate = DateTime.Today.AddHours(10), EnteredBy = "Audit Cashier", TerminalNo = "T01" });
        sale.SalesPayments.Add(new SalesPayment { PaymentType = PaymentTypeCodes.Card, Amount = 2440m, TenderedAmount = 2440m, ReferenceNo = "CARD-9A", PaymentDate = DateTime.Today.AddHours(10), EnteredBy = "Audit Cashier", TerminalNo = "T01" });
        sale.SalesDocumentAudits.Add(new SalesDocumentAudit
        {
            DocumentType = "Receipt", DocumentNumber = invoiceNo, EventType = "OriginalPrint", CopyNumber = 1,
            IsSuccessful = true, OccurredAtUtc = LocalTodayUtc(10), PerformedBy = "Audit Cashier", TerminalNo = "T01", PrinterName = "Test Printer"
        });
        context.SalesHeaders.Add(sale);
        context.SaveChanges();

        SalesLine stockLine = sale.SalesLines.First(line => line.ItemVariantId == scenario.StockVariantId);
        string creditNote = "CN-9A-001";
        var returnHeader = new CustomerReturnHeader
        {
            ReturnNo = "RTN-9A-001",
            CreditNoteNo = creditNote,
            OriginalInvoiceNo = invoiceNo,
            OriginalSalesHeaderId = sale.Id,
            ShiftSessionId = scenario.ShiftSessionId,
            TerminalNo = "T01",
            CashierName = "Audit Cashier",
            AuthorizedBy = "Manager One",
            ReturnDate = DateTime.Today.AddHours(11),
            TotalRefundAmount = 1180m,
            CashRefundAmount = 1180m,
            RefundMethod = "Cash",
            DocumentType = "CreditNote",
            TaxableAmountTotal = 1000m,
            TotalVatAmount = 180m,
            StandardRatedAmount = 1180m,
            ZeroRatedAmount = 0m,
            ExemptAmount = 0m,
            OutOfScopeAmount = 0m,
            TaxSnapshotStatus = "Complete",
            Lines = new List<CustomerReturnLine>
            {
                new()
                {
                    SalesLineId = stockLine.Id,
                    ItemVariantId = scenario.StockVariantId,
                    ItemBatchId = scenario.StockBatchId,
                    ItemDescription = "Test Stock Item",
                    QuantityReturned = 1m,
                    RefundValue = 1180m,
                    LineTotalRefund = 1180m,
                    ReturnReason = "Customer return",
                    InventoryAction = "Restocked",
                    ItemTypeSnapshot = ItemTypeCodes.StockItem,
                    TaxCategoryCodeSnapshot = TaxCategoryCodes.Standard,
                    TaxCodeSnapshot = "VAT-STD",
                    TaxNameSnapshot = "Standard VAT",
                    TaxRatePercentSnapshot = 18m,
                    IsTaxInclusiveSnapshot = true,
                    TaxableAmountSnapshot = 1000m,
                    VatAmountSnapshot = 180m,
                    TaxInclusiveAmountSnapshot = 1180m,
                    OriginalTaxableAmount = 2000m,
                    OriginalVatAmount = 360m,
                    OriginalTaxInclusiveAmount = 2360m,
                    TaxSnapshotStatus = "Complete"
                }
            }
        };
        context.CustomerReturnHeaders.Add(returnHeader);
        context.SaveChanges();
        return new TestDocuments(sale.Id, invoiceNo, creditNote);
    }

    private static SalesLine SaleLine(int variantId, int? batchId, string sku, string description, string type,
        decimal quantity, decimal unitPrice, decimal cost, decimal gross, decimal discount, decimal total,
        decimal taxable, decimal vat)
    {
        return new SalesLine
        {
            ItemVariantId = variantId,
            ItemBatchId = batchId,
            SkuCode = sku,
            Barcode = sku,
            ItemDescription = description,
            BatchNo = batchId.HasValue ? "BATCH-9A" : string.Empty,
            Uom = batchId.HasValue ? "PCS" : "JOB",
            ItemTypeSnapshot = type,
            Quantity = quantity,
            UnitPrice = unitPrice,
            CostPrice = cost,
            GrossAmount = gross,
            DiscountAmount = discount,
            LineTotal = total,
            OriginalUnitPrice = unitPrice,
            TaxCategoryCodeSnapshot = TaxCategoryCodes.Standard,
            TaxCodeSnapshot = "VAT-STD",
            TaxNameSnapshot = "Standard VAT",
            TaxRatePercentSnapshot = 18m,
            IsTaxInclusiveSnapshot = true,
            TaxableAmountSnapshot = taxable,
            VatAmountSnapshot = vat,
            TaxInclusiveAmountSnapshot = taxable + vat,
            TaxSnapshotStatus = "Complete"
        };
    }

    private static CashMovement Movement(TestScenario scenario, string type, decimal amount, string reason, DateTime timestamp) =>
        new()
        {
            ShiftSessionId = scenario.ShiftSessionId,
            MovementType = type,
            Amount = amount,
            ReasonCategory = reason,
            CashierName = "Audit Cashier",
            AuthorizedBy = "Audit Cashier",
            Timestamp = timestamp,
            ReferenceVoucherNo = Guid.NewGuid().ToString("N")[..12]
        };

    private static void SeedSupplierReporting(TestDbContextFactory factory, TestScenario scenario)
    {
        using AppDbContext context = factory.CreateDbContext();
        Supplier alpha = context.Suppliers.Single(row => row.Id == scenario.SupplierId);
        alpha.SupplierName = "Alpha Supplier";
        alpha.CompanyName = "Alpha Trading";
        var beta = new Supplier { SupplierCode = "SUP-BETA", SupplierName = "Beta Supplier", CompanyName = "Beta Trading", Phone1 = "0112222222" };
        context.Suppliers.Add(beta);
        context.SaveChanges();

        context.SupplierLedgers.AddRange(
            new SupplierLedger { SupplierId = alpha.Id, TransactionDate = DateTime.Today, TransactionType = "GRN", ReferenceDocument = "GRN-A", ChargeAmount = 1000m, BalanceAfterTransaction = 1000m, CreatedBy = "Test" },
            new SupplierLedger { SupplierId = alpha.Id, TransactionDate = DateTime.Today, TransactionType = "PAYMENT", ReferenceDocument = "PAY-A", PaymentAmount = 200m, BalanceAfterTransaction = 800m, CreatedBy = "Test" },
            new SupplierLedger { SupplierId = alpha.Id, TransactionDate = DateTime.Today, TransactionType = "DEBIT_NOTE", ReferenceDocument = "DN-A", PaymentAmount = 100m, BalanceAfterTransaction = 700m, CreatedBy = "Test" },
            new SupplierLedger { SupplierId = beta.Id, TransactionDate = DateTime.Today, TransactionType = "GRN", ReferenceDocument = "GRN-B", ChargeAmount = 500m, BalanceAfterTransaction = 500m, CreatedBy = "Test" });

        var alphaGrn = new GrnHeader { GrnNumber = "GRN-9A-A", SupplierId = alpha.Id, SupplierInvoiceNo = "INV-A", ReceivedDate = DateTime.Today, InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), NetPayable = 1000m, Status = "Posted", CreatedBy = "Test", PostedBy = "Test" };
        var betaGrn = new GrnHeader { GrnNumber = "GRN-9A-B", SupplierId = beta.Id, SupplierInvoiceNo = "INV-B", ReceivedDate = DateTime.Today, InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), NetPayable = 500m, Status = "Posted", CreatedBy = "Test", PostedBy = "Test" };
        context.GrnHeaders.AddRange(alphaGrn, betaGrn);
        context.SaveChanges();

        var supplierReturn = new SupplierReturnHeader
        {
            ReturnNumber = "SRTN-9A-A", SupplierId = alpha.Id, GrnHeaderId = alphaGrn.Id,
            OriginalInvoiceNo = alphaGrn.SupplierInvoiceNo, ReturnDate = DateTime.Today,
            AuthorizedBy = "Manager One", GrossCredit = 120m, RestockingFee = 20m,
            NetCredit = 100m, Status = "Posted", CreatedBy = "Test", PostedBy = "Test",
            ReturnLines = new List<SupplierReturnLine>
            {
                new()
                {
                    ItemVariantId = scenario.StockVariantId,
                    ItemBatchId = scenario.StockBatchId,
                    BatchNo = "BATCH-9A",
                    ReturnQty = 2m,
                    HistoricalCost = 60m,
                    CreditValue = 120m,
                    ReasonCode = "DAMAGED",
                    LineStatus = "Posted"
                }
            }
        };
        context.SupplierReturnHeaders.Add(supplierReturn);
        context.SaveChanges();
    }

    private static TestScenario SeedScenario(TestDbContextFactory factory)
    {
        using AppDbContext context = factory.CreateDbContext();
        var category = new Category { CategoryCode = "CAT-9A", CategoryName = "Test Sales", CreatedBy = "Test", UpdatedBy = "Test" };
        var tax = new TaxCategory { CategoryCode = TaxCategoryCodes.Standard, CategoryName = "Standard VAT", TreatmentType = TaxTreatmentTypes.StandardRated, IsRateBased = true, IsActive = true };
        var supplier = new Supplier { SupplierCode = "SUP-ALPHA", SupplierName = "Test Supplier", Phone1 = "0111111111" };
        context.AddRange(category, tax, supplier);
        context.SaveChanges();

        var stockParent = new ItemParent { ItemCode = "STOCK-9A", ItemName = "Test Stock Item", PrintName = "Test Stock Item", CategoryId = category.Id, UnitOfMeasureId = 1, BaseUom = "PCS", ItemType = ItemTypeCodes.StockItem, TaxCategoryId = tax.Id, TaxCode = "VAT-STD", IsTaxInclusive = true };
        var serviceParent = new ItemParent { ItemCode = "SERVICE-9A", ItemName = "Installation Service", PrintName = "Installation Service", CategoryId = category.Id, UnitOfMeasureId = 1, BaseUom = "JOB", ItemType = ItemTypeCodes.Service, TaxCategoryId = tax.Id, TaxCode = "VAT-STD", IsTaxInclusive = true, HasBatchTracking = false, IsPurchaseLocked = true };
        var stockVariant = new ItemVariant { ItemParent = stockParent, SkuCode = "STOCK-SKU-9A", Barcode = "STOCK-BC-9A", VariantDescription = "Standard", AverageCost = 600m, CostPrice = 600m, RetailPrice = 1180m, WholesalePrice = 1062m, MinimumPrice = 600m };
        var serviceVariant = new ItemVariant { ItemParent = serviceParent, SkuCode = "SERVICE-SKU-9A", Barcode = "SERVICE-BC-9A", VariantDescription = "Standard", AverageCost = 400m, CostPrice = 400m, RetailPrice = 1180m, WholesalePrice = 1062m, MinimumPrice = 400m };
        context.ItemVariants.AddRange(stockVariant, serviceVariant);
        context.SaveChanges();

        var batch = new ItemBatch { ItemVariantId = stockVariant.Id, BatchNo = "BATCH-9A", InternalBatchBarcode = "BATCH-BC-9A", ReceivedDate = DateTime.Today.AddDays(-5), CostPrice = 600m, RetailPrice = 1180m, WholesalePrice = 1062m, CurrentStock = 20m };
        var shift = new ShiftSession { TerminalNo = "T01", CashierName = "Audit Cashier", StartTime = DateTime.Today.AddHours(8), Status = "Open" };
        context.AddRange(batch, shift);
        context.SaveChanges();
        return new TestScenario(shift.Id, category.Id, supplier.Id, stockVariant.Id, serviceVariant.Id, batch.Id, stockVariant.SkuCode, serviceVariant.SkuCode);
    }

    private static DateTime LocalTodayUtc(int hour) =>
        DateTime.SpecifyKind(DateTime.Today.AddHours(hour), DateTimeKind.Local).ToUniversalTime();

    private static void AssertThrowsArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected an ArgumentException.");
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}.");
    }

    private static void AssertTrue(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"{label}: expected true.");
    }

    private static void AssertFalse(bool condition, string label)
    {
        if (condition) throw new InvalidOperationException($"{label}: expected false.");
    }

    private static void AssertMoney(decimal expected, decimal actual, string label)
    {
        if (decimal.Round(expected, 2) != decimal.Round(actual, 2))
            throw new InvalidOperationException($"{label}: expected {expected:N2}, actual {actual:N2}.");
    }

    private static void AssertQuantity(decimal expected, decimal actual, string label)
    {
        if (decimal.Round(expected, 3) != decimal.Round(actual, 3))
            throw new InvalidOperationException($"{label}: expected {expected:N3}, actual {actual:N3}.");
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"pos-phase9a-{Guid.NewGuid():N}.db");
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path}").Options;
            using AppDbContext context = CreateDbContext();
            context.Database.EnsureCreated();
        }

        public AppDbContext CreateDbContext() => new(_options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            foreach (string path in new[] { _path, _path + "-shm", _path + "-wal" })
                if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed record TestScenario(int ShiftSessionId, int CategoryId, int SupplierId, int StockVariantId,
        int ServiceVariantId, int StockBatchId, string StockSku, string ServiceSku);
    private sealed record TestDocuments(int SalesHeaderId, string InvoiceNo, string CreditNoteNo);
}
