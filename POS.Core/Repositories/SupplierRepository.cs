using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class SupplierRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SupplierRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // Used by Dropdowns across the system (GRN, PO, Stock Balance)
        public async Task<IEnumerable<Supplier>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Suppliers.AsNoTracking().Where(s => !s.IsDeactivated).ToListAsync();
        }

        // Used by the Supplier Master Grid
        public async Task<IEnumerable<Supplier>> GetAllFilteredAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Suppliers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearch = searchTerm.ToLower();
                query = query.Where(s =>
                    s.SupplierCode.ToLower().Contains(lowerSearch) ||
                    s.CompanyName.ToLower().Contains(lowerSearch) ||
                    s.SupplierName.ToLower().Contains(lowerSearch));
            }

            return await query.ToListAsync();
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentSupplierId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return !await context.Suppliers.AnyAsync(s => s.SupplierCode.ToLower() == code.ToLower() && s.Id != currentSupplierId);
        }

        public async Task AddAsync(Supplier supplier)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.Suppliers.AddAsync(supplier);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Supplier supplier)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Suppliers.Update(supplier);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var supplier = await context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                context.Suppliers.Remove(supplier);
                await context.SaveChangesAsync();
            }
        }
    }
}