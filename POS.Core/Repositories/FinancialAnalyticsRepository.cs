using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class FinancialAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public FinancialAnalyticsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // 1. MACRO FINANCIAL SUMMARY ENGINE (For the Dashboard KPI Cards)
        // ==============================================================================
        public async Task<FinancialSummaryDto> GetFinancialSummaryAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // 1. Fetch Sales Aggregates
            var salesBase = await context.SalesHeaders
                .AsNoTracking()
                .Where(h => h.TransactionDate.Date >= startDate.Date && h.TransactionDate.Date <= endDate.Date && h.Status == "Completed")
                .Select(h => new
                {
                    h.Id,
                    h.GrossTotal,
                    h.TotalDiscount
                })
                .ToListAsync();

            var completedSaleIds = salesBase.Select(s => s.Id).ToList();

            // 2. Fetch Cost of Goods Sold (COGS) strictly for completed sales
            // 2. Fetch Cost of Goods Sold (COGS) strictly for completed sales
            // ✅ FIX: Double-cast the COGS
            var totalCogs = (decimal)await context.SalesLines
                .AsNoTracking()
                .Where(l => completedSaleIds.Contains(l.SalesHeaderId))
                .SumAsync(l => (double)(l.CostPrice * l.Quantity));

            // 3. Fetch Total Returns (Refunds given back to customers)
            // ✅ FIX: Double-cast the Returns
            var totalReturns = (decimal)await context.CustomerReturnHeaders
                .AsNoTracking()
                .Where(r => r.ReturnDate.Date >= startDate.Date && r.ReturnDate.Date <= endDate.Date)
                .SumAsync(r => (double)r.TotalRefundAmount);

            // 4. Fetch Operating Expenses (Petty cash, payouts, store expenses)
            // ✅ FIX: Double-cast the Expenses and the Math.Abs
            var operatingExpenses = (decimal)await context.CashMovements
                .AsNoTracking()
                .Where(m => m.Timestamp.Date >= startDate.Date && m.Timestamp.Date <= endDate.Date && m.Amount < 0)
                .SumAsync(m => Math.Abs((double)m.Amount));

            return new FinancialSummaryDto
            {
                GrossSales = salesBase.Sum(s => s.GrossTotal),
                TotalDiscounts = salesBase.Sum(s => s.TotalDiscount),
                TotalSalesCount = salesBase.Count,
                TotalReturns = totalReturns,
                TotalCostOfGoods = totalCogs,
                OperatingExpenses = operatingExpenses
            };
        }

        // ==============================================================================
        // 2. TIME-SERIES TREND ENGINE (For LiveCharts Graphs)
        // ==============================================================================
        public async Task<List<FinancialTrendPointDto>> GetFinancialTrendsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // We pull the raw flat data first. We do the grouping in memory because SQLite 
            // sometimes struggles to translate 'GroupBy(TransactionDate.Date)' into pure SQL.
            var rawSalesData = await context.SalesHeaders
                            .AsNoTracking()
                            .Where(h => h.TransactionDate.Date >= startDate.Date && h.TransactionDate.Date <= endDate.Date && h.Status == "Completed")
                            .Select(h => new
                            {
                                SaleDate = h.TransactionDate.Date,
                                Revenue = h.NetTotal,
                                // ✅ FIX: Double-cast the inline cost sum
                                Cost = (decimal)h.SalesLines.Sum(l => (double)(l.CostPrice * l.Quantity))
                            })
                            .ToListAsync();

            var trendData = rawSalesData
                .GroupBy(s => s.SaleDate)
                .OrderBy(g => g.Key)
                .Select(g => new FinancialTrendPointDto
                {
                    Date = g.Key,
                    DateLabel = g.Key.ToString("dd-MMM"),
                    Revenue = g.Sum(x => x.Revenue),
                    Cost = g.Sum(x => x.Cost),
                    TransactionCount = g.Count()
                })
                .ToList();

            // Ensure there are no gaps in the dates for a smooth line chart
            var completeTrendLine = new List<FinancialTrendPointDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var existingPoint = trendData.FirstOrDefault(t => t.Date == date);
                if (existingPoint != null)
                {
                    completeTrendLine.Add(existingPoint);
                }
                else
                {
                    // Add an empty point to keep the chart timeline accurate
                    completeTrendLine.Add(new FinancialTrendPointDto
                    {
                        Date = date,
                        DateLabel = date.ToString("dd-MMM"),
                        Revenue = 0,
                        Cost = 0,
                        TransactionCount = 0
                    });
                }
            }

            return completeTrendLine;
        }
    }
}