using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class AttributeRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AttributeRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // GROUP MANAGEMENT
        // =========================================================

        public async Task<IEnumerable<AttributeGroup>> GetAllGroupsAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.AttributeGroups
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(g =>
                    EF.Functions.Like(g.GroupName, $"%{term}%"));
            }

            return await query
                .OrderBy(g => g.DisplayOrder)
                .ThenBy(g => g.GroupName)
                .Take(500)
                .ToListAsync();
        }

        public async Task<AttributeGroup?> GetGroupByIdAsync(int groupId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.AttributeGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == groupId);
        }

        public async Task<bool> IsGroupUniqueAsync(string groupName, int currentGroupId = 0)
        {
            string normalizedName = NormalizeName(groupName).ToLower();

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.AttributeGroups.AnyAsync(g =>
                g.GroupName.ToLower() == normalizedName &&
                g.Id != currentGroupId);
        }

        public async Task<AttributeGroup> AddGroupAsync(AttributeGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            string normalizedName = NormalizeName(group.GroupName);

            ValidateGroupName(normalizedName);
            ValidateDisplayOrder(group.DisplayOrder, "Group display order");

            using var context = await _contextFactory.CreateDbContextAsync();

            bool nameExists = await context.AttributeGroups.AnyAsync(g =>
                g.GroupName.ToLower() == normalizedName.ToLower());

            if (nameExists)
                throw new InvalidOperationException($"Attribute group '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;

            group.GroupName = normalizedName;
            group.CreatedAt = now;
            group.UpdatedAt = now;
            group.DeactivatedAt = group.IsDeactivated ? now : null;

            await context.AttributeGroups.AddAsync(group);
            await context.SaveChangesAsync();

            return group;
        }

        public async Task UpdateGroupAsync(AttributeGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (group.Id <= 0)
                throw new InvalidOperationException("Invalid attribute group record.");

            string normalizedName = NormalizeName(group.GroupName);

            ValidateGroupName(normalizedName);
            ValidateDisplayOrder(group.DisplayOrder, "Group display order");

            using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == group.Id);

            if (existing == null)
                throw new InvalidOperationException("Attribute group record was not found.");

            bool nameExists = await context.AttributeGroups.AnyAsync(g =>
                g.GroupName.ToLower() == normalizedName.ToLower() &&
                g.Id != group.Id);

            if (nameExists)
                throw new InvalidOperationException($"Attribute group '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;

            existing.GroupName = normalizedName;
            existing.DisplayOrder = group.DisplayOrder;
            existing.IsDeactivated = group.IsDeactivated;
            existing.UpdatedAt = now;

            if (existing.IsDeactivated)
                existing.DeactivatedAt ??= now;
            else
                existing.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeactivateGroupAsync(int groupId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var group = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                return;

            DateTime now = DateTime.Now;

            group.IsDeactivated = true;
            group.UpdatedAt = now;
            group.DeactivatedAt ??= now;

            await context.SaveChangesAsync();
        }

        public async Task DeleteGroupAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var group = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
                return;

            bool hasValues = await context.AttributeValues
                .AnyAsync(v => v.AttributeGroupId == id);

            if (hasValues)
            {
                throw new InvalidOperationException(
                    "This group has attribute values. Delete the values first, or deactivate the group instead.");
            }

            bool hasCategoryAssignments = await context.CategoryAttributeGroups
                .AnyAsync(c => c.AttributeGroupId == id);

            if (hasCategoryAssignments)
            {
                throw new InvalidOperationException(
                    "This group is assigned to categories. Remove category assignments first, or deactivate the group instead.");
            }

            bool hasItemMappings = await context.ItemPropertyMappings
                .AnyAsync(m => m.AttributeGroupId == id);

            if (hasItemMappings)
            {
                throw new InvalidOperationException(
                    "This group is already used by item variants. Deactivate it instead of deleting it.");
            }

            context.AttributeGroups.Remove(group);
            await context.SaveChangesAsync();
        }

        // =========================================================
        // VALUE MANAGEMENT
        // =========================================================

        public async Task<IEnumerable<AttributeValue>> GetAllValuesFilteredAsync(int? groupId = null, string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.AttributeValues
                .Include(v => v.AttributeGroup)
                .AsNoTracking()
                .AsQueryable();

            if (groupId.HasValue && groupId.Value > 0)
            {
                query = query.Where(v => v.AttributeGroupId == groupId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(v =>
                    EF.Functions.Like(v.ValueName, $"%{term}%") ||
                    EF.Functions.Like(v.AttributeGroup.GroupName, $"%{term}%"));
            }

            return await query
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.ValueName)
                .Take(500)
                .ToListAsync();
        }

        public async Task<AttributeValue?> GetValueByIdAsync(int valueId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.AttributeValues
                .Include(v => v.AttributeGroup)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == valueId);
        }

        public async Task<bool> IsValueUniqueAsync(string valueName, int groupId, int currentValueId = 0)
        {
            string normalizedName = NormalizeName(valueName).ToLower();

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.AttributeValues.AnyAsync(v =>
                v.ValueName.ToLower() == normalizedName &&
                v.AttributeGroupId == groupId &&
                v.Id != currentValueId);
        }

        public async Task AddValueAsync(AttributeValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.AttributeGroupId <= 0)
                throw new InvalidOperationException("A valid attribute group is required.");

            string normalizedName = NormalizeName(value.ValueName);

            ValidateValueName(normalizedName);
            ValidateDisplayOrder(value.DisplayOrder, "Value display order");

            using var context = await _contextFactory.CreateDbContextAsync();

            var group = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == value.AttributeGroupId);

            if (group == null)
                throw new InvalidOperationException("Selected attribute group was not found.");

            if (group.IsDeactivated)
                throw new InvalidOperationException("Cannot add values to a deactivated attribute group.");

            bool nameExists = await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == value.AttributeGroupId &&
                v.ValueName.ToLower() == normalizedName.ToLower());

            if (nameExists)
                throw new InvalidOperationException($"Value '{normalizedName}' already exists in this group.");

            DateTime now = DateTime.Now;

            value.ValueName = normalizedName;
            value.CreatedAt = now;
            value.UpdatedAt = now;
            value.DeactivatedAt = value.IsDeactivated ? now : null;

            await context.AttributeValues.AddAsync(value);
            await context.SaveChangesAsync();
        }

        public async Task UpdateValueAsync(AttributeValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Id <= 0)
                throw new InvalidOperationException("Invalid attribute value record.");

            string normalizedName = NormalizeName(value.ValueName);

            ValidateValueName(normalizedName);
            ValidateDisplayOrder(value.DisplayOrder, "Value display order");

            using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AttributeValues
                .FirstOrDefaultAsync(v => v.Id == value.Id);

            if (existing == null)
                throw new InvalidOperationException("Attribute value record was not found.");

            bool nameExists = await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == existing.AttributeGroupId &&
                v.ValueName.ToLower() == normalizedName.ToLower() &&
                v.Id != existing.Id);

            if (nameExists)
                throw new InvalidOperationException($"Value '{normalizedName}' already exists in this group.");

            DateTime now = DateTime.Now;

            // AttributeGroupId is intentionally not updated here.
            existing.ValueName = normalizedName;
            existing.DisplayOrder = value.DisplayOrder;
            existing.IsDeactivated = value.IsDeactivated;
            existing.UpdatedAt = now;

            if (existing.IsDeactivated)
                existing.DeactivatedAt ??= now;
            else
                existing.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeactivateValueAsync(int valueId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var value = await context.AttributeValues
                .FirstOrDefaultAsync(v => v.Id == valueId);

            if (value == null)
                return;

            DateTime now = DateTime.Now;

            value.IsDeactivated = true;
            value.UpdatedAt = now;
            value.DeactivatedAt ??= now;

            await context.SaveChangesAsync();
        }

        public async Task DeleteValueAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var value = await context.AttributeValues
                .FirstOrDefaultAsync(v => v.Id == id);

            if (value == null)
                return;

            bool hasItemMappings = await context.ItemPropertyMappings
                .AnyAsync(m => m.AttributeValueId == id);

            if (hasItemMappings)
            {
                throw new InvalidOperationException(
                    "This value is already used by item variants. Deactivate it instead of deleting it.");
            }

            context.AttributeValues.Remove(value);
            await context.SaveChangesAsync();
        }

        // =========================================================
        // CATEGORY <-> GROUP ASSIGNMENT
        // =========================================================

        public async Task<List<int>> GetAssignedCategoryIdsForGroupAsync(int groupId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.CategoryAttributeGroups
                .AsNoTracking()
                .Where(c => c.AttributeGroupId == groupId)
                .Select(c => c.CategoryId)
                .ToListAsync();
        }

        public async Task SyncGroupToCategoriesAsync(int groupId, List<int> categoryIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var groupExists = await context.AttributeGroups
                    .AnyAsync(g => g.Id == groupId);

                if (!groupExists)
                    throw new InvalidOperationException("Attribute group was not found.");

                var cleanCategoryIds = categoryIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToHashSet();

                var existingCategoryIds = await context.CategoryAttributeGroups
                    .Where(c => c.AttributeGroupId == groupId)
                    .Select(c => c.CategoryId)
                    .ToListAsync();

                var removedCategoryIds = existingCategoryIds
                    .Where(id => !cleanCategoryIds.Contains(id))
                    .ToList();

                if (removedCategoryIds.Any())
                {
                    bool removedCategoryIsUsed = await context.ItemPropertyMappings
                        .AnyAsync(m =>
                            m.AttributeGroupId == groupId &&
                            removedCategoryIds.Contains(m.ItemVariant.ItemParent.CategoryId));

                    if (removedCategoryIsUsed)
                    {
                        throw new InvalidOperationException(
                            "One or more removed category assignments are already used by item variants. Keep the assignment or deactivate the group instead.");
                    }
                }

                var assignmentsToRemove = await context.CategoryAttributeGroups
                    .Where(c =>
                        c.AttributeGroupId == groupId &&
                        removedCategoryIds.Contains(c.CategoryId))
                    .ToListAsync();

                context.CategoryAttributeGroups.RemoveRange(assignmentsToRemove);

                var addedCategoryIds = cleanCategoryIds
                    .Where(id => !existingCategoryIds.Contains(id))
                    .ToList();

                foreach (int categoryId in addedCategoryIds)
                {
                    bool categoryExists = await context.Categories
                        .AnyAsync(c => c.Id == categoryId);

                    if (!categoryExists)
                        continue;

                    context.CategoryAttributeGroups.Add(new CategoryAttributeGroup
                    {
                        AttributeGroupId = groupId,
                        CategoryId = categoryId,
                        AssignedAt = DateTime.Now
                    });
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // VALIDATION HELPERS
        // =========================================================

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static void ValidateGroupName(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new InvalidOperationException("Group name is required.");

            if (groupName.Length > 50)
                throw new InvalidOperationException("Group name cannot be longer than 50 characters.");
        }

        private static void ValidateValueName(string valueName)
        {
            if (string.IsNullOrWhiteSpace(valueName))
                throw new InvalidOperationException("Value name is required.");

            if (valueName.Length > 50)
                throw new InvalidOperationException("Value name cannot be longer than 50 characters.");
        }

        private static void ValidateDisplayOrder(int displayOrder, string fieldName)
        {
            if (displayOrder < 0)
                throw new InvalidOperationException($"{fieldName} cannot be negative.");

            if (displayOrder > 9999)
                throw new InvalidOperationException($"{fieldName} is too large.");
        }

        public async Task<IEnumerable<AttributeGroup>> GetAttributeGroupsForCategoryAsync(
    int categoryId,
    bool activeOnly = true)
        {
            if (categoryId <= 0)
                return new List<AttributeGroup>();

            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CategoryAttributeGroups
                .Include(c => c.AttributeGroup)
                .AsNoTracking()
                .Where(c => c.CategoryId == categoryId);

            if (activeOnly)
            {
                query = query.Where(c => !c.AttributeGroup.IsDeactivated);
            }

            return await query
                .Select(c => c.AttributeGroup)
                .OrderBy(g => g.DisplayOrder)
                .ThenBy(g => g.GroupName)
                .ToListAsync();
        }
    }
}