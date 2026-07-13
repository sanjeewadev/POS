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
    public sealed class DashboardRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly FinancialAnalyticsRepository _financialRepository;
        private readonly SalesAnalyticsRepository _salesAnalyticsRepository;
        private readonly SupplierReportRepository _supplierReportRepository;

        public DashboardRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            FinancialAnalyticsRepository financialRepository,
            SalesAnalyticsRepository salesAnalyticsRepository,
            SupplierReportRepository supplierReportRepository)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _financialRepository = financialRepository ?? throw new ArgumentNullException(nameof(financialRepository));
            _salesAnalyticsRepository = salesAnalyticsRepository ?? throw new ArgumentNullException(nameof(salesAnalyticsRepository));
            _supplierReportRepository = supplierReportRepository ?? throw new ArgumentNullException(nameof(supplierReportRepository));
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(DateTime startDate, DateTime endDate)
        {
            if (startDate.Date > endDate.Date)
                throw new ArgumentException("Start date cannot be later than end date.");

            FinancialSummaryDto financial = await _financialRepository.GetFinancialSummaryAsync(startDate, endDate);
            ItemSalesAnalyticsResultDto itemAnalytics = await _salesAnalyticsRepository.GetAnalyticsAsync(startDate, endDate, string.Empty);
            decimal supplierOutstanding = await _supplierReportRepository.GetTotalCompanyOutstandingAsync();

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            List<decimal> customerBalanceRows = await context.CustomerMasters
                .AsNoTracking()
                .Where(customer => customer.IsActive && customer.CurrentBalance > 0m)
                .Select(customer => customer.CurrentBalance)
                .ToListAsync();

            decimal customerOutstanding = customerBalanceRows.Sum();

            var stockVariants = await context.ItemVariants
                .AsNoTracking()
                .Where(variant =>
                    !variant.IsDeactivated &&
                    !variant.ItemParent.IsDeactivated &&
                    variant.ItemParent.ItemType != ItemTypeCodes.Service)
                .Select(variant => new
                {
                    variant.Id,
                    variant.ReorderLevel
                })
                .ToListAsync();

            int[] stockVariantIds = stockVariants
                .Select(variant => variant.Id)
                .ToArray();

            var activeBatchRows = stockVariantIds.Length == 0
                ? new List<DashboardStockBatchRow>()
                : await context.ItemBatches
                    .AsNoTracking()
                    .Where(batch =>
                        stockVariantIds.Contains(batch.ItemVariantId) &&
                        !batch.IsDeactivated)
                    .Select(batch => new DashboardStockBatchRow
                    {
                        ItemVariantId = batch.ItemVariantId,
                        CurrentStock = batch.CurrentStock
                    })
                    .ToListAsync();

            var stockByVariant = activeBatchRows
                .GroupBy(batch => batch.ItemVariantId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(batch => batch.CurrentStock));

            int negativeStock = stockVariants.Count(variant =>
                stockByVariant.GetValueOrDefault(variant.Id) < 0m);

            int lowStock = stockVariants.Count(variant =>
            {
                decimal quantity = stockByVariant.GetValueOrDefault(variant.Id);
                return quantity >= 0m && quantity <= Math.Max(0, variant.ReorderLevel);
            });

            int openShifts = await context.ShiftSessions
                .AsNoTracking()
                .CountAsync(shift => shift.Status == "Open" || shift.Status == "Closing");

            int heldCarts = await context.CashierCartSessions
                .AsNoTracking()
                .CountAsync(cart => cart.Status == CashierCartStatusCodes.Held);

            int draftClaims = await context.FreeItemClaimLogs
                .AsNoTracking()
                .CountAsync(claim => claim.ClaimStatus == SupplierClaimStatusCodes.Draft);

            int submittedClaims = await context.FreeItemClaimLogs
                .AsNoTracking()
                .CountAsync(claim => claim.ClaimStatus == SupplierClaimStatusCodes.Submitted);

            List<DashboardTopItemDto> topItems = itemAnalytics.Items
                .Where(row => row.NetQuantity > 0m)
                .OrderByDescending(row => row.NetSales)
                .ThenByDescending(row => row.NetQuantity)
                .Take(5)
                .Select((row, index) => new DashboardTopItemDto
                {
                    Rank = index + 1,
                    ItemCode = string.IsNullOrWhiteSpace(row.SkuCode) ? row.ItemCode : row.SkuCode,
                    ItemName = row.ItemName,
                    ItemType = row.ItemType,
                    NetQuantity = row.NetQuantity,
                    NetSales = row.NetSales
                })
                .ToList();

            var attention = new List<DashboardAttentionDto>();
            AddAttention(attention, negativeStock, "Critical", "Inventory", "Stock Item variants have negative stock.");
            AddAttention(attention, lowStock, "Warning", "Inventory", "Stock Item variants are at or below reorder level.");
            AddAttention(attention, heldCarts, "Information", "Cashier", "Held carts are waiting for recall or cancellation.");
            AddAttention(attention, openShifts, "Information", "Cashier", "Shift sessions are currently open.");
            AddAttention(attention, draftClaims, "Warning", "Supplier Claims", "Supplier claims are still in Draft status.");
            AddAttention(attention, submittedClaims, "Information", "Supplier Claims", "Supplier claims are waiting for settlement.");

            if (attention.Count == 0)
            {
                attention.Add(new DashboardAttentionDto
                {
                    Severity = "Information",
                    Area = "Operations",
                    Message = "No immediate operational attention items were found.",
                    Count = 0
                });
            }

            return new DashboardSummaryDto
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                MerchandiseSales = financial.MerchandiseSalesAfterDiscounts,
                CustomerReturns = financial.CustomerReturns,
                NetSales = financial.NetSales,
                GrossProfit = financial.GrossProfit,
                CustomerCreditOutstanding = Money(customerOutstanding),
                SupplierOutstanding = Money(Math.Max(0m, supplierOutstanding)),
                LowStockCount = lowStock,
                NegativeStockCount = negativeStock,
                OpenShiftCount = openShifts,
                HeldCartCount = heldCarts,
                DraftSupplierClaimCount = draftClaims,
                SubmittedSupplierClaimCount = submittedClaims,
                TenderTotals = financial.TenderTotals,
                TopItems = topItems,
                AttentionItems = attention
            };
        }

        private static void AddAttention(
            ICollection<DashboardAttentionDto> target,
            int count,
            string severity,
            string area,
            string message)
        {
            if (count <= 0)
                return;

            target.Add(new DashboardAttentionDto
            {
                Severity = severity,
                Area = area,
                Message = message,
                Count = count
            });
        }

        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed class DashboardStockBatchRow
        {
            public int ItemVariantId { get; init; }
            public decimal CurrentStock { get; init; }
        }
    }
}
