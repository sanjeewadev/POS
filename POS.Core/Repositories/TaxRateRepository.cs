using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class TaxRateLinkedDataSummary
    {
        public int ItemCount { get; set; }
        public int PoLineCount { get; set; }

        public bool HasLinkedData => ItemCount > 0 || PoLineCount > 0;

        public string ToUserMessage(string taxCode)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Tax rate '{taxCode}' cannot be deleted because it is already used.");
            builder.AppendLine();

            if (ItemCount > 0)
                builder.AppendLine($"Item Master records: {ItemCount}");

            if (PoLineCount > 0)
                builder.AppendLine($"Purchase Order line records: {PoLineCount}");

            builder.AppendLine();
            builder.AppendLine("Deactivate the tax rate instead of deleting it.");

            return builder.ToString();
        }
    }

    public class TaxRateRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public TaxRateRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task EnsureDefaultsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var set = context.Set<TaxRate>();

            DateTime now = DateTime.Now;

            int? standardCategoryId = await context.TaxCategories
                .Where(c => c.CategoryCode == TaxCategoryCodes.Standard)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            await EnsureDefaultAsync(
                set,
                "TAX-FREE",
                "Tax Free / Exempted",
                0m,
                10,
                now,
                taxCategoryId: null);

            await EnsureDefaultAsync(
                set,
                "VAT-STD",
                "Standard VAT",
                18m,
                20,
                now,
                taxCategoryId: standardCategoryId);

            await EnsureDefaultAsync(
                set,
                "VAT-RED",
                "Reduced VAT",
                5m,
                30,
                now,
                taxCategoryId: null);

            await context.SaveChangesAsync();
        }

        private static async Task EnsureDefaultAsync(
            DbSet<TaxRate> set,
            string taxCode,
            string taxName,
            decimal ratePercent,
            int displayOrder,
            DateTime now,
            int? taxCategoryId)
        {
            string code = NormalizeCode(taxCode);

            var existing = await set.FirstOrDefaultAsync(t =>
                EF.Functions.Collate(t.TaxCode, "NOCASE") == code);

            if (existing != null)
            {
                if (!existing.TaxCategoryId.HasValue &&
                    taxCategoryId.HasValue)
                {
                    existing.TaxCategoryId = taxCategoryId;
                    existing.UpdatedAt = now;
                }

                return;
            }

            await set.AddAsync(new TaxRate
            {
                TaxCode = code,
                TaxName = NormalizeText(taxName),
                TaxCategoryId = taxCategoryId,
                RatePercent = ratePercent,
                IsActive = true,
                IsSystemDefault = true,
                DisplayOrder = displayOrder,
                CreatedAt = now,
                UpdatedAt = now,
                DeactivatedAt = null
            });
        }

        public async Task<IReadOnlyList<TaxRate>> GetAllAsync(
            string searchTerm = "",
            bool includeDeactivated = false)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<TaxRate> query = context.Set<TaxRate>()
                .AsNoTracking();

            if (!includeDeactivated)
                query = query.Where(t => t.IsActive);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(t =>
                    EF.Functions.Like(t.TaxCode, $"%{term}%") ||
                    EF.Functions.Like(t.TaxName, $"%{term}%"));
            }

            return await query
                .OrderBy(t => !t.IsActive)
                .ThenBy(t => t.DisplayOrder)
                .ThenBy(t => t.TaxCode)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TaxRate>> GetActiveAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Set<TaxRate>()
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.TaxCode)
                .ToListAsync();
        }

        public async Task<TaxRate?> GetByCodeAsync(string taxCode)
        {
            string code = NormalizeCode(taxCode);

            if (string.IsNullOrWhiteSpace(code))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Set<TaxRate>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    EF.Functions.Collate(t.TaxCode, "NOCASE") == code);
        }

        public async Task<decimal> GetRatePercentByCodeAsync(string taxCode)
        {
            var taxRate = await GetByCodeAsync(taxCode);

            if (taxRate == null || !taxRate.IsActive)
                return 0m;

            return taxRate.RatePercent;
        }

        public async Task<bool> IsTaxCodeUniqueAsync(string taxCode, int currentId = 0)
        {
            string code = NormalizeCode(taxCode);

            if (string.IsNullOrWhiteSpace(code))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Set<TaxRate>().AnyAsync(t =>
                EF.Functions.Collate(t.TaxCode, "NOCASE") == code &&
                t.Id != currentId);
        }

        public async Task<bool> IsTaxNameUniqueAsync(string taxName, int currentId = 0)
        {
            string name = NormalizeText(taxName);

            if (string.IsNullOrWhiteSpace(name))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Set<TaxRate>().AnyAsync(t =>
                EF.Functions.Collate(t.TaxName, "NOCASE") == name &&
                t.Id != currentId);
        }

        public async Task<TaxRate> AddAsync(TaxRate taxRate)
        {
            if (taxRate == null)
                throw new ArgumentNullException(nameof(taxRate));

            NormalizeAndValidate(taxRate, isNew: true);

            await using var context = await _contextFactory.CreateDbContextAsync();

            bool codeUnique = await IsTaxCodeUniqueAsync(taxRate.TaxCode);

            if (!codeUnique)
                throw new InvalidOperationException($"Tax code '{taxRate.TaxCode}' already exists.");

            bool nameUnique = await IsTaxNameUniqueAsync(taxRate.TaxName);

            if (!nameUnique)
                throw new InvalidOperationException($"Tax name '{taxRate.TaxName}' already exists.");

            DateTime now = DateTime.Now;

            taxRate.Id = 0;
            taxRate.CreatedAt = now;
            taxRate.UpdatedAt = now;
            taxRate.DeactivatedAt = taxRate.IsActive ? null : now;

            await context.Set<TaxRate>().AddAsync(taxRate);
            await context.SaveChangesAsync();

            return taxRate;
        }

        public async Task UpdateAsync(TaxRate taxRate)
        {
            if (taxRate == null)
                throw new ArgumentNullException(nameof(taxRate));

            NormalizeAndValidate(taxRate, isNew: false);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.Set<TaxRate>()
                .FirstOrDefaultAsync(t => t.Id == taxRate.Id);

            if (existing == null)
                throw new InvalidOperationException("Selected tax rate was not found.");

            bool nameUnique = await context.Set<TaxRate>().AllAsync(t =>
                t.Id == taxRate.Id ||
                EF.Functions.Collate(t.TaxName, "NOCASE") != taxRate.TaxName);

            if (!nameUnique)
                throw new InvalidOperationException($"Tax name '{taxRate.TaxName}' already exists.");

            // TaxCode is intentionally locked after first save.
            existing.TaxName = taxRate.TaxName;
            existing.RatePercent = taxRate.RatePercent;
            existing.IsActive = taxRate.IsActive;
            existing.DisplayOrder = taxRate.DisplayOrder;
            existing.UpdatedAt = DateTime.Now;
            existing.DeactivatedAt = taxRate.IsActive ? null : DateTime.Now;

            await context.SaveChangesAsync();
        }

        public async Task<TaxRateLinkedDataSummary> GetLinkedDataSummaryAsync(int taxRateId)
        {
            var result = new TaxRateLinkedDataSummary();

            if (taxRateId <= 0)
                return result;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.Set<TaxRate>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null)
                return result;

            string code = NormalizeCode(taxRate.TaxCode);

            result.ItemCount = await context.ItemParents
                .AsNoTracking()
                .CountAsync(i => EF.Functions.Collate(i.TaxCode ?? string.Empty, "NOCASE") == code);

            result.PoLineCount = await context.PoLines
                .AsNoTracking()
                .CountAsync(l => EF.Functions.Collate(l.TaxCode ?? string.Empty, "NOCASE") == code);

            return result;
        }

        public async Task DeleteAsync(int taxRateId)
        {
            if (taxRateId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.Set<TaxRate>()
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null)
                return;

            var linkedData = await GetLinkedDataSummaryAsync(taxRateId);

            if (linkedData.HasLinkedData)
                throw new InvalidOperationException(linkedData.ToUserMessage(taxRate.TaxCode));

            if (taxRate.IsSystemDefault)
            {
                throw new InvalidOperationException(
                    "System default tax rates should not be deleted. Deactivate it if it is not used for new items.");
            }

            context.Set<TaxRate>().Remove(taxRate);
            await context.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int taxRateId)
        {
            if (taxRateId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.Set<TaxRate>()
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null)
                return;

            taxRate.IsActive = false;
            taxRate.DeactivatedAt ??= DateTime.Now;
            taxRate.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(int taxRateId)
        {
            if (taxRateId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.Set<TaxRate>()
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null)
                return;

            taxRate.IsActive = true;
            taxRate.DeactivatedAt = null;
            taxRate.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
        }

        private static void NormalizeAndValidate(TaxRate taxRate, bool isNew)
        {
            taxRate.TaxCode = NormalizeCode(taxRate.TaxCode);
            taxRate.TaxName = NormalizeText(taxRate.TaxName);

            if (string.IsNullOrWhiteSpace(taxRate.TaxCode))
                throw new InvalidOperationException("Tax code is required.");

            if (taxRate.TaxCode.Length > 20)
                throw new InvalidOperationException("Tax code cannot be longer than 20 characters.");

            if (string.IsNullOrWhiteSpace(taxRate.TaxName))
                throw new InvalidOperationException("Tax name is required.");

            if (taxRate.TaxName.Length > 100)
                throw new InvalidOperationException("Tax name cannot be longer than 100 characters.");

            if (taxRate.RatePercent < 0 || taxRate.RatePercent > 100)
                throw new InvalidOperationException("Tax rate must be between 0 and 100.");

            if (taxRate.DisplayOrder < 0 || taxRate.DisplayOrder > 9999)
                throw new InvalidOperationException("Display order must be between 0 and 9999.");
        }

        private static string NormalizeCode(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
