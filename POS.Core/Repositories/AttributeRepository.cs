using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class AttributeRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AttributeRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // --- GROUP MANAGEMENT ---

        public async Task<IEnumerable<AttributeGroup>> GetAllGroupsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AttributeGroups.AsNoTracking().ToListAsync();
        }

        public async Task<AttributeGroup> AddGroupAsync(string groupName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var newGroup = new AttributeGroup { GroupName = groupName.Trim(), IsDeactivated = false };
            await context.AttributeGroups.AddAsync(newGroup);
            await context.SaveChangesAsync();
            return newGroup;
        }

        // --- VALUE MANAGEMENT ---

        public async Task<IEnumerable<AttributeValue>> GetAllValuesFilteredAsync(int? groupId = null, string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Always Include the Group for the UI DataGrid JOIN
            var query = context.AttributeValues
                               .Include(v => v.AttributeGroup)
                               .AsNoTracking();

            if (groupId.HasValue && groupId.Value > 0)
            {
                query = query.Where(v => v.AttributeGroupId == groupId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(v =>
                    v.ValueName.Contains(searchTerm) ||
                    v.AttributeGroup.GroupName.Contains(searchTerm));
            }

            return await query.ToListAsync();
        }

        // SECURITY: Blocks duplicate Values inside the SAME Group (e.g. Cannot have two "Red"s in "Color")
        public async Task<bool> IsValueUniqueAsync(string valueName, int groupId, int currentValueId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.AttributeValues.AnyAsync(v =>
                v.ValueName.ToLower() == valueName.ToLower() &&
                v.AttributeGroupId == groupId &&
                v.Id != currentValueId);
        }

        public async Task AddValueAsync(AttributeValue value)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.AttributeValues.AddAsync(value);
            await context.SaveChangesAsync();
        }

        public async Task UpdateValueAsync(AttributeValue value)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.AttributeValues.Update(value);
            await context.SaveChangesAsync();
        }

        public async Task DeleteValueAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var value = await context.AttributeValues.FindAsync(id);
            if (value != null)
            {
                context.AttributeValues.Remove(value);
                await context.SaveChangesAsync();
            }
        }

        // --- MANY-TO-MANY SYNCING (CATEGORY <-> GROUP) ---

        public async Task<List<int>> GetAssignedCategoryIdsForGroupAsync(int groupId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.CategoryAttributeGroups
                                .Where(c => c.AttributeGroupId == groupId)
                                .Select(c => c.CategoryId)
                                .ToListAsync();
        }

        public async Task SyncGroupToCategoriesAsync(int groupId, List<int> categoryIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // 1. Wipe existing assignments for this group
            var existingAssignments = context.CategoryAttributeGroups.Where(c => c.AttributeGroupId == groupId);
            context.CategoryAttributeGroups.RemoveRange(existingAssignments);

            // 2. Rebuild assignments based on the checked boxes
            foreach (var catId in categoryIds)
            {
                context.CategoryAttributeGroups.Add(new CategoryAttributeGroup
                {
                    AttributeGroupId = groupId,
                    CategoryId = catId
                });
            }

            await context.SaveChangesAsync();
        }
    }
}