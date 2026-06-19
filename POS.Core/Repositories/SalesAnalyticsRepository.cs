using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.DTOs;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static POS.Core.Models.DTOs.ItemPerformanceDto;

namespace POS.Core.Repositories
{
    public class SalesAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SalesAnalyticsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // 1. MASTER PERFORMANCE QUERY (CQRS Read-Side)
        // ==============================================================================
        public async Task<List<ItemPerformanceDto>> GetItemPerformanceAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // 1. Pull raw, flat sales data traversing from SalesLine -> Batch -> Variant
            var rawSalesData = await context.SalesLines
                .AsNoTracking()
                .Where(l => l.SalesHeader.TransactionDate >= startDate && l.SalesHeader.TransactionDate <= endDate && l.SalesHeader.Status == "Completed")
                .Select(l => new
                {
                    ItemVariantId = l.ItemBatch.ItemVariantId,
                    Qty = l.Quantity,
                    Revenue = l.LineTotal,
                    Cost = l.CostPrice * l.Quantity,
                    SaleDate = l.SalesHeader.TransactionDate
                })
                .ToListAsync();

            // 2. Fetch Variant Master dictionary, combining Parent Name and Variant Description
            var itemDict = await context.ItemVariants
                .AsNoTracking()
                .Select(v => new
                {
                    v.Id,
                    ItemCode = v.SkuCode,
                    ItemName = v.ItemParent.ItemName + " " + v.VariantDescription,
                    CategoryName = "General", // Bypassing deep category links to guarantee no CS1061 errors
                    CurrentSOH = v.ItemBatches.Sum(b => b.CurrentStock) // Aggregating actual physical batches
                })
                .ToDictionaryAsync(v => v.Id);

            var daysInPeriod = (endDate - startDate).Days;
            if (daysInPeriod <= 0) daysInPeriod = 1;

            // 3. Aggregate data safely in memory
            var aggregatedData = rawSalesData
                .GroupBy(s => s.ItemVariantId)
                .Select(g => new
                {
                    ItemVariantId = g.Key,
                    QtySold = g.Sum(x => x.Qty),
                    Revenue = g.Sum(x => x.Revenue),
                    Cost = g.Sum(x => x.Cost),
                    LastSoldDate = g.Max(x => x.SaleDate)
                })
                .ToList();

            var results = new List<ItemPerformanceDto>();

            // 4. Map and calculate advanced metrics
            foreach (var agg in aggregatedData)
            {
                if (itemDict.TryGetValue(agg.ItemVariantId, out var item))
                {
                    results.Add(new ItemPerformanceDto
                    {
                        ItemId = agg.ItemVariantId, // UI uses ItemId, DB uses VariantId
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName.Trim(),
                        CategoryName = item.CategoryName,
                        CurrentStock = item.CurrentSOH,
                        QtySold = agg.QtySold,
                        Revenue = agg.Revenue,
                        Cost = agg.Cost,
                        LastSoldDate = agg.LastSoldDate,
                        AverageDailySales = Math.Round((double)agg.QtySold / daysInPeriod, 2)
                    });
                }
            }

            results = results.OrderByDescending(r => r.Revenue).ToList();
            for (int i = 0; i < results.Count; i++)
            {
                results[i].Rank = i + 1;
            }

