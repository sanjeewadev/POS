using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class PriceManagementRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PriceManagementRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // MASTER GRID
        // =========================================================

        public async Task<List<PriceManagementSummaryDto>> GetPricingSummariesAsync(
            string marginFilter = "All",
            string expiryFilter = "All",
            string searchText = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string search = NormalizeText(searchText).ToUpperInvariant();

            var query = context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(v =>
                    v.ItemParent.ItemCode.ToUpper().Contains(search) ||
                    v.ItemParent.ItemName.ToUpper().Contains(search) ||
                    v.SkuCode.ToUpper().Contains(search) ||
                    v.Barcode.ToUpper().Contains(search));
            }

            var variants = await query
                .Select(v => new
                {
                    v.Id,
                    v.SkuCode,
                    v.Barcode,
                    v.VariantDescription,
                    v.AverageCost,
                    v.CostPrice,
                    v.RetailPrice,
                    v.WholesalePrice,
                    v.MinimumPrice,
                    v.MaximumPrice,
                    ItemCode = v.ItemParent.ItemCode,
                    ItemName = v.ItemParent.ItemName
                })
                .ToListAsync();

            if (!variants.Any())
                return new List<PriceManagementSummaryDto>();

            var variantIds = variants
                .Select(v => v.Id)
                .ToList();

            var batchRows = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    variantIds.Contains(b.ItemVariantId) &&
                    !b.IsDeactivated)
                .Select(b => new
                {
                    b.ItemVariantId,
                    b.CurrentStock,
                    b.ExpiryDate
                })
                .ToListAsync();

            // SQLite decimal-safe aggregation.
            // Do NOT use EF-side Sum() for decimal with SQLite.
            var stockByVariant = batchRows
                .Where(b => b.CurrentStock > 0)
                .GroupBy(b => b.ItemVariantId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.CurrentStock));

            DateTime expiryThreshold = DateTime.Today.AddDays(30);

            var expiringVariantIds = batchRows
                .Where(b =>
                    b.CurrentStock > 0 &&
                    b.ExpiryDate.HasValue &&
                    b.ExpiryDate.Value.Date <= expiryThreshold)
                .Select(b => b.ItemVariantId)
                .Distinct()
                .ToHashSet();

            var results = new List<PriceManagementSummaryDto>();

            foreach (var variant in variants)
            {
                decimal totalSoh = stockByVariant.TryGetValue(variant.Id, out decimal stock)
                    ? stock
                    : 0m;

                var dto = new PriceManagementSummaryDto
                {
                    ItemVariantId = variant.Id,
                    ItemCode = variant.ItemCode,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,
                    Description = variant.ItemName,
                    VariantAttributes = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,

                    TotalSoh = totalSoh,
                    MovingAverageCost = variant.AverageCost,
                    LastLandedCost = variant.CostPrice,

                    RetailPrice = variant.RetailPrice,
                    WholesalePrice = variant.WholesalePrice,
                    MinimumPrice = variant.MinimumPrice,
                    MaximumPrice = variant.MaximumPrice
                };

                dto.AcceptChanges();

                results.Add(dto);
            }

            if (expiryFilter == "Expiring Soon")
            {
                results = results
                    .Where(r => expiringVariantIds.Contains(r.ItemVariantId))
                    .ToList();
            }

            if (marginFilter == "Low Margin Alerts (< 20%)")
            {
                results = results
                    .Where(r => r.GrossMarginPercentage < 20m)
                    .ToList();
            }
            else if (marginFilter == "Healthy Margins")
            {
                results = results
                    .Where(r => r.GrossMarginPercentage >= 20m)
                    .ToList();
            }

            return results
                .OrderBy(r => r.Description)
                .ThenBy(r => r.VariantAttributes)
                .ThenBy(r => r.ItemCode)
                .ToList();
        }

        // =========================================================
        // BATCH INSPECTOR - NEW SAFE DTO VERSION
        // =========================================================

        public async Task<List<PriceManagementBatchDto>> GetActiveBatchPriceRowsAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<PriceManagementBatchDto>();

            using var context = await _contextFactory.CreateDbContextAsync();

            var rows = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock > 0)
                .OrderBy(b => b.ExpiryDate.HasValue ? b.ExpiryDate.Value : DateTime.MaxValue)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchNo)
                .Select(b => new PriceManagementBatchDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    CurrentStock = b.CurrentStock,
                    CostPrice = b.CostPrice,
                    RetailPrice = b.RetailPrice,
                    WholesalePrice = b.WholesalePrice
                })
                .ToListAsync();

            foreach (var row in rows)
                row.AcceptChanges();

            return rows;
        }

        // =========================================================
        // BATCH INSPECTOR - COMPATIBILITY VERSION
        // Keeps your current ViewModel compiling until we replace it.
        // =========================================================

        public async Task<List<ItemBatch>> GetActiveBatchesAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<ItemBatch>();

            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock > 0)
                .OrderBy(b => b.ExpiryDate.HasValue ? b.ExpiryDate.Value : DateTime.MaxValue)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchNo)
                .ToListAsync();
        }

        // =========================================================
        // SAVE PRICING - NEW SAFE DTO VERSION
        // =========================================================

        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto masterPricing,
            List<PriceManagementBatchDto> batchOverrides,
            string updatedBy = "Admin")
        {
            if (masterPricing == null)
                throw new ArgumentNullException(nameof(masterPricing));

            batchOverrides ??= new List<PriceManagementBatchDto>();

            ValidateMasterPricing(masterPricing);

            foreach (var batch in batchOverrides)
                ValidateBatchPricing(batch);

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;

                var variant = await context.ItemVariants
                    .Include(v => v.ItemParent)
                    .FirstOrDefaultAsync(v =>
                        v.Id == masterPricing.ItemVariantId &&
                        !v.IsDeactivated &&
                        !v.ItemParent.IsDeactivated);

                if (variant == null)
                    throw new InvalidOperationException("Selected item variant was not found or is inactive.");

                variant.RetailPrice = RoundMoney(masterPricing.RetailPrice);
                variant.WholesalePrice = RoundMoney(masterPricing.WholesalePrice);
                variant.MinimumPrice = RoundMoney(masterPricing.MinimumPrice);
                variant.MaximumPrice = RoundMoney(masterPricing.MaximumPrice);
                variant.UpdatedAt = now;

                var batchIds = batchOverrides
                    .Where(b => b.ItemBatchId > 0)
                    .Select(b => b.ItemBatchId)
                    .Distinct()
                    .ToList();

                if (batchIds.Any())
                {
                    var dbBatches = await context.ItemBatches
                        .Where(b =>
                            batchIds.Contains(b.Id) &&
                            b.ItemVariantId == variant.Id &&
                            !b.IsDeactivated)
                        .ToDictionaryAsync(b => b.Id);

                    foreach (var batchDto in batchOverrides)
                    {
                        if (!dbBatches.TryGetValue(batchDto.ItemBatchId, out var dbBatch))
                            throw new InvalidOperationException($"Batch '{batchDto.BatchNo}' was not found or does not belong to the selected item.");

                        dbBatch.RetailPrice = RoundMoney(batchDto.RetailPrice);
                        dbBatch.WholesalePrice = RoundMoney(batchDto.WholesalePrice);
                        dbBatch.UpdatedAt = now;
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                masterPricing.AcceptChanges();

                foreach (var batch in batchOverrides)
                    batch.AcceptChanges();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // SAVE PRICING - COMPATIBILITY VERSION
        // Keeps your current ViewModel compiling until we replace it.
        // =========================================================

        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto masterPricing,
            List<ItemBatch> batchOverrides)
        {
            batchOverrides ??= new List<ItemBatch>();

            var batchDtos = batchOverrides
                .Select(b => new PriceManagementBatchDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    CurrentStock = b.CurrentStock,
                    CostPrice = b.CostPrice,
                    RetailPrice = b.RetailPrice,
                    WholesalePrice = b.WholesalePrice
                })
                .ToList();

            await UpdatePricingAsync(masterPricing, batchDtos, "Admin");
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private static void ValidateMasterPricing(PriceManagementSummaryDto pricing)
        {
            var errors = pricing.ValidateForSave();

            if (errors.Any())
            {
                throw new InvalidOperationException(
                    "Price validation failed:\n" + string.Join("\n", errors));
            }
        }

        private static void ValidateBatchPricing(PriceManagementBatchDto batch)
        {
            var errors = batch.ValidateForSave();

            if (errors.Any())
            {
                throw new InvalidOperationException(
                    "Batch price validation failed:\n" + string.Join("\n", errors));
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2);
        }
    }
}