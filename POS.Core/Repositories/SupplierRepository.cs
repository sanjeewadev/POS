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
    public class SupplierLinkedDataSummary
    {
        public int PurchaseOrderCount { get; set; }
        public int GrnCount { get; set; }
        public int SupplierReturnCount { get; set; }
        public int SupplierLedgerCount { get; set; }
        public int ItemSupplierCount { get; set; }
        public int FreeIssueRuleCount { get; set; }
        public int FreeItemClaimCount { get; set; }

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

            builder.AppendLine($"Supplier '{supplierName}' cannot be permanently deleted because it has linked records:");
            builder.AppendLine();

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
            builder.AppendLine("Use Deactivate/Suspend instead of permanent delete.");

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

        // Used by Supplier Master grid.
        // Master page can include suspended/deactivated suppliers for old history review.
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
                query = query.Where(s => !s.IsDeactivated);

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
                    EF.Functions.Like(s.Email, $"%{term}%") ||
                    EF.Functions.Like(s.VatNumber, $"%{term}%"));
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
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            return !await context.Suppliers.AnyAsync(s =>
                EF.Functions.Collate(s.SupplierCode, caseInsensitiveCollation) == normalizedCode &&
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
            ValidateSupplierForSave(supplier);

            await using var context = await _contextFactory.CreateDbContextAsync();
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            bool codeExists = await context.Suppliers.AnyAsync(s =>
                EF.Functions.Collate(s.SupplierCode, caseInsensitiveCollation) == supplier.SupplierCode);

            if (codeExists)
                throw new InvalidOperationException($"Supplier code '{supplier.SupplierCode}' already exists.");

            DateTime now = DateTime.Now;
            supplier.CreatedAt = now;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt = supplier.IsDeactivated ? now : null;
            supplier.CurrentBalance = 0m;

            await context.Suppliers.AddAsync(supplier);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Supplier supplier)
        {
            if (supplier == null)
                throw new ArgumentNullException(nameof(supplier));

            NormalizeSupplierForSave(supplier, isNew: false);
            ValidateSupplierForSave(supplier);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == supplier.Id);

            if (existing == null)
                throw new InvalidOperationException("Supplier was not found.");

            string existingCode = NormalizeCode(existing.SupplierCode);
            string submittedCode = NormalizeCode(supplier.SupplierCode);

            if (!string.Equals(existingCode, submittedCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Supplier code cannot be changed after creation. Delete the unused supplier and create it again if the code is wrong.");
            }

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

            // CurrentBalance is ledger-controlled. Do not update it here.
            existing.IsDeactivated = supplier.IsDeactivated;
            existing.UpdatedAt = DateTime.Now;

            if (existing.IsDeactivated)
                existing.DeactivatedAt ??= existing.UpdatedAt;
            else
                existing.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task HardDeleteAsync(int supplierId)
        {
            if (supplierId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var supplier = await context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == supplierId);

                if (supplier == null)
                    return;

                var summary = await GetLinkedDataSummaryAsync(context, supplierId);

                if (summary.HasLinkedData || supplier.CurrentBalance != 0m)
                {
                    throw new InvalidOperationException(
                        summary.HasLinkedData
                            ? summary.ToUserMessage(supplier.SupplierName)
                            : "Supplier cannot be permanently deleted while current balance is not zero. Use Deactivate instead.");
                }

                context.Suppliers.Remove(supplier);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(int supplierId)
        {
            await HardDeleteAsync(supplierId);
        }

        public async Task DeactivateAsync(int supplierId)
        {
            if (supplierId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == supplierId);

            if (supplier == null)
                return;

            DateTime now = DateTime.Now;
            supplier.IsDeactivated = true;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt ??= now;

            await context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(int supplierId)
        {
            if (supplierId <= 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == supplierId);

            if (supplier == null)
                return;

            DateTime now = DateTime.Now;
            supplier.IsDeactivated = false;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        private static async Task<SupplierLinkedDataSummary> GetLinkedDataSummaryAsync(
            AppDbContext context,
            int supplierId)
        {
            var summary = new SupplierLinkedDataSummary();

            if (supplierId <= 0)
                return summary;

            summary.PurchaseOrderCount = await CountBySupplierIdAsync(context, context.PoHeaders.AsNoTracking(), supplierId);
            summary.GrnCount = await CountBySupplierIdAsync(context, context.GrnHeaders.AsNoTracking(), supplierId);
            summary.SupplierReturnCount = await CountBySupplierIdAsync(context, context.SupplierReturnHeaders.AsNoTracking(), supplierId);
            summary.SupplierLedgerCount = await CountBySupplierIdAsync(context, context.SupplierLedgers.AsNoTracking(), supplierId);
            summary.ItemSupplierCount = await CountBySupplierIdAsync(context, context.ItemSuppliers.AsNoTracking(), supplierId);
            summary.FreeIssueRuleCount = await CountBySupplierIdAsync(context, context.FreeIssueRules.AsNoTracking(), supplierId);
            summary.FreeItemClaimCount = await CountBySupplierIdAsync(context, context.FreeItemClaimLogs.AsNoTracking(), supplierId);

            return summary;
        }

        private static async Task<int> CountBySupplierIdAsync<TEntity>(
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
                    EF.Property<int?>(e, "SupplierId").HasValue &&
                    EF.Property<int?>(e, "SupplierId")!.Value == supplierId);
            }

            return 0;
        }

        private static void NormalizeSupplierForSave(Supplier supplier, bool isNew)
        {
            supplier.SupplierCode = NormalizeCode(supplier.SupplierCode);
            supplier.SupplierName = NormalizeText(supplier.SupplierName);
            supplier.CompanyName = NormalizeText(supplier.CompanyName);
            supplier.ContactPerson = NormalizeText(supplier.ContactPerson);
            supplier.Phone1 = NormalizeText(supplier.Phone1);
            supplier.Phone2 = NormalizeText(supplier.Phone2);
            supplier.Email = NormalizeText(supplier.Email).ToLowerInvariant();
            supplier.Address = NormalizeText(supplier.Address);
            supplier.VatNumber = supplier.HasVat ? NormalizeText(supplier.VatNumber).ToUpperInvariant() : string.Empty;

            if (supplier.DefaultCreditDays < 0)
                supplier.DefaultCreditDays = 0;

            if (isNew)
                supplier.CurrentBalance = 0m;
        }

        private static void ValidateSupplierForSave(Supplier supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier.SupplierCode))
                throw new InvalidOperationException("Supplier code is required.");

            if (supplier.SupplierCode.Length > 20)
                throw new InvalidOperationException("Supplier code cannot be longer than 20 characters.");

            if (!SupplierCodeRegex.IsMatch(supplier.SupplierCode))
                throw new InvalidOperationException("Supplier code can only contain letters, numbers, dash, and underscore.");

            if (string.IsNullOrWhiteSpace(supplier.SupplierName))
                throw new InvalidOperationException("Supplier name is required.");

            if (supplier.SupplierName.Length > 150)
                throw new InvalidOperationException("Supplier name cannot be longer than 150 characters.");

            if (supplier.CompanyName.Length > 150)
                throw new InvalidOperationException("Company name cannot be longer than 150 characters.");

            if (supplier.ContactPerson.Length > 50)
                throw new InvalidOperationException("Contact person cannot be longer than 50 characters.");

            if (string.IsNullOrWhiteSpace(supplier.Phone1))
                throw new InvalidOperationException("Primary phone number is required.");

            if (!PhoneRegex.IsMatch(supplier.Phone1))
                throw new InvalidOperationException("Primary phone number is invalid.");

            if (!string.IsNullOrWhiteSpace(supplier.Phone2) && !PhoneRegex.IsMatch(supplier.Phone2))
                throw new InvalidOperationException("Secondary phone number is invalid.");

            if (supplier.Email.Length > 100)
                throw new InvalidOperationException("Email cannot be longer than 100 characters.");

            if (supplier.Address.Length > 250)
                throw new InvalidOperationException("Address cannot be longer than 250 characters.");

            if (supplier.HasVat && string.IsNullOrWhiteSpace(supplier.VatNumber))
            {
                throw new InvalidOperationException(
                    "VAT registration number is required when supplier is VAT registered.");
            }

            if (supplier.VatNumber.Length > 50)
                throw new InvalidOperationException("VAT registration number cannot be longer than 50 characters.");

            if (supplier.DefaultCreditDays < 0 || supplier.DefaultCreditDays > 365)
                throw new InvalidOperationException("Default credit days must be between 0 and 365.");
        }

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
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
