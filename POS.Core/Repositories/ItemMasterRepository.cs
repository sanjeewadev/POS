using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;
using System;

namespace POS.Core.Repositories
{
    // DTO specifically for the DataGrid in Zone 6
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

    public class ItemMasterRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ItemMasterRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // --- MASTER GRID DATA (Zone 6) ---
        public async Task<IEnumerable<ItemMasterSummaryDto>> GetSummariesAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemParents
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p =>
                    p.ItemCode.Contains(searchTerm) ||
                    p.ItemName.Contains(searchTerm) ||
                    p.Category.CategoryName.Contains(searchTerm));
            }

            // Project directly into the DTO to save massive amounts of RAM
            return await query.Select(p => new ItemMasterSummaryDto
            {
                ParentId = p.Id,
                ItemCode = p.ItemCode,
                ItemName = p.ItemName,
                CategoryName = p.Category.CategoryName,
                VariantCount = p.Variants.Count,
                TotalStockOnHand = p.Variants.Sum(v => v.TotalStockOnHand), // Will be mapped to physical stock ledger later
                StatusText = p.IsDeactivated ? "Deactivated" : "Active"
            }).ToListAsync();
        }

        // --- VALIDATION ---
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
                // 1. Save or Update Parent
                if (parent.Id == 0)
                {
                    await context.ItemParents.AddAsync(parent);
                    await context.SaveChangesAsync(); // Saves to get the new Parent ID
                }
                else
                {
                    context.ItemParents.Update(parent);
                    await context.SaveChangesAsync();
                }

                // 2. Wipe existing untransacted Variants and Mappings to cleanly rebuild the matrix
                // In a true Phase 2 production rollout, you would check the Inventory Ledger here 
                // to prevent wiping variants that already have physical stock.
                var existingVariants = await context.ItemVariants.Where(v => v.ItemParentId == parent.Id).ToListAsync();
                context.ItemVariants.RemoveRange(existingVariants);
                await context.SaveChangesAsync();

                // 3. Insert New Variants
                foreach (var variant in variants)
                {
                    variant.ItemParentId = parent.Id;
                    await context.ItemVariants.AddAsync(variant);
                }
                await context.SaveChangesAsync();

                // 4. Map the properties (Color: Red, Size: XL) to the newly created Variants
                foreach (var variant in variants)
                {
                    // Find the mappings that belong to this specific SKU
                    var variantMappings = mappings.Where(m => m.ItemVariant.SkuCode == variant.SkuCode).ToList();
                    foreach (var mapping in variantMappings)
                    {
                        mapping.ItemVariantId = variant.Id;
                        mapping.ItemVariant = null!; // Clear navigation property before saving to prevent EF confusion
                        await context.ItemPropertyMappings.AddAsync(mapping);
                    }
                }
                await context.SaveChangesAsync();

                // Commit the atomic transaction
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; // Throw back to ViewModel to show UI Error
            }
        }

        public async Task DeleteMatrixAsync(int parentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var parent = await context.ItemParents.FindAsync(parentId);
            if (parent != null)
            {
                context.ItemParents.Remove(parent);
                await context.SaveChangesAsync();
            }
        }

        // ==========================================
        // --- CASHIER POS ENGINE ---
        // ==========================================
        public async Task<ItemVariant?> GetItemByBarcodeAsync(string barcode)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // The scanner searches the Variants table because the Cashier sells specific SKUs/Barcodes
            return await context.ItemVariants
                .Include(v => v.ItemParent) // Bring in the Parent to get the actual Product Name
                .AsNoTracking()             // Lightning fast read-only query
                .FirstOrDefaultAsync(v => v.SkuCode.ToLower() == barcode.ToLower());
        }
    }
}