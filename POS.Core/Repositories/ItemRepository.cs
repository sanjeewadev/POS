using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class ItemRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ItemRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Item>> GetAllAsync()
        {
            using var context = _contextFactory.CreateDbContext();

            // We use .Include() so the UI can display the actual Category Name 
            // instead of just a meaningless CategoryId number.
            return await context.Items
                                .Include(i => i.Category)
                                .Include(i => i.Supplier)
                                .AsNoTracking()
                                .ToListAsync();
        }

        public async Task<Item?> GetByIdAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();

            return await context.Items
                                .Include(i => i.Category)
                                .Include(i => i.Supplier)
                                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task AddAsync(Item item)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.Items.AddAsync(item);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Item item)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Items.Update(item);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            var item = await context.Items.FindAsync(id);
            if (item != null)
            {
                context.Items.Remove(item);
                await context.SaveChangesAsync();
            }
        }
    }
}