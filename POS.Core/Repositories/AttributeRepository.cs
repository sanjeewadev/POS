using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public sealed class AttributeGroupLinkedDataSummary
    {
        public int ValueCount { get; init; }
        public int CategoryAssignmentCount { get; init; }
        public int ItemMappingCount { get; init; }

        public bool HasLinkedData =>
            ValueCount > 0 ||
            CategoryAssignmentCount > 0 ||
            ItemMappingCount > 0;

        public string ToUserMessage(string groupName)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Attribute group '{groupName}' cannot be deleted because it is already linked to other records.");
            builder.AppendLine();
            builder.AppendLine("Linked records:");

            if (ValueCount > 0)
                builder.AppendLine($"- Attribute values: {ValueCount}");

            if (CategoryAssignmentCount > 0)
                builder.AppendLine($"- Category assignments: {CategoryAssignmentCount}");

            if (ItemMappingCount > 0)
                builder.AppendLine($"- Item variant mappings: {ItemMappingCount}");

            builder.AppendLine();
            builder.AppendLine("Deactivate this group instead of deleting it.");

            return builder.ToString();
        }
    }

    public sealed class AttributeValueLinkedDataSummary
    {
        public int ItemMappingCount { get; init; }

        public bool HasLinkedData => ItemMappingCount > 0;

        public string ToUserMessage(string valueName)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Attribute value '{valueName}' cannot be deleted because it is already used by item variants.");
            builder.AppendLine();

            if (ItemMappingCount > 0)
                builder.AppendLine($"Linked item variant mappings: {ItemMappingCount}");

            builder.AppendLine();
            builder.AppendLine("Deactivate this value instead of deleting it.");

            return builder.ToString();
        }
    }

    public class AttributeRepository
    {
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;
        private const int MaxDisplayOrder = 9999;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AttributeRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // =========================================================
        // GROUP MANAGEMENT
        // =========================================================

        public async Task<IReadOnlyList<AttributeGroup>> GetAllGroupsAsync(
            string searchTerm = "",
            bool includeDeactivated = true,
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<AttributeGroup> query = context.AttributeGroups
                .AsNoTracking();

            if (!includeDeactivated)
            {
                query = query.Where(g => !g.IsDeactivated);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(g =>
                    EF.Functions.Like(g.GroupName, $"%{term}%"));
            }

            return await query
                .OrderBy(g => g.IsDeactivated)
                .ThenBy(g => g.DisplayOrder)
                .ThenBy(g => g.GroupName)
                .Take(take)
                .ToListAsync();
        }

        public async Task<AttributeGroup?> GetGroupByIdAsync(int groupId)
        {
            if (groupId <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.AttributeGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == groupId);
        }

        public async Task<bool> IsGroupUniqueAsync(string groupName, int currentGroupId = 0)
        {
            string normalizedName = NormalizeName(groupName);

            if (string.IsNullOrWhiteSpace(normalizedName))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.AttributeGroups.AnyAsync(g =>
                EF.Functions.Collate(g.GroupName, "NOCASE") == normalizedName &&
                g.Id != currentGroupId);
        }

        public async Task<AttributeGroupLinkedDataSummary> GetGroupLinkedDataSummaryAsync(int groupId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await GetGroupLinkedDataSummaryAsync(context, groupId);
        }

        public async Task<AttributeGroup> AddGroupAsync(AttributeGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            string normalizedName = NormalizeName(group.GroupName);
            int displayOrder = group.DisplayOrder;

            ValidateGroupName(normalizedName);
            ValidateDisplayOrder(displayOrder, "Group display order");

            await using var context = await _contextFactory.CreateDbContextAsync();

            bool nameExists = await context.AttributeGroups.AnyAsync(g =>
                EF.Functions.Collate(g.GroupName, "NOCASE") == normalizedName);

            if (nameExists)
                throw new InvalidOperationException($"Attribute group '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;

            group.GroupName = normalizedName;
            group.DisplayOrder = displayOrder;
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
            int displayOrder = group.DisplayOrder;

            ValidateGroupName(normalizedName);
            ValidateDisplayOrder(displayOrder, "Group display order");

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == group.Id);

            if (existing == null)
                throw new InvalidOperationException("Attribute group record was not found.");

            bool nameExists = await context.AttributeGroups.AnyAsync(g =>
                EF.Functions.Collate(g.GroupName, "NOCASE") == normalizedName &&
                g.Id != group.Id);

            if (nameExists)
                throw new InvalidOperationException($"Attribute group '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;
            bool wasDeactivated = existing.IsDeactivated;
            bool isNowDeactivated = group.IsDeactivated;

            existing.GroupName = normalizedName;
            existing.DisplayOrder = displayOrder;
            existing.IsDeactivated = isNowDeactivated;
            existing.UpdatedAt = now;

            if (!wasDeactivated && isNowDeactivated)
            {
                existing.DeactivatedAt = now;
            }
            else if (wasDeactivated && !isNowDeactivated)
            {
                existing.DeactivatedAt = null;
            }
            else if (isNowDeactivated)
            {
                existing.DeactivatedAt ??= now;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeactivateGroupAsync(int groupId)
        {
            if (groupId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var group = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                return;

            if (group.IsDeactivated)
                return;

            DateTime now = DateTime.Now;

            group.IsDeactivated = true;
            group.UpdatedAt = now;
            group.DeactivatedAt = now;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateGroupAsync(int groupId)
        {
            if (groupId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var group = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                return;

            if (!group.IsDeactivated)
                return;

            DateTime now = DateTime.Now;

            group.IsDeactivated = false;
            group.UpdatedAt = now;
            group.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeleteGroupAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var group = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
                return;

            var linkedData = await GetGroupLinkedDataSummaryAsync(context, id);

            if (linkedData.HasLinkedData)
            {
                throw new InvalidOperationException(
                    linkedData.ToUserMessage(group.GroupName));
            }

            context.AttributeGroups.Remove(group);
            await context.SaveChangesAsync();
        }

        // =========================================================
        // VALUE MANAGEMENT
        // =========================================================

        public async Task<IReadOnlyList<AttributeValue>> GetAllValuesFilteredAsync(
            int? groupId = null,
            string searchTerm = "",
            bool includeDeactivated = true,
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<AttributeValue> query = context.AttributeValues
                .Include(v => v.AttributeGroup)
                .AsNoTracking();

            if (groupId.HasValue && groupId.Value > 0)
            {
                query = query.Where(v => v.AttributeGroupId == groupId.Value);
            }

            if (!includeDeactivated)
            {
                query = query.Where(v =>
                    !v.IsDeactivated &&
                    !v.AttributeGroup.IsDeactivated);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(v =>
                    EF.Functions.Like(v.ValueName, $"%{term}%") ||
                    EF.Functions.Like(v.AttributeGroup.GroupName, $"%{term}%"));
            }

            return await query
                .OrderBy(v => v.IsDeactivated)
                .ThenBy(v => v.DisplayOrder)
                .ThenBy(v => v.ValueName)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AttributeValue>> GetAttributeValuesForGroupAsync(
            int groupId,
            bool activeOnly = true,
            int take = MaxTakeLimit)
        {
            if (groupId <= 0)
                return Array.Empty<AttributeValue>();

            return await GetAllValuesFilteredAsync(
                groupId: groupId,
                searchTerm: string.Empty,
                includeDeactivated: !activeOnly,
                take: take);
        }

        public async Task<AttributeValue?> GetValueByIdAsync(int valueId)
        {
            if (valueId <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.AttributeValues
                .Include(v => v.AttributeGroup)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == valueId);
        }

        public async Task<bool> IsValueUniqueAsync(
            string valueName,
            int groupId,
            int currentValueId = 0)
        {
            string normalizedName = NormalizeName(valueName);

            if (groupId <= 0 || string.IsNullOrWhiteSpace(normalizedName))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == groupId &&
                EF.Functions.Collate(v.ValueName, "NOCASE") == normalizedName &&
                v.Id != currentValueId);
        }

        public async Task<AttributeValueLinkedDataSummary> GetValueLinkedDataSummaryAsync(int valueId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await GetValueLinkedDataSummaryAsync(context, valueId);
        }

        public async Task AddValueAsync(AttributeValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.AttributeGroupId <= 0)
                throw new InvalidOperationException("A valid attribute group is required.");

            string normalizedName = NormalizeName(value.ValueName);
            int displayOrder = value.DisplayOrder;

            ValidateValueName(normalizedName);
            ValidateDisplayOrder(displayOrder, "Value display order");

            await using var context = await _contextFactory.CreateDbContextAsync();

            var group = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == value.AttributeGroupId);

            if (group == null)
                throw new InvalidOperationException("Selected attribute group was not found.");

            if (group.IsDeactivated)
                throw new InvalidOperationException("Cannot add values to a deactivated attribute group.");

            bool nameExists = await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == value.AttributeGroupId &&
                EF.Functions.Collate(v.ValueName, "NOCASE") == normalizedName);

            if (nameExists)
                throw new InvalidOperationException($"Value '{normalizedName}' already exists in this group.");

            DateTime now = DateTime.Now;

            value.ValueName = normalizedName;
            value.DisplayOrder = displayOrder;
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
            int displayOrder = value.DisplayOrder;

            ValidateValueName(normalizedName);
            ValidateDisplayOrder(displayOrder, "Value display order");

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AttributeValues
                .Include(v => v.AttributeGroup)
                .FirstOrDefaultAsync(v => v.Id == value.Id);

            if (existing == null)
                throw new InvalidOperationException("Attribute value record was not found.");

            bool nameExists = await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == existing.AttributeGroupId &&
                EF.Functions.Collate(v.ValueName, "NOCASE") == normalizedName &&
                v.Id != existing.Id);

            if (nameExists)
                throw new InvalidOperationException($"Value '{normalizedName}' already exists in this group.");

            bool wasDeactivated = existing.IsDeactivated;
            bool isNowDeactivated = value.IsDeactivated;

            if (wasDeactivated && !isNowDeactivated && existing.AttributeGroup.IsDeactivated)
            {
                throw new InvalidOperationException(
                    "Cannot reactivate this value because its attribute group is deactivated.");
            }

            DateTime now = DateTime.Now;

            // AttributeGroupId is intentionally not updated.
            existing.ValueName = normalizedName;
            existing.DisplayOrder = displayOrder;
            existing.IsDeactivated = isNowDeactivated;
            existing.UpdatedAt = now;

            if (!wasDeactivated && isNowDeactivated)
            {
                existing.DeactivatedAt = now;
            }
            else if (wasDeactivated && !isNowDeactivated)
            {
                existing.DeactivatedAt = null;
            }
            else if (isNowDeactivated)
            {
                existing.DeactivatedAt ??= now;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeactivateValueAsync(int valueId)
        {
            if (valueId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var value = await context.AttributeValues
                .FirstOrDefaultAsync(v => v.Id == valueId);

            if (value == null)
                return;

            if (value.IsDeactivated)
                return;

            DateTime now = DateTime.Now;

            value.IsDeactivated = true;
            value.UpdatedAt = now;
            value.DeactivatedAt = now;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateValueAsync(int valueId)
        {
            if (valueId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var value = await context.AttributeValues
                .Include(v => v.AttributeGroup)
                .FirstOrDefaultAsync(v => v.Id == valueId);

            if (value == null)
                return;

            if (!value.IsDeactivated)
                return;

            if (value.AttributeGroup.IsDeactivated)
            {
                throw new InvalidOperationException(
                    "Cannot reactivate this value because its attribute group is deactivated.");
            }

            DateTime now = DateTime.Now;

            value.IsDeactivated = false;
            value.UpdatedAt = now;
            value.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeleteValueAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var value = await context.AttributeValues
                .FirstOrDefaultAsync(v => v.Id == id);

            if (value == null)
                return;

            var linkedData = await GetValueLinkedDataSummaryAsync(context, id);

            if (linkedData.HasLinkedData)
            {
                throw new InvalidOperationException(
                    linkedData.ToUserMessage(value.ValueName));
            }

            context.AttributeValues.Remove(value);
            await context.SaveChangesAsync();
        }

        // =========================================================
        // CATEGORY <-> GROUP ASSIGNMENT
        // =========================================================

        public async Task<IReadOnlyList<int>> GetAssignedCategoryIdsForGroupAsync(int groupId)
        {
            if (groupId <= 0)
                return Array.Empty<int>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.CategoryAttributeGroups
                .AsNoTracking()
                .Where(c => c.AttributeGroupId == groupId)
                .Select(c => c.CategoryId)
                .ToListAsync();
        }

        public async Task SyncGroupToCategoriesAsync(int groupId, IReadOnlyCollection<int> categoryIds)
        {
            if (groupId <= 0)
                throw new InvalidOperationException("Invalid attribute group record.");

            categoryIds ??= Array.Empty<int>();

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

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

        public async Task<IReadOnlyList<AttributeGroup>> GetAttributeGroupsForCategoryAsync(
            int categoryId,
            bool activeOnly = true)
        {
            if (categoryId <= 0)
                return Array.Empty<AttributeGroup>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<CategoryAttributeGroup> query = context.CategoryAttributeGroups
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

        // =========================================================
        // PRIVATE HELPERS
        // =========================================================

        private static async Task<AttributeGroupLinkedDataSummary> GetGroupLinkedDataSummaryAsync(
            AppDbContext context,
            int groupId)
        {
            if (groupId <= 0)
                return new AttributeGroupLinkedDataSummary();

            int valueCount = await context.AttributeValues
                .AsNoTracking()
                .CountAsync(v => v.AttributeGroupId == groupId);

            int categoryAssignmentCount = await context.CategoryAttributeGroups
                .AsNoTracking()
                .CountAsync(c => c.AttributeGroupId == groupId);

            int itemMappingCount = await context.ItemPropertyMappings
                .AsNoTracking()
                .CountAsync(m => m.AttributeGroupId == groupId);

            return new AttributeGroupLinkedDataSummary
            {
                ValueCount = valueCount,
                CategoryAssignmentCount = categoryAssignmentCount,
                ItemMappingCount = itemMappingCount
            };
        }

        private static async Task<AttributeValueLinkedDataSummary> GetValueLinkedDataSummaryAsync(
            AppDbContext context,
            int valueId)
        {
            if (valueId <= 0)
                return new AttributeValueLinkedDataSummary();

            int itemMappingCount = await context.ItemPropertyMappings
                .AsNoTracking()
                .CountAsync(m => m.AttributeValueId == valueId);

            return new AttributeValueLinkedDataSummary
            {
                ItemMappingCount = itemMappingCount
            };
        }

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
        }

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

            if (displayOrder > MaxDisplayOrder)
                throw new InvalidOperationException($"{fieldName} cannot be greater than {MaxDisplayOrder}.");
        }
    }
}