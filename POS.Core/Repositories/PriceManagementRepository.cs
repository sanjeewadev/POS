using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Services.Pricing;
using POS.Core.Utilities;

namespace POS.Core.Repositories
{
    public class PriceManagementRepository
    {
        private const string GeneralBatchNo = "GENERAL";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PriceManagementRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // =========================================================
        // MASTER GRID
        // =========================================================

        public async Task<List<PriceManagementSummaryDto>> GetPricingSummariesAsync(
            string marginFilter = "All",
            string trackingFilter = "All",
            string itemTypeFilter = "All",
            string searchText = "")
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            string search = NormalizeText(searchText);
            string searchUpper = search.ToUpperInvariant();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                    .ThenInclude(p => p.Category)
                .Include(v => v.ItemParent)
                    .ThenInclude(p => p.UnitOfMeasure)
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated);

            if (!string.IsNullOrWhiteSpace(searchUpper))
            {
                query = query.Where(v =>
                    v.ItemParent.ItemCode.ToUpper().Contains(searchUpper) ||
                    v.ItemParent.ItemName.ToUpper().Contains(searchUpper) ||
                    v.SkuCode.ToUpper().Contains(searchUpper) ||
                    ((v.Barcode ?? string.Empty).ToUpper()).Contains(searchUpper) ||
                    v.ItemBatches.Any(b =>
                        !b.IsDeactivated &&
                        (
                            b.BatchNo.ToUpper().Contains(searchUpper) ||
                            ((b.InternalBatchBarcode ?? string.Empty).ToUpper()).Contains(searchUpper)
                        )));
            }

            if (itemTypeFilter == "Stock Items")
            {
                query = query.Where(v =>
                    v.ItemParent.ItemType == ItemTypeCodes.StockItem);
            }
            else if (itemTypeFilter == "Services")
            {
                query = query.Where(v =>
                    v.ItemParent.ItemType == ItemTypeCodes.Service);
            }

            if (trackingFilter == "Average Cost")
            {
                query = query.Where(v =>
                    v.ItemParent.ItemType == ItemTypeCodes.StockItem &&
                    !v.ItemParent.HasBatchTracking);
            }
            else if (trackingFilter == "Batch")
            {
                query = query.Where(v =>
                    v.ItemParent.HasBatchTracking &&
                    !v.ItemParent.HasExpiryTracking &&
                    !v.ItemParent.HasBatchExpiry);
            }
            else if (trackingFilter == "Batch + Expiry")
            {
                query = query.Where(v =>
                    v.ItemParent.ItemType == ItemTypeCodes.StockItem &&
                    v.ItemParent.HasBatchTracking &&
                    (v.ItemParent.HasExpiryTracking || v.ItemParent.HasBatchExpiry));
            }
            else if (trackingFilter == "Service / No Stock")
            {
                query = query.Where(v =>
                    v.ItemParent.ItemType == ItemTypeCodes.Service);
            }

            var variants = await query
                .OrderBy(v => v.ItemParent.ItemName)
                .ThenBy(v => v.VariantDescription)
                .ThenBy(v => v.SkuCode)
                .Select(v => new
                {
                    v.Id,
                    v.ItemParentId,
                    v.SkuCode,
                    Barcode = v.Barcode ?? string.Empty,
                    v.VariantDescription,

                    v.AverageCost,
                    v.CostPrice,
                    v.RetailPrice,
                    v.WholesalePrice,
                    v.MinimumPrice,
                    v.MaximumPrice,

                    ItemCode = v.ItemParent.ItemCode,
                    ItemName = v.ItemParent.ItemName,
                    ItemType = v.ItemParent.ItemType,
                    CategoryName = v.ItemParent.Category != null ? v.ItemParent.Category.CategoryName : string.Empty,
                    BaseUom = v.ItemParent.BaseUom,
                    MasterUom = v.ItemParent.UnitOfMeasure != null
                        ? v.ItemParent.UnitOfMeasure.UomCode
                        : string.Empty,

                    HasBatchTracking = v.ItemParent.HasBatchTracking,
                    HasExpiryTracking = v.ItemParent.HasExpiryTracking || v.ItemParent.HasBatchExpiry
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
                    b.Id,
                    b.ItemVariantId,
                    b.BatchNo,
                    b.InternalBatchBarcode,
                    b.CurrentStock,
                    b.CostPrice,
                    b.RetailPrice,
                    b.WholesalePrice,
                    b.HasSellingPriceOverride,
                    b.ReceivedDate,
                    b.ExpiryDate
                })
                .ToListAsync();

