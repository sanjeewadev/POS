using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    // Existing DTO for the Admin Item Master Grid
    public class ItemMasterSummaryDto
    {
        public int ParentId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int VariantCount { get; set; }
        public decimal TotalStockOnHand { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    // ==========================================================
    // NEW: DUAL-TABLE DTOs FOR THE CASHIER PLU / SEEK SCREEN
    // ==========================================================

    // Represents Table 1: The Generic "Parent" Item
    public class ParentSeekDto
    {
        public int ParentId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int ActiveVariantsCount { get; set; }
    }

    // Represents Table 2: The Specific "Variants" (Sizes/Colors)
    public class VariantSeekDto
    {
        public int VariantId { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public decimal StockOnHand { get; set; }
    }

    public class ItemMasterRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ItemMasterRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // --- ADMIN GRID DATA : LIGHTWEIGHT SUMMARY FETCH ---
        // ==============================================================================
        public async Task<IEnumerable<ItemMasterSummaryDto>> GetSummariesAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemParents
                .Where(p => !p.IsDeactivated)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearch = searchTerm.ToLower();
                query = query.Where(p =>
                    p.ItemCode.ToLower().Contains(lowerSearch) ||
                    p.ItemName.ToLower().Contains(lowerSearch) ||
                    p.Category.CategoryName.ToLower().Contains(lowerSearch));
            }

            var itemsList = await query.Select(p => new ItemMasterSummaryDto
            {
                ParentId = p.Id,
                ItemCode = p.ItemCode,
                ItemName = p.ItemName,
                CategoryName = p.Category.CategoryName,
                VariantCount = p.Variants.Count(v => !v.IsDeactivated),
                TotalStockOnHand = 0m,
                StatusText = "Active"
            }).ToListAsync();

            if (itemsList.Any())
            {
                var parentIds = itemsList.Select(i => i.ParentId).ToList();
                var rawStockData = await context.InventoryTransactions
                    .Include(t => t.ItemVariant)
                    .Where(t => parentIds.Contains(t.ItemVariant.ItemParentId))
                    .Select(t => new { t.ItemVariant.ItemParentId, t.Quantity })
                    .ToListAsync();

                foreach (var item in itemsList)
                {
                    item.TotalStockOnHand = rawStockData
                        .Where(s => s.ItemParentId == item.ParentId)
                        .Sum(s => s.Quantity);
                }
            }

            return itemsList;
        }

        // ==============================================================================
        // --- FULL MATRIX FETCH (For editing and viewing in the Admin UI) ---
        // ==============================================================================
        public async Task<ItemParent?> GetFullMatrixByIdAsync(int parentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemParents
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
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
            using var context = await _contextFactory.CreateDbContextAsync();
            return !await context.ItemParents.AnyAsync(p => p.ItemCode.ToLower() == itemCode.ToLower() && p.Id != currentParentId);
        }

        // --- ATOMIC MATRIX SAVING ENGINE ---
        public async Task SaveFullMatrixAsync(ItemParent parent, List<ItemVariant> variants, List<ItemPropertyMapping> mappings)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                parent.Variants = null!;

                if (parent.Id == 0)
                {
                    await context.ItemParents.AddAsync(parent);
                }
                else
                {
                    context.ItemParents.Update(parent);
                }
                await context.SaveChangesAsync();

                var existingVariants = await context.ItemVariants.Where(v => v.ItemParentId == parent.Id).ToListAsync();
                var existingVariantIds = existingVariants.Select(v => v.Id).ToList();

                var existingMappings = await context.ItemPropertyMappings.Where(m => existingVariantIds.Contains(m.ItemVariantId)).ToListAsync();
                var existingSuppliers = await context.ItemSuppliers.Where(s => existingVariantIds.Contains(s.ItemVariantId)).ToListAsync();

                context.ItemPropertyMappings.RemoveRange(existingMappings);
                context.ItemSuppliers.RemoveRange(existingSuppliers);
                context.ItemVariants.RemoveRange(existingVariants);
                await context.SaveChangesAsync();

                foreach (var variant in variants)
                {
                    variant.Id = 0;
                    variant.ItemParentId = parent.Id;

                    if (variant.PropertyMappings != null)
                    {
                        foreach (var map in variant.PropertyMappings)
                        {
                            map.ItemVariant = null!;
                        }
                    }

                    if (variant.ItemSuppliers != null)
                    {
                        foreach (var bridge in variant.ItemSuppliers)
                        {
                            bridge.Id = 0;
                            bridge.Supplier = null!;
                            bridge.ItemVariant = null!;
                        }
                    }

                    await context.ItemVariants.AddAsync(variant);
                }
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteMatrixAsync(int parentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var parent = await context.ItemParents
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == parentId);

            if (parent != null)
            {
                parent.IsDeactivated = true;
                foreach (var variant in parent.Variants)
                {
                    variant.IsDeactivated = true;
                }
                context.ItemParents.Update(parent);
                await context.SaveChangesAsync();
            }
        }

        // --- CASHIER & PO SEARCH ENGINE ---
        public async Task<ItemVariant?> GetItemByBarcodeAsync(string barcode)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ItemVariants
                .Include(v => v.ItemParent)
                .Include(v => v.ItemSuppliers)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => !v.IsDeactivated &&
                                    (v.SkuCode.ToLower() == barcode.ToLower() || v.Barcode.ToLower() == barcode.ToLower()));
        }

        public async Task<List<ItemVariant>> GetVariantsByParentIdAsync(int parentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ItemVariants
                .Include(v => v.ItemParent)
                .Include(v => v.ItemSuppliers)
                .Where(v => v.ItemParentId == parentId && !v.IsDeactivated)
                .AsNoTracking()
                .ToListAsync();
        }

        // ==============================================================================
        // --- NEW: DUAL-TABLE PLU / SEEK ENGINE FOR CASHIER UI ---
        // ==============================================================================

        /// <summary>
        /// Populates Table 1: Searches for Parent items by Name, Code, or their Variants' Barcodes.
        /// </summary>
        public async Task<List<ParentSeekDto>> SearchSeekParentsAsync(string searchTerm, string categoryFilter = "ALL CATEGORIES")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemParents
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Where(p => !p.IsDeactivated)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower().Trim();
                // Match parent name, parent code, OR any of its variants' SKUs/Barcodes
                query = query.Where(p =>
                    p.ItemName.ToLower().Contains(term) ||
                    p.ItemCode.ToLower().Contains(term) ||
                    p.Variants.Any(v => !v.IsDeactivated && (v.SkuCode.ToLower() == term || v.Barcode.ToLower() == term))
                );
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter) && categoryFilter != "ALL CATEGORIES")
            {
                query = query.Where(p => p.Category.CategoryName == categoryFilter);
            }

            return await query
                .Select(p => new ParentSeekDto
                {
                    ParentId = p.Id,
                    ItemCode = p.ItemCode,
                    ItemName = p.ItemName,
                    CategoryName = p.Category.CategoryName,
                    ActiveVariantsCount = p.Variants.Count(v => !v.IsDeactivated)
                })
                .Take(100) // Performance cap
                .OrderBy(p => p.ItemName)
                .ToListAsync();
        }

        /// <summary>
        /// Populates Table 2: Gets specific sizes, colors, pricing, and live stock for a Parent.
        /// </summary>
        public async Task<List<VariantSeekDto>> GetSeekVariantsAsync(int parentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var variants = await context.ItemVariants
                .Where(v => v.ItemParentId == parentId && !v.IsDeactivated)
                .Select(v => new
                {
                    v.Id,
                    v.SkuCode,
                    v.Barcode,
                    v.VariantDescription,
                    v.RetailPrice
                })
                .AsNoTracking()
                .ToListAsync();

            if (!variants.Any()) return new List<VariantSeekDto>();

            var variantIds = variants.Select(v => v.Id).ToList();

            // Calculate exact physical stock currently sitting in active batches
            var stockData = await context.ItemBatches
                .Where(b => variantIds.Contains(b.ItemVariantId) && !b.IsDeactivated)
                .GroupBy(b => b.ItemVariantId)
                .Select(g => new { VariantId = g.Key, TotalStock = g.Sum(b => b.CurrentStock) })
                .ToListAsync();

            var results = new List<VariantSeekDto>();

            foreach (var v in variants)
            {
                var stock = stockData.FirstOrDefault(s => s.VariantId == v.Id)?.TotalStock ?? 0m;
                results.Add(new VariantSeekDto
                {
                    VariantId = v.Id,
                    SkuCode = v.SkuCode,
                    Barcode = v.Barcode ?? string.Empty,
                    VariantDescription = string.IsNullOrWhiteSpace(v.VariantDescription) ? "Standard / Base" : v.VariantDescription,
                    RetailPrice = v.RetailPrice,
                    StockOnHand = stock
                });
            }

            return results.OrderBy(v => v.VariantDescription).ToList();
        }
    }
}