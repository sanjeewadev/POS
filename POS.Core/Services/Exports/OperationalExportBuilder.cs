using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Core.Services.Exports
{
    public sealed class OperationalExportBuilder
    {
        private readonly CsvExportService _csv;

        public OperationalExportBuilder(CsvExportService csv)
        {
            _csv = csv ?? throw new ArgumentNullException(nameof(csv));
        }

        public PdfTableDocumentDto BuildPurchaseOrder(
            PoHeader purchaseOrder,
            StoreSettings settings,
            string generatedBy)
        {
            if (purchaseOrder == null)
                throw new ArgumentNullException(nameof(purchaseOrder));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (purchaseOrder.PoLines.Count == 0)
                throw new InvalidOperationException("The selected purchase order has no lines.");

            return new PdfTableDocumentDto
            {
                Title = "PURCHASE ORDER",
                Subtitle = $"{purchaseOrder.PoNumber} | {purchaseOrder.Status}",
                StoreHeading = StoreHeading(settings),
                GeneratedBy = generatedBy,
                FooterText = "Purchase order values are taken from the saved document.",
                SummaryLines = new[]
                {
                    $"Supplier: {purchaseOrder.Supplier?.SupplierCode} - {purchaseOrder.Supplier?.SupplierName}",
                    $"Order date: {purchaseOrder.OrderDate:yyyy-MM-dd}    Expected: {purchaseOrder.ExpectedDate:yyyy-MM-dd}",
                    $"Terms: {purchaseOrder.Terms}    Credit days: {purchaseOrder.CreditDays}",
                    $"Tax mode: {(purchaseOrder.IsTaxInclusive ? "Prices include VAT" : "VAT added on top")}",
                    $"Subtotal: Rs. {purchaseOrder.Subtotal:N2}    Discounts: Rs. {purchaseOrder.TotalDiscountAmount:N2}    VAT: Rs. {purchaseOrder.TotalTaxAmount:N2}    Net: Rs. {purchaseOrder.NetPayable:N2}",
                    string.IsNullOrWhiteSpace(purchaseOrder.Remarks) ? string.Empty : $"Remarks: {purchaseOrder.Remarks}"
                },
                Columns = new[]
                {
                    Column("Item", 4.4),
                    Column("SKU", 2.5),
                    Column("UOM", 1.4),
                    Column("Qty", 1.6, true),
                    Column("Unit Cost", 2.0, true),
                    Column("Discount", 1.9, true),
                    Column("VAT", 1.8, true),
                    Column("Line Total", 2.2, true)
                },
                Rows = purchaseOrder.PoLines
                    .OrderBy(line => line.Id)
                    .Select(line => (IReadOnlyList<string?>)new string?[]
                    {
                        FirstNonEmpty(line.ReceiptDisplayName, line.Description, line.ItemCode),
                        FirstNonEmpty(line.SkuCode, line.ItemVariant?.SkuCode),
                        line.Uom,
                        Quantity(line.OrderQty),
                        Money(line.ExpectedCost),
                        Money(line.LineDiscount),
                        Money(line.TaxAmount),
                        Money(line.LineTotal)
                    })
                    .ToList()
            };
        }

        public PdfTableDocumentDto BuildFinancialSummary(
            FinancialSummaryDto summary,
            DateTime startDate,
            DateTime endDate,
            string storeHeading,
            string generatedBy)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            var rows = new List<IReadOnlyList<string?>>
            {
                Row("Gross merchandise sales", summary.GrossMerchandiseSales),
                Row("Discounts", -summary.TotalDiscounts),
                Row("Customer returns", -summary.CustomerReturns),
                Row("Net sales", summary.NetSales),
                Row("Sale COGS", summary.SaleCostOfGoods),
                Row("Returned COGS reversal", -summary.ReturnedCostOfGoods),
                Row("Net COGS", summary.NetCostOfGoods),
                Row("Gross profit", summary.GrossProfit),
                Row("Gift Voucher issue value (excluded from revenue)", summary.GiftVoucherIssueValue),
                Row("Posted purchases", summary.PostedPurchases),
                Row("Posted supplier returns", summary.PostedSupplierReturns),
                Row("Paid In", summary.PaidIn),
                Row("Paid Out", summary.PaidOut),
                Row("Float In", summary.FloatIn),
                Row("Float Out", summary.FloatOut),
                Row("Customer cash refunds", summary.CustomerCashRefunds)
            };

            foreach (FinancialTenderTotalDto tender in summary.TenderTotals)
                rows.Add(new[] { $"Tender: {tender.PaymentType} ({tender.TransactionCount:N0})", Money(tender.Amount) });

            return new PdfTableDocumentDto
            {
                Title = "FINANCIAL SUMMARY",
                Subtitle = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                StoreHeading = storeHeading,
                GeneratedBy = generatedBy,
                FooterText = "Operational summary only; this is not a complete accounting profit and loss statement.",
                SummaryLines = new[]
                {
                    $"Completed sales: {summary.TotalSalesCount:N0}",
                    $"Average sale value: Rs. {summary.AverageSaleValue:N2}"
                },
                Columns = new[] { Column("Measure", 11.8), Column("Amount (Rs.)", 4.0, true) },
                Rows = rows
            };
        }

        public PdfTableDocumentDto BuildItemSales(
            IReadOnlyCollection<ItemPerformanceDto> rows,
            DateTime startDate,
            DateTime endDate,
            string storeHeading,
            string generatedBy)
        {
            EnsureRows(rows, "item sales");
            return new PdfTableDocumentDto
            {
                Title = "ITEM SALES ANALYSIS",
                Subtitle = $"Period activity: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                StoreHeading = storeHeading,
                GeneratedBy = generatedBy,
                FooterText = "Sales completed in the period less customer returns processed in the period.",
                SummaryLines = new[]
                {
                    $"Rows: {rows.Count:N0}    Net quantity: {rows.Sum(row => row.NetQuantity):N3}",
                    $"Net sales: Rs. {rows.Sum(row => row.NetSales):N2}    Gross profit: Rs. {rows.Sum(row => row.GrossProfit):N2}"
                },
                Columns = new[]
                {
                    Column("Code", 2.1), Column("Item / Service", 4.5), Column("Type", 1.7),
                    Column("Sold", 1.5, true), Column("Returned", 1.6, true), Column("Net Qty", 1.5, true),
                    Column("Net Sales", 2.1, true), Column("Profit", 1.9, true), Column("Stock", 1.5, true)
                },
                Rows = rows.Select(row => (IReadOnlyList<string?>)new string?[]
                {
                    FirstNonEmpty(row.SkuCode, row.ItemCode), row.ItemName, row.ItemType,
                    Quantity(row.SoldQuantity), Quantity(row.ReturnedQuantity), Quantity(row.NetQuantity),
                    Money(row.NetSales), Money(row.GrossProfit), row.CurrentStock.HasValue ? Quantity(row.CurrentStock.Value) : "N/A"
                }).ToList()
            };
        }

        public string BuildItemSalesCsv(IReadOnlyCollection<ItemPerformanceDto> rows)
        {
            EnsureRows(rows, "item sales");
            return _csv.Format(
                new[] { "Rank", "ItemCode", "SkuCode", "ItemName", "ItemType", "Category", "SoldQty", "ReturnedQty", "NetQty", "GrossSales", "Discounts", "ReturnValue", "NetSales", "NetCost", "GrossProfit", "MarginPercent", "CurrentStock", "LastSaleDate" },
                rows.Select(row => (IReadOnlyList<string?>)new string?[]
                {
                    row.Rank.ToString(CultureInfo.InvariantCulture), row.ItemCode, row.SkuCode, row.ItemName, row.ItemType, row.CategoryName,
                    Quantity(row.SoldQuantity), Quantity(row.ReturnedQuantity), Quantity(row.NetQuantity), Money(row.GrossSales), Money(row.Discounts),
                    Money(row.ReturnValue), Money(row.NetSales), Money(row.NetCost), Money(row.GrossProfit), row.MarginPercent.ToString("0.00", CultureInfo.InvariantCulture),
                    row.CurrentStock?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty,
                    row.LastSaleDate?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty
                }));
        }

        public PdfTableDocumentDto BuildSupplierSummary(
            IReadOnlyCollection<SupplierOutstandingSummaryDto> outstanding,
            IReadOnlyCollection<SupplierPurchaseVolumeDto> purchases,
            IReadOnlyCollection<SupplierReturnSummaryDto> returns,
            DateTime startDate,
            DateTime endDate,
            string storeHeading,
            string generatedBy)
        {
            if (outstanding.Count == 0 && purchases.Count == 0 && returns.Count == 0)
                throw new InvalidOperationException("There are no supplier report rows to export.");

            var purchaseMap = purchases.ToDictionary(row => row.SupplierId);
            var returnMap = returns.ToDictionary(row => row.SupplierId);
            var supplierIds = outstanding.Select(row => row.SupplierId)
                .Concat(purchases.Select(row => row.SupplierId))
                .Concat(returns.Select(row => row.SupplierId))
                .Distinct()
                .ToList();
            var outstandingMap = outstanding.ToDictionary(row => row.SupplierId);

            return new PdfTableDocumentDto
            {
                Title = "SUPPLIER SUMMARY",
                Subtitle = $"Purchasing and returns: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                StoreHeading = storeHeading,
                GeneratedBy = generatedBy,
                FooterText = "Outstanding balance is derived from the supplier ledger; period purchases and returns use posted documents.",
                SummaryLines = new[]
                {
                    $"Outstanding: Rs. {outstanding.Where(row => row.NetOutstanding > 0m).Sum(row => row.NetOutstanding):N2}",
                    $"Purchases: Rs. {purchases.Sum(row => row.TotalGrnValue):N2}    Supplier returns: Rs. {returns.Sum(row => row.NetSupplierCredit):N2}"
                },
                Columns = new[]
                {
                    Column("Supplier", 5.0), Column("Outstanding", 2.4, true), Column("Purchases", 2.4, true),
                    Column("GRNs", 1.3, true), Column("Returns", 2.3, true), Column("Return Qty", 1.8, true), Column("Last Activity", 2.3)
                },
                Rows = supplierIds.Select(id =>
                {
                    outstandingMap.TryGetValue(id, out SupplierOutstandingSummaryDto? debt);
                    purchaseMap.TryGetValue(id, out SupplierPurchaseVolumeDto? purchase);
                    returnMap.TryGetValue(id, out SupplierReturnSummaryDto? returned);
                    DateTime? last = new[] { debt?.LastTransactionDate, purchase?.LastGrnDate, returned?.LastReturnDate }
                        .Where(value => value.HasValue)
                        .Select(value => value!.Value)
                        .DefaultIfEmpty()
                        .Max();
                    if (last == default)
                        last = null;
                    return (IReadOnlyList<string?>)new string?[]
                    {
                        FirstNonEmpty(debt?.SupplierDisplayName, purchase?.SupplierDisplayName, returned?.SupplierDisplayName, $"Supplier #{id}"),
                        Money(debt?.NetOutstanding ?? 0m), Money(purchase?.TotalGrnValue ?? 0m),
                        (purchase?.GrnCount ?? 0).ToString(CultureInfo.InvariantCulture), Money(returned?.NetSupplierCredit ?? 0m),
                        Quantity(returned?.TotalReturnedQty ?? 0m), last?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty
                    };
                }).OrderBy(row => row[0]).ToList()
            };
        }

        public string BuildSupplierSummaryCsv(
            IReadOnlyCollection<SupplierOutstandingSummaryDto> outstanding,
            IReadOnlyCollection<SupplierPurchaseVolumeDto> purchases,
            IReadOnlyCollection<SupplierReturnSummaryDto> returns)
        {
            if (outstanding.Count == 0 && purchases.Count == 0 && returns.Count == 0)
                throw new InvalidOperationException("There are no supplier report rows to export.");

            var purchaseMap = purchases.ToDictionary(row => row.SupplierId);
            var returnMap = returns.ToDictionary(row => row.SupplierId);
            var outstandingMap = outstanding.ToDictionary(row => row.SupplierId);
            int[] ids = outstanding.Select(row => row.SupplierId)
                .Concat(purchases.Select(row => row.SupplierId))
                .Concat(returns.Select(row => row.SupplierId)).Distinct().ToArray();

            return _csv.Format(
                new[] { "SupplierCode", "SupplierName", "CompanyName", "TotalGrnBilled", "SupplierReturnsLedger", "TotalPaid", "NetOutstanding", "PeriodGrnCount", "PeriodPurchases", "PeriodReturnCount", "PeriodReturnedQty", "PeriodSupplierCredit" },
                ids.Select(id =>
                {
                    outstandingMap.TryGetValue(id, out SupplierOutstandingSummaryDto? debt);
                    purchaseMap.TryGetValue(id, out SupplierPurchaseVolumeDto? purchase);
                    returnMap.TryGetValue(id, out SupplierReturnSummaryDto? returned);
                    return (IReadOnlyList<string?>)new string?[]
                    {
                        FirstNonEmpty(debt?.SupplierCode, purchase?.SupplierCode, returned?.SupplierCode),
                        FirstNonEmpty(debt?.SupplierName, purchase?.SupplierName, returned?.SupplierName),
                        FirstNonEmpty(debt?.CompanyName, purchase?.CompanyName, returned?.CompanyName),
                        Money(debt?.TotalGrnBilled ?? 0m), Money(debt?.TotalSupplierReturns ?? 0m), Money(debt?.TotalPaid ?? 0m), Money(debt?.NetOutstanding ?? 0m),
                        (purchase?.GrnCount ?? 0).ToString(CultureInfo.InvariantCulture), Money(purchase?.TotalGrnValue ?? 0m),
                        (returned?.ReturnDocumentCount ?? 0).ToString(CultureInfo.InvariantCulture), Quantity(returned?.TotalReturnedQty ?? 0m), Money(returned?.NetSupplierCredit ?? 0m)
                    };
                }));
        }

        public PdfTableDocumentDto BuildVatSummary(
            VatReportSummaryDto summary,
            IReadOnlyCollection<VatCategoryRateRowDto> categoryRows,
            DateTime startDate,
            DateTime endDate,
            string generatedBy)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            var rows = new List<IReadOnlyList<string?>>
            {
                Row("Gross output VAT", summary.GrossOutputVat),
                Row("Customer return VAT", -summary.CustomerReturnVat),
                Row("Net output VAT", summary.NetOutputVat),
                Row("Gross input VAT", summary.GrossInputVat),
                Row("Supplier return VAT", -summary.SupplierReturnVat),
                Row("Net input VAT", summary.NetInputVat),
                Row("Operational VAT position", summary.OperationalVatPosition),
                Row("Sales zero rated", summary.SalesZeroRatedAmount),
                Row("Sales exempt", summary.SalesExemptAmount),
                Row("Sales out of scope", summary.SalesOutOfScopeAmount),
                Row("Purchase zero rated", summary.PurchaseZeroRatedAmount),
                Row("Purchase exempt", summary.PurchaseExemptAmount),
                Row("Purchase out of scope", summary.PurchaseOutOfScopeAmount)
            };

            foreach (VatCategoryRateRowDto category in categoryRows)
            {
                rows.Add(new[]
                {
                    $"{category.SourceType} / {category.TaxCategoryCode} / {category.VatRateDisplay}",
                    $"Taxable {category.NetTaxableEffect:N2}; VAT {category.NetVatEffect:N2}"
                });
            }

            return new PdfTableDocumentDto
            {
                Title = "VAT OPERATIONAL SUMMARY",
                Subtitle = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                StoreHeading = $"{summary.StoreName} | {summary.CurrentRegistrationText}",
                GeneratedBy = generatedBy,
                FooterText = "Operational VAT report based on immutable transaction snapshots; consult a tax professional for statutory filing.",
                SummaryLines = new[]
                {
                    $"Complete documents: {summary.CompleteDocumentCount:N0}    Legacy/unknown: {summary.LegacyUnknownDocumentCount:N0}    Discrepancies: {summary.DiscrepancyCount:N0}",
                    summary.PositionLabel
                },
                Columns = new[] { Column("Measure / Category", 11.5), Column("Value (Rs.)", 4.4, true) },
                Rows = rows
            };
        }

        public string BuildVatDetailsCsv(
            IReadOnlyCollection<VatReportDocumentRowDto> documents,
            IReadOnlyCollection<VatReconciliationRowDto> reconciliation)
        {
            if (documents.Count == 0 && reconciliation.Count == 0)
                throw new InvalidOperationException("There are no VAT rows to export.");

            var rows = documents.Select(document => (IReadOnlyList<string?>)new string?[]
            {
                "Document", document.SourceType, document.DocumentNumber, document.RelatedDocumentNumber,
                document.DocumentDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), document.Direction,
                Money(document.NetTaxableEffect), Money(document.NetVatEffect), Money(document.InclusiveAmount),
                document.SnapshotStatus, string.Empty, string.Empty, string.Empty
            }).Concat(reconciliation.Select(row => (IReadOnlyList<string?>)new string?[]
            {
                "Reconciliation", row.SourceType, row.DocumentNumber, string.Empty,
                row.DocumentDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), row.FieldChecked,
                Money(row.HeaderOrSourceAmount), Money(row.DetailOrReturnedAmount), Money(row.Difference),
                string.Empty, row.Explanation, string.Empty, string.Empty
            }));

            return _csv.Format(
                new[] { "RowType", "SourceType", "DocumentNumber", "RelatedDocument", "Date", "DirectionOrField", "TaxableOrSource", "VatOrDetail", "InclusiveOrDifference", "SnapshotStatus", "Explanation", "Reserved1", "Reserved2" },
                rows);
        }

        public PdfTableDocumentDto BuildSupplierClaimStatement(
            IReadOnlyCollection<FreeItemClaimSearchDto> claims,
            DateTime? startDate,
            DateTime? endDate,
            string generatedBy)
        {
            EnsureRows(claims, "supplier claims");
            return new PdfTableDocumentDto
            {
                Title = "SUPPLIER CLAIM STATEMENT",
                Subtitle = $"{(startDate ?? DateTime.Today.AddDays(-30)):yyyy-MM-dd} to {(endDate ?? DateTime.Today):yyyy-MM-dd}",
                GeneratedBy = generatedBy,
                FooterText = "Free Issue supplier recoveries, net of recorded customer-return adjustments.",
                SummaryLines = new[]
                {
                    $"Claims: {claims.Count:N0}    Original qty: {claims.Sum(row => row.Quantity):N3}    Returned qty: {claims.Sum(row => row.ReturnedQuantity):N3}",
                    $"Original value: Rs. {claims.Sum(row => row.ClaimValue):N2}    Reductions: Rs. {claims.Sum(row => row.ClaimValueReduction):N2}    Net: Rs. {claims.Sum(row => row.NetClaimValue):N2}"
                },
                Columns = new[]
                {
                    Column("Supplier", 3.6), Column("Claim Ref", 2.5), Column("Invoice", 2.2), Column("Item", 4.0),
                    Column("Net Qty", 1.5, true), Column("Net Value", 2.0, true), Column("Status", 1.7)
                },
                Rows = claims.Select(row => (IReadOnlyList<string?>)new string?[]
                {
                    row.SupplierName, FirstNonEmpty(row.ClaimReferenceNo, row.SupplierPromotionReference), row.InvoiceNo,
                    FirstNonEmpty(row.SkuCode, row.ItemDescription), Quantity(row.NetClaimQuantity), Money(row.NetClaimValue), row.ClaimStatus
                }).ToList()
            };
        }

        public string BuildStockBalanceCsv(IReadOnlyCollection<StockBalanceDto> rows)
        {
            EnsureRows(rows, "stock balance");
            return _csv.Format(
                new[] { "ItemCode", "SkuCode", "Barcode", "Description", "Variant", "Category", "Supplier", "Uom", "Tracking", "QtyOnHand", "UnitCost", "UnitRetail", "UnitWholesale", "CostValue", "RetailValue", "WholesaleValue", "PotentialGrossProfit", "StockStatus", "EarliestExpiry", "LastReceived" },
                rows.Select(row => (IReadOnlyList<string?>)new string?[]
                {
                    row.ItemCode, row.SkuCode, row.Barcode, row.Description, row.VariantDescription, row.CategoryName, row.PrimarySupplierName,
                    row.Uom, row.TrackingText, Quantity(row.TotalQtyOnHand), Money(row.UnitCost), Money(row.UnitRetail), Money(row.UnitWholesale),
                    Money(row.TotalCostValue), Money(row.TotalRetailValue), Money(row.TotalWholesaleValue), Money(row.PotentialGrossProfit), row.StockStatus,
                    row.EarliestExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    row.LastReceivedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty
                }));
        }

        public string BuildExpiryMonitorCsv(
            IReadOnlyCollection<ExpiryMonitorRowDto> rows)
        {
            EnsureRows(rows, "expiry monitor");

            return _csv.Format(
                new[]
                {
                    "ExpiryStatus", "DaysRemaining", "ExpiryDate", "ItemCode",
                    "SkuCode", "ItemBarcode", "Description", "Variant", "BatchNo",
                    "BatchBarcode", "Category", "Supplier", "Uom", "AvailableQty",
                    "UnitCost", "CostValue", "ReceivedDate"
                },
                rows.Select(row => (IReadOnlyList<string?>)new string?[]
                {
                    row.ExpiryStatus,
                    row.DaysRemaining?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    row.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    row.ItemCode,
                    row.SkuCode,
                    row.ItemBarcode,
                    row.Description,
                    row.VariantDescription,
                    row.BatchNo,
                    row.BatchBarcode,
                    row.CategoryName,
                    row.PrimarySupplierName,
                    row.Uom,
                    Quantity(row.AvailableQty),
                    Money(row.UnitCost),
                    Money(row.CostValue),
                    row.ReceivedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }));
        }

        private static PdfTableColumnDto Column(string header, double width, bool numeric = false) =>
            new() { Header = header, WidthCentimeters = width, IsNumeric = numeric };

        private static IReadOnlyList<string?> Row(string label, decimal amount) =>
            new[] { label, Money(amount) };

        public static string StoreHeading(StoreSettings settings)
        {
            string address = string.Join(", ", new[] { settings.AddressLine1, settings.AddressLine2, settings.City }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            string identity = FirstNonEmpty(settings.StoreName, settings.LegalName, "Store");
            return string.IsNullOrWhiteSpace(address) ? identity : $"{identity} | {address}";
        }

        private static void EnsureRows<T>(IReadOnlyCollection<T> rows, string name)
        {
            if (rows == null || rows.Count == 0)
                throw new InvalidOperationException($"There are no {name} rows to export.");
        }

        private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Quantity(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
