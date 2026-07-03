using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Services;

namespace POS.Core.Repositories
{
    public sealed class SubCategoryLinkedDataSummary
    {
        public int ItemParentCount { get; init; }
        public int ItemVariantReferenceCount { get; init; }
        public int DiscountRuleCount { get; init; }
        public int FreeIssueRuleCount { get; init; }

        public bool HasLinkedData =>
            ItemParentCount > 0 ||
            ItemVariantReferenceCount > 0 ||
            DiscountRuleCount > 0 ||
            FreeIssueRuleCount > 0;

        public string ToUserMessage(string subCategoryName)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Sub-category '{subCategoryName}' cannot be deleted because it is already linked to other records.");
            builder.AppendLine();
            builder.AppendLine("Linked records:");

            if (ItemParentCount > 0)
                builder.AppendLine($"- Item master records: {ItemParentCount}");

            if (ItemVariantReferenceCount > 0)
                builder.AppendLine($"- Item variant references: {ItemVariantReferenceCount}");

            if (DiscountRuleCount > 0)
                builder.AppendLine($"- Discount rules: {DiscountRuleCount}");

            if (FreeIssueRuleCount > 0)
                builder.AppendLine($"- Free issue rules: {FreeIssueRuleCount}");

            builder.AppendLine();
            builder.AppendLine("Deactivate this sub-category instead of deleting it.");

