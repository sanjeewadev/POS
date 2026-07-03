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
                    (v.Barcode ?? string.Empty).ToUpper().Contains(search));
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

                    MovingAverageCost = RoundMoney(variant.AverageCost),
                    LastLandedCost = RoundMoney(variant.CostPrice),

                    RetailPrice = RoundMoney(variant.RetailPrice),
                    WholesalePrice = RoundMoney(variant.WholesalePrice),
                    MinimumPrice = RoundMoney(variant.MinimumPrice),
                    MaximumPrice = RoundMoney(variant.MaximumPrice)
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
                    .Where(r =>
                        r.MarginHealth == "Negative Margin" ||
                        r.MarginHealth == "Low Margin" ||
                        r.MarginHealth == "Below Minimum" ||
                        r.MarginHealth == "No Retail Price" ||
                        r.MarginHealth == "Cost Missing")
                    .ToList();
            }
            else if (marginFilter == "Healthy Margins")
            {
                results = results
                    .Where(r => r.MarginHealth == "Healthy")
                    .ToList();
            }

            return results
                .OrderBy(r => r.Description)
                .ThenBy(r => r.VariantAttributes)
                .ThenBy(r => r.ItemCode)
                .ToList();
        }

        // =========================================================
        // BATCH INSPECTOR
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
                    CostPrice = RoundMoney(b.CostPrice),
                    RetailPrice = RoundMoney(b.RetailPrice),
                    WholesalePrice = RoundMoney(b.WholesalePrice)
                })
                .ToListAsync();

            foreach (var row in rows)
                row.AcceptChanges();

            return rows;
        }

        // Compatibility method. Keep it for older code until all pages are cleaned.
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
        // SAVE PRICING WITH HISTORY
        // =========================================================

        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto masterPricing,
            List<PriceManagementBatchDto> batchOverrides,
            string updatedBy = "Admin",
            string changeReason = "Price updated from Price Management page",
            string reasonCode = "")
        {
            if (masterPricing == null)
                throw new ArgumentNullException(nameof(masterPricing));

            batchOverrides ??= new List<PriceManagementBatchDto>();

            updatedBy = NormalizeText(updatedBy);
            changeReason = NormalizeText(changeReason);
            reasonCode = NormalizeText(reasonCode);

            if (string.IsNullOrWhiteSpace(updatedBy))
                updatedBy = "Admin";

            if (string.IsNullOrWhiteSpace(changeReason))
                throw new InvalidOperationException("Price change reason is required.");

            if (changeReason.Length > 250)
                throw new InvalidOperationException("Price change reason cannot be longer than 250 characters.");

            if (reasonCode.Length > 100)
                throw new InvalidOperationException("Price change reason code cannot be longer than 100 characters.");

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

                var historyRows = new List<PriceChangeHistory>();

                decimal oldMinimumPrice = RoundMoney(variant.MinimumPrice);
                decimal oldRetailPrice = RoundMoney(variant.RetailPrice);
                decimal oldWholesalePrice = RoundMoney(variant.WholesalePrice);
                decimal oldMaximumPrice = RoundMoney(variant.MaximumPrice);

                decimal newMinimumPrice = RoundMoney(masterPricing.MinimumPrice);
                decimal newRetailPrice = RoundMoney(masterPricing.RetailPrice);
                decimal newWholesalePrice = RoundMoney(masterPricing.WholesalePrice);
                decimal newMaximumPrice = RoundMoney(masterPricing.MaximumPrice);

                bool masterChanged =
                    oldMinimumPrice != newMinimumPrice ||
                    oldRetailPrice != newRetailPrice ||
                    oldWholesalePrice != newWholesalePrice ||
                    oldMaximumPrice != newMaximumPrice;

                var batchIds = batchOverrides
                    .Where(b => b.ItemBatchId > 0)
                    .Select(b => b.ItemBatchId)
                    .Distinct()
                    .ToList();

                Dictionary<int, ItemBatch> dbBatches = new();

                if (batchIds.Any())
                {
                    dbBatches = await context.ItemBatches
                        .Where(b =>
                            batchIds.Contains(b.Id) &&
                            b.ItemVariantId == variant.Id &&
                            !b.IsDeactivated)
                        .ToDictionaryAsync(b => b.Id);

                    foreach (var batchDto in batchOverrides)
                    {
                        if (!dbBatches.TryGetValue(batchDto.ItemBatchId, out var dbBatch))
                            throw new InvalidOperationException($"Batch '{batchDto.BatchNo}' was not found or does not belong to the selected item.");
                    }
                }

                string priceChangeNo = string.Empty;

                bool anyBatchChanged = batchOverrides.Any(batchDto =>
                {
                    if (!dbBatches.TryGetValue(batchDto.ItemBatchId, out var dbBatch))
                        return false;

                    return RoundMoney(dbBatch.RetailPrice) != RoundMoney(batchDto.RetailPrice) ||
                           RoundMoney(dbBatch.WholesalePrice) != RoundMoney(batchDto.WholesalePrice);
                });

                if (masterChanged || anyBatchChanged)
                {
                    priceChangeNo = await GenerateDocumentNumberAsync(context, "PCH");
                }

                if (masterChanged)
                {
                    historyRows.Add(new PriceChangeHistory
                    {
                        PriceChangeNo = priceChangeNo,
                        PriceLevel = "Master",
                        ChangeSource = "PriceManagement",

                        ItemVariantId = variant.Id,
                        ItemBatchId = null,

                        ItemCode = variant.ItemParent.ItemCode,
                        SkuCode = variant.SkuCode,
                        Barcode = variant.Barcode ?? string.Empty,
                        ItemDescription = variant.ItemParent.ItemName,
                        VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                            ? "Standard"
                            : variant.VariantDescription,

                        BatchNo = string.Empty,
                        BatchExpiryDate = null,

                        EffectiveCost = ResolveEffectiveCost(variant.AverageCost, variant.CostPrice),

                        OldMinimumPrice = oldMinimumPrice,
                        NewMinimumPrice = newMinimumPrice,

                        OldRetailPrice = oldRetailPrice,
                        NewRetailPrice = newRetailPrice,

                        OldWholesalePrice = oldWholesalePrice,
                        NewWholesalePrice = newWholesalePrice,

                        OldMaximumPrice = oldMaximumPrice,
                        NewMaximumPrice = newMaximumPrice,

                        ChangedBy = updatedBy,
                        ChangedAt = now,
                        ReasonCode = reasonCode,
                        ChangeReason = changeReason,
                        Remarks = "Master price updated from Price Management page."
                    });
                }

                variant.MinimumPrice = newMinimumPrice;
                variant.RetailPrice = newRetailPrice;
                variant.WholesalePrice = newWholesalePrice;
                variant.MaximumPrice = newMaximumPrice;
                variant.UpdatedAt = now;

                foreach (var batchDto in batchOverrides)
                {
                    if (!dbBatches.TryGetValue(batchDto.ItemBatchId, out var dbBatch))
                        continue;

                    decimal oldBatchRetail = RoundMoney(dbBatch.RetailPrice);
                    decimal oldBatchWholesale = RoundMoney(dbBatch.WholesalePrice);

                    decimal newBatchRetail = RoundMoney(batchDto.RetailPrice);
                    decimal newBatchWholesale = RoundMoney(batchDto.WholesalePrice);

                    bool batchChanged =
                        oldBatchRetail != newBatchRetail ||
                        oldBatchWholesale != newBatchWholesale;

                    if (batchChanged)
                    {
                        historyRows.Add(new PriceChangeHistory
                        {
                            PriceChangeNo = priceChangeNo,
                            PriceLevel = "Batch",
                            ChangeSource = "PriceManagement",

                            ItemVariantId = variant.Id,
                            ItemBatchId = dbBatch.Id,

                            ItemCode = variant.ItemParent.ItemCode,
                            SkuCode = variant.SkuCode,
                            Barcode = variant.Barcode ?? string.Empty,
                            ItemDescription = variant.ItemParent.ItemName,
                            VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                                ? "Standard"
                                : variant.VariantDescription,

                            BatchNo = dbBatch.BatchNo,
                            BatchExpiryDate = dbBatch.ExpiryDate,

                            EffectiveCost = RoundMoney(dbBatch.CostPrice),

                            OldMinimumPrice = 0m,
                            NewMinimumPrice = 0m,

                            OldRetailPrice = oldBatchRetail,
                            NewRetailPrice = newBatchRetail,

                            OldWholesalePrice = oldBatchWholesale,
                            NewWholesalePrice = newBatchWholesale,

                            OldMaximumPrice = 0m,
                            NewMaximumPrice = 0m,

                            ChangedBy = updatedBy,
                            ChangedAt = now,
                            ReasonCode = reasonCode,
                            ChangeReason = changeReason,
                            Remarks = "Batch markdown price updated from Price Management page."
                        });
                    }

                    dbBatch.RetailPrice = newBatchRetail;
                    dbBatch.WholesalePrice = newBatchWholesale;
                    dbBatch.UpdatedAt = now;
                }

                if (historyRows.Any())
                    await context.PriceChangeHistories.AddRangeAsync(historyRows);

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

        // Compatibility method. Keep it for old callers.
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

            await UpdatePricingAsync(
                masterPricing,
                batchDtos,
                "Admin",
                "Price updated from Price Management page",
                string.Empty);
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
                    "Master price validation failed:\n" + string.Join("\n", errors));
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
        // DOCUMENT NUMBER
        // =========================================================

        private static async Task<string> GenerateDocumentNumberAsync(
            AppDbContext context,
            string documentType)
        {
            var sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(s => s.DocumentType == documentType);

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = documentType,
                    Prefix = $"{documentType}-",
                    NextSequenceNumber = 1,
                    PaddingLength = 5,
                    UpdatedAt = DateTime.Now
                };

                await context.DocumentSequences.AddAsync(sequence);
                await context.SaveChangesAsync();
            }

            string number =
                $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();

            return number;
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

        private static decimal ResolveEffectiveCost(decimal averageCost, decimal lastCost)
        {
            averageCost = RoundMoney(averageCost);
            lastCost = RoundMoney(lastCost);

            if (averageCost > 0)
                return averageCost;

            if (lastCost > 0)
                return lastCost;

            return 0m;
        }
    }
}