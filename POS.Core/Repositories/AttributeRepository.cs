using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Data.Configuration;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public sealed class AttributeLinkedDataSummary
    {
        public int AttributeValueCount { get; set; }

        public int ItemPropertyMappingCount { get; set; }

        public int CategoryAssignmentCount { get; set; }

        public bool HasLinkedData =>
            AttributeValueCount > 0 ||
            ItemPropertyMappingCount > 0;

        public string ToUserMessage(string displayName)
        {
            var reasons = new List<string>();

            if (AttributeValueCount > 0)
                reasons.Add($"Values exist under this group: {AttributeValueCount}.");

            if (ItemPropertyMappingCount > 0)
                reasons.Add($"Item variants already use this property: {ItemPropertyMappingCount} mapping(s).");

            if (!reasons.Any())
                return $"'{displayName}' can be safely deleted.";

            return $"'{displayName}' cannot be deleted. Deactivate it instead.\n\n" +
                   string.Join(Environment.NewLine, reasons);
        }
    }

    public class AttributeRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AttributeRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // =========================================================
        // GROUP MANAGEMENT - GLOBAL PROPERTY GROUPS
        // =========================================================

        public async Task<IEnumerable<AttributeGroup>> GetAllGroupsAsync(
            string searchTerm = "",
            bool includeDeactivated = true)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<AttributeGroup> query = context.AttributeGroups
                .AsNoTracking();

            if (!includeDeactivated)
                query = query.Where(g => !g.IsDeactivated);

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
                .Take(500)
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
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            return !await context.AttributeGroups.AnyAsync(g =>
                EF.Functions.Collate(g.GroupName, caseInsensitiveCollation) == normalizedName &&
                g.Id != currentGroupId);
        }

        public async Task<AttributeGroup> AddGroupAsync(AttributeGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            string normalizedName = NormalizeName(group.GroupName);
            ValidateGroupName(normalizedName);
            ValidateDisplayOrder(group.DisplayOrder, "Group display order");

            await using var context = await _contextFactory.CreateDbContextAsync();
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            bool nameExists = await context.AttributeGroups.AnyAsync(g =>
                EF.Functions.Collate(g.GroupName, caseInsensitiveCollation) == normalizedName);

            if (nameExists)
                throw new InvalidOperationException($"Attribute group '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;

            var entity = new AttributeGroup
            {
                GroupName = normalizedName,
                DisplayOrder = group.DisplayOrder,
                IsDeactivated = group.IsDeactivated,
                CreatedAt = now,
                UpdatedAt = now,
                DeactivatedAt = group.IsDeactivated ? now : null
            };

            await context.AttributeGroups.AddAsync(entity);
            await context.SaveChangesAsync();

            return entity;
        }

        public async Task UpdateGroupAsync(AttributeGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (group.Id <= 0)
                throw new InvalidOperationException("Invalid attribute group selected.");

            string normalizedName = NormalizeName(group.GroupName);
            ValidateGroupName(normalizedName);
            ValidateDisplayOrder(group.DisplayOrder, "Group display order");

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AttributeGroups
                .FirstOrDefaultAsync(g => g.Id == group.Id);

            if (existing == null)
                throw new InvalidOperationException("Attribute group was not found.");

            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);
            bool nameExists = await context.AttributeGroups.AnyAsync(g =>
                EF.Functions.Collate(g.GroupName, caseInsensitiveCollation) == normalizedName &&
                g.Id != group.Id);

            if (nameExists)
                throw new InvalidOperationException($"Attribute group '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;

            existing.GroupName = normalizedName;
            existing.DisplayOrder = group.DisplayOrder;
            existing.IsDeactivated = group.IsDeactivated;
            existing.UpdatedAt = now;
            existing.DeactivatedAt = group.IsDeactivated ? existing.DeactivatedAt ?? now : null;

            await context.SaveChangesAsync();
        }

        public async Task<AttributeLinkedDataSummary> GetGroupLinkedDataSummaryAsync(int groupId)
        {
            var result = new AttributeLinkedDataSummary();

            if (groupId <= 0)
                return result;

            await using var context = await _contextFactory.CreateDbContextAsync();

            result.AttributeValueCount = await context.AttributeValues
                .AsNoTracking()
                .CountAsync(v => v.AttributeGroupId == groupId);

            result.ItemPropertyMappingCount = await context.ItemPropertyMappings
                .AsNoTracking()
                .CountAsync(m => m.AttributeGroupId == groupId);

            result.CategoryAssignmentCount = await context.CategoryAttributeGroups
                .AsNoTracking()
                .CountAsync(c => c.AttributeGroupId == groupId);

            return result;
        }

        public async Task DeleteGroupAsync(int groupId)
        {
            if (groupId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                var group = await context.AttributeGroups
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                    return;

                int valueCount = await context.AttributeValues.CountAsync(v => v.AttributeGroupId == groupId);
                int mappingCount = await context.ItemPropertyMappings.CountAsync(m => m.AttributeGroupId == groupId);
                if (valueCount > 0 || mappingCount > 0)
                {
                    throw new InvalidOperationException(
                        "This group cannot be deleted because it has values or item usage. Deactivate it instead.");
                }

                var staleAssignments = await context.CategoryAttributeGroups
                    .Where(c => c.AttributeGroupId == groupId)
                    .ToListAsync();

                if (staleAssignments.Any())
                    context.CategoryAttributeGroups.RemoveRange(staleAssignments);

                context.AttributeGroups.Remove(group);
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
        // LEGACY CATEGORY ASSIGNMENT COMPATIBILITY
        // Category assignment is no longer used. These methods remain
        // so old ViewModels or pages still compile, but Item Master now
        // receives global groups.
        // =========================================================

        public async Task<List<int>> GetAssignedCategoryIdsForGroupAsync(int groupId)
        {
            await Task.CompletedTask;
            return new List<int>();
        }

        public async Task SyncGroupToCategoriesAsync(int groupId, IEnumerable<int> categoryIds)
        {
            if (groupId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existingAssignments = await context.CategoryAttributeGroups
                .Where(c => c.AttributeGroupId == groupId)
                .ToListAsync();

            if (existingAssignments.Any())
            {
                context.CategoryAttributeGroups.RemoveRange(existingAssignments);
                await context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<AttributeGroup>> GetAttributeGroupsForCategoryAsync(
            int categoryId,
            bool activeOnly = true)
        {
            // Global mode: categoryId is intentionally ignored.
            return await GetAllGroupsAsync(
                searchTerm: string.Empty,
                includeDeactivated: !activeOnly);
        }

        // =========================================================
        // VALUE MANAGEMENT
        // =========================================================

        public async Task<IEnumerable<AttributeValue>> GetAllValuesFilteredAsync(
            int groupId,
            string searchTerm = "",
            bool includeDeactivated = false)
        {
            if (groupId <= 0)
                return new List<AttributeValue>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<AttributeValue> query = context.AttributeValues
                .Include(v => v.AttributeGroup)
                .AsNoTracking()
                .Where(v => v.AttributeGroupId == groupId);

            if (!includeDeactivated)
                query = query.Where(v => !v.IsDeactivated && !v.AttributeGroup.IsDeactivated);

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
                .Take(500)
                .ToListAsync();
        }

        public async Task<IEnumerable<AttributeValue>> GetValuesByGroupIdAsync(
            int groupId,
            bool activeOnly = true)
        {
            return await GetAllValuesFilteredAsync(
                groupId,
                searchTerm: string.Empty,
                includeDeactivated: !activeOnly);
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
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            return !await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == groupId &&
                EF.Functions.Collate(v.ValueName, caseInsensitiveCollation) == normalizedName &&
                v.Id != currentValueId);
        }

        public async Task<AttributeValue> AddValueAsync(AttributeValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.AttributeGroupId <= 0)
                throw new InvalidOperationException("Select a valid attribute group first.");

            string normalizedName = NormalizeName(value.ValueName);
            ValidateValueName(normalizedName);
            ValidateDisplayOrder(value.DisplayOrder, "Value display order");

            await using var context = await _contextFactory.CreateDbContextAsync();

            bool groupExists = await context.AttributeGroups.AnyAsync(g => g.Id == value.AttributeGroupId);
            if (!groupExists)
                throw new InvalidOperationException("Selected attribute group was not found.");

            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);
            bool nameExists = await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == value.AttributeGroupId &&
                EF.Functions.Collate(v.ValueName, caseInsensitiveCollation) == normalizedName);

            if (nameExists)
                throw new InvalidOperationException($"Value '{normalizedName}' already exists in this group.");

            DateTime now = DateTime.Now;

            var entity = new AttributeValue
            {
                AttributeGroupId = value.AttributeGroupId,
                ValueName = normalizedName,
                DisplayOrder = value.DisplayOrder,
                IsDeactivated = value.IsDeactivated,
                CreatedAt = now,
                UpdatedAt = now,
                DeactivatedAt = value.IsDeactivated ? now : null
            };

            await context.AttributeValues.AddAsync(entity);
            await context.SaveChangesAsync();

            return entity;
        }

        public async Task UpdateValueAsync(AttributeValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Id <= 0)
                throw new InvalidOperationException("Invalid attribute value selected.");

            string normalizedName = NormalizeName(value.ValueName);
            ValidateValueName(normalizedName);
            ValidateDisplayOrder(value.DisplayOrder, "Value display order");

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AttributeValues
                .FirstOrDefaultAsync(v => v.Id == value.Id);

            if (existing == null)
                throw new InvalidOperationException("Attribute value was not found.");

            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);
            bool nameExists = await context.AttributeValues.AnyAsync(v =>
                v.AttributeGroupId == existing.AttributeGroupId &&
                EF.Functions.Collate(v.ValueName, caseInsensitiveCollation) == normalizedName &&
                v.Id != value.Id);

            if (nameExists)
                throw new InvalidOperationException($"Value '{normalizedName}' already exists in this group.");

            DateTime now = DateTime.Now;

            existing.ValueName = normalizedName;
            existing.DisplayOrder = value.DisplayOrder;
            existing.IsDeactivated = value.IsDeactivated;
            existing.UpdatedAt = now;
            existing.DeactivatedAt = value.IsDeactivated ? existing.DeactivatedAt ?? now : null;

            await context.SaveChangesAsync();
        }

        public async Task<AttributeLinkedDataSummary> GetValueLinkedDataSummaryAsync(int valueId)
        {
            var result = new AttributeLinkedDataSummary();

            if (valueId <= 0)
                return result;

            await using var context = await _contextFactory.CreateDbContextAsync();

            result.ItemPropertyMappingCount = await context.ItemPropertyMappings
                .AsNoTracking()
                .CountAsync(m => m.AttributeValueId == valueId);

            return result;
        }

        public async Task DeleteValueAsync(int valueId)
        {
            if (valueId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                var value = await context.AttributeValues
                    .FirstOrDefaultAsync(v => v.Id == valueId);

                if (value == null)
                    return;

                int mappingCount = await context.ItemPropertyMappings.CountAsync(m => m.AttributeValueId == valueId);

                if (mappingCount > 0)
                {
                    throw new InvalidOperationException(
                        "This value cannot be deleted because item variants already use it. Deactivate it instead.");
                }

                context.AttributeValues.Remove(value);
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

        private static string NormalizeName(string? value)
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
                throw new InvalidOperationException($"{fieldName} cannot be greater than 9999.");
        }
    }
}
