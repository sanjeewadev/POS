using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class BarcodeManagementRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public BarcodeManagementRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // Explicitly using the DTO namespace to avoid clashes with old files
        public async Task<List<POS.Core.Models.DTOs.BarcodeManagementDto>> GetBarcodeManagementListAsync(string searchText, int? categoryId, bool showActiveOnly)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                    .ThenInclude(p => p.Category)
                .AsQueryable();

            if (showActiveOnly)
            {
                query = query.Where(v => !v.ItemParent.IsDeactivated);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.ItemParent.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var search = searchText.ToLower();
                query = query.Where(v =>
                    v.ItemParent.ItemCode.ToLower().Contains(search) ||
                    v.ItemParent.ItemName.ToLower().Contains(search) ||
                    v.Barcode.ToLower().Contains(search));
            }

            var results = await query
                .OrderBy(v => v.ItemParent.ItemName)
                .Select(v => new POS.Core.Models.DTOs.BarcodeManagementDto
                {
                    VariantId = v.Id,
                    ItemCode = v.ItemParent.ItemCode,
                    ItemName = v.ItemParent.ItemName,
                    VariantDescription = v.VariantDescription,
                    CategoryName = v.ItemParent.Category != null ? v.ItemParent.Category.CategoryName : "Uncategorized",
                    Barcode = v.Barcode ?? string.Empty,
                    IsSelected = false
                })
                .AsNoTracking()
                .ToListAsync();

            return results;
        }

        public async Task UpdateSingleBarcodeAsync(int variantId, string newBarcode)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var variant = await context.ItemVariants.FindAsync(variantId);

            if (variant == null)
                throw new Exception("Item variant not found.");

            // Check for uniqueness before saving
            if (!string.IsNullOrWhiteSpace(newBarcode))
            {
                bool exists = await context.ItemVariants.AnyAsync(v => v.Barcode == newBarcode && v.Id != variantId);
                if (exists)
                    throw new Exception($"The barcode '{newBarcode}' is already assigned to another item.");
            }

            variant.Barcode = newBarcode?.Trim();
            // Removed variant.UpdatedAt to match your database schema

            context.ItemVariants.Update(variant);
            await context.SaveChangesAsync();
        }

        public async Task AutoGenerateBarcodesAsync(List<int> variantIds)
        {
            if (variantIds == null || !variantIds.Any()) return;

            using var context = await _contextFactory.CreateDbContextAsync();

            var variantsToUpdate = await context.ItemVariants
                .Where(v => variantIds.Contains(v.Id))
                .ToListAsync();

            foreach (var variant in variantsToUpdate)
            {
                if (string.IsNullOrWhiteSpace(variant.Barcode))
                {
                    variant.Barcode = $"SYS-{variant.Id.ToString().PadLeft(6, '0')}";
                    // Removed variant.UpdatedAt to match your database schema
                }
            }

            context.ItemVariants.UpdateRange(variantsToUpdate);
            await context.SaveChangesAsync();
        }
    }
}