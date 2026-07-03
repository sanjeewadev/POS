using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class SupplierReportRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SupplierReportRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // 1. SIMPLE OUTSTANDING SUMMARY
        // =========================================================
        // Correct source:
        // SupplierLedgers
        //
        // GRN        -> ChargeAmount
        // DEBIT_NOTE -> PaymentAmount
        // PAYMENT    -> PaymentAmount

        public async Task<List<SupplierOutstandingSummaryDto>> GetSupplierOutstandingSummaryAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var suppliers = await context.Suppliers
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.SupplierCode,
                    s.SupplierName,
                    s.CompanyName,
                    s.IsDeactivated
                })
                .ToListAsync();

            var supplierMap = suppliers.ToDictionary(s => s.Id);

            var ledgerRows = await context.SupplierLedgers
                .AsNoTracking()
                .Select(l => new
                {
                    l.SupplierId,
                    l.TransactionDate,
                    l.TransactionType,
                    l.ChargeAmount,
                    l.PaymentAmount
                })
                .ToListAsync();

            var result = ledgerRows
                .GroupBy(l => l.SupplierId)
                .Select(group =>
                {
                    supplierMap.TryGetValue(group.Key, out var supplier);

                    decimal totalGrnBilled = group
                        .Where(l => NormalizeLedgerType(l.TransactionType) == "GRN")
                        .Sum(l => l.ChargeAmount);

                    decimal totalSupplierReturns = group
                        .Where(l => IsSupplierReturnType(NormalizeLedgerType(l.TransactionType)))
                        .Sum(l => l.PaymentAmount);

                    decimal totalPaid = group
                        .Where(l => NormalizeLedgerType(l.TransactionType) == "PAYMENT")
                        .Sum(l => l.PaymentAmount);

                    decimal netOutstanding = group.Sum(l => l.ChargeAmount - l.PaymentAmount);

                    return new SupplierOutstandingSummaryDto
                    {
                        SupplierId = group.Key,
                        SupplierCode = supplier?.SupplierCode ?? string.Empty,
                        SupplierName = supplier?.SupplierName ?? $"Supplier #{group.Key}",
                        CompanyName = supplier?.CompanyName ?? string.Empty,

                        TotalGrnBilled = Math.Round(totalGrnBilled, 2),
                        TotalSupplierReturns = Math.Round(totalSupplierReturns, 2),
                        TotalPaid = Math.Round(totalPaid, 2),
                        NetOutstanding = Math.Round(netOutstanding, 2),

                        TransactionCount = group.Count(),
                        LastTransactionDate = group.Max(l => l.TransactionDate)
                    };
                })
                .Where(r =>
                    r.TotalGrnBilled != 0 ||
                    r.TotalSupplierReturns != 0 ||
                    r.TotalPaid != 0 ||
                    r.NetOutstanding != 0)
                .OrderByDescending(r => r.NetOutstanding)
                .ThenBy(r => r.SupplierDisplayName)
                .ToList();

            return result;
        }

        // =========================================================
        // 2. PURCHASING VOLUME
        // =========================================================
        // Correct simple source:
        // Posted GRN headers within selected date range.

        public async Task<List<SupplierPurchaseVolumeDto>> GetPurchasingVolumeAsync(
            DateTime startDate,
            DateTime endDate)
        {
            NormalizeDateRange(ref startDate, ref endDate);

            using var context = await _contextFactory.CreateDbContextAsync();

            var suppliers = await context.Suppliers
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.SupplierCode,
                    s.SupplierName,
                    s.CompanyName
                })
                .ToListAsync();

            var supplierMap = suppliers.ToDictionary(s => s.Id);

            DateTime endExclusive = endDate.Date.AddDays(1);

            var grnRows = await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.Status == "Posted" &&
                    g.ReceivedDate >= startDate.Date &&
                    g.ReceivedDate < endExclusive)
                .Select(g => new
                {
                    g.SupplierId,
                    g.ReceivedDate,
                    g.NetPayable
                })
                .ToListAsync();

            decimal totalCompanyPurchases = grnRows.Sum(g => g.NetPayable);

            if (totalCompanyPurchases <= 0)
                return new List<SupplierPurchaseVolumeDto>();

            var result = grnRows
                .GroupBy(g => g.SupplierId)
                .Select(group =>
                {
                    supplierMap.TryGetValue(group.Key, out var supplier);

                    decimal totalValue = group.Sum(g => g.NetPayable);

                    return new SupplierPurchaseVolumeDto
                    {
                        SupplierId = group.Key,
                        SupplierCode = supplier?.SupplierCode ?? string.Empty,
                        SupplierName = supplier?.SupplierName ?? $"Supplier #{group.Key}",
                        CompanyName = supplier?.CompanyName ?? string.Empty,

                        GrnCount = group.Count(),
                        TotalGrnValue = Math.Round(totalValue, 2),
                        PercentageOfTotalPurchases = Math.Round(
                            totalValue / totalCompanyPurchases * 100m,
                            2),
                        LastGrnDate = group.Max(g => g.ReceivedDate)
                    };
                })
                .OrderByDescending(r => r.TotalGrnValue)
                .ThenBy(r => r.SupplierDisplayName)
                .Take(50)
                .ToList();

            return result;
        }

        // =========================================================
        // 3. SUPPLIER RETURN SUMMARY
        // =========================================================
        // Correct simple source:
        // Posted SupplierReturnHeaders and SupplierReturnLines.

        public async Task<List<SupplierReturnSummaryDto>> GetSupplierReturnSummaryAsync(
            DateTime startDate,
            DateTime endDate)
        {
            NormalizeDateRange(ref startDate, ref endDate);

            using var context = await _contextFactory.CreateDbContextAsync();

            var suppliers = await context.Suppliers
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.SupplierCode,
                    s.SupplierName,
                    s.CompanyName
                })
                .ToListAsync();

            var supplierMap = suppliers.ToDictionary(s => s.Id);

            DateTime endExclusive = endDate.Date.AddDays(1);

            var purchaseRows = await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.Status == "Posted" &&
                    g.ReceivedDate >= startDate.Date &&
                    g.ReceivedDate < endExclusive)
                .Select(g => new
                {
                    g.SupplierId,
                    g.NetPayable
                })
                .ToListAsync();

            var purchaseValueBySupplier = purchaseRows
                .GroupBy(g => g.SupplierId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.NetPayable));

            var returnHeaders = await context.SupplierReturnHeaders
                .Include(h => h.ReturnLines)
                .AsNoTracking()
                .Where(h =>
                    h.Status == "Posted" &&
                    h.ReturnDate >= startDate.Date &&
                    h.ReturnDate < endExclusive)
                .Select(h => new
                {
                    h.SupplierId,
                    h.ReturnDate,
                    h.GrossCredit,
                    h.RestockingFee,
                    h.NetCredit,
                    Lines = h.ReturnLines
                        .Select(l => new
                        {
                            l.ReturnQty,
                            l.CreditValue
                        })
                        .ToList()
                })
                .ToListAsync();

            var result = returnHeaders
                .GroupBy(h => h.SupplierId)
                .Select(group =>
                {
                    supplierMap.TryGetValue(group.Key, out var supplier);

                    decimal purchaseValue = purchaseValueBySupplier.TryGetValue(group.Key, out decimal purchased)
                        ? purchased
                        : 0m;

                    decimal netSupplierCredit = group.Sum(h => h.NetCredit);

                    decimal returnValuePercentage = purchaseValue <= 0
                        ? 0m
                        : Math.Round(netSupplierCredit / purchaseValue * 100m, 2);

                    return new SupplierReturnSummaryDto
                    {
                        SupplierId = group.Key,
                        SupplierCode = supplier?.SupplierCode ?? string.Empty,
                        SupplierName = supplier?.SupplierName ?? $"Supplier #{group.Key}",
                        CompanyName = supplier?.CompanyName ?? string.Empty,

                        ReturnDocumentCount = group.Count(),
                        TotalReturnedQty = Math.Round(
                            group.Sum(h => h.Lines.Sum(l => l.ReturnQty)),
                            3),
                        GrossReturnValue = Math.Round(group.Sum(h => h.GrossCredit), 2),
                        RestockingFee = Math.Round(group.Sum(h => h.RestockingFee), 2),
                        NetSupplierCredit = Math.Round(netSupplierCredit, 2),
                        PurchaseValueInPeriod = Math.Round(purchaseValue, 2),
                        ReturnValuePercentage = returnValuePercentage,
                        LastReturnDate = group.Max(h => h.ReturnDate)
                    };
                })
                .Where(r => r.NetSupplierCredit > 0 || r.TotalReturnedQty > 0)
                .OrderByDescending(r => r.NetSupplierCredit)
                .ThenByDescending(r => r.TotalReturnedQty)
                .ThenBy(r => r.SupplierDisplayName)
                .ToList();

            return result;
        }

        // =========================================================
        // KPI HELPERS
        // =========================================================

        public async Task<decimal> GetTotalCompanyOutstandingAsync()
        {
            var rows = await GetSupplierOutstandingSummaryAsync();

            return Math.Round(
                rows.Where(r => r.NetOutstanding > 0)
                    .Sum(r => r.NetOutstanding),
                2);
        }

        // =========================================================
        // TEMPORARY COMPATIBILITY METHODS
        // =========================================================
        // Keep these so the older SupplierReportViewModel can still compile
        // until we replace it in the next step.

        public async Task<List<AgedPayableDto>> GetAgedPayablesSummaryAsync()
        {
            var rows = await GetSupplierOutstandingSummaryAsync();

            return rows
                .Where(r => r.NetOutstanding > 0)
                .Select(r => new AgedPayableDto
                {
                    SupplierId = r.SupplierId,
                    SupplierName = r.SupplierDisplayName,

                    // This is not real invoice aging.
                    // It is only a temporary compatibility mapping.
                    CurrentTo30Days = r.NetOutstanding,
                    Days31To60 = 0m,
                    Days61To90 = 0m,
                    Over90Days = 0m
                })
                .OrderByDescending(r => r.TotalOwed)
                .ToList();
        }

        public async Task<List<SupplierVolumeDto>> GetLegacyPurchasingVolumeAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var rows = await GetPurchasingVolumeAsync(startDate, endDate);

            return rows
                .Select(r => new SupplierVolumeDto
                {
                    SupplierId = r.SupplierId,
                    SupplierName = r.SupplierDisplayName,
                    TotalGrnValue = r.TotalGrnValue,
                    PercentageOfTotalStore = (double)r.PercentageOfTotalPurchases
                })
                .ToList();
        }

        public async Task<List<SupplierReturnRateDto>> GetReturnRatesAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var rows = await GetSupplierReturnSummaryAsync(startDate, endDate);

            return rows
                .Select(r => new SupplierReturnRateDto
                {
                    SupplierId = r.SupplierId,
                    SupplierName = r.SupplierDisplayName,
                    TotalItemsBought = 0m,
                    TotalItemsReturned = r.TotalReturnedQty
                })
                .ToList();
        }

        // Keep old method name used by old ViewModel.
        public async Task<List<SupplierVolumeDto>> GetPurchasingVolumeLegacyAsync(
            DateTime startDate,
            DateTime endDate)
        {
            return await GetLegacyPurchasingVolumeAsync(startDate, endDate);
        }

        // =========================================================
        // PRIVATE HELPERS
        // =========================================================

        private static void NormalizeDateRange(
            ref DateTime startDate,
            ref DateTime endDate)
        {
            startDate = startDate.Date;
            endDate = endDate.Date;

            if (startDate > endDate)
            {
                DateTime temp = startDate;
                startDate = endDate;
                endDate = temp;
            }
        }

        private static string NormalizeLedgerType(string? value)
        {
            string type = NormalizeText(value).ToUpperInvariant();

            if (type == "SUPPLIER_RETURN" || type == "RETURN")
                return "DEBIT_NOTE";

            if (type == "SUPPLIER_PAYMENT")
                return "PAYMENT";

            if (string.IsNullOrWhiteSpace(type))
                return "UNKNOWN";

            return type;
        }

        private static bool IsSupplierReturnType(string ledgerType)
        {
            return ledgerType == "DEBIT_NOTE" ||
                   ledgerType == "CREDIT_NOTE" ||
                   ledgerType == "SUPPLIER_RETURN";
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}