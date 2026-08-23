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
    public sealed class CategoryLinkedDataSummary
    {
        public int SubCategoryCount { get; init; }
        public int ItemCount { get; init; }
        public int AttributeAssignmentCount { get; init; }
        public int DiscountRuleCount { get; init; }
        public int FreeIssueRuleCount { get; init; }

        public bool HasLinkedData =>
            SubCategoryCount > 0 ||
            ItemCount > 0 ||
            AttributeAssignmentCount > 0 ||
            DiscountRuleCount > 0 ||
            FreeIssueRuleCount > 0;

        public string ToUserMessage(string categoryName)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Category '{categoryName}' cannot be deleted because it is already linked to other records.");
            builder.AppendLine();
            builder.AppendLine("Linked records:");

            if (SubCategoryCount > 0)
                builder.AppendLine($"- Sub-categories: {SubCategoryCount}");

            if (ItemCount > 0)
                builder.AppendLine($"- Item master records: {ItemCount}");

            if (AttributeAssignmentCount > 0)
                builder.AppendLine($"- Attribute group assignments: {AttributeAssignmentCount}");

            if (DiscountRuleCount > 0)
                builder.AppendLine($"- Discount rules: {DiscountRuleCount}");

            if (FreeIssueRuleCount > 0)
                builder.AppendLine($"- Free issue rules: {FreeIssueRuleCount}");

            builder.AppendLine();
            builder.AppendLine("Deactivate this category instead of deleting it.");

            return builder.ToString();
        }
    }

    public class CategoryRepository
    {
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;
        private const int MaxDisplayOrder = 999999;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly AuthService? _authService;

        private static readonly Regex CategoryCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public CategoryRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            AuthService? authService = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _authService = authService;
        }

        public async Task<IReadOnlyList<Category>> GetAllAsync(
            string searchTerm = "",
            bool includeDeactivated = true,
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<Category> query = context.Categories
                .AsNoTracking();

            if (!includeDeactivated)
            {
                query = query.Where(c => !c.IsDeactivated);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(c =>
                    EF.Functions.Like(c.CategoryCode, $"%{term}%") ||
                    EF.Functions.Like(c.CategoryName, $"%{term}%") ||
                    EF.Functions.Like(c.Description, $"%{term}%"));
            }

            return await query
                .OrderBy(c => c.IsDeactivated)
                .ThenBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .ThenBy(c => c.CategoryCode)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Category>> GetActiveAsync(
            string searchTerm = "",
            int take = DefaultTakeLimit)
        {
            return await GetAllAsync(
                searchTerm: searchTerm,
                includeDeactivated: false,
                take: take);
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentCategoryId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            if (string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Categories.AnyAsync(c =>
                c.CategoryCode == normalizedCode &&
                c.Id != currentCategoryId);
        }

        public async Task<bool> IsNameUniqueAsync(string name, int currentCategoryId = 0)
        {
            string normalizedName = NormalizeName(name);

            if (string.IsNullOrWhiteSpace(normalizedName))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Categories.AnyAsync(c =>
                c.CategoryName == normalizedName &&
                c.Id != currentCategoryId);
        }

        public async Task<CategoryLinkedDataSummary> GetLinkedDataSummaryAsync(int categoryId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await GetLinkedDataSummaryAsync(context, categoryId);
        }

        public async Task<bool> HasLinkedDataAsync(int categoryId)
        {
            var summary = await GetLinkedDataSummaryAsync(categoryId);
            return summary.HasLinkedData;
        }

        public async Task AddAsync(Category category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            string normalizedCode = NormalizeCode(category.CategoryCode);
            string normalizedName = NormalizeName(category.CategoryName);
            string normalizedDescription = NormalizeDescription(category.Description);
            int displayOrder = NormalizeDisplayOrder(category.DisplayOrder);

            await using var context = await _contextFactory.CreateDbContextAsync();

            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                normalizedCode = await GenerateCategoryCodeAsync(context);
                category.CategoryCode = normalizedCode;
            }

            ValidateCategoryCode(normalizedCode);
            ValidateCategoryName(normalizedName);
            ValidateDescription(normalizedDescription);
            ValidateDisplayOrder(displayOrder);

            bool codeExists = await context.Categories.AnyAsync(c =>
                c.CategoryCode == normalizedCode);

            if (codeExists)
                throw new InvalidOperationException($"Category code '{normalizedCode}' already exists.");

            bool nameExists = await context.Categories.AnyAsync(c =>
                c.CategoryName == normalizedName);

            if (nameExists)
                throw new InvalidOperationException($"Category name '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            category.CategoryCode = normalizedCode;
            category.CategoryName = normalizedName;
            category.Description = normalizedDescription;
            category.DisplayOrder = displayOrder;
            category.CreatedAt = now;
            category.CreatedBy = currentUser;
            category.UpdatedAt = now;
            category.UpdatedBy = currentUser;

            if (category.IsDeactivated)
            {
                category.DeactivatedAt = now;
                category.DeactivatedBy = currentUser;
            }
            else
            {
                category.DeactivatedAt = null;
                category.DeactivatedBy = string.Empty;
            }

            await context.Categories.AddAsync(category);
            await context.SaveChangesAsync();
        }

        public async Task AddBulkAsync(IEnumerable<Category> categories)
        {
            if (categories == null || !categories.Any()) return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            foreach (var category in categories)
            {
                string normalizedCode = NormalizeCode(category.CategoryCode);
                string normalizedName = NormalizeName(category.CategoryName);
                string normalizedDescription = NormalizeDescription(category.Description);
                int displayOrder = NormalizeDisplayOrder(category.DisplayOrder);

                if (string.IsNullOrWhiteSpace(normalizedCode))
                {
                    normalizedCode = await GenerateCategoryCodeAsync(context);
                }

                ValidateCategoryCode(normalizedCode);
                ValidateCategoryName(normalizedName);
                ValidateDescription(normalizedDescription);
                ValidateDisplayOrder(displayOrder);

                category.CategoryCode = normalizedCode;
                category.CategoryName = normalizedName;
                category.Description = normalizedDescription;
                category.DisplayOrder = displayOrder;
                category.CreatedAt = now;
                category.CreatedBy = currentUser;
                category.UpdatedAt = now;
                category.UpdatedBy = currentUser;

                if (category.IsDeactivated)
                {
                    category.DeactivatedAt = now;
                    category.DeactivatedBy = currentUser;
                }
                else
                {
                    category.DeactivatedAt = null;
                    category.DeactivatedBy = string.Empty;
                }

                await context.Categories.AddAsync(category);
            }

            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            if (category.Id <= 0)
                throw new InvalidOperationException("Invalid category record.");

            string normalizedName = NormalizeName(category.CategoryName);
            string normalizedDescription = NormalizeDescription(category.Description);
            int displayOrder = NormalizeDisplayOrder(category.DisplayOrder);

            ValidateCategoryName(normalizedName);
            ValidateDescription(normalizedDescription);
            ValidateDisplayOrder(displayOrder);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == category.Id);

            if (existing == null)
                throw new InvalidOperationException("Category record not found.");

            bool nameExists = await context.Categories.AnyAsync(c =>
                c.CategoryName == normalizedName &&
                c.Id != category.Id);

            if (nameExists)
                throw new InvalidOperationException($"Category name '{normalizedName}' already exists.");

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            bool wasDeactivated = existing.IsDeactivated;
            bool isNowDeactivated = category.IsDeactivated;

            // CategoryCode is intentionally not updated.
            // Once created, it must remain stable for item links, reports, imports, and future sync.
            existing.CategoryName = normalizedName;
            existing.Description = normalizedDescription;
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

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return;

            if (category.IsDeactivated)
                return;

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            category.IsDeactivated = true;
            category.UpdatedAt = now;
            category.UpdatedBy = currentUser;
            category.DeactivatedAt = now;
            category.DeactivatedBy = currentUser;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return;

            if (!category.IsDeactivated)
                return;

            DateTime now = DateTime.Now;
            string currentUser = GetCurrentUsername();

            category.IsDeactivated = false;
            category.UpdatedAt = now;
            category.UpdatedBy = currentUser;
            category.DeactivatedAt = null;
            category.DeactivatedBy = string.Empty;

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return;

            var linkedData = await GetLinkedDataSummaryAsync(context, id);

            if (linkedData.HasLinkedData)
            {
                throw new InvalidOperationException(
                    linkedData.ToUserMessage(category.CategoryName));
            }

            context.Categories.Remove(category);
            await context.SaveChangesAsync();
        }

        private static async Task<CategoryLinkedDataSummary> GetLinkedDataSummaryAsync(
            AppDbContext context,
            int categoryId)
        {
            if (categoryId <= 0)
                return new CategoryLinkedDataSummary();

            int subCategoryCount = await context.SubCategories
                .AsNoTracking()
                .CountAsync(s => s.CategoryId == categoryId);

            int itemCount = await context.ItemParents
                .AsNoTracking()
                .CountAsync(i => i.CategoryId == categoryId);

            int attributeAssignmentCount = await context.CategoryAttributeGroups
                .AsNoTracking()
                .CountAsync(a => a.CategoryId == categoryId);

            int discountRuleCount = await context.DiscountRules
                .AsNoTracking()
                .CountAsync(r => r.CategoryId == categoryId);

            int freeIssueRuleCount = await context.FreeIssueRules
                .AsNoTracking()
                .CountAsync(r => r.CategoryId == categoryId);

            return new CategoryLinkedDataSummary
            {
                SubCategoryCount = subCategoryCount,
                ItemCount = itemCount,
                AttributeAssignmentCount = attributeAssignmentCount,
                DiscountRuleCount = discountRuleCount,
                FreeIssueRuleCount = freeIssueRuleCount
            };
        }

        private string GetCurrentUsername()
        {
            string? username = _authService?.CurrentUser?.Username;

            if (!string.IsNullOrWhiteSpace(username))
                return username.Trim();

            return Environment.UserName ?? "System";
        }

        private async Task<string> GenerateCategoryCodeAsync(AppDbContext context)
        {
            var sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(d => d.DocumentType == "CAT");

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = "CAT",
                    Prefix = "CAT-",
                    NextSequenceNumber = 1,
                    PaddingLength = 3,
                    UpdatedAt = DateTime.Now
                };

                context.DocumentSequences.Add(sequence);
            }

            int attempts = 0;

            while (attempts < 1000)
            {
                int nextNumber = sequence.NextSequenceNumber;
                int padding = sequence.PaddingLength <= 0 ? 3 : sequence.PaddingLength;

                string code = $"{sequence.Prefix}{nextNumber.ToString($"D{padding}")}";

                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;

                bool exists = await context.Categories
                    .AnyAsync(c => c.CategoryCode == code);

                if (!exists)
                    return code;

                attempts++;
            }

            throw new InvalidOperationException("Unable to generate a unique category code.");
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

        private static string NormalizeDescription(string description)
        {
            return (description ?? string.Empty).Trim();
        }

        private static int NormalizeDisplayOrder(int displayOrder)
        {
            return displayOrder < 0 ? 0 : displayOrder;
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

        private static void ValidateDescription(string description)
        {
            if (description.Length > 250)
                throw new InvalidOperationException("Description cannot be longer than 250 characters.");
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