            var batchesByVariant = batchRows
                .GroupBy(b => b.ItemVariantId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var results = new List<PriceManagementSummaryDto>();

            foreach (var variant in variants)
            {
                if (!batchesByVariant.TryGetValue(variant.Id, out var allBatches))
                {
                    allBatches = new();
                }

                var stockRows = allBatches
                    .Where(b => b.CurrentStock != 0)
                    .ToList();

                var positiveRows = allBatches
                    .Where(b => b.CurrentStock > 0)
                    .ToList();

                decimal totalSoh = allBatches.Sum(b => (decimal)b.CurrentStock);
                decimal stockValue = allBatches.Sum(b => (decimal)b.CurrentStock * (decimal)b.CostPrice);
                decimal retailValue = allBatches.Sum(b =>
                    (decimal)b.CurrentStock * EffectiveSellingPriceResolver.Resolve(
                        variant.ItemType,
                        variant.HasBatchTracking,
                        b.BatchNo,
                        false,
                        b.HasSellingPriceOverride,
                        b.RetailPrice,
                        b.WholesalePrice,
                        variant.RetailPrice,
                        variant.WholesalePrice).RetailPrice);
                decimal wholesaleValue = allBatches.Sum(b =>
                    (decimal)b.CurrentStock * EffectiveSellingPriceResolver.Resolve(
                        variant.ItemType,
                        variant.HasBatchTracking,
                        b.BatchNo,
                        false,
                        b.HasSellingPriceOverride,
                        b.RetailPrice,
                        b.WholesalePrice,
                        variant.RetailPrice,
                        variant.WholesalePrice).WholesalePrice);

                decimal weightedCost = totalSoh > 0
                    ? Math.Round(stockValue / totalSoh, 2)
                    : 0m;

                decimal lastBatchCost = allBatches
                    .OrderByDescending(b => b.ReceivedDate)
                    .Select(b => (decimal)b.CostPrice)
                    .FirstOrDefault();

                var dto = new PriceManagementSummaryDto
                {
                    ItemVariantId = variant.Id,
                    ItemParentId = variant.ItemParentId,

                    ItemCode = variant.ItemCode,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode,

                    Description = variant.ItemName,
                    VariantAttributes = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,

                    Uom = UomValueResolver.Resolve(
                        variant.BaseUom,
                        variant.MasterUom),

                    CategoryName = variant.CategoryName,
                    ItemType = variant.ItemType,

                    HasBatchTracking = variant.HasBatchTracking,
                    HasExpiryTracking = variant.HasExpiryTracking,

                    TotalSoh = totalSoh,
                    ActiveStockRowCount = stockRows.Count,

                    CurrentStockValue = Math.Round(stockValue, 2),
                    CurrentRetailValue = Math.Round(retailValue, 2),
                    CurrentWholesaleValue = Math.Round(wholesaleValue, 2),

                    LastReceivedDate = allBatches
                        .Where(b => b.CurrentStock != 0)
                        .Select(b => (DateTime?)b.ReceivedDate)
                        .OrderByDescending(d => d)
                        .FirstOrDefault(),

                    EarliestExpiryDate = positiveRows
                        .Where(b => b.ExpiryDate != null)
                        .Select(b => (DateTime?)b.ExpiryDate)
                        .OrderBy(d => d)
                        .FirstOrDefault(),

                    MovingAverageCost = variant.AverageCost > 0
                        ? RoundMoney(variant.AverageCost)
                        : weightedCost,

                    LastLandedCost = variant.CostPrice > 0
                        ? RoundMoney(variant.CostPrice)
                        : RoundMoney(lastBatchCost),

                    RetailPrice = RoundMoney(variant.RetailPrice),
                    WholesalePrice = RoundMoney(variant.WholesalePrice),
                    MinimumPrice = RoundMoney(variant.MinimumPrice),
                    MaximumPrice = RoundMoney(variant.MaximumPrice)
                };

                dto.AcceptChanges();

                results.Add(dto);
            }

            if (marginFilter == "Low Margin Alerts (< 20%)")
            {
                results = results
                    .Where(r =>
                        r.MarginHealth == "Negative Margin" ||
                        r.MarginHealth == "Low Margin" ||
                        r.MarginHealth == "Below Minimum" ||
                        r.MarginHealth == "Above Maximum" ||
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
        // SAVE SIMPLE PRICING
        // =========================================================

        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto pricing,
            string updatedBy = "Admin",
            string changeReason = "Price updated from Price Management page",
            bool applySellingPriceToCurrentStock = true)
        {
            if (pricing == null)
                throw new ArgumentNullException(nameof(pricing));

            updatedBy = NormalizeText(updatedBy);
            changeReason = NormalizeText(changeReason);

            if (string.IsNullOrWhiteSpace(updatedBy))
                updatedBy = "Admin";

            if (string.IsNullOrWhiteSpace(changeReason))
                throw new InvalidOperationException("Price change reason is required.");

            if (changeReason.Length > 250)
                throw new InvalidOperationException("Price change reason cannot be longer than 250 characters.");

            // Transitional compatibility only. Master-price saves now keep
            // non-overridden batch mirrors synchronized automatically.
            _ = applySellingPriceToCurrentStock;

            ValidateMasterPricing(pricing);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                DateTime now = DateTime.Now;

                ItemVariant? variant = await context.ItemVariants
                    .Include(v => v.ItemParent)
                    .FirstOrDefaultAsync(v =>
                        v.Id == pricing.ItemVariantId &&
                        !v.IsDeactivated &&
                        !v.ItemParent.IsDeactivated);

                if (variant == null)
                    throw new InvalidOperationException("Selected item variant was not found or is inactive.");

                decimal oldMinimumPrice = RoundMoney(variant.MinimumPrice);
                decimal oldRetailPrice = RoundMoney(variant.RetailPrice);
                decimal oldWholesalePrice = RoundMoney(variant.WholesalePrice);
                decimal oldMaximumPrice = RoundMoney(variant.MaximumPrice);

                decimal newMinimumPrice = RoundMoney(pricing.MinimumPrice);
                decimal newRetailPrice = RoundMoney(pricing.RetailPrice);
                decimal newWholesalePrice = RoundMoney(pricing.WholesalePrice);
                decimal newMaximumPrice = RoundMoney(pricing.MaximumPrice);

                List<ItemBatch> activeBatches =
                    string.Equals(
                        variant.ItemParent.ItemType,
                        ItemTypeCodes.StockItem,
                        StringComparison.Ordinal)
                        ? await context.ItemBatches
                            .Where(b =>
                                b.ItemVariantId == variant.Id &&
                                !b.IsDeactivated)
                            .OrderBy(b => b.BatchNo)
                            .ThenBy(b => b.Id)
                            .ToListAsync()
                        : new List<ItemBatch>();

                EffectiveSellingPriceResolver.ValidateActiveOverridesAgainstMasterBounds(
                    variant,
                    activeBatches,
                    newMinimumPrice,
                    newMaximumPrice);

                bool masterChanged =
                    oldMinimumPrice != newMinimumPrice ||
                    oldRetailPrice != newRetailPrice ||
                    oldWholesalePrice != newWholesalePrice ||
                    oldMaximumPrice != newMaximumPrice;

                EffectiveSellingPrice expectedMasterPrice =
                    EffectiveSellingPriceResolver.Resolve(
                        variant.ItemParent.ItemType,
                        variant.ItemParent.HasBatchTracking,
                        batchNo: null,
                        isBatchDeactivated: false,
                        hasSellingPriceOverride: false,
                        batchRetailPrice: 0m,
                        batchWholesalePrice: 0m,
                        masterRetailPrice: newRetailPrice,
                        masterWholesalePrice: newWholesalePrice);

                bool mirrorChanged = activeBatches.Any(batch =>
                    !batch.HasSellingPriceOverride &&
                    (RoundMoney(batch.RetailPrice) != expectedMasterPrice.RetailPrice ||
                     RoundMoney(batch.WholesalePrice) != expectedMasterPrice.WholesalePrice));

                if (!masterChanged && !mirrorChanged)
                    return;

                if (masterChanged)
                {
                    string priceChangeNo = await GenerateDocumentNumberAsync(context, "PCH");

                    await context.PriceChangeHistories.AddAsync(
                        new PriceChangeHistory
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

                            EffectiveCost = ResolveEffectiveCost(
                                variant.AverageCost,
                                variant.CostPrice),

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
                            ReasonCode = string.Empty,
                            ChangeReason = changeReason,
                            Remarks = "Master price updated. Non-overridden batch mirrors were synchronized automatically."
                        });
                }

                variant.MinimumPrice = newMinimumPrice;
                variant.RetailPrice = newRetailPrice;
                variant.WholesalePrice = newWholesalePrice;
                variant.MaximumPrice = newMaximumPrice;
                variant.UpdatedAt = now;

                foreach (ItemBatch batch in activeBatches)
                {
                    EffectiveSellingPriceResolver.SynchronizeMasterMirror(
                        variant,
                        batch,
                        now);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                pricing.AcceptChanges();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // COMPATIBILITY METHODS FOR OLD VIEWMODEL/XAML
        // =========================================================

        public async Task<List<PriceManagementBatchDto>> GetActiveBatchPriceRowsAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<PriceManagementBatchDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            List<ItemBatch> batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock != 0)
                .OrderBy(b => b.BatchNo.ToUpper() == GeneralBatchNo ? 0 : 1)
                .ThenBy(b => b.ExpiryDate.HasValue ? b.ExpiryDate.Value : DateTime.MaxValue)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchNo)
                .ToListAsync();

            var rows = new List<PriceManagementBatchDto>();

            foreach (ItemBatch batch in batches)
            {
                EffectiveSellingPrice effectivePrice =
                    EffectiveSellingPriceResolver.Resolve(
                        batch.ItemVariant,
                        batch);

                var row = new PriceManagementBatchDto
                {
                    ItemBatchId = batch.Id,
                    ItemVariantId = batch.ItemVariantId,
                    BatchNo = batch.BatchNo,
                    InternalBatchBarcode = batch.InternalBatchBarcode ?? string.Empty,
                    IsGeneralStockBucket = IsGeneralBatch(batch.BatchNo),
                    ExpiryDate = batch.ExpiryDate,
                    ReceivedDate = batch.ReceivedDate,
                    CurrentStock = batch.CurrentStock,
                    CostPrice = RoundMoney(batch.CostPrice),
                    RetailPrice = effectivePrice.RetailPrice,
                    WholesalePrice = effectivePrice.WholesalePrice,
                    PriceSource = effectivePrice.PriceSource,
                    HasSellingPriceOverride =
                        effectivePrice.PriceSource ==
                        SellingPriceSourceCodes.BatchOverride
                };

                row.AcceptChanges();
                rows.Add(row);
            }

            return rows;
        }

