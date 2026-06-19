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

        // ==============================================================================
        // 1. AGED PAYABLES
        // ==============================================================================
        public async Task<List<AgedPayableDto>> GetAgedPayablesSummaryAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var today = DateTime.Today;

            var date30 = today.AddDays(-30);
            var date60 = today.AddDays(-60);
            var date90 = today.AddDays(-90);

            // Fetch to memory first to avoid SQLite decimal sum errors
            var rawData = await context.GrnHeaders
                .AsNoTracking()
                .Include(g => g.Supplier)
                .Where(g => g.NetPayable > 0 && !g.Supplier.IsDeactivated && g.Status == "Posted")
                .ToListAsync();

            var agedPayables = rawData
                .GroupBy(g => new { g.SupplierId, g.Supplier.CompanyName })
                .Select(group => new AgedPayableDto
                {
                    SupplierId = group.Key.SupplierId,
                    SupplierName = group.Key.CompanyName,

                    CurrentTo30Days = group.Where(g => g.ReceivedDate >= date30).Sum(g => g.NetPayable),
                    Days31To60 = group.Where(g => g.ReceivedDate < date30 && g.ReceivedDate >= date60).Sum(g => g.NetPayable),
                    Days61To90 = group.Where(g => g.ReceivedDate < date60 && g.ReceivedDate >= date90).Sum(g => g.NetPayable),
                    Over90Days = group.Where(g => g.ReceivedDate < date90).Sum(g => g.NetPayable)
                })
                .Where(dto => dto.TotalOwed > 0)
                .OrderByDescending(dto => dto.TotalOwed)
                .ToList();

            return agedPayables;
        }

        // ==============================================================================
        // 2. SUPPLIER PURCHASING VOLUME
        // ==============================================================================
        public async Task<List<SupplierVolumeDto>> GetPurchasingVolumeAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Fetch only required columns to memory first
            var rawVolumeData = await context.GrnHeaders
                .AsNoTracking()
                .Where(g => g.ReceivedDate >= startDate && g.ReceivedDate <= endDate && g.Status == "Posted")
                .Select(g => new { g.SupplierId, g.Supplier.CompanyName, g.NetPayable })
                .ToListAsync();

            // Calculate total company purchases in memory
            var totalCompanyPurchases = rawVolumeData.Sum(g => g.NetPayable);

            if (totalCompanyPurchases == 0) return new List<SupplierVolumeDto>();

            // Group and calculate percentages in memory
            var volumeData = rawVolumeData
                .GroupBy(g => new { g.SupplierId, g.CompanyName })
                .Select(group => new
                {
                    SupplierId = group.Key.SupplierId,
                    SupplierName = group.Key.CompanyName,
                    TotalGrnValue = group.Sum(g => g.NetPayable)
                })
                .OrderByDescending(x => x.TotalGrnValue)
                .Take(20)
                .ToList();

            return volumeData.Select(v => new SupplierVolumeDto
            {
                SupplierId = v.SupplierId,
                SupplierName = v.SupplierName,
                TotalGrnValue = v.TotalGrnValue,
                PercentageOfTotalStore = Math.Round((double)(v.TotalGrnValue / totalCompanyPurchases) * 100, 2)
            }).ToList();
        }

        // ==============================================================================
        // 3. RETURN & DEFECT RATES
        // ==============================================================================
        public async Task<List<SupplierReturnRateDto>> GetReturnRatesAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Fetch raw bought data to memory
            var rawBoughtData = await context.GrnLines
                .AsNoTracking()
                .Where(l => l.GrnHeader.ReceivedDate >= startDate && l.GrnHeader.ReceivedDate <= endDate && l.GrnHeader.Status == "Posted")
                .Select(l => new { l.GrnHeader.SupplierId, l.GrnHeader.Supplier.CompanyName, l.ReceivedQty })
                .ToListAsync();

            var boughtData = rawBoughtData
                .GroupBy(l => new { l.SupplierId, l.CompanyName })
                .Select(g => new
                {
                    SupplierId = g.Key.SupplierId,
                    SupplierName = g.Key.CompanyName,
                    BoughtQty = g.Sum(l => l.ReceivedQty)
                })
                .ToDictionary(x => x.SupplierId);

            // Fetch raw return data to memory
            var rawReturnData = await context.ReturnLines
                .AsNoTracking()
                .Where(l => l.ReturnHeader.ReturnDate >= startDate && l.ReturnHeader.ReturnDate <= endDate && l.ReturnHeader.Status == "Posted")
                .Select(l => new { l.ReturnHeader.SupplierId, l.ReturnQty })
                .ToListAsync();

            var returnData = rawReturnData
                .GroupBy(l => l.SupplierId)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    ReturnedQty = g.Sum(l => l.ReturnQty)
                })
                .ToDictionary(x => x.SupplierId);

            var resultList = new List<SupplierReturnRateDto>();

            // Merge dictionaries
            foreach (var kvp in boughtData)
            {
                var supplierId = kvp.Key;
                var bought = kvp.Value.BoughtQty;
                var returned = returnData.ContainsKey(supplierId) ? returnData[supplierId].ReturnedQty : 0;

                resultList.Add(new SupplierReturnRateDto
                {
                    SupplierId = supplierId,
                    SupplierName = kvp.Value.SupplierName,
                    TotalItemsBought = bought,
                    TotalItemsReturned = returned
                });
            }

            return resultList
                .OrderByDescending(r => r.DefectPercentage)
                .Where(r => r.TotalItemsBought > 0)
                .ToList();
        }
    }
}