using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class CategoryRepository
    {
        // 1. Inject the Factory, NOT the raw DbContext!
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CategoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            // The 'using' keyword safely opens and destroys the SQLite connection
            using var context = _contextFactory.CreateDbContext();

            // AsNoTracking makes reading data incredibly fast
            return await context.Categories.AsNoTracking().ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Categories.FindAsync(id);
        }

        public async Task AddAsync(Category category)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.Categories.AddAsync(category);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Categories.Update(category);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            var category = await context.Categories.FindAsync(id);
            if (category != null)
            {
                context.Categories.Remove(category);
                await context.SaveChangesAsync();
            }
        }
    }
}