        public async Task<List<ItemBatch>> GetActiveBatchesAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<ItemBatch>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock != 0)
                .OrderBy(b => b.BatchNo.ToUpper() == GeneralBatchNo ? 0 : 1)
                .ThenBy(b => b.ExpiryDate.HasValue ? b.ExpiryDate.Value : DateTime.MaxValue)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchNo)
                .ToListAsync();
        }

        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto masterPricing,
            List<PriceManagementBatchDto> batchOverrides,
            string updatedBy = "Admin",
            string changeReason = "Price updated from Price Management page",
            string reasonCode = "")
        {
            if (batchOverrides == null)
                throw new ArgumentNullException(nameof(batchOverrides));

            if (batchOverrides.Any(row => row.HasBatchPriceChanged))
            {
                throw new InvalidOperationException(
                    "Batch selling-price override editing is not available until the dedicated Pricing workflow is installed.");
            }

            _ = reasonCode;

            await UpdatePricingAsync(
                masterPricing,
                updatedBy,
                changeReason,
                applySellingPriceToCurrentStock: true);
        }

        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto masterPricing,
            List<ItemBatch> batchOverrides)
        {
            if (batchOverrides == null)
                throw new ArgumentNullException(nameof(batchOverrides));

            if (batchOverrides.Count > 0)
            {
                throw new InvalidOperationException(
                    "Legacy batch-price editing is disabled. Batch overrides will be managed through the dedicated Pricing workflow.");
            }

            await UpdatePricingAsync(
                masterPricing,
                "Admin",
                "Price updated from Price Management page",
                applySellingPriceToCurrentStock: true);
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

        private static bool IsGeneralBatch(string? batchNo)
        {
            return string.Equals(
                NormalizeText(batchNo),
                GeneralBatchNo,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
