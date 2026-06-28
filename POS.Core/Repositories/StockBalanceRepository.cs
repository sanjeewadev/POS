using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class StockBalanceRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public StockBalanceRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<StockBalanceDto>> GetStockBalancesAsync(
            string searchText = "",
            int? categoryId = null,
            int? supplierId = null,
            bool hideZeroStock = false,
            bool showNegativeOnly = false)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string search = (searchText ?? string.Empty).Trim();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                    .ThenInclude(p => p.Category)
                .Include(v => v.ItemParent)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Include(v => v.ItemBatches)
                .Include(v => v.ItemSuppliers)
                    .ThenInclude(s => s.Supplier)
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated)
                .AsQueryable();

            // =========================================================
            // FILTERS
            // =========================================================

            if (!string.IsNullOrWhiteSpace(search))
            {
                string like = $"%{search}%";

                query = query.Where(v =>
                    EF.Functions.Like(v.SkuCode, like) ||
                    EF.Functions.Like(v.Barcode, like) ||
                    EF.Functions.Like(v.ItemParent.ItemCode, like) ||
                    EF.Functions.Like(v.ItemParent.ItemName, like) ||
                    EF.Functions.Like(v.ItemParent.PrintName, like));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.ItemParent.CategoryId == categoryId.Value);
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(v => v.ItemSuppliers.Any(s => s.SupplierId == supplierId.Value));
            }

            var rawData = await query
                .OrderBy(v => v.ItemParent.ItemCode)
                .ThenBy(v => v.VariantDescription)
                .ToListAsync();

            var result = new List<StockBalanceDto>();

            foreach (var variant in rawData)
            {
                var activeBatches = variant.ItemBatches
                    .Where(b => !b.IsDeactivated)
                    .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(b => b.BatchNo)
                    .ToList();

                decimal totalQty = activeBatches.Sum(b => b.CurrentStock);

                if (showNegativeOnly && totalQty >= 0)
                    continue;

                if (hideZeroStock && totalQty == 0)
                    continue;

                decimal totalCostValue = activeBatches.Sum(b => b.CurrentStock * b.CostPrice);
                decimal totalRetailValue = activeBatches.Sum(b => b.CurrentStock * b.RetailPrice);
                decimal totalWholesaleValue = activeBatches.Sum(b => b.CurrentStock * b.WholesalePrice);

                var primarySupplier = variant.ItemSuppliers
                    .Where(s => s.Supplier != null)
                    .OrderByDescending(s => s.IsPrimary)
                    .ThenBy(s => s.Supplier.SupplierName)
                    .FirstOrDefault();

                bool hasExpiredBatch = activeBatches.Any(b =>
                    b.CurrentStock > 0 &&
                    b.ExpiryDate.HasValue &&
                    b.ExpiryDate.Value.Date < DateTime.Now.Date);

                bool hasExpiringSoonBatch = activeBatches.Any(b =>
                    b.CurrentStock > 0 &&
                    b.ExpiryDate.HasValue &&
                    b.ExpiryDate.Value.Date >= DateTime.Now.Date &&
                    b.ExpiryDate.Value.Date <= DateTime.Now.Date.AddDays(30));

                var dto = new StockBalanceDto
                {
                    VariantId = variant.Id,

                    ItemCode = variant.ItemParent?.ItemCode ?? string.Empty,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode,

                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,

                    Description = variant.ItemParent?.ItemName ?? string.Empty,

                    Uom = variant.ItemParent?.UnitOfMeasure?.UomCode
                        ?? variant.ItemParent?.BaseUom
                        ?? "PCS",

                    CategoryName = variant.ItemParent?.Category?.CategoryName ?? string.Empty,

                    PrimarySupplierName = primarySupplier?.Supplier?.SupplierName
                        ?? primarySupplier?.Supplier?.CompanyName
                        ?? string.Empty,

                    TotalQtyOnHand = totalQty,

                    UnitCost = variant.AverageCost > 0
                        ? variant.AverageCost
                        : variant.CostPrice,

                    UnitRetail = variant.RetailPrice,
                    UnitWholesale = variant.WholesalePrice,

                    TotalCostValue = Math.Round(totalCostValue, 2),
                    TotalRetailValue = Math.Round(totalRetailValue, 2),
                    TotalWholesaleValue = Math.Round(totalWholesaleValue, 2),

                    BatchCount = activeBatches.Count,

                    HasExpiredBatch = hasExpiredBatch,
                    HasExpiringSoonBatch = hasExpiringSoonBatch,

                    StockStatus = BuildStockStatus(totalQty, hasExpiredBatch, hasExpiringSoonBatch),

                    EarliestExpiryDate = activeBatches
                        .Where(b => b.CurrentStock > 0 && b.ExpiryDate.HasValue)
                        .Select(b => b.ExpiryDate)
                        .OrderBy(d => d)
                        .FirstOrDefault(),

                    LastReceivedDate = activeBatches
                        .Where(b => b.CurrentStock != 0)
                        .Select(b => (DateTime?)b.ReceivedDate)
                        .OrderByDescending(d => d)
                        .FirstOrDefault(),

                    Batches = activeBatches.Select(b => new ItemBatchDto
                    {
                        BatchId = b.Id,
                        ItemVariantId = b.ItemVariantId,
                        BatchNo = b.BatchNo,
                        ExpiryDate = b.ExpiryDate,
                        ReceivedDate = b.ReceivedDate,
                        CurrentStock = b.CurrentStock,
                        CostPrice = b.CostPrice,
                        RetailPrice = b.RetailPrice,
                        WholesalePrice = b.WholesalePrice,
                        IsDeactivated = b.IsDeactivated
                    }).ToList()
                };

                result.Add(dto);
            }

            return result;
        }

        private static string BuildStockStatus(
            decimal totalQty,
            bool hasExpiredBatch,
            bool hasExpiringSoonBatch)
        {
            if (totalQty < 0)
                return "Negative Stock";

            if (totalQty == 0)
                return "Zero Stock";

            if (hasExpiredBatch)
                return "Expired Batch";

            if (hasExpiringSoonBatch)
                return "Expiring Soon";

            return "In Stock";
        }
    }
}