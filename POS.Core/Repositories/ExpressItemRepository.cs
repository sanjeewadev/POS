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
    public class ExpressItemRepository
    {
        private const int MaxGridRows = 20;
        private const int MaxGridColumns = 5;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ExpressItemRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // ADMIN - SEARCH SELLABLE ITEMS
        // =========================================================

        public async Task<List<ExpressItemSearchDto>> SearchSellableItemsAsync(string searchText)
        {
            string search = NormalizeText(searchText).ToUpperInvariant();

            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(v =>
                    v.SkuCode.ToUpper().Contains(search) ||
                    v.Barcode.ToUpper().Contains(search) ||
                    v.ItemParent.ItemCode.ToUpper().Contains(search) ||
                    v.ItemParent.ItemName.ToUpper().Contains(search) ||
                    v.VariantDescription.ToUpper().Contains(search));
            }

            var variants = await query
                .OrderBy(v => v.ItemParent.ItemName)
                .ThenBy(v => v.VariantDescription)
                .ThenBy(v => v.SkuCode)
                .Take(75)
                .Select(v => new
                {
                    ItemVariantId = v.Id,
                    v.ItemParentId,
                    v.SkuCode,
                    v.Barcode,
                    v.VariantDescription,
                    v.RetailPrice,
                    ItemCode = v.ItemParent.ItemCode,
                    ItemName = v.ItemParent.ItemName
                })
                .ToListAsync();

            if (!variants.Any())
                return new List<ExpressItemSearchDto>();

            var variantIds = variants
                .Select(v => v.ItemVariantId)
                .ToList();

            var batchRows = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    variantIds.Contains(b.ItemVariantId) &&
                    !b.IsDeactivated &&
                    b.CurrentStock > 0)
                .Select(b => new
                {
                    b.ItemVariantId,
                    b.CurrentStock
                })
                .ToListAsync();

            // SQLite decimal-safe stock sum.
            var stockByVariant = batchRows
                .GroupBy(b => b.ItemVariantId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.CurrentStock));

            var results = new List<ExpressItemSearchDto>();

            foreach (var variant in variants)
            {
                decimal stock = stockByVariant.TryGetValue(variant.ItemVariantId, out decimal stockOnHand)
                    ? stockOnHand
                    : 0m;

                results.Add(new ExpressItemSearchDto
                {
                    ItemVariantId = variant.ItemVariantId,
                    ItemParentId = variant.ItemParentId,
                    ItemCode = variant.ItemCode,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,
                    ItemName = variant.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,
                    RetailPrice = variant.RetailPrice,
                    StockOnHand = stock
                });
            }

            return results;
        }

        // Compatibility method for older generated ViewModels.
        public async Task<List<ItemVariant>> SearchVariantsAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string search = NormalizeText(searchTerm).ToUpperInvariant();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(v =>
                    v.SkuCode.ToUpper().Contains(search) ||
                    v.Barcode.ToUpper().Contains(search) ||
                    v.ItemParent.ItemName.ToUpper().Contains(search) ||
                    v.VariantDescription.ToUpper().Contains(search));
            }

            return await query
                .OrderBy(v => v.ItemParent.ItemName)
                .ThenBy(v => v.VariantDescription)
                .Take(75)
                .ToListAsync();
        }

        // =========================================================
        // ADMIN - LOAD EXISTING BUTTONS
        // =========================================================

        public async Task<List<ExpressItemLayoutDto>> GetAdminLayoutsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var rows = await context.ExpressItemLayouts
                .AsNoTracking()
                .Where(e =>
                    e.ItemVariant != null &&
                    !e.ItemVariant.IsDeactivated &&
                    !e.ItemVariant.ItemParent.IsDeactivated)
                .OrderBy(e => e.GridRow)
                .ThenBy(e => e.GridColumn)
                .ThenBy(e => e.DisplayLabel)
                .Select(e => new
                {
                    LayoutId = e.Id,
                    e.ItemVariantId,
                    e.DisplayLabel,
                    e.ButtonColorHex,
                    e.TextColorHex,
                    e.GridRow,
                    e.GridColumn,
                    e.IsActive,
                    e.ItemVariant!.SkuCode,
                    e.ItemVariant.Barcode,
                    e.ItemVariant.VariantDescription,
                    e.ItemVariant.RetailPrice,
                    ItemCode = e.ItemVariant.ItemParent.ItemCode,
                    ItemName = e.ItemVariant.ItemParent.ItemName
                })
                .ToListAsync();

            return rows
                .Select(e => new ExpressItemLayoutDto
                {
                    LayoutId = e.LayoutId,
                    ItemVariantId = e.ItemVariantId,
                    ItemCode = e.ItemCode,
                    SkuCode = e.SkuCode,
                    Barcode = e.Barcode ?? string.Empty,
                    ItemName = e.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(e.VariantDescription)
                        ? "Standard"
                        : e.VariantDescription,
                    RetailPrice = e.RetailPrice,
                    DisplayLabel = e.DisplayLabel,
                    ButtonColorHex = string.IsNullOrWhiteSpace(e.ButtonColorHex) ? "#005555" : e.ButtonColorHex,
                    TextColorHex = string.IsNullOrWhiteSpace(e.TextColorHex) ? "#FFFFFF" : e.TextColorHex,
                    GridRow = e.GridRow <= 0 ? 1 : e.GridRow,
                    GridColumn = e.GridColumn <= 0 ? 1 : e.GridColumn,
                    IsActive = e.IsActive
                })
                .ToList();
        }

        // Compatibility method for older generated ViewModels and cashier VM.
        public async Task<List<ExpressItemLayout>> GetAllLayoutsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ExpressItemLayouts
                .Include(e => e.ItemVariant)
                    .ThenInclude(v => v!.ItemParent)
                .OrderBy(e => e.GridRow)
                .ThenBy(e => e.GridColumn)
                .AsNoTracking()
                .ToListAsync();
        }

        // =========================================================
        // CASHIER - LOAD ACTIVE EXPRESS BUTTONS
        // =========================================================

        public async Task<List<ExpressItemButtonDto>> GetActiveCashierButtonsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ExpressItemLayouts
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.ItemVariant != null &&
                    !e.ItemVariant.IsDeactivated &&
                    !e.ItemVariant.ItemParent.IsDeactivated)
                .OrderBy(e => e.GridRow)
                .ThenBy(e => e.GridColumn)
                .ThenBy(e => e.DisplayLabel)
                .Select(e => new ExpressItemButtonDto
                {
                    LayoutId = e.Id,
                    ItemVariantId = e.ItemVariantId,
                    ItemCode = e.ItemVariant!.ItemParent.ItemCode,
                    SkuCode = e.ItemVariant.SkuCode,
                    Barcode = e.ItemVariant.Barcode ?? string.Empty,
                    ItemName = e.ItemVariant.ItemParent.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(e.ItemVariant.VariantDescription)
                        ? "Standard"
                        : e.ItemVariant.VariantDescription,
                    DisplayLabel = e.DisplayLabel,
                    ButtonColorHex = string.IsNullOrWhiteSpace(e.ButtonColorHex)
                        ? "#005555"
                        : e.ButtonColorHex,
                    TextColorHex = string.IsNullOrWhiteSpace(e.TextColorHex)
                        ? "#FFFFFF"
                        : e.TextColorHex,
                    GridRow = e.GridRow <= 0 ? 1 : e.GridRow,
                    GridColumn = e.GridColumn <= 0 ? 1 : e.GridColumn,
                    RetailPrice = e.ItemVariant.RetailPrice
                })
                .ToListAsync();
        }

        // =========================================================
        // POSITION HELPER
        // =========================================================

        public async Task<(int Row, int Column)> GetNextAvailablePositionAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var occupied = await context.ExpressItemLayouts
                .AsNoTracking()
                .Select(e => new
                {
                    e.GridRow,
                    e.GridColumn
                })
                .ToListAsync();

            var occupiedSet = occupied
                .Where(x => x.GridRow > 0 && x.GridColumn > 0)
                .Select(x => $"{x.GridRow}:{x.GridColumn}")
                .ToHashSet();

            for (int row = 1; row <= MaxGridRows; row++)
            {
                for (int column = 1; column <= MaxGridColumns; column++)
                {
                    if (!occupiedSet.Contains($"{row}:{column}"))
                        return (row, column);
                }
            }

            return (MaxGridRows, MaxGridColumns);
        }

        // =========================================================
        // SAVE
        // =========================================================

        public async Task<ExpressItemLayoutDto> SaveLayoutAsync(ExpressItemLayoutDto layout)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            var validationErrors = layout.ValidateForSave();

            if (validationErrors.Any())
            {
                throw new InvalidOperationException(
                    "Express item validation failed:\n" +
                    string.Join("\n", validationErrors));
            }

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await ValidateItemIsSellableAsync(context, layout.ItemVariantId);

                await ValidateNoDuplicateItemAsync(
                    context,
                    layout.ItemVariantId,
                    layout.LayoutId);

                await ValidateNoPositionCollisionAsync(
                    context,
                    layout.GridRow,
                    layout.GridColumn,
                    layout.LayoutId);

                ExpressItemLayout entity;

                if (layout.LayoutId == 0)
                {
                    entity = new ExpressItemLayout();
                    await context.ExpressItemLayouts.AddAsync(entity);
                }
                else
                {
                    entity = await context.ExpressItemLayouts
                        .FirstOrDefaultAsync(e => e.Id == layout.LayoutId)
                        ?? throw new InvalidOperationException("Selected express button was not found.");
                }

                entity.ItemVariantId = layout.ItemVariantId;
                entity.TabCategory = ExpressItemLayout.MainTabCategory;
                entity.DisplayLabel = NormalizeLabel(layout.DisplayLabel);
                entity.ButtonColorHex = NormalizeColor(layout.ButtonColorHex, "#005555");
                entity.TextColorHex = NormalizeColor(layout.TextColorHex, "#FFFFFF");
                entity.GridRow = layout.GridRow;
                entity.GridColumn = layout.GridColumn;
                entity.IsActive = layout.IsActive;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                layout.LayoutId = entity.Id;

                return layout;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Compatibility method for older generated ViewModels.
        public async Task<ExpressItemLayout> SaveLayoutAsync(ExpressItemLayout layout)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            var dto = new ExpressItemLayoutDto
            {
                LayoutId = layout.Id,
                ItemVariantId = layout.ItemVariantId,
                DisplayLabel = layout.DisplayLabel,
                ButtonColorHex = layout.ButtonColorHex,
                TextColorHex = layout.TextColorHex,
                GridRow = layout.GridRow <= 0 ? 1 : layout.GridRow,
                GridColumn = layout.GridColumn <= 0 ? 1 : layout.GridColumn,
                IsActive = layout.IsActive
            };

            var saved = await SaveLayoutAsync(dto);

            layout.Id = saved.LayoutId;
            layout.TabCategory = ExpressItemLayout.MainTabCategory;

            return layout;
        }

        // =========================================================
        // DELETE
        // =========================================================

        public async Task DeleteLayoutAsync(int id)
        {
            if (id <= 0)
                return;

            using var context = await _contextFactory.CreateDbContextAsync();

            var layout = await context.ExpressItemLayouts
                .FirstOrDefaultAsync(e => e.Id == id);

            if (layout == null)
                return;

            context.ExpressItemLayouts.Remove(layout);
            await context.SaveChangesAsync();
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private static async Task ValidateItemIsSellableAsync(
            AppDbContext context,
            int itemVariantId)
        {
            bool exists = await context.ItemVariants
                .AsNoTracking()
                .AnyAsync(v =>
                    v.Id == itemVariantId &&
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated);

            if (!exists)
                throw new InvalidOperationException("Selected item is inactive or does not exist.");
        }

        private static async Task ValidateNoDuplicateItemAsync(
            AppDbContext context,
            int itemVariantId,
            int currentLayoutId)
        {
            bool duplicate = await context.ExpressItemLayouts
                .AsNoTracking()
                .AnyAsync(e =>
                    e.ItemVariantId == itemVariantId &&
                    e.Id != currentLayoutId);

            if (duplicate)
            {
                throw new InvalidOperationException(
                    "This item already has an express button. Edit the existing button instead of creating another one.");
            }
        }

        private static async Task ValidateNoPositionCollisionAsync(
            AppDbContext context,
            int gridRow,
            int gridColumn,
            int currentLayoutId)
        {
            bool occupied = await context.ExpressItemLayouts
                .AsNoTracking()
                .AnyAsync(e =>
                    e.GridRow == gridRow &&
                    e.GridColumn == gridColumn &&
                    e.Id != currentLayoutId);

            if (occupied)
            {
                throw new InvalidOperationException(
                    $"Another express button already uses Row {gridRow}, Column {gridColumn}. Choose a different position.");
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeLabel(string? value)
        {
            string label = NormalizeText(value);

            if (label.Length > 50)
                label = label.Substring(0, 50).Trim();

            return label;
        }

        private static string NormalizeColor(string? value, string fallback)
        {
            string color = NormalizeText(value);

            if (string.IsNullOrWhiteSpace(color))
                return fallback;

            return color;
        }
    }
}