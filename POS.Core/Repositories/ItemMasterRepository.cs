using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    // Existing DTO for the Item Master Grid
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

    // Lightweight container for Barcode Management Grid
    public class BarcodeManagementDto
    {
        public int VariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public bool IsDeactivated { get; set; }

        public bool IsSelected { get; set; } = false;
    }

    // Lightweight container for the Live-Add Seek Window
    public class ProductSeekDto
    {
        public string Barcode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
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
        // --- MASTER GRID DATA : LIGHTWEIGHT SUMMARY FETCH ---
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
        // --- FULL MATRIX FETCH (NEW: For editing and viewing in the UI) ---
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
                // Sever the tie to the children to prevent recursive tracking errors
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
                    // Reset IDs to force a brand new INSERT
                    variant.Id = 0;
                    variant.ItemParentId = parent.Id;

                    if (variant.PropertyMappings != null)
                    {
                        foreach (var map in variant.PropertyMappings)
                        {
                            map.ItemVariant = null!; // Prevent circular tracking loops
                        }
                    }

                    if (variant.ItemSuppliers != null)
                    {
                        foreach (var bridge in variant.ItemSuppliers)
                        {
                            bridge.Id = 0; // Force Insert for the Supplier bridge
                            bridge.Supplier = null!; // Prevent UNIQUE constraint crash
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
                // ✅ NEW: Added Include so PO Scanner pulls supplier costs and codes!
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
        // --- BARCODE MANAGEMENT ENGINE ---
        // ==============================================================================

        public async Task<List<BarcodeManagementDto>> GetBarcodeManagementListAsync(string searchTerm = "", int? categoryId = null, bool activeOnly = true)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .ThenInclude(p => p.Category)
                .AsNoTracking()
                .AsQueryable();

            if (activeOnly)
            {
                query = query.Where(v => !v.IsDeactivated && !v.ItemParent.IsDeactivated);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var search = searchTerm.ToLower();
                query = query.Where(v =>
                    (v.Barcode != null && v.Barcode.ToLower().Contains(search)) ||
                    v.ItemParent.ItemCode.ToLower().Contains(search) ||
                    v.ItemParent.ItemName.ToLower().Contains(search) ||
                    v.VariantDescription.ToLower().Contains(search));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.ItemParent.CategoryId == categoryId.Value);
            }

            return await query
                .Select(v => new BarcodeManagementDto
                {
                    VariantId = v.Id,
                    ItemCode = v.ItemParent.ItemCode,
                    ItemName = v.ItemParent.ItemName,
                    VariantDescription = v.VariantDescription,
                    Barcode = v.Barcode ?? string.Empty,
                    CategoryName = v.ItemParent.Category.CategoryName,
                    IsDeactivated = v.IsDeactivated,
                    IsSelected = false
                })
                .OrderBy(v => v.ItemCode)
                .ToListAsync();
        }

        public async Task UpdateBarcodeAsync(int variantId, string newBarcode)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var variant = await context.ItemVariants.FindAsync(variantId);
            if (variant == null) throw new Exception("CRITICAL: Variant not found in database.");

            string formattedBarcode = newBarcode?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(formattedBarcode))
            {
                bool exists = await context.ItemVariants.AnyAsync(v => v.Barcode == formattedBarcode && v.Id != variantId);
                if (exists)
                {
                    throw new Exception($"The barcode '{formattedBarcode}' is already assigned to another item in your system.");
                }
            }

            variant.Barcode = formattedBarcode;
            context.ItemVariants.Update(variant);
            await context.SaveChangesAsync();
        }

        public async Task AutoGenerateBarcodesAsync(List<int> variantIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var variants = await context.ItemVariants
                    .Where(v => variantIds.Contains(v.Id) && string.IsNullOrWhiteSpace(v.Barcode))
                    .ToListAsync();

                foreach (var variant in variants)
                {
                    string generatedSku = $"20{variant.Id.ToString("D8")}";

                    while (await context.ItemVariants.AnyAsync(v => v.Barcode == generatedSku))
                    {
                        generatedSku = $"20{new Random().Next(10000000, 99999999)}";
                    }

                    variant.Barcode = generatedSku;
                    context.ItemVariants.Update(variant);
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

        // ==============================================================================
        // --- CONTINUOUS SEEK SEARCH ENGINE ---
        // ==============================================================================

        public async Task<List<ProductSeekDto>> SearchSeekItemsAsync(string nameFilter, string barcodeFilter, string categoryFilter)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .ThenInclude(p => p.Category)
                .Where(v => !v.IsDeactivated && !v.ItemParent.IsDeactivated)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                query = query.Where(v => v.ItemParent.ItemName.ToLower().Contains(nameFilter) ||
                                        (v.VariantDescription != null && v.VariantDescription.ToLower().Contains(nameFilter)));
            }

            if (!string.IsNullOrWhiteSpace(barcodeFilter))
            {
                query = query.Where(v => (v.Barcode != null && v.Barcode.ToLower().Contains(barcodeFilter)) ||
                                         (v.SkuCode != null && v.SkuCode.ToLower().Contains(barcodeFilter)));
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter) && categoryFilter != "ALL CATEGORIES")
            {
                query = query.Where(v => v.ItemParent.Category.CategoryName == categoryFilter);
            }

            var rawResults = await query
                .Select(v => new
                {
                    VariantId = v.Id,
                    RawBarcode = v.Barcode,
                    RawSku = v.SkuCode,
                    ParentName = v.ItemParent.ItemName,
                    VariantDesc = v.VariantDescription,
                    CategoryName = v.ItemParent.Category.CategoryName,
                    RetailPrice = v.RetailPrice
                })
                .Take(100)
                .ToListAsync();

            if (rawResults.Any())
            {
                var variantIds = rawResults.Select(r => r.VariantId).ToList();

                var stockData = await context.ItemBatches
                    .Where(b => variantIds.Contains(b.ItemVariantId) && !b.IsDeactivated)
                    .Select(b => new { b.ItemVariantId, b.CurrentStock })
                    .ToListAsync();

                var finalResults = rawResults.Select(r => new ProductSeekDto
                {
                    Barcode = string.IsNullOrWhiteSpace(r.RawBarcode) ? r.RawSku : r.RawBarcode,
                    Description = string.IsNullOrWhiteSpace(r.VariantDesc)
                                  ? r.ParentName
                                  : r.ParentName + " - " + r.VariantDesc,
                    CategoryName = r.CategoryName,
                    RetailPrice = r.RetailPrice,
                    StockOnHand = stockData.Where(s => s.ItemVariantId == r.VariantId).Sum(s => s.CurrentStock)
                })
                .OrderBy(dto => dto.Description)
                .ToList();

                return finalResults;
            }

            return new List<ProductSeekDto>();
        }
    }
}