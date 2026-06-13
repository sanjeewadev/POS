using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class UnitOfMeasureRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public UnitOfMeasureRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<UnitOfMeasure>> GetAllAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.UnitsOfMeasure.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(u => u.UomCode.Contains(searchTerm) || u.UomDescription.Contains(searchTerm));
            }

            return await query.ToListAsync();
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentUomId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return !await context.UnitsOfMeasure.AnyAsync(u => u.UomCode.ToLower() == code.ToLower() && u.Id != currentUomId);
        }

        public async Task AddAsync(UnitOfMeasure uom)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.UnitsOfMeasure.AddAsync(uom);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(UnitOfMeasure uom)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.UnitsOfMeasure.Update(uom);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var uom = await context.UnitsOfMeasure.FindAsync(id);
            if (uom != null)
            {
                context.UnitsOfMeasure.Remove(uom);
                await context.SaveChangesAsync();
            }
        }
    }
}