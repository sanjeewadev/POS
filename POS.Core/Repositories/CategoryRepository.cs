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
    public class CategoryRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private static readonly Regex CategoryCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        public CategoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Category>> GetAllAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Categories
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(c =>
                    EF.Functions.Like(c.CategoryCode, $"%{term}%") ||
                    EF.Functions.Like(c.CategoryName, $"%{term}%"));
            }

            return await query
                .OrderBy(c => c.CategoryName)
                .ThenBy(c => c.CategoryCode)
                .Take(500)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentCategoryId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Categories.AnyAsync(c =>
                c.CategoryCode.ToUpper() == normalizedCode &&
                c.Id != currentCategoryId);
        }

        public async Task<bool> IsNameUniqueAsync(string name, int currentCategoryId = 0)
        {
            string normalizedName = NormalizeName(name).ToLower();

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Categories.AnyAsync(c =>
                c.CategoryName.ToLower() == normalizedName &&
                c.Id != currentCategoryId);
        }

        public async Task<bool> HasLinkedDataAsync(int categoryId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await HasLinkedDataAsync(context, categoryId);
        }

        public async Task AddAsync(Category category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            string normalizedCode = NormalizeCode(category.CategoryCode);
            string normalizedName = NormalizeName(category.CategoryName);

            ValidateCategoryCode(normalizedCode);
            ValidateCategoryName(normalizedName);

            using var context = await _contextFactory.CreateDbContextAsync();

            bool codeExists = await context.Categories.AnyAsync(c =>
                c.CategoryCode.ToUpper() == normalizedCode);

            if (codeExists)
                throw new InvalidOperationException($"Category code '{normalizedCode}' already exists.");

            bool nameExists = await context.Categories.AnyAsync(c =>
                c.CategoryName.ToLower() == normalizedName.ToLower());

            if (nameExists)
                throw new InvalidOperationException($"Category name '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;

            category.CategoryCode = normalizedCode;
            category.CategoryName = normalizedName;
            category.CreatedAt = now;
            category.UpdatedAt = now;
            category.DeactivatedAt = category.IsDeactivated ? now : null;

            await context.Categories.AddAsync(category);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            if (category.Id <= 0)
                throw new InvalidOperationException("Invalid category record.");

            string normalizedName = NormalizeName(category.CategoryName);

            ValidateCategoryName(normalizedName);

            using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == category.Id);

            if (existing == null)
                throw new InvalidOperationException("Category record not found.");

            bool nameExists = await context.Categories.AnyAsync(c =>
                c.CategoryName.ToLower() == normalizedName.ToLower() &&
                c.Id != category.Id);

            if (nameExists)
                throw new InvalidOperationException($"Category name '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;

            // Important:
            // CategoryCode is intentionally NOT updated here.
            // Once created, the code should stay stable for reports, imports, and sync.
            existing.CategoryName = normalizedName;
            existing.IsDeactivated = category.IsDeactivated;
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

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return;

            DateTime now = DateTime.Now;

            category.IsDeactivated = true;
            category.UpdatedAt = now;
            category.DeactivatedAt ??= now;

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return;

            bool hasLinkedData = await HasLinkedDataAsync(context, id);

            if (hasLinkedData)
            {
                throw new InvalidOperationException(
                    "This category is linked to other records. Deactivate it instead of deleting it.");
            }

            context.Categories.Remove(category);
            await context.SaveChangesAsync();
        }

        private static async Task<bool> HasLinkedDataAsync(AppDbContext context, int categoryId)
        {
            bool hasSubCategories = await context.SubCategories
                .AnyAsync(s => s.CategoryId == categoryId);

            if (hasSubCategories)
                return true;

            bool hasAttributeAssignments = await context.CategoryAttributeGroups
                .AnyAsync(a => a.CategoryId == categoryId);

            if (hasAttributeAssignments)
                return true;

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

        private static void ValidateCategoryCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Category code is required.");

            if (code.Length > 20)
                throw new InvalidOperationException("Category code cannot be longer than 20 characters.");

            if (!CategoryCodeRegex.IsMatch(code))
            {
                throw new InvalidOperationException(
                    "Category code can only contain letters, numbers, dash, and underscore.");
            }
        }

        private static void ValidateCategoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Category name is required.");

            if (name.Length > 100)
                throw new InvalidOperationException("Category name cannot be longer than 100 characters.");
        }
    }
}