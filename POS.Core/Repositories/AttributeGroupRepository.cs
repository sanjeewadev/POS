using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class AttributeGroupRepository
    {
        private readonly AppDbContext _context;

        public AttributeGroupRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AttributeGroup>> GetAllAsync()
        {
            return await _context.AttributeGroups.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<AttributeGroup>> GetGroupsWithCategoriesAsync()
        {
            // Includes the M2M Categories securely without over-fetching values
            return await _context.AttributeGroups
                                 .Include(g => g.Categories)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<AttributeGroup?> GetByIdAsync(int id)
        {
            return await _context.AttributeGroups
                                 .Include(g => g.Categories)
                                 .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task AddAsync(AttributeGroup group)
        {
            // We must track the categories so EF doesn't try to create brand new Categories in the DB
            var newGroup = new AttributeGroup
            {
                GroupName = group.GroupName,
                IsActive = group.IsActive
            };

            foreach (var cat in group.Categories)
            {
                var trackedCat = await _context.Categories.FindAsync(cat.Id);
                if (trackedCat != null) newGroup.Categories.Add(trackedCat);
            }

            await _context.AttributeGroups.AddAsync(newGroup);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(AttributeGroup group)
        {
            // 1. Fetch the existing entity from the database WITH its current categories
            var existingGroup = await _context.AttributeGroups
                                              .Include(g => g.Categories)
                                              .FirstOrDefaultAsync(g => g.Id == group.Id);

            if (existingGroup != null)
            {
                // 2. Update simple properties
                existingGroup.GroupName = group.GroupName;
                existingGroup.IsActive = group.IsActive;

                // 3. Clear old categories and surgically map the new ones
                existingGroup.Categories.Clear();
                foreach (var cat in group.Categories)
                {
                    var trackedCat = await _context.Categories.FindAsync(cat.Id);
                    if (trackedCat != null) existingGroup.Categories.Add(trackedCat);
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task SoftDeleteAsync(int id)
        {
            // Soft delete protects historical product data
            var group = await _context.AttributeGroups.FindAsync(id);
            if (group != null)
            {
                group.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}