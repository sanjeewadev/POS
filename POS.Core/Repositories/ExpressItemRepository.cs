using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// using POS.Core.Data; // Ensure this points to your AppDbContext

namespace POS.Core.Repositories
{
    public class ExpressItemRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ExpressItemRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==========================================
        // 1. GET ALL LAYOUTS (For Admin Grid)
        // ==========================================
        public async Task<List<ExpressItemLayout>> GetAllLayoutsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Includes the Variant and Parent so the grid can display the actual item name
            return await context.ExpressItemLayouts
                .Include(e => e.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .Where(e => e.IsActive)
                .OrderBy(e => e.TabCategory)
                .ThenBy(e => e.GridRow)
                .ThenBy(e => e.GridColumn)
                .ToListAsync();
        }

        // ==========================================
        // 2. SEARCH VARIANTS TO ASSIGN (Admin Side)
        // ==========================================
        public async Task<List<ItemVariant>> SearchVariantsAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Exclude deactivated items
            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .AsNoTracking()
                .Where(v => !v.IsDeactivated && !v.ItemParent.IsDeactivated);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower().Trim();
                query = query.Where(v =>
                    v.SkuCode.ToLower().Contains(term) ||
                    v.ItemParent.ItemName.ToLower().Contains(term) ||
                    v.VariantDescription.ToLower().Contains(term) ||
                    (!string.IsNullOrWhiteSpace(v.Barcode) && v.Barcode.ToLower().Contains(term))
                );
            }

            // Limit to 50 so the search stays lightning fast
            return await query.Take(50).ToListAsync();
        }

        // ==========================================
        // 3. SAVE LAYOUT MAPPING
        // ==========================================
        public async Task<ExpressItemLayout> SaveLayoutAsync(ExpressItemLayout layout)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            if (layout.Id == 0)
            {
                context.ExpressItemLayouts.Add(layout);
            }
            else
            {
                context.ExpressItemLayouts.Update(layout);
            }

            await context.SaveChangesAsync();
            return layout;
        }

        // ==========================================
        // 4. DELETE LAYOUT MAPPING
        // ==========================================
        public async Task DeleteLayoutAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var layout = await context.ExpressItemLayouts.FindAsync(id);
            if (layout != null)
            {
                // Since this is just a UI button mapping (not financial data), 
                // a hard delete from the database is perfectly safe.
                context.ExpressItemLayouts.Remove(layout);
                await context.SaveChangesAsync();
            }
        }
    }
}