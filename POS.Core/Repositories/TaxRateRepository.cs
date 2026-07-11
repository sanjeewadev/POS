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
    public sealed class TaxRateLinkedDataSummary
    {
        public int LegacyItemCount { get; set; }
        public int PurchaseOrderLineCount { get; set; }
        public int GrnLineCount { get; set; }
        public int SalesLineCount { get; set; }
        public int CustomerReturnLineCount { get; set; }
        public int SupplierReturnLineCount { get; set; }

        public int TotalTransactionCount =>
            PurchaseOrderLineCount +
            GrnLineCount +
            SalesLineCount +
            CustomerReturnLineCount +
            SupplierReturnLineCount;

        public bool HasLinkedData =>
            LegacyItemCount > 0 ||
            TotalTransactionCount > 0;

        public string ToCompactSummary()
        {
            if (!HasLinkedData)
                return "Unused rate version. Rate, start date and period may still be corrected.";

            var parts = new List<string>();

            if (LegacyItemCount > 0)
                parts.Add($"legacy items: {LegacyItemCount}");

            if (PurchaseOrderLineCount > 0)
                parts.Add($"PO lines: {PurchaseOrderLineCount}");

            if (GrnLineCount > 0)
                parts.Add($"GRN lines: {GrnLineCount}");

            if (SalesLineCount > 0)
                parts.Add($"sales lines: {SalesLineCount}");

            if (CustomerReturnLineCount > 0)
                parts.Add($"customer-return lines: {CustomerReturnLineCount}");

            if (SupplierReturnLineCount > 0)
                parts.Add($"supplier-return lines: {SupplierReturnLineCount}");

            return "Used by " + string.Join(", ", parts) +
                   ". The rate and effective-from date are locked; historical snapshots remain unchanged.";
        }

        public string ToUserMessage(string taxCode)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Tax rate '{taxCode}' cannot be deleted because it is already used.");
            builder.AppendLine();

            if (LegacyItemCount > 0)
                builder.AppendLine($"Legacy Item Master references: {LegacyItemCount}");

            if (PurchaseOrderLineCount > 0)
                builder.AppendLine($"Purchase Order line records: {PurchaseOrderLineCount}");

            if (GrnLineCount > 0)
                builder.AppendLine($"GRN line records: {GrnLineCount}");

            if (SalesLineCount > 0)
                builder.AppendLine($"Sales line records: {SalesLineCount}");

            if (CustomerReturnLineCount > 0)
                builder.AppendLine($"Customer-return line records: {CustomerReturnLineCount}");

            if (SupplierReturnLineCount > 0)
                builder.AppendLine($"Supplier-return line records: {SupplierReturnLineCount}");

            builder.AppendLine();
            builder.AppendLine("Keep the record for history. End its effective period or deactivate it only when a replacement rate is available.");

            return builder.ToString();
        }
    }

    public class TaxRateRepository
    {
        private static readonly DateTime StandardVatEffectiveFrom = new(2024, 1, 1);

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public TaxRateRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task EnsureDefaultsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            DateTime now = DateTime.Now;

            var approvedCategories = new[]
            {
                new TaxCategorySeed(
                    TaxCategoryCodes.Standard,
                    "Standard VAT",
                    TaxTreatmentTypes.StandardRated,
                    IsRateBased: true,
                    DisplayOrder: 10),

                new TaxCategorySeed(
                    TaxCategoryCodes.ZeroRated,
                    "Zero Rated",
                    TaxTreatmentTypes.ZeroRated,
                    IsRateBased: false,
                    DisplayOrder: 20),

                new TaxCategorySeed(
                    TaxCategoryCodes.Exempt,
                    "Exempt",
                    TaxTreatmentTypes.Exempt,
                    IsRateBased: false,
                    DisplayOrder: 30),

                new TaxCategorySeed(
                    TaxCategoryCodes.OutOfScope,
                    "Out of Scope",
                    TaxTreatmentTypes.OutOfScope,
                    IsRateBased: false,
                    DisplayOrder: 40)
            };

            foreach (var seed in approvedCategories)
                await EnsureCategoryAsync(context, seed, now);

            await context.SaveChangesAsync();

            int standardCategoryId = await context.TaxCategories
                .Where(c => c.CategoryCode == TaxCategoryCodes.Standard)
                .Select(c => c.Id)
                .SingleAsync();

            var standardByCode = await context.TaxRates
                .FirstOrDefaultAsync(t =>
                    EF.Functions.Collate(t.TaxCode, "NOCASE") == "VAT-STD");

            bool anyStandardRate = await context.TaxRates
                .AnyAsync(t => t.TaxCategoryId == standardCategoryId);

            if (standardByCode == null && !anyStandardRate)
            {
                context.TaxRates.Add(new TaxRate
                {
                    TaxCode = "VAT-STD",
                    TaxName = "Standard VAT",
                    TaxCategoryId = standardCategoryId,
                    RatePercent = 18m,
                    EffectiveFrom = StandardVatEffectiveFrom,
                    EffectiveTo = null,
                    ChangeReason = "Sri Lanka standard VAT rate effective from 2024-01-01.",
                    CreatedBy = "System",
                    UpdatedBy = "System",
                    IsActive = true,
                    IsSystemDefault = true,
                    DisplayOrder = 10,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeactivatedAt = null
                });
            }
            else if (standardByCode != null)
            {
                bool changed = false;

                if (!standardByCode.TaxCategoryId.HasValue)
                {
                    standardByCode.TaxCategoryId = standardCategoryId;
                    changed = true;
                }

                // Only backfill the legally unambiguous 18% VAT-STD record.
                // A customized or ambiguous rate is left for manual review.
                if (!standardByCode.EffectiveFrom.HasValue &&
                    standardByCode.RatePercent == 18m)
                {
                    standardByCode.EffectiveFrom = StandardVatEffectiveFrom;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(standardByCode.ChangeReason) &&
                    standardByCode.RatePercent == 18m)
                {
                    standardByCode.ChangeReason =
                        "Sri Lanka standard VAT rate effective from 2024-01-01.";
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(standardByCode.CreatedBy))
                {
                    standardByCode.CreatedBy = "System";
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(standardByCode.UpdatedBy))
                {
                    standardByCode.UpdatedBy = "System";
                    changed = true;
                }

                if (!standardByCode.IsSystemDefault)
                {
                    standardByCode.IsSystemDefault = true;
                    changed = true;
                }

                if (changed)
                    standardByCode.UpdatedAt = now;
            }

            await context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<TaxCategory>> GetApprovedCategoriesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            string[] approvedCodes =
            {
                TaxCategoryCodes.Standard,
                TaxCategoryCodes.ZeroRated,
                TaxCategoryCodes.Exempt,
                TaxCategoryCodes.OutOfScope
            };

            return await context.TaxCategories
                .AsNoTracking()
                .Where(c => approvedCodes.Contains(c.CategoryCode))
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryCode)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TaxRate>> GetAllAsync(
            string searchTerm = "",
            bool includeDeactivated = false,
            int? taxCategoryId = null,
            bool unclassifiedOnly = false)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<TaxRate> query = context.TaxRates
                .AsNoTracking()
                .Include(t => t.TaxCategory);

            if (!includeDeactivated)
                query = query.Where(t => t.IsActive);

            if (unclassifiedOnly)
                query = query.Where(t => t.TaxCategoryId == null);
            else if (taxCategoryId.HasValue)
                query = query.Where(t => t.TaxCategoryId == taxCategoryId.Value);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(t =>
                    EF.Functions.Like(t.TaxCode, $"%{term}%") ||
                    EF.Functions.Like(t.TaxName, $"%{term}%") ||
                    (t.TaxCategory != null &&
                     EF.Functions.Like(t.TaxCategory.CategoryName, $"%{term}%")));
            }

            return await query
                .OrderBy(t => !t.IsActive)
                .ThenByDescending(t => t.EffectiveFrom)
                .ThenBy(t => t.DisplayOrder)
                .ThenBy(t => t.TaxCode)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TaxRate>> GetActiveAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.TaxRates
                .AsNoTracking()
                .Include(t => t.TaxCategory)
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

            return await context.TaxRates
                .AsNoTracking()
                .Include(t => t.TaxCategory)
                .FirstOrDefaultAsync(t =>
                    EF.Functions.Collate(t.TaxCode, "NOCASE") == code);
        }

        public async Task<TaxRate?> GetEffectiveRateAsync(
            string categoryCode,
            DateTime transactionDate)
        {
            string code = NormalizeCode(categoryCode);
            DateTime date = transactionDate.Date;

            if (string.IsNullOrWhiteSpace(code))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var category = await context.TaxCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    EF.Functions.Collate(c.CategoryCode, "NOCASE") == code &&
                    c.IsActive);

            if (category == null || !category.IsRateBased)
                return null;

            return await context.TaxRates
                .AsNoTracking()
                .Include(t => t.TaxCategory)
                .Where(t =>
                    t.TaxCategoryId == category.Id &&
                    t.IsActive &&
                    t.EffectiveFrom.HasValue &&
                    t.EffectiveFrom.Value <= date &&
                    (!t.EffectiveTo.HasValue || t.EffectiveTo.Value >= date))
                .OrderByDescending(t => t.EffectiveFrom)
                .FirstOrDefaultAsync();
        }

        public async Task<decimal> GetRatePercentByCodeAsync(string taxCode)
        {
            var taxRate = await GetByCodeAsync(taxCode);

            if (taxRate == null || !taxRate.IsActive)
                return 0m;

            return taxRate.RatePercent;
        }

        public async Task<TaxRate> AddAsync(TaxRate taxRate)
        {
            if (taxRate == null)
                throw new ArgumentNullException(nameof(taxRate));

            NormalizeAndValidateBasic(taxRate);

            await using var context = await _contextFactory.CreateDbContextAsync();

            await ValidateCategoryAndPeriodAsync(
                context,
                taxRate,
                currentId: 0);

            bool codeExists = await context.TaxRates.AnyAsync(t =>
                EF.Functions.Collate(t.TaxCode, "NOCASE") == taxRate.TaxCode);

            if (codeExists)
                throw new InvalidOperationException($"Tax code '{taxRate.TaxCode}' already exists.");

            DateTime now = DateTime.Now;

            taxRate.Id = 0;
            taxRate.CreatedAt = now;
            taxRate.UpdatedAt = now;
            taxRate.CreatedBy = NormalizeAuditName(taxRate.CreatedBy);
            taxRate.UpdatedBy = NormalizeAuditName(taxRate.UpdatedBy);
            taxRate.DeactivatedAt = taxRate.IsActive ? null : now;

            context.TaxRates.Add(taxRate);
            await context.SaveChangesAsync();

            return taxRate;
        }

        public async Task UpdateAsync(TaxRate taxRate)
        {
            if (taxRate == null)
                throw new ArgumentNullException(nameof(taxRate));

            NormalizeAndValidateBasic(taxRate);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.TaxRates
                .Include(t => t.TaxCategory)
                .FirstOrDefaultAsync(t => t.Id == taxRate.Id);

            if (existing == null)
                throw new InvalidOperationException("Selected tax rate was not found.");

            if (!string.Equals(existing.TaxCode, taxRate.TaxCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tax code cannot be changed after the rate version is saved.");

            if (existing.TaxCategoryId != taxRate.TaxCategoryId)
                throw new InvalidOperationException("Tax category cannot be changed after the rate version is saved.");

            var linkedData = await BuildLinkedDataSummaryAsync(context, existing);

            if (linkedData.HasLinkedData)
            {
                if (existing.RatePercent != taxRate.RatePercent)
                {
                    throw new InvalidOperationException(
                        "This tax rate is already used. Create a new effective-dated rate version instead of changing its percentage.");
                }

                if (NormalizeNullableDate(existing.EffectiveFrom) !=
                    NormalizeNullableDate(taxRate.EffectiveFrom))
                {
                    throw new InvalidOperationException(
                        "This tax rate is already used. Its effective-from date cannot be changed.");
                }
            }

            await ValidateCategoryAndPeriodAsync(
                context,
                taxRate,
                currentId: existing.Id);

            existing.TaxName = taxRate.TaxName;
            existing.RatePercent = taxRate.RatePercent;
            existing.EffectiveFrom = NormalizeNullableDate(taxRate.EffectiveFrom);
            existing.EffectiveTo = NormalizeNullableDate(taxRate.EffectiveTo);
            existing.ChangeReason = NormalizeOptionalText(taxRate.ChangeReason);
            existing.DisplayOrder = taxRate.DisplayOrder;
            existing.UpdatedBy = NormalizeAuditName(taxRate.UpdatedBy);
            existing.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
        }

        public async Task<TaxRateLinkedDataSummary> GetLinkedDataSummaryAsync(int taxRateId)
        {
            if (taxRateId <= 0)
                return new TaxRateLinkedDataSummary();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.TaxRates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null)
                return new TaxRateLinkedDataSummary();

            return await BuildLinkedDataSummaryAsync(context, taxRate);
        }

        public async Task DeleteAsync(int taxRateId)
        {
            if (taxRateId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.TaxRates
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null)
                return;

            var linkedData = await BuildLinkedDataSummaryAsync(context, taxRate);

            if (linkedData.HasLinkedData)
                throw new InvalidOperationException(linkedData.ToUserMessage(taxRate.TaxCode));

            if (taxRate.IsSystemDefault)
            {
                throw new InvalidOperationException(
                    "The system standard VAT record cannot be deleted. Keep it for the effective-rate history.");
            }

            context.TaxRates.Remove(taxRate);
            await context.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int taxRateId, string updatedBy = "System")
        {
            if (taxRateId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.TaxRates
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null || !taxRate.IsActive)
                return;

            DateTime today = DateTime.Today;

            bool isEffectiveToday =
                taxRate.TaxCategoryId.HasValue &&
                taxRate.EffectiveFrom.HasValue &&
                taxRate.EffectiveFrom.Value <= today &&
                (!taxRate.EffectiveTo.HasValue ||
                 taxRate.EffectiveTo.Value >= today);

            if (isEffectiveToday)
            {
                bool replacementExists = await context.TaxRates.AnyAsync(t =>
                    t.Id != taxRate.Id &&
                    t.TaxCategoryId == taxRate.TaxCategoryId &&
                    t.IsActive &&
                    t.EffectiveFrom.HasValue &&
                    t.EffectiveFrom.Value <= today &&
                    (!t.EffectiveTo.HasValue || t.EffectiveTo.Value >= today));

                if (!replacementExists)
                {
                    throw new InvalidOperationException(
                        "This is the rate currently effective today. End its period and create the replacement rate before deactivating it.");
                }
            }

            taxRate.IsActive = false;
            taxRate.DeactivatedAt = DateTime.Now;
            taxRate.UpdatedAt = DateTime.Now;
            taxRate.UpdatedBy = NormalizeAuditName(updatedBy);

            await context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(int taxRateId, string updatedBy = "System")
        {
            if (taxRateId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxRate = await context.TaxRates
                .FirstOrDefaultAsync(t => t.Id == taxRateId);

            if (taxRate == null || taxRate.IsActive)
                return;

            if (taxRate.TaxCategoryId.HasValue)
            {
                var category = await context.TaxCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == taxRate.TaxCategoryId.Value);

                if (category?.IsRateBased == true)
                {
                    var candidate = new TaxRate
                    {
                        Id = taxRate.Id,
                        TaxCode = taxRate.TaxCode,
                        TaxName = taxRate.TaxName,
                        TaxCategoryId = taxRate.TaxCategoryId,
                        RatePercent = taxRate.RatePercent,
                        EffectiveFrom = taxRate.EffectiveFrom,
                        EffectiveTo = taxRate.EffectiveTo,
                        ChangeReason = taxRate.ChangeReason,
                        IsActive = true,
                        DisplayOrder = taxRate.DisplayOrder
                    };

                    await ValidateCategoryAndPeriodAsync(
                        context,
                        candidate,
                        currentId: taxRate.Id);
                }
            }

            taxRate.IsActive = true;
            taxRate.DeactivatedAt = null;
            taxRate.UpdatedAt = DateTime.Now;
            taxRate.UpdatedBy = NormalizeAuditName(updatedBy);

            await context.SaveChangesAsync();
        }

        private static async Task EnsureCategoryAsync(
            AppDbContext context,
            TaxCategorySeed seed,
            DateTime now)
        {
            var existing = await context.TaxCategories
                .FirstOrDefaultAsync(c =>
                    EF.Functions.Collate(c.CategoryCode, "NOCASE") == seed.Code);

            if (existing == null)
            {
                context.TaxCategories.Add(new TaxCategory
                {
                    CategoryCode = seed.Code,
                    CategoryName = seed.Name,
                    TreatmentType = seed.TreatmentType,
                    IsRateBased = seed.IsRateBased,
                    IsActive = true,
                    DisplayOrder = seed.DisplayOrder,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeactivatedAt = null
                });

                return;
            }

            bool changed = false;

            if (existing.CategoryName != seed.Name)
            {
                existing.CategoryName = seed.Name;
                changed = true;
            }

            if (existing.TreatmentType != seed.TreatmentType)
            {
                existing.TreatmentType = seed.TreatmentType;
                changed = true;
            }

            if (existing.IsRateBased != seed.IsRateBased)
            {
                existing.IsRateBased = seed.IsRateBased;
                changed = true;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.DeactivatedAt = null;
                changed = true;
            }

            if (existing.DisplayOrder != seed.DisplayOrder)
            {
                existing.DisplayOrder = seed.DisplayOrder;
                changed = true;
            }

            if (changed)
                existing.UpdatedAt = now;
        }

        private static async Task ValidateCategoryAndPeriodAsync(
            AppDbContext context,
            TaxRate taxRate,
            int currentId)
        {
            if (!taxRate.TaxCategoryId.HasValue)
                throw new InvalidOperationException("A tax category is required for a new authoritative rate version.");

            var category = await context.TaxCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == taxRate.TaxCategoryId.Value);

            if (category == null)
                throw new InvalidOperationException("The selected tax category was not found.");

            if (!category.IsActive)
                throw new InvalidOperationException("The selected tax category is inactive.");

            if (!category.IsRateBased)
            {
                throw new InvalidOperationException(
                    $"'{category.CategoryName}' is a fixed tax treatment and does not require a percentage rate record.");
            }

            if (!taxRate.EffectiveFrom.HasValue)
                throw new InvalidOperationException("Effective-from date is required.");

            DateTime start = taxRate.EffectiveFrom.Value.Date;
            DateTime? end = NormalizeNullableDate(taxRate.EffectiveTo);

            if (end.HasValue && end.Value < start)
                throw new InvalidOperationException("Effective-to date cannot be earlier than effective-from date.");

            taxRate.EffectiveFrom = start;
            taxRate.EffectiveTo = end;

            if (!taxRate.IsActive)
                return;

            DateTime upperBound = end ?? DateTime.MaxValue.Date;

            bool overlapExists = await context.TaxRates.AnyAsync(t =>
                t.Id != currentId &&
                t.TaxCategoryId == taxRate.TaxCategoryId &&
                t.IsActive &&
                (!t.EffectiveFrom.HasValue || t.EffectiveFrom.Value <= upperBound) &&
                (!t.EffectiveTo.HasValue || t.EffectiveTo.Value >= start));

            if (overlapExists)
            {
                throw new InvalidOperationException(
                    "The effective period overlaps another active rate version in the same tax category. End the previous period before saving the new version.");
            }
        }

        private static async Task<TaxRateLinkedDataSummary> BuildLinkedDataSummaryAsync(
            AppDbContext context,
            TaxRate taxRate)
        {
            string code = NormalizeCode(taxRate.TaxCode);

            var result = new TaxRateLinkedDataSummary
            {
                LegacyItemCount = await context.ItemParents
                    .AsNoTracking()
                    .CountAsync(i =>
                        EF.Functions.Collate(i.TaxCode ?? string.Empty, "NOCASE") == code),

                PurchaseOrderLineCount = await context.PoLines
                    .AsNoTracking()
                    .CountAsync(l =>
                        l.TaxRateId == taxRate.Id ||
                        EF.Functions.Collate(l.TaxCode ?? string.Empty, "NOCASE") == code ||
                        EF.Functions.Collate(l.TaxCodeSnapshot ?? string.Empty, "NOCASE") == code),

                GrnLineCount = await context.GrnLines
                    .AsNoTracking()
                    .CountAsync(l =>
                        l.TaxRateId == taxRate.Id ||
                        EF.Functions.Collate(l.TaxCodeSnapshot ?? string.Empty, "NOCASE") == code),

                SalesLineCount = await context.SalesLines
                    .AsNoTracking()
                    .CountAsync(l =>
                        l.TaxRateId == taxRate.Id ||
                        EF.Functions.Collate(l.TaxCodeSnapshot ?? string.Empty, "NOCASE") == code),

                CustomerReturnLineCount = await context.CustomerReturnLines
                    .AsNoTracking()
                    .CountAsync(l =>
                        l.TaxRateId == taxRate.Id ||
                        EF.Functions.Collate(l.TaxCodeSnapshot ?? string.Empty, "NOCASE") == code),

                SupplierReturnLineCount = await context.SupplierReturnLines
                    .AsNoTracking()
                    .CountAsync(l =>
                        l.TaxRateId == taxRate.Id ||
                        EF.Functions.Collate(l.TaxCodeSnapshot ?? string.Empty, "NOCASE") == code)
            };

            return result;
        }

        private static void NormalizeAndValidateBasic(TaxRate taxRate)
        {
            taxRate.TaxCode = NormalizeCode(taxRate.TaxCode);
            taxRate.TaxName = NormalizeText(taxRate.TaxName);
            taxRate.ChangeReason = NormalizeOptionalText(taxRate.ChangeReason);

            if (string.IsNullOrWhiteSpace(taxRate.TaxCode))
                throw new InvalidOperationException("Tax code is required.");

            if (taxRate.TaxCode.Length > 20)
                throw new InvalidOperationException("Tax code cannot be longer than 20 characters.");

            if (string.IsNullOrWhiteSpace(taxRate.TaxName))
                throw new InvalidOperationException("Tax name is required.");

            if (taxRate.TaxName.Length > 100)
                throw new InvalidOperationException("Tax name cannot be longer than 100 characters.");

            if (taxRate.RatePercent <= 0 || taxRate.RatePercent > 100)
                throw new InvalidOperationException("A rate-based tax percentage must be greater than 0 and not more than 100.");

            if (decimal.Round(taxRate.RatePercent, 2) != taxRate.RatePercent)
                throw new InvalidOperationException("Tax rate can have no more than two decimal places.");

            if (taxRate.ChangeReason?.Length > 250)
                throw new InvalidOperationException("Change reason cannot be longer than 250 characters.");

            if (taxRate.DisplayOrder < 0 || taxRate.DisplayOrder > 9999)
                throw new InvalidOperationException("Display order must be between 0 and 9999.");
        }

        private static DateTime? NormalizeNullableDate(DateTime? value)
        {
            return value?.Date;
        }

        private static string NormalizeCode(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string? NormalizeOptionalText(string? value)
        {
            string normalized = NormalizeText(value);
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static string NormalizeAuditName(string? value)
        {
            string normalized = NormalizeText(value);
            return string.IsNullOrWhiteSpace(normalized) ? "System" : normalized;
        }

        private sealed record TaxCategorySeed(
            string Code,
            string Name,
            string TreatmentType,
            bool IsRateBased,
            int DisplayOrder);
    }
}
