using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class ItemMasterSummaryDto
    {
        public int ParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public int VariantCount { get; set; }

        public decimal TotalStockOnHand { get; set; }

        public bool IsDeactivated { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public bool HasBatchTracking { get; set; }

        public bool HasExpiryTracking { get; set; }
    }

    public class ParentSeekDto
    {
        public int ParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public int ActiveVariantsCount { get; set; }

        public bool HasBatchTracking { get; set; }

        public bool HasExpiryTracking { get; set; }

        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }
    }

    public class VariantSeekDto
    {
        public int VariantId { get; set; }

        public int ParentId { get; set; }

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public decimal RetailPrice { get; set; }

        public decimal StockOnHand { get; set; }

        public bool HasBatchTracking { get; set; }

        public bool HasExpiryTracking { get; set; }

        public bool HasStock => StockOnHand > 0m;

        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }
    }

    public class BatchSeekDto
    {
        public int ItemBatchId { get; set; }

        public int ItemVariantId { get; set; }

        public string InternalBatchBarcode { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        public DateTime ReceivedDate { get; set; }

        public decimal CostPrice { get; set; }

        public decimal RetailPrice { get; set; }

        public decimal WholesalePrice { get; set; }

        public decimal AvailableQty { get; set; }

        public bool IsExpired =>
            ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;

        public bool IsNearExpiry =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date >= DateTime.Today &&
            ExpiryDate.Value.Date <= DateTime.Today.AddDays(30);

        public bool IsSelectable =>
            AvailableQty > 0m && !IsExpired;

        public string BatchDisplayText =>
            string.IsNullOrWhiteSpace(BatchNo)
                ? "-"
                : BatchNo.Trim();

        public string ExpiryDisplayText =>
            ExpiryDate.HasValue
                ? ExpiryDate.Value.ToString("yyyy-MM-dd")
                : "-";

        public string BarcodeDisplayText =>
            string.IsNullOrWhiteSpace(InternalBatchBarcode)
                ? "-"
                : InternalBatchBarcode.Trim();

        public string WarningText
        {
            get
            {
                if (IsExpired)
                    return "EXPIRED";

                if (IsNearExpiry)
                    return "NEAR EXPIRY";

                return string.Empty;
            }
        }
    }

    public class CashierSellableItemDto
    {
        public int VariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = "PCS";

        public decimal AverageCost { get; set; }

        public decimal RetailPrice { get; set; }

        public decimal WholesalePrice { get; set; }

        public decimal MinimumPrice { get; set; }

        public decimal MaximumPrice { get; set; }

        public decimal StockOnHand { get; set; }

        public bool HasBatchTracking { get; set; }

        public bool HasExpiryTracking { get; set; }

        public bool HasStock => StockOnHand > 0;

        public string DisplayDescription
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                return $"{Description} - {VariantDescription}";
            }
        }
    }

    public class ItemHardDeleteCheckResult
    {
        public int ParentId { get; set; }

        public bool CanDelete => BlockingReasons.Count == 0;

        public List<string> BlockingReasons { get; } = new();

        public string Message
        {
            get
            {
                if (CanDelete)
                    return "Item can be safely deleted.";

                return string.Join(Environment.NewLine, BlockingReasons);
            }
        }

        public void AddBlock(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                BlockingReasons.Add(reason.Trim());
        }
    }

    public class ItemMasterRepository
    {
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;
        private const string GeneralBatchNo = "GENERAL";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ItemMasterRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // =========================================================
        // ADMIN GRID DATA
        // =========================================================

        public async Task<IReadOnlyList<ItemMasterSummaryDto>> GetSummariesAsync(
            string searchTerm = "",
            bool includeDeactivated = false,
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<ItemParent> query = context.ItemParents
                .AsNoTracking();

            if (!includeDeactivated)
            {
                query = query.Where(p => !p.IsDeactivated);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(p =>
                    EF.Functions.Like(p.ItemCode, $"%{term}%") ||
                    EF.Functions.Like(p.ItemName, $"%{term}%") ||
                    EF.Functions.Like(p.Category.CategoryName, $"%{term}%") ||
                    p.Variants.Any(v =>
                        EF.Functions.Like(v.SkuCode, $"%{term}%") ||
                        EF.Functions.Like(v.Barcode, $"%{term}%") ||
                        EF.Functions.Like(v.VariantDescription, $"%{term}%")));
            }

            var itemsList = await query
                .OrderBy(p => p.IsDeactivated)
                .ThenBy(p => p.ItemName)
                .ThenBy(p => p.ItemCode)
                .Select(p => new ItemMasterSummaryDto
                {
                    ParentId = p.Id,
                    ItemCode = p.ItemCode,
                    ItemName = p.ItemName,
                    CategoryName = p.Category.CategoryName,
                    VariantCount = includeDeactivated
                        ? p.Variants.Count()
                        : p.Variants.Count(v => !v.IsDeactivated),
                    TotalStockOnHand = 0m,
                    IsDeactivated = p.IsDeactivated,
                    StatusText = p.IsDeactivated ? "Deactivated" : "Active",
                    HasBatchTracking = p.HasBatchTracking,
                    HasExpiryTracking = p.HasExpiryTracking || p.HasBatchExpiry
                })
                .Take(take)
                .ToListAsync();

            if (!itemsList.Any())
                return itemsList;

            var parentIds = itemsList
                .Select(i => i.ParentId)
                .ToList();

            var rawStockData = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    parentIds.Contains(b.ItemVariant.ItemParentId) &&
                    !b.IsDeactivated &&
                    !b.ItemVariant.IsDeactivated)
                .Select(b => new
                {
                    b.ItemVariant.ItemParentId,
                    b.CurrentStock
                })
                .ToListAsync();

            foreach (var item in itemsList)
            {
                item.TotalStockOnHand = rawStockData
                    .Where(s => s.ItemParentId == item.ParentId)
                    .Sum(s => s.CurrentStock);
            }

            return itemsList;
        }

        // =========================================================
        // FULL MATRIX FETCH
        // =========================================================

        public async Task<ItemParent?> GetFullMatrixByIdAsync(int parentId)
        {
            if (parentId <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemParents
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.UnitOfMeasure)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.PropertyMappings)
                        .ThenInclude(m => m.AttributeGroup)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.PropertyMappings)
                        .ThenInclude(m => m.AttributeValue)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.ItemSuppliers)
                        .ThenInclude(s => s.Supplier)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == parentId);
        }

        public async Task<bool> IsItemCodeUniqueAsync(string itemCode, int currentParentId = 0)
        {
            string normalizedCode = NormalizeCode(itemCode);

            if (string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.ItemParents.AnyAsync(p =>
                EF.Functions.Collate(p.ItemCode, "NOCASE") == normalizedCode &&
                p.Id != currentParentId);
        }

        public async Task<bool> IsSkuCodeUniqueAsync(string skuCode, int currentVariantId = 0)
        {
            string normalizedSku = NormalizeCode(skuCode);

            if (string.IsNullOrWhiteSpace(normalizedSku))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.ItemVariants.AnyAsync(v =>
                EF.Functions.Collate(v.SkuCode, "NOCASE") == normalizedSku &&
                v.Id != currentVariantId);
        }

        public async Task<bool> IsBarcodeUniqueAsync(string barcode, int currentVariantId = 0)
        {
            string normalizedBarcode = NormalizeText(barcode);

            if (string.IsNullOrWhiteSpace(normalizedBarcode))
                return true;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.ItemVariants.AnyAsync(v =>
                EF.Functions.Collate(v.Barcode ?? string.Empty, "NOCASE") == normalizedBarcode &&
                v.Id != currentVariantId);
        }

        public async Task<bool> IsBarcodeUniqueAcrossItemsAndBatchesAsync(
            string barcode,
            int currentVariantId = 0,
            int currentBatchId = 0)
        {
            string normalizedBarcode = NormalizeText(barcode);

            if (string.IsNullOrWhiteSpace(normalizedBarcode))
                return true;

            await using var context = await _contextFactory.CreateDbContextAsync();

            bool itemBarcodeExists = await context.ItemVariants.AnyAsync(v =>
                EF.Functions.Collate(v.Barcode ?? string.Empty, "NOCASE") == normalizedBarcode &&
                v.Id != currentVariantId);

            if (itemBarcodeExists)
                return false;

            bool batchBarcodeExists = await context.ItemBatches.AnyAsync(b =>
                EF.Functions.Collate(b.InternalBatchBarcode ?? string.Empty, "NOCASE") == normalizedBarcode &&
                b.Id != currentBatchId);

            return !batchBarcodeExists;
        }

        // =========================================================
        // ATOMIC MATRIX SAVE
        // =========================================================

        public async Task SaveFullMatrixAsync(
            ItemParent parent,
            List<ItemVariant> variants,
            List<ItemPropertyMapping> mappings)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            if (variants == null || !variants.Any())
                throw new InvalidOperationException("At least one item variant is required.");

            NormalizeParent(parent);
            ValidateParent(parent);

            foreach (var variant in variants)
            {
                NormalizeVariant(variant);
                ValidateVariant(variant);
            }

            ValidateSubmittedVariantDuplicates(variants);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;

                if (parent.Id == 0)
                {
                    ApplyParentActivationStateToSubmittedVariants(
                        parent,
                        variants,
                        parentIsBeingReactivated: false,
                        now);

                    parent.CreatedAt = now;
                    parent.UpdatedAt = now;
                    parent.DeactivatedAt = parent.IsDeactivated ? now : null;

                    parent.Category = null!;
                    parent.SubCategory = null;
                    parent.UnitOfMeasure = null!;
                    parent.Variants = new List<ItemVariant>();

                    await context.ItemParents.AddAsync(parent);
                    await context.SaveChangesAsync();

                    foreach (var variant in variants)
                    {
                        await AddVariantGraphAsync(context, parent.Id, variant, now);
                    }
                }
                else
                {
                    var existingParent = await context.ItemParents
                        .FirstOrDefaultAsync(p => p.Id == parent.Id);

                    if (existingParent == null)
                        throw new InvalidOperationException("Item record was not found.");

                    await ValidateLockedSetupFieldsForExistingItemAsync(
                        context,
                        existingParent,
                        parent,
                        variants);

                    bool parentWasDeactivated = existingParent.IsDeactivated;
                    bool parentIsBeingReactivated = parentWasDeactivated && !parent.IsDeactivated;

                    ApplyParentActivationStateToSubmittedVariants(
                        parent,
                        variants,
                        parentIsBeingReactivated,
                        now);

                    // Editable fields after first save.
                    existingParent.ItemName = parent.ItemName;
                    existingParent.PrintName = parent.PrintName;
                    existingParent.UnitOfMeasureId = parent.UnitOfMeasureId;
                    existingParent.BaseUom = parent.BaseUom;
                    existingParent.TaxCode = parent.TaxCode;
                    existingParent.AllowCashierDiscount = parent.AllowCashierDiscount;
                    existingParent.IsPurchaseLocked = parent.IsPurchaseLocked;
                    existingParent.IsSaleLocked = parent.IsSaleLocked;
                    existingParent.IsDeactivated = parent.IsDeactivated;
                    existingParent.UpdatedAt = now;

                    if (existingParent.IsDeactivated)
                        existingParent.DeactivatedAt ??= now;
                    else
                        existingParent.DeactivatedAt = null;

                    await context.SaveChangesAsync();

                    var existingVariants = await context.ItemVariants
                        .Include(v => v.PropertyMappings)
                        .Include(v => v.ItemSuppliers)
                        .Where(v => v.ItemParentId == parent.Id)
                        .ToListAsync();

                    var existingVariantIds = existingVariants
                        .Select(v => v.Id)
                        .ToList();

                    var usedVariantIds = await GetUsedVariantIdsAsync(context, existingVariantIds);

                    var submittedExistingIds = new HashSet<int>();

                    foreach (var submittedVariant in variants)
                    {
                        var existingVariant = FindMatchingExistingVariant(existingVariants, submittedVariant);

                        if (existingVariant == null)
                        {
                            await ValidateSkuAndBarcodeAgainstDatabaseAsync(
                                context,
                                submittedVariant,
                                currentVariantId: 0);

                            await AddVariantGraphAsync(context, parent.Id, submittedVariant, now);
                            continue;
                        }

                        submittedExistingIds.Add(existingVariant.Id);

                        await ValidateSkuAndBarcodeAgainstDatabaseAsync(
                            context,
                            submittedVariant,
                            existingVariant.Id);

                        bool variantHasHistory = usedVariantIds.Contains(existingVariant.Id);

                        if (variantHasHistory)
                        {
                            string existingMappingKey = BuildMappingKey(existingVariant.PropertyMappings);
                            string submittedMappingKey = BuildMappingKey(submittedVariant.PropertyMappings);

                            if (existingMappingKey != submittedMappingKey)
                            {
                                throw new InvalidOperationException(
                                    $"Variant '{existingVariant.SkuCode}' already has stock or transaction history. Its attribute combination cannot be changed. Deactivate it and create a new variant instead.");
                            }
                        }

                        UpdateVariantScalarFields(existingVariant, submittedVariant, now);

                        if (!variantHasHistory)
                        {
                            await ReplaceVariantMappingsAsync(context, existingVariant.Id, submittedVariant);
                        }

                        await ReplaceVariantSuppliersAsync(context, existingVariant.Id, submittedVariant, now);
                    }

                    var removedVariants = existingVariants
                        .Where(v => !submittedExistingIds.Contains(v.Id))
                        .ToList();

                    foreach (var removedVariant in removedVariants)
                    {
                        bool hasHistory = usedVariantIds.Contains(removedVariant.Id);

                        if (hasHistory)
                        {
                            removedVariant.IsDeactivated = true;
                            removedVariant.UpdatedAt = now;
                            removedVariant.DeactivatedAt ??= now;
                        }
                        else
                        {
                            var maps = await context.ItemPropertyMappings
                                .Where(m => m.ItemVariantId == removedVariant.Id)
                                .ToListAsync();

                            var suppliers = await context.ItemSuppliers
                                .Where(s => s.ItemVariantId == removedVariant.Id)
                                .ToListAsync();

                            context.ItemPropertyMappings.RemoveRange(maps);
                            context.ItemSuppliers.RemoveRange(suppliers);
                            context.ItemVariants.Remove(removedVariant);
                        }
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task ValidateLockedSetupFieldsForExistingItemAsync(
            AppDbContext context,
            ItemParent existingParent,
            ItemParent submittedParent,
            List<ItemVariant> submittedVariants)
        {
            if (!string.Equals(
                    NormalizeCode(existingParent.ItemCode),
                    NormalizeCode(submittedParent.ItemCode),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Item code cannot be changed after the item is saved. Delete the unused item and create it again with the correct code.");
            }

            if (existingParent.CategoryId != submittedParent.CategoryId)
            {
                throw new InvalidOperationException(
                    "Category cannot be changed after the item is saved. Delete the unused item and create it again under the correct category.");
            }

            if ((existingParent.SubCategoryId ?? 0) != (submittedParent.SubCategoryId ?? 0))
            {
                throw new InvalidOperationException(
                    "Sub-category cannot be changed after the item is saved. Delete the unused item and create it again under the correct sub-category.");
            }

            if (existingParent.HasBatchTracking != submittedParent.HasBatchTracking)
            {
                throw new InvalidOperationException(
                    "Batch tracking cannot be changed after the item is saved. Delete the unused item and create it again with the correct tracking type.");
            }

            bool existingExpiryTracking = existingParent.HasExpiryTracking || existingParent.HasBatchExpiry;
            bool submittedExpiryTracking = submittedParent.HasExpiryTracking || submittedParent.HasBatchExpiry;

            if (existingExpiryTracking != submittedExpiryTracking)
            {
                throw new InvalidOperationException(
                    "Expiry tracking cannot be changed after the item is saved. Delete the unused item and create it again with the correct expiry rule.");
            }

            if (existingParent.IsScaleItem != submittedParent.IsScaleItem)
            {
                throw new InvalidOperationException(
                    "Scale item setting cannot be changed after the item is saved. Delete the unused item and create it again with the correct scale setting.");
            }

            if (existingParent.IsSerialized != submittedParent.IsSerialized)
            {
                throw new InvalidOperationException(
                    "Serialized item setting cannot be changed after the item is saved. Delete the unused item and create it again with the correct serialized setting.");
            }

            var existingVariants = await context.ItemVariants
                .Include(v => v.PropertyMappings)
                .Where(v => v.ItemParentId == existingParent.Id)
                .ToListAsync();

            if (submittedVariants.Count != existingVariants.Count)
            {
                throw new InvalidOperationException(
                    "Variant structure cannot be changed after the item is saved. Delete the unused item and create it again, or deactivate a variant if it already exists.");
            }

            foreach (var existingVariant in existingVariants)
            {
                var submittedVariant = submittedVariants.FirstOrDefault(v => v.Id == existingVariant.Id)
                    ?? submittedVariants.FirstOrDefault(v =>
                        string.Equals(v.SkuCode, existingVariant.SkuCode, StringComparison.OrdinalIgnoreCase));

                if (submittedVariant == null)
                {
                    throw new InvalidOperationException(
                        "Existing variants cannot be removed from Item Master save. Use Delete for unused items or deactivate the variant/item when there is history.");
                }

                if (!string.Equals(
                        NormalizeCode(existingVariant.SkuCode),
                        NormalizeCode(submittedVariant.SkuCode),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"SKU cannot be changed after save: {existingVariant.SkuCode}.");
                }

                if (!string.Equals(
                        NormalizeText(existingVariant.VariantDescription),
                        NormalizeText(submittedVariant.VariantDescription),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Variant description cannot be changed after save: {existingVariant.SkuCode}.");
                }

                string existingMappingKey = BuildMappingKey(existingVariant.PropertyMappings);
                string submittedMappingKey = BuildMappingKey(submittedVariant.PropertyMappings);

                if (existingMappingKey != submittedMappingKey)
                {
                    throw new InvalidOperationException(
                        $"Variant matrix/property structure cannot be changed after save: {existingVariant.SkuCode}.");
                }
            }
        }

        private static void ApplyParentActivationStateToSubmittedVariants(
            ItemParent parent,
            IEnumerable<ItemVariant> variants,
            bool parentIsBeingReactivated,
            DateTime now)
        {
            foreach (var variant in variants)
            {
                if (parent.IsDeactivated)
                {
                    variant.IsDeactivated = true;
                    variant.DeactivatedAt ??= now;
                    continue;
                }

                if (parentIsBeingReactivated)
                {
                    variant.IsDeactivated = false;
                    variant.DeactivatedAt = null;
                }
            }
        }

        private static ItemVariant? FindMatchingExistingVariant(
            List<ItemVariant> existingVariants,
            ItemVariant submittedVariant)
        {
            if (submittedVariant.Id > 0)
            {
                var byId = existingVariants.FirstOrDefault(v => v.Id == submittedVariant.Id);

                if (byId != null)
                    return byId;
            }

            return existingVariants.FirstOrDefault(v =>
                string.Equals(v.SkuCode, submittedVariant.SkuCode, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task AddVariantGraphAsync(
            AppDbContext context,
            int parentId,
            ItemVariant variant,
            DateTime now)
        {
            variant.Id = 0;
            variant.ItemParentId = parentId;
            variant.ItemParent = null!;

            variant.CreatedAt = now;
            variant.UpdatedAt = now;
            variant.DeactivatedAt = variant.IsDeactivated ? now : null;

            var mappings = variant.PropertyMappings?
                .ToList() ?? new List<ItemPropertyMapping>();

            var suppliers = variant.ItemSuppliers?
                .ToList() ?? new List<ItemSupplier>();

            variant.PropertyMappings = new List<ItemPropertyMapping>();
            variant.ItemSuppliers = new List<ItemSupplier>();

            await ValidateMappingsAsync(context, mappings);
            await ValidateSuppliersAsync(context, suppliers);

            await context.ItemVariants.AddAsync(variant);
            await context.SaveChangesAsync();

            foreach (var map in mappings)
            {
                context.ItemPropertyMappings.Add(new ItemPropertyMapping
                {
                    ItemVariantId = variant.Id,
                    AttributeGroupId = map.AttributeGroupId,
                    AttributeValueId = map.AttributeValueId
                });
            }

            foreach (var supplier in suppliers)
            {
                context.ItemSuppliers.Add(new ItemSupplier
                {
                    ItemVariantId = variant.Id,
                    SupplierId = supplier.SupplierId,
                    SupplierItemCode = string.Empty,
                    LastCostPrice = supplier.LastCostPrice,
                    IsPrimary = false,
                    MinimumOrderQuantity = supplier.MinimumOrderQuantity <= 0
                        ? 1
                        : supplier.MinimumOrderQuantity,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await context.SaveChangesAsync();
        }

        private static void UpdateVariantScalarFields(
            ItemVariant existing,
            ItemVariant submitted,
            DateTime now)
        {
            existing.SkuCode = submitted.SkuCode;
            existing.VariantDescription = submitted.VariantDescription;
            existing.Barcode = submitted.Barcode;

            existing.AverageCost = submitted.AverageCost;
            existing.CostPrice = submitted.CostPrice;
            existing.RetailPrice = submitted.RetailPrice;
            existing.WholesalePrice = submitted.WholesalePrice;
            existing.MinimumPrice = submitted.MinimumPrice;
            existing.MaximumPrice = submitted.MaximumPrice;
            existing.ReorderLevel = submitted.ReorderLevel;

            existing.IsDeactivated = submitted.IsDeactivated;
            existing.UpdatedAt = now;

            if (existing.IsDeactivated)
                existing.DeactivatedAt ??= now;
            else
                existing.DeactivatedAt = null;
        }

        private static async Task ReplaceVariantMappingsAsync(
            AppDbContext context,
            int variantId,
            ItemVariant submittedVariant)
        {
            var submittedMappings = submittedVariant.PropertyMappings?
                .ToList() ?? new List<ItemPropertyMapping>();

            await ValidateMappingsAsync(context, submittedMappings);

            var existingMappings = await context.ItemPropertyMappings
                .Where(m => m.ItemVariantId == variantId)
                .ToListAsync();

            context.ItemPropertyMappings.RemoveRange(existingMappings);

            foreach (var map in submittedMappings)
            {
                context.ItemPropertyMappings.Add(new ItemPropertyMapping
                {
                    ItemVariantId = variantId,
                    AttributeGroupId = map.AttributeGroupId,
                    AttributeValueId = map.AttributeValueId
                });
            }
        }

        private static async Task ReplaceVariantSuppliersAsync(
            AppDbContext context,
            int variantId,
            ItemVariant submittedVariant,
            DateTime now)
        {
            var submittedSuppliers = submittedVariant.ItemSuppliers?
                .ToList() ?? new List<ItemSupplier>();

            await ValidateSuppliersAsync(context, submittedSuppliers);

            var existingSuppliers = await context.ItemSuppliers
                .Where(s => s.ItemVariantId == variantId)
                .ToListAsync();

            context.ItemSuppliers.RemoveRange(existingSuppliers);

            foreach (var supplier in submittedSuppliers)
            {
                context.ItemSuppliers.Add(new ItemSupplier
                {
                    ItemVariantId = variantId,
                    SupplierId = supplier.SupplierId,
                    SupplierItemCode = string.Empty,
                    LastCostPrice = supplier.LastCostPrice,
                    IsPrimary = false,
                    MinimumOrderQuantity = supplier.MinimumOrderQuantity <= 0
                        ? 1
                        : supplier.MinimumOrderQuantity,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        private static async Task ValidateMappingsAsync(
            AppDbContext context,
            IEnumerable<ItemPropertyMapping> mappings)
        {
            var mappingList = mappings.ToList();

            var duplicateGroup = mappingList
                .GroupBy(m => m.AttributeGroupId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateGroup != null)
            {
                throw new InvalidOperationException(
                    "One variant cannot have more than one value from the same property group.");
            }

            foreach (var map in mappingList)
            {
                if (map.AttributeGroupId <= 0 || map.AttributeValueId <= 0)
                {
                    throw new InvalidOperationException(
                        "Invalid variant property mapping.");
                }

                bool valueBelongsToGroup = await context.AttributeValues.AnyAsync(v =>
                    v.Id == map.AttributeValueId &&
                    v.AttributeGroupId == map.AttributeGroupId);

                if (!valueBelongsToGroup)
                {
                    throw new InvalidOperationException(
                        "One or more selected property values do not belong to the selected property group.");
                }
            }
        }

        private static async Task ValidateSuppliersAsync(
            AppDbContext context,
            IEnumerable<ItemSupplier> suppliers)
        {
            var supplierList = suppliers.ToList();

            var duplicateSupplier = supplierList
                .GroupBy(s => s.SupplierId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateSupplier != null)
            {
                throw new InvalidOperationException(
                    "The same supplier cannot be assigned twice to one variant.");
            }

            foreach (var supplier in supplierList)
            {
                if (supplier.SupplierId <= 0)
                    throw new InvalidOperationException("Invalid supplier assignment.");

                bool supplierExists = await context.Suppliers.AnyAsync(s =>
                    s.Id == supplier.SupplierId);

                if (!supplierExists)
                    throw new InvalidOperationException("One or more selected suppliers are missing.");

                if (supplier.LastCostPrice < 0)
                    throw new InvalidOperationException("Supplier cost cannot be negative.");

                if (supplier.MinimumOrderQuantity <= 0)
                    throw new InvalidOperationException("Minimum order quantity must be greater than zero.");
            }
        }

        private static async Task ValidateSkuAndBarcodeAgainstDatabaseAsync(
            AppDbContext context,
            ItemVariant variant,
            int currentVariantId)
        {
            string sku = NormalizeCode(variant.SkuCode);

            bool skuExists = await context.ItemVariants.AnyAsync(v =>
                EF.Functions.Collate(v.SkuCode, "NOCASE") == sku &&
                v.Id != currentVariantId);

            if (skuExists)
            {
                throw new InvalidOperationException(
                    $"SKU '{variant.SkuCode}' already exists.");
            }

            string barcode = NormalizeText(variant.Barcode);

            if (!string.IsNullOrWhiteSpace(barcode))
            {
                bool barcodeExistsOnItem = await context.ItemVariants.AnyAsync(v =>
                    EF.Functions.Collate(v.Barcode ?? string.Empty, "NOCASE") == barcode &&
                    v.Id != currentVariantId);

                if (barcodeExistsOnItem)
                {
                    throw new InvalidOperationException(
                        $"Barcode '{variant.Barcode}' already exists.");
                }

                bool barcodeExistsOnBatch = await context.ItemBatches.AnyAsync(b =>
                    EF.Functions.Collate(b.InternalBatchBarcode ?? string.Empty, "NOCASE") == barcode);

                if (barcodeExistsOnBatch)
                {
                    throw new InvalidOperationException(
                        $"Barcode '{variant.Barcode}' already exists as a GRN batch barcode.");
                }
            }
        }

        // =========================================================
        // SAFE DELETE / DEACTIVATE
        // =========================================================

        public async Task<ItemHardDeleteCheckResult> CanHardDeleteMatrixAsync(int parentId)
        {
            var result = new ItemHardDeleteCheckResult
            {
                ParentId = parentId
            };

            if (parentId <= 0)
            {
                result.AddBlock("Invalid item selected.");
                return result;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            bool parentExists = await context.ItemParents
                .AsNoTracking()
                .AnyAsync(p => p.Id == parentId);

            if (!parentExists)
            {
                result.AddBlock("Item was not found.");
                return result;
            }

            var variantIds = await context.ItemVariants
                .AsNoTracking()
                .Where(v => v.ItemParentId == parentId)
                .Select(v => v.Id)
                .ToListAsync();

            if (!variantIds.Any())
                return result;

            int supplierLinks = await context.ItemSuppliers
                .AsNoTracking()
                .CountAsync(s => variantIds.Contains(s.ItemVariantId));

            if (supplierLinks > 0)
            {
                result.AddBlock(
                    $"Supplier links exist for this item ({supplierLinks}). Remove all approved supplier links before deleting.");
            }

            int itemBatches = await context.ItemBatches
                .AsNoTracking()
                .CountAsync(b => variantIds.Contains(b.ItemVariantId));

            if (itemBatches > 0)
                result.AddBlock($"Stock batches/stock buckets exist for this item ({itemBatches}).");

            int inventoryTransactions = await CountLinkedRowsAsync(
                context,
                context.InventoryTransactions.AsNoTracking(),
                variantIds);

            if (inventoryTransactions > 0)
                result.AddBlock($"Inventory transactions exist for this item ({inventoryTransactions}).");

            int grnLines = await CountLinkedRowsAsync(
                context,
                context.GrnLines.AsNoTracking(),
                variantIds);

            if (grnLines > 0)
                result.AddBlock($"GRN history exists for this item ({grnLines} line(s)).");

            int poLines = await CountLinkedRowsAsync(
                context,
                context.PoLines.AsNoTracking(),
                variantIds);

            if (poLines > 0)
                result.AddBlock($"Purchase order history exists for this item ({poLines} line(s)).");

            int salesLines = await CountLinkedRowsAsync(
                context,
                context.SalesLines.AsNoTracking(),
                variantIds);

            if (salesLines > 0)
                result.AddBlock($"Sales history exists for this item ({salesLines} line(s)).");

            int customerReturnLines = await CountLinkedRowsAsync(
                context,
                context.CustomerReturnLines.AsNoTracking(),
                variantIds);

            if (customerReturnLines > 0)
                result.AddBlock($"Customer return history exists for this item ({customerReturnLines} line(s)).");

            int supplierReturnLines = await CountLinkedRowsAsync(
                context,
                context.SupplierReturnLines.AsNoTracking(),
                variantIds);

            if (supplierReturnLines > 0)
                result.AddBlock($"Supplier return history exists for this item ({supplierReturnLines} line(s)).");

            int stockAdjustmentLines = await CountLinkedRowsAsync(
                context,
                context.StockAdjustmentLines.AsNoTracking(),
                variantIds);

            if (stockAdjustmentLines > 0)
                result.AddBlock($"Stock adjustment history exists for this item ({stockAdjustmentLines} line(s)).");

            return result;
        }

        public async Task HardDeleteMatrixAsync(int parentId)
        {
            var deleteCheck = await CanHardDeleteMatrixAsync(parentId);

            if (!deleteCheck.CanDelete)
                throw new InvalidOperationException("Item cannot be deleted:\n\n" + deleteCheck.Message);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var parent = await context.ItemParents
                    .Include(p => p.Variants)
                        .ThenInclude(v => v.PropertyMappings)
                    .Include(p => p.Variants)
                        .ThenInclude(v => v.ItemSuppliers)
                    .FirstOrDefaultAsync(p => p.Id == parentId);

                if (parent == null)
                    return;

                var variants = parent.Variants.ToList();
                var variantIds = variants.Select(v => v.Id).ToList();

                var mappings = await context.ItemPropertyMappings
                    .Where(m => variantIds.Contains(m.ItemVariantId))
                    .ToListAsync();

                var suppliers = await context.ItemSuppliers
                    .Where(s => variantIds.Contains(s.ItemVariantId))
                    .ToListAsync();

                if (suppliers.Any())
                {
                    throw new InvalidOperationException(
                        "Item cannot be deleted while supplier links exist. Remove all supplier links first.");
                }

                context.ItemPropertyMappings.RemoveRange(mappings);
                context.ItemVariants.RemoveRange(variants);
                context.ItemParents.Remove(parent);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeactivateMatrixAsync(int parentId)
        {
            if (parentId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var parent = await context.ItemParents
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == parentId);

            if (parent == null)
                return;

            DateTime now = DateTime.Now;

            parent.IsDeactivated = true;
            parent.UpdatedAt = now;
            parent.DeactivatedAt ??= now;

            foreach (var variant in parent.Variants)
            {
                variant.IsDeactivated = true;
                variant.UpdatedAt = now;
                variant.DeactivatedAt ??= now;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteMatrixAsync(int parentId)
        {
            await DeactivateMatrixAsync(parentId);
        }

        public async Task ReactivateMatrixAsync(int parentId)
        {
            if (parentId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var parent = await context.ItemParents
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == parentId);

            if (parent == null)
                return;

            DateTime now = DateTime.Now;

            parent.IsDeactivated = false;
            parent.UpdatedAt = now;
            parent.DeactivatedAt = null;

            foreach (var variant in parent.Variants)
            {
                variant.IsDeactivated = false;
                variant.UpdatedAt = now;
                variant.DeactivatedAt = null;
            }

            await context.SaveChangesAsync();
        }

        public async Task<bool> ParentHasHistoryAsync(int parentId)
        {
            if (parentId <= 0)
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var variantIds = await context.ItemVariants
                .Where(v => v.ItemParentId == parentId)
                .Select(v => v.Id)
                .ToListAsync();

            if (!variantIds.Any())
                return false;

            var usedVariantIds = await GetUsedVariantIdsAsync(context, variantIds);

            return usedVariantIds.Any();
        }

        private static async Task<HashSet<int>> GetUsedVariantIdsAsync(
            AppDbContext context,
            List<int> variantIds)
        {
            var usedIds = new HashSet<int>();

            if (!variantIds.Any())
                return usedIds;

            await AddLinkedVariantIdsAsync(context, context.ItemBatches, variantIds, usedIds);
            await AddLinkedVariantIdsAsync(context, context.InventoryTransactions, variantIds, usedIds);
            await AddLinkedVariantIdsAsync(context, context.GrnLines, variantIds, usedIds);
            await AddLinkedVariantIdsAsync(context, context.PoLines, variantIds, usedIds);
            await AddLinkedVariantIdsAsync(context, context.SalesLines, variantIds, usedIds);
            await AddLinkedVariantIdsAsync(context, context.CustomerReturnLines, variantIds, usedIds);
            await AddLinkedVariantIdsAsync(context, context.SupplierReturnLines, variantIds, usedIds);
            await AddLinkedVariantIdsAsync(context, context.StockAdjustmentLines, variantIds, usedIds);

            return usedIds;
        }

        private static async Task<int> CountLinkedRowsAsync<TEntity>(
            AppDbContext context,
            IQueryable<TEntity> query,
            List<int> variantIds) where TEntity : class
        {
            if (!variantIds.Any())
                return 0;

            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var property = entityType?.FindProperty("ItemVariantId");

            if (property == null)
                return 0;

            if (property.ClrType == typeof(int))
            {
                return await query.CountAsync(e =>
                    variantIds.Contains(EF.Property<int>(e, "ItemVariantId")));
            }

            if (property.ClrType == typeof(int?))
            {
                return await query.CountAsync(e =>
                    EF.Property<int?>(e, "ItemVariantId").HasValue &&
                    variantIds.Contains(EF.Property<int?>(e, "ItemVariantId")!.Value));
            }

            return 0;
        }

        private static async Task AddLinkedVariantIdsAsync<TEntity>(
            AppDbContext context,
            IQueryable<TEntity> query,
            List<int> variantIds,
            HashSet<int> usedIds) where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var property = entityType?.FindProperty("ItemVariantId");

            if (property == null)
                return;

            if (property.ClrType == typeof(int))
            {
                var ids = await query
                    .Where(e => variantIds.Contains(EF.Property<int>(e, "ItemVariantId")))
                    .Select(e => EF.Property<int>(e, "ItemVariantId"))
                    .Distinct()
                    .ToListAsync();

                foreach (int id in ids)
                    usedIds.Add(id);

                return;
            }

            if (property.ClrType == typeof(int?))
            {
                var ids = await query
                    .Where(e => EF.Property<int?>(e, "ItemVariantId").HasValue &&
                                variantIds.Contains(EF.Property<int?>(e, "ItemVariantId")!.Value))
                    .Select(e => EF.Property<int?>(e, "ItemVariantId")!.Value)
                    .Distinct()
                    .ToListAsync();

                foreach (int id in ids)
                    usedIds.Add(id);
            }
        }

        // =========================================================
        // PO / GRN ITEM LOOKUP
        // =========================================================

        public async Task<ItemVariant?> GetItemByBarcodeAsync(string barcodeOrSku)
        {
            string term = NormalizeText(barcodeOrSku);

            if (string.IsNullOrWhiteSpace(term))
                return null;

            string upperTerm = term.ToUpperInvariant();

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemVariants
                .Include(v => v.ItemParent)
                .Include(v => v.ItemSuppliers)
                    .ThenInclude(s => s.Supplier)
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    !v.ItemParent.IsPurchaseLocked &&
                    (
                        v.SkuCode.ToUpper() == upperTerm ||
                        ((v.Barcode ?? string.Empty).ToUpper()) == upperTerm
                    ));
        }

        public async Task<List<ItemVariant>> GetVariantsByParentIdAsync(int parentId)
        {
            if (parentId <= 0)
                return new List<ItemVariant>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemVariants
                .Include(v => v.ItemParent)
                .Include(v => v.ItemSuppliers)
                    .ThenInclude(s => s.Supplier)
                .Where(v =>
                    v.ItemParentId == parentId &&
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated)
                .AsNoTracking()
                .OrderBy(v => v.VariantDescription)
                .ThenBy(v => v.SkuCode)
                .ToListAsync();
        }

        // =========================================================
        // CASHIER SEEK / PRODUCT SEARCH
        // =========================================================

        public async Task<List<ParentSeekDto>> SearchSeekParentsAsync(
            string searchTerm,
            string categoryFilter = "ALL CATEGORIES")
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemParents
                .AsNoTracking()
                .Where(p => !p.IsDeactivated && !p.IsSaleLocked);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();
                string upperTerm = term.ToUpperInvariant();

                query = query.Where(p =>
                    EF.Functions.Like(p.ItemName, $"%{term}%") ||
                    EF.Functions.Like(p.ItemCode, $"%{term}%") ||
                    p.Variants.Any(v =>
                        !v.IsDeactivated &&
                        (
                            v.SkuCode.ToUpper() == upperTerm ||
                            ((v.Barcode ?? string.Empty).ToUpper()) == upperTerm
                        )));
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter) &&
                categoryFilter != "ALL CATEGORIES")
            {
                query = query.Where(p => p.Category.CategoryName == categoryFilter);
            }

            return await query
                .OrderBy(p => p.ItemName)
                .Select(p => new ParentSeekDto
                {
                    ParentId = p.Id,
                    ItemCode = p.ItemCode,
                    ItemName = p.ItemName,
                    CategoryName = p.Category.CategoryName,
                    ActiveVariantsCount = p.Variants.Count(v => !v.IsDeactivated),
                    HasBatchTracking = p.HasBatchTracking,
                    HasExpiryTracking = p.HasExpiryTracking || p.HasBatchExpiry
                })
                .Take(100)
                .ToListAsync();
        }

        public async Task<List<VariantSeekDto>> GetSeekVariantsAsync(int parentId)
        {
            if (parentId <= 0)
                return new List<VariantSeekDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var variants = await context.ItemVariants
                .Where(v =>
                    v.ItemParentId == parentId &&
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    !v.ItemParent.IsSaleLocked)
                .Select(v => new
                {
                    v.Id,
                    ParentId = v.ItemParentId,
                    v.SkuCode,
                    v.Barcode,
                    v.VariantDescription,
                    v.RetailPrice,
                    HasBatchTracking = v.ItemParent.HasBatchTracking,
                    HasExpiryTracking = v.ItemParent.HasExpiryTracking || v.ItemParent.HasBatchExpiry
                })
                .AsNoTracking()
                .ToListAsync();

            if (!variants.Any())
                return new List<VariantSeekDto>();

            var variantIds = variants
                .Select(v => v.Id)
                .ToList();

            var stockRows = await context.ItemBatches
                .Where(b =>
                    variantIds.Contains(b.ItemVariantId) &&
                    !b.IsDeactivated)
                .Select(b => new
                {
                    b.ItemVariantId,
                    b.CurrentStock
                })
                .AsNoTracking()
                .ToListAsync();

            var stockData = stockRows
                .GroupBy(b => b.ItemVariantId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.CurrentStock));

            var results = new List<VariantSeekDto>();

            foreach (var variant in variants)
            {
                decimal stock = stockData.TryGetValue(variant.Id, out decimal stockOnHand)
                    ? stockOnHand
                    : 0m;

                results.Add(new VariantSeekDto
                {
                    VariantId = variant.Id,
                    ParentId = variant.ParentId,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,
                    RetailPrice = variant.RetailPrice,
                    StockOnHand = stock,
                    HasBatchTracking = variant.HasBatchTracking,
                    HasExpiryTracking = variant.HasExpiryTracking
                });
            }

            return results
                .OrderBy(v => v.VariantDescription)
                .ThenBy(v => v.SkuCode)
                .ToList();
        }

        public async Task<List<BatchSeekDto>> GetSeekBatchesByVariantIdAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<BatchSeekDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            DateTime today = DateTime.Today;

            var rows = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock > 0 &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsSaleLocked &&
                    b.ItemVariant.ItemParent.HasBatchTracking &&
                    !string.IsNullOrWhiteSpace(b.InternalBatchBarcode) &&
                    (!b.ExpiryDate.HasValue || b.ExpiryDate.Value >= today))
                .Select(b => new BatchSeekDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    InternalBatchBarcode = b.InternalBatchBarcode ?? string.Empty,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    CostPrice = b.CostPrice,
                    RetailPrice = b.RetailPrice,
                    WholesalePrice = b.WholesalePrice,
                    AvailableQty = b.CurrentStock
                })
                .ToListAsync();

            return rows
                .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(b => b.ExpiryDate)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchNo)
                .ToList();
        }

        // =========================================================
        // CASHIER BATCH / BARCODE RESOLUTION
        // =========================================================

        public async Task<CashierBatchDto?> GetSellableBatchByInternalBarcodeAsync(string internalBatchBarcode)
        {
            string term = NormalizeText(internalBatchBarcode);

            if (string.IsNullOrWhiteSpace(term))
                return null;

            string upperTerm = term.ToUpperInvariant();

            await using var context = await _contextFactory.CreateDbContextAsync();

            DateTime today = DateTime.Today;

            return await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeactivated &&
                    b.CurrentStock > 0 &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsSaleLocked &&
                    b.ItemVariant.ItemParent.HasBatchTracking &&
                    !string.IsNullOrWhiteSpace(b.InternalBatchBarcode) &&
                    b.InternalBatchBarcode.ToUpper() == upperTerm &&
                    (!b.ExpiryDate.HasValue || b.ExpiryDate.Value >= today))
                .Select(b => new CashierBatchDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    CostPrice = b.CostPrice,
                    RetailPrice = b.RetailPrice,
                    WholesalePrice = b.WholesalePrice,
                    AvailableQty = b.CurrentStock
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CashierBatchDto?> GetGeneralSellableBatchForVariantAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock > 0 &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsSaleLocked &&
                    !b.ItemVariant.ItemParent.HasBatchTracking &&
                    b.BatchNo.ToUpper() == GeneralBatchNo)
                .OrderBy(b => b.Id)
                .Select(b => new CashierBatchDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    CostPrice = b.CostPrice,
                    RetailPrice = b.RetailPrice,
                    WholesalePrice = b.WholesalePrice,
                    AvailableQty = b.CurrentStock
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<CashierBatchDto>> GetSellableBatchesByVariantIdAsync(int itemVariantId)
        {
            if (itemVariantId <= 0)
                return new List<CashierBatchDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            DateTime today = DateTime.Today;

            var batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    b.ItemVariantId == itemVariantId &&
                    !b.IsDeactivated &&
                    b.CurrentStock > 0 &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsSaleLocked &&
                    (!b.ExpiryDate.HasValue || b.ExpiryDate.Value >= today))
                .Select(b => new CashierBatchDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    CostPrice = b.CostPrice,
                    RetailPrice = b.RetailPrice,
                    WholesalePrice = b.WholesalePrice,
                    AvailableQty = b.CurrentStock
                })
                .ToListAsync();

            return batches
                .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(b => b.ExpiryDate)
                .ThenBy(b => b.ReceivedDate)
                .ThenBy(b => b.BatchNo)
                .ToList();
        }

        public async Task<CashierBatchDto?> GetSellableBatchByIdAsync(int itemBatchId)
        {
            if (itemBatchId <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            DateTime today = DateTime.Today;

            return await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    b.Id == itemBatchId &&
                    !b.IsDeactivated &&
                    b.CurrentStock > 0 &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsSaleLocked &&
                    (!b.ExpiryDate.HasValue || b.ExpiryDate.Value >= today))
                .Select(b => new CashierBatchDto
                {
                    ItemBatchId = b.Id,
                    ItemVariantId = b.ItemVariantId,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    CostPrice = b.CostPrice,
                    RetailPrice = b.RetailPrice,
                    WholesalePrice = b.WholesalePrice,
                    AvailableQty = b.CurrentStock
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CashierSellableItemDto?> GetSellableItemByVariantIdAsync(int variantId)
        {
            if (variantId <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var variant = await context.ItemVariants
                .Include(v => v.ItemParent)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Include(v => v.ItemBatches)
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    v.Id == variantId &&
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    !v.ItemParent.IsSaleLocked);

            if (variant == null)
                return null;

            return BuildCashierSellableItemDto(variant);
        }

        public async Task<CashierSellableItemDto?> GetSellableItemByBarcodeOrSkuAsync(string barcodeOrSku)
        {
            string term = NormalizeText(barcodeOrSku);

            if (string.IsNullOrWhiteSpace(term))
                return null;

            string upperTerm = term.ToUpperInvariant();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var variant = await context.ItemVariants
                .Include(v => v.ItemParent)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Include(v => v.ItemBatches)
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    !v.ItemParent.IsSaleLocked &&
                    (
                        v.SkuCode.ToUpper() == upperTerm ||
                        ((v.Barcode ?? string.Empty).ToUpper()) == upperTerm
                    ));

            if (variant == null)
                return null;

            return BuildCashierSellableItemDto(variant);
        }

        private static CashierSellableItemDto BuildCashierSellableItemDto(ItemVariant variant)
        {
            decimal stockOnHand = variant.ItemBatches?
                .Where(b => !b.IsDeactivated)
                .Sum(b => b.CurrentStock) ?? 0m;

            return new CashierSellableItemDto
            {
                VariantId = variant.Id,

                ItemCode = variant.ItemParent?.ItemCode ?? string.Empty,
                SkuCode = variant.SkuCode,
                Barcode = variant.Barcode ?? string.Empty,

                Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                    ? "Standard"
                    : variant.VariantDescription,

                Uom = variant.ItemParent?.UnitOfMeasure?.UomCode
                    ?? variant.ItemParent?.BaseUom
                    ?? "PCS",

                AverageCost = variant.AverageCost,

                RetailPrice = variant.RetailPrice,
                WholesalePrice = variant.WholesalePrice,
                MinimumPrice = variant.MinimumPrice,
                MaximumPrice = variant.MaximumPrice,

                StockOnHand = stockOnHand,

                HasBatchTracking = variant.ItemParent?.HasBatchTracking ?? true,
                HasExpiryTracking =
                    variant.ItemParent?.HasExpiryTracking == true ||
                    variant.ItemParent?.HasBatchExpiry == true
            };
        }

        // =========================================================
        // VALIDATION HELPERS
        // =========================================================

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
        }

        private static void NormalizeParent(ItemParent parent)
        {
            parent.ItemCode = NormalizeCode(parent.ItemCode);
            parent.ItemName = NormalizeText(parent.ItemName);
            parent.PrintName = NormalizeText(parent.PrintName);
            parent.BaseUom = NormalizeText(parent.BaseUom);
            parent.TaxCode = NormalizeText(parent.TaxCode);

            if (parent.HasExpiryTracking && !parent.HasBatchTracking)
            {
                throw new InvalidOperationException(
                    "Expiry tracking requires batch tracking.");
            }

            parent.HasBatchExpiry = parent.HasExpiryTracking;
        }

        private static void ValidateParent(ItemParent parent)
        {
            if (string.IsNullOrWhiteSpace(parent.ItemCode))
                throw new InvalidOperationException("Item code is required.");

            if (parent.ItemCode.Length > 50)
                throw new InvalidOperationException("Item code cannot be longer than 50 characters.");

            if (string.IsNullOrWhiteSpace(parent.ItemName))
                throw new InvalidOperationException("Item name is required.");

            if (parent.ItemName.Length > 150)
                throw new InvalidOperationException("Item name cannot be longer than 150 characters.");

            if (parent.PrintName.Length > 50)
                throw new InvalidOperationException("Print name cannot be longer than 50 characters.");

            if (parent.CategoryId <= 0)
                throw new InvalidOperationException("Category is required.");

            if (parent.UnitOfMeasureId <= 0)
                throw new InvalidOperationException("Unit of Measure is required.");

            if (parent.TaxCode.Length > 20)
                throw new InvalidOperationException("Tax code cannot be longer than 20 characters.");

            if (parent.HasExpiryTracking && !parent.HasBatchTracking)
                throw new InvalidOperationException("Expiry tracking requires batch tracking.");
        }

        private static void NormalizeVariant(ItemVariant variant)
        {
            variant.SkuCode = NormalizeCode(variant.SkuCode);
            variant.Barcode = NormalizeText(variant.Barcode);
            variant.VariantDescription = NormalizeText(variant.VariantDescription);

            if (string.IsNullOrWhiteSpace(variant.VariantDescription))
                variant.VariantDescription = "Standard";
        }

        private static void ValidateVariant(ItemVariant variant)
        {
            if (string.IsNullOrWhiteSpace(variant.SkuCode))
                throw new InvalidOperationException("Variant SKU is required.");

            if (variant.SkuCode.Length > 100)
                throw new InvalidOperationException("Variant SKU cannot be longer than 100 characters.");

            if (variant.VariantDescription.Length > 250)
                throw new InvalidOperationException("Variant description cannot be longer than 250 characters.");

            if (variant.Barcode.Length > 100)
                throw new InvalidOperationException("Barcode cannot be longer than 100 characters.");

            if (variant.CostPrice < 0)
                throw new InvalidOperationException("Cost price cannot be negative.");

            if (variant.AverageCost < 0)
                throw new InvalidOperationException("Average cost cannot be negative.");

            if (variant.RetailPrice < 0)
                throw new InvalidOperationException("Retail price cannot be negative.");

            if (variant.WholesalePrice < 0)
                throw new InvalidOperationException("Wholesale price cannot be negative.");

            if (variant.MinimumPrice < 0)
                throw new InvalidOperationException("Minimum price cannot be negative.");

            if (variant.MaximumPrice < 0)
                throw new InvalidOperationException("Maximum price cannot be negative.");

            if (variant.MaximumPrice > 0 && variant.MinimumPrice > variant.MaximumPrice)
                throw new InvalidOperationException("Minimum price cannot be greater than maximum price.");

            if (variant.ReorderLevel < 0)
                throw new InvalidOperationException("Reorder level cannot be negative.");
        }

        private static void ValidateSubmittedVariantDuplicates(List<ItemVariant> variants)
        {
            var duplicateSku = variants
                .GroupBy(v => v.SkuCode, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateSku != null)
            {
                throw new InvalidOperationException(
                    $"Duplicate SKU found in generated variants: {duplicateSku.Key}");
            }

            var duplicateBarcode = variants
                .Where(v => !string.IsNullOrWhiteSpace(v.Barcode))
                .GroupBy(v => v.Barcode, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateBarcode != null)
            {
                throw new InvalidOperationException(
                    $"Duplicate barcode found in generated variants: {duplicateBarcode.Key}");
            }
        }

        private static string BuildMappingKey(IEnumerable<ItemPropertyMapping>? mappings)
        {
            if (mappings == null)
                return string.Empty;

            return string.Join("|",
                mappings
                    .Select(m => $"{m.AttributeGroupId}:{m.AttributeValueId}")
                    .OrderBy(x => x));
        }

        private static string NormalizeCode(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}