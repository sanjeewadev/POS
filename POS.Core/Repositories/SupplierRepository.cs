using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public sealed class SupplierLinkedDataSummary
    {
        public int PurchaseOrderCount { get; init; }
        public int GrnCount { get; init; }
        public int SupplierReturnCount { get; init; }
        public int SupplierLedgerCount { get; init; }
        public int ItemSupplierCount { get; init; }
        public int FreeIssueRuleCount { get; init; }
        public int FreeItemClaimCount { get; init; }

        public bool HasLinkedData =>
            PurchaseOrderCount > 0 ||
            GrnCount > 0 ||
            SupplierReturnCount > 0 ||
            SupplierLedgerCount > 0 ||
            ItemSupplierCount > 0 ||
            FreeIssueRuleCount > 0 ||
            FreeItemClaimCount > 0;

        public string ToUserMessage(string supplierName)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Supplier '{supplierName}' cannot be deleted because it is already linked to system records.");
            builder.AppendLine();
            builder.AppendLine("Linked records:");

            if (PurchaseOrderCount > 0)
                builder.AppendLine($"- Purchase Orders: {PurchaseOrderCount}");

            if (GrnCount > 0)
                builder.AppendLine($"- GRNs: {GrnCount}");

            if (SupplierReturnCount > 0)
                builder.AppendLine($"- Supplier Returns: {SupplierReturnCount}");

            if (SupplierLedgerCount > 0)
                builder.AppendLine($"- Supplier Ledger Entries: {SupplierLedgerCount}");

            if (ItemSupplierCount > 0)
                builder.AppendLine($"- Item Supplier Assignments: {ItemSupplierCount}");

            if (FreeIssueRuleCount > 0)
                builder.AppendLine($"- Free Issue Rules: {FreeIssueRuleCount}");

            if (FreeItemClaimCount > 0)
                builder.AppendLine($"- Free Item Claims: {FreeItemClaimCount}");

            builder.AppendLine();
            builder.AppendLine("Suspend / deactivate this supplier instead of deleting it.");

            return builder.ToString();
        }
    }

    public class SupplierRepository
    {
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private static readonly Regex SupplierCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PhoneRegex =
            new("^[0-9+\\-\\s()]{7,20}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public SupplierRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // Used by dropdowns across the system:
        // PO, GRN, Supplier Return, Item Supplier assignment.
        // Only active suppliers should appear here.
        public async Task<IReadOnlyList<Supplier>> GetAllAsync(int take = MaxTakeLimit)
        {
            return await GetActiveAsync(take);
        }

        public async Task<IReadOnlyList<Supplier>> GetActiveAsync(int take = MaxTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.SupplierName)
                .ThenBy(s => s.SupplierCode)
                .Take(take)
                .ToListAsync();
        }

        // Used by the Supplier Master grid.
        // This must include deactivated suppliers so old suppliers can be searched and edited.
        public async Task<IReadOnlyList<Supplier>> GetAllFilteredAsync(
            string searchTerm = "",
            bool includeDeactivated = true,
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<Supplier> query = context.Suppliers
                .AsNoTracking();

            if (!includeDeactivated)
            {
                query = query.Where(s => !s.IsDeactivated);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(s =>
                    EF.Functions.Like(s.SupplierCode, $"%{term}%") ||
                    EF.Functions.Like(s.SupplierName, $"%{term}%") ||
                    EF.Functions.Like(s.CompanyName, $"%{term}%") ||
                    EF.Functions.Like(s.ContactPerson, $"%{term}%") ||
                    EF.Functions.Like(s.Phone1, $"%{term}%") ||
                    EF.Functions.Like(s.Phone2, $"%{term}%") ||
                    EF.Functions.Like(s.Email, $"%{term}%"));
            }

            return await query
                .OrderBy(s => s.IsDeactivated)
                .ThenBy(s => s.SupplierName)
                .ThenBy(s => s.SupplierCode)
                .Take(take)
                .ToListAsync();
        }

        public async Task<Supplier?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentSupplierId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            if (string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Suppliers.AnyAsync(s =>
                EF.Functions.Collate(s.SupplierCode, "NOCASE") == normalizedCode &&
                s.Id != currentSupplierId);
        }

        public async Task<SupplierLinkedDataSummary> GetLinkedDataSummaryAsync(int supplierId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await GetLinkedDataSummaryAsync(context, supplierId);
        }

        public async Task<bool> HasLinkedDataAsync(int supplierId)
        {
            var summary = await GetLinkedDataSummaryAsync(supplierId);
            return summary.HasLinkedData;
        }

        public async Task AddAsync(Supplier supplier)
        {
            if (supplier == null)
                throw new ArgumentNullException(nameof(supplier));

            NormalizeSupplierForSave(supplier, isNew: true);
            ValidateSupplier(supplier, isNew: true);

            await using var context = await _contextFactory.CreateDbContextAsync();

            bool codeExists = await context.Suppliers.AnyAsync(s =>
                EF.Functions.Collate(s.SupplierCode, "NOCASE") == supplier.SupplierCode);

            if (codeExists)
                throw new InvalidOperationException($"Supplier code '{supplier.SupplierCode}' already exists.");

            DateTime now = DateTime.Now;

            supplier.CreatedAt = now;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt = supplier.IsDeactivated ? now : null;

            // New supplier balance must always start from zero.
            // Later GRN/payment/ledger modules should update this.
            supplier.CurrentBalance = 0m;

            await context.Suppliers.AddAsync(supplier);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Supplier supplier)
        {
            if (supplier == null)
                throw new ArgumentNullException(nameof(supplier));

            if (supplier.Id <= 0)
                throw new InvalidOperationException("Invalid supplier record.");

            NormalizeSupplierForSave(supplier, isNew: false);
            ValidateSupplier(supplier, isNew: false);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == supplier.Id);

            if (existing == null)
                throw new InvalidOperationException("Supplier record was not found.");

            DateTime now = DateTime.Now;
            bool wasDeactivated = existing.IsDeactivated;
            bool isNowDeactivated = supplier.IsDeactivated;

            // SupplierCode is intentionally not updated after creation.
            // CurrentBalance is intentionally not updated by Supplier Master.

            existing.SupplierName = supplier.SupplierName;
            existing.CompanyName = supplier.CompanyName;
            existing.ContactPerson = supplier.ContactPerson;
            existing.Phone1 = supplier.Phone1;
            existing.Phone2 = supplier.Phone2;
            existing.Email = supplier.Email;
            existing.Address = supplier.Address;

            existing.HasVat = supplier.HasVat;
            existing.VatNumber = supplier.HasVat ? supplier.VatNumber : string.Empty;
            existing.DefaultCreditDays = supplier.DefaultCreditDays;

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

        public async Task DeactivateAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return;

            if (supplier.IsDeactivated)
                return;

            DateTime now = DateTime.Now;

            supplier.IsDeactivated = true;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt = now;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return;

            if (!supplier.IsDeactivated)
                return;

            DateTime now = DateTime.Now;

            supplier.IsDeactivated = false;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            if (id <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return;

            var linkedData = await GetLinkedDataSummaryAsync(context, id);

            if (linkedData.HasLinkedData)
            {
                throw new InvalidOperationException(
                    linkedData.ToUserMessage(supplier.SupplierName));
            }

            try
            {
                context.Suppliers.Remove(supplier);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException(
                    "This supplier cannot be deleted because it is linked to other records. Suspend/deactivate it instead.");
            }
        }

        private static async Task<SupplierLinkedDataSummary> GetLinkedDataSummaryAsync(
            AppDbContext context,
            int supplierId)
        {
            if (supplierId <= 0)
                return new SupplierLinkedDataSummary();

            int poCount = await CountLinkedEntityAsync(context, context.PoHeaders, supplierId);
            int grnCount = await CountLinkedEntityAsync(context, context.GrnHeaders, supplierId);
            int returnCount = await CountLinkedEntityAsync(context, context.SupplierReturnHeaders, supplierId);
            int ledgerCount = await CountLinkedEntityAsync(context, context.SupplierLedgers, supplierId);
            int itemSupplierCount = await CountLinkedEntityAsync(context, context.ItemSuppliers, supplierId);
            int freeIssueRuleCount = await CountLinkedEntityAsync(context, context.FreeIssueRules, supplierId);
            int freeItemClaimCount = await CountLinkedEntityAsync(context, context.FreeItemClaimLogs, supplierId);

            return new SupplierLinkedDataSummary
            {
                PurchaseOrderCount = poCount,
                GrnCount = grnCount,
                SupplierReturnCount = returnCount,
                SupplierLedgerCount = ledgerCount,
                ItemSupplierCount = itemSupplierCount,
                FreeIssueRuleCount = freeIssueRuleCount,
                FreeItemClaimCount = freeItemClaimCount
            };
        }

        private static async Task<int> CountLinkedEntityAsync<TEntity>(
            AppDbContext context,
            IQueryable<TEntity> query,
            int supplierId) where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var property = entityType?.FindProperty("SupplierId");

            if (property == null)
                return 0;

            if (property.ClrType == typeof(int))
            {
                return await query.CountAsync(e =>
                    EF.Property<int>(e, "SupplierId") == supplierId);
            }

            if (property.ClrType == typeof(int?))
            {
                return await query.CountAsync(e =>
                    EF.Property<int?>(e, "SupplierId") == supplierId);
            }

            return 0;
        }

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
        }

        private static void NormalizeSupplierForSave(Supplier supplier, bool isNew)
        {
            if (isNew)
            {
                supplier.SupplierCode = NormalizeCode(supplier.SupplierCode);
            }

            supplier.SupplierName = NormalizeText(supplier.SupplierName);
            supplier.CompanyName = NormalizeText(supplier.CompanyName);
            supplier.ContactPerson = NormalizeText(supplier.ContactPerson);
            supplier.Phone1 = NormalizeText(supplier.Phone1);
            supplier.Phone2 = NormalizeText(supplier.Phone2);
            supplier.Email = NormalizeText(supplier.Email).ToLowerInvariant();
            supplier.Address = NormalizeText(supplier.Address);
            supplier.VatNumber = supplier.HasVat ? NormalizeText(supplier.VatNumber) : string.Empty;
        }

        private static string NormalizeCode(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static void ValidateSupplier(Supplier supplier, bool isNew)
        {
            if (isNew)
            {
                ValidateSupplierCode(supplier.SupplierCode);
            }

            ValidateRequiredText(supplier.SupplierName, "Supplier name", 150);
            ValidateOptionalText(supplier.CompanyName, "Company name", 150);
            ValidateOptionalText(supplier.ContactPerson, "Contact person", 50);
            ValidateRequiredPhone(supplier.Phone1, "Phone 1");
            ValidateOptionalPhone(supplier.Phone2, "Phone 2");
            ValidateOptionalEmail(supplier.Email);
            ValidateOptionalText(supplier.Address, "Address", 250);

            if (supplier.HasVat)
            {
                ValidateRequiredText(supplier.VatNumber, "VAT number", 50);
            }

            if (supplier.DefaultCreditDays < 0)
                throw new InvalidOperationException("Default credit days cannot be negative.");

            if (supplier.DefaultCreditDays > 365)
                throw new InvalidOperationException("Default credit days cannot be greater than 365.");
        }

        private static void ValidateSupplierCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Supplier code is required.");

            if (code.Length > 20)
                throw new InvalidOperationException("Supplier code cannot be longer than 20 characters.");

            if (!SupplierCodeRegex.IsMatch(code))
            {
                throw new InvalidOperationException(
                    "Supplier code can only contain letters, numbers, dash, and underscore.");
            }
        }

        private static void ValidateRequiredText(string value, string fieldName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{fieldName} is required.");

            if (value.Length > maxLength)
                throw new InvalidOperationException($"{fieldName} cannot be longer than {maxLength} characters.");
        }

        private static void ValidateOptionalText(string value, string fieldName, int maxLength)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
                throw new InvalidOperationException($"{fieldName} cannot be longer than {maxLength} characters.");
        }

        private static void ValidateRequiredPhone(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{fieldName} is required.");

            if (!PhoneRegex.IsMatch(value))
                throw new InvalidOperationException($"{fieldName} is not valid.");
        }

        private static void ValidateOptionalPhone(string value, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(value) && !PhoneRegex.IsMatch(value))
                throw new InvalidOperationException($"{fieldName} is not valid.");
        }

        private static void ValidateOptionalEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return;

            if (email.Length > 100)
                throw new InvalidOperationException("Email cannot be longer than 100 characters.");

            try
            {
                _ = new System.Net.Mail.MailAddress(email);
            }
            catch
            {
                throw new InvalidOperationException("Email address is not valid.");
            }
        }
    }
}