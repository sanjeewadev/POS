using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    // --- MASTER DTO (The Summary Row) ---
    public class StockBalanceDto
    {
        public int VariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Uom { get; set; } = string.Empty;

        public decimal TotalQtyOnHand { get; set; }
        public decimal TotalCostValue { get; set; }
        public decimal TotalRetailValue { get; set; }

        // The embedded list of distinct batches
        public List<ItemBatchDto> Batches { get; set; } = new();
    }

    // --- DETAIL DTO (The Expanded Row) ---
    public class ItemBatchDto
    {
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal TotalBatchCost => CurrentStock * CostPrice;
    }

    public class StockBalanceRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public StockBalanceRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<StockBalanceDto>> GetStockBalancesAsync(
            string searchText = "",
            int? categoryId = null,
            int? supplierId = null,
            bool hideZeroStock = false,
            bool showNegativeOnly = false)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .Include(v => v.ItemBatches)
                .AsNoTracking()
                .AsQueryable();

            // --- 1. APPLY FILTERS ---
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerSearch = searchText.ToLower();
                query = query.Where(v =>
                    v.SkuCode.ToLower().Contains(lowerSearch) ||
                    v.ItemParent.ItemName.ToLower().Contains(lowerSearch) ||
                    v.ItemParent.ItemCode.ToLower().Contains(lowerSearch));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.ItemParent.CategoryId == categoryId.Value);
            }

            // Note: Uncomment this if you added PrimarySupplierId to ItemParent table in Phase 1
            // if (supplierId.HasValue && supplierId.Value > 0)
            // {
            //     query = query.Where(v => v.ItemParent.PrimarySupplierId == supplierId.Value);
            // }

            // --- 2. FETCH RAW DATA INTO RAM TO BYPASS SQLITE LIMITATIONS ---
            var rawData = await query.ToListAsync();

            var result = new List<StockBalanceDto>();

            // --- 3. C# MATH & PROJECTION (100% Accurate Hybrid Batch Logic) ---
            foreach (var variant in rawData)
            {
                var activeBatches = variant.ItemBatches.Where(b => !b.IsDeactivated).ToList();
                decimal totalQty = activeBatches.Sum(b => b.CurrentStock);

                // Apply Stock Filters in RAM
                if (showNegativeOnly && totalQty >= 0) continue;
                if (hideZeroStock && totalQty == 0) continue;

                var dto = new StockBalanceDto
                {
                    VariantId = variant.Id,
                    ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                    VariantDescription = variant.VariantDescription,
                    Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                    Uom = variant.ItemParent?.BaseUom ?? "PCS",
                    TotalQtyOnHand = totalQty,
                    TotalCostValue = activeBatches.Sum(b => b.CurrentStock * b.CostPrice),
                    TotalRetailValue = activeBatches.Sum(b => b.CurrentStock * b.RetailPrice),

                    Batches = activeBatches.Select(b => new ItemBatchDto
                    {
                        BatchNo = b.BatchNo,
                        ExpiryDate = b.ExpiryDate,
                        CurrentStock = b.CurrentStock,
                        CostPrice = b.CostPrice,
                        RetailPrice = b.RetailPrice
                    }).ToList()
                };

                result.Add(dto);
            }

            return result;
        }
    }
}