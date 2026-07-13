using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public sealed class FinancialAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public FinancialAnalyticsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<FinancialSummaryDto> GetFinancialSummaryAsync(
            DateTime startDate,
            DateTime endDate)
        {
            if (startDate.Date > endDate.Date)
                throw new ArgumentException("Start date cannot be later than end date.");

            DateTime start = startDate.Date;
            DateTime endExclusive = endDate.Date.AddDays(1);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            var sales = await context.SalesHeaders
                .AsNoTracking()
                .Where(header =>
                    header.Status == "Completed" &&
                    !header.IsVoided &&
                    header.TransactionDate >= start &&
                    header.TransactionDate < endExclusive)
                .Select(header => new
                {
                    header.Id,
                    header.GrossTotal,
                    header.TotalDiscount,
                    header.GiftVoucherIssueTotal
                })
                .ToListAsync();

            int[] saleIds = sales.Select(row => row.Id).ToArray();

            var saleLines = saleIds.Length == 0
                ? new List<SaleCostRow>()
                : await context.SalesLines
                    .AsNoTracking()
                    .Where(line => saleIds.Contains(line.SalesHeaderId) && !line.IsGiftVoucherSale)
                    .Select(line => new SaleCostRow
                    {
                        SalesHeaderId = line.SalesHeaderId,
                        Cost = line.CostPrice * line.Quantity
                    })
                    .ToListAsync();

            var returnHeaders = await context.CustomerReturnHeaders
                .AsNoTracking()
                .Where(header => header.ReturnDate >= start && header.ReturnDate < endExclusive)
                .Select(header => new
                {
                    header.Id,
                    header.TotalRefundAmount,
                    header.CashRefundAmount
                })
                .ToListAsync();

            int[] returnIds = returnHeaders.Select(row => row.Id).ToArray();
            var returnCosts = returnIds.Length == 0
                ? new List<decimal>()
                : await context.CustomerReturnLines
                    .AsNoTracking()
                    .Where(line => returnIds.Contains(line.CustomerReturnHeaderId) && line.SalesLine != null)
                    .Select(line => line.SalesLine!.CostPrice * line.QuantityReturned)
                    .ToListAsync();

            var tenderPaymentRows = saleIds.Length == 0
                ? new List<TenderPaymentRow>()
                : await context.SalesPayments
                    .AsNoTracking()
                    .Where(payment => saleIds.Contains(payment.SalesHeaderId))
                    .Select(payment => new TenderPaymentRow
                    {
                        PaymentType = payment.PaymentType,
                        Amount = payment.Amount
                    })
                    .ToListAsync();

            var tenderRows = tenderPaymentRows
                .GroupBy(payment => CustomerCreditCodes.IsCustomerCreditPayment(payment.PaymentType)
                    ? CustomerCreditCodes.PaymentDisplayName
                    : payment.PaymentType, StringComparer.OrdinalIgnoreCase)
                .Select(group => new FinancialTenderTotalDto
                {
                    PaymentType = group.Key,
                    Amount = Money(group.Sum(payment => payment.Amount)),
                    TransactionCount = group.Count()
                })
                .OrderBy(row => row.PaymentType)
                .ToList();

            var purchases = await context.GrnHeaders
                .AsNoTracking()
                .Where(header =>
                    header.Status == "Posted" &&
                    header.ReceivedDate >= start &&
                    header.ReceivedDate < endExclusive)
                .Select(header => header.NetPayable)
                .ToListAsync();

            var supplierReturns = await context.SupplierReturnHeaders
                .AsNoTracking()
                .Where(header =>
                    header.Status == "Posted" &&
                    header.ReturnDate >= start &&
                    header.ReturnDate < endExclusive)
                .Select(header => header.NetCredit)
                .ToListAsync();

            var cashMovements = await context.CashMovements
                .AsNoTracking()
                .Where(row => row.Timestamp >= start && row.Timestamp < endExclusive)
                .Select(row => new
                {
                    row.MovementType,
                    row.ReasonCategory,
                    row.Amount
                })
                .ToListAsync();

            decimal floatIn = cashMovements
                .Where(row => CashMovementReasonCodes.IsFloatIn(row.ReasonCategory))
                .Sum(row => Math.Abs(row.Amount));
            decimal floatOut = cashMovements
                .Where(row => CashMovementReasonCodes.IsFloatOut(row.ReasonCategory))
                .Sum(row => Math.Abs(row.Amount));
            decimal customerRefunds = cashMovements
                .Where(row => CashMovementReasonCodes.IsCustomerRefund(row.ReasonCategory))
                .Sum(row => Math.Abs(row.Amount));
            decimal paidIn = cashMovements
                .Where(row => row.MovementType.Equals(CashMovementTypeCodes.PaidIn, StringComparison.OrdinalIgnoreCase) &&
                    !CashMovementReasonCodes.IsFloatIn(row.ReasonCategory))
                .Sum(row => Math.Abs(row.Amount));
            decimal paidOut = cashMovements
                .Where(row => row.MovementType.Equals(CashMovementTypeCodes.PaidOut, StringComparison.OrdinalIgnoreCase) &&
                    !CashMovementReasonCodes.IsFloatOut(row.ReasonCategory) &&
                    !CashMovementReasonCodes.IsCustomerRefund(row.ReasonCategory))
                .Sum(row => Math.Abs(row.Amount));

            return new FinancialSummaryDto
            {
                GrossMerchandiseSales = Money(sales.Sum(row => row.GrossTotal - row.GiftVoucherIssueTotal)),
                TotalDiscounts = Money(sales.Sum(row => row.TotalDiscount)),
                CustomerReturns = Money(returnHeaders.Sum(row => row.TotalRefundAmount)),
                SaleCostOfGoods = Money(saleLines.Sum(row => row.Cost)),
                ReturnedCostOfGoods = Money(returnCosts.Sum()),
                GiftVoucherIssueValue = Money(sales.Sum(row => row.GiftVoucherIssueTotal)),
                PostedPurchases = Money(purchases.Sum()),
                PostedSupplierReturns = Money(supplierReturns.Sum()),
                PaidIn = Money(paidIn),
                PaidOut = Money(paidOut),
                FloatIn = Money(floatIn),
                FloatOut = Money(floatOut),
                CustomerCashRefunds = Money(customerRefunds),
                TotalSalesCount = sales.Count,
                TenderTotals = tenderRows
            };
        }

        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed class SaleCostRow
        {
            public int SalesHeaderId { get; init; }
            public decimal Cost { get; init; }
        }

        private sealed class TenderPaymentRow
        {
            public string PaymentType { get; init; } = string.Empty;
            public decimal Amount { get; init; }
        }
    }
}
