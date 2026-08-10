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
    public class PriceManagementItemSummaryDto
    {
        public int ItemVariantId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalStock { get; set; }
        public int BatchCount { get; set; }
        public int OverrideCount { get; set; }
    }

    public class PriceManagementBatchRowDto
    {
        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal CostPrice { get; set; }
        public decimal EffectiveRetailPrice { get; set; }
        public decimal EffectiveWholesalePrice { get; set; }
        public string PriceSource { get; set; } = string.Empty;
    }

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

        public async Task<List<PriceManagementItemSummaryDto>> GetItemSummariesAsync(
            string searchText = "",
            string categoryFilter = "All")
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            string searchUpper = NormalizeText(searchText).ToUpperInvariant();
            string category = NormalizeText(categoryFilter);

            var query = context.ItemVariants
                .AsNoTracking()
                .Where(variant =>
                    !variant.IsDeactivated &&
                    variant.ItemParent != null &&
                    !variant.ItemParent.IsDeactivated &&
                    variant.ItemParent.HasBatchTracking &&
                    variant.ItemBatches.Any(b => b.CurrentStock > 0 && !b.IsDeactivated));

            if (!string.IsNullOrWhiteSpace(searchUpper))
            {
                query = query.Where(variant =>
                    variant.ItemParent.ItemCode.ToUpper().Contains(searchUpper) ||
                    variant.ItemParent.ItemName.ToUpper().Contains(searchUpper) ||
                    variant.SkuCode.ToUpper().Contains(searchUpper) ||
                    (variant.Barcode ?? string.Empty).ToUpper().Contains(searchUpper));
            }

            if (!string.IsNullOrWhiteSpace(category) &&
                !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(variant =>
                    variant.ItemParent.Category != null &&
                    variant.ItemParent.Category.CategoryName == category);
            }

            var rawData = await query
                .OrderBy(v => v.ItemParent.ItemName)
                .ThenBy(v => v.VariantDescription)
                .Select(v => new
                {
                    v.Id,
                    v.ItemParent.ItemName,
                    v.VariantDescription,
                    v.SkuCode,
                    CategoryName = v.ItemParent.Category != null ? v.ItemParent.Category.CategoryName : string.Empty,
                    Batches = v.ItemBatches
                        .Where(b => !b.IsDeactivated)
                        .Select(b => new { b.CurrentStock, b.HasSellingPriceOverride })
                }).ToListAsync();

            var summaries = rawData.Select(v => new PriceManagementItemSummaryDto
            {
                ItemVariantId = v.Id,
                DisplayName = string.IsNullOrWhiteSpace(v.VariantDescription) || v.VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase)
                    ? v.ItemName
                    : $"{v.ItemName} - {v.VariantDescription}",
                SkuCode = v.SkuCode,
                CategoryName = v.CategoryName,
                TotalStock = v.Batches.Sum(b => b.CurrentStock),
                BatchCount = v.Batches.Count(b => b.CurrentStock > 0),
                OverrideCount = v.Batches.Count(b => b.CurrentStock > 0 && b.HasSellingPriceOverride)
            }).ToList();

            return summaries;
        }

        public async Task<List<PriceManagementBatchRowDto>> GetBatchesForItemVariantAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<PriceManagementBatchRowDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var batches = await context.ItemBatches
                .AsNoTracking()
                .Where(b => b.ItemVariantId == itemVariantId && b.CurrentStock > 0 && !b.IsDeactivated)
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.BatchNo)
                .ToListAsync();

            var results = new List<PriceManagementBatchRowDto>();
            foreach (var batch in batches)
            {
                var effective = EffectiveSellingPriceResolver.Resolve(
                        batch.ItemVariant.ItemParent.ItemType,
                        batch.ItemVariant.ItemParent.HasBatchTracking,
                        batch.BatchNo,
                        false,
                        batch.HasSellingPriceOverride,
                        batch.RetailPrice,
                        batch.WholesalePrice,
                        batch.ItemVariant.RetailPrice,
                        batch.ItemVariant.WholesalePrice);

                results.Add(new PriceManagementBatchRowDto
                {
                    ItemVariantId = batch.ItemVariantId,
                    ItemBatchId = batch.Id,
                    BatchNo = batch.BatchNo,
                    ExpiryDate = batch.ExpiryDate,
                    CurrentStock = batch.CurrentStock,
                    CostPrice = RoundMoney(batch.CostPrice),
                    EffectiveRetailPrice = effective.RetailPrice,
                    EffectiveWholesalePrice = effective.WholesalePrice,
                    PriceSource = effective.PriceSource
                });
            }

            return results;
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
