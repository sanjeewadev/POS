using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class AttributeValueRepository
    {
        private readonly AppDbContext _context;

        public AttributeValueRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AttributeValue>> GetAllAsync()
        {
            return await _context.AttributeValues
                                 .Include(v => v.AttributeGroup)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<AttributeValue>> GetValuesByGroupIdAsync(int groupId)
        {
            return await _context.AttributeValues
                                 .Include(v => v.AttributeGroup)
                                 .Where(v => v.AttributeGroupId == groupId)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<AttributeValue?> GetByIdAsync(int id)
        {
            return await _context.AttributeValues.FindAsync(id);
        }

        public async Task AddAsync(AttributeValue attributeValue)
        {
            await _context.AttributeValues.AddAsync(attributeValue);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(AttributeValue attributeValue)
        {
            _context.AttributeValues.Update(attributeValue);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id)
        {
            var value = await _context.AttributeValues.FindAsync(id);
            if (value != null)
            {
                value.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}