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

        // ==========================================
        // GROUP MANAGEMENT (Upgraded for Master-Detail)
        // ==========================================

        public async Task<IEnumerable<AttributeGroup>> GetAllGroupsAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.AttributeGroups.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(g => g.GroupName.Contains(searchTerm));
            }

            return await query.ToListAsync();
        }

        public async Task<bool> IsGroupUniqueAsync(string groupName, int currentGroupId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return !await context.AttributeGroups.AnyAsync(g =>
                g.GroupName.ToLower() == groupName.ToLower() &&
                g.Id != currentGroupId);
        }

        public async Task<AttributeGroup> AddGroupAsync(AttributeGroup group)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.AttributeGroups.AddAsync(group);
            await context.SaveChangesAsync();
            return group;
        }

        public async Task UpdateGroupAsync(AttributeGroup group)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.AttributeGroups.Update(group);
            await context.SaveChangesAsync();
        }

        public async Task DeleteGroupAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var group = await context.AttributeGroups.FindAsync(id);
            if (group != null)
            {
                context.AttributeGroups.Remove(group);
                await context.SaveChangesAsync();
            }
        }

        // ==========================================
        // VALUE MANAGEMENT
        // ==========================================

        public async Task<IEnumerable<AttributeValue>> GetAllValuesFilteredAsync(int? groupId = null, string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

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

        // ==========================================
        // MANY-TO-MANY SYNCING (CATEGORY <-> GROUP)
        // ==========================================

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

            var existingAssignments = context.CategoryAttributeGroups.Where(c => c.AttributeGroupId == groupId);
            context.CategoryAttributeGroups.RemoveRange(existingAssignments);

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