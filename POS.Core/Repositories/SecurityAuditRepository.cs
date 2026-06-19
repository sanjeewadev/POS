using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class SecurityAuditRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SecurityAuditRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // 1. RAW RETURN LEDGER (Specific Refund Transactions)
        // ==============================================================================
        public async Task<List<ReturnAuditRecordDto>> GetReturnRecordsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.CustomerReturnHeaders
                .AsNoTracking()
                .Where(r => r.ReturnDate.Date >= startDate.Date && r.ReturnDate.Date <= endDate.Date)
                .Select(r => new ReturnAuditRecordDto
                {
                    ReturnNo = r.ReturnNo,
                    OriginalInvoiceNo = r.OriginalInvoiceNo ?? "Blind Return",
                    ReturnDate = r.ReturnDate,
                    CashierName = r.CashierName,
                    TerminalNo = r.TerminalNo,
                    AuthorizedBy = r.AuthorizedBy ?? string.Empty,
                    RefundAmount = r.TotalRefundAmount,
                    RefundMethod = r.RefundMethod
                })
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();
        }

        // ==============================================================================
        // 2. RAW VOID & SUSPENDED LEDGER (Cancelled Sales)
        // ==============================================================================
        public async Task<List<VoidAuditRecordDto>> GetVoidRecordsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.SalesHeaders
                .AsNoTracking()
                .Where(h => h.TransactionDate.Date >= startDate.Date && h.TransactionDate.Date <= endDate.Date &&
                           (h.Status == "Voided" || h.Status == "Suspended"))
                .Select(h => new VoidAuditRecordDto
                {
                    InvoiceNo = h.InvoiceNo,
                    TransactionDate = h.TransactionDate,
                    CashierName = h.CashierName,
                    TerminalNo = h.TerminalNo,
                    AttemptedAmount = h.GrossTotal,
                    Status = h.Status
                })
                .OrderByDescending(h => h.TransactionDate)
                .ToListAsync();
        }

        // ==============================================================================
        // 3. CASHIER BEHAVIORAL PROFILING (Fraud Risk Assessment)
        // ==============================================================================
        public async Task<List<CashierFraudRiskDto>> GetCashierRiskProfilesAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Step 1: Count all sales and voids per cashier
            var salesStats = await context.SalesHeaders
                .AsNoTracking()
                .Where(h => h.TransactionDate.Date >= startDate.Date && h.TransactionDate.Date <= endDate.Date)
                .GroupBy(h => h.CashierName)
                .Select(g => new
                {
                    CashierName = g.Key,
                    TotalTransactions = g.Count(),
                    VoidCount = g.Count(h => h.Status == "Voided")
                })
                .ToListAsync();

            // Step 2: Count all returns per cashier
            var returnStats = await context.CustomerReturnHeaders
                .AsNoTracking()
                .Where(r => r.ReturnDate.Date >= startDate.Date && r.ReturnDate.Date <= endDate.Date)
                .GroupBy(r => r.CashierName)
                .Select(g => new
                {
                    CashierName = g.Key,
                    ReturnCount = g.Count()
                })
                .ToListAsync();

            var riskProfiles = new List<CashierFraudRiskDto>();

            // Step 3: Merge the data in-memory to build the final profiles
            foreach (var stat in salesStats)
            {
                var returnsForCashier = returnStats.FirstOrDefault(r => r.CashierName == stat.CashierName)?.ReturnCount ?? 0;

                riskProfiles.Add(new CashierFraudRiskDto
                {
                    CashierName = stat.CashierName,
                    TotalTransactions = stat.TotalTransactions,
                    VoidCount = stat.VoidCount,
                    ReturnCount = returnsForCashier
                });
            }

            // Order by the most high-risk cashiers first (Highest Void Rate)
            return riskProfiles.OrderByDescending(p => p.VoidRate).ThenByDescending(p => p.ReturnRate).ToList();
        }

        // ==============================================================================
        // 4. MACRO SECURITY SUMMARY (For Top KPI Cards)
        // ==============================================================================
        public async Task<SecurityAuditSummaryDto> GetSecuritySummaryAsync(DateTime startDate, DateTime endDate)
        {
            var voidRecords = await GetVoidRecordsAsync(startDate, endDate);
            var returnRecords = await GetReturnRecordsAsync(startDate, endDate);
            var riskProfiles = await GetCashierRiskProfilesAsync(startDate, endDate);

            return new SecurityAuditSummaryDto
            {
                TotalVoidCount = voidRecords.Count(v => v.Status == "Voided"),
                TotalVoidAmount = voidRecords.Where(v => v.Status == "Voided").Sum(v => v.AttemptedAmount),

                TotalReturnCount = returnRecords.Count,
                TotalReturnAmount = returnRecords.Sum(r => r.RefundAmount),

                SuspendedCartCount = voidRecords.Count(v => v.Status == "Suspended"),

                // Count how many cashiers tripped the internal security wire (e.g., > 5% voids)
                HighRiskCashierCount = riskProfiles.Count(p => p.IsHighRisk)
            };
        }
    }
}