            return builder.ToString();
        }
    }

    public class SubCategoryRepository
    {
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;
        private const int MaxSubCategoryCodeLength = 40;
        private const int MaxDisplayOrder = 999999;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly AuthService? _authService;

        private static readonly Regex SubCategoryCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public SubCategoryRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            AuthService? authService = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _authService = authService;
        }

        public async Task<IReadOnlyList<SubCategory>> GetAllFilteredAsync(
            int? parentCategoryId = null,
            string searchTerm = "",
            bool includeDeactivated = true,
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<SubCategory> query = context.SubCategories
                .Include(s => s.Category)
                .AsNoTracking();

            if (!includeDeactivated)
            {
                query = query.Where(s => !s.IsDeactivated && !s.Category.IsDeactivated);
            }

            if (parentCategoryId.HasValue && parentCategoryId.Value > 0)
            {
                query = query.Where(s => s.CategoryId == parentCategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(s =>
                    EF.Functions.Like(s.SubCategoryCode, $"%{term}%") ||
                    EF.Functions.Like(s.SubCategoryName, $"%{term}%") ||
                    EF.Functions.Like(s.Category.CategoryCode, $"%{term}%") ||
                    EF.Functions.Like(s.Category.CategoryName, $"%{term}%"));
            }

            return await query
                .OrderBy(s => s.Category.CategoryName)
                .ThenBy(s => s.IsDeactivated)
                .ThenBy(s => s.DisplayOrder)
                .ThenBy(s => s.SubCategoryName)
                .ThenBy(s => s.SubCategoryCode)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<SubCategory>> GetAllAsync(
            bool includeDeactivated = true,
            int take = DefaultTakeLimit)
        {
            return await GetAllFilteredAsync(
                parentCategoryId: null,
                searchTerm: string.Empty,
                includeDeactivated: includeDeactivated,
                take: take);
        }

        public async Task<IReadOnlyList<SubCategory>> GetActiveByCategoryAsync(
            int categoryId,
            string searchTerm = "",
            int take = DefaultTakeLimit)
        {
            if (categoryId <= 0)
                return Array.Empty<SubCategory>();

            return await GetAllFilteredAsync(
                parentCategoryId: categoryId,
                searchTerm: searchTerm,
                includeDeactivated: false,
                take: take);
        }

        public async Task<SubCategory?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.SubCategories
                .Include(s => s.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentSubCategoryId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            if (string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.SubCategories.AnyAsync(s =>
                s.SubCategoryCode == normalizedCode &&
                s.Id != currentSubCategoryId);
        }

        public async Task<bool> IsCodeUniqueAsync(
            int categoryId,
            string code,
            int currentSubCategoryId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            if (categoryId <= 0 || string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.SubCategories.AnyAsync(s =>
                s.CategoryId == categoryId &&
                s.SubCategoryCode == normalizedCode &&
                s.Id != currentSubCategoryId);
        }

        public async Task<bool> IsNameUniqueAsync(
            int categoryId,
            string name,
            int currentSubCategoryId = 0)
        {
            string normalizedName = NormalizeName(name);

            if (categoryId <= 0 || string.IsNullOrWhiteSpace(normalizedName))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.SubCategories.AnyAsync(s =>
                s.CategoryId == categoryId &&
                s.SubCategoryName == normalizedName &&
                s.Id != currentSubCategoryId);
        }

        public async Task<SubCategoryLinkedDataSummary> GetLinkedDataSummaryAsync(int subCategoryId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await GetLinkedDataSummaryAsync(context, subCategoryId);
        }

        public async Task<bool> HasLinkedDataAsync(int subCategoryId)
        {
            var summary = await GetLinkedDataSummaryAsync(subCategoryId);
            return summary.HasLinkedData;
        }

        public async Task AddAsync(SubCategory subCategory)
        {
            if (subCategory == null)
                throw new ArgumentNullException(nameof(subCategory));

            if (subCategory.CategoryId <= 0)
                throw new InvalidOperationException("A valid parent category is required.");

            string normalizedCode = NormalizeCode(subCategory.SubCategoryCode);
            string normalizedName = NormalizeName(subCategory.SubCategoryName);
            int displayOrder = NormalizeDisplayOrder(subCategory.DisplayOrder);

            ValidateSubCategoryCode(normalizedCode);
            ValidateSubCategoryName(normalizedName);
            ValidateDisplayOrder(displayOrder);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var parentCategory = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == subCategory.CategoryId);

            if (parentCategory == null)
                throw new InvalidOperationException("Selected parent category was not found.");

            if (parentCategory.IsDeactivated)
                throw new InvalidOperationException("Cannot create a sub-category under a deactivated parent category.");

            bool codeExists = await context.SubCategories.AnyAsync(s =>
                s.CategoryId == subCategory.CategoryId &&
                s.SubCategoryCode == normalizedCode);

            if (codeExists)
                throw new InvalidOperationException($"Sub-category code '{normalizedCode}' already exists under this parent category.");

            bool nameExists = await context.SubCategories.AnyAsync(s =>
                s.CategoryId == subCategory.CategoryId &&
                s.SubCategoryName == normalizedName);

            if (nameExists)
                throw new InvalidOperationException($"Sub-category name '{normalizedName}' already exists under this parent category.");

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            subCategory.SubCategoryCode = normalizedCode;
            subCategory.SubCategoryName = normalizedName;
            subCategory.DisplayOrder = displayOrder;
            subCategory.CreatedAt = now;
            subCategory.CreatedBy = currentUser;
            subCategory.UpdatedAt = now;
            subCategory.UpdatedBy = currentUser;

            if (subCategory.IsDeactivated)
            {
                subCategory.DeactivatedAt = now;
                subCategory.DeactivatedBy = currentUser;
            }
            else
            {
                subCategory.DeactivatedAt = null;
                subCategory.DeactivatedBy = string.Empty;
            }

            await context.SubCategories.AddAsync(subCategory);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SubCategory subCategory)
        {
            if (subCategory == null)
                throw new ArgumentNullException(nameof(subCategory));

            if (subCategory.Id <= 0)
                throw new InvalidOperationException("Invalid sub-category record.");

            string normalizedName = NormalizeName(subCategory.SubCategoryName);
            int displayOrder = NormalizeDisplayOrder(subCategory.DisplayOrder);

            ValidateSubCategoryName(normalizedName);
            ValidateDisplayOrder(displayOrder);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.SubCategories
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.Id == subCategory.Id);

            if (existing == null)
                throw new InvalidOperationException("Sub-category record was not found.");

            bool nameExists = await context.SubCategories.AnyAsync(s =>
                s.CategoryId == existing.CategoryId &&
                s.SubCategoryName == normalizedName &&
                s.Id != existing.Id);

            if (nameExists)
                throw new InvalidOperationException($"Sub-category name '{normalizedName}' already exists under this parent category.");

            bool wasDeactivated = existing.IsDeactivated;
            bool isNowDeactivated = subCategory.IsDeactivated;

            if (wasDeactivated && !isNowDeactivated && existing.Category.IsDeactivated)
            {
                throw new InvalidOperationException(
                    "Cannot reactivate this sub-category because its parent category is deactivated.");
            }

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            // Parent category and SubCategoryCode are intentionally not updated.
            // They must remain stable for item links, reports, rules, and future sync.
            existing.SubCategoryName = normalizedName;
            existing.DisplayOrder = displayOrder;
            existing.IsDeactivated = isNowDeactivated;
            existing.UpdatedAt = now;
            existing.UpdatedBy = currentUser;

            if (!wasDeactivated && isNowDeactivated)
            {
                existing.DeactivatedAt = now;
                existing.DeactivatedBy = currentUser;
            }
            else if (wasDeactivated && !isNowDeactivated)
            {
                existing.DeactivatedAt = null;
                existing.DeactivatedBy = string.Empty;
            }
            else if (isNowDeactivated)
            {
                existing.DeactivatedAt ??= now;

                if (string.IsNullOrWhiteSpace(existing.DeactivatedBy))
                    existing.DeactivatedBy = currentUser;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var subCategory = await context.SubCategories
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subCategory == null)
                return;

            if (subCategory.IsDeactivated)
                return;

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            subCategory.IsDeactivated = true;
            subCategory.UpdatedAt = now;
            subCategory.UpdatedBy = currentUser;
            subCategory.DeactivatedAt = now;
            subCategory.DeactivatedBy = currentUser;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var subCategory = await context.SubCategories
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subCategory == null)
                return;

            if (!subCategory.IsDeactivated)
                return;

            if (subCategory.Category.IsDeactivated)
            {
                throw new InvalidOperationException(
                    "Cannot reactivate this sub-category because its parent category is deactivated.");
            }

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            subCategory.IsDeactivated = false;
            subCategory.UpdatedAt = now;
            subCategory.UpdatedBy = currentUser;
            subCategory.DeactivatedAt = null;
            subCategory.DeactivatedBy = string.Empty;

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var subCategory = await context.SubCategories
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subCategory == null)
                return;

            var linkedData = await GetLinkedDataSummaryAsync(context, id);

            if (linkedData.HasLinkedData)
            {
                throw new InvalidOperationException(
                    linkedData.ToUserMessage(subCategory.SubCategoryName));
            }

            context.SubCategories.Remove(subCategory);
            await context.SaveChangesAsync();
        }

        private static async Task<SubCategoryLinkedDataSummary> GetLinkedDataSummaryAsync(
            AppDbContext context,
            int subCategoryId)
        {
            if (subCategoryId <= 0)
                return new SubCategoryLinkedDataSummary();

            int itemParentCount = await context.ItemParents
                .AsNoTracking()
                .CountAsync(i => i.SubCategoryId == subCategoryId);

            int itemVariantReferenceCount = await CountEntitySubCategoryReferencesAsync(
                context,
                context.ItemVariants,
                subCategoryId);

            int discountRuleCount = await context.DiscountRules
                .AsNoTracking()
                .CountAsync(r => r.SubCategoryId == subCategoryId);

            int freeIssueRuleCount = await context.FreeIssueRules
                .AsNoTracking()
                .CountAsync(r => r.SubCategoryId == subCategoryId);

            return new SubCategoryLinkedDataSummary
            {
                ItemParentCount = itemParentCount,
                ItemVariantReferenceCount = itemVariantReferenceCount,
                DiscountRuleCount = discountRuleCount,
                FreeIssueRuleCount = freeIssueRuleCount
            };
        }

        private static async Task<int> CountEntitySubCategoryReferencesAsync<TEntity>(
            AppDbContext context,
            IQueryable<TEntity> query,
            int subCategoryId) where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var property = entityType?.FindProperty("SubCategoryId");

            if (property == null)
                return 0;

            if (property.ClrType == typeof(int))
            {
                return await query.CountAsync(e =>
                    EF.Property<int>(e, "SubCategoryId") == subCategoryId);
            }

            if (property.ClrType == typeof(int?))
            {
                return await query.CountAsync(e =>
                    EF.Property<int?>(e, "SubCategoryId") == subCategoryId);
            }

            return 0;
        }

        private string GetCurrentUsername()
        {
            string? username = _authService?.CurrentUser?.Username;

            if (!string.IsNullOrWhiteSpace(username))
                return username.Trim();

            return Environment.UserName ?? "System";
        }

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
        }

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeName(string name)
        {
            return (name ?? string.Empty).Trim();
        }

        private static int NormalizeDisplayOrder(int displayOrder)
        {
            return displayOrder < 0 ? 0 : displayOrder;
        }

        private static void ValidateSubCategoryCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Sub-category code is required.");

            if (code.Length > MaxSubCategoryCodeLength)
                throw new InvalidOperationException($"Sub-category code cannot be longer than {MaxSubCategoryCodeLength} characters.");

            if (!SubCategoryCodeRegex.IsMatch(code))
            {
                throw new InvalidOperationException(
                    "Sub-category code can only contain letters, numbers, dash, and underscore.");
            }
        }

        private static void ValidateSubCategoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Sub-category name is required.");

            if (name.Length > 100)
                throw new InvalidOperationException("Sub-category name cannot be longer than 100 characters.");
        }

        private static void ValidateDisplayOrder(int displayOrder)
        {
            if (displayOrder < 0)
                throw new InvalidOperationException("Display order cannot be less than zero.");

            if (displayOrder > MaxDisplayOrder)
                throw new InvalidOperationException($"Display order cannot be greater than {MaxDisplayOrder}.");
        }
    }
}