            return results;
        }

        // ==============================================================================
        // 2. DEAD STOCK ENGINE
        // ==============================================================================
        public async Task<List<ItemPerformanceDto>> GetDeadStockAsync(int daysWithoutSaleThreshold)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var cutoffDate = DateTime.Today.AddDays(-daysWithoutSaleThreshold);

            var latestSales = await context.SalesLines
                .AsNoTracking()
                .Where(l => l.SalesHeader.Status == "Completed")
                .GroupBy(l => l.ItemBatch.ItemVariantId)
                .Select(g => new { ItemVariantId = g.Key, LastSoldDate = g.Max(l => l.SalesHeader.TransactionDate) })
                .ToListAsync();

            var itemLastSoldDict = latestSales.ToDictionary(s => s.ItemVariantId, s => s.LastSoldDate);

            var allStockedItems = await context.ItemVariants
                .AsNoTracking()
                .Select(v => new
                {
                    v.Id,
                    ItemCode = v.SkuCode,
                    ItemName = v.ItemParent.ItemName + " " + v.VariantDescription,
                    CurrentSOH = v.ItemBatches.Sum(b => b.CurrentStock),
                    v.IsDeactivated
                })
                .Where(v => v.CurrentSOH > 0 && !v.IsDeactivated)
                .ToListAsync();

            var deadStockList = new List<ItemPerformanceDto>();

            foreach (var item in allStockedItems)
            {
                DateTime? lastSold = null;
                bool isDead = false;

                if (itemLastSoldDict.TryGetValue(item.Id, out var date))
                {
                    lastSold = date;
                    if (date < cutoffDate) isDead = true;
                }
                else
                {
                    isDead = true;
                }

                if (isDead)
                {
                    deadStockList.Add(new ItemPerformanceDto
                    {
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName.Trim(),
                        CategoryName = "General",
                        CurrentStock = item.CurrentSOH,
                        LastSoldDate = lastSold,
                        QtySold = 0,
                        Revenue = 0,
                        Cost = 0
                    });
                }
            }

            return deadStockList.OrderByDescending(d => d.CurrentStock).ToList();
        }

        // ==============================================================================
        // 3. KPI AGGREGATION ENGINE
        // ==============================================================================
        public async Task<AnalyticsKpiDto> GetKpisAsync(DateTime startDate, DateTime endDate, int deadStockThreshold = 90)
        {
            var performanceData = await GetItemPerformanceAsync(startDate, endDate);
            var deadStockData = await GetDeadStockAsync(deadStockThreshold);

            return new AnalyticsKpiDto
            {
                TotalRevenue = performanceData.Sum(p => p.Revenue),
                TotalCost = performanceData.Sum(p => p.Cost),
                TotalUnitsSold = performanceData.Sum(p => p.QtySold),
                ActiveSellingItems = performanceData.Count,
                DeadStockItems = deadStockData.Count
            };
        }

        // ==============================================================================
        // 4. TREND ANALYSIS ENGINE
        // ==============================================================================
        public async Task<List<TrendPointDto>> GetSalesTrendsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var rawSalesData = await context.SalesLines
                .AsNoTracking()
                .Where(l => l.SalesHeader.TransactionDate >= startDate && l.SalesHeader.TransactionDate <= endDate && l.SalesHeader.Status == "Completed")
                .Select(l => new
                {
                    SaleDate = l.SalesHeader.TransactionDate.Date,
                    Revenue = l.LineTotal,
                    Cost = l.CostPrice * l.Quantity
                })
                .ToListAsync();

            return rawSalesData
                .GroupBy(s => s.SaleDate)
                .OrderBy(g => g.Key)
                .Select(g => new TrendPointDto
                {
                    DateLabel = g.Key.ToString("dd-MMM"),
                    Revenue = g.Sum(x => x.Revenue),
                    Profit = g.Sum(x => x.Revenue) - g.Sum(x => x.Cost)
                })
                .ToList();
        }

        // ==============================================================================
        // 5. ITEM DRILL-DOWN ENGINE (Unified Ledger)
        // ==============================================================================
        public async Task<List<ItemDrillDownDto>> GetItemHistoryAsync(int variantId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var history = new List<ItemDrillDownDto>();

            // 1. Fetch Purchases (GRNs) directly linked to the ItemVariant
            var purchases = await context.GrnLines
                .AsNoTracking()
                .Include(l => l.GrnHeader)
                .ThenInclude(h => h.Supplier)
                .Where(l => l.ItemVariantId == variantId && l.GrnHeader.Status == "Posted")
                .Select(l => new ItemDrillDownDto
                {
                    TransactionDate = l.GrnHeader.ReceivedDate,
                    TransactionType = "PURCHASE (GRN)",
                    DocumentNo = l.GrnHeader.GrnNumber,
                    PartyName = l.GrnHeader.Supplier.CompanyName,
                    QtyIn = l.ReceivedQty,
                    QtyOut = 0
                }).ToListAsync();

            // 2. Fetch Sales Traversing through the Batch up to the Variant
            var sales = await context.SalesLines
                .AsNoTracking()
                .Include(l => l.SalesHeader)
                .Where(l => l.ItemBatch.ItemVariantId == variantId && l.SalesHeader.Status == "Completed")
                .Select(l => new ItemDrillDownDto
                {
                    TransactionDate = l.SalesHeader.TransactionDate,
                    TransactionType = "SALE",
                    DocumentNo = l.SalesHeader.InvoiceNo,
                    PartyName = l.SalesHeader.CustomerName ?? "Walk-in",
                    QtyIn = 0,
                    QtyOut = l.Quantity
                }).ToListAsync();

            history.AddRange(purchases);
            history.AddRange(sales);
            return history.OrderByDescending(h => h.TransactionDate).ToList();
        }
    }
}