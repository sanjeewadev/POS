using System;
using System.Collections.Generic;
using System.Data;
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

        public async Task<List<string>> GetCategoryNamesAsync()
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.Categories
                .AsNoTracking()
                .Where(category => !category.IsDeactivated)
                .OrderBy(category => category.CategoryName)
                .Select(category => category.CategoryName)
                .ToListAsync();
        }

        public async Task<List<PriceManagementSummaryDto>> GetPricingSummariesAsync(
            string trackingFilter = "All",
            string itemTypeFilter = "All",
            string searchText = "",
            string categoryFilter = "All")
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            string searchUpper = NormalizeText(searchText).ToUpperInvariant();
            string category = NormalizeText(categoryFilter);

            var query = context.ItemVariants
                .Include(variant => variant.ItemParent)
                    .ThenInclude(parent => parent.Category)
                .Include(variant => variant.ItemParent)
                    .ThenInclude(parent => parent.UnitOfMeasure)
                .AsNoTracking()
                .Where(variant =>
                    !variant.IsDeactivated &&
                    !variant.ItemParent.IsDeactivated);

            if (!string.IsNullOrWhiteSpace(searchUpper))
            {
                query = query.Where(variant =>
                    variant.ItemParent.ItemCode.ToUpper().Contains(searchUpper) ||
                    variant.ItemParent.ItemName.ToUpper().Contains(searchUpper) ||
                    variant.SkuCode.ToUpper().Contains(searchUpper) ||
                    (variant.Barcode ?? string.Empty).ToUpper().Contains(searchUpper) ||
                    variant.ItemBatches.Any(batch =>
                        !batch.IsDeactivated &&
                        (batch.BatchNo.ToUpper().Contains(searchUpper) ||
                         (batch.InternalBatchBarcode ?? string.Empty).ToUpper().Contains(searchUpper))));
            }

            if (itemTypeFilter == "Stock Items")
                query = query.Where(variant => variant.ItemParent.ItemType == ItemTypeCodes.StockItem);
            else if (itemTypeFilter == "Services")
                query = query.Where(variant => variant.ItemParent.ItemType == ItemTypeCodes.Service);

            if (trackingFilter == "Average Cost")
            {
                query = query.Where(variant =>
                    variant.ItemParent.ItemType == ItemTypeCodes.StockItem &&
                    !variant.ItemParent.HasBatchTracking);
            }
            else if (trackingFilter == "Batch")
            {
                query = query.Where(variant =>
                    variant.ItemParent.ItemType == ItemTypeCodes.StockItem &&
                    variant.ItemParent.HasBatchTracking &&
                    !variant.ItemParent.HasExpiryTracking &&
                    !variant.ItemParent.HasBatchExpiry);
            }
            else if (trackingFilter == "Batch + Expiry")
            {
                query = query.Where(variant =>
                    variant.ItemParent.ItemType == ItemTypeCodes.StockItem &&
                    variant.ItemParent.HasBatchTracking &&
                    (variant.ItemParent.HasExpiryTracking || variant.ItemParent.HasBatchExpiry));
            }
            else if (trackingFilter == "Service / No Stock")
            {
                query = query.Where(variant => variant.ItemParent.ItemType == ItemTypeCodes.Service);
            }

            if (!string.IsNullOrWhiteSpace(category) &&
                !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(variant =>
                    variant.ItemParent.Category != null &&
                    variant.ItemParent.Category.CategoryName == category);
            }

            var variants = await query
                .OrderBy(variant => variant.ItemParent.ItemName)
                .ThenBy(variant => variant.VariantDescription)
                .ThenBy(variant => variant.SkuCode)
                .Select(variant => new
                {
                    variant.Id,
                    variant.ItemParentId,
                    variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,
                    variant.VariantDescription,
                    variant.AverageCost,
                    variant.CostPrice,
                    variant.RetailPrice,
                    variant.WholesalePrice,
                    variant.MinimumPrice,
                    variant.MaximumPrice,
                    ItemCode = variant.ItemParent.ItemCode,
                    ItemName = variant.ItemParent.ItemName,
                    ItemType = variant.ItemParent.ItemType,
                    CategoryName = variant.ItemParent.Category != null
                        ? variant.ItemParent.Category.CategoryName
                        : string.Empty,
                    BaseUom = variant.ItemParent.BaseUom,
                    MasterUom = variant.ItemParent.UnitOfMeasure != null
                        ? variant.ItemParent.UnitOfMeasure.UomCode
                        : string.Empty,
                    HasBatchTracking = variant.ItemParent.HasBatchTracking,
                    HasExpiryTracking =
                        variant.ItemParent.HasExpiryTracking ||
                        variant.ItemParent.HasBatchExpiry
                })
                .ToListAsync();

            if (variants.Count == 0)
                return new List<PriceManagementSummaryDto>();

            int[] variantIds = variants.Select(variant => variant.Id).ToArray();
            var batches = await context.ItemBatches
                .AsNoTracking()
                .Where(batch =>
                    variantIds.Contains(batch.ItemVariantId) &&
                    !batch.IsDeactivated)
                .Select(batch => new
                {
                    batch.Id,
                    batch.ItemVariantId,
                    batch.BatchNo,
                    batch.CurrentStock,
                    batch.CostPrice,
                    batch.RetailPrice,
                    batch.WholesalePrice,
                    batch.HasSellingPriceOverride,
                    batch.ReceivedDate,
                    batch.ExpiryDate
                })
                .ToListAsync();

            var byVariant = batches
                .GroupBy(batch => batch.ItemVariantId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var results = new List<PriceManagementSummaryDto>();
            foreach (var variant in variants)
            {
                if (!byVariant.TryGetValue(variant.Id, out var variantBatches))
                    variantBatches = new();

                decimal totalSoh = variantBatches.Sum(batch => batch.CurrentStock);
                decimal stockValue = variantBatches.Sum(batch => batch.CurrentStock * batch.CostPrice);
                decimal retailValue = variantBatches.Sum(batch =>
                    batch.CurrentStock * EffectiveSellingPriceResolver.Resolve(
                        variant.ItemType,
                        variant.HasBatchTracking,
                        batch.BatchNo,
                        false,
                        batch.HasSellingPriceOverride,
                        batch.RetailPrice,
                        batch.WholesalePrice,
                        variant.RetailPrice,
                        variant.WholesalePrice).RetailPrice);
                decimal wholesaleValue = variantBatches.Sum(batch =>
                    batch.CurrentStock * EffectiveSellingPriceResolver.Resolve(
                        variant.ItemType,
                        variant.HasBatchTracking,
                        batch.BatchNo,
                        false,
                        batch.HasSellingPriceOverride,
                        batch.RetailPrice,
                        batch.WholesalePrice,
                        variant.RetailPrice,
                        variant.WholesalePrice).WholesalePrice);

                bool physicalBatchItem =
                    variant.ItemType == ItemTypeCodes.StockItem &&
                    variant.HasBatchTracking;

                int physicalBatchCount = physicalBatchItem
                    ? variantBatches.Count(batch => !IsGeneralBatch(batch.BatchNo))
                    : 0;
                int overrideCount = physicalBatchItem
                    ? variantBatches.Count(batch =>
                        !IsGeneralBatch(batch.BatchNo) &&
                        batch.HasSellingPriceOverride)
                    : 0;

                decimal weightedCost = totalSoh > 0m
                    ? Math.Round(stockValue / totalSoh, 2)
                    : 0m;
                decimal lastBatchCost = variantBatches
                    .OrderByDescending(batch => batch.ReceivedDate)
                    .Select(batch => batch.CostPrice)
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
                    Uom = UomValueResolver.Resolve(variant.BaseUom, variant.MasterUom),
                    CategoryName = variant.CategoryName,
                    ItemType = variant.ItemType,
                    HasBatchTracking = variant.HasBatchTracking,
                    HasExpiryTracking = variant.HasExpiryTracking,
                    TotalSoh = totalSoh,
                    ActiveStockRowCount = variantBatches.Count(batch => batch.CurrentStock != 0m),
                    PhysicalBatchCount = physicalBatchCount,
                    BatchOverrideCount = overrideCount,
                    CurrentStockValue = Math.Round(stockValue, 2),
                    CurrentRetailValue = Math.Round(retailValue, 2),
                    CurrentWholesaleValue = Math.Round(wholesaleValue, 2),
                    LastReceivedDate = variantBatches
                        .Where(batch => batch.CurrentStock != 0m)
                        .Select(batch => (DateTime?)batch.ReceivedDate)
                        .OrderByDescending(date => date)
                        .FirstOrDefault(),
                    EarliestExpiryDate = variantBatches
                        .Where(batch => batch.CurrentStock > 0m && batch.ExpiryDate.HasValue)
                        .Select(batch => batch.ExpiryDate)
                        .OrderBy(date => date)
                        .FirstOrDefault(),
                    MovingAverageCost = variant.AverageCost > 0m
                        ? RoundMoney(variant.AverageCost)
                        : weightedCost,
                    LastLandedCost = variant.CostPrice > 0m
                        ? RoundMoney(variant.CostPrice)
                        : RoundMoney(lastBatchCost),
                    MinimumPrice = RoundMoney(variant.MinimumPrice),
                    RetailPrice = RoundMoney(variant.RetailPrice),
                    WholesalePrice = RoundMoney(variant.WholesalePrice),
                    MaximumPrice = RoundMoney(variant.MaximumPrice)
                };
                dto.AcceptChanges();
                results.Add(dto);
            }

            return results;
        }

        public async Task<List<PriceManagementBatchDto>> GetActiveBatchPriceRowsAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<PriceManagementBatchDto>();

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            List<ItemBatch> batches = await context.ItemBatches
                .Include(batch => batch.ItemVariant)
                    .ThenInclude(variant => variant.ItemParent)
                .AsNoTracking()
                .Where(batch =>
                    batch.ItemVariantId == itemVariantId &&
                    !batch.IsDeactivated)
                .OrderByDescending(batch => batch.CurrentStock > 0m)
                .ThenBy(batch => batch.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(batch => batch.ReceivedDate)
                .ThenBy(batch => batch.BatchNo)
                .ToListAsync();

            var rows = new List<PriceManagementBatchDto>();
            foreach (ItemBatch batch in batches)
            {
                EffectiveSellingPrice effective = EffectiveSellingPriceResolver.Resolve(
                    batch.ItemVariant,
                    batch);
                bool eligible = EffectiveSellingPriceResolver.IsOverrideEligible(
                    batch.ItemVariant,
                    batch);

                var row = new PriceManagementBatchDto
                {
                    ItemBatchId = batch.Id,
                    ItemVariantId = batch.ItemVariantId,
                    BatchNo = batch.BatchNo,
                    InternalBatchBarcode = batch.InternalBatchBarcode ?? string.Empty,
                    IsGeneralStockBucket = IsGeneralBatch(batch.BatchNo),
                    IsDeactivated = batch.IsDeactivated,
                    IsOverrideEligible = eligible,
                    HasSellingPriceOverride =
                        eligible &&
                        batch.HasSellingPriceOverride &&
                        effective.PriceSource == SellingPriceSourceCodes.BatchOverride,
                    ExpiryDate = batch.ExpiryDate,
                    ReceivedDate = batch.ReceivedDate,
                    CurrentStock = batch.CurrentStock,
                    CostPrice = RoundMoney(batch.CostPrice),
                    StoredRetailPrice = RoundMoney(batch.RetailPrice),
                    StoredWholesalePrice = RoundMoney(batch.WholesalePrice),
                    EffectiveRetailPrice = effective.RetailPrice,
                    EffectiveWholesalePrice = effective.WholesalePrice,
                    PriceSource = effective.PriceSource
                };
                row.InitializeEditor();
                rows.Add(row);
            }

            return rows;
        }

        public async Task UpdateMasterPricingAsync(
            PriceManagementSummaryDto pricing,
            string changedBy)
        {
            await UpdateMasterPricingCoreAsync(
                pricing,
                changedBy,
                "Master prices updated from Pricing");
        }

        public async Task SetBatchPriceOverrideAsync(
            int itemVariantId,
            int itemBatchId,
            decimal retailPrice,
            decimal wholesalePrice,
            string changedBy)
        {
            changedBy = RequireUser(changedBy);
            retailPrice = RoundMoney(retailPrice);
            wholesalePrice = RoundMoney(wholesalePrice);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                ItemBatch? batch = await context.ItemBatches
                    .Include(row => row.ItemVariant)
                        .ThenInclude(variant => variant.ItemParent)
                    .FirstOrDefaultAsync(row => row.Id == itemBatchId);

                if (batch == null || batch.ItemVariantId != itemVariantId)
                    throw new InvalidOperationException("The selected batch does not belong to the selected item variant.");

                ItemVariant variant = batch.ItemVariant;
                EffectiveSellingPriceResolver.ValidateOverride(
                    variant,
                    batch,
                    retailPrice,
                    wholesalePrice);

                EffectiveSellingPrice oldPrice = EffectiveSellingPriceResolver.Resolve(variant, batch);
                bool wasOverride = oldPrice.PriceSource == SellingPriceSourceCodes.BatchOverride;

                if (wasOverride &&
                    RoundMoney(batch.RetailPrice) == retailPrice &&
                    RoundMoney(batch.WholesalePrice) == wholesalePrice)
                {
                    return;
                }

                DateTime now = DateTime.Now;
                string changeNo = await GenerateDocumentNumberAsync(context, "PCH");
                batch.HasSellingPriceOverride = true;
                batch.RetailPrice = retailPrice;
                batch.WholesalePrice = wholesalePrice;
                batch.UpdatedAt = now;

                await context.PriceChangeHistories.AddAsync(
                    BuildBatchHistory(
                        changeNo,
                        variant,
                        batch,
                        wasOverride
                            ? PriceChangeActionCodes.BatchOverrideUpdated
                            : PriceChangeActionCodes.BatchOverrideCreated,
                        oldPrice,
                        new EffectiveSellingPrice(
                            retailPrice,
                            wholesalePrice,
                            SellingPriceSourceCodes.BatchOverride),
                        changedBy,
                        now,
                        "PriceManagement",
                        "Batch selling-price override saved from Pricing"));

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RemoveBatchPriceOverrideAsync(
            int itemVariantId,
            int itemBatchId,
            string changedBy)
        {
            changedBy = RequireUser(changedBy);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                ItemBatch? batch = await context.ItemBatches
                    .Include(row => row.ItemVariant)
                        .ThenInclude(variant => variant.ItemParent)
                    .FirstOrDefaultAsync(row => row.Id == itemBatchId);

                if (batch == null || batch.ItemVariantId != itemVariantId)
                    throw new InvalidOperationException("The selected batch does not belong to the selected item variant.");

                ItemVariant variant = batch.ItemVariant;
                if (!EffectiveSellingPriceResolver.IsOverrideEligible(variant, batch))
                    throw new InvalidOperationException("The selected stock bucket cannot use a batch override.");
                if (!batch.HasSellingPriceOverride)
                    throw new InvalidOperationException("The selected batch is already using the master price.");

                EffectiveSellingPrice oldPrice = EffectiveSellingPriceResolver.Resolve(variant, batch);
                DateTime now = DateTime.Now;
                string changeNo = await GenerateDocumentNumberAsync(context, "PCH");

                batch.HasSellingPriceOverride = false;
                EffectiveSellingPriceResolver.SynchronizeMasterMirror(variant, batch, now);
                EffectiveSellingPrice newPrice = EffectiveSellingPriceResolver.Resolve(variant, batch);

                await context.PriceChangeHistories.AddAsync(
                    BuildBatchHistory(
                        changeNo,
                        variant,
                        batch,
                        PriceChangeActionCodes.BatchOverrideRemoved,
                        oldPrice,
                        newPrice,
                        changedBy,
                        now,
                        "PriceManagement",
                        "Batch selling-price override removed from Pricing"));

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Patch 1 compatibility signature used by existing tests and older callers.
        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto pricing,
            string updatedBy = "",
            string changeReason = "Price updated from Price Management page",
            bool applySellingPriceToCurrentStock = true)
        {
            _ = applySellingPriceToCurrentStock;
            await UpdateMasterPricingCoreAsync(pricing, updatedBy, changeReason);
        }

        public async Task UpdatePricingAsync(
            PriceManagementSummaryDto masterPricing,
            List<PriceManagementBatchDto> batchOverrides,
            string updatedBy = "",
            string changeReason = "Price updated from Price Management page",
            string reasonCode = "")
        {
            _ = reasonCode;
            if (batchOverrides == null)
                throw new ArgumentNullException(nameof(batchOverrides));
            if (batchOverrides.Any(row => row.HasBatchPriceChanged))
                throw new InvalidOperationException("Use the explicit batch override commands to change batch prices.");
            await UpdateMasterPricingCoreAsync(masterPricing, updatedBy, changeReason);
        }

        public Task UpdatePricingAsync(
            PriceManagementSummaryDto masterPricing,
            List<ItemBatch> batchOverrides)
        {
            if (batchOverrides == null)
                throw new ArgumentNullException(nameof(batchOverrides));
            if (batchOverrides.Count > 0)
                throw new InvalidOperationException("Legacy batch-price editing is disabled.");
            throw new InvalidOperationException(
                "The legacy pricing overload has no authenticated user and is no longer supported.");
        }

        public async Task<List<ItemBatch>> GetActiveBatchesAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<ItemBatch>();
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.ItemBatches
                .AsNoTracking()
                .Where(batch =>
                    batch.ItemVariantId == itemVariantId &&
                    !batch.IsDeactivated)
                .OrderByDescending(batch => batch.CurrentStock > 0m)
                .ThenBy(batch => batch.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(batch => batch.ReceivedDate)
                .ThenBy(batch => batch.BatchNo)
                .ToListAsync();
        }

        private async Task UpdateMasterPricingCoreAsync(
            PriceManagementSummaryDto pricing,
            string changedBy,
            string changeReason)
        {
            if (pricing == null)
                throw new ArgumentNullException(nameof(pricing));
            changedBy = RequireUser(changedBy);
            changeReason = NormalizeText(changeReason);
            if (string.IsNullOrWhiteSpace(changeReason))
                changeReason = "Master prices updated from Pricing";
            if (changeReason.Length > 250)
                changeReason = changeReason[..250];

            List<string> validationErrors = pricing.ValidateForSave();
            if (validationErrors.Count > 0)
                throw new InvalidOperationException(
                    "Price validation failed:\n" + string.Join("\n", validationErrors));

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                ItemVariant? variant = await context.ItemVariants
                    .Include(row => row.ItemParent)
                    .FirstOrDefaultAsync(row =>
                        row.Id == pricing.ItemVariantId &&
                        !row.IsDeactivated &&
                        !row.ItemParent.IsDeactivated);
                if (variant == null)
                    throw new InvalidOperationException("Selected item variant was not found or is inactive.");

                decimal oldMinimum = RoundMoney(variant.MinimumPrice);
                decimal oldRetail = RoundMoney(variant.RetailPrice);
                decimal oldWholesale = RoundMoney(variant.WholesalePrice);
                decimal oldMaximum = RoundMoney(variant.MaximumPrice);
                decimal newMinimum = RoundMoney(pricing.MinimumPrice);
                decimal newRetail = RoundMoney(pricing.RetailPrice);
                decimal newWholesale = RoundMoney(pricing.WholesalePrice);
                decimal newMaximum = RoundMoney(pricing.MaximumPrice);

                List<ItemBatch> batches = variant.ItemParent.ItemType == ItemTypeCodes.StockItem
                    ? await context.ItemBatches
                        .Where(batch =>
                            batch.ItemVariantId == variant.Id &&
                            !batch.IsDeactivated)
                        .ToListAsync()
                    : new List<ItemBatch>();

                EffectiveSellingPriceResolver.ValidateActiveOverridesAgainstMasterBounds(
                    variant,
                    batches,
                    newMinimum,
                    newMaximum);

                bool changed =
                    oldMinimum != newMinimum ||
                    oldRetail != newRetail ||
                    oldWholesale != newWholesale ||
                    oldMaximum != newMaximum;

                DateTime now = DateTime.Now;
                if (changed)
                {
                    string changeNo = await GenerateDocumentNumberAsync(context, "PCH");
                    await context.PriceChangeHistories.AddAsync(new PriceChangeHistory
                    {
                        PriceChangeNo = changeNo,
                        PriceLevel = "Master",
                        ChangeSource = "PriceManagement",
                        ChangeAction = PriceChangeActionCodes.MasterPriceUpdated,
                        OldPriceSource = SellingPriceSourceCodes.Master,
                        NewPriceSource = SellingPriceSourceCodes.Master,
                        ItemVariantId = variant.Id,
                        ItemBatchId = null,
                        ItemCode = variant.ItemParent.ItemCode,
                        SkuCode = variant.SkuCode,
                        Barcode = variant.Barcode ?? string.Empty,
                        ItemDescription = variant.ItemParent.ItemName,
                        VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                            ? "Standard"
                            : variant.VariantDescription,
                        EffectiveCost = ResolveEffectiveCost(variant.AverageCost, variant.CostPrice),
                        OldMinimumPrice = oldMinimum,
                        NewMinimumPrice = newMinimum,
                        OldRetailPrice = oldRetail,
                        NewRetailPrice = newRetail,
                        OldWholesalePrice = oldWholesale,
                        NewWholesalePrice = newWholesale,
                        OldMaximumPrice = oldMaximum,
                        NewMaximumPrice = newMaximum,
                        ChangedBy = changedBy,
                        ChangedAt = now,
                        ReasonCode = "MASTER_PRICE_UPDATE",
                        ChangeReason = changeReason,
                        Remarks = "Master price updated. Non-overridden batch mirrors were synchronized automatically."
                    });
                }

                variant.MinimumPrice = newMinimum;
                variant.RetailPrice = newRetail;
                variant.WholesalePrice = newWholesale;
                variant.MaximumPrice = newMaximum;
                variant.UpdatedAt = now;
                foreach (ItemBatch batch in batches)
                    EffectiveSellingPriceResolver.SynchronizeMasterMirror(variant, batch, now);

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

        private static PriceChangeHistory BuildBatchHistory(
            string changeNo,
            ItemVariant variant,
            ItemBatch batch,
            string action,
            EffectiveSellingPrice oldPrice,
            EffectiveSellingPrice newPrice,
            string changedBy,
            DateTime now,
            string source,
            string reason)
        {
            return new PriceChangeHistory
            {
                PriceChangeNo = changeNo,
                PriceLevel = "Batch",
                ChangeSource = source,
                ChangeAction = action,
                OldPriceSource = oldPrice.PriceSource,
                NewPriceSource = newPrice.PriceSource,
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
                OldMinimumPrice = RoundMoney(variant.MinimumPrice),
                NewMinimumPrice = RoundMoney(variant.MinimumPrice),
                OldRetailPrice = oldPrice.RetailPrice,
                NewRetailPrice = newPrice.RetailPrice,
                OldWholesalePrice = oldPrice.WholesalePrice,
                NewWholesalePrice = newPrice.WholesalePrice,
                OldMaximumPrice = RoundMoney(variant.MaximumPrice),
                NewMaximumPrice = RoundMoney(variant.MaximumPrice),
                ChangedBy = changedBy,
                ChangedAt = now,
                ReasonCode = action,
                ChangeReason = reason,
                Remarks = $"Batch {batch.BatchNo}: {oldPrice.PriceSource} to {newPrice.PriceSource}."
            };
        }

        private static async Task<string> GenerateDocumentNumberAsync(
            AppDbContext context,
            string documentType)
        {
            DocumentSequence? sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(row => row.DocumentType == documentType);
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

            string number = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";
            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
            return number;
        }

        private static string RequireUser(string? value)
        {
            string user = NormalizeText(value);
            if (string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("An authenticated user is required to save pricing changes.");
            if (user.Length > 100)
                user = user[..100];
            return user;
        }

        private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();
        private static decimal RoundMoney(decimal value) => Math.Round(value, 2);
        private static decimal ResolveEffectiveCost(decimal averageCost, decimal lastCost) =>
            RoundMoney(averageCost) > 0m ? RoundMoney(averageCost) : RoundMoney(lastCost);
        private static bool IsGeneralBatch(string? batchNo) =>
            string.Equals(NormalizeText(batchNo), GeneralBatchNo, StringComparison.OrdinalIgnoreCase);
    }
}
