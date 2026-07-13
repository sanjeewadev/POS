using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public sealed class MasterSalesAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public MasterSalesAnalyticsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public Task<PagedSalesResult> GetPagedSalesAsync(
            DateTime startDate,
            DateTime endDate,
            string searchText,
            string statusFilter,
            int pageIndex,
            int pageSize)
        {
            // Retain the pre-Phase-9A call shape used by the completed Gift Voucher
            // revenue regression. Sales Explorer is now intentionally completed-sale only.
            return GetPagedSalesAsync(
                startDate,
                endDate,
                searchText,
                string.Empty,
                "All",
                pageIndex,
                pageSize);
        }

        public async Task<PagedSalesResult> GetPagedSalesAsync(
            DateTime startDate,
            DateTime endDate,
            string searchText,
            string terminalFilter,
            string returnStatusFilter,
            int pageIndex,
            int pageSize)
        {
            if (startDate.Date > endDate.Date)
                throw new ArgumentException("Start date cannot be later than end date.");

            pageIndex = Math.Max(1, pageIndex);
            pageSize = Math.Clamp(pageSize, 10, 200);
            DateTime start = startDate.Date;
            DateTime endExclusive = endDate.Date.AddDays(1);
            string search = Normalize(searchText);
            string terminal = Normalize(terminalFilter);
            string returnFilter = Normalize(returnStatusFilter);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            var query = context.SalesHeaders
                .AsNoTracking()
                .Where(header =>
                    header.Status == "Completed" &&
                    !header.IsVoided &&
                    header.TransactionDate >= start &&
                    header.TransactionDate < endExclusive);

            if (!string.IsNullOrWhiteSpace(terminal))
                query = query.Where(header => header.TerminalNo.Contains(terminal));

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(header =>
                    header.InvoiceNo.Contains(search) ||
                    header.CustomerName.Contains(search) ||
                    header.CustomerCode.Contains(search) ||
                    header.CashierName.Contains(search) ||
                    header.TerminalNo.Contains(search) ||
                    (header.TaxInvoiceNo ?? string.Empty).Contains(search));
            }

            var headers = await query
                .Select(header => new
                {
                    header.Id,
                    header.InvoiceNo,
                    header.TransactionDate,
                    header.TerminalNo,
                    header.CustomerName,
                    header.CashierName,
                    header.GrossTotal,
                    header.TotalDiscount,
                    MerchandiseNet = header.NetTotal - header.GiftVoucherIssueTotal,
                    TaxInvoiceNo = header.TaxInvoiceNo ?? string.Empty
                })
                .ToListAsync();

            int[] saleIds = headers.Select(header => header.Id).ToArray();

            var saleLines = saleIds.Length == 0
                ? new List<SaleLineAggregateRow>()
                : await context.SalesLines
                    .AsNoTracking()
                    .Where(line => saleIds.Contains(line.SalesHeaderId) && !line.IsGiftVoucherSale)
                    .Select(line => new SaleLineAggregateRow
                    {
                        SalesHeaderId = line.SalesHeaderId,
                        SalesLineId = line.Id,
                        Quantity = line.Quantity,
                        Cost = line.CostPrice * line.Quantity
                    })
                    .ToListAsync();

            var returnLines = saleIds.Length == 0
                ? new List<ReturnLineAggregateRow>()
                : await context.CustomerReturnLines
                    .AsNoTracking()
                    .Where(line =>
                        line.SalesLineId.HasValue &&
                        line.SalesLine != null &&
                        saleIds.Contains(line.SalesLine.SalesHeaderId))
                    .Select(line => new ReturnLineAggregateRow
                    {
                        SalesHeaderId = line.SalesLine!.SalesHeaderId,
                        SalesLineId = line.SalesLineId!.Value,
                        Quantity = line.QuantityReturned,
                        Refund = line.LineTotalRefund,
                        ReturnedCost = line.SalesLine.CostPrice * line.QuantityReturned
                    })
                    .ToListAsync();

            var paymentRows = saleIds.Length == 0
                ? new List<PaymentAggregateRow>()
                : await context.SalesPayments
                    .AsNoTracking()
                    .Where(payment => saleIds.Contains(payment.SalesHeaderId))
                    .Select(payment => new PaymentAggregateRow
                    {
                        SalesHeaderId = payment.SalesHeaderId,
                        PaymentType = payment.PaymentType
                    })
                    .ToListAsync();

            var lineBySale = saleLines.GroupBy(line => line.SalesHeaderId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var returnBySale = returnLines.GroupBy(line => line.SalesHeaderId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var paymentBySale = paymentRows.GroupBy(payment => payment.SalesHeaderId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var records = new List<SalesExplorerRecordDto>();

            foreach (var header in headers)
            {
                lineBySale.TryGetValue(header.Id, out var lines);
                returnBySale.TryGetValue(header.Id, out var returned);
                paymentBySale.TryGetValue(header.Id, out var payments);
                lines ??= new();
                returned ??= new();
                payments ??= new();

                string returnStatus = CalculateReturnStatus(lines, returned);
                if (!MatchesReturnStatus(returnStatus, returnFilter))
                    continue;

                records.Add(new SalesExplorerRecordDto
                {
                    SaleId = header.Id,
                    InvoiceNo = header.InvoiceNo,
                    TransactionDate = header.TransactionDate,
                    TerminalNo = header.TerminalNo,
                    CustomerName = header.CustomerName,
                    CashierName = header.CashierName,
                    GrossAmount = header.GrossTotal,
                    TotalDiscount = header.TotalDiscount,
                    NetAmount = Money(header.MerchandiseNet),
                    ReturnedAmount = Money(returned.Sum(row => row.Refund)),
                    TotalCost = Money(lines.Sum(row => row.Cost)),
                    ReturnedCost = Money(returned.Sum(row => row.ReturnedCost)),
                    PaymentMethods = string.Join(", ", payments
                        .Select(row => CustomerCreditCodes.IsCustomerCreditPayment(row.PaymentType)
                            ? CustomerCreditCodes.PaymentDisplayName
                            : row.PaymentType)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                    ReturnStatus = returnStatus,
                    TaxInvoiceNo = header.TaxInvoiceNo
                });
            }

            records = records
                .OrderByDescending(row => row.TransactionDate)
                .ThenByDescending(row => row.SaleId)
                .ToList();

            int totalCount = records.Count;
            var page = records
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedSalesResult
            {
                Records = page,
                TotalCount = totalCount,
                SummaryNetSales = Money(records.Sum(row => row.NetAmount)),
                SummaryReturns = Money(records.Sum(row => row.ReturnedAmount)),
                SummaryNetAfterReturns = Money(records.Sum(row => row.NetAfterReturns)),
                SummaryGrossProfit = Money(records.Sum(row => row.GrossProfit))
            };
        }

        public async Task<SaleReceiptDetailsDto?> GetSaleReceiptDetailsAsync(int saleId)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            var sale = await context.SalesHeaders
                .AsNoTracking()
                .Include(header => header.SalesLines)
                .Include(header => header.SalesPayments)
                .Include(header => header.SalesDocumentAudits)
                .FirstOrDefaultAsync(header => header.Id == saleId);

            if (sale == null)
                return null;

            var returns = await context.CustomerReturnHeaders
                .AsNoTracking()
                .Include(header => header.Lines)
                .Where(header => header.OriginalSalesHeaderId == saleId)
                .OrderByDescending(header => header.ReturnDate)
                .ToListAsync();

            var returnedByLine = returns
                .SelectMany(header => header.Lines)
                .Where(line => line.SalesLineId.HasValue)
                .GroupBy(line => line.SalesLineId!.Value)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.QuantityReturned));

            var lines = sale.SalesLines
                .OrderBy(line => line.Id)
                .Select(line =>
                {
                    returnedByLine.TryGetValue(line.Id, out decimal returnedQuantity);
                    return new SaleReceiptLineDto
                    {
                        SalesLineId = line.Id,
                        ItemCode = FirstNonEmpty(line.SkuCode, line.Barcode),
                        Description = line.ItemDescription,
                        ItemType = line.ItemTypeSnapshot ?? string.Empty,
                        Qty = line.Quantity,
                        ReturnedQty = RoundQuantity(returnedQuantity),
                        UnitPrice = line.UnitPrice,
                        DiscountAmount = line.DiscountAmount,
                        LineTotal = line.LineTotal,
                        TaxCategory = FirstNonEmpty(line.TaxCategoryCodeSnapshot, line.TaxNameSnapshot),
                        TaxRatePercent = line.TaxRatePercentSnapshot,
                        TaxableAmount = line.TaxableAmountSnapshot,
                        VatAmount = line.VatAmountSnapshot,
                        TaxInclusiveAmount = line.TaxInclusiveAmountSnapshot,
                        TaxSnapshotStatus = line.TaxSnapshotStatus,
                        IsFreeItem = line.IsFreeItem,
                        IsGiftVoucherSale = line.IsGiftVoucherSale,
                        ReturnStatus = CalculateLineReturnStatus(line.Quantity, returnedQuantity)
                    };
                })
                .ToList();

            decimal totalReturned = Money(returns.Sum(header => header.TotalRefundAmount));

            return new SaleReceiptDetailsDto
            {
                SaleId = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                TransactionDate = sale.TransactionDate,
                TerminalNo = sale.TerminalNo,
                CashierName = sale.CashierName,
                CustomerName = sale.CustomerName,
                CustomerCode = sale.CustomerCode,
                Status = sale.Status,
                DocumentType = sale.DocumentType,
                TaxInvoiceNo = sale.TaxInvoiceNo ?? string.Empty,
                GrossAmount = sale.GrossTotal,
                TotalDiscount = sale.TotalDiscount,
                GiftVoucherIssueTotal = sale.GiftVoucherIssueTotal,
                NetAmount = sale.NetTotal,
                ReturnedAmount = totalReturned,
                TaxableAmountTotal = sale.TaxableAmountTotal,
                TotalVatAmount = sale.TotalVatAmount,
                StandardRatedAmount = sale.StandardRatedAmount,
                ZeroRatedAmount = sale.ZeroRatedAmount,
                ExemptAmount = sale.ExemptAmount,
                OutOfScopeAmount = sale.OutOfScopeAmount,
                TaxSnapshotStatus = sale.TaxSnapshotStatus,
                ReturnStatus = CalculateReturnStatus(
                    sale.SalesLines
                        .Where(line => !line.IsGiftVoucherSale)
                        .Select(line => new SaleLineAggregateRow
                        {
                            SalesHeaderId = sale.Id,
                            SalesLineId = line.Id,
                            Quantity = line.Quantity,
                            Cost = line.CostPrice * line.Quantity
                        }).ToList(),
                    returns.SelectMany(header => header.Lines)
                        .Where(line => line.SalesLineId.HasValue)
                        .Select(line => new ReturnLineAggregateRow
                        {
                            SalesHeaderId = sale.Id,
                            SalesLineId = line.SalesLineId!.Value,
                            Quantity = line.QuantityReturned,
                            Refund = line.LineTotalRefund,
                            ReturnedCost = (line.SalesLine?.CostPrice ?? 0m) * line.QuantityReturned
                        }).ToList()),
                Lines = lines,
                Payments = sale.SalesPayments
                    .OrderBy(payment => payment.Id)
                    .Select(payment => new SaleReceiptPaymentDto
                    {
                        PaymentType = CustomerCreditCodes.IsCustomerCreditPayment(payment.PaymentType)
                            ? CustomerCreditCodes.PaymentDisplayName
                            : payment.PaymentType,
                        Amount = payment.Amount,
                        TenderedAmount = payment.TenderedAmount,
                        ChangeAmount = payment.ChangeAmount,
                        ReferenceNo = payment.ReferenceNo,
                        BankOrCardType = payment.BankOrCardType,
                        CardLastDigits = payment.CardLastDigits,
                        PaymentDate = payment.PaymentDate,
                        EnteredBy = payment.EnteredBy,
                        TerminalNo = payment.TerminalNo,
                        GiftVoucherNo = payment.GiftVoucherNo
                    }).ToList(),
                CreditNotes = returns.Select(header => new SalesExplorerCreditNoteDto
                {
                    CustomerReturnHeaderId = header.Id,
                    CreditNoteNo = header.CreditNoteNo ?? header.ReturnNo,
                    ReturnDate = header.ReturnDate,
                    RefundAmount = header.TotalRefundAmount,
                    RefundMethod = header.RefundMethod,
                    AuthorizedBy = header.AuthorizedBy
                }).ToList(),
                DocumentAudits = sale.SalesDocumentAudits
                    .OrderByDescending(audit => audit.OccurredAtUtc)
                    .Select(audit => new SalesExplorerDocumentAuditDto
                    {
                        OccurredAt = ToLocal(audit.OccurredAtUtc),
                        DocumentType = audit.DocumentType,
                        DocumentNumber = audit.DocumentNumber,
                        EventType = audit.EventType,
                        CopyNumber = audit.CopyNumber,
                        IsSuccessful = audit.IsSuccessful,
                        PerformedBy = audit.PerformedBy,
                        TerminalNo = audit.TerminalNo,
                        PrinterName = audit.PrinterName,
                        ErrorMessage = audit.ErrorMessage
                    }).ToList()
            };
        }

        private static bool MatchesReturnStatus(string actual, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                   filter.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                   actual.Equals(filter, StringComparison.OrdinalIgnoreCase);
        }

        private static string CalculateReturnStatus(
            IReadOnlyCollection<SaleLineAggregateRow> lines,
            IReadOnlyCollection<ReturnLineAggregateRow> returned)
        {
            decimal soldQuantity = lines.Sum(line => line.Quantity);
            decimal returnedQuantity = returned.Sum(line => line.Quantity);

            if (returnedQuantity <= 0m)
                return "Not Returned";
            if (soldQuantity > 0m && returnedQuantity >= soldQuantity - 0.0005m)
                return "Fully Returned";
            return "Partially Returned";
        }

        private static string CalculateLineReturnStatus(decimal sold, decimal returned)
        {
            if (returned <= 0m)
                return "Not Returned";
            if (returned >= sold - 0.0005m)
                return "Fully Returned";
            return "Partially Returned";
        }

        private static DateTime ToLocal(DateTime utcDate) =>
            DateTime.SpecifyKind(utcDate, DateTimeKind.Utc).ToLocalTime();

        private static string Normalize(string? value) => (value ?? string.Empty).Trim();
        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        private static decimal RoundQuantity(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero);

        private sealed class SaleLineAggregateRow
        {
            public int SalesHeaderId { get; init; }
            public int SalesLineId { get; init; }
            public decimal Quantity { get; init; }
            public decimal Cost { get; init; }
        }

        private sealed class ReturnLineAggregateRow
        {
            public int SalesHeaderId { get; init; }
            public int SalesLineId { get; init; }
            public decimal Quantity { get; init; }
            public decimal Refund { get; init; }
            public decimal ReturnedCost { get; init; }
        }

        private sealed class PaymentAggregateRow
        {
            public int SalesHeaderId { get; init; }
            public string PaymentType { get; init; } = string.Empty;
        }
    }
}
