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
        private readonly AppDbContext _context;

        public SubCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SubCategory>> GetAllAsync()
        {
            // .Include() performs a SQL JOIN to fetch the Parent Category name
            return await _context.SubCategories
                                 .Include(s => s.Category)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<SubCategory?> GetByIdAsync(int id)
        {
            return await _context.SubCategories
                                 .Include(s => s.Category)
                                 .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<IEnumerable<SubCategory>> GetByCategoryIdAsync(int categoryId)
        {
            // Fast filtering at the database level, not the UI level
            return await _context.SubCategories
                                 .Include(s => s.Category)
                                 .Where(s => s.CategoryId == categoryId)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task AddAsync(SubCategory subCategory)
        {
            await _context.SubCategories.AddAsync(subCategory);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SubCategory subCategory)
        {
            _context.SubCategories.Update(subCategory);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var subCategory = await _context.SubCategories.FindAsync(id);
            if (subCategory != null)
            {
                _context.SubCategories.Remove(subCategory);
                await _context.SaveChangesAsync();
            }
        }
    }
}