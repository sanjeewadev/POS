using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class SubCategoryRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private static readonly Regex SubCategoryCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        public SubCategoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<SubCategory>> GetAllFilteredAsync(int? parentCategoryId = null, string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.SubCategories
                .Include(s => s.Category)
                .AsNoTracking()
                .AsQueryable();

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
                .ThenBy(s => s.SubCategoryName)
                .ThenBy(s => s.SubCategoryCode)
                .Take(500)
                .ToListAsync();
        }

        public async Task<IEnumerable<SubCategory>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.SubCategories
                .Include(s => s.Category)
                .AsNoTracking()
                .OrderBy(s => s.Category.CategoryName)
                .ThenBy(s => s.SubCategoryName)
                .ToListAsync();
        }

        public async Task<SubCategory?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.SubCategories
                .Include(s => s.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        // Kept for backward compatibility with your current ViewModel.
        // This checks globally by final SubCategoryCode.
        public async Task<bool> IsCodeUniqueAsync(string code, int currentSubCategoryId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.SubCategories.AnyAsync(s =>
                s.SubCategoryCode.ToUpper() == normalizedCode &&
                s.Id != currentSubCategoryId);
        }

        // Better version for the updated ViewModel.
        // This checks uniqueness inside the selected parent category.
        public async Task<bool> IsCodeUniqueAsync(int categoryId, string code, int currentSubCategoryId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.SubCategories.AnyAsync(s =>
                s.CategoryId == categoryId &&
                s.SubCategoryCode.ToUpper() == normalizedCode &&
                s.Id != currentSubCategoryId);
        }

        public async Task<bool> IsNameUniqueAsync(int categoryId, string name, int currentSubCategoryId = 0)
        {
            string normalizedName = NormalizeName(name).ToLower();

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.SubCategories.AnyAsync(s =>
                s.CategoryId == categoryId &&
                s.SubCategoryName.ToLower() == normalizedName &&
                s.Id != currentSubCategoryId);
        }

        public async Task AddAsync(SubCategory subCategory)
        {
            if (subCategory == null)
                throw new ArgumentNullException(nameof(subCategory));

            if (subCategory.CategoryId <= 0)
                throw new InvalidOperationException("A valid parent category is required.");

            string normalizedCode = NormalizeCode(subCategory.SubCategoryCode);
            string normalizedName = NormalizeName(subCategory.SubCategoryName);

            ValidateSubCategoryCode(normalizedCode);
            ValidateSubCategoryName(normalizedName);

            using var context = await _contextFactory.CreateDbContextAsync();

            var parentCategory = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == subCategory.CategoryId);

            if (parentCategory == null)
                throw new InvalidOperationException("Selected parent category was not found.");

            if (parentCategory.IsDeactivated)
                throw new InvalidOperationException("Cannot create a sub-category under a deactivated parent category.");

            bool codeExists = await context.SubCategories.AnyAsync(s =>
                s.CategoryId == subCategory.CategoryId &&
                s.SubCategoryCode.ToUpper() == normalizedCode);

            if (codeExists)
                throw new InvalidOperationException($"Sub-category code '{normalizedCode}' already exists under this parent category.");

            bool nameExists = await context.SubCategories.AnyAsync(s =>
                s.CategoryId == subCategory.CategoryId &&
                s.SubCategoryName.ToLower() == normalizedName.ToLower());

            if (nameExists)
                throw new InvalidOperationException($"Sub-category name '{normalizedName}' already exists under this parent category.");

            DateTime now = DateTime.Now;

            subCategory.SubCategoryCode = normalizedCode;
            subCategory.SubCategoryName = normalizedName;
            subCategory.CreatedAt = now;
            subCategory.UpdatedAt = now;
            subCategory.DeactivatedAt = subCategory.IsDeactivated ? now : null;

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

            ValidateSubCategoryName(normalizedName);

            using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.SubCategories
                .FirstOrDefaultAsync(s => s.Id == subCategory.Id);

            if (existing == null)
                throw new InvalidOperationException("Sub-category record was not found.");

            bool nameExists = await context.SubCategories.AnyAsync(s =>
                s.CategoryId == existing.CategoryId &&
                s.SubCategoryName.ToLower() == normalizedName.ToLower() &&
                s.Id != existing.Id);

            if (nameExists)
                throw new InvalidOperationException($"Sub-category name '{normalizedName}' already exists under this parent category.");

            DateTime now = DateTime.Now;

            // Important:
            // Parent category and sub-category code are intentionally not updated here.
            // They should stay stable after creation.
            existing.SubCategoryName = normalizedName;
            existing.IsDeactivated = subCategory.IsDeactivated;
            existing.UpdatedAt = now;

            if (existing.IsDeactivated)
            {
                existing.DeactivatedAt ??= now;
            }
            else
            {
                existing.DeactivatedAt = null;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var subCategory = await context.SubCategories
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subCategory == null)
                return;

            DateTime now = DateTime.Now;

            subCategory.IsDeactivated = true;
            subCategory.UpdatedAt = now;
            subCategory.DeactivatedAt ??= now;

            await context.SaveChangesAsync();
        }

        public async Task<bool> HasLinkedDataAsync(int subCategoryId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await HasLinkedDataAsync(context, subCategoryId);
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var subCategory = await context.SubCategories
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subCategory == null)
                return;

            bool hasLinkedData = await HasLinkedDataAsync(context, id);

            if (hasLinkedData)
            {
                throw new InvalidOperationException(
                    "This sub-category is linked to item records. Deactivate it instead of deleting it.");
            }

            try
            {
                context.SubCategories.Remove(subCategory);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException(
                    "This sub-category cannot be deleted because it is linked to other records. Deactivate it instead.");
            }
        }

        private static async Task<bool> HasLinkedDataAsync(AppDbContext context, int subCategoryId)
        {
            // Safe check for future ItemParent.SubCategoryId.
            // If the property does not exist yet, this check simply returns false.
            if (await HasLinkedEntityAsync(context, context.ItemParents, subCategoryId))
                return true;

            // Safe check for future ItemVariant.SubCategoryId.
            // If the property does not exist yet, this check simply returns false.
            if (await HasLinkedEntityAsync(context, context.ItemVariants, subCategoryId))
                return true;

            return false;
        }

        private static async Task<bool> HasLinkedEntityAsync<TEntity>(
            AppDbContext context,
            IQueryable<TEntity> query,
            int subCategoryId) where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var property = entityType?.FindProperty("SubCategoryId");

            if (property == null)
                return false;

            if (property.ClrType == typeof(int))
            {
                return await query.AnyAsync(e =>
                    EF.Property<int>(e, "SubCategoryId") == subCategoryId);
            }

            if (property.ClrType == typeof(int?))
            {
                return await query.AnyAsync(e =>
                    EF.Property<int?>(e, "SubCategoryId") == subCategoryId);
            }

            return false;
        }

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeName(string name)
        {
            return (name ?? string.Empty).Trim();
        }

        private static void ValidateSubCategoryCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Sub-category code is required.");

            if (code.Length > 20)
                throw new InvalidOperationException("Sub-category code cannot be longer than 20 characters.");

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
    }
}