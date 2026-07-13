using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    /// <summary>
    /// Read-only operational VAT reporting over immutable transaction snapshots.
    /// This repository never resolves current Tax Master values for historical rows.
    /// </summary>
    public sealed class VatReportRepository
    {
        private const decimal DifferenceTolerance = 0.01m;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public VatReportRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<VatReportResultDto> GetReportAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            DateTime from = startDate.Date;
            DateTime toExclusive = endDate.Date.AddDays(1);

            if (from >= toExclusive)
                throw new ArgumentException("Start date cannot be later than end date.");

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync(cancellationToken);

            StoreSettings? store = await context.StoreSettings
                .AsNoTracking()
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            List<SalesHeader> sales = await context.SalesHeaders
                .AsNoTracking()
                .Where(s =>
                    s.TransactionDate >= from &&
                    s.TransactionDate < toExclusive &&
                    s.Status == "Completed" &&
                    !s.IsVoided)
                .Include(s => s.SalesLines)
                .OrderBy(s => s.TransactionDate)
                .ThenBy(s => s.InvoiceNo)
                .ToListAsync(cancellationToken);

            List<CustomerReturnHeader> customerReturns =
                await context.CustomerReturnHeaders
                    .AsNoTracking()
                    .Where(r =>
                        r.ReturnDate >= from &&
                        r.ReturnDate < toExclusive)
                    .Include(r => r.Lines)
                        .ThenInclude(l => l.SalesLine)
                    .OrderBy(r => r.ReturnDate)
                    .ThenBy(r => r.ReturnNo)
                    .ToListAsync(cancellationToken);

            List<GrnHeader> grns = await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.InvoiceDate >= from &&
                    g.InvoiceDate < toExclusive &&
                    g.Status == "Posted")
                .Include(g => g.GrnLines)
                .OrderBy(g => g.InvoiceDate)
                .ThenBy(g => g.GrnNumber)
                .ToListAsync(cancellationToken);

            List<SupplierReturnHeader> supplierReturns =
                await context.SupplierReturnHeaders
                    .AsNoTracking()
                    .Where(r =>
                        r.ReturnDate >= from &&
                        r.ReturnDate < toExclusive &&
                        r.Status == "Posted")
                    .Include(r => r.ReturnLines)
                        .ThenInclude(l => l.GrnLine)
                    .OrderBy(r => r.ReturnDate)
                    .ThenBy(r => r.ReturnNumber)
                    .ToListAsync(cancellationToken);

            var result = new VatReportResultDto
            {
                StartDate = from,
                EndDate = endDate.Date,
                Summary = new VatReportSummaryDto
                {
                    StoreName = FirstNonBlank(store?.StoreName, store?.LegalName, "Store"),
                    IsCurrentlyVatRegistered = store?.IsVatRegistered ?? false,
                    VatRegistrationNumber = store?.VatRegistrationNumber?.Trim() ?? string.Empty
                }
            };

            var categoryAccumulators =
                new Dictionary<CategoryKey, CategoryAccumulator>();

            foreach (SalesHeader sale in sales)
            {
                if (!sale.SalesLines.Any(line => !line.IsGiftVoucherSale))
                    continue;

                decimal merchandiseInclusive = Money(
                    sale.NetTotal - sale.GiftVoucherIssueTotal);

                if (!IsCompleteSale(sale))
                {
                    AddLegacy(
                        result,
                        "Sale",
                        sale.InvoiceNo,
                        string.Empty,
                        sale.TransactionDate,
                        merchandiseInclusive,
                        sale.TaxSnapshotStatus,
                        BuildIncompleteWarning(sale.TaxSnapshotStatus, "sale"));
                    continue;
                }

                AddDocument(
                    result,
                    "Sale",
                    sale.InvoiceNo,
                    string.Empty,
                    sale.TransactionDate,
                    isReversal: false,
                    sale.TaxableAmountTotal!.Value,
                    sale.TotalVatAmount!.Value,
                    merchandiseInclusive,
                    sale.StandardRatedAmount!.Value,
                    sale.ZeroRatedAmount!.Value,
                    sale.ExemptAmount!.Value,
                    sale.OutOfScopeAmount!.Value,
                    sale.TaxSnapshotStatus);

                AddCategoryLines(
                    categoryAccumulators,
                    "Sale",
                    sale.InvoiceNo,
                    isReversal: false,
                    sale.SalesLines
                        .Where(l => !l.IsGiftVoucherSale)
                        .Select(l => new SnapshotLine(
                        l.TaxCategoryCodeSnapshot,
                        l.TaxRatePercentSnapshot,
                        l.TaxableAmountSnapshot,
                        l.VatAmountSnapshot,
                        l.TaxInclusiveAmountSnapshot)));
            }

            foreach (CustomerReturnHeader customerReturn in customerReturns)
            {
                if (!IsCompleteCustomerReturn(customerReturn))
                {
                    AddLegacy(
                        result,
                        "Customer Credit Note",
                        FirstNonBlank(customerReturn.CreditNoteNo, customerReturn.ReturnNo, "-"),
                        customerReturn.OriginalInvoiceNo ?? string.Empty,
                        customerReturn.ReturnDate,
                        customerReturn.TotalRefundAmount,
                        customerReturn.TaxSnapshotStatus,
                        BuildIncompleteWarning(customerReturn.TaxSnapshotStatus, "customer return"));
                    continue;
                }

                string number = FirstNonBlank(
                    customerReturn.CreditNoteNo,
                    customerReturn.ReturnNo,
                    "-");

                AddDocument(
                    result,
                    "Customer Credit Note",
                    number,
                    customerReturn.OriginalInvoiceNo ?? string.Empty,
                    customerReturn.ReturnDate,
                    isReversal: true,
                    customerReturn.TaxableAmountTotal!.Value,
                    customerReturn.TotalVatAmount!.Value,
                    customerReturn.TotalRefundAmount,
                    customerReturn.StandardRatedAmount!.Value,
                    customerReturn.ZeroRatedAmount!.Value,
                    customerReturn.ExemptAmount!.Value,
                    customerReturn.OutOfScopeAmount!.Value,
                    customerReturn.TaxSnapshotStatus);

                AddCategoryLines(
                    categoryAccumulators,
                    "Customer Credit Note",
                    number,
                    isReversal: true,
                    customerReturn.Lines.Select(l => new SnapshotLine(
                        l.TaxCategoryCodeSnapshot,
                        l.TaxRatePercentSnapshot,
                        l.TaxableAmountSnapshot,
                        l.VatAmountSnapshot,
                        l.TaxInclusiveAmountSnapshot)));
            }

            foreach (GrnHeader grn in grns)
            {
                if (!IsCompleteGrn(grn))
                {
                    AddLegacy(
                        result,
                        "GRN Purchase",
                        grn.GrnNumber,
                        grn.SupplierInvoiceNo,
                        grn.InvoiceDate,
                        grn.NetPayable,
                        grn.TaxSnapshotStatus,
                        BuildIncompleteWarning(grn.TaxSnapshotStatus, "GRN"));
                }
                else
                {
                    decimal productInclusive = Money(
                        grn.GrnLines.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m));

                    AddDocument(
                        result,
                        "GRN Purchase",
                        grn.GrnNumber,
                        grn.SupplierInvoiceNo,
                        grn.InvoiceDate,
                        isReversal: false,
                        grn.TaxableAmountTotal!.Value,
                        grn.TotalVatAmount,
                        productInclusive,
                        grn.StandardRatedAmount!.Value,
                        grn.ZeroRatedAmount!.Value,
                        grn.ExemptAmount!.Value,
                        grn.OutOfScopeAmount!.Value,
                        grn.TaxSnapshotStatus);

                    AddCategoryLines(
                        categoryAccumulators,
                        "GRN Purchase",
                        grn.GrnNumber,
                        isReversal: false,
                        grn.GrnLines.Select(l => new SnapshotLine(
                            l.TaxCategoryCodeSnapshot,
                            l.TaxRatePercentSnapshot,
                            l.TaxableAmountSnapshot,
                            l.VatAmountSnapshot,
                            l.TaxInclusiveAmountSnapshot)));
                }

                if (grn.FreightAmount != 0m &&
                    !IsCompleteStatus(grn.FreightTaxSnapshotStatus))
                {
                    AddLegacy(
                        result,
                        "GRN Freight",
                        grn.GrnNumber,
                        grn.SupplierInvoiceNo,
                        grn.InvoiceDate,
                        grn.FreightAmount,
                        grn.FreightTaxSnapshotStatus,
                        "Freight VAT treatment is unclassified. No VAT was inferred from the current rate.");
                }
            }

            foreach (SupplierReturnHeader supplierReturn in supplierReturns)
            {
                if (!IsCompleteSupplierReturn(supplierReturn))
                {
                    AddLegacy(
                        result,
                        "Supplier Debit Note",
                        supplierReturn.ReturnNumber,
                        supplierReturn.OriginalInvoiceNo,
                        supplierReturn.ReturnDate,
                        supplierReturn.NetCredit,
                        supplierReturn.TaxSnapshotStatus,
                        BuildIncompleteWarning(supplierReturn.TaxSnapshotStatus, "supplier return"));
                    continue;
                }

                decimal returnInclusive = Money(
                    supplierReturn.ReturnLines.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m));

                AddDocument(
                    result,
                    "Supplier Debit Note",
                    supplierReturn.ReturnNumber,
                    supplierReturn.OriginalInvoiceNo,
                    supplierReturn.ReturnDate,
                    isReversal: true,
                    supplierReturn.TaxableAmountTotal!.Value,
                    supplierReturn.TotalVatAmount!.Value,
                    returnInclusive,
                    supplierReturn.StandardRatedAmount!.Value,
                    supplierReturn.ZeroRatedAmount!.Value,
                    supplierReturn.ExemptAmount!.Value,
                    supplierReturn.OutOfScopeAmount!.Value,
                    supplierReturn.TaxSnapshotStatus);

                AddCategoryLines(
                    categoryAccumulators,
                    "Supplier Debit Note",
                    supplierReturn.ReturnNumber,
                    isReversal: true,
                    supplierReturn.ReturnLines.Select(l => new SnapshotLine(
                        l.TaxCategoryCodeSnapshot,
                        l.TaxRatePercentSnapshot,
                        l.TaxableAmountSnapshot,
                        l.VatAmountSnapshot,
                        l.TaxInclusiveAmountSnapshot)));
            }

            result.CategoryRateRows = categoryAccumulators
                .Values
                .Select(a => a.ToDto())
                .OrderBy(r => r.SourceType)
                .ThenBy(r => r.TaxCategoryCode)
                .ThenBy(r => r.VatRatePercent)
                .ToList();

            await BuildReconciliationAsync(
                context,
                result,
                sales,
                customerReturns,
                grns,
                supplierReturns,
                cancellationToken);

            BuildSummary(result);
            return result;
        }

        private static void BuildSummary(VatReportResultDto result)
        {
            List<VatReportDocumentRowDto> sales = result.Documents
                .Where(d => d.SourceType == "Sale")
                .ToList();
            List<VatReportDocumentRowDto> customerReturns = result.Documents
                .Where(d => d.SourceType == "Customer Credit Note")
                .ToList();
            List<VatReportDocumentRowDto> purchases = result.Documents
                .Where(d => d.SourceType == "GRN Purchase")
                .ToList();
            List<VatReportDocumentRowDto> supplierReturns = result.Documents
                .Where(d => d.SourceType == "Supplier Debit Note")
                .ToList();

            VatReportSummaryDto summary = result.Summary;
            summary.GrossSalesTaxableAmount = Money(sales.Sum(d => d.TaxableAmount));
            summary.CustomerReturnTaxableAmount = Money(customerReturns.Sum(d => d.TaxableAmount));
            summary.NetSalesTaxableAmount = Money(
                summary.GrossSalesTaxableAmount - summary.CustomerReturnTaxableAmount);
            summary.GrossOutputVat = Money(sales.Sum(d => d.VatAmount));
            summary.CustomerReturnVat = Money(customerReturns.Sum(d => d.VatAmount));
            summary.NetOutputVat = Money(summary.GrossOutputVat - summary.CustomerReturnVat);

            summary.GrossPurchaseTaxableAmount = Money(purchases.Sum(d => d.TaxableAmount));
            summary.SupplierReturnTaxableAmount = Money(supplierReturns.Sum(d => d.TaxableAmount));
            summary.NetPurchaseTaxableAmount = Money(
                summary.GrossPurchaseTaxableAmount - summary.SupplierReturnTaxableAmount);
            summary.GrossInputVat = Money(purchases.Sum(d => d.VatAmount));
            summary.SupplierReturnVat = Money(supplierReturns.Sum(d => d.VatAmount));
            summary.NetInputVat = Money(summary.GrossInputVat - summary.SupplierReturnVat);

            summary.OperationalVatPosition = Money(
                summary.NetOutputVat - summary.NetInputVat);
            summary.CompleteDocumentCount = result.Documents.Count;
            summary.LegacyUnknownDocumentCount = result.LegacyUnknownRows.Count;
            summary.DiscrepancyCount = result.ReconciliationRows.Count;

            summary.SalesZeroRatedAmount = Money(
                sales.Sum(d => d.ZeroRatedAmount) -
                customerReturns.Sum(d => d.ZeroRatedAmount));
            summary.SalesExemptAmount = Money(
                sales.Sum(d => d.ExemptAmount) -
                customerReturns.Sum(d => d.ExemptAmount));
            summary.SalesOutOfScopeAmount = Money(
                sales.Sum(d => d.OutOfScopeAmount) -
                customerReturns.Sum(d => d.OutOfScopeAmount));
            summary.PurchaseZeroRatedAmount = Money(
                purchases.Sum(d => d.ZeroRatedAmount) -
                supplierReturns.Sum(d => d.ZeroRatedAmount));
            summary.PurchaseExemptAmount = Money(
                purchases.Sum(d => d.ExemptAmount) -
                supplierReturns.Sum(d => d.ExemptAmount));
            summary.PurchaseOutOfScopeAmount = Money(
                purchases.Sum(d => d.OutOfScopeAmount) -
                supplierReturns.Sum(d => d.OutOfScopeAmount));
        }

        private static async Task BuildReconciliationAsync(
            AppDbContext context,
            VatReportResultDto result,
            IReadOnlyCollection<SalesHeader> sales,
            IReadOnlyCollection<CustomerReturnHeader> customerReturns,
            IReadOnlyCollection<GrnHeader> grns,
            IReadOnlyCollection<SupplierReturnHeader> supplierReturns,
            CancellationToken cancellationToken)
        {
            foreach (SalesHeader sale in sales.Where(s =>
                         IsCompleteStatus(s.TaxSnapshotStatus) &&
                         s.SalesLines.Any(line => !line.IsGiftVoucherSale)))
            {
                List<SalesLine> merchandiseLines = sale.SalesLines
                    .Where(line => !line.IsGiftVoucherSale)
                    .ToList();
                decimal lineTaxable = Money(merchandiseLines.Sum(l => l.TaxableAmountSnapshot ?? 0m));
                decimal lineVat = Money(merchandiseLines.Sum(l => l.VatAmountSnapshot ?? 0m));
                decimal lineInclusive = Money(merchandiseLines.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m));

                AddDifference(result, "Sale", sale.InvoiceNo, sale.TransactionDate,
                    "Taxable total", sale.TaxableAmountTotal ?? 0m, lineTaxable,
                    "Header taxable value does not equal saved line snapshots.");
                AddDifference(result, "Sale", sale.InvoiceNo, sale.TransactionDate,
                    "VAT total", sale.TotalVatAmount ?? 0m, lineVat,
                    "Header VAT does not equal saved line snapshots.");
                AddDifference(result, "Sale", sale.InvoiceNo, sale.TransactionDate,
                    "Inclusive/net total", Money(sale.NetTotal - sale.GiftVoucherIssueTotal), lineInclusive,
                    "Sale net total does not equal saved line inclusive values.");
                AddCategoryDifferences(result, "Sale", sale.InvoiceNo, sale.TransactionDate,
                    sale.StandardRatedAmount, sale.ZeroRatedAmount, sale.ExemptAmount,
                    sale.OutOfScopeAmount,
                    merchandiseLines.Select(ToSnapshotLine));
            }

            foreach (GrnHeader grn in grns.Where(g => IsCompleteStatus(g.TaxSnapshotStatus)))
            {
                decimal lineTaxable = Money(grn.GrnLines.Sum(l => l.TaxableAmountSnapshot ?? 0m));
                decimal lineVat = Money(grn.GrnLines.Sum(l => l.VatAmountSnapshot ?? 0m));
                decimal lineInclusive = Money(grn.GrnLines.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m));
                decimal expectedPayable = Money(lineInclusive + grn.FreightAmount);

                AddDifference(result, "GRN Purchase", grn.GrnNumber, grn.InvoiceDate,
                    "Taxable total", grn.TaxableAmountTotal ?? 0m, lineTaxable,
                    "GRN header taxable value does not equal saved product-line snapshots.");
                AddDifference(result, "GRN Purchase", grn.GrnNumber, grn.InvoiceDate,
                    "VAT total", grn.TotalVatAmount, lineVat,
                    "GRN header product VAT does not equal saved line snapshots.");
                AddDifference(result, "GRN Purchase", grn.GrnNumber, grn.InvoiceDate,
                    "Net payable", grn.NetPayable, expectedPayable,
                    "GRN payable does not equal product inclusive total plus saved freight.");
                AddCategoryDifferences(result, "GRN Purchase", grn.GrnNumber, grn.InvoiceDate,
                    grn.StandardRatedAmount, grn.ZeroRatedAmount, grn.ExemptAmount,
                    grn.OutOfScopeAmount,
                    grn.GrnLines.Select(ToSnapshotLine));
            }

            foreach (CustomerReturnHeader customerReturn in customerReturns
                .Where(r => IsCompleteStatus(r.TaxSnapshotStatus)))
            {
                string number = FirstNonBlank(customerReturn.CreditNoteNo, customerReturn.ReturnNo, "-");
                decimal lineTaxable = Money(customerReturn.Lines.Sum(l => l.TaxableAmountSnapshot ?? 0m));
                decimal lineVat = Money(customerReturn.Lines.Sum(l => l.VatAmountSnapshot ?? 0m));
                decimal lineInclusive = Money(customerReturn.Lines.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m));

                AddDifference(result, "Customer Credit Note", number, customerReturn.ReturnDate,
                    "Taxable total", customerReturn.TaxableAmountTotal ?? 0m, lineTaxable,
                    "Credit Note header taxable value does not equal saved return-line snapshots.");
                AddDifference(result, "Customer Credit Note", number, customerReturn.ReturnDate,
                    "VAT total", customerReturn.TotalVatAmount ?? 0m, lineVat,
                    "Credit Note header VAT does not equal saved return-line snapshots.");
                AddDifference(result, "Customer Credit Note", number, customerReturn.ReturnDate,
                    "Refund total", customerReturn.TotalRefundAmount, lineInclusive,
                    "Credit Note refund does not equal saved return-line inclusive values.");
                AddCategoryDifferences(result, "Customer Credit Note", number, customerReturn.ReturnDate,
                    customerReturn.StandardRatedAmount, customerReturn.ZeroRatedAmount,
                    customerReturn.ExemptAmount, customerReturn.OutOfScopeAmount,
                    customerReturn.Lines.Select(ToSnapshotLine));
            }

            foreach (SupplierReturnHeader supplierReturn in supplierReturns
                .Where(r => IsCompleteStatus(r.TaxSnapshotStatus)))
            {
                decimal lineTaxable = Money(supplierReturn.ReturnLines.Sum(l => l.TaxableAmountSnapshot ?? 0m));
                decimal lineVat = Money(supplierReturn.ReturnLines.Sum(l => l.VatAmountSnapshot ?? 0m));
                decimal lineInclusive = Money(supplierReturn.ReturnLines.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m));

                AddDifference(result, "Supplier Debit Note", supplierReturn.ReturnNumber,
                    supplierReturn.ReturnDate, "Taxable total",
                    supplierReturn.TaxableAmountTotal ?? 0m, lineTaxable,
                    "Debit Note header taxable value does not equal saved return-line snapshots.");
                AddDifference(result, "Supplier Debit Note", supplierReturn.ReturnNumber,
                    supplierReturn.ReturnDate, "VAT total",
                    supplierReturn.TotalVatAmount ?? 0m, lineVat,
                    "Debit Note header VAT does not equal saved return-line snapshots.");
                AddDifference(result, "Supplier Debit Note", supplierReturn.ReturnNumber,
                    supplierReturn.ReturnDate, "Supplier credit",
                    supplierReturn.NetCredit, lineInclusive,
                    "Supplier credit does not equal saved return-line inclusive values.");
                AddCategoryDifferences(result, "Supplier Debit Note", supplierReturn.ReturnNumber,
                    supplierReturn.ReturnDate, supplierReturn.StandardRatedAmount,
                    supplierReturn.ZeroRatedAmount, supplierReturn.ExemptAmount,
                    supplierReturn.OutOfScopeAmount,
                    supplierReturn.ReturnLines.Select(ToSnapshotLine));
            }

            HashSet<int> periodSalesLineIds = customerReturns
                .SelectMany(r => r.Lines)
                .Where(l => l.SalesLineId.HasValue)
                .Select(l => l.SalesLineId!.Value)
                .ToHashSet();

            if (periodSalesLineIds.Count > 0)
            {
                List<CustomerReturnLine> allCustomerReturnLines =
                    await context.CustomerReturnLines
                        .AsNoTracking()
                        .Where(l => l.SalesLineId.HasValue &&
                            periodSalesLineIds.Contains(l.SalesLineId.Value))
                        .Include(l => l.SalesLine)
                        .Include(l => l.CustomerReturnHeader)
                        .ToListAsync(cancellationToken);

                foreach (IGrouping<int, CustomerReturnLine> group in
                    allCustomerReturnLines.GroupBy(l => l.SalesLineId!.Value))
                {
                    SalesLine? source = group.Select(l => l.SalesLine).FirstOrDefault(l => l != null);
                    if (source == null)
                        continue;

                    CustomerReturnLine first = group.First();
                    DateTime date = first.CustomerReturnHeader?.ReturnDate ?? DateTime.MinValue;
                    string number = FirstNonBlank(
                        first.CustomerReturnHeader?.CreditNoteNo,
                        first.CustomerReturnHeader?.ReturnNo,
                        source.SalesHeaderId.ToString());

                    AddExcess(result, "Customer Return vs Sale", number, date,
                        "Cumulative returned quantity", source.Quantity,
                        group.Sum(l => l.QuantityReturned),
                        "Cumulative returned quantity exceeds the original sale line.");
                    AddExcessNullable(result, "Customer Return vs Sale", number, date,
                        "Cumulative returned taxable", source.TaxableAmountSnapshot,
                        group.Sum(l => l.TaxableAmountSnapshot ?? 0m),
                        "Cumulative returned taxable value exceeds the original sale snapshot.");
                    AddExcessNullable(result, "Customer Return vs Sale", number, date,
                        "Cumulative returned VAT", source.VatAmountSnapshot,
                        group.Sum(l => l.VatAmountSnapshot ?? 0m),
                        "Cumulative returned VAT exceeds the original sale snapshot.");
                    AddExcessNullable(result, "Customer Return vs Sale", number, date,
                        "Cumulative returned inclusive", source.TaxInclusiveAmountSnapshot,
                        group.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m),
                        "Cumulative returned value exceeds the original sale snapshot.");
                }
            }

            HashSet<int> periodGrnLineIds = supplierReturns
                .SelectMany(r => r.ReturnLines)
                .Where(l => l.GrnLineId.HasValue)
                .Select(l => l.GrnLineId!.Value)
                .ToHashSet();

            if (periodGrnLineIds.Count > 0)
            {
                List<SupplierReturnLine> allSupplierReturnLines =
                    await context.SupplierReturnLines
                        .AsNoTracking()
                        .Where(l => l.GrnLineId.HasValue &&
                            periodGrnLineIds.Contains(l.GrnLineId.Value) &&
                            l.ReturnHeader != null &&
                            l.ReturnHeader.Status == "Posted")
                        .Include(l => l.GrnLine)
                        .Include(l => l.ReturnHeader)
                        .ToListAsync(cancellationToken);

                foreach (IGrouping<int, SupplierReturnLine> group in
                    allSupplierReturnLines.GroupBy(l => l.GrnLineId!.Value))
                {
                    GrnLine? source = group.Select(l => l.GrnLine).FirstOrDefault(l => l != null);
                    if (source == null)
                        continue;

                    SupplierReturnLine first = group.First();
                    DateTime date = first.ReturnHeader?.ReturnDate ?? DateTime.MinValue;
                    string number = first.ReturnHeader?.ReturnNumber ?? source.GrnHeaderId.ToString();

                    AddExcess(result, "Supplier Return vs GRN", number, date,
                        "Cumulative returned quantity", source.ReceivedQty,
                        group.Sum(l => l.ReturnQty),
                        "Cumulative supplier-return quantity exceeds the original GRN line.");
                    AddExcessNullable(result, "Supplier Return vs GRN", number, date,
                        "Cumulative returned taxable", source.TaxableAmountSnapshot,
                        group.Sum(l => l.TaxableAmountSnapshot ?? 0m),
                        "Cumulative returned taxable value exceeds the original GRN snapshot.");
                    AddExcessNullable(result, "Supplier Return vs GRN", number, date,
                        "Cumulative returned VAT", source.VatAmountSnapshot,
                        group.Sum(l => l.VatAmountSnapshot ?? 0m),
                        "Cumulative returned VAT exceeds the original GRN snapshot.");
                    AddExcessNullable(result, "Supplier Return vs GRN", number, date,
                        "Cumulative returned inclusive", source.TaxInclusiveAmountSnapshot,
                        group.Sum(l => l.TaxInclusiveAmountSnapshot ?? 0m),
                        "Cumulative supplier credit exceeds the original GRN snapshot.");
                }
            }
        }

        private static bool IsCompleteSale(SalesHeader header) =>
            IsCompleteStatus(header.TaxSnapshotStatus) &&
            HeaderTotalsPresent(
                header.TaxableAmountTotal,
                header.TotalVatAmount,
                header.StandardRatedAmount,
                header.ZeroRatedAmount,
                header.ExemptAmount,
                header.OutOfScopeAmount) &&
            LinesComplete(
                header.SalesLines
                    .Where(line => !line.IsGiftVoucherSale)
                    .Select(ToSnapshotLineWithStatus));

        private static bool IsCompleteCustomerReturn(CustomerReturnHeader header) =>
            IsCompleteStatus(header.TaxSnapshotStatus) &&
            HeaderTotalsPresent(
                header.TaxableAmountTotal,
                header.TotalVatAmount,
                header.StandardRatedAmount,
                header.ZeroRatedAmount,
                header.ExemptAmount,
                header.OutOfScopeAmount) &&
            LinesComplete(header.Lines.Select(ToSnapshotLineWithStatus));

        private static bool IsCompleteGrn(GrnHeader header) =>
            IsCompleteStatus(header.TaxSnapshotStatus) &&
            HeaderTotalsPresent(
                header.TaxableAmountTotal,
                header.TotalVatAmount,
                header.StandardRatedAmount,
                header.ZeroRatedAmount,
                header.ExemptAmount,
                header.OutOfScopeAmount) &&
            LinesComplete(header.GrnLines.Select(ToSnapshotLineWithStatus));

        private static bool IsCompleteSupplierReturn(SupplierReturnHeader header) =>
            IsCompleteStatus(header.TaxSnapshotStatus) &&
            HeaderTotalsPresent(
                header.TaxableAmountTotal,
                header.TotalVatAmount,
                header.StandardRatedAmount,
                header.ZeroRatedAmount,
                header.ExemptAmount,
                header.OutOfScopeAmount) &&
            LinesComplete(header.ReturnLines.Select(ToSnapshotLineWithStatus));

        private static bool HeaderTotalsPresent(params decimal?[] totals) =>
            totals.All(value => value.HasValue);

        private static bool LinesComplete(IEnumerable<SnapshotLineWithStatus> lines)
        {
            List<SnapshotLineWithStatus> materialized = lines.ToList();
            return materialized.Count > 0 && materialized.All(line =>
                IsCompleteStatus(line.Status) &&
                !string.IsNullOrWhiteSpace(line.CategoryCode) &&
                line.TaxableAmount.HasValue &&
                line.VatAmount.HasValue &&
                line.InclusiveAmount.HasValue);
        }

        private static bool IsCompleteStatus(string? status) =>
            string.Equals(
                status?.Trim(),
                TaxSnapshotStatuses.Complete,
                StringComparison.OrdinalIgnoreCase);

        private static SnapshotLine ToSnapshotLine(SalesLine line) => new(
            line.TaxCategoryCodeSnapshot,
            line.TaxRatePercentSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static SnapshotLine ToSnapshotLine(CustomerReturnLine line) => new(
            line.TaxCategoryCodeSnapshot,
            line.TaxRatePercentSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static SnapshotLine ToSnapshotLine(GrnLine line) => new(
            line.TaxCategoryCodeSnapshot,
            line.TaxRatePercentSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static SnapshotLine ToSnapshotLine(SupplierReturnLine line) => new(
            line.TaxCategoryCodeSnapshot,
            line.TaxRatePercentSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static SnapshotLineWithStatus ToSnapshotLineWithStatus(SalesLine line) => new(
            line.TaxSnapshotStatus,
            line.TaxCategoryCodeSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static SnapshotLineWithStatus ToSnapshotLineWithStatus(CustomerReturnLine line) => new(
            line.TaxSnapshotStatus,
            line.TaxCategoryCodeSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static SnapshotLineWithStatus ToSnapshotLineWithStatus(GrnLine line) => new(
            line.TaxSnapshotStatus,
            line.TaxCategoryCodeSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static SnapshotLineWithStatus ToSnapshotLineWithStatus(SupplierReturnLine line) => new(
            line.TaxSnapshotStatus,
            line.TaxCategoryCodeSnapshot,
            line.TaxableAmountSnapshot,
            line.VatAmountSnapshot,
            line.TaxInclusiveAmountSnapshot);

        private static void AddDocument(
            VatReportResultDto result,
            string sourceType,
            string documentNumber,
            string relatedNumber,
            DateTime date,
            bool isReversal,
            decimal taxable,
            decimal vat,
            decimal inclusive,
            decimal standard,
            decimal zero,
            decimal exempt,
            decimal outOfScope,
            string status)
        {
            result.Documents.Add(new VatReportDocumentRowDto
            {
                SourceType = sourceType,
                DocumentNumber = documentNumber,
                RelatedDocumentNumber = relatedNumber,
                DocumentDate = date,
                IsReversal = isReversal,
                TaxableAmount = Money(taxable),
                VatAmount = Money(vat),
                InclusiveAmount = Money(inclusive),
                StandardRatedAmount = Money(standard),
                ZeroRatedAmount = Money(zero),
                ExemptAmount = Money(exempt),
                OutOfScopeAmount = Money(outOfScope),
                SnapshotStatus = status
            });
        }

        private static void AddLegacy(
            VatReportResultDto result,
            string sourceType,
            string documentNumber,
            string relatedNumber,
            DateTime date,
            decimal amount,
            string? status,
            string warning)
        {
            result.LegacyUnknownRows.Add(new VatLegacyUnknownRowDto
            {
                SourceType = sourceType,
                DocumentNumber = documentNumber,
                RelatedDocumentNumber = relatedNumber,
                DocumentDate = date,
                FinancialAmount = Money(amount),
                SnapshotStatus = string.IsNullOrWhiteSpace(status)
                    ? TaxSnapshotStatuses.LegacyUnknown
                    : status.Trim(),
                Warning = warning
            });
        }

        private static void AddCategoryLines(
            IDictionary<CategoryKey, CategoryAccumulator> accumulators,
            string sourceType,
            string documentNumber,
            bool isReversal,
            IEnumerable<SnapshotLine> lines)
        {
            foreach (SnapshotLine line in lines)
            {
                string category = NormalizeCategory(line.CategoryCode);
                decimal? rate = line.RatePercent.HasValue
                    ? decimal.Round(line.RatePercent.Value, 4, MidpointRounding.AwayFromZero)
                    : null;
                var key = new CategoryKey(sourceType, category, rate, isReversal);

                if (!accumulators.TryGetValue(key, out CategoryAccumulator? accumulator))
                {
                    accumulator = new CategoryAccumulator(key);
                    accumulators.Add(key, accumulator);
                }

                accumulator.Add(
                    documentNumber,
                    line.TaxableAmount ?? 0m,
                    line.VatAmount ?? 0m,
                    line.InclusiveAmount ?? 0m);
            }
        }

        private static void AddCategoryDifferences(
            VatReportResultDto result,
            string sourceType,
            string documentNumber,
            DateTime date,
            decimal? standard,
            decimal? zero,
            decimal? exempt,
            decimal? outOfScope,
            IEnumerable<SnapshotLine> lines)
        {
            List<SnapshotLine> materialized = lines.ToList();
            AddDifference(result, sourceType, documentNumber, date, "Standard Rated total",
                standard ?? 0m, SumCategory(materialized, TaxCategoryCodes.Standard),
                "Header Standard Rated value does not equal saved line snapshots.");
            AddDifference(result, sourceType, documentNumber, date, "Zero Rated total",
                zero ?? 0m, SumCategory(materialized, TaxCategoryCodes.ZeroRated),
                "Header Zero Rated value does not equal saved line snapshots.");
            AddDifference(result, sourceType, documentNumber, date, "Exempt total",
                exempt ?? 0m, SumCategory(materialized, TaxCategoryCodes.Exempt),
                "Header Exempt value does not equal saved line snapshots.");
            AddDifference(result, sourceType, documentNumber, date, "Out of Scope total",
                outOfScope ?? 0m, SumCategory(materialized, TaxCategoryCodes.OutOfScope),
                "Header Out of Scope value does not equal saved line snapshots.");
        }

        private static decimal SumCategory(
            IEnumerable<SnapshotLine> lines,
            string categoryCode) =>
            Money(lines
                .Where(l => string.Equals(
                    NormalizeCategory(l.CategoryCode),
                    categoryCode,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(l => l.TaxableAmount ?? 0m));

        private static void AddDifference(
            VatReportResultDto result,
            string sourceType,
            string documentNumber,
            DateTime date,
            string field,
            decimal headerAmount,
            decimal detailAmount,
            string explanation)
        {
            decimal difference = Money(headerAmount - detailAmount);
            if (Math.Abs(difference) <= DifferenceTolerance)
                return;

            result.ReconciliationRows.Add(new VatReconciliationRowDto
            {
                SourceType = sourceType,
                DocumentNumber = documentNumber,
                DocumentDate = date,
                FieldChecked = field,
                HeaderOrSourceAmount = Money(headerAmount),
                DetailOrReturnedAmount = Money(detailAmount),
                Difference = difference,
                Explanation = explanation
            });
        }

        private static void AddExcess(
            VatReportResultDto result,
            string sourceType,
            string documentNumber,
            DateTime date,
            string field,
            decimal original,
            decimal returned,
            string explanation)
        {
            decimal excess = Money(returned - original);
            if (excess <= DifferenceTolerance)
                return;

            result.ReconciliationRows.Add(new VatReconciliationRowDto
            {
                SourceType = sourceType,
                DocumentNumber = documentNumber,
                DocumentDate = date,
                FieldChecked = field,
                HeaderOrSourceAmount = Money(original),
                DetailOrReturnedAmount = Money(returned),
                Difference = excess,
                Explanation = explanation
            });
        }

        private static void AddExcessNullable(
            VatReportResultDto result,
            string sourceType,
            string documentNumber,
            DateTime date,
            string field,
            decimal? original,
            decimal returned,
            string explanation)
        {
            if (original.HasValue)
                AddExcess(result, sourceType, documentNumber, date, field,
                    original.Value, returned, explanation);
        }

        private static string BuildIncompleteWarning(string? status, string documentType) =>
            IsCompleteStatus(status)
                ? $"The {documentType} is marked Complete but one or more required saved line snapshots are missing. It was excluded from VAT totals."
                : $"The {documentType} has LegacyUnknown tax snapshots. VAT was not reconstructed.";

        private static string NormalizeCategory(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? "UNKNOWN"
                : value.Trim().ToUpperInvariant();

        private static string FirstNonBlank(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
            ?? string.Empty;

        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed record SnapshotLine(
            string? CategoryCode,
            decimal? RatePercent,
            decimal? TaxableAmount,
            decimal? VatAmount,
            decimal? InclusiveAmount);

        private sealed record SnapshotLineWithStatus(
            string? Status,
            string? CategoryCode,
            decimal? TaxableAmount,
            decimal? VatAmount,
            decimal? InclusiveAmount);

        private readonly record struct CategoryKey(
            string SourceType,
            string CategoryCode,
            decimal? RatePercent,
            bool IsReversal);

        private sealed class CategoryAccumulator
        {
            private readonly HashSet<string> _documents =
                new(StringComparer.OrdinalIgnoreCase);

            public CategoryAccumulator(CategoryKey key)
            {
                Key = key;
            }

            public CategoryKey Key { get; }
            public decimal TaxableAmount { get; private set; }
            public decimal VatAmount { get; private set; }
            public decimal InclusiveAmount { get; private set; }

            public void Add(
                string documentNumber,
                decimal taxable,
                decimal vat,
                decimal inclusive)
            {
                _documents.Add(documentNumber);
                TaxableAmount += taxable;
                VatAmount += vat;
                InclusiveAmount += inclusive;
            }

            public VatCategoryRateRowDto ToDto() => new()
            {
                SourceType = Key.SourceType,
                TaxCategoryCode = Key.CategoryCode,
                VatRatePercent = Key.RatePercent,
                IsReversal = Key.IsReversal,
                DocumentCount = _documents.Count,
                TaxableAmount = Money(TaxableAmount),
                VatAmount = Money(VatAmount),
                InclusiveAmount = Money(InclusiveAmount)
            };
        }
    }
}
