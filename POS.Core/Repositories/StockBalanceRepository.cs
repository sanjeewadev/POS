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
        private const string GeneralBatchNo = "GENERAL";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public StockBalanceRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<StockBalanceDto>> GetStockBalancesAsync(
            string searchText = "",
            int? categoryId = null,
            int? supplierId = null,
            bool hideZeroStock = false,
            bool showNegativeOnly = false)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            string search = NormalizeText(searchText);

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

            if (!string.IsNullOrWhiteSpace(search))
            {
                string like = $"%{search}%";

                query = query.Where(v =>
                    EF.Functions.Like(v.SkuCode, like) ||
                    EF.Functions.Like(v.Barcode ?? string.Empty, like) ||
                    EF.Functions.Like(v.ItemParent.ItemCode, like) ||
                    EF.Functions.Like(v.ItemParent.ItemName, like) ||
                    EF.Functions.Like(v.ItemParent.PrintName, like) ||
                    v.ItemBatches.Any(b =>
                        EF.Functions.Like(b.BatchNo, like) ||
                        EF.Functions.Like(b.InternalBatchBarcode ?? string.Empty, like)));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.ItemParent.CategoryId == categoryId.Value);
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(v => v.ItemSuppliers.Any(s => s.SupplierId == supplierId.Value));
            }

            var variants = await query
                .OrderBy(v => v.ItemParent.ItemCode)
                .ThenBy(v => v.VariantDescription)
                .ThenBy(v => v.SkuCode)
                .ToListAsync();

            var result = new List<StockBalanceDto>();

            foreach (var variant in variants)
            {
                bool hasBatchTracking = variant.ItemParent?.HasBatchTracking ?? true;
                bool hasExpiryTracking =
                    (variant.ItemParent?.HasExpiryTracking ?? false) ||
                    (variant.ItemParent?.HasBatchExpiry ?? false);

                var stockRows = GetStockRowsForVariant(variant.ItemBatches, hasBatchTracking);

                decimal totalQty = stockRows.Sum(b => b.CurrentStock);

                if (showNegativeOnly && totalQty >= 0m)
                    continue;

                if (hideZeroStock && totalQty == 0m)
                    continue;

                decimal totalCostValue = CalculateTotalCostValue(
                    stockRows,
                    hasBatchTracking,
                    totalQty,
                    variant.AverageCost,
                    variant.CostPrice);

                decimal totalRetailValue = CalculateTotalRetailValue(
                    stockRows,
                    totalQty,
                    variant.RetailPrice);

                decimal totalWholesaleValue = CalculateTotalWholesaleValue(
                    stockRows,
                    totalQty,
                    variant.WholesalePrice);

                decimal unitCost = CalculateUnitValue(
                    totalCostValue,
                    totalQty,
                    variant.AverageCost > 0m ? variant.AverageCost : variant.CostPrice);

                decimal unitRetail = CalculateUnitValue(
                    totalRetailValue,
                    totalQty,
                    variant.RetailPrice);

                decimal unitWholesale = CalculateUnitValue(
                    totalWholesaleValue,
                    totalQty,
                    variant.WholesalePrice);

                var primarySupplier = variant.ItemSuppliers?
                    .Where(s => s.Supplier != null)
                    .OrderByDescending(s => s.IsPrimary)
                    .ThenBy(s => s.Supplier!.SupplierName)
                    .FirstOrDefault();

                bool hasExpiredBatch = hasBatchTracking && stockRows.Any(b =>
                    b.CurrentStock > 0m &&
                    b.ExpiryDate.HasValue &&
                    b.ExpiryDate.Value.Date < DateTime.Today);

                bool hasExpiringSoonBatch = hasBatchTracking && stockRows.Any(b =>
                    b.CurrentStock > 0m &&
                    b.ExpiryDate.HasValue &&
                    b.ExpiryDate.Value.Date >= DateTime.Today &&
                    b.ExpiryDate.Value.Date <= DateTime.Today.AddDays(30));

                var dto = new StockBalanceDto
                {
                    ParentId = variant.ItemParentId,
                    VariantId = variant.Id,

                    ItemCode = variant.ItemParent?.ItemCode ?? string.Empty,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,

                    Description = variant.ItemParent?.ItemName ?? string.Empty,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,

                    Uom = variant.ItemParent?.UnitOfMeasure?.UomCode
                        ?? variant.ItemParent?.BaseUom
                        ?? "PCS",

                    CategoryName = variant.ItemParent?.Category?.CategoryName ?? string.Empty,

                    PrimarySupplierName = primarySupplier?.Supplier?.SupplierName
                        ?? primarySupplier?.Supplier?.CompanyName
                        ?? string.Empty,

                    HasBatchTracking = hasBatchTracking,
                    HasExpiryTracking = hasBatchTracking && hasExpiryTracking,

                    TotalQtyOnHand = totalQty,

                    UnitCost = Math.Round(unitCost, 2),
                    UnitRetail = Math.Round(unitRetail, 2),
                    UnitWholesale = Math.Round(unitWholesale, 2),

                    TotalCostValue = Math.Round(totalCostValue, 2),
                    TotalRetailValue = Math.Round(totalRetailValue, 2),
                    TotalWholesaleValue = Math.Round(totalWholesaleValue, 2),

                    BatchCount = hasBatchTracking
                        ? stockRows.Count
                        : 0,

                    StockBucketCount = hasBatchTracking
                        ? stockRows.Count
                        : stockRows.Count,

                    HasExpiredBatch = hasExpiredBatch,
                    HasExpiringSoonBatch = hasExpiringSoonBatch,

                    StockStatus = BuildStockStatus(
                        totalQty,
                        hasBatchTracking,
                        hasExpiredBatch,
                        hasExpiringSoonBatch),

                    EarliestExpiryDate = hasBatchTracking
                        ? stockRows
                            .Where(b => b.CurrentStock > 0m && b.ExpiryDate.HasValue)
                            .Select(b => b.ExpiryDate)
                            .OrderBy(d => d)
                            .FirstOrDefault()
                        : null,

                    LastReceivedDate = stockRows
                        .Where(b => b.CurrentStock != 0m)
                        .Select(b => (DateTime?)b.ReceivedDate)
                        .OrderByDescending(d => d)
                        .FirstOrDefault(),

                    Batches = stockRows
                        .Select(b => new ItemBatchDto
                        {
                            BatchId = b.Id,
                            ItemVariantId = b.ItemVariantId,
                            BatchNo = string.IsNullOrWhiteSpace(b.BatchNo)
                                ? (hasBatchTracking ? string.Empty : GeneralBatchNo)
                                : b.BatchNo,
                            InternalBatchBarcode = hasBatchTracking
                                ? b.InternalBatchBarcode ?? string.Empty
                                : string.Empty,
                            IsGeneralStockBucket = !hasBatchTracking || IsGeneralBatch(b.BatchNo),
                            ExpiryDate = hasBatchTracking ? b.ExpiryDate : null,
                            ReceivedDate = b.ReceivedDate,
                            CurrentStock = b.CurrentStock,
                            CostPrice = hasBatchTracking
                                ? b.CostPrice
                                : unitCost,
                            RetailPrice = b.RetailPrice > 0m
                                ? b.RetailPrice
                                : variant.RetailPrice,
                            WholesalePrice = b.WholesalePrice > 0m
                                ? b.WholesalePrice
                                : variant.WholesalePrice,
                            IsDeactivated = b.IsDeactivated,
                            BarcodePrintedCount = hasBatchTracking ? b.BarcodePrintedCount : 0,
                            LastBarcodePrintedAt = hasBatchTracking ? b.LastBarcodePrintedAt : null,
                            LastBarcodePrintedBy = hasBatchTracking ? b.LastBarcodePrintedBy ?? string.Empty : string.Empty
                        })
                        .OrderBy(b => b.IsGeneralStockBucket ? 0 : 1)
                        .ThenBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                        .ThenBy(b => b.ReceivedDate)
                        .ThenBy(b => b.BatchNo)
                        .ToList()
                };

                result.Add(dto);
            }

            return result;
        }

        private static List<POS.Core.Models.ItemBatch> GetStockRowsForVariant(
            IEnumerable<POS.Core.Models.ItemBatch>? sourceBatches,
            bool hasBatchTracking)
        {
            var batches = sourceBatches?
                .Where(b => !b.IsDeactivated)
                .ToList() ?? new List<POS.Core.Models.ItemBatch>();

            if (!batches.Any())
                return batches;

            if (hasBatchTracking)
            {
                return batches
                    .Where(b => !IsGeneralBatch(b.BatchNo))
                    .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(b => b.ReceivedDate)
                    .ThenBy(b => b.BatchNo)
                    .ToList();
            }

            return batches
                .OrderByDescending(b => IsGeneralBatch(b.BatchNo))
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.Id)
                .ToList();
        }

        private static decimal CalculateTotalCostValue(
            List<POS.Core.Models.ItemBatch> stockRows,
            bool hasBatchTracking,
            decimal totalQty,
            decimal averageCost,
            decimal fallbackCost)
        {
            if (!stockRows.Any())
                return totalQty * GetBestCost(averageCost, fallbackCost);

            if (!hasBatchTracking)
            {
                decimal cost = GetBestAverageCost(stockRows, averageCost, fallbackCost);
                return totalQty * cost;
            }

            return stockRows.Sum(b => b.CurrentStock * b.CostPrice);
        }

        private static decimal CalculateTotalRetailValue(
            List<POS.Core.Models.ItemBatch> stockRows,
            decimal totalQty,
            decimal fallbackRetail)
        {
            if (!stockRows.Any())
                return totalQty * fallbackRetail;

            return stockRows.Sum(b => b.CurrentStock * (b.RetailPrice > 0m ? b.RetailPrice : fallbackRetail));
        }

        private static decimal CalculateTotalWholesaleValue(
            List<POS.Core.Models.ItemBatch> stockRows,
            decimal totalQty,
            decimal fallbackWholesale)
        {
            if (!stockRows.Any())
                return totalQty * fallbackWholesale;

            return stockRows.Sum(b => b.CurrentStock * (b.WholesalePrice > 0m ? b.WholesalePrice : fallbackWholesale));
        }

        private static decimal CalculateUnitValue(
            decimal totalValue,
            decimal totalQty,
            decimal fallbackUnitValue)
        {
            if (totalQty == 0m)
                return fallbackUnitValue;

            return totalValue / totalQty;
        }

        private static decimal GetBestAverageCost(
            List<POS.Core.Models.ItemBatch> stockRows,
            decimal averageCost,
            decimal fallbackCost)
        {
            if (averageCost > 0m)
                return averageCost;

            decimal totalQty = stockRows.Sum(b => b.CurrentStock);
            decimal totalValue = stockRows.Sum(b => b.CurrentStock * b.CostPrice);

            if (totalQty != 0m && totalValue != 0m)
                return totalValue / totalQty;

            return GetBestCost(averageCost, fallbackCost);
        }

        private static decimal GetBestCost(decimal averageCost, decimal fallbackCost)
        {
            if (averageCost > 0m)
                return averageCost;

            return fallbackCost;
        }

        private static string BuildStockStatus(
            decimal totalQty,
            bool hasBatchTracking,
            bool hasExpiredBatch,
            bool hasExpiringSoonBatch)
        {
            if (totalQty < 0m)
                return "Negative Stock";

            if (totalQty == 0m)
                return "Zero Stock";

            if (hasBatchTracking && hasExpiredBatch)
                return "Expired Batch";

            if (hasBatchTracking && hasExpiringSoonBatch)
                return "Expiring Soon";

            return "In Stock";
        }

        private static bool IsGeneralBatch(string? batchNo)
        {
            return string.Equals(
                NormalizeText(batchNo),
                GeneralBatchNo,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
