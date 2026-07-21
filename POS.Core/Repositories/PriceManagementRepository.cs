using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

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
                    Uom = v.ItemParent.UnitOfMeasure != null ? v.ItemParent.UnitOfMeasure.UomCode : v.ItemParent.BaseUom,

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
                decimal retailValue = allBatches.Sum(b => (decimal)b.CurrentStock * (decimal)b.RetailPrice);
                decimal wholesaleValue = allBatches.Sum(b => (decimal)b.CurrentStock * (decimal)b.WholesalePrice);

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

                    Uom = string.IsNullOrWhiteSpace(variant.Uom)
                        ? "PCS"
                        : variant.Uom,

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

            ValidateMasterPricing(pricing);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                DateTime now = DateTime.Now;

                var variant = await context.ItemVariants
                    .Include(v => v.ItemParent)
                    .FirstOrDefaultAsync(v =>
                        v.Id == pricing.ItemVariantId &&
                        !v.IsDeactivated &&
                        !v.ItemParent.IsDeactivated);

                if (variant == null)
                    throw new InvalidOperationException("Selected item variant was not found or is inactive.");

                var historyRows = new List<PriceChangeHistory>();

                decimal oldMinimumPrice = RoundMoney(variant.MinimumPrice);
                decimal oldRetailPrice = RoundMoney(variant.RetailPrice);
                decimal oldWholesalePrice = RoundMoney(variant.WholesalePrice);
                decimal oldMaximumPrice = RoundMoney(variant.MaximumPrice);

                decimal newMinimumPrice = RoundMoney(pricing.MinimumPrice);
                decimal newRetailPrice = RoundMoney(pricing.RetailPrice);
                decimal newWholesalePrice = RoundMoney(pricing.WholesalePrice);
                decimal newMaximumPrice = RoundMoney(pricing.MaximumPrice);

                bool masterChanged =
                    oldMinimumPrice != newMinimumPrice ||
                    oldRetailPrice != newRetailPrice ||
                    oldWholesalePrice != newWholesalePrice ||
                    oldMaximumPrice != newMaximumPrice;

                bool canSyncCurrentStock =
                    applySellingPriceToCurrentStock &&
                    string.Equals(
                        variant.ItemParent.ItemType,
                        ItemTypeCodes.StockItem,
                        StringComparison.Ordinal);

                var currentStockBatches = canSyncCurrentStock
                    ? await context.ItemBatches
                        .Where(b =>
                            b.ItemVariantId == variant.Id &&
                            !b.IsDeactivated &&
                            b.CurrentStock != 0)
                        .OrderBy(b => b.BatchNo)
                        .ThenBy(b => b.Id)
                        .ToListAsync()
                    : new List<ItemBatch>();

                bool batchPriceChanged = false;

                if (canSyncCurrentStock)
                {
                    batchPriceChanged = currentStockBatches.Any(b =>
                        RoundMoney(b.RetailPrice) != newRetailPrice ||
                        RoundMoney(b.WholesalePrice) != newWholesalePrice);
                }

                if (!masterChanged && !batchPriceChanged)
                    return;

                string priceChangeNo = await GenerateDocumentNumberAsync(context, "PCH");

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
                        ReasonCode = string.Empty,
                        ChangeReason = changeReason,
                        Remarks = applySellingPriceToCurrentStock
                            ? "Master price updated. Current stock selling prices were also synced."
                            : "Master price updated only."
                    });
                }

                variant.MinimumPrice = newMinimumPrice;
                variant.RetailPrice = newRetailPrice;
                variant.WholesalePrice = newWholesalePrice;
                variant.MaximumPrice = newMaximumPrice;
                variant.UpdatedAt = now;

                if (canSyncCurrentStock)
                {
                    foreach (var batch in currentStockBatches)
                    {
                        decimal oldBatchRetail = RoundMoney(batch.RetailPrice);
                        decimal oldBatchWholesale = RoundMoney(batch.WholesalePrice);

                        bool thisBatchChanged =
                            oldBatchRetail != newRetailPrice ||
                            oldBatchWholesale != newWholesalePrice;

                        if (!thisBatchChanged)
                            continue;

                        historyRows.Add(new PriceChangeHistory
                        {
                            PriceChangeNo = priceChangeNo,
                            PriceLevel = "Batch",
                            ChangeSource = "PriceManagement",

                            ItemVariantId = variant.Id,
                            ItemBatchId = batch.Id,

                            ItemCode = variant.ItemParent.ItemCode,
                            SkuCode = variant.SkuCode,
                            Barcode = variant.Barcode ?? string.Empty,
                            ItemDescription = variant.ItemParent.ItemName,
                            VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                                ? "Standard"
                                : variant.VariantDescription,

                            BatchNo = batch.BatchNo,
                            BatchExpiryDate = batch.ExpiryDate,

                            EffectiveCost = RoundMoney(batch.CostPrice),

                            OldMinimumPrice = 0m,
                            NewMinimumPrice = 0m,

                            OldRetailPrice = oldBatchRetail,
                            NewRetailPrice = newRetailPrice,

                            OldWholesalePrice = oldBatchWholesale,
                            NewWholesalePrice = newWholesalePrice,

                            OldMaximumPrice = 0m,
                            NewMaximumPrice = 0m,

                            ChangedBy = updatedBy,
                            ChangedAt = now,
                            ReasonCode = string.Empty,
                            ChangeReason = changeReason,
                            Remarks = IsGeneralBatch(batch.BatchNo)
                                ? "GENERAL stock bucket selling price synced from master price."
                                : "Current batch selling price synced from master price."
                        });

                        batch.RetailPrice = newRetailPrice;
                        batch.WholesalePrice = newWholesalePrice;
                        batch.UpdatedAt = now;
                    }
                }

                if (historyRows.Any())
                    await context.PriceChangeHistories.AddRangeAsync(historyRows);

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

            var rows = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock != 0)
                .OrderBy(b => b.BatchNo.ToUpper() == GeneralBatchNo ? 0 : 1)
                .ThenBy(b => b.ExpiryDate.HasValue ? b.ExpiryDate.Value : DateTime.MaxValue)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchNo)
                .Select(b => new PriceManagementBatchDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    BatchNo = b.BatchNo,
                    InternalBatchBarcode = b.InternalBatchBarcode ?? string.Empty,
                    IsGeneralStockBucket = b.BatchNo.ToUpper() == GeneralBatchNo,
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
