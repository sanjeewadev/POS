using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Data.Configuration;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public sealed class UnitOfMeasureLinkedDataSummary
    {
        public int ItemParentCount { get; init; }

        public bool HasLinkedData => ItemParentCount > 0;

        public string ToUserMessage(string uomCode)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Unit of Measure '{uomCode}' cannot be deleted because it is already linked to item records.");
            builder.AppendLine();

            if (ItemParentCount > 0)
                builder.AppendLine($"Linked item records: {ItemParentCount}");

            builder.AppendLine();
            builder.AppendLine("Make this UOM inactive instead of deleting it.");

            return builder.ToString();
        }
    }

    public class UnitOfMeasureRepository
    {
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;
        private const int MaxDisplayOrder = 9999;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private static readonly Regex UomCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public UnitOfMeasureRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<IReadOnlyList<UnitOfMeasure>> GetAllAsync(
            string searchTerm = "",
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<UnitOfMeasure> query = context.UnitsOfMeasure
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(u =>
                    EF.Functions.Like(u.UomCode, $"%{term}%") ||
                    EF.Functions.Like(u.UomDescription, $"%{term}%"));
            }

            return await query
                .OrderBy(u => u.IsActive ? 0 : 1)
                .ThenBy(u => u.DisplayOrder)
                .ThenBy(u => u.UomCode)
                .Take(take)
                .ToListAsync();
        }

        // Item Master should use this for the UOM dropdown.
        // Inactive UOMs must not appear when creating new items.
        public async Task<IReadOnlyList<UnitOfMeasure>> GetActiveAsync(int take = MaxTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.UnitsOfMeasure
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayOrder)
                .ThenBy(u => u.UomCode)
                .Take(take)
                .ToListAsync();
        }

        public async Task<UnitOfMeasure?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentUomId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            if (string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            return !await context.UnitsOfMeasure.AnyAsync(u =>
                EF.Functions.Collate(u.UomCode, caseInsensitiveCollation) == normalizedCode &&
                u.Id != currentUomId);
        }

        public async Task<bool> IsDescriptionUniqueAsync(string description, int currentUomId = 0)
        {
            string normalizedDescription = NormalizeDescription(description);

            if (string.IsNullOrWhiteSpace(normalizedDescription))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            return !await context.UnitsOfMeasure.AnyAsync(u =>
                EF.Functions.Collate(u.UomDescription, caseInsensitiveCollation) == normalizedDescription &&
                u.Id != currentUomId);
        }

        public async Task<UnitOfMeasureLinkedDataSummary> GetLinkedDataSummaryAsync(int uomId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await GetLinkedDataSummaryAsync(context, uomId);
        }

        public async Task<bool> HasLinkedItemsAsync(int uomId)
        {
            var summary = await GetLinkedDataSummaryAsync(uomId);
            return summary.HasLinkedData;
        }

        public async Task AddAsync(UnitOfMeasure uom)
        {
            if (uom == null)
                throw new ArgumentNullException(nameof(uom));

            string normalizedCode = NormalizeCode(uom.UomCode);
            string normalizedDescription = NormalizeDescription(uom.UomDescription);
            int displayOrder = uom.DisplayOrder;

            ValidateUomCode(normalizedCode);
            ValidateUomDescription(normalizedDescription);
            ValidateDisplayOrder(displayOrder);

            await using var context = await _contextFactory.CreateDbContextAsync();
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            bool codeExists = await context.UnitsOfMeasure.AnyAsync(u =>
                EF.Functions.Collate(u.UomCode, caseInsensitiveCollation) == normalizedCode);

            if (codeExists)
                throw new InvalidOperationException($"UOM code '{normalizedCode}' already exists.");

            bool descriptionExists = await context.UnitsOfMeasure.AnyAsync(u =>
                EF.Functions.Collate(u.UomDescription, caseInsensitiveCollation) == normalizedDescription);

            if (descriptionExists)
                throw new InvalidOperationException($"UOM description '{normalizedDescription}' already exists.");

            DateTime now = DateTime.Now;

            uom.UomCode = normalizedCode;
            uom.UomDescription = normalizedDescription;
            uom.DisplayOrder = displayOrder;
            uom.CreatedAt = now;
            uom.UpdatedAt = now;
            uom.DeactivatedAt = uom.IsActive ? null : now;

            await context.UnitsOfMeasure.AddAsync(uom);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(UnitOfMeasure uom)
        {
            if (uom == null)
                throw new ArgumentNullException(nameof(uom));

            if (uom.Id <= 0)
                throw new InvalidOperationException("Invalid UOM record.");

            string normalizedDescription = NormalizeDescription(uom.UomDescription);
            int displayOrder = uom.DisplayOrder;

            ValidateUomDescription(normalizedDescription);
            ValidateDisplayOrder(displayOrder);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Id == uom.Id);

            if (existing == null)
                throw new InvalidOperationException("UOM record was not found.");

            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);
            bool descriptionExists = await context.UnitsOfMeasure.AnyAsync(u =>
                EF.Functions.Collate(u.UomDescription, caseInsensitiveCollation) == normalizedDescription &&
                u.Id != existing.Id);

            if (descriptionExists)
                throw new InvalidOperationException($"UOM description '{normalizedDescription}' already exists.");

            bool hasLinkedItems = await context.ItemParents
                .AnyAsync(i => i.UnitOfMeasureId == existing.Id);

            if (hasLinkedItems && existing.AllowDecimals != uom.AllowDecimals)
            {
                throw new InvalidOperationException(
                    "Allow Decimal Quantities cannot be changed because this UOM is already assigned to item records.");
            }

            DateTime now = DateTime.Now;
            bool wasActive = existing.IsActive;
            bool isNowActive = uom.IsActive;

            // UOM code is intentionally not updated after creation.
            existing.UomDescription = normalizedDescription;
            existing.AllowDecimals = uom.AllowDecimals;
            existing.DisplayOrder = displayOrder;
            existing.IsActive = isNowActive;
            existing.UpdatedAt = now;

            if (wasActive && !isNowActive)
            {
                existing.DeactivatedAt = now;
            }
            else if (!wasActive && isNowActive)
            {
                existing.DeactivatedAt = null;
            }
            else if (!isNowActive)
            {
                existing.DeactivatedAt ??= now;
            }

            await context.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var uom = await context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Id == id);

            if (uom == null)
                return;

            if (!uom.IsActive)
                return;

            DateTime now = DateTime.Now;

            uom.IsActive = false;
            uom.UpdatedAt = now;
            uom.DeactivatedAt = now;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var uom = await context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Id == id);

            if (uom == null)
                return;

            if (uom.IsActive)
                return;

            DateTime now = DateTime.Now;

            uom.IsActive = true;
            uom.UpdatedAt = now;
            uom.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var uom = await context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Id == id);

            if (uom == null)
                return;

            var linkedData = await GetLinkedDataSummaryAsync(context, id);

            if (linkedData.HasLinkedData)
            {
                throw new InvalidOperationException(
                    linkedData.ToUserMessage(uom.UomCode));
            }

            context.UnitsOfMeasure.Remove(uom);
            await context.SaveChangesAsync();
        }

        private static async Task<UnitOfMeasureLinkedDataSummary> GetLinkedDataSummaryAsync(
            AppDbContext context,
            int uomId)
        {
            if (uomId <= 0)
                return new UnitOfMeasureLinkedDataSummary();

            int itemParentCount = await context.ItemParents
                .AsNoTracking()
                .CountAsync(i => i.UnitOfMeasureId == uomId);

            return new UnitOfMeasureLinkedDataSummary
            {
                ItemParentCount = itemParentCount
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

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeDescription(string description)
        {
            return (description ?? string.Empty).Trim();
        }

        private static void ValidateUomCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("UOM code is required.");

            if (code.Length > 10)
                throw new InvalidOperationException("UOM code cannot be longer than 10 characters.");

            if (!UomCodeRegex.IsMatch(code))
            {
                throw new InvalidOperationException(
                    "UOM code can only contain letters, numbers, dash, and underscore.");
            }
        }

        private static void ValidateUomDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new InvalidOperationException("UOM description is required.");

            if (description.Length > 100)
                throw new InvalidOperationException("UOM description cannot be longer than 100 characters.");
        }

        private static void ValidateDisplayOrder(int displayOrder)
        {
            if (displayOrder < 0)
                throw new InvalidOperationException("Display order cannot be negative.");

            if (displayOrder > MaxDisplayOrder)
                throw new InvalidOperationException($"Display order cannot be greater than {MaxDisplayOrder}.");
        }
    }
}