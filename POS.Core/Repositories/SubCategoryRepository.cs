using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class SubCategoryRepository
    {
        // CRITICAL UPGRADE: Using the Factory pattern for WPF thread-safety
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SubCategoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // The Unified Search Engine: Handles Parent Filters and Text Search inside SQL
        public async Task<IEnumerable<SubCategory>> GetAllFilteredAsync(int? parentCategoryId = null, string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Always Include the Parent Category for the UI DataGrid JOIN
            var query = context.SubCategories
                               .Include(s => s.Category)
                               .AsNoTracking();

            // 1. Apply Parent Filter if a specific category is selected (and not the "ALL" dropdown option)
            if (parentCategoryId.HasValue && parentCategoryId.Value > 0)
            {
                query = query.Where(s => s.CategoryId == parentCategoryId.Value);
            }

            // 2. Apply Text Search if the user is typing
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(s =>
                    s.SubCategoryCode.Contains(searchTerm) ||
                    s.SubCategoryName.Contains(searchTerm));
            }

            return await query.ToListAsync();
        }

        // Retrieves a single SubCategory by ID
        public async Task<SubCategory?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.SubCategories
                                .Include(s => s.Category)
                                .FirstOrDefaultAsync(s => s.Id == id);
        }

        // SECURITY: Blocks duplicate Sub-Category Codes from crashing the inventory
        public async Task<bool> IsCodeUniqueAsync(string code, int currentSubCategoryId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.SubCategories.AnyAsync(s =>
                s.SubCategoryCode.ToLower() == code.ToLower() &&
                s.Id != currentSubCategoryId);
        }

        public async Task AddAsync(SubCategory subCategory)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.SubCategories.AddAsync(subCategory);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SubCategory subCategory)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.SubCategories.Update(subCategory);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var subCategory = await context.SubCategories.FindAsync(id);
            if (subCategory != null)
            {
                context.SubCategories.Remove(subCategory);
                await context.SaveChangesAsync();
            }
        }
    }
}