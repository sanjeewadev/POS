using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class CategoryRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CategoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // Search logic now happens inside SQL Server/SQLite, not in system RAM.
        public async Task<IEnumerable<Category>> GetAllAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Categories.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Converts to a highly efficient SQL LIKE query
                query = query.Where(c =>
                    c.CategoryCode.Contains(searchTerm) ||
                    c.CategoryName.Contains(searchTerm));
            }

            return await query.ToListAsync();
        }

        // CRITICAL SECURITY: Checks if a code exists before saving
        public async Task<bool> IsCodeUniqueAsync(string code, int currentCategoryId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Categories.AnyAsync(c =>
                c.CategoryCode.ToLower() == code.ToLower() &&
                c.Id != currentCategoryId);
        }

        public async Task AddAsync(Category category)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.Categories.AddAsync(category);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Categories.Update(category);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var category = await context.Categories.FindAsync(id);
            if (category != null)
            {
                context.Categories.Remove(category);
                await context.SaveChangesAsync();
            }
        }
    }
}