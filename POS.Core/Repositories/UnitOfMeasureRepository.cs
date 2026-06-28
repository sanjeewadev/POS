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
    public class UnitOfMeasureRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private static readonly Regex UomCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        public UnitOfMeasureRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<UnitOfMeasure>> GetAllAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.UnitsOfMeasure
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(u =>
                    EF.Functions.Like(u.UomCode, $"%{term}%") ||
                    EF.Functions.Like(u.UomDescription, $"%{term}%"));
            }

            return await query
                .OrderBy(u => u.DisplayOrder)
                .ThenBy(u => u.UomCode)
                .Take(500)
                .ToListAsync();
        }

        // Later Item Master should use this for the UOM dropdown.
        public async Task<IEnumerable<UnitOfMeasure>> GetActiveAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.UnitsOfMeasure
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayOrder)
                .ThenBy(u => u.UomCode)
                .ToListAsync();
        }

        public async Task<UnitOfMeasure?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentUomId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.UnitsOfMeasure.AnyAsync(u =>
                u.UomCode.ToUpper() == normalizedCode &&
                u.Id != currentUomId);
        }

        public async Task<bool> IsDescriptionUniqueAsync(string description, int currentUomId = 0)
        {
            string normalizedDescription = NormalizeDescription(description).ToLower();

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.UnitsOfMeasure.AnyAsync(u =>
                u.UomDescription.ToLower() == normalizedDescription &&
                u.Id != currentUomId);
        }

        public async Task AddAsync(UnitOfMeasure uom)
        {
            if (uom == null)
                throw new ArgumentNullException(nameof(uom));

            string normalizedCode = NormalizeCode(uom.UomCode);
            string normalizedDescription = NormalizeDescription(uom.UomDescription);

            ValidateUomCode(normalizedCode);
            ValidateUomDescription(normalizedDescription);
            ValidateDisplayOrder(uom.DisplayOrder);

            using var context = await _contextFactory.CreateDbContextAsync();

            bool codeExists = await context.UnitsOfMeasure.AnyAsync(u =>
                u.UomCode.ToUpper() == normalizedCode);

            if (codeExists)
                throw new InvalidOperationException($"UOM code '{normalizedCode}' already exists.");

            bool descriptionExists = await context.UnitsOfMeasure.AnyAsync(u =>
                u.UomDescription.ToLower() == normalizedDescription.ToLower());

            if (descriptionExists)
                throw new InvalidOperationException($"UOM description '{normalizedDescription}' already exists.");

            DateTime now = DateTime.Now;

            uom.UomCode = normalizedCode;
            uom.UomDescription = normalizedDescription;
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

            ValidateUomDescription(normalizedDescription);
            ValidateDisplayOrder(uom.DisplayOrder);

            using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Id == uom.Id);

            if (existing == null)
                throw new InvalidOperationException("UOM record was not found.");

            bool descriptionExists = await context.UnitsOfMeasure.AnyAsync(u =>
                u.UomDescription.ToLower() == normalizedDescription.ToLower() &&
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

            // UOM code is intentionally not updated after creation.
            existing.UomDescription = normalizedDescription;
            existing.AllowDecimals = uom.AllowDecimals;
            existing.DisplayOrder = uom.DisplayOrder;
            existing.IsActive = uom.IsActive;
            existing.UpdatedAt = now;

            if (!existing.IsActive)
                existing.DeactivatedAt ??= now;
            else
                existing.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var uom = await context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Id == id);

            if (uom == null)
                return;

            DateTime now = DateTime.Now;

            uom.IsActive = false;
            uom.UpdatedAt = now;
            uom.DeactivatedAt ??= now;

            await context.SaveChangesAsync();
        }

        public async Task<bool> HasLinkedItemsAsync(int uomId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemParents
                .AnyAsync(i => i.UnitOfMeasureId == uomId);
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var uom = await context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Id == id);

            if (uom == null)
                return;

            bool hasLinkedItems = await context.ItemParents
                .AnyAsync(i => i.UnitOfMeasureId == id);

            if (hasLinkedItems)
            {
                throw new InvalidOperationException(
                    "This Unit of Measure is assigned to item records. Deactivate it instead of deleting it.");
            }

            context.UnitsOfMeasure.Remove(uom);
            await context.SaveChangesAsync();
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

            if (displayOrder > 9999)
                throw new InvalidOperationException("Display order is too large.");
        }
    }
}